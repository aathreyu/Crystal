"""Minimal, dependency-free PNG writer (RGBA, 8-bit).

Only the subset needed to export Mir sprite images: a single IHDR/IDAT/IEND
stream with filter type 0 on every scanline. Uses the stdlib ``zlib`` module so
no Pillow / imageio install is required.
"""

import struct
import zlib

_PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"


def _chunk(chunk_type: bytes, data: bytes) -> bytes:
    return (
        struct.pack(">I", len(data))
        + chunk_type
        + data
        + struct.pack(">I", zlib.crc32(chunk_type + data) & 0xFFFFFFFF)
    )


def encode_rgba_png(width: int, height: int, rgba: bytes, level: int = 9) -> bytes:
    """Encode raw RGBA bytes (R,G,B,A per pixel, row-major) into PNG bytes."""
    if width <= 0 or height <= 0:
        raise ValueError(f"invalid image size {width}x{height}")
    expected = width * height * 4
    if len(rgba) != expected:
        raise ValueError(
            f"pixel buffer is {len(rgba)} bytes, expected {expected} "
            f"({width}x{height} RGBA)"
        )

    # IHDR: width, height, bit depth 8, colour type 6 (RGBA), no interlace.
    ihdr = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)

    stride = width * 4
    raw = bytearray((stride + 1) * height)
    src = 0
    dst = 0
    for _ in range(height):
        raw[dst] = 0  # filter type: None
        dst += 1
        raw[dst : dst + stride] = rgba[src : src + stride]
        src += stride
        dst += stride

    idat = zlib.compress(bytes(raw), level)

    return (
        _PNG_SIGNATURE
        + _chunk(b"IHDR", ihdr)
        + _chunk(b"IDAT", idat)
        + _chunk(b"IEND", b"")
    )


def write_rgba_png(path, width: int, height: int, rgba: bytes) -> None:
    """Write an RGBA buffer to ``path`` as a PNG file."""
    with open(path, "wb") as handle:
        handle.write(encode_rgba_png(width, height, rgba))
