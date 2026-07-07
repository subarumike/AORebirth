# Corpse Open, Loot, And Credit Validation

Capture folder:

`tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260707-172254`

Status: validated against AORebirth private-server live client capture.

## Validated Behavior

- Corpse open ordering is validated:
  - OUT `GenericCmd Use`
  - IN `InventoryUpdate`
  - IN `Stat Cash`
  - IN `GenericCmd` success ack
- Corpse item transfer is validated:
  - OUT `ClientMoveItemToInventory`
  - IN `ContainerAddItem`
- Duplicate loot prevention/state cleanup is validated:
  - Reopening the fully looted corpse returned `InventoryUpdate` with `Items=count=0[]`.
- Corpse credit payout is validated:
  - One `Stat Cash` update was emitted for the credited corpse.
  - Reopen did not duplicate the Cash update.
  - Item transfers did not emit additional Cash updates.
- XP remains death-tied:
  - XP/UnsavedXP and `You received 1 xp.` occurred on NPC death, not corpse open or item transfer.
- No corpse credit chat/feedback packet was validated:
  - No server `FormatFeedback` or credit `ChatText` packet was captured for corpse credit payout.

## Retest Guidance

Do not retest corpse open, corpse item transfer, or corpse credit payout unless related code changes.
