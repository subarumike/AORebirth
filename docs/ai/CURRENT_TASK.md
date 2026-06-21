# Current Task

## Active Task

Make Book of Knowledge behave as a backpack-style container and keep it out of other containers.

## Current Scope

- Use live AOSharp capture evidence for the right-click and drag behavior.
- Keep the change in the existing inventory/container classification and move validation path.
- Do not rewrite inventory, bank, or backpack systems.
- Do not change database schemas or perform destructive database operations.
- Preserve normal non-container item moves into backpacks.
- Preserve backpack-to-inventory moves.
- Preserve existing bank/backpack behavior.
- Add focused logging/validation consistent with existing patterns.

## Live Evidence

- Capture `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260621-052003` shows right-click on Book of Knowledge sends `GenericCmd Use` for `Inventory:0049`.
- The server resolved that slot to `ItemLowId=99302 ItemHighId=99302` and sent `TemplateAction` plus a success ACK instead of a backpack container open.
- Dragging the same item into a real backpack was accepted because the shared container classifier did not recognize `99302/99302` as a container.

## Validation Plan

- Run `cmd /d /c tools\build_aorebirth_debug.cmd`.
- Run `git diff --check`.
- Review final `git status --short --branch`.
- Commit only intended source/doc changes.
