# Current Task

## Current Focus

Mail Terminal restored from orphaned commit `d63ce53a` ("Fixing mail") that was dropped when pull rebased onto `8bb22776` (Contain invalid GUI tree keys). That pull is client-proxy only; it did not contain mail fixes — the rebase discarded local "Fixing mail".

## Done in this slice

- Recovered mail stack from `d63ce53a`: Messaging types/serializer, `MailRuntimeService` (939-line attach/TakeAll/Delete path with RemoveItem+SendDeleteItem), handler, InventoryItemRules mail guards, FullCharacter/InventoryUpdate container identity helpers, CharInPlay envelope sync.

## Remaining

1. Restart engines and live-validate: attach item leaves sender inventory; recipient Take All gets real item; NoDrop/container send reject; credits still OK.
2. Backpack still may drag into Item field (client GUI) until Container dynel binding is proven — send must still reject.
3. Subway when Mike returns that priority.

## Constraints

- Mail is in-memory only (cleared on Zone restart).
- Do not change database schemas.
