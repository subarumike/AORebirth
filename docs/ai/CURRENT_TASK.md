# Current Task

## Active

TASK ID: GENERATED-MISSION-LIVE-EXPIRY-001

Generated-terminal missions now use their persisted absolute `ExpiryUtc` as
live authority. A process-wide scheduler immediately blocks expired objective,
combat, corpse, chest, door, terminal, machine, entry, and token activity;
evacuates connected occupants; durably cleans only the exact accepted
mission's runtime state and inventory artifacts; sends exact Quest Delete
without completion rewards; and releases the allocated PF2 only after every
cleanup predicate is independently verified.

The version-1 SHA-256 expiry journal is restart-resumable. Startup restores
incomplete cleaned-release PF2 holds before new mission allocation is exposed.
Offline owners keep the exact PF2 reserved until reconnect permits owner
inventory and client-state reconciliation. Expiry wins before
`RewardClaimStarted`; durable completion wins at and after that phase.
Abandonment shares the same atomic owner gate: it may win only before the
deadline and cannot interleave cleanup after expiry or durable completion owns.

Inside-at-expiry evacuation is a provisional private-server policy: use the
persisted exterior destination with the existing outdoor standoff when valid,
otherwise use the side hub. It is not claimed as official behavior.

Deferred live smoke: use a short-lived test mission or persisted fixture to
verify expiry while outside and inside the mission, reconnect cleanup, exact
Quest Delete, exact key/item removal, and PF2 release. The existing
capture-backed `21–87` generated-mission corpse-credit repair remains intact;
ordinary/authored corpses are unchanged.
