# Asset pipeline

Two steps: **convert** the proprietary Crystal assets with the Python tool, then
**consume** the output at runtime in Unreal.

## 1. Convert

`Tools/MirAssetConverter` reads `.Lib` (MLibrary v3) and `.map` (type 100)
files. See `Tools/MirAssetConverter/README.md` for the exact binary layouts.

```bash
python convert.py lib <src>/Monsters.Lib  <proj>/Content/MirData/Monsters --atlas
python convert.py map <src>/0.map         <proj>/Content/MirData/Maps/0.json
```

Output for a library:

```
Content/MirData/Monsters/
├── atlas.png        # shelf-packed RGBA sprites
├── atlas.json       # {"width","height","sprites":[{index,x,y,width,height}],"frames":{...}}
├── library.json     # per-image metadata (size + draw offset) and frames
└── img_XXXX.png     # individual frames (optional, handy for debugging)
```

Output for a map: `<name>.json` with `width`, `height`, `cellSize` and a
column-major `cells[x][y]` grid.

## 2. Consume at runtime

`UMirAssetSubsystem` loads `atlas.png` via `FImageUtils::ImportBufferAsTexture2D`
and parses `atlas.json` with the built-in JSON serializer. It exposes
`GetAtlasTexture`, `GetSpriteUV`, `GetSpriteSize` and `GetFrame`. No manual
`.uasset` import is required, which keeps the converted data portable.

## Required material assets (one-time, in-editor)

The renderer draws sprites on quads and needs two tiny materials. Create them
once in `Content/Materials/`:

### `M_MirSprite` (characters/effects) — used by `AMirCharacter`
A material with:

| Parameter | Type | Purpose |
|---|---|---|
| `SpriteTexture` | Texture parameter | the library atlas |
| `UVMin` | Vector | sprite UV min (`.xy`) |
| `UVMax` | Vector | sprite UV max (`.xy`) |

Graph: `TextureSampleParameter2D(SpriteTexture)` → multiply its UVs so they map
`[UVMin, UVMax]` → `Emissive Color`, with `Opacity = texture alpha`, and
**Blend Mode = Masked** (or Translucent for effects), **Shading Model =
Unlit**, **Two Sided = true**.

### `M_MirTile` (map tiles) — used by `AMirMapActor`
Same idea, but the UV rect comes from **per-instance custom data**
(`NumCustomDataFloats = 4`; data index 0..3 = UVMin.xy, UVMax.xy). Use a
`VertexInterpolator`/`Custom` node reading `PerInstanceCustomData` to build the
UV rect. Blend Mode = Masked, Shading Model = Unlit.

Assign `M_MirSprite` to a Blueprint child of `AMirCharacter` and `M_MirTile` to
`AMirMapActor.TileMaterial`.

## Data locations

| Data | Where |
|---|---|
| Converted atlases/maps | `<Project>/Content/MirData/**` |
| Library name → folder map | `AMirMapActor.MapLibraryNames` (int → FName) |
| Startup map | `AMirGameMode.StartupMapJson` |

## Caveats

- Pixel data is BGRA in the source; the converter emits standard RGBA PNGs.
- Negative draw offsets (`X`/`Y` in `library.json`) are the engine's sprite
  anchoring and are preserved for faithful placement.
- Large libraries (e.g. `Monsters`) may exceed one 2048² atlas; raise
  `--atlas-size` (e.g. 4096) or split per sub-range.
- The converter's own output is validated by `python convert.py selftest`.
