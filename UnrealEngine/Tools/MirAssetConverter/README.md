# MirAssetConverter

Converts **Legend of Mir 2 (Crystal)** proprietary assets into formats Unreal
Engine can consume. Pure Python 3 standard library — no third-party packages.

## What it reads

| Format | Reader | Source of truth |
|---|---|---|
| `.Lib` graphic libraries (MLibrary v3) | `mir_assets/lib_reader.py` | `Client/MirGraphics/MLibrary.cs`, `LibraryEditor/Graphics/MLibraryV2.cs` |
| `.map` map files (type 100 / v1) | `mir_assets/map_reader.py` | `Client/MirObjects/MapCode.cs` (`LoadMapType100`) |

### `.Lib` (v3) layout
```
int32 version(=3) | int32 count | int32 frameSeek | int32[count] imageOffsets
MImage: int16 W,H,X,Y,ShadowX,ShadowY | uint8 Shadow(bit7=HasMask) | int32 Length | gzip(BGRA W*H*4)
        [ if HasMask: int16 MaskW,MaskH,MaskX,MaskY | int32 MaskLength | gzip(BGRA) ]
FrameSet @ frameSeek: int32 n | n * ( uint8 action | int32 Start,Count,Skip,Interval,
                                     int32 EStart,ECount,ESkip,EInterval | uint8 Reverse | uint8 Blend )
```

### `.map` (type 100) layout
```
uint8 major(=1),uint8 minor(=0),uint8[2] | int16 Width | int16 Height
then Width*Height cells (26 bytes each), column-major (for x: for y:)
  int16 BackIndex | int32 BackImage | int16 MiddleIndex | int16 MiddleImage
  int16 FrontIndex | int16 FrontImage | uint8 DoorIndex | uint8 DoorOffset
  uint8 FrontAnimFrame | uint8 FrontAnimTick | uint8 MiddleAnimFrame | uint8 MiddleAnimTick
  int16 TileAnimImage | int16 TileAnimOffset | uint8 TileAnimFrames | uint8 Light
```

## Usage

```bash
# Convert a graphic library (PNG per frame + JSON manifest); add --atlas for a packed sheet
python convert.py lib /path/to/Monsters.Lib out/Monsters --atlas

# Convert a map to JSON
python convert.py map /path/to/0.map out/0.json

# Run the round-trip self test (generates synthetic assets; needs no game files)
python convert.py selftest
```

## Outputs

- `img_XXXX.png` — one RGBA PNG per non-empty sprite frame.
- `library.json` — image metadata (size + draw offset `x`/`y`) and animation frames.
- `atlas.png` + `atlas.json` — shelf-packed sprite sheet + per-sprite rects.
- `<map>.json` — header, `cellSize` (48×32) and the full cell grid.

## Notes / caveats

- Pixel data is stored **BGRA**; readers expose RGBA for PNG export.
- Negative `X`/`Y` offsets are the engine's sprite anchoring and are preserved.
- The mask (layer-2) length field is read inconsistently by the managed client
  (`Length` vs `MaskLength`); this reader prefers `MaskLength` and falls back
  safely. Masks are rarely used for tiles/sprites.
- `BackImage` bit 15 is the "high wall" flag; it is surfaced as `highWall`.
- `Light` 100–119 marks a fishing cell (surfaced as `fishing`).
- Only `.map` type 100 (version 1) is implemented; other Crystal map types can
  be added to `map_reader.py`.
