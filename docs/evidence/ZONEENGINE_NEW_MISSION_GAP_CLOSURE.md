# ZoneEngine_New remaining mission gap closure

## Scope

Reviewed against checkpoint `05c8d4ef7429e1ed765a19bd63ace0e21a2b29e5` on the existing
`codex/zoneengine-new-gameplay-reconciliation` worktree. This is a classification
of the documented remaining limits, not a new whole-engine or retail-AO audit.
No production, client, capture, migration execution or schema change is authorized
by this slice. Existing reward, corpse-currency, DOJA and cleanup transactions remain
the authority. The coordinating task owns builds and commit/push/master decisions.

## Baseline gap matrix

| Gap | Exact Legacy/current evidence | Checkpoint New behavior and connection | Classification / severity | Required for master? | Repair or disposition |
| --- | --- | --- | --- | --- | --- |
| Top-level bank/carried-page mission-key possession and cleanup | `AORebirth.Core/Inventory/PlayerInventory.cs:58` registers bank in `Pages`; `BaseInventoryPages.Read:385` hydrates all those pages; `MissionKeyGrantService.HasMissionKeyInstance:966` and `TryRemoveExactMissionArtifact:447` scan them. | `GeneratedMissionAcgService.TryEnter` checks main inventory only; `GeneratedMissionService.CleanupArtifacts` and generated DAO cleanup reject every other page. A banked exact key blocks entry and retains the ended mission's PF reservation. | `MASTER_BLOCKER`, supported Legacy regression | Yes | Extend only exact owner top-level page verification, preserving full row CAS and cleanup/checkpoint atomicity. A nonhydrated bank uses the existing durable item row; no invented recursive owner tree. |
| Backpack-interior cleanup / key eligibility | Legacy `BaseInventoryPages.GetOrCreateBackpackPage:244` stores interiors in a separate `backpackPages` dictionary, not `Pages`; both exact mission consumers above omit it. | New rejects unverified container ownership and preserves artifacts/lease pending. | `LEGACY_BUG`, existing omission | No | Do not promote a recursive scan as accepted parity. Keep fail-closed state and document the separate owner-tree prerequisite for any future repair. |
| Existing occupied Legacy generated sidecars on engine switch | Prior mission report explicitly requires reviewed typed import; Legacy offer/binding/object state is file-backed. | New normalized SQL is authoritative; no silent import, deletion or opaque sidecar compatibility store. | `FUTURE_FEATURE` for master; mandatory occupied-deployment prerequisite | No | A real occupied switch still requires separately reviewed migration/import and occupied-state acceptance. Master readiness is not production-switch authorization. |
| Generated corpse item loot | Existing accepted generated corpse policy declares unresolved-empty item contents. | Dedicated New corpse preserves empty contents and durable currency. | `UNPROVEN_FEATURE` | No | No invented item pool or probabilities. |
| Below-100-percent completion token probability | Shared accepted token policy explicitly represents unresolved probability; full100/faction level graph and neutral-none are defined. | New freezes the same completion-time disposition and exact supported grant once. | `UNPROVEN_FEATURE` | No | Preserve accepted unresolved state; do not reinterpret it as randomized no-token proof. |
| Restart-specific door/chest open rendering | Prior report and accepted Legacy operational projection use the original spawn packet plus use acknowledgement, despite persisted open state. | Same durable state/use projection; no separately proven altered restart spawn bytes. | `UNPROVEN_FEATURE` | No | No guessed wire encoding. |
| Generated floors/navigation | Accepted spatial authority supplies a captured envelope, not floors/nav geometry. | New's existing null-world motor holds altitude; the mission-only accepted envelope guard remains. | `UNPROVEN_FEATURE` | No | Do not fabricate geometry or classify absent Legacy geometry as a new regression. |
| Authored actor/world activation | Existing mission report identifies missing trusted actor bridge. | Durable authored item/state/stat/account/ledger capability exists. | Cross-domain `MASTER_BLOCKER` where supported Legacy route exists | Yes, coordinated | Dialogue domain owns exact actor activation. This slice does not edit `AuthoredQuest*` or bypass the existing transaction model. |
| Repeating a completed DOJA daily cycle | Dialogue-domain audit: shared `PersistentMissionService` refuses Completed reactivation; Legacy `DojaChipQuestRuntime` nevertheless emits accepted QFU on AlreadyApplied, then requires Active for turn-in. | New preserves the same terminal state and additionally refuses the false accepted QFU. | `LEGACY_BUG` | No | Preserve terminal/reward history. A future repeat-cycle identity needs a deliberate design; do not delete/reset rows or bypass ledgers for apparent parity. |

