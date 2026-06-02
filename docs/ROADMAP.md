# Roadmap

Generated: 2026-06-02

## Phase 1 - Stabilize The Active Worktree

Dependencies: none.

Tasks:

- Separate documentation-only work from gameplay code changes.
- Review dirty source files and group them by system.
- Avoid committing unverified NPC movement changes as stable.
- Keep source assertions for locked packet behavior.
- Preserve `CellAO/Documentation/ProjectWorkingReference.md` as active session memory.

## Phase 2 - Repair Inventory, Corpse Credits, And Trade

Dependencies: Phase 1.

Tasks:

- Build a full trace of cash stat changes, corpse credit chat text, and inventory/container packet responses.
- Compare `ContainerAddItem`, `ClientMoveItemToInventory`, `InventoryUpdate`, `InventoryUpdated`, and player trade packets against captures and AOSharp models.
- Fix stale trade-window item state.
- Fix trade credit transfer.
- Validate item transfer without visual duplication or invalid item copies.
- Add smoke/source assertions for the confirmed packet paths.

## Phase 3 - Harden Death, Corpse, Loot, And DB Loot

Dependencies: Phase 2.

Tasks:

- Keep modern death/respawn path locked.
- Clean `CorpseFullUpdate` toward current-client and captured structure.
- Expand DB-backed loot only from verified mob/template evidence.
- Keep deterministic test-mob loot separated from real DB loot.
- Add replay or source assertions for corpse open, item move, credit loot, and despawn.

## Phase 4 - Rebuild NPC Movement From Evidence

Dependencies: Phases 1-3.

Tasks:

- Mine one or more captured combat chase windows into replay fixtures.
- Add local packet tap/trace for CellAO S2C movement packets.
- Compare local movement packet sequence against official/private evidence.
- Build a minimal server-authoritative enemy behavior contract.
- Only then wire runtime `NPCController` changes.

## Future

- Quest state service based on captured `QuestFullUpdate` and KnuBot flows.
- Team, organization, and player social systems.
- Player shops, vendors, and economy cleanup.
- Better DB data quality tooling.
- Playfield population and pathing using verified navmesh/data sources.
- More complete packet serializer tests for recovered N3 messages.
- Long-term refactor of `Playfield.cs` into narrower services.

