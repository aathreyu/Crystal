# Recreating the Crystal client in Unreal Engine 5.8

A concrete porting plan derived from *this* repository (Crystal / Legend of Mir 2).
Every number and format below was read out of the code, with file references so you can verify.

---

## 0. TL;DR

1. **Do not port the renderer.** The existing client is a WinForms app that blits
   `.Lib` sprites with Direct3D 9 (SlimDX). None of it survives in UE, and almost
   none of it *needs* to: the server is a separate authoritative process that you keep untouched.
2. **Your real contract is the TCP protocol** (`Shared/Packet.cs`,
   `Shared/ClientPackets.cs`, `Shared/ServerPackets.cs`) plus **the asset formats**
   (`.Lib` v3, `.map`). Everything else is a rewrite.
3. **Build tooling first**: a `.Lib`/`.map` exporter (C# console app that reuses the
   parsing rules already in the repo) and a packet codegen/test harness. That tooling
   de-risks ~70% of the project; the UE client is then "just" a sprite game + 39 dialogs.
4. **Realistic effort for parity**: ~9–14 months single dev, ~3–5 months for a small
   team. A playable vertical slice (log in → walk a map → chat → hit a mob) is ~4–6 weeks.

---

## 1. What you are actually working with

Measured from the repo:

| Area | Files | Lines | Notes |
|---|---:|---:|---|
| `Client/MirScenes` | 39 | 42,347 | `GameScene.cs` alone ~11k lines; 38 files are dialogs |
| `Client/MirObjects` | 15 | 15,379 | `PlayerObject`, `MonsterObject`, `UserObject`, `MapObject`, `PathFinder` |
| `Client/MirControls` | 17 | 7,835 | hand-rolled immediate-mode widget toolkit |
| `Client/MirGraphics` | 5 | 2,444 | `DXManager`, `MLibrary`, particles |
| `Forms`, `MirSounds`, `Resolution`, `MirNetwork` | 15 | 3,806 | game loop, NAudio, 255-line socket |
| **Client total** | **97** | **73,325** | |
| `Shared` | 21 | ~13k | packets, enums, `Language.cs` (4.3k) |
| `Server` | 276 | — | keep it; it is the source of truth |

Stack facts that matter:

* Rendering: **Direct3D 9 via SlimDX**, immediate-mode, straight-alpha sprites, no
  scene graph, no z-buffer sorting (paint order is manual: back → middle → objects by
  cell → front → effects).
* Loop: `Application.Idle` driven, FPS capped (`Settings.MaxFPS`, default 100; `Client/Forms/CMain.cs`).
* UI: GDI+/WinForms controls in **absolute pixels on a 1024×768 canvas**
  (`Settings.ScreenWidth/Height = 1024/768`), Arial 8pt with drop shadow.
