"""Synthetic ``.Lib`` / ``.map`` writers used for round-trip testing.

The Crystal asset binaries are not redistributed with the source repo, so the
converter is validated against files we generate ourselves using the exact same
byte layout the engine writes. If the reader round-trips these fixtures, it is
consistent with the format documented in the reader modules.
"""

from __future__ import annotations

import gzip
import struct
from typing import Iterable, List, Sequence, Tuple

from .map_reader import _CELL


def _pack_bgra(pixels: Sequence[Tuple[int, int, int, int]]) -> bytes:
    out = bytearray()
    for r, g, b, a in pixels:
        out += bytes((b, g, r, a))  # engine stores BGRA
    return bytes(out)


def make_image_blob(
    width: int,
    height: int,
    x: int,
    y: int,
    bgra: bytes,
    mask_bgra: bytes = b"",
    shadow: int = 0,
    shadow_x: int = 0,
    shadow_y: int = 0,
) -> bytes:
    """Build a single MImage record exactly as ``MLibraryV2.Save`` writes it."""
    has_mask = bool(mask_bgra)
    shadow_byte = shadow | (0x80 if has_mask else 0)

    gz = gzip.compress(bgra, 9)
    blob = struct.pack("<6h", width, height, x, y, shadow_x, shadow_y)
    blob += bytes((shadow_byte,))
    blob += struct.pack("<i", len(gz)) + gz

    if has_mask:
        mgz = gzip.compress(mask_bgra, 9)
        # mask metadata carries the *image* size in the editor implementation.
        blob += struct.pack("<4h", width, height, x, y)
        blob += struct.pack("<i", len(mgz)) + mgz

    return blob


def write_library(
    path: str,
    images: Iterable[bytes],
    frames: Sequence[Tuple[int, int, int, int, int, int, int, int, bool, bool]] = (),
) -> None:
    """Write a complete v3 ``.Lib`` file.

    ``images`` are MImage blobs (see :func:`make_image_blob`). ``frames`` are
    tuples of (action, start, count, skip, interval, eStart, eCount, eSkip,
    eInterval, reverse, blend).
    """
    images = list(images)
    count = len(images)

    body = b"".join(images)
    base = 12 + count * 4  # header (version, count, frameSeek) + index list
    offsets = []
    cursor = 0
    for blob in images:
        offsets.append(base + cursor)
        cursor += len(blob)
    frame_seek = base + cursor

    with open(path, "wb") as handle:
        handle.write(struct.pack("<iii", 3, count, frame_seek))
        for off in offsets:
            handle.write(struct.pack("<i", off))
        handle.write(body)
        handle.write(struct.pack("<i", len(frames)))
        for frame in frames:
            action = frame[0]
            handle.write(bytes((action,)))
            handle.write(
                struct.pack(
                    "<8i??",
                    frame[1], frame[2], frame[3], frame[4],
                    frame[5], frame[6], frame[7], frame[8],
                    bool(frame[9]), bool(frame[10]),
                )
            )


def write_map(path: str, width: int, height: int, cells: Sequence[Sequence[tuple]]) -> None:
    """Write a type-100 ``.map`` file.

    ``cells[x][y]`` is a 16-tuple matching :data:`mir_assets.map_reader.MapCell`
    field order.
    """
    with open(path, "wb") as handle:
        handle.write(bytes((1, 0, 0, 0)))
        handle.write(struct.pack("<hh", width, height))
        for x in range(width):
            for y in range(height):
                handle.write(_CELL.pack(*cells[x][y]))


def sample_library(path: str) -> None:
    """Write a small but representative ``.Lib`` fixture (2x2 red, 1x1 green)."""
    red = _pack_bgra([(255, 0, 0, 255)] * 4)
    green = _pack_bgra([(0, 255, 0, 255)])
    img0 = make_image_blob(2, 2, -1, -2, red)
    img1 = make_image_blob(1, 1, 0, 0, green, shadow=5)
    frames = [
        (0, 0, 4, 0, 450, 0, 0, 0, 0, False, False),   # Standing
        (1, 32, 6, 0, 100, 0, 0, 0, 0, False, False),  # Walking
    ]
    write_library(path, [img0, img1], frames)


def sample_map(path: str) -> None:
    """Write a tiny 2x2 type-100 map fixture."""
    def cell(back_index, back_image, light=0):
        return (
            back_index, back_image, -1, 0, -1, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, light,
        )

    cells = [
        [cell(0, 0), cell(0, 1)],
        [cell(0, 2, light=110), cell(1, 0)],
    ]
    write_map(path, 2, 2, cells)
