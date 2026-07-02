# InventoryContainerRuntimeService Epic Checkpoint

Date: 2026-07-02

## Scope

Inventory/container runtime orchestration is now centralized behind
`InventoryContainerRuntimeService` for the safe surfaces covered by the epic.
This checkpoint is a guardrail and documentation pass only; it does not change
runtime behavior.

## Centralized Ownership

`InventoryContainerRuntimeService` owns orchestration for:

- backpack open, close, handle registration, and transfers
- bank open and bank slot flow
- container add, move, and ownership routing
- inventory item use
- full-character inventory page filtering
- character action inventory mutations
- trade inventory helpers
- corpse loot transfer inventory add and persistence
- quest reward inventory grant and duplicate carried-item checks
- vendor shop inventory page lookup, purchase offers, sell offers, and return helpers
- tradeskill source/target item resolution, result insertion, and delete mutations
- KnuBot trade lookup and trade item remove helper

Message handlers and controllers should call the service instead of owning
inventory/container orchestration directly.

## Included Local Commits

The final finishing commits in this checkpoint batch are:

- `72775837` Move corpse loot inventory transfer into service
- `a82a1c42` Move quest reward inventory grant into service
- `0821e1bb` Move vendor shop inventory helpers into service
- `1fc8606b` Move tradeskill inventory orchestration into service
- `9b009447` Move KnuBot trade lookup into inventory service

Earlier local InventoryContainerRuntimeService commits in the same ahead batch
covered bank, backpack, container movement, item use, full-character inventory
page handling, character action mutations, trade helpers, weapon visual repair,
inventory routing, and the first ownership guardrails.

## Remaining Excluded References

Direct `BaseInventory` references outside the service are intentionally limited
to these areas:

- `GuestKeyGeneratorInteractionHandler.cs`: captured guest-key flow remains
  isolated until that feature receives its own lifecycle work.
- `NpcCombatTickCoordinator.cs`: combat weapon reads are gameplay/combat logic,
  not inventory orchestration.
- `Playfield.cs`: legacy combat weapon read path remains gameplay/combat logic.
- `WeaponItemFullUpdate.cs`: weapon page read is packet formatting and
  serialization.

These are not safe to move as part of the inventory/container ownership epic
without opening a combat/equipment or packet-serialization boundary.

## Guardrail

`InventoryContainerRuntimeServiceFinalOwnershipGuardrailAllowsOnlyNamedRemainingReferences`
asserts that the service exposes the expected ownership surfaces and that any
future direct `BaseInventory` reference under `ZoneEngine\Core` must either be
inside the service or added to the explicit exception list.

## Recommended Next Major System

Start a combat/equipment runtime boundary before further combat tuning. The
remaining inventory references are weapon reads used by combat and weapon packet
formatting; those should move only behind a combat/equipment model that can
preserve attack timing, stat modifiers, and packet output.
