# ZoneEngine_New mission reconciliation

## Scope and authority

Child branch: `codex/zoneengine-new-gameplay-reconciliation`, starting at
`6dab287bd52590d7b9c0e7054bef95160a1594ba`. No production database, live client,
capture launch, public listener, or master mutation is part of this mission slice.
The coordinating task owns the authorized final commit/push and acceptance workflow;
this domain agent does not make independent commits.
Mike explicitly approved additive mission SQL/schema and DAO extensions on
2026-09-08 after the structured generated-mission persistence gap was reported.

## Baseline discrepancy and current reconciliation matrix

| Accepted Legacy responsibility | Starting-checkpoint discrepancy | Current source / remaining acceptance limit |
| --- | --- | --- |
| Five-offer terminal roll, accepted slider/type/location/reward distributions | `MissionTerminal` had no request handler | Root links the same accepted pure generator, substitutes SQL identity allocation, and adds an owned/range-checked roll handler. No new probability or item eligibility inferred. |
| Durable offer freeze and one roll fee | `MissionOfferStore` uses atomic text sidecars; authored `IMissionDao` cannot represent generated offers | Approved normalized batch/offer rows and existing reward ledger in one transaction; derived frozen wire body/hash is secondary to typed destination/reward fields. |
| Accept owner/original offer/accepted quest/team/key/bundle/live PF/objective | No New acceptance connection or SQL representation | CreateQuest handler, exact compiled-bundle planner and typed DAO atomically insert key/component rows, full binding and all frozen object states. Actual five-bundle materialization/factory tests pass. |
| Exact exterior destination through reload and dungeon entry | PF integer alone loses full identity and entrance building pair | Full typed destination/entrance/building/bundle/hash/live PF survives SQL reload. Generic use claims the exact owned exterior marker (10 horizontal/14 vertical), requires the physical key and unique binding, and enters only the dedicated MissionPlayfield; no generic fallback. |
| Objective observations and exactly-once completion | No New generated mission objective/reward connection | Concrete pre-publication NPC death, exact InfoRequest, pickup, repair and issuing-terminal return hooks share owned object CAS/observation transactions. Completion combines items, online cash, full direct XP/SK progression, token claim and existing ledger before publication. |
| Return-item / repair consumption | Receipt of a packet is not authoritative possession/consumption | Exact accepted physical item/main-slot plan is retired in the same transaction as object consumption and progress. Pickup grants its exact frozen item and removes the world object only after commit. Nested/unverified page ownership remains fail-closed. |
| Full inventory, SQL failure, ambiguous commit | Independent item grant would split mission/reward authority | Pure `InventoryGrantPlan` under `PersistenceGate`; no grant before known commit. Full inventory rejects, late SQL failure rolls back, unknown outcome quarantines and never retries from memory. |
| Journal reconnect, zoning, restart | Existing authored `PersistentMissionService.Reload` explicitly does not claim journal reconciliation | Generated login restore and accepted QFU replay are wired, exact expiry rebased to the actual GameTime send anchor. All five worlds restore durable NPC HP/position/death and consumed objectives. Terminal/expired login resolves the frozen exterior. This does not imply authored actor/dialogue parity. |
| Expiry / abandon / cleanup and PF release | New lacks the accepted ordered cleanup machinery | Owning-dispatcher lifecycle and Quest Delete routes checkpoint evacuation/world removal before exact artifact retirement/client removal. Completed owners retain movement/exit and combat until cleanup/lifetime ends. PF reservation releases only at all 17 checkpoints. Unverified bag/bank items retain a pending lease. |
| Doors and chests | No durable generated object consumer | Owned 8-unit use, locked-door refusal, durable toggle/open state and original accepted spawn packets are connected. Legacy also sends original packets plus use acknowledgement: restart-specific open/closed visual encoding remains unproven, not falsely claimed rendered parity. |
| Generated corpses | Generic New corpse uses different identity/loot/cash/body/timers | Dedicated exact same-instance corpse uses accepted raw CFU, owner-only durable 21–87 currency, explicit unresolved-empty item contents, 600ms spawn/10s dead-NPC removal/60s corpse lifetime, 500ms award/550ms acknowledgement. SQL claim, session/world fencing and restart suppression prevent duplicate cash. |
| Generated tokens | No durable completion-time policy | Exact ambient death population freezes percent/level/side/disposition/count with objective verification, before capacity-dependent reward retries. Existing official level graph and faction item pairs are reused. Full100 eligible, neutral explicit-none, below100 explicitly unresolved without a guessed probability. Token item/ledger/completion share one transaction. |
| Existing authored quests | Pure DAO foundation was not included in New | Separate authored adapter and atomic inventory/stat/account-cooldown capability are connected; existing authored SQL dependencies are required read-only baseline schema. Trusted actor/world activation remains an explicit cross-domain gap, not inferred from a name. |
| Occupied Legacy generated state on switching engines | Existing accepted missions remain in sidecars | Migration does not erase/import/ignore sidecars. A reviewed typed import and occupied-state validation is required before any real switch. No production migration attempted. |

## WHY_EXISTING_RECONCILIATION_WAS_INSUFFICIENT

