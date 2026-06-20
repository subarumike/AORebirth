# Current Task

## Current status

- `docs/project/PROJECT_STATE.md` is now the primary Codex memory file.
- AI startup/read order now reads `docs/project/PROJECT_STATE.md` before this file.
- Bank repair closure is complete as of 2026-06-20.
- Manual private-server smoke confirmed bank deposit, withdraw, slot persistence, close/reopen, zone change, relog, and persistence.
- Root cause: inventory-to-bank drag uses `ClientContainerAddItem` (`0x1F4D5F7E`) with base character identity, one-byte `Unknown`, target `0xDEAD:<char>`, and source `Inventory:<slot>`; AO Rebirth previously decoded the message as two immediate identities and ignored/misread the actual deposit path.
- Serializer repair: `ClientContainerAddItemMessageSerializer` now reads `N3MessageType`, base `Identity`, `Unknown`, `Target`, and `Source`.
- Bank persistence repair: `BankSlot.Placement` and `BaseInventoryPage.ToInventoryArray()` now emit real bank slot keys, and successful deposits persist through `BaseInventory.Write()`.
- Validation performed: `N3RecoveredContractTests` passed `11/11`; `AORebirth.Core` Debug focused build passed; `ZoneEngine` Debug focused build passed; `git diff --check` passed with only LF-to-CRLF warnings; Chat/Login/Zone were restarted after rebuild.
- Files changed for bank repair: `ClientContainerAddItemMessage.cs`, `RecoveredN3MessageSerializers.cs`, `BankSlot.cs`, `N3RecoveredContractTests.cs`, `BaseInventoryPage.cs`, `ClientContainerAddItemMessageHandler.cs`, and `ZoneEngine.csproj`.
- Rex works through B18F on the current stable baseline.
- Marcus quest chain is not implemented.
- The Marcus dirty vertical slice was rolled back.
- Last live-smoked committed quest baseline is `ecbca7d` (`Implement Marcus B18F to B194 transition`).
- Uncommitted Marcus Phase 4B item `296780` handout work exists in `MarcusB18FCompletionHandler.cs`.
- The item handout has focused ZoneEngine build/search validation, but it has not had live smoke.
- Marcus quest work is paused and on the back burner.
- Worktree also has a pre-existing `.gitignore` modification.
- Next development task should be selected from non-quest gameplay bugs.

## Stable baseline

- Current stable baseline commit: `0946690`.

## Active task

Bank repair closure complete. Await user selection of the next non-quest gameplay bug.

## Explicit non-goals

- Do not continue Marcus quest implementation.
- Do not run Marcus live smoke unless Mike explicitly resumes it.
- Do not implement gas fire use.
- Do not implement B196 or B197.
- Do not implement Flint.
- Do not implement KnuBot trade.
- Do not add Marcus rewards.
- Do not add DB mission persistence.
- Do not modify Rex unless user asks.
- Do not alter gate behavior.
- Do not stage or commit without user instruction.

## Next safe options

- Review `docs/project/PROJECT_STATE.md` before selecting any new implementation task.
- Select the next development target explicitly before code changes.
- Prefer non-quest gameplay bugs for the next work item.
- Recommended next investigation: audit backpack/container item movement next, because it shares container identity and slot-placement behavior with the repaired bank path while staying adjacent to the current evidence.
- Before switching work, decide whether to commit the validated-but-unsmoked item handout or revert it.
