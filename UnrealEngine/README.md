# Legend of Mir 2 (Crystal) → Unreal Engine

A recreation of the classic 1999 isometric MMORPG **The Legend of Mir 2** in
**Unreal Engine 5**, built from the open-source Crystal engine in this
repository.

## Chosen direction

> **2D isometric UE client + a brand-new native Unreal (C++) dedicated server**,
> reusing the original `.Lib` / `.map` assets through a conversion pipeline.

This keeps the authentic 2D look of the original while replacing the C#
authoritative server with Unreal's replication model.

## Layout

```
UnrealEngine/
├── Docs/                      Design + pipeline documentation
├── Tools/MirAssetConverter/   Python converter for .Lib and .map (validated)
└── MirUE/                     Unreal Engine 5 C++ project
    ├── MirUE.uproject
    ├── Config/                Engine/Game/Input defaults
    └── Source/MirUE/
        ├── Core/              MirTypes.h - enums mirrored from Shared/Enums.cs
        ├── Assets/            MirAssetSubsystem - loads converted atlases at runtime
        ├── Player/            MirCharacter, MirPlayerController, MirIsometricCameraActor
        ├── World/             MirMapActor - isometric tile renderer
        └── Game/              MirGameMode / MirPlayerState / MirGameState
```

## Quick start

1. **Convert assets** (no game files required to test the tool):
   ```bash
   cd Tools/MirAssetConverter
   python convert.py selftest            # validate the converter
   python convert.py lib  <path>/Monsters.Lib  <proj>/Content/MirData/Monsters --atlas
   python convert.py map  <path>/0.map          <proj>/Content/MirData/Maps/0.json
   ```
2. **Open the project**: right-click `MirUE/MirUE.uproject` → *Generate Visual
   Studio project files* → build → open in the editor (UE 5.3+).
3. Create the small material assets described in `Docs/ASSET_PIPELINE.md`, then
   point `AMirMapActor.MapLibraryNames` and `AMirGameMode.StartupMapJson` at your
   converted data.

## Status

| Area | State |
|---|---|
| `.Lib` / `.map` converter | ✅ working, unit-tested (`python convert.py selftest`) |
| UE project scaffold + build files | ✅ |
| Isometric tile renderer | ✅ (needs the documented tile material asset) |
| Character/pawn + sprite animation | ✅ (needs the documented sprite material asset) |
| Isometric camera + input | ✅ |
| Server-authoritative replication | ✅ scaffolded (game mode/state, RPCs) |
| Combat, spells, inventory, AI, quests | ⛔ not yet (see `Docs/ROADMAP.md`) |

See `Docs/ROADMAP.md` for the phased plan and `Docs/ARCHITECTURE.md` for how the
systems map onto the original engine.
