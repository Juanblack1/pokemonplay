"""Pure tests only: no emulator/core/native binary execution. CC0-1.0."""
import hashlib
import struct
import unittest
from original_gba_frontend_test import (BUTTON_RECTS, COUNTER_RECTS, WIDTH, HEIGHT,
    MAGIC_RECTS, BORDER_RECTS, PAYLOAD_OFFSET, ROM_SIZE, _payload, rom_bytes, decode_frame, verify_observations)


def fixture(mask=0, counter=1):
    # Independent positions/expected color model, not production ROM instructions.
    pixels = [(0, 0, 0)] * (240 * 160)
    def fill(rect, color):
        x, y, w, h = rect
        for row in range(y, y + h):
            pixels[row * 240 + x:row * 240 + x + w] = [color] * w
    for rect in ((0,0,240,4),(0,156,240,4),(0,4,4,152),(236,4,4,152)):
        fill(rect, (255,255,0))
    for i, color in enumerate(((255,255,0),(255,255,255),(0,0,255),(255,0,0))):
        fill((92+14*i,100,10,8),color)
    for bit in range(10):
        fill((12 + 44 * (bit % 5), 12 + 54 * (bit // 5), 32, 32),
             (255, 0, 0) if mask & (1 << bit) else (0, 0, 255))
    for bit in range(32):
        state = bool(counter & (1 << bit))
        fill((8 + 14 * (bit%16), 112+14*(bit//16), 10, 8), (255, 255, 255) if state else (0, 0, 0))
    return pixels


class OracleTests(unittest.TestCase):
    def test_every_button_exact_mask_and_release(self):
        for bit in range(10):
            with self.subTest(bit=bit):
                mask = 1 << bit
                verify_observations([fixture(mask, 4), fixture(mask, 5)], mask)
                verify_observations([fixture(0, 6), fixture(0, 7)], 0)
                with self.assertRaisesRegex(ValueError, 'mask mismatch'):
                    verify_observations([fixture(1 << ((bit + 1) % 10), 4), fixture(mask, 5)], mask)

    def test_a_plus_b_is_not_a_only(self):
        self.assertEqual(decode_frame(fixture(3, 11)), {'mask': 3, 'counter': 11})
        with self.assertRaisesRegex(ValueError, 'mask mismatch'):
            verify_observations([fixture(3, 11), fixture(3, 12)], 1)

    def test_stuck_release(self):
        with self.assertRaisesRegex(ValueError, 'mask mismatch'):
            verify_observations([fixture(1, 2), fixture(1, 3)], 0)

    def test_stale_reverse_and_wrap(self):
        for before, after in ((10, 10), (10, 9), (0, 0x80000000)):
            with self.assertRaisesRegex(ValueError, 'counter'):
                verify_observations([fixture(0, before), fixture(0, after)], 0)
        verify_observations([fixture(0, 0xFFFFFFFF), fixture(0, 0)], 0)

    def test_mixed_invalid_and_complement(self):
        mutations = [('Mixed', (255, 0, 0), 16, 16),
                     ('Invalid region', (0, 255, 0), 16, 16),
                     ('Invalid RGB', (256, 0, 0), 16, 16),
                     ('Invalid region', (0, 0, 0), 94, 102),
                     ('Invalid region', (0, 0, 0), 0, 0)]
        for error, color, x, y in mutations:
            pixels = fixture(0, 0)
            pixels[y * 240 + x] = color
            with self.subTest(error=error), self.assertRaisesRegex(ValueError, error):
                decode_frame(pixels)

    def test_uint32_high_bits_and_bounded_lag(self):
        for value in (0x80000000,0xFFFFFFFF,0x12345678):
            self.assertEqual(decode_frame(fixture(0,value))['counter'],value)
        verify_observations([fixture(0,3),fixture(0,15)],0,max_counter_delta=12)
        with self.assertRaises(ValueError):
            verify_observations([fixture(0,3),fixture(0,16)],0,max_counter_delta=12)
        with self.assertRaises(ValueError):
            verify_observations([fixture(0,0),fixture(0,0x80000001)],0)

    def test_invalid_size_single_observation_expectation(self):
        with self.assertRaises(ValueError):
            decode_frame(fixture()[:-1])
        with self.assertRaises(ValueError):
            verify_observations([fixture()], 0)
        for mask in (-1, 1024):
            with self.assertRaises(ValueError):
                verify_observations([fixture(), fixture(counter=2)], mask)

    def test_layout_has_large_disjoint_regions(self):
        occupied = set()
        for x, y, w, h in BUTTON_RECTS + COUNTER_RECTS + MAGIC_RECTS + BORDER_RECTS:
            self.assertTrue(0 <= x < x + w <= WIDTH and 0 <= y < y + h <= HEIGHT)
            region = {(px, py) for px in range(x, x + w) for py in range(y, y + h)}
            self.assertFalse(region & occupied)
            occupied |= region
        self.assertEqual(len(BUTTON_RECTS), 10)


class GeneratorTests(unittest.TestCase):
    def test_determinism_header_and_bounds(self):
        first, second = rom_bytes(), rom_bytes()
        self.assertEqual(hashlib.sha256(first).digest(), hashlib.sha256(second).digest())
        self.assertEqual(len(first), ROM_SIZE)
        self.assertEqual(first[4:0xA0], bytes(0x9C))  # no Nintendo logo
        self.assertEqual(first[0xA0:0xAC], b'PP FRONTEND\0')
        self.assertEqual(first[0xB2], 0x96)
        self.assertEqual((sum(first[0xA0:0xBE]) + 0x19) & 255, 0)
        branch = struct.unpack_from('<I', first)[0]
        self.assertEqual(8 + ((branch & 0xFFFFFF) << 2), PAYLOAD_OFFSET)
        payload = _payload()
        self.assertGreater(len(payload), 512)  # explicitly no DS segment cap
        self.assertLessEqual(PAYLOAD_OFFSET + len(payload), ROM_SIZE)
        self.assertEqual(first[PAYLOAD_OFFSET:PAYLOAD_OFFSET + len(payload)], payload)
        self.assertEqual(first[PAYLOAD_OFFSET + len(payload):], bytes(ROM_SIZE - PAYLOAD_OFFSET - len(payload)))

    def test_payload_mmio_and_edge_wait_instructions(self):
        payload = _payload()
        words = struct.unpack('<' + 'I' * (len(payload) // 4), payload)
        for register in (0x04000000, 0x04000006, 0x04000130):
            self.assertIn(register, words)
        self.assertIn(0x0600A000, words)
        self.assertEqual(words.count(0xE2277010), 1)  # one page flip
        self.assertEqual(words.count(0xE1C670B0), 2)  # init and publication
        # Both wait loops compare VCOUNT160; counter has a single increment.
        self.assertEqual(words.count(0xE35200A0), 2)
        self.assertEqual(words.count(0xE28AA001), 1)
        self.assertEqual(words.count(0xE1D940B0), 1)
        for x, y, _, _ in BUTTON_RECTS + COUNTER_RECTS:
            self.assertIn(x + WIDTH * y, words)


if __name__ == '__main__':
    unittest.main()
