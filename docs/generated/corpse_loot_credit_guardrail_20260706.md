# Corpse Loot Credit Guardrail - 2026-07-06

Scope: guardrail only. No runtime behavior was moved.

## Current Protected Flow

### Corpse Access

`Playfield.TryUseCorpse` delegates access sequencing through `PlayfieldRuntimeSystems.TryUseCorpse` and `PlayfieldCorpseAccessRuntimeService`.

Protected order:

1. corpse access action is sent when the existing access-only branch requires it
2. use action finished acknowledgement follows that access action
3. corpse inventory update is emitted before credit award scheduling
4. corpse despawn/lifetime scheduling remains in the existing access service sequence

### Corpse Item Transfer

`Playfield.TryLootCorpseItem` delegates item-transfer sequencing through `PlayfieldRuntimeSystems.TryLootCorpseItem` and `PlayfieldCorpseAccessRuntimeService`.

Protected order:

1. unique-item validation happens before inventory insertion
2. inventory insertion happens before corpse loot state is marked looted
3. corpse item state is marked looted before the corpse is marked opened
4. corpse opened state is marked before the `ContainerAddItemMessage` callback
5. empty/remaining corpse despawn or lifetime extension follows the transfer callback

`InventoryContainerRuntimeService` remains the owner of unique-item checks and corpse loot inventory insertion helpers.

### Corpse Credit Award

`Playfield.ProcessPendingCorpseCreditAwards` delegates due-award iteration through `PlayfieldRuntimeSystems.ProcessPendingCorpseCreditAwards`.

`Playfield` intentionally still owns:

- `pendingCorpseCreditAwards`
- `AwardCorpseCredits`
- `corpse.CreditsLooted` mutation
- cash stat mutation
- changed-stat notification
- stat persistence write

## Intentional Playfield Ownership

The following stays in `Playfield` until a future behavior-preserving extraction has stronger coverage:

- corpse state dictionaries
- pending credit award dictionaries
- corpse inventory packet construction
- corpse container add packet construction
- corpse access action packet construction
- item and credit roll/materialization callbacks
- cash/stat mutation and persistence

`PlayfieldCorpseAccessRuntimeService` must remain orchestration-only and must not own packet construction, item materialization, credit mutation, pending-credit storage, or inventory algorithms.
