# Current Task

## Active

TASK ID: OFFICIAL-MISSION-LEVEL-GRAPH-001

Generated-terminal mission rolling now requires one complete validated official
mission-level graph before it can resolve a mission QL. Runtime data is compiled
from the canonical checked-in `XML Data/MissionLevels.csv` by
`tools/generate_mission_level_graph.cmd`; the production ZoneEngine no longer
searches for or partially reads an external CSV or ODS at runtime.

The generated format contains exactly levels `1..220`, exactly difficulty
positions `Q0..Q10`, and the unchanged official token column. Its canonical
LF-normalized source and payload SHA-256 is
`295ade2cac00ddfc975bbf1c3f0d7f953f3726e08cc21c0c1f32a5b5b30eb70f`.
The upstream ODS SHA-256 is
`5efdba9a2e8310253246d82a9e733d90b32bb4b360a035c157f9d81832f4a0e7`;
its mission cells match the canonical table through level 133, but levels
134–220 were spreadsheet-coerced into lossy scientific notation, so the ODS is
provenance rather than an exact full-table regeneration source.

The loader rejects malformed headers or decimal tokens, duplicate rows or
difficulty cells, missing or extra rows/columns, levels or difficulty indexes
outside their exact ranges, mission QLs outside `1..250`, token counts outside
the unchanged `1..9` table range, row/column/token decreases, and any row whose
neutral `Q5` value does not equal its level. It validates the embedded payload
hash and full deterministic serialization before atomically publishing one
immutable snapshot. A failed reload cannot partially replace a valid snapshot;
without a valid snapshot, mission rolling fails explicitly before any fee is
charged.

No mission-location selection, sliders, rewards, token progress, expiry, corpse
state, ACG layout, authored quest, or database behavior changes in this task.

## Previous completed status

TASK ID: GENERATED-MISSION-TOKEN-PROGRESS-001

Generated-terminal mission token progress is being moved from the process-local
tracker to a durable record owned by one exact accepted quest. The existing
tracker can otherwise lose progress on restart and can conflate activity by
character/playfield instead of proving the accepted quest, objective, runtime
source, and allocated PF2 that own a death event.

The version-1 sidecar lives under `mission-state/acg-token-progress`. It uses
deterministic key ordering, a SHA-256 integrity hash, atomic replacement, and
fail-closed loading. One record freezes the accepted quest, explicit solo owner,
mission type, objective binding, allocated live PF2, captured materializable
Ambient-slot denominator, and exact per-death event journal. Each event binds a
deterministic event ID to its runtime source identity, captured Ambient slot,
spawn generation, and actor identity.

Event recovery distinguishes `NotObserved`, `Validated`, `DurablyApplied`,
`ClientUpdatePending`, `ClientUpdateSent`, and `TerminalFailure`. Validation,
objective verification, completion, abandonment, and expiry use mutually
exclusive lifecycle claims so one persisted transition wins. Duplicate packets,
callbacks, restart recovery, and client-notification retries may resume an
incomplete event but cannot increment its applied count twice.

Only captured materializable `Ambient` slots form the generated-mission token
denominator; objective slots are excluded. The existing progress formula is
preserved as `floor(applied * 100 / total)`, with a known exact zero denominator
equal to `100`. This stage does not change token amounts, token reward rules,
rewards, or accepted QFU fields. A pending feedback update records a server send,
not a client acknowledgement.

Migration is deliberately narrow. An active mission with no token sidecar is
safe to initialize only when every countable Ambient source is still alive.
Prior Ambient deaths with no sidecar are ambiguous and fail closed as invalid.
An existing exact sidecar can reconcile a persisted dead source using its exact
quest/PF2/runtime-slot identity without replaying an already applied event.
Cleanup removes transient runtime registration but retains the durable audit
record so a later callback cannot replay progress.

Durable team token distribution remains deferred because generated mission
bindings currently have authoritative explicit no-team ownership only. Authored
quests and unrelated token systems retain their existing paths.

## Earlier completed status

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
That gate also holds a short completion-transition lease across both durable
`CompletionStarted` writes, closing the validation-to-persistence abandonment
race without changing the persisted lifecycle model. Restart recovery accepts
only the exact split state of binding `CompletionStarted` plus objective
`ObjectiveVerified` and finishes the second write before the deadline.

Inside-at-expiry evacuation is a provisional private-server policy: use the
persisted exterior destination with the existing outdoor standoff when valid,
otherwise use the side hub. It is not claimed as official behavior.

Deferred live smoke: use a short-lived test mission or persisted fixture to
verify expiry while outside and inside the mission, reconnect cleanup, exact
Quest Delete, exact key/item removal, and PF2 release. The existing
capture-backed `21–87` generated-mission corpse-credit repair remains intact;
ordinary/authored corpses are unchanged.
