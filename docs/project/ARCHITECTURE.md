# Architecture

Generated: 2026-06-02

## System Architecture

AO Rebirth is split into console engines plus shared libraries:

```mermaid
flowchart LR
    Client["AO Client"] --> Login["LoginEngine"]
    Client --> Chat["ChatEngine"]
    Client --> Zone["ZoneEngine"]
    Web["WebEngine"] --> DB["cellao_codex_clean MySQL"]
    Assets["Pinned local WebCore htdocs"] --> Web
    PHP["Validated local php-cgi"] --> Web
    Login --> DB
    Chat --> DB
    Zone --> DB
    Zone --> Core["AORebirth.Core"]
    Zone --> Msg["AOtomation Messaging"]
    Login --> Core
    Chat --> Core
```

## Major Subsystems

- Login: credentials, character list, character creation, selected character handoff.
- Chat: chat sessions, tells, channels, buddy/org structures.
- Zone: playfield entry, character state, movement, combat, inventory, equipment, trade, NPCs, corpses, loot, stat updates, commands, teleporting, and packet responses.
- Core: entities, dynels, characters, inventory pages, items, requirements, functions, vectors, playfields, NPC/vendor handling.
- Database: DAO/entity layer for accounts, characters, items, templates, mobs, loot, and related persisted data.
- Messaging: AOtomation N3 messages and serializers.
- Tools: capture, replay, smoke tests, client hook/injector experiments, and historical utilities.

## Data Flow

Typical login/playfield flow:

```mermaid
sequenceDiagram
    participant C as AO Client
    participant L as LoginEngine
    participant Ch as ChatEngine
    participant Z as ZoneEngine
    participant DB as MySQL
    C->>L: login credentials / character selection
    L->>DB: account and character lookup
    C->>Ch: chat login/session
    Ch->>DB: character/chat state
    C->>Z: zone login
    Z->>DB: load character/inventory/state
    Z->>C: FullCharacter / playfield / dynels / stats
```

Typical combat/loot flow:

```mermaid
sequenceDiagram
    participant C as AO Client
    participant Z as ZoneEngine
    participant PF as Playfield
    C->>Z: Attack / actions / movement
    Z->>PF: update combat state
    PF->>C: AttackInfo / stat changes
    PF->>C: NPC death / CorpseFullUpdate
    C->>Z: GenericCmd Use corpse
    Z->>C: InventoryUpdate
    C->>Z: ClientMoveItemToInventory or ContainerAddItem
    Z->>C: ContainerAddItem ack / corpse despawn
```

## Class And Module Structure

Important files and directories:

- `AORebirth/Libraries/Source/AORebirth.Core/Entities/Dynel.cs`: base dynamic entity.
- `AORebirth/Libraries/Source/AORebirth.Core/Entities/Character.cs`: character model.
- `AORebirth/Libraries/Source/AORebirth.Core/Inventory`: inventory pages and item movement models.
- `AORebirth/Server/ZoneEngine/Core/Controllers/PlayerController.cs`: player runtime controller.
- `AORebirth/Server/ZoneEngine/Core/Controllers/NPCController.cs`: NPC runtime controller and movement/combat behavior.
- `AORebirth/Server/ZoneEngine/Core/Navigation/`: global hostile-NPC chase capability, bounded route planning/following, route lifecycle state, and playfield navigation-provider contract. PF127 is the first provider; see `docs/project/NPC_CHASE_NAVIGATION.md`.
- `AORebirth/Server/ZoneEngine/Core/Playfields/OrdinaryEnemyProfile.cs`, `OrdinaryEnemyCatalog.cs`, and `OrdinaryEnemyRuntimeService.cs`: validated ordinary-enemy type/spawn data and the single shared runtime path. See `docs/project/ORDINARY_ENEMY_RUNTIME.md`.
- `AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs`: playfield entity registry, combat, death, corpse, loot, despawn, and broad gameplay flow — **do not add new system ecosystems here**; extract to `Core/<System>/` (see `docs/project/SUBSYSTEMS.md`).
- `AORebirth/Server/ZoneEngine/Core/Mail/`: Mail Terminal runtime + handler subsystem.
- `AORebirth/Server/ZoneEngine/Core/Arete/`: Arete dialogue/quest subsystem.
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers`: N3 message handlers for zone gameplay (handlers may also live inside a subsystem folder).
- `AORebirth/Server/ZoneEngine/Core/Packets`: custom packet builders.
- `AORebirth/Server/ZoneEngine/ChatCommands`: GM/debug command surface.
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging`: message models and serializer contracts.

## Networking Architecture

The project uses N3/AOtomation message models for client/server packet flow. Important packet families from current work:

- `Action = 0x2049527C`
- `Attack = 0x28494070`
- `CharacterAction = 0x5E477770`
- `ContainerAddItem = 0x47537A24`
- `InventoryUpdate = 0x4E536976`
- `InventoryUpdated = 0x485E7202`
- `SimpleItemFullUpdate = 0x3B11256F`
- `TemplateAction = 0x35505644`
- `WeaponItemFullUpdate = 0x3B1D2268`

