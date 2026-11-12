# AORebirth NPC Editor

The standalone WPF editor opens a mode chooser. **AORebirth server** connects to
a local `ZoneEngine_New` built from this branch and supports manual selection,
editing, named loot and coverage. The optional [AOSharp target bridge](../AORebirth.NpcEditor.AOSharp/README.md)
adds target following for an AO client connected to that same server.
**Official AO — Read Only** opens a separate local snapshot inspector.

## Build and launch on Windows

Run these commands from the checkout root with the .NET 10 SDK and Windows Desktop runtime.

```cmd
dotnet build Tools\AORebirth.NpcEditor\AORebirth.NpcEditor.csproj
dotnet build AORebirth\Server\ZoneEngine_New\ZoneEngine_New.csproj
Tools\AORebirth.NpcEditor\bin\Debug\net10.0-windows\AORebirth.NpcEditor.exe
```

For a local development server, configure these variables in the CMD session
that starts ZoneEngine. Use the normal [AORebirth startup workflow](../../docs/ai/WORKFLOW.md)
with a development database and GameData. Start with just the first setting for
inspection. Add the second for live changes. Add the third to save changes,
pointing it to a pre-created, access-controlled folder separate from
the GameData root. Use a separate port and overlay folder for each Zone process.

```cmd
set "AO_REBIRTH_NPC_EDITOR_PORT=7591"
set "AO_REBIRTH_NPC_EDITOR_ALLOW_WRITES=1"
set "AO_REBIRTH_GAMEDATA_OVERRIDE_PATH=D:\LocalNpcEditor\GameDataOverride"
```

Copy the 64-digit value from `NPC Editor listening ... TLS SHA256=` in the
local ZoneEngine log. Enter the port, fingerprint, and an existing GM account
and password. Verify the fingerprint against the trusted server log after
each ZoneEngine restart. A new server process generates a new certificate.
The editor connects only to `127.0.0.1` and keeps credentials and bearer tokens
in memory. Sign in again when the 20-minute session expires. The read threshold
comes from the `npc` command setting with a minimum of 1. The write threshold
also honors `set` and `setOtherCharacter` in `GmPermissions.json`, which defaults
to GM level 100 for modifying another character. Account sanctions and legacy
login flags apply. Writes recheck the account in the server's account store.

## Inspect, edit, and restore

- **Browse** searches loaded NPCs by name, template, spawn hash, family hash
  and playfield. Select one and lock it before editing.
  Instance identity includes an opaque spawn generation. A despawned NPC or a
  replacement with a reused ID requires a new selection. Runtime stat values
  refresh while the NPC is selected. Apply Live uses the base value captured
  when the edit began and rejects a stale value. Follow resolves the current AOSharp
  target to a server generation and refreshes that identity while the target stays
  selected. A locked selection stays pinned. Live stat refresh runs while the
  active Live Stats view is visible. Other views check the locked generation
  with a lightweight presence request.
- **Stats** shows base, bonus, effective and configured values and their
  sources. Live edits change an allowed base stat and retain applied modifiers.
  An active percentage modifier makes its dependent stat read-only for live edits.
  Maximum HP and nano edits resize the associated current vital and validate
  the resulting values. Save to GameData shows the selected definition, loaded
  NPC count and overlapping overrides. Use the exact template level band for
  vital edits. Independent combat and behavior stats also support an authored
  family or one resource playfield's exact band. A playfield band's value takes
  precedence over the exact template band, which takes precedence over family.
  Overlapping families cannot assign the same stat. Future spawns use the
  resolved configuration. Clearing an override reveals the next applicable
  value. Restore Previous records a new revision.
- **Behavior** shows the native AI's attackability, proximity and engagement
  rules, weapon range, movement, home anchor, uploaded nanos and scripts. On an
  eligible idle NPC, apply breed hostility and native patrol settings live.
  Patrol supports up to 64 waypoints and walk or run pace. Script-owned movement
  remains read-only. Save supported hostility and detection stats through the
  configured Stats view. Patrol edits last for the selected spawn.
- **Spawn** shows the original authored placement, saved configuration and
  loaded point side by side. For an eligible authored `Spawns.json` entry,
  save its primary position and facing, level range, delay, chance and exact NPC
  template hash. Existing template resolution can clamp the rolled level.
  Activation rules and secondary site transforms stay authored. The point keeps
  its original placement key. Saved changes take effect when the owning
  playfield is reconstructed, which can require a ZoneEngine restart for a
  permanently loaded field. The live preview uses native movement to relocate
  an idle selected NPC temporarily. The Spawn points browser can select and
  restore loaded placements with no current NPC, including a point saved at 0%.
