#!/usr/bin/env python3
"""Extract the app and small config files from the Pokemons Play v21 bundle.

The archive contains copyrighted ROM images. This tool deliberately extracts
only an allowlist of application/configuration files and streams past every
other entry without writing it to disk.
"""

from __future__ import annotations

import argparse
import mmap
import os
import pathlib
import struct
import sys
import zlib

MARKER = b"POKEMONS_PLAY_DATA_20260921_V21"
ALLOWLIST = {
    "Pokemons Play.exe",
    "Pokemons Play.ico",
    "Pokemon - Arquivos/pt_BR/vbam.ini",
    "Pokemon 3DS - Arquivos/Azahar/scripting/citra.py",
    "Pokemon DS - Arquivos/melonDS.toml",
    "Settings/input-presets.txt",
}


class DeflateReader:
    """Read a raw-DEFLATE stream incrementally with a bounded output buffer."""

    def __init__(self, source):
        self.source = source
        self.decoder = zlib.decompressobj(wbits=-15)
        self.pending = b""
        self.output = bytearray()
        self.finished = False

    def read(self, count: int) -> bytes:
        while len(self.output) < count and not self.finished:
            if not self.pending:
                self.pending = self.source.read(64 * 1024)
            if not self.pending:
                self.finished = True
                break
            decoded = self.decoder.decompress(
                self.pending, max(64 * 1024, count - len(self.output))
            )
            self.pending = self.decoder.unconsumed_tail
            self.output.extend(decoded)
            if self.decoder.eof:
                self.finished = True
        size = min(count, len(self.output))
        result = bytes(self.output[:size])
        del self.output[:size]
        return result

    def read_exact(self, count: int) -> bytes:
        result = self.read(count)
        if len(result) != count:
            raise EOFError(f"expected {count} decompressed bytes, got {len(result)}")
        return result

    def copy_to(self, count: int, destination) -> None:
        while count:
            part = min(count, 1024 * 1024)
            destination.write(self.read_exact(part))
            count -= part

    def skip(self, count: int) -> None:
        while count:
            part = min(count, 1024 * 1024)
            self.read_exact(part)
            count -= part


def normalized_relative_path(value: str) -> pathlib.PurePosixPath:
    path = pathlib.PurePosixPath(value.replace("\\", "/"))
    if path.is_absolute() or not path.parts or any(part in ("", ".", "..") for part in path.parts):
        raise ValueError(f"unsafe bundle path: {value!r}")
    if path.parts[0].endswith(":"):
        raise ValueError(f"drive-qualified bundle path: {value!r}")
    return path


def extract(bundle: pathlib.Path, destination: pathlib.Path) -> int:
    with bundle.open("rb") as source:
        with mmap.mmap(source.fileno(), 0, access=mmap.ACCESS_READ) as mapped:
            marker_offset = mapped.find(MARKER)
        if marker_offset < 0:
            raise ValueError("bundle marker not found; this tool supports the v21 bundle only")
        source.seek(marker_offset + len(MARKER))
        archive = DeflateReader(source)
        count = struct.unpack("<i", archive.read_exact(4))[0]
        if not 0 <= count <= 100_000:
            raise ValueError(f"implausible bundle entry count: {count}")

        root = destination.resolve()
        extracted = 0
        for _ in range(count):
            name_length = struct.unpack("<i", archive.read_exact(4))[0]
            if not 1 <= name_length <= 32_768:
                raise ValueError(f"invalid path length: {name_length}")
            name = archive.read_exact(name_length).decode("utf-8")
            size = struct.unpack("<q", archive.read_exact(8))[0]
            if size < 0:
                raise ValueError(f"invalid file length for {name!r}")

            relative = normalized_relative_path(name)
            if relative.as_posix() in ALLOWLIST:
                output = root.joinpath(*relative.parts)
                if os.path.commonpath((str(root), str(output.resolve()))) != str(root):
                    raise ValueError(f"bundle path escapes destination: {name!r}")
                output.parent.mkdir(parents=True, exist_ok=True)
                with output.open("wb") as target:
                    archive.copy_to(size, target)
                print(f"extracted {size:>12} bytes: {relative.as_posix()}")
                extracted += 1
            else:
                archive.skip(size)
    return extracted


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("bundle", type=pathlib.Path)
    parser.add_argument("destination", type=pathlib.Path)
    args = parser.parse_args()
    try:
        count = extract(args.bundle, args.destination)
    except (OSError, ValueError, EOFError, zlib.error) as exc:
        print(f"extraction failed: {exc}", file=sys.stderr)
        return 1
    print(f"done; extracted {count} allowlisted application/config files")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
