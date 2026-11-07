# AORebirth NPC Editor foundation

The dedicated `codex/npc-editor` worktree started at public master
`d4285053660ccfe9b01cee6ed656af6b78b84e11`. The original checkout was on
`27386e22f16938319da97d742f164b27a8e7a15a` with existing edits. Those edits
stayed in the original checkout.

## Existing owners

| Area | Observed implementation used by the editor |
| --- | --- |
| Runtime identity and inspection | `NpcCommand`, `PlayfieldManager`, `Playfield.Dispatch`, `DynelRegistry`, `NpcCharacter`, and `WorldNpcFactory` |
| Stats and equipment | `StatCollection`, `Character` stat dependencies, `Dynel` dirty stat publication, NPC wear/weapon rebasing, and `IItemBuilder` |
| Loot | `Corpse`, `LootableDynel`, `EquipmentLoot`, `NpcTemplateCatalog`, `GameDataStore`, and `HashItemCatalog` |
| Item lookup | ZoneEngine's `ItemTemplateCatalog`, populated from GameData `items.dat`, with item-family mappings from `ItemTemplates.json` |
| Authentication | Existing password hashing, account sanctions and login flags, and the `GmPermissions` policy used by `.npc` and `.set` |
| AOSharp target selection | `Tools/aosharp_npc_inspect` target hooks and AOSharp's playfield model identity |

The existing debug MCP listener lacks GM authentication. The editor uses the
same ASP.NET Core hosting stack on a separately enabled, authenticated port.

## Implemented

- **PROVEN in focused validation.** Windows loopback TLS with a pinned
  certificate, GM sessions, access checks, session expiry and revocation.
  The real mutation route rejected unauthenticated and read-only sessions.
  **OBSERVED in source.** Runtime snapshots and edits dispatch to the selected
  NPC's owning playfield.
  Writes require both the matching spawn generation and the expected live
  value or item instance. Health uses the existing percentage recalculation.
- **PROVEN in focused validation.** The editor can add, replace and remove
  supported NPC armor and replace a supported visible right-hand weapon. The
  existing monster combat weapon item remains intact. The native weapon rebase
  may derive the first monster weapon's range from the visible hand item.
  NPC equipment that supplies loot, nanos, spawn weapons or boss behavior is
  protected from these changes.
- **PROVEN in focused validation.** Effective loot inspection shows the native
  materialized rolls and their reachable item families, including nested hash
  selection, carried items, and equipment contributions. Saved overlay rows
  have exact leaf-template scope. They are validated against the existing
  item catalog and the selected authored template's signature. A temporary
  GameData fixture verified native loot generation after a fresh load, revision
  rollback, stale and external write rejection, and isolation of other
  templates and original files.
- **PROVEN by build and launch.** The WPF editor window opened. Its controls
  cover manual browsing, spawn locking, runtime and configured details, live
  edits and history, and saved loot editing. The optional AOSharp bridge
  compiled against the installed runtime assemblies. Actual AO client targeting
  and appearance updates remain **UNVERIFIED**.

## Scope boundaries and next stage

- The active loot runtime has independent chance checks and hash-family
  resolution. The fixture-only global assignment and weighted roll models
  would need an active runtime owner before the editor could expose them.
- Loot writes affect only editor-authored rows on an exact resolved leaf
  template. Live and cached NPC templates retain their existing loot snapshot.
  A future spawn sees the change when it resolves the catalog again. Frozen
  spawn rolls can retain their snapshot until the owning subsystem rebuilds it.
- Persistent stats and equipment require a validated authoring path for level
  bands, placed-NPC definitions, and equipment that mixes appearance and combat
  metadata. Those saves are disabled. The live paths are per instance and have
  bounded in-memory rollback history. The durable loot journal records the GM
  account and retains revisions across restarts with the same overlay root.
- The bridge has no authoritative AOSharp server identity. Following is opt-in
  for the same local ZoneEngine and requires a unique, fresh target hint. The
  desktop resolves that hint through the authenticated ZoneEngine endpoint.
- Connected acceptance against a running AORebirth deployment and an official
  client remains **UNVERIFIED**. Validation uses a temporary loopback listener,
  simulated account records and isolated NPC/loot fixtures. Production GameData,
  services and database were outside validation scope.

## Windows validation

| Check | Result |
| --- | --- |
| WPF developer build and window launch | PASS |
| Optional AOSharp bridge build against installed runtime | PASS |
| ZoneEngine_New developer build | PASS |
| Guarded `NpcEditorFocusedTests` | PASS, 14 passed, 0 failed, 0 skipped |

The focused run used a worktree-owned validation profile and the existing
release helper's `FEATURE_VALIDATION` operation. The helper measured the
selected GameData for input identity. Test writes used temporary folders and
in-memory NPCs. The receipt records `DEPLOYABLE=NO` and
`WINDOWS_USER_ACCEPTED=NO`.

## Change inventory

| Change | Files |
| --- | --- |
| Added WPF project | `Tools/AORebirth.NpcEditor/` with `AORebirth.NpcEditor.csproj`, `App.xaml` and its code-behind, `MainWindow.xaml` and its code-behind, `ItemPicker.xaml` and its code-behind, `EditorApi.cs`, `EditorModels.cs`, `TargetBridge.cs`, `README.md` |
| Added AOSharp project | `Tools/AORebirth.NpcEditor.AOSharp/` with `AORebirth.NpcEditor.AOSharp.csproj`, `Main.cs`, `README.md` |
| Added server editor and journal | `Core/NpcEditor/` with `NpcEditorHost.cs`, `NpcEditorSessions.cs`, `NpcEditorRuntime.cs`, `NpcEditorRuntimeModels.cs`, `NpcEditorRuntimeEdits.cs`, `NpcEditorLoot.cs`, plus `Core/GameData/NpcEditorLootJournal.cs` under `ZoneEngine_New` |
| Updated ZoneEngine integration | `Program.cs`, `ZoneEngine_New.csproj`, `Core/Accounts/AccountSanctionRepository.cs`, `Core/Entities/NpcCharacter.cs`, `Core/GameData/GameDataStore.cs`, `Core/GameData/NpcTemplateCatalog.cs`, `Core/Mobs/MobTemplates.cs` |
| Added focused tests | `NpcEditorAuthTests.cs`, `NpcEditorLootTests.cs`, and `NpcEditorRuntimeTests.cs`, registered in `ZoneEngine_New.Tests.csproj` |
| Added implementation report | `docs/reports/NPC_EDITOR_FOUNDATION.md` |

The [editor README](../../Tools/AORebirth.NpcEditor/README.md) contains build,
launch, connection and validation commands. The [bridge README](../../Tools/AORebirth.NpcEditor.AOSharp/README.md)
contains optional plugin setup.