- **Batch** supports up to 20 selected NPCs in one loaded playfield. Narrow the
  current results by authored family hash or level range, then preview every
  target before applying. Stats, hostility, armor additions and loot row
  additions are live instance edits. Respawn delay and chance are saved to
  distinct authored points, with scope shown in the preview. Each target is a
  transaction and has its own result. Undo checks the saved or live state
  for intervening edits. Plans last two minutes and results remain available
  to the same editor session for ten minutes after application.
- **Item Catalog** pages through `items.dat` by name, AO template ID, category
  and QL. Select a row to preview its RDB description and available stats.
  Loot selection shows item families from `ItemTemplates.json`.
- **Loot** displays potential corpse loot for the selected NPC, including
  configured rows, nested hash groups, equipment-triggered drops and carried
  items. Select a native table row to change or remove it, or add a row to the
  table. Select an item family from the picker or enter an existing group hash.
  Chance is an independent percentage per repeat. Chance 100 passes every
  chance check. Item eligibility, corpse capacity and spoils rules still apply.
  Use a level-relative variance or explicit minimum and maximum QL. Native
  groups pick one eligible child uniformly or use `SpawnAll`, as configured.
- **Simulate** runs up to 10,000 rolls against the selected NPC's current loot.
  The results give observed frequency per corpse, average items, empty corpse
  frequency and QL counts. Simulation rolls item identities without minting
  items or creating a corpse.
- **Equipment** shows live items and the configured template band separately.
  Replace or remove a supported visible weapon, or add, replace or remove
  supported armor. Monster combat weapons and special equipment have their own
  runtime roles and stay read-only. Live changes use the NPC's existing
  equipment and appearance publication paths.
- **Apply Live** updates the selected NPC. **Revert Draft** discards pending
  form edits. Live history can undo an applied edit while its recorded state
  still matches. **Save to GameData** updates the displayed exact template
  band and shows an estimate of loaded NPCs using it. Newly resolved spawns
  use the saved loot and equipment. Existing spawns keep their materialized
  configuration until edited or replaced. A frozen spawn roll keeps its
  snapshot until its owning subsystem rebuilds it.
- **Restore Previous** records a new revision using the selected saved
  configuration. The separate overlay stores GM history in
  `NpcEditorLootOverrides.json`, `NpcEditorEquipmentOverrides.json`,
  `NpcEditorStatOverrides.json` and `NpcEditorSpawnOverrides.json`.
  Keep the same overlay folder configured after restart.

Loot and equipment save to exact authored template bands. Spawn settings save
to an exact authored point in a resource playfield. Global overrides and
playfield-wide stat defaults spanning every template are unavailable. Generated,
static and script-owned placements are read-only in the Spawn tab. The native
loot resolver uses authored hash groups and independent per-row chances.

The override folder is a local development input. Moving approved changes into
canonical GameData requires the existing private content review and intake
workflow.

## Named loot

Open **Named loot** from the NPC browser to create, copy, edit or delete a table,
or edit its assignments. Tables have stable IDs, display metadata, provenance
and ordered groups. Use the RDB picker to add an item at its native QL or choose
a low/high pair from the same registered family. The quality window is limited
to those endpoints. Level-based quality clamps the NPC level within the window.
Quantities consume one corpse slot per generated item.

- **Independent** evaluates each eligible entry's chance on each group roll.
- **Weighted one** selects one eligible outcome. **Weighted many** repeats
  weighted selection. Empty weight adds an explicit no-item outcome.
- **Guaranteed all** emits every eligible entry on each group roll after group
  activation. Entry and group conditions can restrict level, resource playfield
  and boss status. Unique entries share the corpse's exact item-pair limit.

Assignment precedence runs from global to family, exact template, template
band, resource playfield, boss template and exact authored point. Lower
priorities within a scope are processed first. An assignment can append a
table, replace an inherited named group or suppress it. The editor rejects
ambiguous replacements, duplicate overlapping appends and deletion of referenced
tables. The assignment preview shows the selected stable definition and loaded
matches. Use **Effective loot** on a selected NPC to inspect the same resolver
trace used by native corpse generation.

