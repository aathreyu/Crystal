"""Reader for Legend of Mir 2 (Crystal) ``.map`` files.

Matches ``Client/MirObjects/MapCode.cs`` (``LoadMapType100``). This is the
"type 100" / version-1 layout used by Crystal map files::

    header:
        uint8   versionMajor   # must be 1
        uint8   versionMinor   # must be 0
        uint8   unknown[2]
        int16   Width
        int16   Height

    cells (Width * Height), iterated column-major: for x in range(Width): for y in range(Height):
        int16  BackIndex        # which MapLibs[n] library to sample
        int32  BackImage        # image index inside that library (bit15 = high wall flag)
        int16  MiddleIndex
        int16  MiddleImage
        int16  FrontIndex
        int16  FrontImage
        uint8  DoorIndex        # bit7 stripped by the client
        uint8  DoorOffset
        uint8  FrontAnimationFrame
        uint8  FrontAnimationTick
        uint8  MiddleAnimationFrame
        uint8  MiddleAnimationTick
        int16  TileAnimationImage
        int16  TileAnimationOffset
        uint8  TileAnimationFrames
        uint8  Light            # 100..119 marks a fishing cell

Each cell is exactly 26 bytes.
"""

from __future__ import annotations

import struct
from dataclasses import dataclass, field
from typing import List

# <h i h h h h B B B B B B h h B B  == 26 bytes, standard size (no padding).
_CELL = struct.Struct("<h i h h h h B B B B B B h h B B")
CELL_SIZE = _CELL.size

#: BackImage bit 15 marks a "high wall" tile; the client remaps it to bit 29.
BACK_IMAGE_HIGH_WALL = 0x8000


@dataclass
class MapCell:
    back_index: int
    back_image: int
    middle_index: int
    middle_image: int
    front_index: int
    front_image: int
    door_index: int
    door_offset: int
    front_anim_frame: int
    front_anim_tick: int
    middle_anim_frame: int
    middle_anim_tick: int
    tile_anim_image: int
    tile_anim_offset: int
    tile_anim_frames: int
    light: int

    @property
    def is_high_wall(self) -> bool:
        return (self.back_image & BACK_IMAGE_HIGH_WALL) != 0

    @property
    def is_fishing_cell(self) -> bool:
        return 100 <= self.light <= 119

    @property
    def is_empty(self) -> bool:
        return (
            self.back_index < 0
            and self.middle_index < 0
            and self.front_index < 0
        )

    def to_dict(self) -> dict:
        return {
            "backIndex": self.back_index,
            "backImage": self.back_image & 0x7FFF if self.is_high_wall else self.back_image,
            "middleIndex": self.middle_index,
            "middleImage": self.middle_image,
            "frontIndex": self.front_index,
            "frontImage": self.front_image,
            "doorIndex": self.door_index,
            "doorOffset": self.door_offset,
            "frontAnimationFrame": self.front_anim_frame,
            "frontAnimationTick": self.front_anim_tick,
            "middleAnimationFrame": self.middle_anim_frame,
            "middleAnimationTick": self.middle_anim_tick,
            "tileAnimationImage": self.tile_anim_image,
            "tileAnimationOffset": self.tile_anim_offset,
            "tileAnimationFrames": self.tile_anim_frames,
            "light": self.light,
            "highWall": self.is_high_wall,
            "fishing": self.is_fishing_cell,
        }


@dataclass
class MirMap:
    width: int
    height: int
    cells: List[List[MapCell]] = field(default_factory=list)
    path: str = ""

    def cell(self, x: int, y: int) -> MapCell:
        return self.cells[x][y]


def read_map(path: str) -> MirMap:
    """Parse a Crystal ``.map`` file into a :class:`MirMap`."""
    with open(path, "rb") as handle:
        data = handle.read()

    if len(data) < 8:
        raise ValueError(f"{path}: too small to be a Mir map")

    major, minor = data[0], data[1]
    if major != 1 or minor != 0:
        raise ValueError(
            f"{path}: unsupported map version {major}.{minor} "
            "(only type 100 / version 1 is supported)"
        )

    width, height = struct.unpack_from("<hh", data, 4)
    if width <= 0 or height <= 0:
        raise ValueError(f"{path}: invalid map size {width}x{height}")

    expected = 8 + width * height * CELL_SIZE
    if len(data) < expected:
        raise ValueError(
            f"{path}: truncated map, expected >= {expected} bytes, got {len(data)}"
        )

    cells: List[List[MapCell]] = []
    offset = 8
    for _x in range(width):
        column: List[MapCell] = []
        for _y in range(height):
            values = _CELL.unpack_from(data, offset)
            offset += CELL_SIZE
            column.append(
                MapCell(
                    back_index=values[0],
                    back_image=values[1],
                    middle_index=values[2],
                    middle_image=values[3],
                    front_index=values[4],
                    front_image=values[5],
                    door_index=values[6] & 0x7F,
                    door_offset=values[7],
                    front_anim_frame=values[8],
                    front_anim_tick=values[9],
                    middle_anim_frame=values[10],
                    middle_anim_tick=values[11],
                    tile_anim_image=values[12],
                    tile_anim_offset=values[13],
                    tile_anim_frames=values[14],
                    light=values[15],
                )
            )
        cells.append(column)

    return MirMap(width=width, height=height, cells=cells, path=path)
