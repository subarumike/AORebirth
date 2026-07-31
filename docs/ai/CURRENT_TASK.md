# Current Task

## Active

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

## Previous completed status

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

## Local WIP (not pushed)

### Pet owner dialogue (Mike)

Pet command announces currently use Zone FormatFeedback (brown AOML + leading
`: `) so lines show without ChatEngine. Live capture `20260731-085057` still
points at chat type 35 Your Pets for the real Public Groups toggle; that path
is not required for the visible FormatFeedback fallback.