The frozen integration schema covered item instances only. Existing authored
`IMissionDao` tables have no structured random-offer payload, full destination,
key/ACG/live-PF binding or generated objective representation. Serializing a new
JSON ledger into a generic flag table would create a parallel opaque store and
would not satisfy the required persistence boundary. The explicitly approved
additive migration therefore introduces seven typed tables (including exact
per-world-object identity, position, frozen difficulty, HP and lifecycle), reuses
`missionrewardledger` and `item_instances`, and adds an optional generated
capability on the same injected `MySqlMissionDao` connection factory.

The SQL migration is operator-only, additive and idempotent. It does not reset
identity counters, erase historical rows, bootstrap unrelated tables, or execute
at engine startup. Read-only readiness now requires its exact columns,
transactional engines, migration identity and uniqueness constraints.

Online cash/XP inputs come from base stats while holding `Player.PersistenceGate`,
the same aggregate boundary used by trade/snapshot/inventory. This preserves
legitimate unsnapshotted gains; the logout snapshot (which marks Online=0) is not
called during gameplay. Completion never rerolls the frozen reward.

## Files inspected

- Legacy `Core/Missions`: `MissionOfferStore`, `MissionAcgBindingStore`,
  `MissionAcgAcceptedProjectionStore`, `MissionAcgAcceptanceCoordinator`,
  `MissionAcgAllocationService`, `MissionAcgObjectiveBinding`, `MissionStateDirectory`,
  `MissionRuntime`, `PersistentMissionService`, `MissionRewardCoordinator`,
  `MissionDefinitionCatalog`, `MissionRollService`, `MissionTypeCatalog`,
  `MissionLocationPool`, `MissionOfferIdentityStore`.
- Legacy request/accept/delete handlers; existing `IMissionDao`,
  `MySqlMissionDao`, and `SqlTables/missionrewardledger.sql`.
- Legacy generated operational/object/token policies, `NpcCorpseLifecycleRules`,
  `CombatCorpseRules`, `Playfield` corpse registration/access/currency consumer,
  `CorpseFullUpdate` and its accepted L7 Tilda projection, exact corpse interaction timing.
- New player persistence gate, inventory grant/flush/item APIs, trade base-stat
  publication, logout snapshot, terminal/request handler, roll projection.
- Existing schema readiness, explicit operator migration tool, disposable
  schema harness and focused schema/inventory test fixtures.
- `docs/evidence/ARPA3_CLICKSAVER_MISSION_RECONSTRUCTION_20260901.md`: its
  evidence-only constraints do not authorize new runtime roll probabilities.

## Files changed in the mission slice

- Shared `Persistence/Missions/IGeneratedMissionDao.cs` and
  `Domain/Missions/MySqlMissionDao.Generated.cs`; explicit old project includes.
- `SqlTables/generatedmission{sequences,batches,offers,bindings,observations,artifacts,objects}.sql`
  and `Migrations/20260908_generated_mission_state.sql`.
- Shared schema contract/readiness and operator migration resource/ownership list.
- New mission service/ACG/objective/lifecycle/corpse/token sources and deterministic tests;
  dedicated MissionPlayfield, Quest Delete/Create handlers and mission-only movement guard.
- Pure shared corpse credit/token reward policies with Legacy forwarding consumers.
- Disposable harness mission/inventory transaction tests and exact baseline extension.
- Root-owned generator seam, projection, request handler and shared project/DI wiring
  are described here for integration context; they are not independent persistence owners.

## Validation status

- Historical focused checkpoints: 230/230 first integrated ACG/NPC; 262/262 after
  movement/cleanup/direct-XP and all-five-bundle materialization. The later raw-CFU,
  corpse-session/lifetime and visibility checkpoint had all mission tests PASS
  within 298 PASS / 2 unrelated authored DOJA failures. The complete suite was not
  called PASS at that checkpoint. Final focused checkpoint: **PASS 307/307**,
  including token policy/freeze adapter, raw corpse/visibility, session/lifetime,
  five-bundle materialization, authored fixes and direct-XP tests.
- Actual disposable MySQL mission and inventory tests: PASS. Frozen offer/fee
  deduplication; conflicting typed destination rejection; acceptance item rollback;
  exact destination/key/ACG reload; owned exact objective deduplication; late XP
  failure rollback over items/cash/ledger/completion; exactly-once rewards; expiry
  and cleanup CAS; inventory split success/stale-count and late-nano rollback.
- A subsequent approved disposable run also passed exact object-state restart/CAS,
  pickup item+object rollback, return exact physical item/issuing terminal/replay,
  inventory late-stat and nonempty-container rollback, empty-container retirement,
  and active-nano legacy/current multi-owner replacement/late-stat rollback.
  Its sole initial failure was the test's CHECK on an auto-increment column
  (MySQL 3818); the corrected synthetic LowId CHECK proved the intended rollback.
- Existing negative/current/incompatible read-only schema checks, explicit migration,
  migration rerun, copy rollback, snapshot/trade atomicity, runtime start/stop/restart
  and database fingerprints: PASS. Exact owned Docker container/network residue:
  NONE; cleanup PASS. Log: ignored `Tools/ZoneEngineSchemaValidation/validation.latest.log`.
