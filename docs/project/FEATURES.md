# Features

Generated: 2026-06-02

## Login And Character Entry

Status: In Progress

Implementation notes:

- Login, chat, and zone handoff exist.
- Current-client `FullCharacter` version 26 and live-style login state are important locked fixes.
- Playfield entry is still an area of current-client mismatch, especially `PlayfieldAnarchyF`.

## Chat

Status: In Progress

Implementation notes:

- Chat engine and basic chat packet handlers exist.
- Vicinity/global output is used heavily during playtesting.
- TODO: Requires human clarification for intended final chat feature scope.

## Equipment

Status: Complete for current test scope

Implementation notes:

- Weapons appear in hand.
- Weapon firing animation and equipped weapon attack source were validated.
- Dual wield works and alternates attacks.
- Armor visual equip works.
- HUD/deck/util testing showed several items working, including screen effects.
- Equipment delays align with the client equip meter.
- Equipped items persist across relog.

## Inventory

Status: In Progress

Implementation notes:

- Inventory pages and item movement exist.
- Equipment persistence and GM `giveitem` path are improved.
- Active risks remain in corpse loot, trade windows, stale visuals, and credit transfer.

## Player Trade

Status: In Progress

Implementation notes:

- Correct player trade window path was reached.
- Trade can complete in some sequences.
- Stale item visuals, visual duplication, and credit transfer issues require continued packet-backed repair.

## Combat

Status: In Progress

Implementation notes:

- Basic weapon combat works.
- Normal auto-attacks use `AttackInfo` only.
- `HealthDamage` is gated away from normal weapon/unarmed auto-attacks.
- NPC/player damage, death, and corpse creation are partially working.
- Combat packet metadata remains approximate where no visible/text bug exists.

## Death And Respawn

Status: Complete for current test scope

Implementation notes:

- The white-screen-after-respawn issue was fixed.
- Current path follows modern client behavior, not the old reclaim system.
- Continue to protect this with smoke/source assertions.

## Corpse And Loot

Status: In Progress

Implementation notes:

- Corpse windows, item looting, unique checks, and despawn timing have been worked on.
- Credit desync is active and must be traced at the source.
- Corpse full update remains an area for cleanup toward live/current-client structure.

## NPC Movement And AI

Status: In Progress

Implementation notes:

- Hostile mobs can spawn and fight.
- Passive mobs retaliate when attacked.
- Social/KnuBot NPCs are excluded from normal combat.
- Movement/chase remains unstable and requires replay/capture work before more runtime patches.

## Debug Commands

Status: In Progress

Implementation notes:

- Commands exist for spawning, item giving, NPC tools, teleporting, stat work, posture, weather, and diagnostics.
- Mike uses commands for playtesting, including `/command giveitem <id> <ql>` and spawn commands.

## Capture And Test Tooling

Status: In Progress

Implementation notes:

- `tools-temp/live-pcaps` stores captures.
- `tools-temp/AOSharpLiveCapture` and `tools-temp/AOSharpLiveInjector` support live capture work.
- `tools-temp/AORebirthCombatSmokeTests/Run-CombatSmokeTests.ps1` contains important source assertions.
- `tools-temp/enemy-movement-replay` contains enemy movement replay infrastructure.

