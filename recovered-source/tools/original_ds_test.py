"""Original DS keypad diagnostic with twelve independent framebuffer regions.

SPDX-License-Identifier: CC0-1.0
The generator and original ARM9/ARM7 programs are dedicated under CC0-1.0.
Hardware constants and the shared-memory bridge are documented in
docs/sdd/continuous-improvements/ds-keypad-research.md.
"""
import struct


class ArmProgram:
    def __init__(self):
        self.words, self.literals, self.labels, self.fixes = [], [], {}, []

    def emit(self, word):
        self.words.append(word)

    def label(self, name):
        self.labels[name] = len(self.words)

    def literal(self, register, value, condition=14):
        self.fixes.append((len(self.words), len(self.literals), "literal"))
        self.words.append((condition << 28) | 0x059F0000 | (register << 12))
        self.literals.append(value)

    def branch(self, label, condition=14):
        self.fixes.append((len(self.words), label, "branch"))
        self.words.append((condition << 28) | 0x0A000000)

    def build(self, address, regions=()):
        words = list(self.words)
        for index, target, kind in self.fixes:
            if kind == "literal":
                displacement = (len(words) + target) * 4 - (index * 4 + 8)
                if not 0 <= displacement <= 4095:
                    raise ValueError("Literal pool is outside ARM addressing range")
                words[index] |= displacement
            else:
                displacement = self.labels[target] - index - 2
                if not -(1 << 23) <= displacement < (1 << 23):
                    raise ValueError("Branch is outside ARM addressing range")
                words[index] |= displacement & 0xFFFFFF
        table_address = address + (len(words) + len(self.literals)) * 4
        words.extend(table_address if value == "regions" else value for value in self.literals)
        words.extend(regions)
        payload = struct.pack("<" + "I" * len(words), *words)
        if len(payload) > 0x200:
            raise ValueError("Original ARM payload exceeds its reserved ROM segment")
        return payload.ljust(0x200, b"\0")


def arm_immediate(value):
    for rotation in range(16):
        bits = rotation * 2
        candidate = ((value << bits) | (value >> (32 - bits))) & 0xFFFFFFFF
        if candidate <= 255:
            return (rotation << 8) | candidate
    raise ValueError("Value is not an ARM rotated immediate")


def original_ds_test_rom(path):
    arm9 = ArmProgram()
    # Engine A on the upper LCD, VRAM A in LCDC mode, direct framebuffer.
    arm9.literal(0, 0x04000304)
    arm9.literal(1, 0x8003)
    arm9.emit(0xE1C010B0)  # strh r1,[r0]
    arm9.literal(0, 0x04000240)
    arm9.emit(0xE3A01080)
    arm9.emit(0xE5C01000)
    arm9.literal(0, 0x04000000)
    arm9.literal(1, 0x00020000)
    arm9.emit(0xE5801000)
    arm9.literal(3, 0x04000130)  # KEYINPUT, ten low buttons
    arm9.literal(8, 0x02003000)  # shared EXTKEYIN snapshot, ready at +2
    arm9.literal(10, 0x5044)
    arm9.literal(11, 0x3FF)
    arm9.label("sample")
    arm9.emit(0xE1D890B2)  # ldrh r9,[r8,#2]
    arm9.emit(0xE159000A)  # cmp r9,r10
    arm9.branch("sample", 1)  # no pixels until ARM7 publishes ready
    arm9.emit(0xE1D340B0)  # ldrh r4,[r3]
    arm9.emit(0xE004400B)  # and r4,r4,r11
    arm9.emit(0xE1D850B0)  # ldrh r5,[r8]
    arm9.emit(0xE2055003)  # and r5,r5,#3 (X/Y only, excluding lid/touch)
    arm9.emit(0xE1844505)  # orr r4,r4,r5,lsl #10
    arm9.literal(7, "regions")
    arm9.emit(0xE3A06001)  # bit mask starts at A
    arm9.emit(0xE3A0C00C)  # twelve regions
    arm9.label("region")
    arm9.emit(0xE1140006)  # tst r4,r6 -- active low
    arm9.literal(1, 0x001F, 0)
    arm9.literal(1, 0x7C00, 1)
    arm9.emit(0xE4970004)  # ldr r0,[r7],#4
    arm9.emit(0xE3A05010)  # sixteen rows
    arm9.label("row")
    arm9.emit(0xE3A02010)  # sixteen pixels
    arm9.label("pixel")
    arm9.emit(0xE0C010B2)  # strh r1,[r0],#2
    arm9.emit(0xE2522001)
    arm9.branch("pixel", 1)
    arm9.emit(0xE2800000 | arm_immediate(480))  # next row: stride512 - width32
    arm9.emit(0xE2555001)
    arm9.branch("row", 1)
    arm9.emit(0xE1A06086)  # mov r6,r6,lsl #1
    arm9.emit(0xE25CC001)
    arm9.branch("region", 1)
    arm9.branch("sample")
    regions = [0x06800000 + 2 * ((16 + 56 * (bit % 4)) + 256 * (16 + 56 * (bit // 4))) for bit in range(12)]
    arm9_payload = arm9.build(0x02000000, regions)

    arm7 = ArmProgram()
    arm7.literal(0, 0x04000136)  # ARM7 EXTKEYIN
    arm7.literal(1, 0x02003000)  # main RAM, one writer
    arm7.literal(3, 0x5044)
    arm7.label("sample")
    arm7.emit(0xE1D020B0)  # ldrh r2,[r0]
    arm7.emit(0xE1C120B0)  # strh r2,[r1]
    arm7.emit(0xE1C130B2)  # strh r3,[r1,#2] -- publish ready after snapshot
    arm7.branch("sample")
    arm7_payload = arm7.build(0x03800000)

    rom = bytearray(128 * 1024)
    rom[:12] = b"PP DS KEYS\0\0"
    rom[12:16] = b"####"
    # No logo, banner, filesystem, BIOS or commercial runtime is included.
    struct.pack_into("<4I", rom, 0x20, 0x1000, 0x02000000, 0x02000000, len(arm9_payload))
    struct.pack_into("<4I", rom, 0x30, 0x1200, 0x03800000, 0x03800000, len(arm7_payload))
    struct.pack_into("<2I", rom, 0x80, len(rom), 0x1000)
    crc = 0xFFFF
    for byte in rom[:0x15E]:
        crc ^= byte
        for _ in range(8):
            crc = (crc >> 1) ^ (0xA001 if crc & 1 else 0)
    struct.pack_into("<H", rom, 0x15E, crc)
    rom[0x1000:0x1200] = arm9_payload
    rom[0x1200:0x1400] = arm7_payload
    path.write_bytes(rom)
