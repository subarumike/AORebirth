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
inspection. Add the second for live changes. Add the third for durable loot
changes, pointing it to a pre-created, access-controlled folder separate from
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

## Inspect, apply, and revert

- **Browse** searches loaded NPCs. Select one and lock it before editing.
  Instance identity includes an opaque spawn generation. A despawned NPC or a
  replacement with a reused ID requires a new selection.
- **Stats and equipment** show runtime and configured values where the active
  template supplies them. Edit the enabled stats or equipment slots with
  **Apply Live**. The live history supports rollback while the same NPC is
  spawned and the modified value or item still matches the recorded change.
- **Loot** shows the current NPC's materialized loot rolls, nested item
  families, equipment-triggered entries and carried items. The editor labels
  configured rows and live inputs separately from GM-authored rows. Chance and
  repeats use the server's existing independent-roll rules. Level variance is
  the existing level-relative percentage field. Zero variance selects the
  gameplay default. Set a chance of 100 to pass the chance check on every roll.
  Item eligibility, corpse capacity and spoils rules still apply.
- **Save to GameData** on the Loot tab appends, replaces or removes a row in
  the separate `NpcEditorLootOverrides.json` file. It applies to future NPCs
  resolved from the displayed exact template hash, including respawns that
  resolve the catalog again. Other spawns that share this hash also receive the
  new rows. The selected live NPC keeps the loot it spawned with. The original
  authored rows stay in place. The saved history records the GM account and
  revisions. Reverting an earlier saved revision records a new revision.
  Keep the overlay configured across restarts to retain it.

Live stat and equipment edits expire with the selected NPC. Durable stat and
equipment authoring, live loot edits, authored base-row replacement, new global
tables, and family or spawn-specific loot assignments are outside this slice.
The active corpse generator uses NPC materialization and native hash-family
resolution. The global assignment and weighted-roll models in the test fixtures
have no active ZoneEngine resolver. The editor leaves those controls disabled.

The override folder is a local development input. Moving approved changes into
canonical GameData requires the existing private content review and intake
workflow.

## Focused validation

Use the existing development-only wrapper with the owning checkout's private
profile and GameData. These tests exercise temporary content and offline NPCs.

```cmd
cmd /d /c tools\windows_release_contract.cmd run-feature-validation -- "C:\Program Files\dotnet\dotnet.exe" test AORebirth\Server\ZoneEngine_New.Tests\ZoneEngine_New.Tests.csproj -p:NpcEditorFocusedTests=true --filter "FullyQualifiedName~NpcEditor"
```
