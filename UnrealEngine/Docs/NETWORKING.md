# Networking

The recreation uses a **native Unreal dedicated server** rather than the C#
Crystal server. This document explains the model and how it maps to the original
packet flow.

## Model

- The server is authoritative: it owns movement, combat, loot and experience.
- Clients send **intent** via RPCs; the server validates and replicates results.
- Standard UE replication handles relevancy, dormancy and (optionally) the
  Replication Graph for large player counts.

## Transport mapping

| Original Crystal packet | Direction | Unreal equivalent |
|---|---|---|
| `C.Walk` / `C.Run` | client→server | `AMirPlayerController::ServerMoveTo` |
| `C.Attack` | client→server | `AMirPlayerController::ServerAttack` |
| `C.Magic` | client→server | planned `ServerCastSpell` |
| `C.PickUp` | client→server | `AMirPlayerController::ServerPickup` |
| `S.ObjectPlayer` / `S.ObjectMonster` | server→client | `AMirCharacter` replication |
| `S.UserInformation` | server→client | `AMirPlayerState` replication |
| `S.MapInformation` / `S.NewMapInfo` | server→client | `AMirGameState::MapName` + map load |
| `S.ObjectMove` | server→client | replicated transform (`SetReplicateMovement`) |
| `S.ObjectHealth` / `S.ObjectDied` | server→client | replicated `CurrentHP` / action `Die` |

## Key design points

- **Direction + action are replicated enums** (`EMirDirection`, `EMirAction`) on
  `AMirCharacter`, with `ReplicatedUsing` callbacks so clients re-drive the
  sprite only on change.
- **Movement** uses `SetReplicateMovement(true)`; the server applies
  `AddMovementInput` and clients interpolate. For the authentic grid feel, the
  server should snap to tile centres (`AMirMapActor::WorldToTile`) and only then
  set `Walking`/`Running`.
- **High-frequency combat** (damage numbers, spell effects) should use
  `NetMulticast` cosmetic RPCs while the authoritative state stays in replicated
  properties, so late joiners still see correct HP/position.
- **Interest management**: for hundreds of players, enable the Replication
  Graph plugin and grid-based relevancy keyed on the Mir map.

## Not yet implemented

- Account/login/character-select (original `LoginScene` / `SelectScene`).
- Zone hand-off between Mir maps (`MapInfo` transitions).
- Anti-cheat validation of movement speed and attack range (server currently
  trusts the client's `ServerMoveTo` target — add server-side rate limits and
  path validation before shipping).
