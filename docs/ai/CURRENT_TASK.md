# Current Task

## Current status

- `docs/project/PROJECT_STATE.md` is the primary Codex memory file.
- AI startup/read order reads `docs/project/PROJECT_STATE.md` before this file.
- Backpack open repair is committed in `e85440de` (`Implement backpack open protocol handling`).
- Current uncommitted work extends the backpack repair for item movement, worn-slot open/close, and post-zone persistence visibility.
- Manual private-server smoke confirmed right-click open, X close, and right-click reopen of the same backpack.
- Latest private-server smoke confirmed bags persist, items inside bags persist, and bags can be opened from both worn slots and inventory.
- Confirmed client behavior: backpack open is `GenericCmd Use` on `Inventory:<placement>`, not a double-click-only path.
- Confirmed live response shape: `ChestFullUpdate` for `Container:<id>`, `InventoryUpdate` for `Container:<id>`, then a `GenericCmd` success ack for initial open; known close/reopen uses live-shaped `Action` packets `0x66` and `0x64` plus the normal ack.
- Confirmed worn-slot behavior: right-clicking a bag in `ArmorPage:<slot>` or `SocialPage:<slot>` sends `GenericCmd Use` for that worn slot; X close sends `GenericCmd Use` for `Container:<id>`.
- Confirmed movement behavior: inventory-to-backpack uses `ClientContainerAddItem` with `Target=Container:<id>` and `Source=Inventory:<slot>`, answered by `ContainerAddItem`; backpack-to-inventory uses `ClientMoveItemToInventory` with `SourceContainer=Backpack:<handle/slot>`, answered by `ContainerAddItem`.
- Backpack pages are keyed by `Container:<id>`, not by backpack template id or inventory slot.
- Backpack page handles from `InventoryUpdate` are mapped back to `Container:<id>` so `Backpack:<handle/slot>` move-out packets can resolve to the correct page.
- Current-session live smoke for the backpack movement/open/persistence follow-up passed after another round of testing.
- Closure audit is complete; no source changes should be made unless a regression is found.
- Rex works through B18F on the current stable baseline.
- Marcus quest chain is not implemented and remains on the back burner.
- Next development task should be selected from non-quest gameplay bugs.

## Stable baseline

- Current stable baseline commit: `e85440de`.

## Active task

Backpack item movement plus worn-slot open/persistence repair is live-smoked, validated, dirty, and awaiting commit instruction.

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
- Do not expand backpack work beyond the captured item movement/open/close/persistence paths.
- Do not edit bank repair unless direct evidence requires it.
- Do not stage or commit without user instruction.

## Next safe options

- Review `docs/project/PROJECT_STATE.md` before selecting any new implementation task.
- Select the next development target explicitly before code changes.
- Prefer non-quest gameplay bugs for the next work item.
- Recommended next step: commit the backpack movement/worn-slot/persistence repair when Mike is ready.