Packet repairs must distinguish:

- AOtomation model shape.
- Captured runtime envelope shape.
- AO stripdown recovered subclass body.
- Current-client live behavior.

## Database Architecture

The local configuration is MySQL and must use only `cellao_codex_clean`. The project includes DAO/entity code under `AORebirth/Libraries/Source/AORebirth.Database`. Do not infer schema safety from code alone; data mutation requires explicit project-owner approval when destructive or broad.

## Asset Pipeline

The repo contains logos, XML data files, documentation generated from enums/stats, and tooling. Client assets come from the installed AO client and external reverse-engineered sources. There is no single build-time pipeline for all of these assets.

Optional WebEngine content has a separate, fail-closed supply boundary. The
runtime contains the HTTP/PHP host but does not fetch website content. An
operator must supply a local ZIP for the exact CellAO WebCore commit
`765c3850767b63af1cd259bab7f2f7ca3e97adf9`; the explicit import command checks
the pinned archive identity and writes the extracted `htdocs` tree locally. A
checked-in manifest is copied beside `WebEngine.exe` and binds all 7,140 files,
their sizes and hashes, and the 26,648,501-byte total. Both explicit validation
and `start-web-engine.cmd` fail closed when the manifest or local tree does not
match. Runtime startup never downloads or updates this content. See
`docs/project/WEBCORE_ASSET_SUPPLY.md`.

## UI Architecture

The project itself has console engines and no modern app UI. AO client UI behavior is driven by packet responses. Player trade windows, loot windows, equipment visuals, death screen behavior, and NPC movement visuals are client UI effects triggered by server packet flow.

## Build Architecture

Primary build:

```cmd
cmd /d /c tools\build_aorebirth_debug.cmd
```

The standard validation wrapper verifies required package folders before MSBuild, runs explicit MSBuild solution restore only when package folders are missing, then builds `AORebirth.Core`, `ZoneEngine`, the read-only `DatabasePreflight`, and `WebEngine` with single-node MSBuild (`/m:1`) and node reuse disabled (`/nr:false`). Legacy build-time NuGet restore through `.nuget\NuGet.targets` has been removed from project files. PowerShell and `.ps1` wrappers are implementation details behind approved CMD lifecycle entrypoints and are not invoked directly by Codex. The solution includes server engines, shared libraries, AOtomation, msgpack-cli, and utility projects. Some tools under `tools-temp` are separate projects and are not necessarily part of the main solution.

## Dependency Graph

High-level dependency direction:

```mermaid
flowchart TB
    ZoneEngine --> AORebirthCore["AORebirth.Core"]
    ZoneEngine --> AORebirthDatabase["AORebirth.Database"]
    ZoneEngine --> AOtomation["AOtomation.Messaging"]
    LoginEngine --> AORebirthDatabase
    ChatEngine --> AORebirthDatabase
    WebEngine --> AORebirthDatabase
    WebEngine --> LocalPhpCgi["Validated local php-cgi"]
    WebEngine --> PinnedWebCore["Manifest-bound local WebCore assets"]
    AORebirthCore --> AORebirthStats["AORebirth.Stats"]
    AORebirthCore --> AORebirthEnums["AORebirth.Enums"]
    AORebirthDatabase --> Dapper
    AORebirthDatabase --> MySqlConnector["MySqlConnector"]
```

## Architectural Concerns

- `Playfield.cs` is a large god object and owns many unrelated systems.
- Packet behavior is split across AOtomation models, handlers, and custom packet builders.
- Some current-client packet contracts differ from old AO Rebirth assumptions.
- Movement and NPC behavior need capture/replay validation before more runtime edits.
- Tests are mostly smoke/source assertions; they are useful but not full simulation coverage.
- WebEngine remains optional and not production-safe: the pinned WebCore
  snapshot has unresolved licensing and obsolete PHP/MySQL/mcrypt/config
  dependencies, and no maintained PHP compatibility has been proven.

## Hostile NPC Chase Navigation

The global navigation boundary is `ZoneEngine.Core.Navigation`, not an enemy or playfield content profile. Existing combat policy requests pursuit through `PlayfieldNpcCombatMovementRuntimeService`; `NpcChaseNavigationRuntimeService` chooses direct movement, a cached provider route, or a fail-closed hold. `IPlayfieldChaseNavigationProvider` supplies authoritative segment checks and route generation without exposing PF127 details to combat code. Valid destinations continue through `NPCController.MoveTo`, preserving server movement cadence and client synchronization.

PF127/resource `127` is currently the only supported provider. It derives a bounded same-elevation grid from the promoted collision geometry and validates every segment against that geometry. Other playfields explicitly remain unsupported and preserve legacy direct chase. See `docs/project/NPC_CHASE_NAVIGATION.md` for exact limits, failure behavior, lifecycle cleanup, validation, and the provider-adoption process.
