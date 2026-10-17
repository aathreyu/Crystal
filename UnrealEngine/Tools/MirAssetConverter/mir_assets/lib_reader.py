"""Reader for Legend of Mir 2 (Crystal) ``.Lib`` graphic libraries (MLibrary v3).

Binary layout (little-endian), matching ``Client/MirGraphics/MLibrary.cs`` and
``LibraryEditor/Graphics/MLibraryV2.cs``::

    header:
        int32   version          # >= 2; current is 3
        int32   count            # number of images
        int32   frameSeek        # (version >= 3) absolute offset of the FrameSet block
        int32[count] indexList   # absolute file offset of each MImage record

    MImage record (at indexList[i]):
        int16   Width, Height, X, Y, ShadowX, ShadowY
        uint8   Shadow           # bit 7 set => a second (mask) layer follows
        int32   Length           # byte length of gzip(layer-1 pixels)
        bytes   Length           # gzip-compressed raw BGRA pixels (W*H*4)
        [ if HasMask:
            int16 MaskWidth, MaskHeight, MaskX, MaskY
            int32 MaskLength
            bytes MaskLength     # gzip-compressed raw BGRA mask pixels ]

    FrameSet block (at frameSeek, version >= 3):
        int32   frameCount
        repeat frameCount times:
            uint8 action         # MirAction value
            int32 Start, Count, Skip, Interval
            int32 EffectStart, EffectCount, EffectSkip, EffectInterval
            uint8 Reverse
            uint8 Blend

Pixel buffers decompress to raw 32bpp data in BGRA byte order (as produced by
.NET ``Format32bppArgb`` and consumed by the D3D ``A8R8G8B8`` texture).
"""

from __future__ import annotations

import gzip
import struct
from dataclasses import dataclass, field
from typing import Dict, List

LIB_VERSION = 3

# MirAction enum names (order matters - index == enum value), see Shared/Enums.cs.
MIR_ACTION_NAMES = [
    "Standing", "Walking", "Running", "Pushed", "DashL", "DashR", "DashFail",
    "Stance", "Stance2", "Attack1", "Attack2", "Attack3", "Attack4", "Attack5",
    "AttackRange1", "AttackRange2", "AttackRange3", "Special", "Struck",
    "Harvest", "Spell", "Die", "Dead", "Skeleton", "Show", "Hide", "Stoned",
    "Appear", "Revive", "SitDown", "Mine", "Sneek", "DashAttack", "Lunge",
    "WalkingBow", "RunningBow", "Jump", "MountStanding", "MountWalking",
    "MountRunning", "MountStruck", "MountAttack", "FishingCast", "FishingWait",
    "FishingReel",
]


def action_name(value: int) -> str:
    if 0 <= value < len(MIR_ACTION_NAMES):
        return MIR_ACTION_NAMES[value]
    return f"Action{value}"


@dataclass
class Frame:
    """Animation metadata for one MirAction within a library."""

    start: int
    count: int
    skip: int
    interval: int
    effect_start: int = 0
    effect_count: int = 0
    effect_skip: int = 0
    effect_interval: int = 0
    reverse: bool = False
    blend: bool = False

    @property
    def offset(self) -> int:
        return self.count + self.skip

    def to_dict(self) -> dict:
        return {
            "start": self.start,
            "count": self.count,
            "skip": self.skip,
            "interval": self.interval,
            "effectStart": self.effect_start,
            "effectCount": self.effect_count,
            "effectSkip": self.effect_skip,
            "effectInterval": self.effect_interval,
            "reverse": self.reverse,
            "blend": self.blend,
        }


@dataclass
class MImage:
    """A single sprite frame plus its draw-offset metadata."""

    width: int
    height: int
    x: int
    y: int
    shadow_x: int
    shadow_y: int
    shadow: int
    length: int
    has_mask: bool
    pixels_bgra: bytes
    mask_width: int = 0
    mask_height: int = 0
    mask_x: int = 0
    mask_y: int = 0
    mask_pixels_bgra: bytes = b""

    @property
    def is_empty(self) -> bool:
        return self.width <= 0 or self.height <= 0 or not self.pixels_bgra

    def rgba(self) -> bytes:
        """Return the layer-1 pixels converted from BGRA to RGBA."""
        return _bgra_to_rgba(self.pixels_bgra)

    def mask_rgba(self) -> bytes:
        return _bgra_to_rgba(self.mask_pixels_bgra)

    def to_dict(self, index: int) -> dict:
        return {
            "index": index,
            "width": self.width,
            "height": self.height,
            "x": self.x,
            "y": self.y,
            "shadowX": self.shadow_x,
            "shadowY": self.shadow_y,
            "hasMask": self.has_mask,
        }