### Final review accounting

The counting unit is a documented gap/review unit, not a mission definition,
dialogue identity, NPC, handler or test. The nine baseline matrix rows plus the
separately proven repeated-corpse-use regression below make **10 review units**:

| Final disposition | Review units | Meaning |
| --- | ---: | --- |
| Direct mission regressions repaired | 2 | Exact top-level key/cleanup ownership; generated corpse close/reopen/ack order. |
| Cross-domain supported-regression family still partly open | 1 | Authored actor/world/quest routing: selected exact chains are connected, but the complete accepted authored domain is not claimed closed. |
| Nonblocking documented limits | 7 | Two existing Legacy bugs, four unproven features, and one occupied-sidecar import prerequisite/future capability. |

There are **zero remaining direct blockers in the two bounded mission repairs**.
The one cross-domain review family must not be reported as one missing quest or
silently marked complete because several dialogue/quest routes now work. Its exact
per-route evidence and remaining accepted transitions belong to
`ZONEENGINE_NEW_DIALOGUE_GAP_CLOSURE.md`; their counts are separate, nonadditive
units. General NPC activation and speculative retail completeness are not new
mission review units in this scoped report.

The final coordinated focused checkpoint is **420/420 tests PASS**, including the
mission/corpse, authored, shop and metadata corrections. This replaces the earlier
partial-suite checkpoint for current validation; the historical failures below
remain recorded as provenance. Fresh disposable persistence proof follows below.

### Required bounded reopenings

`EXISTING_AREA_REOPENED=generated mission artifact cleanup and key possession`

`WHY_REQUIRED=Legacy supports the exact owner's bank and other top-level pages; the checkpoint admits only main inventory.`

`BLOCKER_THAT_PROVED_IT=Legacy PlayerInventory Pages and MissionKeyGrantService exact lookup/removal versus New TryEnter/CleanupArtifacts; the disposable fixture incorrectly labels an owned bank row as a foreign page.`

The repair maps the existing six exposed character pages, verifies complete loaded
item shape under `PersistenceGate`, and uses exact owner/row authority for an unopened
bank without manufacturing partial hydration. Only cleanup's existing-row allowlist
expands; objective consumption remains main-slot-only. Historical retirement, earned
reward exclusion and checkpoint atomicity are unchanged. Focused tests cover all
six pages, unopened bank, foreign owner, unverified container, stale/locked row,
known rollback and unknown-commit quarantine. The disposable fixture now uses an
actual foreign owner/container as its negative and adds owned-bank cleanup plus
late checkpoint failure rollback and restart idempotence.

`EXISTING_AREA_REOPENED=generated corpse repeated-use UI interaction`

`WHY_REQUIRED=The completed durable currency model does not implement the accepted close/reopen interaction.`

`BLOCKER_THAT_PROVED_IT=Compiled Legacy PlayfieldCorpseAccessRuntimeService.TryUseCorpse:82 sends close plus use-finished, while New GeneratedMissionCorpseDynel.Open:43 silently coalesces repeated requests.`

`PlayfieldCorpseAccessRuntimeService.TryUseCorpse:82` implements captured repeated-use
close (CharacterAction 0x66, CharacterAction 110 and use acknowledgement, without
another InventoryUpdate). Checkpoint `GeneratedMissionCorpseDynel.Open:43` instead
coalesces every repeated request while a delayed award is pending. This is a
supported UI regression within the documented corpse interaction boundary. No
corpse economy or timers are replaced. The coordinating task approved a bounded
wire/order repair preserving the existing claim, lease and exactly-once authority.

