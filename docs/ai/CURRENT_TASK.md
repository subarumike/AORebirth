# Current Task

## Active

No implementation is active after delivery of the PF1931 post-login crash
repair. The standing gameplay priority remains incremental capture-backed PF127
Subway work when Mike supplies or requests the next capture task.

## Latest delivered reconciliation

- Removed the PF1931 `PlayfieldAnarchyF` generated-building identity that was
  emitted without a generator payload and crashed the official client.
- Added a fail-closed serializer contract preventing generated-playfield
  identities without exact payloads.
- Preserved the complete 43-door captured initial replay unchanged.
- Confirmed Soldier could enter PF1931, remain connected, and exit to PF647 with
  the full replay enabled.
- Contained the independent PF6553 Marcus heartbeat exception without inventing
  missing attack-start context.

## Delivery acceptance

- Focused Temple, packet-shape, and Marcus combat-context tests pass.
- Complete mandatory integration gate passes twice from the unchanged final
  commit and leaves the worktree clean.
- Debug build passes, database preflight passes, Chat/Login/Zone are restarted
  through approved wrappers with exact port ownership, and optional WebEngine
  remains inactive.
- Audit evidence: `docs/evidence/TEMPLE_POST_LOGIN_DOOR_CRASH_20260804.md`.
