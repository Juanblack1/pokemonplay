"""CC0-1.0: original generator, ARM payload and pure RGB oracle (no external assets).

240x160 Mode4 double-buffer: ten 32x32 rectangles, bit order A,B,Select,Start,Right,
Left,Up,Down,R,L. 32 counter bits (LSB first) in two rows at y=112/126; yellow 4px border.
Magic rectangles at y=100 carry yellow/white/blue/red. Palette indexes 0..4
are black/blue/red/yellow/white. Published buffer switches only at VBlank.
The renderer must finish within one frame; that timing requires CI execution.
Counter increments once per published polled VBlank rising edge; no IRQ/SWI/runtime.
Sources (constants only; no upstream program is copied):
https://github.com/devkitPro/libgba/blob/master/include/gba_video.h
https://github.com/devkitPro/libgba/blob/master/include/gba_input.h
https://github.com/devkitPro/libgba/blob/master/include/gba_types.h
Pure tests do not demonstrate guest execution, frontend composition or hardware.
"""
import struct

WIDTH, HEIGHT = 240, 160
BUTTONS = ('A', 'B', 'Select', 'Start', 'Right', 'Left', 'Up', 'Down', 'R', 'L')
BUTTON_RECTS = tuple((12 + 44 * (i % 5), 12 + 54 * (i // 5), 32, 32) for i in range(10))
COUNTER_RECTS = tuple((8 + 14 * (i % 16), 112 + 14 * (i // 16), 10, 8) for i in range(32))
MAGIC_RECTS = tuple((92 + 14 * i, 100, 10, 8) for i in range(4))
MAGIC_RGB = ((255,255,0), (255,255,255), (0,0,255), (255,0,0))
BORDER_RECTS = ((0,0,240,4), (0,156,240,4), (0,4,4,152), (236,4,4,152))
ROM_SIZE, PAYLOAD_OFFSET = 32768, 0xC0


class _Arm:
    def __init__(self):
        self.words, self.pool, self.labels, self.fix = [], [], {}, []

    def emit(self, word):
        self.words.append(word)

    def literal(self, reg, value, cond=14):
        self.fix.append((len(self.words), len(self.pool), 'literal'))
        self.emit((cond << 28) | 0x059F0000 | reg << 12)
        self.pool.append(value)

    def label(self, name):
        self.labels[name] = len(self.words)

    def branch(self, target, cond=14):
        self.fix.append((len(self.words), target, 'branch'))
        self.emit((cond << 28) | 0x0A000000)

    def build(self):
        words = self.words.copy()
        for i, target, kind in self.fix:
            if kind == 'literal':
                offset = 4 * (len(words) + target - i) - 8
                if not 0 <= offset <= 4095:
                    raise ValueError('ARM literal out of range')
                words[i] |= offset
            else:
                offset = self.labels[target] - i - 2
                if not -(1 << 23) <= offset < (1 << 23):
                    raise ValueError('ARM branch out of range')
                words[i] |= offset & 0xFFFFFF
        return struct.pack('<' + 'I' * (len(words) + len(self.pool)), *(words + self.pool))


def _imm(value):
    for rotation in range(16):
        shift = rotation * 2
        candidate = ((value << shift) | (value >> ((32 - shift) % 32))) & 0xFFFFFFFF
        if candidate <= 255:
            return rotation << 8 | candidate
    raise ValueError('ARM immediate out of range')


def _payload():
    p = _Arm()
    p.literal(6, 0x04000000)
    p.literal(7, 0x404)  # Mode4 + BG2, page0 initially displayed
    p.emit(0xE1C670B0)
    # Explicit Mode4 affine identity; no dependency on BIOS matrix initialization.
    for address, value in ((0x04000020,0x100),(0x04000022,0),
                           (0x04000024,0),(0x04000026,0x100),
                           (0x04000028,0),(0x0400002C,0)):
        p.literal(0, address)
        p.literal(1, value)
        p.emit(0xE5801000 if address in (0x04000028,0x0400002C) else 0xE1C010B0)
    p.literal(0, 0x05000000)
    for color in (0, 0x7C00, 0x001F, 0x03FF, 0x7FFF):
        p.literal(1, color)
        p.emit(0xE0C010B2)
    p.literal(8, 0x04000006)
    p.literal(9, 0x04000130)
    p.literal(11, 0x0600A000)  # initially hidden page1
    p.emit(0xE3A0A000)

    def rectangle(rect, name):
        x, y, width, height = rect
        p.literal(0, x + 240 * y)
        p.emit(0xE08B0000)  # add r0,r11,r0 (hidden page base)
        p.emit(0xE3A05000 | _imm(height))
        p.label(name + 'row')
        p.emit(0xE3A02000 | _imm(width // 2))
        p.label(name + 'pixel')
        p.emit(0xE0C010B2)  # two identical indexed pixels per halfword
        p.emit(0xE2522001)
        p.branch(name + 'pixel', 1)
        p.emit(0xE2800000 | _imm(240 - width))
        p.emit(0xE2555001)
        p.branch(name + 'row', 1)

    p.emit(0xE3A0C002)  # initialize static border/background/magic on both pages
    p.label('initialize_pages')
    p.literal(1, 0x0303)
    rectangle((0,0,240,160), 'border')
    p.literal(1, 0)
    rectangle((4,4,232,152), 'background')
    for i, rect in enumerate(MAGIC_RECTS):
        p.literal(1, (3,4,1,2)[i] * 0x101)
        rectangle(rect, 'magic' + str(i))
    p.emit(0xE22BB000 | _imm(0xA000))
    p.emit(0xE25CC001)
    p.branch('initialize_pages', 1)
    p.label('render')
    p.emit(0xE28AA001)
    p.emit(0xE1D940B0)
    for i, rect in enumerate(BUTTON_RECTS):
        p.emit(0xE3140000 | _imm(1 << i))
        p.literal(1, 0x0202, 0)
        p.literal(1, 0x0101, 1)
        rectangle(rect, 'button' + str(i))
    for i, rect in enumerate(COUNTER_RECTS):
        p.emit(0xE31A0000 | _imm(1 << i))
        p.literal(1, 0x0404, 1)
        p.literal(1, 0, 0)
        rectangle(rect, 'counter' + str(i))
    # Never flip during active scanout. If rendering overran, wait for a new edge.
    p.label('leave_vblank')
    p.emit(0xE1D820B0)
    p.emit(0xE35200A0)
    p.branch('leave_vblank', 2)
    p.label('enter_vblank')
    p.emit(0xE1D820B0)
    p.emit(0xE35200A0)
    p.branch('enter_vblank', 3)
    p.emit(0xE2277010)  # toggle DISPCNT displayed page
    p.emit(0xE1C670B0)  # atomic publication: mask/counter/magic together
    p.emit(0xE22BB000 | _imm(0xA000))  # next hidden page
    p.branch('render')
    return p.build()


def rom_bytes():
    payload = _payload()
    if PAYLOAD_OFFSET + len(payload) > ROM_SIZE:
        raise ValueError('Payload exceeds ROM')
    rom = bytearray(ROM_SIZE)
    struct.pack_into('<I', rom, 0, 0xEA00002E)  # entry branch to0xC0
    rom[0xA0:0xAC] = b'PP FRONTEND\0'
    rom[0xAC:0xB0] = b'PPF0'
    rom[0xB2] = 0x96
    rom[0xBD] = (-sum(rom[0xA0:0xBD]) - 0x19) & 255
    rom[PAYLOAD_OFFSET:PAYLOAD_OFFSET + len(payload)] = payload
    return bytes(rom)


def original_gba_frontend_test_rom(path):
    path.write_bytes(rom_bytes())


def decode_frame(pixels):
    """Decode exactly240x160 normalized RGB triples; caller handles crop/scale/stride.

    Every interior pixel of each region must match one color class. Sampling
    only centers is intentionally insufficient to accept mixed/tearing frames.
    Background outside the border/magic/button/counter regions is not sampled.
    """
    if len(pixels) != WIDTH * HEIGHT:
        raise ValueError('Invalid normalized frame dimensions')
    def classify(rect, counter=False, fixed=None, inset=2):
        x, y, w, h = rect
        states = set()
        for py in range(y + inset, y + h - inset):
            for px in range(x + inset, x + w - inset):
                rgb = pixels[py * WIDTH + px]
                if not isinstance(rgb, (tuple, list)) or len(rgb) != 3 or any(not isinstance(v, int) or not 0 <= v <= 255 for v in rgb):
                    raise ValueError('Invalid RGB pixel')
                r, g, b = rgb
                if fixed is not None:
                    state = 1 if all(abs(a-b) <= 16 for a,b in zip(rgb,fixed)) else None
                elif counter:
                    state = 1 if min(rgb) >= 240 else 0 if max(rgb) <= 16 else None
                else:
                    state = 1 if r >= 240 and g <= 16 and b <= 16 else 0 if b >= 240 and r <= 16 and g <= 16 else None
                if state is None:
                    raise ValueError('Invalid region color')
                states.add(state)
        if len(states) != 1:
            raise ValueError('Mixed region pixels')
        return states.pop()
    for rect in BORDER_RECTS:
        classify(rect, fixed=(255,255,0), inset=0)
    for rect, color in zip(MAGIC_RECTS, MAGIC_RGB):
        classify(rect, fixed=color)
    mask = sum(classify(rect) << i for i, rect in enumerate(BUTTON_RECTS))
    bits = [classify(rect, True) for rect in COUNTER_RECTS]
    return {'mask': mask, 'counter': sum(bits[i] << i for i in range(32))}


def verify_observations(frames, expected_mask, max_counter_delta=0x7FFFFFFF):
    """Require exact10-bit mask and forward counter progression, including wrap.

    Counter is uint32; modular deltas >=2**31 are ambiguous and rejected. Capture
    cadence/lag must be bounded independently; this function cannot certify time.
    """
    if not 0 <= expected_mask <= 0x3FF or not 1 <= max_counter_delta < 0x80000000:
        raise ValueError('Invalid oracle expectation')
    decoded = [decode_frame(frame) for frame in frames]
    if len(decoded) < 2:
        raise ValueError('Need two guest observations')
    if any(frame['mask'] != expected_mask for frame in decoded):
        raise ValueError('Guest button mask mismatch')
    for before, after in zip(decoded, decoded[1:]):
        delta = (after['counter'] - before['counter']) & 0xFFFFFFFF
        if not 1 <= delta <= max_counter_delta:
            raise ValueError('Guest counter stale/backward/outside capture lag')
    return decoded
