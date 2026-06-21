# Current Task

## Current Status

- `docs/project/PROJECT_STATE.md` is the primary Codex memory file.
- AI startup/read order reads `docs/project/PROJECT_STATE.md` before this file.
- Current stable baseline commit: `d243fbb1` (`Fix backpack container open, movement, and persistence`).
- Backpack container open, close, item movement, worn-slot open, zoning visibility, and persistence are committed and pushed.
- Current dirty work is the BS Signup OFAB profession armor terminal data repair.
- Live capture for this task is `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260620-230138`.
- Live evidence confirmed the accessible Meta-Physicist OFAB terminal opens with `GenericCmd Use` to `VendingMachine:C0091777`, followed by `ShopUpdate`, `Trade Open`, and `GenericCmd` success ack.
- Live denial capture `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260620-234156` confirmed profession-locked terminal attempts receive only `GenericCmd Use` failure acks with `Temp1=2`; no `ShopUpdate` or `Trade Open` follows.
- Live client feedback for denied profession terminal attempts includes `This effect can only be utilitized by <Profession>.` followed by `Your GM capabilities is required to be at least 1!`.
- The remaining OFAB profession armor terminals are profession-locked for the current live character, so direct full shop capture is not feasible from this account/character.
- The current repair uses the live Meta-Physicist shop packet/content shape plus the current `docs/Ofab` profession item lists and item-name data for profession accessories.
- Current source repair gates the fourteen OFAB profession armor terminal hashes by character profession before the shop-open path emits `ShopUpdate` or `Trade Open`, and sends the captured denial feedback text.

## Active Task

Validate the dirty OFAB profession armor terminal data and profession-lock repair.

## Explicit Non-Goals

- Do not edit Marcus/Rex/Arete quest files.
- Do not edit backpack, bank, trade, or KnuBot trade code for this task.
- Do not change database schemas.
- Do not perform destructive database operations.
- Do not stage or commit without user instruction.
- Do not treat non-Meta-Physicist OFAB profession terminal stock as directly captured; it is capture-limited fallback data.

## Next Safe Options

- Smoke the private OFAB terminal behavior: matching profession armor terminal opens; other profession armor terminals do not; General/Melee/Ranged still open.
- Keep the lock limited to the captured OFAB profession armor terminal hashes.
- Do not commit until Mike confirms the private smoke result.
