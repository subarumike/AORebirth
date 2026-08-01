# Current Task

## Active

TASK ID: GENERATED-MISSION-ACCEPTED-PROJECTION-001

Generated-terminal mission acceptance now freezes one complete accepted
projection instead of depending on process-local roll data. Version 1 is stored
under `mission-state/acg-accepted-projections` as 65 deterministic `key=value`
fields with the complete serialized roll response body encoded as Base64,
SHA-256 integrity, and atomic replacement. The selected offer is recovered from
that body and cross-validated against the stored offer index, original offer ID,
accepted quest ID, owner, binding, rewards, QFU contract, artifacts, expiry,
and lifecycle fields; malformed, partial, unknown-version, duplicate, or
conflicting records fail closed.

Acceptance is idempotent by owner plus original offer identity. The offer is
durably claimed before the binding, objective, key, mission artifact, accepted
state, or QFU is exposed. Persisted acceptance phases make each later boundary
recoverable: binding and objective persistence; key and exact Repair/Return
Item artifact grants; accepted-state commit; and pending/sent QFU delivery.
Retries and reconnects resume the same accepted quest, bundle, building, PF2,
key, frozen Repair artifact template/identity, frozen rewards, action fields,
title/description, and
type-specific structured QFU rather than allocating or recalculating them.

Offer expiry and accepted-mission expiry are independent: acceptance requires a
live offer, then starts the existing 48-hour accepted duration. Startup cleans
expired pre-binding reservations without waiting for owner reconnect, and
reconnect suppresses the second mission-list QFU when recovery already sent the
exact pending accepted QFU.

Generated offer IDs use a separate version-1 durable cursor under
`mission-state/offer-identities`, with collision checks against live offers,
accepted projections, and bindings. Ambiguous legacy generated accepted-state
rows cannot reconstruct the complete projection and are rejected rather than
filled from defaults. Authored quests and true legacy missions keep their
existing owners. No database schema, reward formula, slider, loot, ACG payload,
or procedural-generation behavior changes in this stage.

## Previous completed status

TASK ID: GENERATED-MISSION-LEGACY-FALLTHROUGH-001

Generated-terminal mission runtime ownership is being made fail-closed at every
legacy boundary. An allocated PF2, reversibly encoded runtime identity, exact
accepted mission artifact, or exact exterior marker that belongs to a generated
mission may dispatch only through its accepted binding and instance-scoped
runtime. Missing or invalid generated state is an explicit rejection, never
permission to continue into replay-era spawn, global/newest mission selection,
template-only interaction, shared-playfield routing, or legacy completion.

The ownership fence is deliberately exact. The allocator's numeric PF2 range is
not sufficient because legacy mission instances also allocate within that
range. True legacy and authored-quest traffic retains its existing handlers.
This stage does not alter ACG payloads, rewards, sliders, loot, token progress,
expiry behavior, database schema, or procedural generation.

## Previous completed status

TASK ID: OFFICIAL-MISSION-LEVEL-GRAPH-001

Generated-terminal mission rolling requires one complete validated official
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
state, ACG layout, authored quest, or database behavior changes in that task.

## Earlier completed status

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

## Local WIP (not pushed)

### Pet owner dialogue (Mike)

Pet command announces currently use Zone FormatFeedback (brown AOML + leading
`: `) so lines show without ChatEngine. Live capture `20260731-085057` still
points at chat type 35 Your Pets for the real Public Groups toggle; that path
is not required for the visible FormatFeedback fallback.
