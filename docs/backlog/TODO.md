# TODO

Generated: 2026-06-02

## Critical

- Trace and repair corpse credit desync at the source.
- Verify no packet path is treating placement sentinel values as credit amounts.
- Revalidate player trade credits after corpse credit flow is stable.
- Keep database changes restricted to `cellao_codex_clean`.

## High Priority

- Compare CellAO inventory/trade/corpse packet classes against AOSharp and captured packet evidence:
  - `ContainerAddItem`
  - `ClientContainerAddItem`
  - `ClientMoveItemToInventory`
  - `InventoryUpdate`
  - `InventoryUpdated`
  - `Trade`
- Add/update smoke assertions for confirmed inventory/trade packet contracts.
- Split current dirty gameplay changes into clear buckets before committing.
- Preserve `FullCharacter` version 26 and live-style login state.
- Keep death/respawn current-client path locked.

## Medium Priority

- Clean `CorpseFullUpdate` toward evidence-backed current-client shape.
- Repair or model `PlayfieldAnarchyF` only with captured login/playfield-entry bytes.
- Add decode/test-only message classes for recovered missing N3 packets where useful.
- Build local S2C packet tap for CellAO movement/combat comparison.
- Mine one chase capture into enemy movement replay data.

## Low Priority

- Reduce permanent debug-log noise.
- Improve docs for debug chat commands and common playtest commands.
- Document known-live test item IDs and mob spawn commands.
- Add a source index for external AOSharp/AODB/stripdown references.

## Future Ideas

- Refactor `Playfield.cs` into smaller services after current gameplay repairs stabilize.
- Add quest-state service from captured quest flows.
- Add better DB data quality tooling for mob loot and item templates.
- Integrate navmesh/path data only after packet movement contract is correct.
- Create a formal packet truth table for every implemented N3 message.

