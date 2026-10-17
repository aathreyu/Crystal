"""MirAssetConverter - convert Legend of Mir 2 (Crystal) assets to Unreal-friendly formats.

The Crystal engine stores graphics in proprietary ``.Lib`` files (MLibrary v3) and
maps in custom binary ``.map`` files. This package reads both formats without any
third-party dependencies and exports PNG sprite sheets plus JSON manifests that
Unreal Engine (Paper2D / DataAssets) can consume.

Public API:
    read_library(path)   -> MirLibrary
    read_map(path)       -> MirMap
    export_library(...)  -> writes PNGs + manifest
    export_map(...)      -> writes JSON
"""

from .lib_reader import MirLibrary, MImage, Frame, read_library, LIB_VERSION
from .map_reader import MirMap, MapCell, read_map
from .export import (
    export_library_images,
    export_library_manifest,
    export_library_atlas,
    export_map_json,
)
from .png import write_rgba_png

__all__ = [
    "MirLibrary",
    "MImage",
    "Frame",
    "read_library",
    "LIB_VERSION",
    "MirMap",
    "MapCell",
    "read_map",
    "export_library_images",
    "export_library_manifest",
    "export_library_atlas",
    "export_map_json",
    "write_rgba_png",
]

__version__ = "0.1.0"
