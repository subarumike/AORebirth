# Current Task

## Active Task

Fix grid terminal use so `Terminal:C0040320` transfers the player into the captured destination instead of stopping after statel lookup.

## Current Scope

- Keep changes scoped to grid terminal/statel use and existing teleport/zone transfer behavior.
- Use capture or repo-owned evidence before changing packet behavior.
- Do not rework surgery clinic, bank, generic statel semantics, or database schemas.

## Current Evidence

- Baseline commit: `3f5fa044`.
- `Terminal:C0040320` is captured as `Enter The Grid` in Borealis at `(636.4026, 66.81002, 728.8094)`.
- Live terminal-use captures show `GenericCmd Use` followed by `N3Teleport` to playfield proxy `0xC79E:00000098`, playfield `152`, with the character's current position and heading.
- No separate successful `GenericCmd` acknowledgement was observed between use and teleport.
- `tools-temp/playfield-teleport-audit.csv` maps raw statel instance `0xC0040320` to playfield `2063`, but live terminal-use capture supersedes this older audit row for the grid terminal route.
- Current server log reaches `Found Statel with 1 events`, which is emitted by `PlayerController.UseStatel` before the statel `OnUse` event dispatch.

## Validation Plan

- Run `cmd /d /c git diff --check`.
- Run `cmd /d /c tools\build_aorebirth_debug.cmd`.
- After successful rebuild, run `cmd /d /c restart-engines.cmd`.
- Live smoke: use `Terminal:C0040320`, confirm playfield transfer to the captured destination, client stability, surgery clinic behavior, and bank behavior.

## Validation Result

- `cmd /d /c git diff --check`: PASS.
- `cmd /d /c tools\build_aorebirth_debug.cmd`: PASS after stopping the running engine that held `ZoneEngine.exe` open.
- `cmd /d /c restart-engines.cmd`: PASS on retry; `LoginEngine` required one forced stop after the first restart attempt did not open port `7500`.
- Live client smoke for `Terminal:C0040320`: pending.
