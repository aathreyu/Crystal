# CrystalAssetExport

Converts Crystal (Legend of Mir 2) client data into engine-neutral files, so a new
renderer (Unreal 5.8, Unity, Godot, …) does not have to parse the legacy formats at
runtime.

Two readers, both ported from this repository's own code:

| Input | Ported from | Output |
|---|---|---|
| `.Lib` sprite libraries (v2/v3) | `Client/MirGraphics/MLibrary.cs` | PNG per sprite + mask PNG + `library.json` (pivots, shadow offsets, frame table) |
| `.map` tile grids (9 formats) | `Client/MirObjects/MapCode.cs` | `.json` or compact `.bin` (per-cell layer indices, door/animation/light data) |

No Windows APIs, no SlimDX, no NuGet packages: plain .NET 8 + `System.IO.Compression`
and `System.Text.Json`. It builds and runs on Linux/macOS/Windows.

---

## Build and run

```bash
dotnet build Tools/CrystalAssetExport/CrystalAssetExport.csproj
dotnet run --project Tools/CrystalAssetExport -- selftest        # 72 assertions, no data pack needed
dotnet run --project Tools/CrystalAssetExport -- all "C:/CrystalClient" --out ./export
```

The binary lands in `Build/Tools/Debug/CrystalAssetExport.dll` (run it with `dotnet <path>`).

### Commands

```
CrystalAssetExport lib <file.Lib | directory> [more...] [--out <dir>]
CrystalAssetExport map <file.map | directory> [more...] [--out <dir>] [--binary]
CrystalAssetExport all <crystalClientRoot> [--out <dir>] [--binary]
CrystalAssetExport selftest
```

* `lib` — walks directories recursively (case-insensitive `.Lib`) and keeps the
  relative folder structure in the output.
* `map` — same for `.map` files.
* `all` — expects a client install: `<root>/Data/**/*.Lib` and `<root>/Map/**/*.map`.
* Exit codes: `0` success, `1` at least one input failed (others are still converted),
  `2` usage error.

### Options

| Option | Meaning |
|---|---|
| `-o, --out <dir>` | Output root (default `./export`) |
| `--clean-transparent` | Zero RGB where alpha = 0. Recommended if the target engine uses bilinear filtering or mipmaps, which would otherwise bleed stale colour into edges. |
| `--binary` | Write maps as `.bin` instead of `.json` (≈3–5× smaller, single-buffer read in C++) |
| `-q, --quiet` | Only errors and the final summary |

---

## Output: libraries

```
export/Data/CArmour/00/
  00000.png
  00001.png
  00001_mask.png
  library.json
```

`library.json`:

```json
{
  "source": "00.Lib",
  "version": 3,
  "imageCount": 3,
  "images": [
    { "index": 0, "file": "00000.png", "width": 4, "height": 3,
      "x": 5, "y": -6, "shadowX": 1, "shadowY": 2, "shadow": 0, "empty": false },
    { "index": 1, "file": "00001.png", "width": 2, "height": 2, "x": 0, "y": 0,
      "shadowX": 0, "shadowY": 0, "shadow": 128, "empty": false,
      "mask": { "file": "00001_mask.png", "width": 2, "height": 2, "x": -1, "y": -1 } },
    { "index": 2, "width": 0, "height": 0, "x": 0, "y": 0,
      "shadowX": 0, "shadowY": 0, "shadow": 0, "empty": true }
  ],
  "frames": {
    "Standing": { "start": 0, "count": 4, "skip": 0, "interval": 450,
                  "effectStart": 0, "effectCount": 0, "effectSkip": 0, "effectInterval": 0,
                  "reverse": false, "blend": false }
  }
}
```

Rules the renderer must honour:

* **`x`/`y` are draw offsets**, applied only when the client draws with `offSet = true`
  (`MLibrary.Draw(index, point, colour, offSet)`). UI widgets usually don't offset;
  world sprites do. Keep the PNG at its natural size; do not crop by `x`/`y`.
