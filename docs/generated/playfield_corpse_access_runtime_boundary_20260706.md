# Playfield Corpse Access Runtime Boundary

## Scope

This checkpoint introduces `PlayfieldCorpseAccessRuntimeService` as the named owner for corpse access and loot orchestration.

The service owns:

- corpse use/access routing
- dead-NPC corpse use routing to the matching corpse identity
- corpse loot transfer branch orchestration
- corpse inventory update then credit-award callback sequencing
- due pending corpse credit award processing

## Preserved Playfield Ownership

`Playfield` intentionally still owns the behavior that must not move into the access runtime service:

- packet construction and emission
- corpse state dictionaries and mutations
- item creation and loot materialization
- credit math and stat persistence
- inventory transfer algorithms delegated through `InventoryContainerRuntimeService`
- corpse lifetime storage and object cleanup state

## Behavior

No gameplay behavior, packet payloads, packet ordering, corpse lifetime timing, inventory behavior, credit behavior, or persistence behavior changed.

The service receives callbacks for all packet, state, item, inventory, and credit work so the moved boundary is orchestration-only.