Existing carried items, equipment loot and ordinary item table rows retain
their order. Named groups fill the remaining slots in the same 20-slot corpse.
Named replacement and suppression affect named groups. Existing template-band
controls manage ordinary rows. A corpse resolves its loot once at death.
Simulation takes an optional seed and returns per-item and named-group outcomes
without allocating items. Weight and initial conditional selection chance are
separate from observed per-corpse frequency.

The `NpcEditorNamedLoot.json` overlay stores atomic registry revisions, GM
attribution and restore history. No migration is required for existing loot.
The active runtime identifies boss templates through its existing boss feature.
Use a verified authored point to give a particular placement special loot.
Dyna, dungeon, mission, raid, quest and event table categories are available
for organization. Dedicated Dyna, mission-instance, encounter and event
assignment scopes require authoritative runtime identities.

## Loot evidence and coverage

Open **Loot evidence** to stage a decoded AOSharp
`corpse-loot-observations.csv`, the decoded packet export
`loot_drop_observations.csv`, or a JSON array of observations. A single import
accepts up to 256 KiB and 2,000 item observations. Use a stable capture label
when reimporting. The JSON fields include `enemyName`, `enemyIdentity`,
`playfieldId`, `monsterData`, `templateHash`, `level`, `lowId`, `highId`,
`quality`, `observationCount`, `capturedUtc`, `classification`, `patch`
and `sourceRow`. Provide patch attribution only where the source supports it.

The separate `NpcEditorLootEvidence.json` overlay records capture provenance
and GM review. Linked initial corpse observations support availability only.
Unlinked records stay unresolved. Imported guarantee claims remain unresolved
with the original claim preserved. Approval records review of the observation.
It creates no loot entries or assignments.

**Selected NPC comparison** distinguishes exact template observations, possible
name/MonsterData matches, unresolved item IDs, and configured candidates with
missing supporting evidence. **Coverage** scans catalog bands, authored spawn
points, named assignments and staged observations. Placement results use
representative levels for the applicable bands. Use the filters and export up
to 10,000 matching rows to CSV or JSON. A missing observation or unconfigured
row is a review finding. The report does not estimate retail completeness.

## Official AO read-only research

Build and load the updated [AOSharp target bridge](../AORebirth.NpcEditor.AOSharp/README.md)
using the existing AOSharp loader. Start the editor and choose **Official AO —
Read Only**. Select an NPC yourself in the AO client. The editor reads the
local snapshot and offers an explicit session choice when several clients
are reporting. **Read selected target** refreshes the local publication.
**Lock target** holds the exact observed selection generation. A changed or
unavailable locked target must be unlocked before following another selection.
To change editor modes, close and reopen the app.

To record loot from a corpse you opened during normal gameplay, lock the NPC
while it is selected and use **Opened-corpse observations**. An unavailable
lock may retain the last snapshot as a historical reference for manual
recording. Enter the corpse or item identifiers only when you know them.
The export retains the manual association and observation status.

**Export JSON and CSV** creates a new file pair in
`%LOCALAPPDATA%\AORebirth\NpcEditor\Exports`. **Compare with AORebirth** opens
a local `NpcTemplates.json` for a read-only comparison of candidate template
bands. The comparison keeps client observations separate from authored
values. See the [capability discovery report](CAPABILITY_DISCOVERY.md) for
field-level sources and a manual acceptance checklist.

## Focused validation

Use the existing development-only wrapper with the owning checkout's private
profile. The tests exercise an isolated HTTPS endpoint and a temporary
playfield, including native NPC spawning, corpse generation, stat scope
precedence, live patrol and aggression changes, stale generation rejection,
and per-target batch apply and undo. Separate fixtures check spawn
reconstruction, scheduling, reassignment, dormant placement restoration,
and stat persistence across catalog reload and reopen. Set
`AO_REBIRTH_NPC_EDITOR_VERIFY_ITEMS_ROOT` to an existing RDB-derived GameData
root to include the read-only real item search check.

```cmd
cmd /d /c tools\windows_release_contract.cmd run-feature-validation -- "C:\Program Files\dotnet\dotnet.exe" test AORebirth\Server\ZoneEngine_New.Tests\ZoneEngine_New.Tests.csproj -p:NpcEditorFocusedTests=true --filter "FullyQualifiedName~NpcEditor"
```
