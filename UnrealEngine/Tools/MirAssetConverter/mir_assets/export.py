"""Export parsed Mir assets into Unreal-friendly artefacts.

* :func:`export_library_images` - one PNG per sprite frame.
* :func:`export_library_manifest` - JSON describing images + animation frames.
* :func:`export_library_atlas` - shelf-packed atlas PNG + JSON rect manifest
  (importable as a Paper2D sprite sheet).
* :func:`export_map_json` - a map's cells as JSON for a UE data loader.

All coordinates are preserved from the source (including the negative draw
offsets the engine uses to anchor sprites to a map cell), so a UE renderer can
reproduce the original placement exactly.
"""

from __future__ import annotations

import json
import os
from typing import Dict, List, Tuple

from .lib_reader import MirLibrary, action_name
from .map_reader import MirMap
from .png import write_rgba_png


def export_library_images(library: MirLibrary, out_dir: str, prefix: str = "img") -> List[str]:
    """Write each non-empty image as ``{prefix}_{index:04d}.png``. Returns paths."""
    os.makedirs(out_dir, exist_ok=True)
    written: List[str] = []
    for index, image in enumerate(library.images):
        if image.is_empty:
            continue
        name = f"{prefix}_{index:04d}.png"
        path = os.path.join(out_dir, name)
        write_rgba_png(path, image.width, image.height, image.rgba())
        written.append(path)
    return written


def export_library_manifest(library: MirLibrary, path: str, image_dir: str = "") -> None:
    """Write a JSON manifest describing the library's images and frames."""
    manifest = {
        "source": os.path.basename(library.path),
        "version": library.version,
        "count": library.count,
        "imageDir": image_dir,
        "images": [
            img.to_dict(i) for i, img in enumerate(library.images) if not img.is_empty
        ],
        "frames": {
            action_name(action): frame.to_dict()
            for action, frame in library.frames.items()
        },
    }
    with open(path, "w", encoding="utf-8") as handle:
        json.dump(manifest, handle, indent=2)


def export_library_atlas(
    library: MirLibrary,
    atlas_path: str,
    manifest_path: str,
    max_size: int = 2048,
    padding: int = 1,
) -> None:
    """Shelf-pack every image into one RGBA atlas PNG plus a rect manifest.

    Raises ``RuntimeError`` if the images do not fit in a single ``max_size``
    square - callers can split libraries or raise the limit.
    """
    entries: List[Tuple[int, int, int, int, int]] = []  # (index, x, y, w, h)
    shelf_x = shelf_y = shelf_h = 0
    placements: List[Tuple[int, int, int, int]] = []

    for index, image in enumerate(library.images):
        if image.is_empty:
            continue
        w, h = image.width, image.height
        if shelf_x + w + padding > max_size:
            shelf_x = 0
            shelf_y += shelf_h + padding
            shelf_h = 0
        if shelf_y + h + padding > max_size or w > max_size or h > max_size:
            raise RuntimeError(
                f"library does not fit in a {max_size}x{max_size} atlas"
            )
        placements.append((index, shelf_x, shelf_y, w, h))
        shelf_x += w + padding
        shelf_h = max(shelf_h, h)

    atlas_h = shelf_y + shelf_h + padding
    if atlas_h <= 0:
        atlas_h = 1

    canvas = bytearray(max_size * atlas_h * 4)
    for (index, px, py, w, h) in placements:
        rgba = library.images[index].rgba()
        row = w * 4
        for y in range(h):
            src = y * row
            dst = ((py + y) * max_size + px) * 4
            canvas[dst : dst + row] = rgba[src : src + row]
        entries.append((index, px, py, w, h))

    write_rgba_png(atlas_path, max_size, atlas_h, bytes(canvas))

    manifest = {
        "source": os.path.basename(library.path),
        "atlas": os.path.basename(atlas_path),
        "width": max_size,
        "height": atlas_h,
        "padding": padding,
        "sprites": [
            {"index": i, "x": x, "y": y, "width": w, "height": h}
            for (i, x, y, w, h) in entries
        ],
        "images": [
            library.images[i].to_dict(i) for (i, _x, _y, _w, _h) in entries
        ],
        "frames": {
            action_name(a): f.to_dict() for a, f in library.frames.items()
        },
    }
    with open(manifest_path, "w", encoding="utf-8") as handle:
        json.dump(manifest, handle, indent=2)


def export_map_json(mir_map: MirMap, path: str) -> None:
    """Write a map's header + cells as JSON."""
    payload: Dict = {
        "source": os.path.basename(mir_map.path),
        "width": mir_map.width,
        "height": mir_map.height,
        "cellSize": {"width": 48, "height": 32},
        "cells": [
            [mir_map.cells[x][y].to_dict() for y in range(mir_map.height)]
            for x in range(mir_map.width)
        ],
    }
    with open(path, "w", encoding="utf-8") as handle:
        json.dump(payload, handle, indent=2)
