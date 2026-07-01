# Current Task

## Active Task

Playfield content-module foundation: private-city ready/init guardrails.

This is a foundation/test-harness scope. Do not make gameplay behavior changes while this task is active.

## Current Target

- Flow: private-city ready/init packet and lifecycle ordering
- Primary owner today: `Playfield.cs` plus the `ClientConnected` ready flow
- Harness: `PlayfieldLifecycleTrace`
- Regression target: private-city org/init state before `FullCharacter`, followed by towers/cities ready-block packets

## Completed Foundation Work

- `6dc792d7` added the first Playfield content-module boundary.
- `a6fb22a2` fixed the unrelated item/nano loader startup blocker.
- Private-city ready/init now has trace-only begin/end and summary markers for the current ready sequence.
- Playfield lifecycle tests now assert the private-city ready/init packet message order and key org/towers/cities details.

## Next Foundation Work

Recommended next task:

`Extract Private City Ready Init Sequencing From Playfield`

Keep the extraction behavior-preserving. Do not implement CityAdvantages, org command flows, ownership management, city purchase logic, guest-key lifecycle, combat tuning, or capture tooling changes.

## Regression Risks Only

Preserve these while changing private-city ready/init boundaries:

- Private-city zoning, guest-key generator, City Controller open/close, and private-city org initialization.
- `/org info` behavior.
- Same-playfield player visibility and movement rendering.
- Cleaning robot combat, patrol, death, corpse, despawn, and loot behavior.

## Validation Plan

For trace/test-only changes:

- `cmd /d /c git diff --check`
- focused Playfield lifecycle tests

For production trace code changes:

- `cmd /d /c tools\build_aorebirth_debug.cmd`
- `cmd /d /c restart-engines.cmd`

Mike performs live AO client playtesting. Do not claim live validation unless Mike reports it.
