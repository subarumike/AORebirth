# AORebirth NPC Editor

The standalone WPF editor connects to a local `ZoneEngine_New` built from this
branch. It works with manual NPC selection. The optional [AOSharp target bridge](../AORebirth.NpcEditor.AOSharp/README.md)
adds selection following for an AO client connected to the same local server.

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
inspection. Add the second for live changes. Add the third for durable loot and
equipment changes, pointing it to a pre-created, access-controlled folder separate from
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

- **Browse** searches loaded NPCs. Select one and lock it before editing.
  Instance identity includes an opaque spawn generation. A despawned NPC or a
  replacement with a reused ID requires a new selection. Runtime stat values
  refresh while the NPC is selected. Apply Live uses the base value captured
  when the edit began and rejects a stale value.
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
- **Apply Live** updates the selected spawn. **Revert Draft** discards pending
  form edits. Live history can undo an applied edit while its recorded state
  still matches. **Save to GameData** updates the displayed exact template
  band and shows an estimate of loaded NPCs using it. Newly resolved spawns
  use the saved loot and equipment. Existing spawns keep their materialized
  configuration until edited or replaced. A frozen spawn roll keeps its
  snapshot until its owning subsystem rebuilds it.
- **Restore Previous** records a new revision using the selected saved
  configuration. The separate overlay stores GM history in
  `NpcEditorLootOverrides.json` and `NpcEditorEquipmentOverrides.json`.
  Keep the same overlay folder configured after restart.

Durable stat edits and independent named loot tables remain unavailable.
The native resolver has no assignment layer for spawn, family, playfield or
global loot overrides, and its hash groups have no per-child weight field.
The editor supports the selected live instance and the exact authored template band.

The override folder is a local development input. Moving approved changes into
canonical GameData requires the existing private content review and intake
workflow.

## Focused validation

Use the existing development-only wrapper with the owning checkout's private
profile. The tests exercise an isolated HTTPS endpoint and a temporary
playfield, including native NPC spawning and corpse generation. Set
`AO_REBIRTH_NPC_EDITOR_VERIFY_ITEMS_ROOT` to an existing RDB-derived GameData
root to include the read-only real item search check.

```cmd
cmd /d /c tools\windows_release_contract.cmd run-feature-validation -- "C:\Program Files\dotnet\dotnet.exe" test AORebirth\Server\ZoneEngine_New.Tests\ZoneEngine_New.Tests.csproj -p:NpcEditorFocusedTests=true --filter "FullyQualifiedName~NpcEditor"
```
