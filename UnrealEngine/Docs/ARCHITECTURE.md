# Architecture

How the original Crystal engine maps onto the Unreal Engine recreation.

## Original engine recap

| Crystal project | Responsibility |
|---|---|
| `Client/` | SlimDX/D3D9 client, WinForms immediate-mode UI, isometric renderer |
| `Server/` | Authoritative simulation: movement, combat, AI, loot, quests, guilds |
| `Shared/` | Packet definitions, enums, data structures shared by both |

The client is a **dumb renderer + input**: it sends intent (`C.Attack`,
`C.Move`, `C.Magic`, ...) and renders what the server tells it
(`S.ObjectPlayer`, `S.ObjectMonster`, ...). Movement/combat resolution, hit
detection, loot and experience all happen server-side.

## Unreal mapping

| Original concept | Unreal Engine equivalent |
|---|---|
| Server authoritative simulation | **Dedicated Unreal server** (`AGameModeBase`, `AGameStateBase`) |
| `Connection` / `PlayerObject` | `AMirPlayerState` + `AMirCharacter` (replicated) |
| Packet streams (`ServerPackets`/`ClientPackets`) | **UE property replication + RPCs** |
| Client-side prediction/interp | `SetReplicateMovement` + `UPROPERTY(ReplicatedUsing=...)` |
| `MirDirection` / `MirAction` | replicated enums on `AMirCharacter` |
| Map cells (`CellInfo`) | `FMirMapCell` + `AMirMapActor` instances |
| `MLibrary` sprite draw | `UMirAssetSubsystem` atlas lookup + per-instance UV |
| `FrameSet` animation | `FMirAnimFrame` (Start/Count/Skip/Interval) |

### Why RPCs instead of the original packet protocol
The Crystal server is C#/.NET; the recreation uses a native Unreal dedicated
server, so the wire protocol is Unreal's. This is deliberate: it gives us
server-authoritative movement, relevancy, and lag compensation for free. The
original packet *semantics* (which actions exist, what they carry) are preserved
in `Core/MirTypes.h` and the RPC signatures on `AMirPlayerController`.

### Isometric projection
The original draws on a 48×32 isometric grid. The recreation reproduces this
exactly:

- `MirIso::CellWidth = 48`, `CellHeight = 32`.
- `AMirMapActor::TileToWorld` implements the classic
  `x = (tx - ty) * 24`, `y = (tx + ty) * 16` transform.
- `AMirIsometricCameraActor` uses an orthographic camera at yaw −45° and
  pitch −atan(32/48) ≈ −33.69°, so a tile appears as a 3:2 diamond.

### Sprite selection
Mir sprites are pre-rendered per direction. For a library frame with
`Start`, `Count`, `Skip`, the sprite index is:

```
index = Start + (direction * (Count + Skip)) + currentFrame
```

This is implemented in `AMirCharacter::RefreshSprite`, matching the original
`BodyLibrary.Draw(...)` indexing.

## Class responsibilities

- **`UMirAssetSubsystem`** (`UGameInstanceSubsystem`): loads converted atlases
  (`atlas.png` + `atlas.json`) at runtime, caches textures/rects/frames.
- **`AMirCharacter`**: replicated pawn; owns direction/action state and drives
  the billboard sprite.
- **`AMirPlayerController`**: local input, cursor→world picking, server RPCs.
- **`AMirIsometricCameraActor`**: fixed isometric camera that follows a target.
- **`AMirMapActor`**: parses converted map JSON and builds tile instances.
- **`AMirGameMode`**: spawns the map, assigns player classes, seeds player state.
- **`AMirGameState`** / **`AMirPlayerState`**: replicated world/player data.
