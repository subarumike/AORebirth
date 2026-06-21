# Current Task

## Active Task

Fix bank items appearing in the Wear window Weapon tab after deposit and zone/relog.

## Current Scope

- Keep changes scoped to bank inventory state, wear/equipment serialization, and packet routing.
- Use live capture evidence before changing packet behavior.
- Do not rework unrelated inventory systems.

## Current Evidence

- Capture `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260621-073837` shows an inventory-to-bank `ClientContainerAddItem`, a server `ContainerAddItem` acknowledgement, then zone initialization.
- During the zone sequence, the server sends `WeaponItemFullUpdate` packets for bank-owned item instances before `FullCharacter`.
- The same bank-owned item instances are present in the `FullCharacter` inventory slot payload.

## Validation Plan

- Run `cmd /d /c git diff --check`.
- Run `cmd /d /c tools\build_aorebirth_debug.cmd`.
- After successful rebuild, run `cmd /d /c restart-engines.cmd`.
- Live smoke: deposit bank item, zone or relog, confirm Wear Weapon tab does not show bank items, withdraw still works, and equipped items remain visible.

## Validation Result

- Build and restart passed.
- Mike's live smoke passed: bank deposit, zone/relog Wear Weapon tab check, and bank withdraw.