- **Final approved disposable Release wrapper: PASS, exit 0**, after the 307-test
  checkpoint. This used the dirty reconciliation working tree based on
  `6dab287bd52590d7b9c0e7054bef95160a1594ba`, not a claimed future final commit.
  It freshly rebuilt the runtime and exercised all previous gates plus:
  `MISSION_CORPSE_CREDITS_ATOMIC`, `MISSION_CORPSE_LATE_FAILURE_ROLLBACK`,
  `MISSION_CORPSE_RESTART_EXACTLY_ONCE`, `MISSION_TOKEN_OBJECTIVE_TIME_FREEZE`,
  `MISSION_TOKEN_ATOMIC_GRANT_ROLLBACK`, `MISSION_TOKEN_RESTART_EXACTLY_ONCE`,
  `COMPLETED_MISSION_NPC_LIFETIME_GUARD`, `MAX_LEVEL_MISSION_XP_NOOP`,
  actual character XYZ save/rollback without Online/stat clobber, ordered artifact
  cleanup and earned-reward preservation. All are PASS.
- The same fresh disposable database passed authored item/state/stat/account
  transaction success, late failure rollback, reward replay, owner/account fences,
  stale item CAS and nonempty-child retirement rollback; active-nano and inventory
  late-failure proofs also PASS. Runtime start/stop/restart left its database
  fingerprint unchanged. `DISPOSABLE_CONTAINER_RESIDUE=NONE`,
  `DISPOSABLE_NETWORK_RESIDUE=NONE`, `DISPOSABLE_CLEANUP=PASS`,
  `PRODUCTION_CONTACT=NO`. A subsequent authored-only accepted-QFU lifecycle guard
  is outside this disposable checkpoint; it changes no schema/DAO transaction.
  Coordinating task owns full Windows/final-source validation.
- Synthetic DAO payloads used by transactional tests are not game assets,
  protocol evidence, accepted generation content, or proof of dungeon gameplay.
- Live gameplay parity, occupied-state migration and master eligibility are not
  implied by deterministic/disposable PASS; the remaining limits below remain explicit.

### Connected runtime and exact remaining validation boundary

The current source additionally includes `GeneratedMissionAcgService`, its objective
adapter, immutable `GeneratedMissionWorld`, dedicated `MissionPlayfield`, and the
CreateQuest handler. These reuse the five complete accepted generated bundles;
selectable source data, identity mapping, generator payload and fresh 48-hour
acceptance lifetime are preserved. Exact accepted item packet order is emitted
only after the binding/items transaction. Repair components follow the existing
six-template selection policy and existing fallback endpoints, frozen at accept.

Pickup and return/repair consume plans now share the mission transaction with
durable object CAS and objective observations. Kill verification requires a
durable zero-HP/dead target witness in the same transaction; info requests require
the exact live target. Root's NPC callback must run before damage publication.
Pickup/return/object rollback, restart, complete five-bundle materialization,
token freeze/reward, existing authored-table readiness and the combined disposable
suite are PASS at the recorded checkpoints above.

The current source adds a full direct-XP/SK projection from the existing Legacy
tables, committed with reward items/cash/ledger, followed by publication. Ordered
cleanup records actual evacuation/world removal before exact main-inventory
artifact retirement and client removal. An artifact in an unverified bag/bank
retains a precise pending state; no foreign row or earned reward is deleted.
Position persistence uses the existing character XYZ/Playfield row without
overwriting Online or unrelated base stats.

The typed CFU decoder failed on the exact accepted L7 Tilda name/material body;
this was a lossy-decoder boundary, not permission to synthesize a different corpse.
The shared Legacy raw projector now feeds a narrow raw-spawn path, with exact
byte/projection tests over every supported bundle and visibility tests proving
ordinary typed spawns are unchanged and failed visibility publication can retry.
Corpse SQL freezes the death actor/time/currency/expiry. New death timing follows
the separate accepted 600ms corpse spawn, 10s dead-NPC visual lifetime and 60s
corpse lifetime. Delayed award/acknowledgement revalidate the original session,
owner, world, live state, range and expiry; stale transports cannot inherit them.

`MissionAcgSpatialAuthority` explicitly provides a captured coordinate envelope,
not floors or navigation. New's existing null-world motor already holds current
altitude; the mission-only movement guard uses the same accepted envelope and
does not fabricate collision geometry. Authoritative geometry-dependent actions
remain unresolved as in the accepted Legacy policy.

## Remaining risks

Full Windows/final-source and later occupied/live acceptance remain coordinating
task gates, not implied by the recorded disposable PASS. Unopened bag/bank artifact cleanup needs an exact owner-tree
hydration boundary; it currently preserves pending state and rows. Authored
mission actor/world bridges require trusted identity activation. Generated item
loot remains explicitly unresolved-empty as accepted Legacy; below100 token
probability and restart-specific door/chest visual encoding remain unproven.
Explicit reviewed import of pre-existing Legacy sidecars is still required before
any occupied-engine switch. No production schema or state migration was attempted.
