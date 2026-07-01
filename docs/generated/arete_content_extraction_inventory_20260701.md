# Arete Content Extraction Inventory

Date: 2026-07-01

Scope: documentation-only inventory of Arete-specific content still owned by `Playfield` before any extraction work.

No production behavior changes were made for this report.

## Summary

Most captured Arete robot content has already been moved behind the Playfield content-module boundary:

- `AreteContentModule` supports private Arete PF `6553`.
- `AreteContentModule` registers captured robot spawns through `PlayfieldContentRegistration.RegisterCapturedNpcSpawns`.
- `CapturedAreteRobotContentProvider` owns the captured robot spawn rows and patrol replay file references.
- `CapturedAreteRobotSpawnOrchestrator` owns the captured robot spawn orchestration.

The main Arete-specific behavior still directly owned by `Playfield` is the legacy DB-spawn suppression for captured cleaning robot test rows in PF `6553`.

## Inventory

| Area | Current owner | Evidence | Classification | Notes |
| --- | --- | --- | --- | --- |
| Captured cleaning robot spawn definitions | `CapturedAreteRobotContentProvider` | `SpawnDefinitions`, `RobotName`, `MonsterData`, source identities, HP, level, run speed, spawn/patrol coordinates | Safe content-module candidate | Already outside `Playfield` and registered through `AreteContentModule`. Keep data/provider content-only; do not move combat behavior into the module. |
| Captured cleaning robot patrol replay references | `CapturedAreteRobotContentProvider` and `NpcPatrolReplayCoordinator` | `PatrolReplayRelativePath`, committed `Content\Captured\Arete\cleaning_robot_patrol_replay.csv`, evidence capture path | Uncertain, needs guardrail first | Replay file reference is content-like, but runtime replay assignment is movement-system-adjacent. Keep coordinator/runtime ownership separate from content data. |
| Captured cleaning robot spawn orchestration | `CapturedAreteRobotSpawnOrchestrator` via `AreteContentModule` | `SpawnForPlayfield`, `SpawnCapturedAreteCleaningRobot`, `AssignCapturedAreteRobotReplay` | Runtime-system-owned, do not move into content data | The content module can register the orchestration hook, but spawning NPCs, assigning controllers, and broadcasting SCFU are runtime behavior. |
| Legacy Arete cleaning robot DB-spawn suppression | `Playfield.LoadMobSpawns` / `IsAreteCleaningRobotTestSpawn` | PF `6553`, skipped DB mob IDs `2027138231`, `2027138245`, `2027138246`, `2027138249`, `2027138259` | Safe content-module candidate | Best first extraction candidate. It is Arete-specific content-selection policy currently embedded in generic mob loading. Add a guardrail first so the captured robot spawn count/order stays unchanged. |
| Generic mob DB loading | `Playfield.LoadMobSpawns` | `MobSpawnDao`, `MobSpawnStatDao`, `NonPlayerCharacterHandler.InstantiateMobSpawn` | Runtime-system-owned, do not move | Generic mob loading is a runtime loader path, not Arete content. Only the Arete-specific exclusion predicate should be considered for extraction. |
| Static dynels | `Playfield.LoadStaticDynels` | `StaticDynelDao`, `ItemLoader.ItemList`, per-playfield DB rows | Runtime-system-owned, do not move | No Arete-specific static dynel definitions were found inline in `Playfield`; this is generic per-playfield DB loading. Future Arete static dynel content should need identity-backed evidence and its own guardrail. |
| Vendors | `Playfield.LoadVendors` | `VendorHandler.SpawnVendorsForPlayfield`, PFData statels where `IdentityType.VendingMachine` | Runtime-system-owned, do not move | No Arete-specific vendor list is inline in `Playfield`; this is generic PFData/statel-driven vendor loading. |
| Terminals/statels | `ResolvePlayfieldStatels` and PFData-backed statels | `PlayfieldLoader.PFData`, statel list loading | Runtime-system-owned, do not move | No Arete-specific terminal definitions were found inline in `Playfield`. Terminal/use routing is outside content-module scope. |
| Arete quest/dialogue hooks referenced near Playfield systems | `ZoneEngine.Core.Arete.*` runtime services | `Playfield` and adjacent systems reference Arete quest/runtime namespaces | Runtime-system-owned, do not move | Quest, dialogue, corpse, and NPC controller integrations are runtime systems. They should not be extracted into content modules without separate guardrails. |

## Recommended First Extraction Candidate

First extract the legacy Arete cleaning robot DB-spawn suppression:

- Move the PF `6553` and DB mob ID list out of `Playfield.IsAreteCleaningRobotTestSpawn`.
- Keep the actual mob loading loop in `Playfield`.
- Use the content-module boundary only to provide content-selection data or a predicate.
- Add/extend a guardrail proving that captured Arete robot spawns still come from the captured provider and that the legacy DB test rows remain suppressed.

Reason: this is the smallest Arete-specific content rule still embedded in `Playfield`, and it does not require touching combat, corpse lifecycle, GenericCmd, inventory, org commands, packet serialization, private-city ready/init, guest keys, City Controller behavior, database import, or capture tooling.

## Not Recommended Yet

Do not move these in the next extraction:

- `CapturedAreteRobotSpawnOrchestrator.SpawnCapturedAreteCleaningRobot`: it creates live NPCs, assigns controllers, records lifecycle trace, and broadcasts updates.
- `NpcPatrolReplayCoordinator`: movement/runtime coordination should remain outside content data.
- `LoadStaticDynels`, `LoadVendors`, `ResolvePlayfieldStatels`: current implementations are generic runtime loaders.
- Arete quest/dialogue/corpse integrations: runtime systems with separate behavioral risks.

## Files Inspected

- `AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/Content/AreteContentModule.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/Content/PlayfieldContentRegistration.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/CapturedAreteRobotContentProvider.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/CapturedAreteRobotSpawnOrchestrator.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/NpcPatrolReplayCoordinator.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/PlayfieldLifecycleTrace.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging.Tests/PlayfieldLifecycleTraceTests.cs`
