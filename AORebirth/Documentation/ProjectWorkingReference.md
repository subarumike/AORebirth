# CellAO Project Working Reference (Codex + Mike)

Last updated: 2026-06-01 (America/Chicago)

## 1) Environment and Safety Rules

- Working repo: `C:\Users\Mike\Documents\Cellao-Clean`
- AO client install: `C:\Funcom\Anarchy Online`
- Private-server client path: `C:\Users\Mike\Desktop\win`
- Reverse-engineering source set: `C:\Users\Mike\Documents\AO stripdown\Anarchy Online`
- Database policy: use only `cellao_codex_clean`
  - Do not change schema.
  - Do not wipe or mass-edit data without explicit Mike approval.

## 2) Operating Rules For This Project

- Packet and behavior changes must be evidence-backed.
- Source-of-truth order:
  1. Official live capture
  2. Private-server capture (shape/reference only, not authority over live behavior)
  3. `AO stripdown` decompile notes/contracts
  4. Local assumptions (last resort only)
- Three-attempt rule is enforced:
  - 3 attempts on one path, then switch path.
  - 3 more attempts, then stop and re-evaluate with capture/source evidence.

## 3) Locked-In Fixes (Do Not Regress)

### Login + sit/stand baseline

- `FullCharacterMessageHandler.cs` sends `MsgVersion = 26` (not 25).
- `ClientConnected.cs` initializes live-style login state:
  - `State = 0`
  - `CurrentMovementMode = Run`
  - `PrevMovementMode = Run`
  - `CurrentState = 0`
  - `WaitState = 0`
  - `SocialStatus = 4`
  - `SpecialCondition = 3`
  - `ActionCategory = 0`
- Fake server-side action bootstrap packets were removed.

### Equipment / weapon handling

- Weapon visual mesh path is active and verified in playtests:
  - equip shows in hand,
  - attack uses weapon behavior (not MA fallback when properly equipped),
  - dual wield was validated,
  - equip delays align with client equip meter,
  - equipped items persist across relog.
- Solar-powered weapon path was validated in live-style tests.
- Armor and multiple equipment classes (HUD/deck/util) were tested and generally working.

### Death/respawn white-screen issue

- White-screen-after-respawn loop was repaired and validated in user playtests.
- Death -> respawn now restores gameplay visibility.

### Trade window routing

- Player trade now uses the correct player trade window path (not NPC shop window).
- Core player trade flow reached working state; stale/duplication regressions were partially repaired during iteration.

### Packet contract hardening

- Recent commit line (2026-05-28 through 2026-05-31) locked:
  - recovered N3 packet contracts,
  - captured envelope contracts,
  - death teleport shape fixes,
  - smoke harnesses for corpse, logout, combat gates.

## 4) Current In-Flight Problem Areas

### A) NPC chase/combat movement

- Main unresolved behavior: jitter/teleport/death-spiral style pursuit artifacts under circular movement and rapid spacing changes.
- Movement work must stay replay/capture-driven; avoid adding local AI heuristics without packet evidence.
- Relevant files:
  - `CellAO\Server\ZoneEngine\Core\Controllers\NPCController.cs`
  - `CellAO\Server\ZoneEngine\Core\MessageHandlers\FollowTargetMessageHandler.cs`
  - `CellAO\Server\ZoneEngine\Core\Playfields\Playfield.cs`
  - `CellAO\Server\ZoneEngine\Core\EnemyBehaviorContract.cs`

### B) Corpse credit desync (`111 credits from the corpse`)

- Symptom observed in local:
  - expected: `You received 1 credit from the corpse.`
  - unexpected extra line: `You received 111 credits from the corpse.`
- Evidence found so far:
  - server cash stat path shows +1 only in logs for repro cases,
  - `111` matches move-to-inventory placement sentinel (`0x6F`).
- Latest mitigation patch (pending user re-test):
  - on corpse item loot ack, server now echoes `ContainerAddItem.TargetPlacement = targetPlacement` (live-shaped `0x6F`) instead of forcing resolved slot.
  - file: `CellAO\Server\ZoneEngine\Core\Playfields\Playfield.cs`

### C) Trade credits

- Credit transfer/trade failures and stale display issues were seen while corpse-credit behavior was unstable.
- Trade credit behavior must be revalidated after corpse-credit fix is confirmed.

## 5) Capture and Evidence Locations

- Capture root:
  - `C:\Users\Mike\Documents\Cellao-Clean\tools-temp\live-pcaps`
- Key capture sets:
  - `official-live-player-trade-exact`
  - `official-live-player-trade-complete`
  - `official-live-death-respawn`
  - `official-live-mob-chase`
  - `private-server-loot`
- AOSharp capture tooling:
  - `tools-temp\AOSharpLiveCapture`
  - `tools-temp\AOSharpLiveInjector`

## 6) Build / Run Commands

### Stop engines

```powershell
Get-Process ChatEngine,LoginEngine,ZoneEngine,WebEngine,MSBuild -ErrorAction SilentlyContinue | Stop-Process -Force
```

### Build (Debug)

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' 'CellAO\CellAO.sln' /t:Build /p:Configuration=Debug /m
```

### Start engines

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\Mike\Documents\Cellao-Clean\start-engines.ps1'
```

## 7) Test Accounts and Session Notes

- Main test account: `Mike / 123456`
- Naming note:
  - account name can differ from character name (`mikedoc`, `mikeenf`, `Traderjoeeat`, etc).
- Workflow preference:
  - Codex runs tools/build/servers.
  - Mike performs in-client playtests.
  - Do not start/stop client-side live testing flow without explicit coordination.

## 8) Immediate Next-Step Checklist

1. Re-test corpse loot for leet/spider after latest `ContainerAddItem` ack patch.
2. Confirm if extra `111 credits` line is gone.
3. Re-test player trade credits once corpse credit behavior is stable.
4. Continue NPC chase work only with fresh live/private capture windows and replay assertions.
5. Keep commits split by system (movement vs trade vs corpse vs docs) to avoid mixed regressions.

