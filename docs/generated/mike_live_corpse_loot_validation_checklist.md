# Mike-Run Live Corpse Loot Validation Checklist

Scope: AORebirth corpse open and corpse item-transfer behavior pushed through `ec901154`.

Codex must not launch the AO client or AOSharp capture tooling for this validation. Mike runs the AO client and capture plugin; Codex analyzes the saved evidence afterward.

## Preconditions

- AORebirth engines are built and running from current `origin/master`.
- AO client is connected to the private server with AOSharpLiveCapture loaded.
- AOSharpLiveCapture plugin command registration is visible in the client log or chat:
  - `/aocap ...`
  - `/aosmoke start [mobAlias] | stop | status | log`
- Default smoke target is `beachleet`, mapped by the harness to `Codex Test Beach Leet`.
- The harness file is `tools-temp/AOSharpLiveCapture/CombatLootSmoke.cs`.
- The harness writes capture sessions under:
  - `tools-temp/AOSharpLiveCapture/bin/Debug/captures/<timestamp>/`
- The harness writes smoke logs/results under:
  - `tools-temp/AOSharpLiveCapture/bin/Debug/smoke-runs/<timestamp>-combat-loot.log`
  - `tools-temp/AOSharpLiveCapture/bin/Debug/last-combat-loot-smoke.result`

## Launch Steps

1. Start AORebirth engines normally.
2. Launch the AO client and connect to AORebirth.
3. Confirm AOSharpLiveCapture is loaded and a capture folder is created.
4. In AO chat, start the smoke:

```text
/aosmoke start beachleet
```

Use this only if the mob under test has one corpse item and the close/reopen phase cannot be exercised:

```text
/aosmoke start beachleet basic
```

5. Watch chat for:

```text
Combat loot smoke started ...
Combat loot smoke PASS ...
```

or:

```text
Combat loot smoke FAIL ...
```

6. If needed, query current state/log path:

```text
/aosmoke status
/aosmoke log
```

7. If a run gets stuck, stop it:

```text
/aosmoke stop
```

## What The Harness Does

- Cleans known test loot items from the player's inventory.
- Spawns or finds the selected test mob.
- Attacks the mob.
- Waits for `Remains of <mob name>`.
- Opens the corpse.
- Waits for corpse item contents.
- Moves one item to inventory placement `0x6F`.
- Confirms the corpse item count decreased.
- For full mode, closes and reopens the corpse, then loots remaining items.
- Handles duplicate unique test items by treating already-owned unique leftovers as non-lootable rather than duplicated.
- Waits for the corpse to despawn after loot is exhausted.

## Evidence To Save

Save the whole capture folder and these files at minimum:

- `capture_info.json`
- `capture-health.json`
- `capture-session.json`
- `events.log`
- `packets.hex.log`
- `inventory-updates.csv`
- `npc-interactions.log`
- `enemy-combat.csv`
- `system-messages.log`

Also save:

- `tools-temp/AOSharpLiveCapture/bin/Debug/smoke-runs/<timestamp>-combat-loot.log`
- `tools-temp/AOSharpLiveCapture/bin/Debug/last-combat-loot-smoke.result`

## Pass Criteria

- `last-combat-loot-smoke.result` contains `RESULT PASS`.
- Smoke log contains `Opened corpse first time`.
- Capture evidence for corpse open shows:
  - OUT `GenericCmd Use`
  - IN `InventoryUpdate`
  - IN `GenericCmd` success ack after the `InventoryUpdate`
- `inventory-updates.csv` shows the corpse inventory update for the opened corpse.
- Smoke log contains `Corpse first open has ... items:` and item descriptions include item id/high id/quality details.
- Smoke log contains `First loot attempt ...`.
- Smoke log contains `First item moved. Remaining items=...`.
- Capture evidence contains the item move path:
  - OUT `ClientMoveItemToInventory`
  - IN `ContainerAddItem`
- The looted item appears in the resolved player inventory slot.
- Reopen/full-mode run does not expose the already-looted item again.
- Repeat loot handling does not duplicate unique items; duplicate unique leftovers are skipped only when already owned.
- Empty corpse behavior is verified separately when a zero-loot corpse is available:
  - corpse Use still gets an `InventoryUpdate`
  - update contains zero item entries
  - no phantom item appears in player inventory
  - GenericCmd success ack follows the zero-item update

## Fail Criteria

- Smoke result is `RESULT FAIL`.
- Corpse open does not produce `InventoryUpdate`.
- `GenericCmd` success ack appears before corpse `InventoryUpdate`.
- Item-bearing corpse opens without visible item id/high id/quality evidence.
- Loot attempt does not reduce corpse item count.
- `ContainerAddItem` is missing after a successful item move.
- The same corpse item can be looted twice.
- Empty corpse produces a phantom item.
- Corpse remains exposed with already-looted items after reopen.

## Notes For Evidence Review

- The smoke log is best for high-level state and item-count behavior.
- `packets.hex.log` and `events.log` are required for exact packet ordering around `InventoryUpdate` and inbound `GenericCmd` success ack.
- `inventory-updates.csv` is the quickest structured view of corpse inventory contents and item ids/qualities.
- If the AO client is not connected or the plugin is not loaded, do not mark this validation failed; rerun after the client/capture preconditions are satisfied.
