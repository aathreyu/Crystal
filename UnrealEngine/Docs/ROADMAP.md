# Roadmap

Phased plan. Phase 0–2 are scaffolded in this repo; later phases are the bulk of
the gameplay work.

## Phase 0 — Project setup ✅
- [x] UE5 C++ project (`MirUE.uproject`, targets, module, config)
- [x] Core enums mirrored from `Shared/Enums.cs` (`Core/MirTypes.h`)

## Phase 1 — Asset pipeline ✅ (tooling) / ◻ (data)
- [x] `.Lib` v3 reader + PNG/atlas/JSON export (unit-tested)
- [x] `.map` type-100 reader + JSON export (unit-tested)
- [ ] Convert the full shipped asset set (Monsters, Items, MapLibs, Magic, ...)
- [ ] Batch-conversion script + `MapLibs` index resolution
- [ ] Optional: an Unreal editor import utility that wraps the converter

## Phase 2 — Isometric rendering ✅ (scaffold)
- [x] 48×32 isometric projection + camera
- [x] Tile renderer with per-layer instancing
- [x] Character sprite + `FrameSet` animation
- [ ] Tile/material assets + lighting (`LightSetting`, weather, fishing cells)
- [ ] Depth sorting parity with the original (objects interleaved with tiles)

## Phase 3 — Networking & server ◻
- [x] Replicated player state / character / game state, input RPCs
- [ ] Grid-snapped movement + server validation
- [ ] Account login, character select, map transitions
- [ ] Replication Graph for scale

## Phase 4 — Core gameplay ◻
- [ ] 5 classes with class-specific stats and skill trees
- [ ] ~120 spells (`Spell` enum) with targeting, cooldowns, buffs
- [ ] Buffs / poison / status effects
- [ ] Inventory, equipment (14 slots), storage, gold
- [ ] Items + drop tables, monsters and AI behaviours
- [ ] NPC dialogue, shops, quests
- [ ] Groups, guilds, heroes, mounts, fishing, crafting

## Phase 5 — Meta & polish ◻
- [ ] UI (chat, inventory, minimap, character sheet) — UMG
- [ ] Sound (`SoundRules.txt` index mapping)
- [ ] Persistence (character/DB layer)
- [ ] Siege, conquest, PvP rules
