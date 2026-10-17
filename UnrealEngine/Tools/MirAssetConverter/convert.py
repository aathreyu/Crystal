#!/usr/bin/env python3
"""Command line entry point for MirAssetConverter.

Examples::

    # Inspect / convert a graphic library
    python convert.py lib Client/Data/Monsters.Lib out/Monsters
    python convert.py lib Client/Data/Monsters.Lib out/Monsters --atlas

    # Convert a map to JSON
    python convert.py map Server/Maps/0.map out/0.json

    # Run the built-in round-trip self test (no game assets required)
    python convert.py selftest
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import tempfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from mir_assets import (  # noqa: E402
    read_library,
    read_map,
    export_library_images,
    export_library_manifest,
    export_library_atlas,
    export_map_json,
)
from mir_assets import synth  # noqa: E402


def _cmd_lib(args: argparse.Namespace) -> int:
    library = read_library(args.input)
    os.makedirs(args.output, exist_ok=True)

    pngs = export_library_images(library, args.output)
    manifest_path = os.path.join(args.output, "library.json")
    export_library_manifest(library, manifest_path, image_dir=args.output)

    summary = {
        "library": os.path.basename(args.input),
        "version": library.version,
        "imageCount": library.count,
        "nonEmptyImages": len(pngs),
        "frames": sorted(library.frame_names().keys()),
        "imagesWritten": len(pngs),
        "manifest": manifest_path,
    }

    if args.atlas:
        atlas_png = os.path.join(args.output, "atlas.png")
        atlas_json = os.path.join(args.output, "atlas.json")
        export_library_atlas(library, atlas_png, atlas_json, max_size=args.atlas_size)
        summary["atlas"] = atlas_png
        summary["atlasManifest"] = atlas_json

    print(json.dumps(summary, indent=2))
    return 0


def _cmd_map(args: argparse.Namespace) -> int:
    mir_map = read_map(args.input)
    os.makedirs(os.path.dirname(os.path.abspath(args.output)) or ".", exist_ok=True)
    export_map_json(mir_map, args.output)
    print(
        json.dumps(
            {
                "map": os.path.basename(args.input),
                "width": mir_map.width,
                "height": mir_map.height,
                "cells": mir_map.width * mir_map.height,
                "output": args.output,
            },
            indent=2,
        )
    )
    return 0


def _cmd_selftest(_args: argparse.Namespace) -> int:
    from tests.test_roundtrip import run  # local import: keeps CLI startup light

    return 0 if run() else 1


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Convert Legend of Mir 2 (Crystal) assets.")
    sub = parser.add_subparsers(dest="command", required=True)

    p_lib = sub.add_parser("lib", help="convert a .Lib graphic library")
    p_lib.add_argument("input", help="path to the .Lib file")
    p_lib.add_argument("output", help="output directory")
    p_lib.add_argument("--atlas", action="store_true", help="also emit a packed atlas")
    p_lib.add_argument("--atlas-size", type=int, default=2048, help="atlas edge size")
    p_lib.set_defaults(func=_cmd_lib)

    p_map = sub.add_parser("map", help="convert a .map file to JSON")
    p_map.add_argument("input", help="path to the .map file")
    p_map.add_argument("output", help="output .json path")
    p_map.set_defaults(func=_cmd_map)

    p_test = sub.add_parser("selftest", help="run the round-trip self test")
    p_test.set_defaults(func=_cmd_selftest)

    return parser


def main(argv=None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    return args.func(args)


if __name__ == "__main__":
    raise SystemExit(main())