@dataclass
class MirLibrary:
    """A parsed ``.Lib`` file."""

    version: int
    count: int
    images: List[MImage] = field(default_factory=list)
    frames: Dict[int, Frame] = field(default_factory=dict)
    path: str = ""

    def frame_names(self) -> Dict[str, Frame]:
        return {action_name(k): v for k, v in self.frames.items()}


def _bgra_to_rgba(buf: bytes) -> bytes:
    out = bytearray(len(buf))
    out[0::4] = buf[2::4]  # R <- B
    out[1::4] = buf[1::4]  # G
    out[2::4] = buf[0::4]  # B <- R
    out[3::4] = buf[3::4]  # A
    return bytes(out)


def _decompress_pixels(blob: bytes, width: int, height: int) -> bytes:
    """Gunzip a pixel blob and normalise it to exactly width*height*4 bytes."""
    raw = gzip.decompress(blob)
    expected = max(0, width) * max(0, height) * 4
    if len(raw) < expected:
        raw = raw + b"\x00" * (expected - len(raw))
    elif len(raw) > expected:
        raw = raw[:expected]
    return raw


def _read_mimage(data: bytes, offset: int) -> MImage:
    p = offset
    width, height, x, y, shadow_x, shadow_y = struct.unpack_from("<6h", data, p)
    p += 12
    shadow = data[p]
    p += 1
    (length,) = struct.unpack_from("<i", data, p)
    p += 4

    has_mask = (shadow >> 7) == 1
    layer1 = data[p : p + length]
    p += length
    pixels = _decompress_pixels(layer1, width, height)

    mask_w = mask_h = mask_x = mask_y = 0
    mask_pixels = b""
    if has_mask:
        mask_w, mask_h, mask_x, mask_y = struct.unpack_from("<4h", data, p)
        p += 8
        (mask_length,) = struct.unpack_from("<i", data, p)
        p += 4
        # The managed client reads `length` bytes here, but the editor writes
        # `mask_length`; prefer the explicit mask length, guarding the buffer.
        take = mask_length
        if take <= 0 or p + take > len(data):
            take = length
        if p + take > len(data):
            take = max(0, len(data) - p)
        mask_pixels = _decompress_pixels(data[p : p + take], mask_w, mask_h)

    return MImage(
        width=width,
        height=height,
        x=x,
        y=y,
        shadow_x=shadow_x,
        shadow_y=shadow_y,
        shadow=shadow,
        length=length,
        has_mask=has_mask,
        pixels_bgra=pixels,
        mask_width=mask_w,
        mask_height=mask_h,
        mask_x=mask_x,
        mask_y=mask_y,
        mask_pixels_bgra=mask_pixels,
    )


def _read_frames(data: bytes, seek: int) -> Dict[int, Frame]:
    frames: Dict[int, Frame] = {}
    p = seek
    if p < 0 or p + 4 > len(data):
        return frames
    (frame_count,) = struct.unpack_from("<i", data, p)
    p += 4
    for _ in range(frame_count):
        if p + 1 + 34 > len(data):
            break
        action = data[p]
        p += 1
        values = struct.unpack_from("<8i??", data, p)
        p += 34
        frames[action] = Frame(
            start=values[0],
            count=values[1],
            skip=values[2],
            interval=values[3],
            effect_start=values[4],
            effect_count=values[5],
            effect_skip=values[6],
            effect_interval=values[7],
            reverse=bool(values[8]),
            blend=bool(values[9]),
        )
    return frames


def read_library(path: str) -> MirLibrary:
    """Parse a ``.Lib`` file into a :class:`MirLibrary`."""
    with open(path, "rb") as handle:
        data = handle.read()

    if len(data) < 12:
        raise ValueError(f"{path}: too small to be a Mir library")

    version, count = struct.unpack_from("<ii", data, 0)
    if version < 2:
        raise ValueError(
            f"{path}: unsupported library version {version} (expected >= 2)"
        )

    p = 8
    frame_seek = 0
    if version >= 3:
        (frame_seek,) = struct.unpack_from("<i", data, p)
        p += 4

    offsets = list(struct.unpack_from(f"<{count}i", data, p))
    p += 4 * count

    images: List[MImage] = []
    for off in offsets:
        if off <= 0 or off >= len(data):
            images.append(MImage(0, 0, 0, 0, 0, 0, 0, 0, False, b""))
            continue
        images.append(_read_mimage(data, off))

    frames: Dict[int, Frame] = {}
    if version >= 3 and frame_seek:
        frames = _read_frames(data, frame_seek)

    return MirLibrary(
        version=version,
        count=count,
        images=images,
        frames=frames,
        path=path,
    )
