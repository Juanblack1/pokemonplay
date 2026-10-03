#!/usr/bin/env python3
"""Windowless libretro ROM/input smoke check with an isolated save/system directory.

No ROM is downloaded. Omit --rom to generate an original GBA or DS input-test program.
This checks core input callbacks and rendering, not physical gamepad delivery.
"""
import argparse
import ctypes as C
import hashlib
import json
import os
from pathlib import Path
import struct
import time
import zlib
from original_ds_test import original_ds_test_rom


DS_OPTIONS = {
    "melonds_console_mode": b"ds", "melonds_sysfile_mode": b"builtin",
    "melonds_boot_mode": b"direct", "melonds_render_mode": b"software",
    "melonds_network_mode": b"disabled", "melonds_firmware_username": b"melonDS DS",
    "melonds_screen_layout1": b"top-bottom", "melonds_screen_gap": b"0",
    "melonds_secondary_screen_scale": b"100", "melonds_number_of_screen_layouts": b"1",
    "melonds_show_cursor": b"disabled",
}


def ds_frame_sample(frame, output, phase, expected):
    if frame["format"] != 1 or (frame["width"], frame["height"]) != (256, 384):
        raise RuntimeError("DS oracle requires the declared XRGB8888 256x384 layout")
    pitch, data = frame["pitch"], frame["bytes"]
    if pitch < 256 * 4 or pitch % 4 or len(data) != pitch * 384:
        raise RuntimeError("Invalid DS framebuffer stride")
    def rgb(x, y):
        value = struct.unpack_from("<I", data, y * pitch + x * 4)[0]
        return [(value >> 16) & 255, (value >> 8) & 255, value & 255]
    samples = [rgb(x, y) for x in (64, 128, 192) for y in (64, 96, 128)]
    active = 0 if expected == "red" else 2
    passed = all(pixel[active] >= 240 and all(pixel[channel] <= 16 for channel in range(3) if channel != active) for pixel in samples)
    # Save original synthetic frames without requiring Pillow/numpy on the runner.
    scanlines = b"".join(b"\0" + bytes(channel for x in range(256) for channel in rgb(x, y)) for y in range(384))
    def chunk(kind, body):
        return struct.pack(">I", len(body)) + kind + body + struct.pack(">I", zlib.crc32(kind + body) & 0xFFFFFFFF)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">2I5B", 256, 384, 8, 2, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(scanlines)) + chunk(b"IEND", b"")
    (output / (phase + ".png")).write_bytes(png)
    return {"phase": phase, "expected": expected, "samples_rgb": samples, "passed": passed}


class Variable(C.Structure):
    _fields_ = [("key", C.c_char_p), ("value", C.c_char_p)]


class OptionValue(C.Structure):
    _fields_ = [("value", C.c_char_p), ("label", C.c_char_p)]


class Option(C.Structure):
    _fields_ = [("key", C.c_char_p), ("desc", C.c_char_p), ("info", C.c_char_p),
                ("values", OptionValue * 128), ("default", C.c_char_p)]


class OptionV2(C.Structure):
    _fields_ = [(name, C.c_char_p) for name in ("key", "desc", "desc_category", "info", "info_category", "category")]
    _fields_ += [("values", OptionValue * 128), ("default", C.c_char_p)]


class OptionsV2(C.Structure):
    _fields_ = [("categories", C.c_void_p), ("definitions", C.POINTER(OptionV2))]


class OptionsIntl(C.Structure):
    _fields_ = [("us", C.POINTER(Option)), ("local", C.POINTER(Option))]


class OptionsIntlV2(C.Structure):
    _fields_ = [("us", C.POINTER(OptionsV2)), ("local", C.POINTER(OptionsV2))]


class SystemInfo(C.Structure):
    _fields_ = [("name", C.c_char_p), ("version", C.c_char_p), ("extensions", C.c_char_p),
                ("fullpath", C.c_bool), ("block_extract", C.c_bool)]


class GameInfo(C.Structure):
    _fields_ = [("path", C.c_char_p), ("data", C.c_void_p), ("size", C.c_size_t), ("meta", C.c_char_p)]