* **`mask`** is a second sprite (glow/weapon effect), not an alpha channel. Draw it at
  `mask.x`/`mask.y` within the same cell with additive or alpha blending. `shadow`
  keeps the raw byte (`0x80` = mask present, low bits are the client's shadow selector).
* **`frames`** is the library's `FrameSet` (`MirAction` name → range). `start` is the
  first image index, `count` frames are drawn, `skip` frames are skipped between them,
  `interval` is milliseconds per frame, `reverse` plays backwards, `blend` uses additive
  blending. Effect fields describe a parallel effect animation.
* **Empty images are omitted**, not written as 1×1 files. `index` is always the runtime
  index, so `images[i].index` may not equal `i`.

Colours: the archives store **BGRA (A8R8G8B8, straight alpha)**; the PNGs are RGBA.
No gamma conversion is applied — import as sRGB.

---

## Output: maps

```
export/Map/WemadeMir2/0.json     # or 0.bin with --binary
```

JSON is compact (not indented) because a 1000×1000 map is ~18M numbers:

```json
{ "source": "0.map", "format": "WemadeMir2", "formatId": 0,
  "width": 300, "height": 300, "cellWidth": 48, "cellHeight": 32,
  "layout": "row-major over the client's [x,y] indexing: index = x * height + y",
  "fields": ["backIndex", "backImage", "..."],
  "cells": [ [0, 101, 1, 5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], ... ] }
```

`cells` is `width * height` entries of 18 integers in the client's `[x, y]` order
(`index = x * height + y`, **not** row-major `y * width + x` — the original loops
`for x { for y { … } }`).

Field order (also emitted as `fields`, so you never have to guess):

| # | Field | Meaning |
|---:|---|---|
| 0 | `backIndex` | tile layer library (`Libraries.MapLibs[i]`), `-1` = none |
| 1 | `backImage` | image index inside that library |
| 2 | `middleIndex` | middle/object layer library |
| 3 | `middleImage` | |
| 4 | `frontIndex` | front layer library (drawn **after** actors) |
| 5 | `frontImage` | |
| 6 | `doorIndex` | door group (already masked with `0x7F`) |
| 7 | `doorOffset` | open/closed offset |
| 8 | `frontAnimFrame` | front layer animation frame |
| 9 | `frontAnimTick` | |
| 10 | `middleAnimFrame` | middle layer animation frame |
| 11 | `middleAnimTick` | |
| 12 | `tileAnimImage` | animated tile image index (library 190) |
| 13 | `tileAnimOffset` | |
| 14 | `tileAnimFrames` | frame count |
| 15 | `light` | per-cell light (0–255; `100..119` = fishing cell in Crystal) |
| 16 | `unknown` | raw byte preserved from the map (format-specific) |
| 17 | `flags` | bit 0 = fishing cell |

Notes:

* `backImage` values with `0x20000000` set come from the source's `0x8000` bit; the
  client uses that as a "draw as background/animated" marker. Preserve it.
* Some tiles are **96×64 (2×2 cells)**. Detect via the library image size and draw once
  per 2×2 block, or you will get seams (see `GameScene.cs`).
* Front-layer tiles are drawn at `y - imageHeight`, not at the cell origin.

### Binary map layout (`--binary`)

Little endian, fixed 24-byte header, then a flat array:

```
char[8]  "MIRMAP01"
int32    formatId      (0 = WemadeMir2, …, 100 = Crystal)
int32    width
int32    height
int32    fieldCount    (= 18)
int32[width*height*fieldCount] cells   // index = (x*height + y)*fieldCount + field
```

Read it with `TArray<uint8>` + `FMemoryReader` in Unreal; no JSON parser needed.

---

## Fidelity notes (differences from the client)

Deliberate, and documented so nobody hunts a phantom bug:

1. **Truncated input is an error, not silence.** The client wraps every loader in
   `try/catch` and leaves a partially filled grid; this tool throws
   `MapFormatException` / `LibFormatException` with file, format, offset and byte counts.
2. **Mask length uses `MaskLength`.** `MLibrary.CreateTexture` skips 12 header bytes then
   reads `Length` bytes for the mask (the *layer 1* length). Both are equal in practice;
   this tool reads the mask header properly and uses `MaskLength`.
3. **`frameSeek` may be 0** (v2, or a v3 file without frames). No frames are emitted then.
4. **Unknown `.Lib` versions are rejected** (`< 2` or `> 3`), matching the client. v0/v1
   files would need `LibraryEditor/Graphics/MLibraryV0.cs`/`V1.cs` and are out of scope.
5. **`Light *= 2` / `Light *= 4`** for the Mir3 map families is applied, because that is
   what the client does before rendering.
6. Map detection reproduces the original's magic-byte order *and* its `||`/`&&`
   precedence quirk for the Shanda 2012 check.

## Limitations

* No atlas packing; no `.Lib` **writer** and no map **writer** (read-only tool).
* One manifest per library; very large libraries produce many small PNGs — pack them in
  the engine's import step if that matters.
* Does not validate that a map's library indices exist in a matching data set; index
  ranges come from the server's `Library` enum, not from the files.
* Sprite/animation *composition* (which armour piece goes over which, hard-coded frame
  counts in `PlayerObject`/`MonsterObject`) is not data-driven in the original, so it is
  not exported.

## Self-test

`CrystalAssetExport selftest` builds a synthetic `.Lib` v3 (normal image, masked image,
zero-sized image, two frames), a classic Mir2 map, a Crystal map and a truncated map in a
temp folder, then:

* parses them back and compares header, pivot, mask, frame and cell values;
* exports them and re-reads `library.json` / map JSON / map `.bin`;
* decodes the generated PNG (signature, IHDR, IDAT zlib stream, exact pixel values,
  including BGRA→RGBA order);
* asserts that truncated inputs raise the documented exceptions.

Set `CRYSTAL_SELFTEST_KEEP=1` to keep the fixtures for inspection.

