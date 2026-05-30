# CellAO Project Reassessment - 2026-05-30

## Current State

- Branch is `master`.
- Latest committed work is stable around equipment, dual wield, HealthDamage gating, timed logout smoke coverage, corpse/loot smoke coverage, and capture documentation.
- Worktree is dirty. Runtime NPC movement, docs, smoke tests, and capture-tool edits are still mixed together.
- AOtomation FollowTarget packet-model changes were split and committed in the submodule as `6b51b54 Model captured FollowTarget variants`.
- An enemy movement replay fixture now exists at `tools-temp\enemy-movement-replay`.
- Verification run during this review:
  - `tools-temp\enemy-movement-replay\Test-EnemyMovementReplay.ps1` passed.
  - `tools-temp\CellAOCombatSmokeTests\Run-CombatSmokeTests.ps1` passed.
  - `tools-temp\live-data-collector\Test-LiveDataCollector.ps1` passed.
  - `CellAO\CellAO.sln` Debug build passed with existing warnings only.

## Systems That Are In Good Shape

- Equipment is the strongest recent system. Weapons, armor, HUD, deck, util, visual mesh updates, item stats, dual wield alternation, equipment delay sync, persistence, and relog visuals have all had live playtest success.
- Corpse and loot are usable. Death creates corpse state, corpse use routes through corpse identity, inventory update and item looting work, unique checks exist, and empty corpse cleanup follows capture-backed short despawn timing.
- Normal weapon combat is usable. Weapon attacks use equipped weapon source, damage/range/timing are no longer pure unarmed defaults, and normal weapon/unarmed hits no longer send duplicate `HealthDamage`.
- Timed logout has source assertions and a packet model. The code path now supports sit posture, `StartLogout`, and the 30-second server timer, but needs one more full user-facing verification pass.
- Capture tooling is a major asset. It can decode known captures, label authority, export packet coverage, timelines, corpse sessions, quest observations, and combat/loot summaries.

## Weak Points

1. **Enemy movement is the highest-risk area.**
   Recent work mixed evidence, local observation, and emergency patches. The latest correction that melee attacks can happen while moving is important, but the whole chase path still needs a clean design and replay validation before more playtest churn. The weak files are `NPCController.cs` and `Playfield.cs`.

2. **Movement packet authority is still not settled.**
   We have official live `FollowTarget` evidence, private-server rich S2C evidence, N3/dynel decompile notes, and local video/log observations. Those sources are useful, but they are not the same authority. The project must keep official-live, private-server, and decompile evidence separated.

3. **The dirty worktree is too broad.**
   Runtime combat, NPC movement, packet serializers, docs, smoke tests, and AOtomation submodule edits are all uncommitted. This makes it easy to accidentally commit unstable movement work together with stable packet/model repairs.

4. **AOtomation submodule is now locked, parent pointer still needs a clean parent commit.**
   The FollowTarget serializer/model files and new `FollowPositionInfo` / `FollowStopInfo` files were committed inside AOtomation. The parent repo now sees the submodule pointer as modified and should commit that pointer with the replay/evidence bucket, not with unverified runtime movement.

5. **Combat packet metadata is still approximate.**
   `AttackInfo` is working well enough for visible combat, but fields remain simplified compared with private-server captures. Do not change them unless a visible animation/text bug appears. `HealthDamage` should stay out of normal auto-attacks.

6. **Tests are mostly source/assertion smoke tests.**
   They are valuable and caught a compile break, but they do not simulate live movement windows. Enemy behavior needs replay tests built from decoded captures, not only regex guards.

7. **`Playfield.cs` is a god object.**
   Combat ticks, death, corpse state, loot, movement, despawn, and telemetry all live together. That raises regression risk and makes movement fixes spill into unrelated systems.

8. **Inventory persistence is improved but not transaction-safe.**
   Equip/unequip/move now writes inventory immediately, but the base inventory layer still has an old warning about item placement switches during crashes. This is acceptable for current testing, but not robust.

9. **DB content quality is uneven.**
   Item DB contains many non-live or non-useful entries. Spawning random IDs is not a reliable test path. Use known-live item lists, capture-derived candidates, or curated test catalogs.

10. **Logs are too noisy and too large.**
    `ZoneEngineLog.txt` is already very large. Movement debugging needs targeted, short-lived telemetry with identifiers and timestamps, not permanent high-volume spam.

## Recommended Next Path

### First: Stabilize The Repository

1. Stop treating the current dirty tree as one change.
2. Keep changes split into three buckets:
   - Stable proven work: equipment, loot, logout, HealthDamage policy, banner/startup fix.
   - Packet-model support: committed AOtomation FollowTarget serializer/model changes plus replay/evidence files.
   - Unverified enemy movement work: NPCController/Playfield chase changes and docs.
3. Commit or stash each bucket separately. Do not push unverified movement as if it is solved.

### Second: Build Enemy Movement From A Replay Contract

Use the simple AO rule Mike stated:

- If target is out of attack range, follow.
- If target is in melee range, melee can attack while moving.
- If target is ranged/nano and in range, stop and attack.
- Do not clear threat/follow just because the target moved.
- Stop only for target invalid, death, zoning, or a captured hard reset.

The first replay fixture now exists. The next implementation step should be to feed it a mined real combat window:

1. Take one decoded live/private combat window.
2. Convert it into a compact CSV/JSON transcript: time, dynel, packet family, positions, range, attack event.
3. Run the pure C# state contract over it.
4. Assert state transitions: `Idle -> Chasing -> MeleeAttackingWhileMoving` or `RangedStopAndAttack`.
5. Only after that, wire runtime `NPCController` to the contract.

### Third: Capture Local CellAO Packet Output

We need to compare CellAO S2C output against the capture facts, not just watch the client.

Add or reuse a local packet tap that records:

- `FollowTarget`
- `SetWantedDirection`
- `SetPos`
- `StopMovingCmd`
- `Attack`
- `AttackInfo`
- `StopFight`

Then compare local sequence against the mined live/private windows. This will tell us if the packet flow is wrong before Mike has to describe visuals by hand.

### Fourth: Keep Working Systems Moving

After movement is isolated:

1. Lock equipment with a clean commit and a playtest checklist.
2. Expand curated spawn/item catalogs using known-live item evidence.
3. Clean corpse full update from debug/template-ish data toward live packet shape.
4. Add DB loot coverage for a few verified mobs only.
5. Revisit logout X behavior with one focused live/local capture pass.

## What Not To Do Next

- Do not keep patching NPC movement during a heated playtest loop.
- Do not mix official-live C2S-only data with private-server S2C data without labels.
- Do not use one mob family's movement as a global rule for every NPC.
- Do not commit dirty submodule and parent repo changes without splitting them.
- Do not add `SetPos`, teleport, leash, or stop/start movement helpers without a packet window proving the trigger.

## Immediate Tomorrow Checklist

1. Decide whether to preserve or discard the latest unverified enemy movement edits.
2. Commit/stash stable non-movement work separately.
3. Commit the parent submodule pointer with replay/evidence files.
4. Mine one short combat chase window into replay data.
5. Only then restart local movement playtesting.
