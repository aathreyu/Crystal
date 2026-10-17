"""Round-trip tests: generate synthetic assets, read them back, assert fidelity.

Runnable three ways::

    python -m unittest discover -s Tools/MirAssetConverter
    python Tools/MirAssetConverter/convert.py selftest
    python Tools/MirAssetConverter/tests/test_roundtrip.py
"""

from __future__ import annotations

import os
import struct
import sys
import tempfile
import unittest

_HERE = os.path.dirname(os.path.abspath(__file__))
_ROOT = os.path.dirname(_HERE)
if _ROOT not in sys.path:
    sys.path.insert(0, _ROOT)

from mir_assets import (  # noqa: E402
    read_library,
    read_map,
    export_library_images,
    export_library_manifest,
    export_library_atlas,
    export_map_json,
)
from mir_assets import synth  # noqa: E402
from mir_assets.png import encode_rgba_png  # noqa: E402


class LibraryRoundTripTest(unittest.TestCase):
    def setUp(self) -> None:
        self.tmp = tempfile.mkdtemp(prefix="mir_lib_")
        self.path = os.path.join(self.tmp, "Test.Lib")
        synth.sample_library(self.path)
        self.library = read_library(self.path)

    def test_header(self) -> None:
        self.assertEqual(self.library.version, 3)
        self.assertEqual(self.library.count, 2)

    def test_image_metadata(self) -> None:
        img0 = self.library.images[0]
        self.assertEqual((img0.width, img0.height), (2, 2))
        self.assertEqual((img0.x, img0.y), (-1, -2))
        self.assertFalse(img0.has_mask)

    def test_bgra_to_rgba(self) -> None:
        # source pixel was RGBA (255,0,0,255) -> stored BGRA -> back to RGBA.
        img0 = self.library.images[0]
        rgba = img0.rgba()
        self.assertEqual(tuple(rgba[0:4]), (255, 0, 0, 255))
        self.assertEqual(len(rgba), 2 * 2 * 4)

    def test_frames(self) -> None:
        names = self.library.frame_names()
        self.assertIn("Standing", names)
        self.assertIn("Walking", names)
        self.assertEqual(names["Standing"].count, 4)
        self.assertEqual(names["Standing"].interval, 450)
        self.assertEqual(names["Walking"].start, 32)

    def test_export_images_and_manifest(self) -> None:
        out = os.path.join(self.tmp, "out")
        pngs = export_library_images(self.library, out)
        self.assertEqual(len(pngs), 2)
        for png in pngs:
            with open(png, "rb") as handle:
                self.assertEqual(handle.read(8), b"\x89PNG\r\n\x1a\n")

        manifest = os.path.join(out, "library.json")
        export_library_manifest(self.library, manifest)
        self.assertTrue(os.path.getsize(manifest) > 0)

    def test_export_atlas(self) -> None:
        atlas = os.path.join(self.tmp, "atlas.png")
        manifest = os.path.join(self.tmp, "atlas.json")
        export_library_atlas(self.library, atlas, manifest, max_size=64)
        with open(atlas, "rb") as handle:
            data = handle.read()
        self.assertEqual(data[:8], b"\x89PNG\r\n\x1a\n")
        # IHDR width/height are the first 8 bytes of the IHDR data chunk.
        width, height = struct.unpack_from(">II", data, 16)
        self.assertEqual(width, 64)
        self.assertGreater(height, 0)
        self.assertTrue(os.path.getsize(manifest) > 0)


class MapRoundTripTest(unittest.TestCase):
    def setUp(self) -> None:
        self.tmp = tempfile.mkdtemp(prefix="mir_map_")
        self.path = os.path.join(self.tmp, "0.map")
        synth.sample_map(self.path)
        self.mir_map = read_map(self.path)

    def test_dimensions(self) -> None:
        self.assertEqual((self.mir_map.width, self.mir_map.height), (2, 2))

    def test_cell_values(self) -> None:
        c00 = self.mir_map.cell(0, 0)
        self.assertEqual(c00.back_index, 0)
        self.assertEqual(c00.back_image, 0)
        c10 = self.mir_map.cell(0, 1)
        self.assertEqual(c10.back_image, 1)
        c01 = self.mir_map.cell(1, 0)
        self.assertEqual(c01.back_image, 2)
        self.assertTrue(c01.is_fishing_cell)
        c11 = self.mir_map.cell(1, 1)
        self.assertEqual(c11.back_index, 1)

    def test_export_json(self) -> None:
        out = os.path.join(self.tmp, "0.json")
        export_map_json(self.mir_map, out)
        self.assertTrue(os.path.getsize(out) > 0)


class PngTest(unittest.TestCase):
    def test_encode_header(self) -> None:
        data = encode_rgba_png(2, 1, bytes(2 * 1 * 4))
        self.assertEqual(data[:8], b"\x89PNG\r\n\x1a\n")
        width, height = struct.unpack_from(">II", data, 16)
        self.assertEqual((width, height), (2, 1))

    def test_rejects_bad_buffer(self) -> None:
        with self.assertRaises(ValueError):
            encode_rgba_png(2, 2, b"\x00\x00\x00")


def run() -> bool:
    loader = unittest.TestLoader()
    suite = loader.loadTestsFromModule(sys.modules[__name__])
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    return result.wasSuccessful()


if __name__ == "__main__":
    raise SystemExit(0 if run() else 1)