def original_test_rom(path):
    rom = bytearray(32768)
    struct.pack_into("<I", rom, 0, 0xEA00002E)  # ARM branch to our program at 0xC0.
    rom[0xA0:0xAC] = b"PP INPUTTEST"
    rom[0xAC:0xB0] = b"PPT0"
    rom[0xB2] = 0x96
    rom[0xBD] = (-sum(rom[0xA0:0xBD]) - 0x19) & 255
    # Mode 3 framebuffer; the first pixel is red while GBA A is held, blue otherwise.
    program = [0xE59F0028, 0xE59F1028, 0xE1C010B0, 0xE59F2024, 0xE59F3024,
               0xE1D340B0, 0xE3140001, 0x059F501C, 0x159F501C, 0xE1C250B0,
               0xEAFFFFF9, 0xE1A00000, 0x04000000, 0x00000403, 0x06000000,
               0x04000130, 0x001F, 0x7C00]
    struct.pack_into("<" + "I" * len(program), rom, 0xC0, *program)
    path.write_bytes(rom)


def probe(args):
    output = Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    isolated = output / "isolated-core-data"
    isolated.mkdir(exist_ok=True)
    rom = Path(args.rom).resolve() if args.rom else output / ("original-input-test." + ("nds" if args.system == "ds" else "gba"))
    if not args.rom:
        (original_ds_test_rom if args.system == "ds" else original_test_rom)(rom)
    if not rom.is_file():
        raise FileNotFoundError(rom)
    core_path = Path(args.core).resolve()
    directory_handles = []
    if os.name == "nt":
        for directory in (core_path.parent, core_path.parent.parent):
            directory_handles.append(os.add_dll_directory(str(directory)))
    core = C.CDLL(str(core_path))
    options, buffers, native_logs, configuration_errors = {}, {}, [], []
    stats = {"video_calls": 0, "input_queries": 0, "positive_input_queries": 0, "audio_frames": 0}
    frame = {"bytes": None, "width": 0, "height": 0, "pitch": 0, "format": 0}
    pressed = set()

    def set_option(key, default, choices):
        key = key.decode()
        value = default
        if "renderer" in key or "render_mode" in key:
            value = next((v for v in choices if v.lower() == b"software"), default)
        if key in ("mgba_use_bios", "mgba_skip_bios"):
            value = b"OFF" if key == "mgba_use_bios" else b"ON"
        if args.system == "ds" and key in DS_OPTIONS:
            value = DS_OPTIONS[key]
            if value not in choices:
                configuration_errors.append("Unsupported DS option value: " + key)
        options[key] = value
        buffers[key] = C.create_string_buffer(value or b"")

    log_callback_type = C.CFUNCTYPE(None, C.c_int, C.c_char_p)
    @log_callback_type
    def native_log(level, text):
        if text and len(native_logs) < 60:
            native_logs.append({"level": level, "format": text.decode(errors="replace")})

    path_buffer = C.create_string_buffer(os.fsencode(isolated))
    environment_type = C.CFUNCTYPE(C.c_bool, C.c_uint, C.c_void_p)
    @environment_type
    def environment(command, data):
        command &= 0xFFFF
        if command in (9, 31):
            C.cast(data, C.POINTER(C.c_void_p))[0] = C.cast(path_buffer, C.c_void_p).value
            return True
        if command == 10:
            frame["format"] = C.cast(data, C.POINTER(C.c_int))[0]
            return frame["format"] in (0, 1, 2)
        if command == 15:
            variable = C.cast(data, C.POINTER(Variable)).contents
            key = variable.key.decode()
            variable.value = C.cast(buffers[key], C.c_char_p) if key in buffers else None
            return key in buffers
        if command == 16:
            definitions = C.cast(data, C.POINTER(Variable))
            for index in range(512):
                definition = definitions[index]
                if not definition.key:
                    break
                choices = definition.value.split(b";", 1)[1].strip().split(b"|")
                set_option(definition.key, choices[0], choices)
            return True
        if command in (53, 54, 67, 68):
            if command==53:definitions=C.cast(data,C.POINTER(Option))
            elif command==54:definitions=C.cast(data,C.POINTER(OptionsIntl)).contents.us
            elif command==67:definitions=C.cast(data,C.POINTER(OptionsV2)).contents.definitions
            else:definitions=C.cast(data,C.POINTER(OptionsIntlV2)).contents.us.contents.definitions
            for index in range(512):
                definition = definitions[index]
                if not definition.key:
                    break
                choices = [v.value for v in definition.values if v.value]
                set_option(definition.key, definition.default, choices)
            return True
        if command == 52:
            C.cast(data, C.POINTER(C.c_uint))[0] = 2
            return True
        if command == 17:
            C.cast(data, C.POINTER(C.c_bool))[0] = False
            return True
        if command == 27:
            C.cast(data, C.POINTER(C.c_void_p))[0] = C.cast(native_log, C.c_void_p).value
            return True
        if command in (39, 61):
            C.cast(data, C.POINTER(C.c_uint))[0] = 0 if command == 39 else 1
            return True
        if command == 51:
            return True  # Input callback implements RetroPad bitmasks as well as individual buttons.
        # Accept informational descriptors; unsupported callbacks remain unavailable.
        return command in (11, 18, 35, 44, 62, 63)

    video_type = C.CFUNCTYPE(None, C.c_void_p, C.c_uint, C.c_uint, C.c_size_t)
    @video_type
    def video(data, width, height, pitch):
        if data and data != C.c_void_p(-1).value and pitch * height <= 16 * 1024 * 1024:
            frame.update(bytes=C.string_at(data, pitch * height), width=width, height=height, pitch=pitch)
            stats["video_calls"] += 1

    audio_type = C.CFUNCTYPE(None, C.c_int16, C.c_int16)
    @audio_type
    def audio(left, right):
        stats["audio_frames"] += 1

    audio_batch_type = C.CFUNCTYPE(C.c_size_t, C.c_void_p, C.c_size_t)
    @audio_batch_type
    def audio_batch(data, count):
        stats["audio_frames"] += count
        return count

    poll_type = C.CFUNCTYPE(None)
    @poll_type
    def poll():
        pass

    input_type = C.CFUNCTYPE(C.c_int16, C.c_uint, C.c_uint, C.c_uint, C.c_uint)
    @input_type
    def input_state(port, device, index, button):
        stats["input_queries"] += 1
        value = 0
        if port == 0 and device == 1 and not args.suppress_input:
            value = sum(1 << b for b in pressed) if button == 256 else int(button in pressed)
        if value:
            stats["positive_input_queries"] += 1
        return value

    for name, callback in (("environment", environment), ("video_refresh", video),
                           ("audio_sample", audio), ("audio_sample_batch", audio_batch),
                           ("input_poll", poll), ("input_state", input_state)):
        function = getattr(core, "retro_set_" + name)
        function.argtypes = [type(callback)]
        function(callback)
    core.retro_get_system_info.argtypes = [C.POINTER(SystemInfo)]
    info = SystemInfo()
    core.retro_get_system_info(C.byref(info))
    core.retro_load_game.argtypes = [C.POINTER(GameInfo)]
    core.retro_load_game.restype = C.c_bool
    core.retro_serialize_size.restype = C.c_size_t
    for name in ("retro_serialize", "retro_unserialize"):
        function = getattr(core, name)
        function.argtypes = [C.c_void_p, C.c_size_t]
        function.restype = C.c_bool
    core.retro_set_controller_port_device.argtypes = [C.c_uint, C.c_uint]
    rom_buffer = None if info.fullpath else C.create_string_buffer(rom.read_bytes())
    game = GameInfo(os.fsencode(rom), C.cast(rom_buffer, C.c_void_p) if rom_buffer else None,
                    rom.stat().st_size if rom_buffer else 0, None)
    started = time.monotonic()
    loaded = False
    core.retro_init()
    try:
        if args.system == "ds":
            missing = [key for key in DS_OPTIONS if key not in options]
            if missing or configuration_errors:
                raise RuntimeError("DS configuration rejected: " + "; ".join(missing + configuration_errors))
        loaded = core.retro_load_game(C.byref(game))
        if not loaded:
            raise RuntimeError("Core rejected ROM; see native log formats in evidence.")
        core.retro_set_controller_port_device(0, 1)
        def run(count, buttons):
            pressed.clear()
            pressed.update(buttons)
            for _ in range(count):
                if time.monotonic() - started > 120:
                    raise TimeoutError("Bounded core probe exceeded 120 seconds.")
                core.retro_run()
            if frame["bytes"] is None:
                raise RuntimeError("No software framebuffer was delivered.")
            return hashlib.sha256(frame["bytes"]).hexdigest()

        seed_hash = run(args.frames, [])
        comparison = {"supported": False}
        guest_input = []
        size = core.retro_serialize_size() if args.system == "gba" else 0
        if args.system == "ds":
            guest_input.append(ds_frame_sample(frame, output, "released-before", "blue"))
            run(args.frames, [8])
            guest_input.append(ds_frame_sample(frame, output, "a-held", "red"))
            run(args.frames, [])
            guest_input.append(ds_frame_sample(frame, output, "released-after", "blue"))
            if not all(phase["passed"] for phase in guest_input):
                raise RuntimeError("DS guest framebuffer did not follow released/A/released input")
        elif 0 < size <= 128 * 1024 * 1024:
            saved = C.create_string_buffer(size)
            if core.retro_serialize(saved, size):
                first = run(180, [])
                if not core.retro_unserialize(saved, size):
                    raise RuntimeError("Core failed to restore diagnostic state.")
                second = run(180, [])
                if not core.retro_unserialize(saved, size):
                    raise RuntimeError("Core failed to restore diagnostic state.")
                active = run(180, [3, 8])  # Start and A in the libretro RetroPad API.
                comparison = {"supported": True, "baseline_repeatable": first == second,
                              "input_changes_frame": first == second and active != first,
                              "baseline_hash": first, "pressed_hash": active}
        else:
            run(180, [3, 8])
        if not stats["video_calls"] or not stats["positive_input_queries"]:
            raise RuntimeError("Core did not deliver rendering and consume supplied controller input.")
        if args.system == "gba" and not args.rom and not comparison.get("input_changes_frame"):
            raise RuntimeError("Original input-test ROM did not respond to controller input.")
        try:
            import numpy as np
            from PIL import Image
            pixel_bytes = 4 if frame["format"] == 1 else 2
            pixels = np.frombuffer(frame["bytes"], dtype="<u4" if pixel_bytes == 4 else "<u2").reshape(frame["height"], frame["pitch"] // pixel_bytes)[:, :frame["width"]]
            if pixel_bytes == 4:
                rgb = np.stack(((pixels >> 16) & 255, (pixels >> 8) & 255, pixels & 255), axis=-1)
            else:
                red_shift, green_bits = (11, 6) if frame["format"] == 2 else (10, 5)
                rgb = np.stack((((pixels >> red_shift) & 31) * 255 // 31,
                                ((pixels >> 5) & ((1 << green_bits) - 1)) * 255 // ((1 << green_bits) - 1),
                                (pixels & 31) * 255 // 31), axis=-1)
            Image.fromarray(rgb.astype("uint8")).save(output / "last-frame.png")
        except ImportError:
            pass
        return {"result": "passed", "core": info.name.decode(), "core_version": info.version.decode(),
                "rom_name": rom.name, "original_diagnostic_rom": not bool(args.rom),
                "rom_sha256": hashlib.sha256(rom.read_bytes()).hexdigest(), "seed_frame_hash": seed_hash,
                "core_sha256": hashlib.sha256(core_path.read_bytes()).hexdigest(),
                "generator_sha256": hashlib.sha256(Path(__file__).with_name("original_ds_test.py").read_bytes()).hexdigest() if args.system == "ds" else None,
                "width": frame["width"], "height": frame["height"], "frames": args.frames,
                "seconds": time.monotonic() - started, "stats": stats, "state_comparison": comparison,
                "guest_input": guest_input,
                "options": {key: (value or b"").decode() for key, value in options.items()}, "native_log_formats": native_logs,
                "scope": "Core loading, software rendering and supplied input; physical controller and launcher embedding were not exercised."}
    except Exception as error:
        (output / "native-failure.json").write_text(json.dumps({
            "result": "failed", "error": str(error), "stats": stats,
            "guest_input": locals().get("guest_input", []),
            "options": {key: (value or b"").decode() for key, value in options.items()},
            "native_log_formats": native_logs}, indent=2), encoding="utf-8")
        raise
    finally:
        if loaded:
            core.retro_unload_game()
        core.retro_deinit()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--core", required=True)
    parser.add_argument("--rom")
    parser.add_argument("--system", choices=("gba", "ds"), default="gba")
    parser.add_argument("--suppress-input", action="store_true", help="Negative control: withhold all guest input")
    parser.add_argument("--output", required=True)
    parser.add_argument("--frames", type=int, default=1800)
    args = parser.parse_args()
    if args.system == "ds" and args.rom:
        parser.error("DS guest input verification uses only the original generated diagnostic ROM")
    if not 1 <= args.frames <= 10000:
        parser.error("--frames must be between 1 and 10000")
    try:
        result = probe(args)
    except Exception as error:
        Path(args.output).mkdir(parents=True, exist_ok=True)
        Path(args.output, "result.json").write_text(json.dumps({
            "result": "failed", "error": str(error)}, indent=2), encoding="utf-8")
        raise
    Path(args.output, "result.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps({key: result[key] for key in ("result", "core", "width", "height", "seconds", "stats", "state_comparison")}, indent=2))