* Audio: NAudio, `.wav` from `./Sound`, max 3 overlapping (`Settings.SoundOverLap`).
* Network: **plain TCP**, 4-byte header + little-endian `BinaryWriter` payload, **no encryption**.
* Assets: `./Data/**/*.Lib` (v3: gzip'd BGRA + frame metadata), `./Map/**.map`
  (8 historical formats, auto-detected), `./Sound/*.wav`, `./Localization/*.json`.
* Build: `net8.0-windows7.0`, x64, Windows-only (SlimDX + WinForms).

**Implication:** the server, database, patcher and tools stay. You are writing a new
front-end against a fixed protocol that is fully specified by the C# source.

---

## 2. Pick a strategy

| Strategy | What it means | Verdict |
|---|---|---|
| **A. UE front-end, C++ protocol client** (recommended) | New UE 5.8 project; re-implement socket + packets + rendering + UI in C++/UMG/BP; Crystal server unchanged. | Most work, cleanest result, best long-term. |
| **B. Hybrid with embedded .NET** (UnrealSharp / UnrealCLR / USharp) | Host `Shared.dll` + ported client logic in C#, call into UE for rendering/UI. | Saves protocol + dialog logic porting, but adds a third-party runtime dependency (verify 5.8 support), complicates packaging/consoles, and you *still* rewrite rendering and UI. Only if you must ship fast and Windows-only. |
| **C. Rewrite server too** | — | No. Throws away a working, community-hardened server and DB schema. |

Recommended sequence regardless of choice: **assets → protocol → rendering → UI →
systems**, because later stages depend on earlier formats.

---

## 3. Target architecture in UE 5.8

Module split (start with one `Mir` module; split when compile times hurt):

```
Source/
  MirCore/     # protocol: reader/writer, packet structs, codec, dispatcher (keep UE deps out)
  MirNet/      # FSocket thread, framing, reconnect, keepalive
  MirAssets/   # runtime .Lib / .map loaders OR generated asset registry
  MirRender/   # sprite batch, ortho camera, sorting, light tint
  MirUI/       # UMirWidget base + MirControl equivalents
  MirGame/     # actors, game mode, Login/Select/Game state machine
```

### Class mapping (C# → UE)

| Crystal | Unreal | Notes |
|---|---|---|
| `Program` / `CMain` | `AMirGameMode`, `UGameInstanceSubsystem` | fixed-tick driver, config |
| `MirScene` (Login/Select/Game) | `UMirSceneWidget` + `AMirPlayerController` state enum | scene = widget screen + input mode |
| `MirControl` + 16 subclasses | `UMirWidget : UUserWidget` subclasses | keep the same API surface so dialog code ports 1:1 |
| `MapObject` / `PlayerObject` / `MonsterObject` | `AMapActor` base + `APlayerActor`, `AMonsterActor`, `AItemActor`, `ANpcActor`, `AMissileActor` | or one `AMapActor` with data components |
| `DXManager` | `UMirRenderSubsystem` + fixed ortho camera actor | screen-space 2D pipeline |
| `MLibrary` / `MImage` | `UMirSpriteLibrary` (runtime) or `UPaperSprite` assets (offline) | see §5 |
| `Network` | `FMirSocket : FRunnable` | thread → `TQueue` → game thread |
| `PathFinder` | `UMirPathfinder` (pure C++) | A* on the cell grid, same costs |
| `SoundManager` | `UMirAudioSubsystem` (component pool) | wav → `USoundWave` |
| `Particle` / `FogParticle` | Niagara systems | effects are additive sprites |
| `GameLanguage` | `FText` + String Tables | reuse `Localization/*.json` keys |

### Frame flow

```
Socket thread ──(parsed packets)──> TQueue<FMirPacketPtr, EQueueMode::Spsc>
                                        │ drain per tick
                                        ▼
                              UMirGameSession::ProcessPacket()
                                   ├─> state update (authoritative copy)
                                   ├─> actor spawn/update (render state)
                                   └─> UI event bus
```

Keep the **game-thread tick decoupled from network cadence** (the original drains the
whole queue per frame; do the same, but cap packets/tick to avoid stalls).

---

## 4. The wire protocol (your hard contract)

Source: `Shared/Packet.cs` (`GetPacketBytes`, `ReceivePacket`),
`Shared/Functions/Functions.cs`.

### 4.1 Framing

```
offset 0 : uint16  total length, INCLUDING these 4 header bytes   (little endian)
offset 2 : int16   packet id                                      (little endian)
offset 4 : payload (length - 4 bytes)
```

`ReceivePacket` requires `length >= 2` (and `>= 4` to be useful) and never trusts
`length` beyond the buffer; leftover bytes carry to the next read. A packet is
**consumed whole or not at all** — replicate that.

### 4.2 Payload encoding

`BinaryWriter`/`BinaryReader` semantics:

| Type | Encoding |
|---|---|
| `byte`, `sbyte`, `bool` | 1 byte (bool 0/1) |
| `short`, `ushort`, `int`, `uint`, `long` | fixed width, little endian |
| `string` | **7-bit encoded byte length**, then UTF-8 (NOT null-terminated, NOT int32 length) |
| `byte[]` / `T[]` | `int32` count, then items (unless a packet writes another scheme — check each) |
| `List<T>` | `int32` count, then items |
| `DateTime` | `long` from `DateTime.ToBinary()` (`writer.Write(dt.ToBinary())`), not ticks |
| `enum` | width declared in `Shared/Enums.cs` (mostly `byte` or `short`) |

> ⚠️ The 7-bit length prefix is the #1 re-implementation bug. `FString` +
> `SerializeAsANSIString` or a naive `int32` length desyncs the stream and the server
> will drop you instantly.

### 4.3 Compression

`Packet.Compressed` (virtual, per packet) → payload is **gzip**'d
(`Functions.CompressBytes` / `DecompressBytes`). The 4-byte header length is the
*compressed* size. In this revision only **one** packet sets it (`S.NPCGoods`, shop
stock lists), but implement the flag generically in both directions — server forks add
more, and the framing code should not know packet names.

### 4.4 Handshake (exact, from `LoginScene.cs` + `Server/MirNetwork/MirConnection.cs`)

```
S: Connected                 (id 0, empty)
C: ClientVersion { hash }    (hash = MD5 of the client executable, 16 bytes)
S: ClientVersion { Result }  (1 = ok, 0 = wrong version → server disconnects)
   ... Stage = Login
C: Login { AccountID, Password }
S: LoginSuccess { ... }  →  C: StartGame  →  Select/Game scenes
```

* The MD5 gate is server-side (`Server/Settings.cs`: `CheckVersion` + `VersionPath`,
  a comma-separated list of files hashed with MD5 at boot). Either register your UE
  binary's MD5, or set `CheckVersion=false` while developing.
* Keep-alive: client sends `C.KeepAlive` when idle for `Settings.TimeOut` (5000 ms);
  server echoes `S.KeepAlive { Time }`. The client also hard-times-out the connection
  after 5 s without reaching "connected".
* Unhandled packet id → `ReceivePacket` returns null → server **discards the whole
  buffered stream** for that connection (`extra = new byte[0]`). A malformed client
  looks exactly like a flood of disconnects; log ids aggressively.

### 4.5 Packet inventory

Generated from `Shared/Enums.cs` and cross-checked against the dispatchers in
`Shared/Packet.cs` → see **[`packet_ids.csv`](./packet_ids.csv)** (153 client→server
ids, 279 server→client ids; 152/278 `case` branches actually exist).

Two enum entries have **no dispatcher case** in `Packet.cs`
(`ClientPacketIds.PurchaseGuildTerritory`, `ServerPacketIds.RefineCancel`). Treat
`GetClientPacket`/`GetServerPacket` (not the enum) as the authoritative list, and
verify those two against a running server before relying on them.

### 4.6 Generating the C++ side

Do **not** hand-write ~430 packets and hope. Two workable approaches:

1. **IL-driven codegen (best breadth).** Load the built `Shared.dll`, reflect all
   `Packet` subclasses, read `ReadPacket`/`WritePacket` with Mono.Cecil, translate the
   `BinaryReader`/`BinaryWriter` call sequence into a small op list, emit C++
   `Read`/`Write`. The sequences are very regular (`Write(field)`, loops over
   `List<T>`), so a ~600-line generator covers ~95% and flags the rest for review.
2. **Golden fixtures (best confidence, less code).** Add a C# test harness that builds
   each packet with deterministic values, serialises via `GetPacketBytes()`, writes
   `.bin` + expected-decoding JSON. A UE automation test decodes each `.bin` and
   compares. This catches endianness, 7-bit strings, `DateTime.ToBinary()` and gzip
   bugs immediately, and runs in CI without a server.

Do both if you can: codegen for breadth, fixtures for evidence.

### 4.7 Minimal C++ framing sketch

```cpp
// Strict, bounds-checked BinaryReader equivalent.
struct FMirReader
{
    const uint8* P = nullptr;
    int32 Left = 0;
    bool bError = false;

    bool ReadBytes(void* Out, int32 N)
    {
        if (N < 0 || Left < N) { bError = true; return false; }
        FMemory::Memcpy(Out, P, N); P += N; Left -= N; return true;
    }
    int32 ReadInt32() { int32 V = 0; ReadBytes(&V, 4); return V; }
    FString ReadString()                       // 7-bit byte length + UTF-8
    {
        int32 Len = 0, Shift = 0; uint8 B = 0;
        do { if (!ReadBytes(&B, 1)) return FString(); Len |= (B & 0x7F) << Shift; Shift += 7; }
        while (B & 0x80);
        if (Len < 0 || Left < Len) { bError = true; return FString(); }
        FString S(FUTF8ToTCHAR(reinterpret_cast<const ANSICHAR*>(P), Len));
        P += Len; Left -= Len; return S;
    }
};
```

```cpp
// Framing loop (runs on the socket thread).
void FMirSocket::OnBytes(const uint8* Data, int32 Num)
{
    Buffer.Append(Data, Num);
    int32 Off = 0;
    while (Buffer.Num() - Off >= 4)
    {
        const uint16 Len = uint16(Buffer[Off]) | (uint16(Buffer[Off + 1]) << 8);
        if (Len < 4) { OnProtocolError(TEXT("bad length")); return; }
        if (Buffer.Num() - Off < Len) break;                  // wait for the rest
        const int16 Id = int16(uint16(Buffer[Off + 2]) | (uint16(Buffer[Off + 3]) << 8));

        TArray<uint8> Payload;
        if (IsCompressedPacket(Id))                           // gzip
            Payload = Gunzip(Buffer.GetData() + Off + 4, Len - 4);
        else
            Payload.Append(Buffer.GetData() + Off + 4, Len - 4);

        Incoming.Enqueue(MakeShared<FMirPacket>(Id, MoveTemp(Payload))); // decode on game thread
        Off += Len;
    }
    if (Off) Buffer.RemoveAt(0, Off, EAllowShrinking::No);
}
```

Decode on the **game thread** (or a worker pool), never inside the recv callback, so
actor/UI mutations stay single-threaded like the original.

---

## 5. Asset pipeline (do this first — it unblocks everything)

### 5.1 `.Lib` version 3 layout

From `Client/MirGraphics/MLibrary.cs` (`Initialize`, `MImage`, `CreateTexture`):

```
int32  version          // must be >= 2; current is 3
int32  count            // image count
int32  frameSeek        // only if version >= 3: absolute offset of the FrameSet block
int32[count] offsets    // absolute file offsets of each image record

at frameSeek (v3):
  int32 frameCount
  frameCount × { uint8 mirAction; Frame frame }
    Frame = int32 Start, Count, Skip, Interval,
                   EffectStart, EffectCount, EffectSkip, EffectInterval,
                   bool Reverse, bool Blend        // 32 bytes + 2 bools

image record at offsets[i]:
  int16 Width, Height
  int16 X, Y              // draw offsets (pivot), applied when offSet == true
  int16 ShadowX, ShadowY
  uint8 Shadow            // bit 7 set => a second (mask) layer follows
  int32 Length            // compressed byte length of layer 1
  byte[Length] gzip(BGRA) // exactly Width*Height*4 bytes decompressed, A8R8G8B8
  if (Shadow & 0x80):
    int16 MaskWidth, MaskHeight
    int16 MaskX, MaskY
    int32 MaskLength
    byte[MaskLength] gzip(BGRA)
```

Notes that bite:

* Pixels are **BGRA**, uncompressed size exactly `W*H*4`, rows top-down, **no**
  stride padding (one image can be 1000×1000 → 4 MB; a whole library can be hundreds
  of MB decompressed — the original keeps everything in RAM for 10 minutes,
  `Settings.CleanDelay`).
* The mask layer is not alpha; it is a second sprite drawn with additive/alpha
  blending (weapon glows, etc.). Import it as a separate texture and draw it with the
  same pivot.
* Frame `Skip` exists for actions whose sprites are interleaved with other actions.

### 5.2 Exporter (implemented)

**Status: done — see [`Tools/CrystalAssetExport`](../../Tools/CrystalAssetExport/README.md).**
It is a dependency-free .NET 8 console app (no SlimDX, no `System.Drawing`) that ports
the `.Lib` v2/v3 reader and all nine map loaders, writes PNGs + `library.json`
(pivots, masks, frame tables) and map grids as JSON or a compact `.bin`, and ships a
`selftest` command with 72 assertions over synthetic fixtures.

Do not reuse `Client/MirGraphics/MLibrary.cs` directly (it is welded to D3D9) — the
tool re-implements its ~80 lines of parsing, which is what you want for any port.

Output per library:

```
out/Data/Items/0001/00001.png
out/Data/Items/0001/00001_mask.png
out/Data/Items/0001/library.json  { "version": 3, "images": [ { "index": 1, "width": 64,
                                    "height": 48, "x": 12, "y": -8, "shadowX": 0,
                                    "shadowY": 0, "mask": { ... } } ],
                                    "frames": { "Standing": { "start": 0, "count": 4,
                                                "skip": 0, "interval": 450, ... } } }
```

Then either:

* **Offline import** (recommended for shipping): batch-import PNGs into
  `UTexture2D` with `TF_BGRA8`/`BC7`, `MipGenSettings=NoMipmaps` for UI,
  `Filter=TF_Nearest` at the sampler, `sRGB` on, and build `UPaperSprite`-like
  assets from a JSON manifest. Deterministic, pak-friendly, no runtime parsing.
* **Runtime loading** (recommended if you want mod/data-pack compatibility): ship a
  `FArchive`-based `UMirSpriteLibrary` that streams `.Lib` directly, creates
  `UTexture2D` with `CreateTransient` per image on demand, with an LRU budget.
  This keeps your client compatible with existing Crystal data packs — a big win for
  servers that distribute assets.

Practical middle ground: **runtime `.Lib` for world content** (monsters, maps, player
armour/weapons — the stuff that changes per server) and **baked assets for UI**
(Prguse/UI_32bit), which is stable and benefits from atlas packing.

### 5.3 Maps

`Client/MirObjects/MapCode.cs` auto-detects **8 map formats** by magic bytes and
loads them into `CellInfo[,]` (see `LoadMapType0..7`). Cell grid is **48×32 px**
(`MapControl.CellWidth/CellHeight` in `GameScene.cs`), layers = Back / Middle / Front,
plus door index/offset, front & middle animation frames/ticks, `TileAnimationImage/
Offset/Frames`, `Light`, and (in newer formats) `FishingCell`.

Export a neutral JSON per map:

```json
{ "w": 300, "h": 300, "cells": [ { "b": [lib,img], "m": [lib,img], "f": [lib,img],
  "door": 0, "doorOff": 0, "fAnim": [frame, tick], "mAnim": [frame, tick],
  "ani": [img, off, frames], "light": 0 } ] }
```

Rendering rules you must preserve (from `GameScene.cs` ~10,700–10,940):

* Tiles are 48×32; some are 96×64 (2×2) — detect by library `GetSize` and draw once
  per 2 cells, or you get seams.
* `FrontIndex != 0` cells draw after objects, offset by `- Height` (tall tiles overlay
  actors).
* `MapLibs[28]` special-cases a `CellHeight Up` offset; `MapLibs[190]` is the animated
  tile library.
* Draw order: back layer → middle → objects sorted per cell → front layer → effects →
  lighting masks.

In UE do **not** bake maps into `.umap` files. Load the JSON and build a tile
renderer at runtime (see §6); this mirrors the original, supports the map editor, and
avoids thousands of actors.

### 5.4 Sound and text

* `./Sound/*.wav` → `USoundWave` import; `Client/MirSounds/SoundList.cs` is the name
  enum. Keep `SoundOverLap = 3` semantics with a small component pool.
* `Client/Localization/{English,Chinese}.json` + `Shared/Language.cs` → UE String
  Tables (`FText`, namespaced keys). Do not hard-code strings in C++; the server also
  localises its own messages (`ServerTextKeys`), so keep the two tables separate.

---

## 6. Rendering: making UE behave like a 2D isometric sprite game

This is where most ports go wrong, because UE defaults are 3D and "2D in 3D" defaults
are wrong for this game.

**Camera.** Orthographic, fixed axis, no rotation: `ProjectionMode = Orthographic`,
`OrthoWidth = 1024` (matching `Settings.ScreenWidth`), locked to the player's cell with
the original offset rule (`OffSetX = ScreenWidth / 2 / 48`,
`OffSetY = ScreenHeight / 2 / 32 - 1`). Never let the camera smoothing differ from the
sprite offsets, or actors "swim" against the tiles.

**World scale.** 1 cell = 48×32 "pixels", 1 UU = 1 pixel is fine (ortho width in
pixels), sprites at PPU = 1. Keep the 48/32 ratio exact — diamond tiles depend on it.

**Sorting (the hard part).** Crystal sorts manually: by cell, then by the stack in
`CellInfo.CellObjects`. UE sorts translucent primitives automatically and will fight
you. Options, best first:

1. **Per-actor sprites + explicit sort priority.** One `UPaperSpriteComponent` (or a
   single quad) per actor; each frame set `SetTranslucencySortPriority(CellX + CellY)`
   and `TranslucencySortDistanceOffset`. With ~100–250 visible actors this is fine and
   trivially debuggable.
2. **One instanced mesh for tiles, one for actors.** Fast, but instanced translucency
   sorting is per-component, so you end up with one component per sort "row".
3. **Custom sprite batch** (single `FPrimitiveSceneProxy` writing sorted quads with a
   custom vertex factory). Best perf and exact parity with the original paint order,
   highest cost. Only if you must hit 300+ fps.

Either way keep the **object sort key identical to the original** (Y, then X, then the
cell stack), and never rely on depth testing for overlap.

**Material.** Unlit, straight alpha, no specular, no shadow maps:

* `Shading Model: Unlit`, `Blend Mode: Translucent`, `Two Sided: false`, `Responsive
  AA` off, sampler `Nearest`.
* Multiply by a **per-instance tint** to reproduce lighting (map light + day/night +
  object glow). `DXManager` computes light masks; matching the *values* is easy,
  matching the *masks* is optional polish.
* Shadows are baked into sprites (`ShadowX/ShadowY`), not UE shadow maps. Disable
  dynamic shadows on these actors.

**Project settings that matter:**

* Anti-aliasing: **TSR/TAA will ghost sprites** → None or FXAA.
* Motion blur off, Lumen off (unlit world), Auto Exposure off (fixed), Screen
  Percentage 100, bloom off unless wanted for effects.
* Nearest filtering; no mipmaps for UI.
* Fixed frame rate cap (60/100) so movement interpolation matches the original.

**Effects/weather.** `ParticleEngine.cs`, `FogParticle.cs`, `Weather` library are
additive camera-facing sprites → Niagara sprite renderer with additive material using
the same sprite indices. `Effect`/`Magic` libraries are animation atlases, not
particles; treat them as sprite sheets.

**World text.** Damage numbers, names, chat bubbles → screen-space widget components or
a retained Slate text layer; `UWidgetComponent` in world space is heavier than the
original.

---

## 7. UI (39 dialogs, ~31k lines — the long pole)

`Client/MirControls` is a self-contained widget toolkit: `MirControl` (base),
`MirImageControl`, `MirAnimatedControl`, `MirButton`, `MirAnimatedButton`, `MirLabel`,
`MirScrollingLabel`, `MirTextBox`, `MirInputBox`, `MirCheckBox`, `MirDropDownBox`,
`MirItemCell`, `MirGoodsCell`, `MirGameShopCell`, `MirAmountBox`, `MirMessageBox`,
`MirScene`.

Recommended approach: **recreate that toolkit in UMG, then port dialog code ~1:1.**

* `UMirWidget : UUserWidget` with the same members (`Location`, `Size`, `Visible`,
  `Enabled`, `Parent`, `DrawImage(index)`, `Text`, click delegates…). The C# dialogs are
  imperative `new MirLabel { Parent = this, Location = new Point(…) }` code — if the
  API matches, porting is mechanical search/replace, not redesign.
* Layout: absolute pixel coordinates on a 1024×768 canvas. Use a Canvas Panel inside a
  Scale Box (or a custom `UMirCanvas` scaling by `ScreenWidth/1024`) so coordinates stay
  valid at any resolution.
* Font: Arial 8pt + 1px shadow in GDI. Use a Slate font with matching metrics or a
  bitmap font from the same TTF, otherwise labels measure wrong and layouts break.
* Item cells: share `MirItemCell` logic (grid, stack counts, durability bar, glow from
  `StateItem`, tooltip from `Items_Tooltip_32bit`).
* Message boxes, amount boxes and tooltips are re-entrant; keep the same modal stack or
  you will get input-state bugs.

Do **not** hand-build 38 dialogs in the UMG designer. Writing them in C++ against a
faithful `UMirWidget` API is faster and stays close to upstream changes.

Order: HUD (belt/chat/status) → inventory/character → NPC dialog + shop → trade/storage
→ everything else.

---

## 8. Gameplay/state systems to port

The server is authoritative; the client mirrors state and predicts movement. Port in
dependency order, not by user-visible size:

| Priority | System | Source | Notes |
|---:|---|---|---|
| 1 | Login / NewAccount / ChangePassword | `LoginScene.cs` + `S.Login*` | validates protocol + string encoding |
| 2 | Character select / create / delete | `SelectScene.cs` | list + preview sprites (`ChrSel`) |
| 3 | Map load + camera + movement | `MapControl`, `UserObject`, `PathFinder` | 48×32 grid, prediction, `OffSetMove` |
| 4 | Object lifecycle | `S.ObjectPlayer/Monster/Item/NPC`, `ObjectRemove`, `ObjectTurn/Walk/Run` | spawn, interp, cell stack |
| 5 | Combat + damage numbers | `S.ObjectAttack`, `DamageIndicator`, `HealthChanged`, `Struck`, `Death` | action/frame tables |
| 6 | Inventory / belt / equipment | `InventoryDialog`, `CharacterDialog`, `S.ItemInfo` | tooltips, durability, stacks |
| 7 | Chat + channels | `S.Chat`, `ChatType`, `MainDialogs` | shout/whisper/guild/announcement |
| 8 | NPC dialog / shop / repair / storage | `NPCDialogs`, `S.NPCGoods`, `S.NPCSell…` | script-driven pages |
| 9 | Trade / group / guild | `TradeDialogs`, `GroupDialog`, `GuildDialog` | multi-step handshakes |
| 10 | Magic / effects / animations | `Magic`, `Effect`, `MagicC`, `SpellObject` | biggest content surface |
| 11 | Quests / mail / hero / mount / fishing / pet / relationships / ranking / siege / gameshop | remaining dialogs | 1–3 packets each |

Movement detail worth copying **exactly**: `MapObject.Movement` is the *target* cell
while `OffSetMove` is the pixel interpolation toward it; the camera follows
`User.Movement + OffSetMove`. If you let UE's CharacterMovement drive this, you desync
from the server's turn/walk/run ticks and from other clients' rendering.

Animation: per-action frame ranges come from the `.Lib` v3 `FrameSet`
(`MirAction` → `Start/Count/Skip/Interval`), but body/weapon/hair *composition* and
monster frame counts are hard-coded in `PlayerObject.cs` / `MonsterObject.cs`
(e.g. `Frame(0, 4, 0, 450)` for standing). Port those tables into a DataTable rather
than scattering constants in C++ — do not re-derive them; they encode art-specific
quirks (`ArmourOffSet = -416` when mounted, alternate "AR" libraries for archer, …).

---

## 9. Milestones and effort

| # | Milestone | Deliverable / acceptance | Solo effort |
|---|---|---|---:|
| M0 | Tooling | `.Lib`→PNG+JSON exporter; `.map`→JSON/`.bin`; packet fixtures (`GetPacketBytes` dumps) | **done** (exporter) |
| M1 | Handshake | UE client connects to a stock Crystal server, passes the MD5 gate, logs in, lists characters | 2 wk |
| M2 | World | map renders correctly at 1024×768 ortho; player walks/turns with prediction; camera exact | 3–4 wk |
| M3 | Entities | other players/monsters/items animate, sort correctly, attacks + damage numbers | 4–6 wk |
| M4 | Core UI | HUD, chat, inventory, character sheet, item tooltips, message boxes | 6–8 wk |
| M5 | Economy | NPC dialogs, shop, storage, repair, trade | 6–8 wk |
| M6 | Magic | spell casting, effects, buffs, projectiles, weather | 6–8 wk |
| M7 | Long tail | quests, mail, group/guild, hero, mounts, fishing, pets, ranking, big map | 8–12 wk |
| M8 | Ship | patching/launcher, settings, keybinds, packaging, localization | 4 wk |

**Vertical slice (M1–M3 without dialogs) ≈ 4–6 weeks** and is what you should demo.

Team split with 3 devs: (a) assets + rendering, (b) protocol + gameplay, (c) UI. UI is
roughly half the total work; start it in parallel as soon as M2 renders.

---

## 10. Risk / gotcha checklist

1. **7-bit string length** and `DateTime.ToBinary()` — desync on first contact.
2. **gzip payloads** — detect via `Packet.Compressed`, not via size heuristics.
3. **Unknown packet id kills the connection** (server drops its whole buffer).
4. **MD5 version gate** — a new binary must be registered or `CheckVersion=false`.
5. **Translucency sorting** — assert exact overlaps (player behind a wall tile).
6. **2×2 tiles and front-layer offsets** — seams and wrong occlusion if ignored.
7. **8 map formats** — test one map from each family (Wemade/Shanda/Mir3/2010/AntiHack).
8. **`.Lib` memory** — unbounded decoding will OOM; use an LRU texture budget.
9. **GDI text metrics** — every layout assumes Arial 8pt measurement.
10. **1024×768 absolute coordinates** — scale the UI as a whole, not per control.
11. **Frame tables are art data** — copy them, don't infer them.
12. **Turn/walk/run timing** must match server ticks.
13. **No z-buffer assumptions** — tall objects are drawn by cell order, not height.
14. **Mouse picking is cell-based** (`MouseLocation / 48,32`), not raycasts.
15. **Patching** — repo ships `AutoPatcherAdmin(V2)`; UE `.pak` needs a new story, or ship
    data outside the pak (what most servers do).
16. **Anti-cheat is version-hash only** — don't add client trust the server lacks.
17. **Two localization tables** — client `Language.cs` vs server `ServerTextKeys`.
18. **Console/platform** — TCP, runtime file loading and GPL all constrain storefronts.
19. **Upstream drift** — pin the Crystal commit your codegen runs against.
20. **Don't gold-plate** — no Lumen, Nanite, World Partition or replication; this game
    needs none of it.

---

## 11. Licensing (read before writing code)

* This repository is **GPL v2** (README badge). A client derived from its code — ported
  `Shared` packet definitions, `PathFinder`, frame tables, dialog logic — is a derivative
  work and must be distributed under GPL v2 with source.
* The **art, maps, sounds and names are Wemade/Actoz IP**. Reimplementing the client
  grants no rights to the assets; private-server distribution is already legally grey and
  a UE remake does not change that. For anything commercial: original art + legal advice.
* If you need a non-GPL client, the cleanest split is: implement the protocol/formats from
  observed behaviour and format specs (facts, not copied code), replace all art, and talk
  to the GPL server only over the wire.

---

## 12. First three commits (concrete)

1. **DONE** — `Tools/CrystalAssetExport`: parses `.Lib` v2/v3 and all `.map` formats
   (ported from `MLibrary` + `MapCode.LoadMapType0..7/100`), emits PNG + `library.json`
   and map `.json`/`.bin`. Verified by `dotnet run --project Tools/CrystalAssetExport -- selftest`.
   See [`Tools/CrystalAssetExport/README.md`](../../Tools/CrystalAssetExport/README.md).
2. `Source/MirCore` + `Source/MirNet` — reader/writer, framing, `ClientVersion` + `Login`
   handshake, plus the golden-fixture tests from §4.6. Acceptance: connect to a stock
   server and reach the first character-select packet.
3. `Source/MirRender` — ortho camera, `.Lib` runtime texture cache, back/middle/front tile
   pass for one map. Acceptance: a screenshot that matches the original client at the same
   coordinates.

---

## Appendix: quick file reference

| Purpose | Path |
|---|---|
| Packet framing / dispatch | `Shared/Packet.cs` |
| Packet bodies | `Shared/ClientPackets.cs`, `Shared/ServerPackets.cs` |
| Packet ids, enums, `MirAction` | `Shared/Enums.cs` |
| Sprite library reader | `Client/MirGraphics/MLibrary.cs` (v3 `MImage`) |
| Renderer / light / particles | `Client/MirGraphics/DXManager.cs`, `ParticleEngine.cs` |
| Map parsing (8 formats) | `Client/MirObjects/MapCode.cs` |
| Isometric draw order | `Client/MirScenes/GameScene.cs` (`MapControl`, ~10,270–11,300) |
| Object state / interpolation | `Client/MirObjects/{MapObject,UserObject,PlayerObject,MonsterObject}.cs` |
| Frame tables | `Client/MirObjects/Frames.cs` + per-object overrides |
| Widget toolkit | `Client/MirControls/*.cs` |
| Socket | `Client/MirNetwork/Network.cs` |
| Server packet handlers | `Server/MirNetwork/MirConnection.cs` |
| Server version gate | `Server/Settings.cs` (`LoadVersion`), `MirConnection.ClientVersion` |
| Client config / paths | `Client/Settings.cs` (`DataPath`, `MapPath`, ports) |
| Packet id table | [`packet_ids.csv`](./packet_ids.csv) (generated) |
