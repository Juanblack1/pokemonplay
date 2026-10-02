#!/usr/bin/env python3
"""Rebuild the original self-extracting launcher without extracting its payload."""

from __future__ import annotations

import argparse
import hashlib
import os
from pathlib import Path
import re
import struct
import sys
import zlib

MARKER = b"POKEMONS_PLAY_DATA_20261001_V113"
SOURCE_MARKER_PREFIX = b"POKEMONS_PLAY_DATA_"
MAIN_NAME = "Pokemons Play.exe"
RUNTIME_DIR = "PokemonPlayRuntime"
CHUNK = 1024 * 1024


class DeflateReader:
    def __init__(self, stream, offset: int):
        stream.seek(offset)
        self.stream = stream
        self.decoder = zlib.decompressobj(-15)
        self.pending = b""
        self.buffer = bytearray()
        self.finished = False

    def _fill(self, needed: int) -> None:
        while len(self.buffer) < needed and not self.finished:
            if self.pending:
                compressed = self.pending
            else:
                compressed = self.stream.read(CHUNK)
                if not compressed:
                    self.buffer.extend(self.decoder.flush())
                    self.finished = True
                    break
            part = self.decoder.decompress(compressed, max(CHUNK, needed - len(self.buffer)))
            self.buffer.extend(part)
            self.pending = self.decoder.unconsumed_tail
            if self.decoder.eof:
                self.finished = True

    def read(self, count: int) -> bytes:
        if count < 0:
            raise ValueError("negative read size")
        self._fill(count)
        if len(self.buffer) < count:
            raise EOFError(f"expected {count} bytes, got {len(self.buffer)}")
        result = bytes(self.buffer[:count])
        del self.buffer[:count]
        return result

    def read_i32(self) -> int:
        return struct.unpack("<i", self.read(4))[0]

    def read_i64(self) -> int:
        return struct.unpack("<q", self.read(8))[0]

    def copy_to(self, count: int, emit) -> None:
        while count:
            size = min(CHUNK, count)
            emit(self.read(size))
            count -= size


class DeflateWriter:
    def __init__(self, stream):
        self.stream = stream
        self.encoder = zlib.compressobj(level=6, wbits=-15)

    def write(self, data: bytes) -> None:
        compressed = self.encoder.compress(data)
        if compressed:
            self.stream.write(compressed)

    def finish(self) -> None:
        self.stream.write(self.encoder.flush(zlib.Z_FINISH))


def marker_offset(path: Path) -> int:
    overlap = b""
    with path.open("rb") as source:
        while chunk := source.read(CHUNK):
            data = overlap + chunk
            index = data.find(SOURCE_MARKER_PREFIX)
            if index >= 0:
                return source.tell() - len(chunk) - len(overlap) + index
            overlap = data[-(len(SOURCE_MARKER_PREFIX) - 1):]
    raise ValueError(f"bundle marker not found in {path}")


def marker_end(path: Path, offset: int) -> int:
    with path.open("rb") as source:
        source.seek(offset)
        candidate = source.read(128)
    match = re.match(rb"POKEMONS_PLAY_DATA_[0-9]{8}_V[0-9]+", candidate)
    if not match:
        raise ValueError(f"invalid bundle marker in {path}")
    return offset + match.end()


def parse_header(reader: DeflateReader) -> tuple[int, bytes, int]:
    name_size = reader.read_i32()
    if not 0 < name_size <= 1024 * 1024:
        raise ValueError(f"invalid path length: {name_size}")
    name = reader.read(name_size)
    size = reader.read_i64()
    if size < 0:
        raise ValueError(f"invalid payload size for {name!r}")
    return name_size, name, size


def entries(path: Path):
    offset = marker_offset(path)
    with path.open("rb") as source:
        reader = DeflateReader(source, marker_end(path, offset))
        count = reader.read_i32()
        if not 0 < count < 100_000:
            raise ValueError(f"invalid bundle entry count: {count}")
        for _ in range(count):
            _, raw_name, size = parse_header(reader)
            name = raw_name.decode("utf-8")
            yield name, size, reader


def publish_files(folder: Path) -> list[tuple[str, Path]]:
    files = []
    for file in sorted(folder.rglob("*")):
        if not file.is_file() or file.suffix.lower() == ".pdb":
            continue
        rel = file.relative_to(folder).as_posix()
        files.append((rel, file))
    if not any(name == MAIN_NAME for name, _ in files):
        raise ValueError(f"publish folder must contain {MAIN_NAME}")
    return files


