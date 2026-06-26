# Current Task

## Active Task

Captured Cleaning Robot Combat Parity.

The active scope is enemy combat behavior for the captured `Malfunctioning Cleaning Robot`. Old private-city, org-command, city-controller, guest-key, and player-visibility work is not active implementation scope for this task.

## Current Target

- Enemy: `Malfunctioning Cleaning Robot`
- MonsterData: `297023`
- HP: `12`
- RunSpeed: `5/6`
- Primary evidence folder: `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260625-192827`

## Proven Live Combat Sequence

- `Attack`
- `AttackInfo`
- `FollowTarget`
- occasional `SetPos`
- `StopFight`
- `CharacterAction Death`
- `CorpseFullUpdate`
- delayed despawn

## Completed Enemy Work

- Cleaning robot combat chase uses continuous `FollowTarget` behavior.
- Cleaning robot damage text path sends captured `SpecialAttackWeapon` `LIW2`/`LIW1` context before robot `AttackInfo`.

Recent enemy commits:

- `d9a9b9d2` - cleaning robot continuous `FollowTarget` chase.
- `c8f517b8` - cleaning robot `SpecialAttackWeapon` damage context before `AttackInfo`.

## Next Enemy Work

- Mike live-tests robot incoming damage text and reports whether the AO client still shows `nanobots` or `unknown damage`.
- Patch death, corpse, and despawn parity:
  - `StopFight`
  - `CharacterAction Death Parameter2=500`
  - `CorpseFullUpdate`
  - delayed despawn

## Regression Risks Only

These systems are not active implementation scope for this task. Preserve them while changing enemy combat behavior:

- Private-city zoning, guest-key generator, city-controller open/close, and private-city org initialization.
- `/org info` behavior.
- Same-playfield player visibility and movement rendering.

## Validation Plan

For docs-only task updates:

- `cmd /d /c git diff --check`

For future code changes affecting server binaries:

- `cmd /d /c stop-engines.cmd`
- `cmd /d /c tools\build_aorebirth_debug.cmd`
- `cmd /d /c restart-engines.cmd`
- `cmd /d /c git diff --check`

Mike performs live AO client playtesting. Do not claim live validation unless Mike reports it.