The repair reproduces ActionMessage 0x66 then CharacterAction 110 without another
InventoryUpdate on close; reopen sends the rotated container handle. Each accepted
request receives its own 550ms acknowledgement, while the single claim remains at
500ms and original corpse removal at 550ms. Weak player-owned pending UI work is
polled on the existing dispatcher before its slower lifecycle throttle, allowing a
close acknowledgement after corpse removal without extending world visibility.
Original session/world/live owner/range/expiry and known claim success fence every
delayed acknowledgement. A closed-but-not-yet-unbound session also cannot claim.
Root's corpse-only reply hook sets the accepted Temp4=1; ordinary replies and the
original request's target array remain unchanged.

## Validation

Classification is source-backed. Coordinated focused checkpoint of 349 tests:
all mission/bank/corpse tests PASS; seven unrelated nano tests failed, so this is
not recorded as a whole-suite PASS. The later fifth combined 386-test checkpoint
again passed all mission/bank/corpse cases; its unrelated failures still prevent a
whole-suite PASS. The fresh current frozen-runtime Release build and disposable
schema/DAO/runtime gate now PASS, including the actual bank fixture. Final
combined/current-SHA Windows/Linux validation remains with root;
the already accepted checkpoint's previous PASS is not a PASS for future edits.
No production database, runtime or client was contacted during this slice.

### Fresh frozen-runtime disposable proof

The gate used the frozen working tree based on
`05c8d4ef7429e1ed765a19bd63ace0e21a2b29e5`, including uncommitted gap repairs;
this is not a claim those repairs existed in that base commit. The tested Release
`ZoneEngine_New.dll` SHA256 was
`cec83e657b7d5999d8df7c25e0943d1076ed3be3cf0bb2aebc645fe7dd1f6693`.

Logs: `build-verify/zoneengine-gap-closure-release.log` and
`build-verify/zoneengine-gap-closure-disposable.log`. Build exit 0, zero errors;
disposable wrapper exit 0. No runtime or fixture fix was required during this run.

- `MISSION_BANK_CLEANUP_ATOMIC=PASS`
- `MISSION_BANK_CLEANUP_LATE_FAILURE_ROLLBACK=PASS`
- `MISSION_BANK_CLEANUP_RESTART_IDEMPOTENCE=PASS`
- `MISSION_CLEANUP_FOREIGN_PAGE_PENDING=PASS`
- `MISSION_CLEANUP_REWARD_PRESERVED=PASS`
- `SCHEMA_NEGATIVE_TEST=PASS`, `SCHEMA_NEGATIVE_NORMAL_STARTUP=PASS`,
  `EXPECTED_DATABASE_MISMATCH_REFUSED=PASS`, `SCHEMA_CURRENT_TEST=PASS`,
  `EXPLICIT_MIGRATION_TEST=PASS`, `MIGRATION_RERUN=PASS`,
  `SCHEMA_INCOMPATIBLE_TEST=PASS`, `INCOMPATIBLE_MIGRATION_REFUSED=PASS`,
  `COPY_FAILURE_ROLLBACK=PASS`, `LEGACY_SOURCE_PRESERVED=PASS`,
  `MIGRATION_RETRY_AFTER_REPAIR=PASS`.
- Existing generated mission position/object state/frozen offers/acceptance/exact
  destination/objective/reward/expiry/cleanup/return/corpse/token/max-level proofs
  all PASS. Character snapshot, two-party trade, inventory split/stat/nano/bag
  rollback, active-nano multi-owner transactions, and authored mission
  item/state/stat/ledger/owner-account/child-row proofs all PASS.
- `RUNTIME_START_STOP=PASS`, `RUNTIME_RESTART=PASS`,
  `DATABASE_RUNTIME_FINGERPRINT_UNCHANGED=PASS`; negative/current/incompatible/
  target-mismatch database fingerprints also remained unchanged.
- `DISPOSABLE_CONTAINER_RESIDUE=NONE`, `DISPOSABLE_NETWORK_RESIDUE=NONE`,
  `DISPOSABLE_CLEANUP=PASS`, `PRODUCTION_CONTACT=NO`.

These are fresh disposable persistence/lifecycle proofs, not live client gameplay
or exhaustive NPC parity. The local build lock was returned after cleanup.
