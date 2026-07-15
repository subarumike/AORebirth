# Current Task

## Current Focus

Mail lives in subsystem `ZoneEngine/Core/Mail/`. Documented pull/merge rules so subsystem work survives GitHub pulls. Pets extraction is next when Mike prioritizes it.

## Done in this slice

- Moved Mail runtime + handler into `AORebirth/Server/ZoneEngine/Core/Mail/` (`ZoneEngine.Core.Mail`).
- Added `docs/project/SUBSYSTEMS.md` and Known Decision: systems own a folder; commit before pull; always `--no-rebase`.
- Updated Architecture / AI_START_HERE pointers.

## Remaining

1. Live-validate Mail after engines restart (attach / Take All / NoDrop).
2. Extract Pets into `Core/Pets/` when Mike asks (currently `Pet*.cs` still under Core root).
3. Subway when Mike returns that priority.

## Constraints

- Do not grow new system logic in `Playfield.cs`.
- Mail still in-memory only; no DB schema without approval.
- Always `git pull --no-rebase`; commit subsystem first.