def inspect(source: Path) -> None:
    count = 0
    total = 0
    matches = []
    ext_sizes: dict[str, int] = {}
    for name, size, reader in entries(source):
        count += 1
        total += size
        suffix = Path(name).suffix.lower() or "(none)"
        ext_sizes[suffix] = ext_sizes.get(suffix, 0) + size
        if name.lower() == MAIN_NAME.lower() or Path(name).name.lower() in {"pokemonmenu.v21.exe", "pokemonmenu.v21.dll"}:
            matches.append((name, size))
        reader.copy_to(size, lambda _: None)
    print(f"Entries: {count}; payload bytes: {total:,}")
    print("Launcher matches:", matches)
    print("Top payload extensions:", sorted(ext_sizes.items(), key=lambda item: item[1], reverse=True)[:12])


def repack(source: Path, stub: Path, publish: Path, destination: Path) -> None:
    payload = publish_files(publish)
    added = [(f"{RUNTIME_DIR}\\{name.replace('/', chr(92))}", file) for name, file in payload]
    retained_count = 0
    for name, size, reader in entries(source):
        if not name.replace("/", "\\").lower().startswith(RUNTIME_DIR.lower() + "\\"):
            retained_count += 1
        reader.copy_to(size, lambda _: None)
    offset = marker_offset(source)
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary = destination.with_suffix(destination.suffix + ".building")
    try:
        with source.open("rb") as original, stub.open("rb") as stub_file, temporary.open("wb") as output:
            while prefix := stub_file.read(CHUNK):
                output.write(prefix)
            output.write(MARKER)
            writer = DeflateWriter(output)

            def write_record(name_bytes: bytes, size: int) -> None:
                writer.write(struct.pack("<i", len(name_bytes)))
                writer.write(name_bytes)
                writer.write(struct.pack("<q", size))

            reader = DeflateReader(original, marker_end(source, offset))
            entry_count = reader.read_i32()
            new_main = next(file for name, file in payload if name.lower() == MAIN_NAME.lower())
            all_count = retained_count + len(added)
            writer.write(struct.pack("<i", all_count))

            for index in range(entry_count):
                _, raw_name, size = parse_header(reader)
                if raw_name.decode("utf-8").replace("/", "\\").lower().startswith(RUNTIME_DIR.lower() + "\\"):
                    reader.copy_to(size, lambda _: None)
                else:
                    write_record(raw_name, size)
                    reader.copy_to(size, writer.write)
                if (index + 1) % 25 == 0:
                    print(f"Repacked original payload entries: {index + 1}/{entry_count}", flush=True)

            for name, file in added:
                raw_name = name.encode("utf-8")
                write_record(raw_name, file.stat().st_size)
                with file.open("rb") as stream:
                    while chunk := stream.read(CHUNK):
                        writer.write(chunk)
            writer.finish()
            output.flush()
            os.fsync(output.fileno())

        verify(destination=temporary, expected_main=new_main)
        os.replace(temporary, destination)
        print(f"Updated launcher written and verified: {destination}")
        print(f"Size: {destination.stat().st_size:,} bytes")
    except Exception:
        temporary.unlink(missing_ok=True)
        raise


def verify(destination: Path, expected_main: Path) -> None:
    expected_hash = hashlib.sha256()
    with expected_main.open("rb") as stream:
        while chunk := stream.read(CHUNK):
            expected_hash.update(chunk)
    found = False
    count = 0
    expected_name = f"{RUNTIME_DIR}\\{MAIN_NAME}".lower()
    for name, size, reader in entries(destination):
        if name.lower() == expected_name:
            if size != expected_main.stat().st_size:
                raise ValueError("embedded launcher size mismatch")
            actual_hash = hashlib.sha256()
            remaining = size
            while remaining:
                chunk = reader.read(min(CHUNK, remaining))
                actual_hash.update(chunk)
                remaining -= len(chunk)
            if actual_hash.digest() != expected_hash.digest():
                raise ValueError("embedded launcher hash mismatch")
            found = True
        else:
            reader.copy_to(size, lambda _: None)
        count += 1
    if not found:
        raise ValueError("updated bundle does not contain its launcher")
    print(f"Verified bundle entries: {count}; embedded launcher SHA-256 matches")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True, help="Original self-extracting EXE")
    parser.add_argument("--inspect", action="store_true", help="Inspect manifest without extracting files")
    parser.add_argument("--stub", type=Path, help="New self-extracting stub executable")
    parser.add_argument("--publish", type=Path, help="Folder from self-contained dotnet publish")
    parser.add_argument("--output", type=Path, help="Path for the rebuilt launcher")
    args = parser.parse_args()
    try:
        if args.inspect:
            inspect(args.source)
            return 0
        if not args.stub or not args.publish or not args.output:
            parser.error("--stub, --publish, and --output are required unless --inspect is used")
        repack(args.source, args.stub, args.publish, args.output)
        return 0
    except Exception as error:
        print(f"repack failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
