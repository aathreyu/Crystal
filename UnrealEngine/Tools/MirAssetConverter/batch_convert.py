#!/usr/bin/env python3
"""Batch-convert every ``.Lib`` and ``.map`` under a source tree.

Mirrors the folder layout the runtime expects::

    <out>/Libraries/<Name>/atlas.png + atlas.json + library.json
    <out>/Maps/<name>.json

Usage::

    python batch_convert.py <mir_data_dir> <out_dir> [--atlas-size 4096]
"""

from __future__ import annotations

import argparse
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from mir_assets import (  # noqa: E402
    read_library,
    read_map,
    export_library_atlas,
    export_library_manifest,
    export_map_json,
)


def _iter_files(root: str, suffix: str):
    for dirpath, _dirs, files in os.walk(root):
        for name in files:
            if name.lower().endswith(suffix):
                yield os.path.join(dirpath, name)


def convert_libraries(src: str, out: str, atlas_size: int) -> tuple[int, int]:
    ok = failed = 0
    for path in _iter_files(src, ".lib"):
        name = os.path.splitext(os.path.basename(path))[0]
        target = os.path.join(out, "Libraries", name)
        os.makedirs(target, exist_ok=True)
        try:
            library = read_library(path)
            export_library_atlas(
                library,
                os.path.join(target, "atlas.png"),
                os.path.join(target, "atlas.json"),
                max_size=atlas_size,
            )
            export_library_manifest(
                library, os.path.join(target, "library.json")
            )
            ok += 1
        except Exception as exc:  # noqa: BLE001 - report and continue
            failed += 1
            print(f"[skip] {path}: {exc}", file=sys.stderr)
    return ok, failed


def convert_maps(src: str, out: str) -> tuple[int, int]:
    ok = failed = 0
    for path in _iter_files(src, ".map"):
        name = os.path.splitext(os.path.basename(path))[0]
        target = os.path.join(out, "Maps", f"{name}.json")
        os.makedirs(os.path.dirname(target), exist_ok=True)
        try:
            export_map_json(read_map(path), target)
            ok += 1
        except Exception as exc:  # noqa: BLE001 - report and continue
            failed += 1
            print(f"[skip] {path}: {exc}", file=sys.stderr)
    return ok, failed


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("src", help="directory to scan for .Lib / .map files")
    parser.add_argument("out", help="output directory")
    parser.add_argument("--atlas-size", type=int, default=4096)
    args = parser.parse_args(argv)

    lib_ok, lib_fail = convert_libraries(args.src, args.out, args.atlas_size)
    map_ok, map_fail = convert_maps(args.src, args.out)

    print(f"libraries: {lib_ok} converted, {lib_fail} failed")
    print(f"maps:      {map_ok} converted, {map_fail} failed")
    return 0 if (lib_fail == 0 and map_fail == 0) else 1


if __name__ == "__main__":
    raise SystemExit(main())
