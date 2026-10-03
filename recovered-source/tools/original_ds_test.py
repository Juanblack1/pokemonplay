"""Original DS keypad/framebuffer diagnostic, generated without third-party assets.

SPDX-License-Identifier: CC0-1.0
The generator and its generated original ARM program are dedicated under CC0-1.0.
Hardware constants are documented in docs/sdd/continuous-improvements/ds-validation-research.md.
"""
import struct


def original_ds_test_rom(path):
    words, literals, labels, fixes = [], [], {}, []

    def instruction(word):
        words.append(word)

    def literal(register, value, condition=14):
        index = len(words)
        words.append((condition << 28) | 0x059F0000 | (register << 12))
        fixes.append((index, len(literals), "literal"))
        literals.append(value)

    def branch(label, condition=14):
        fixes.append((len(words), label, "branch"))
        words.append((condition << 28) | 0x0A000000)

    # Configure engine A on the upper LCD; map VRAM bank A into LCDC space.
    literal(0, 0x04000304)
    literal(1, 0x8003)
    instruction(0xE1C010B0)  # strh r1,[r0]
    literal(0, 0x04000240)
    instruction(0xE3A01080)  # mov r1,#0x80
    instruction(0xE5C01000)  # strb r1,[r0]
    literal(0, 0x04000000)
    literal(1, 0x00020000)
    instruction(0xE5801000)  # str r1,[r0]
    literal(3, 0x04000130)
    labels["sample"] = len(words)
    instruction(0xE1D340B0)  # ldrh r4,[r3] -- A is active low
    instruction(0xE3140001)  # tst r4,#1
    literal(1, 0x001F, 0)   # red while A is held
    literal(1, 0x7C00, 1)   # blue otherwise
    literal(0, 0x06800000)
    instruction(0xE3A02CC0)  # mov r2,#49152 (192 rotated right by 24)
    labels["fill"] = len(words)
    instruction(0xE0C010B2)  # strh r1,[r0],#2
    instruction(0xE2522001)  # subs r2,r2,#1
    branch("fill", 1)
    branch("sample")
    code_words = len(words)
    for index, target, kind in fixes:
        if kind == "literal":
            displacement = (code_words + target) * 4 - (index * 4 + 8)
            if not 0 <= displacement <= 4095:
                raise ValueError("Literal pool is outside ARM addressing range")
            words[index] |= displacement
        else:
            words[index] |= (labels[target] - index - 2) & 0xFFFFFF
    words.extend(literals)
    arm9 = struct.pack("<" + "I" * len(words), *words).ljust(0x200, b"\0")
    arm7 = struct.pack("<I", 0xEAFFFFFE).ljust(0x200, b"\0")
    rom = bytearray(128 * 1024)
    rom[:12] = b"PP DS INPUT\0"
    rom[12:16] = b"####"
    # No logo, banner, filesystem, BIOS or commercial runtime is included.
    struct.pack_into("<4I", rom, 0x20, 0x1000, 0x02000000, 0x02000000, len(arm9))
    struct.pack_into("<4I", rom, 0x30, 0x1200, 0x03800000, 0x03800000, len(arm7))
    struct.pack_into("<2I", rom, 0x80, len(rom), 0x1000)
    crc = 0xFFFF
    for byte in rom[:0x15E]:
        crc ^= byte
        for _ in range(8):
            crc = (crc >> 1) ^ (0xA001 if crc & 1 else 0)
    struct.pack_into("<H", rom, 0x15E, crc)
    rom[0x1000:0x1200] = arm9
    rom[0x1200:0x1400] = arm7
    path.write_bytes(rom)
