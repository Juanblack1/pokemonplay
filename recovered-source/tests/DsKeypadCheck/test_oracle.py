import importlib.util
from pathlib import Path
import struct
import sys
import tempfile
import unittest

TOOLS = Path(__file__).resolve().parents[2] / "tools"
sys.path.insert(0, str(TOOLS))
from original_ds_test import ArmProgram, original_ds_test_rom
spec = importlib.util.spec_from_file_location("probe_libretro", TOOLS / "probe-libretro.py")
probe = importlib.util.module_from_spec(spec)
spec.loader.exec_module(probe)


class KeypadOracleCheck(unittest.TestCase):
    def frame(self, held=(), serial=1, pitch=1056):
        data = bytearray(pitch * 384)
        positions = ((16,16),(72,16),(128,16),(184,16),
                     (16,72),(72,72),(128,72),(184,72),
                     (16,128),(72,128),(128,128),(184,128))
        for bit, (x,y) in enumerate(positions):
            color = 0x00FB0000 if bit in held else 0x000000FB
            for row in range(y,y+16):
                for column in range(x,x+16):
                    struct.pack_into("<I",data,row*pitch+column*4,color)
        return {"format":1,"width":256,"height":384,"pitch":pitch,"bytes":bytes(data),"serial":serial}

    def test_all_buttons_and_unrelated_regions(self):
        for bit in range(12):
            with self.subTest(bit=bit):
                result = probe.ds_frame_regions(self.frame((bit,)),1<<bit)
                self.assertTrue(result["passed"])
                self.assertEqual(result["observed_mask"],1<<bit)
                wrong = probe.ds_frame_regions(self.frame((bit,(bit+1)%12)),1<<bit)
                self.assertFalse(wrong["passed"])
        self.assertTrue(probe.ds_frame_regions(self.frame(),0)["passed"])
        self.assertFalse(probe.ds_frame_regions(self.frame((10,)),0)["passed"])

    def test_two_final_frames_and_duplicate_callback_gap(self):
        with tempfile.TemporaryDirectory() as output:
            path=Path(output)
            good=probe.ds_frame_sample([self.frame((11,),1),self.frame((11,),2)],path,"y-held",2048)
            self.assertTrue(good["passed"])
            self.assertTrue((path/"y-held.png").is_file())
            mismatch=probe.ds_frame_sample([self.frame((),1),self.frame((11,),2)],path,"unstable",2048)
            self.assertFalse(mismatch["passed"])
            for serial in (1,3):
                with self.assertRaisesRegex(RuntimeError,"fresh consecutive"):
                    probe.ds_frame_sample([self.frame((),1),self.frame((),serial)],path,"stale",0)

    def test_invalid_format_and_stride_are_rejected(self):
        frame=self.frame();frame["format"]=2
        with self.assertRaisesRegex(RuntimeError,"XRGB8888"):
            probe.ds_frame_regions(frame,0)
        frame=self.frame();frame["pitch"]=1023
        with self.assertRaisesRegex(RuntimeError,"stride"):
            probe.ds_frame_regions(frame,0)

    def test_original_segments_are_bounded_and_deterministic(self):
        with tempfile.TemporaryDirectory() as output:
            first,second=Path(output)/"first.nds",Path(output)/"second.nds"
            original_ds_test_rom(first);original_ds_test_rom(second)
            data=first.read_bytes()
            self.assertEqual(data,second.read_bytes())
            self.assertEqual(len(data),128*1024)
            self.assertEqual(struct.unpack_from("<4I",data,0x20),(0x1000,0x02000000,0x02000000,512))
            self.assertEqual(struct.unpack_from("<4I",data,0x30),(0x1200,0x03800000,0x03800000,512))
            self.assertEqual(data[0xC0:0x15C],bytes(0x9C))
            self.assertEqual(data[0x1400:],bytes(len(data)-0x1400))
        oversized=ArmProgram()
        for _ in range(129):oversized.emit(0xE1A00000)
        with self.assertRaisesRegex(ValueError,"reserved ROM segment"):
            oversized.build(0x02000000)


if __name__ == "__main__":
    unittest.main()
