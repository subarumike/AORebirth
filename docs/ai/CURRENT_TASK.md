# Current Task

## Active

Reconcile missions, teams, nano casting, supported inventory actions and accepted
NPC/combat mappings on `codex/zoneengine-new-gameplay-reconciliation`, beginning
at validated checkpoint `6dab287bd52590d7b9c0e7054bef95160a1594ba`.
Use the existing isolated integration worktree; preserve the primary master
worktree and the prior integration branch. Starting origin/master and merge base:
`6e90dda030774726aa2060acb9edb756ea1f635c`.

Preserve the proven schema/startup/persistence/packaging boundaries. Classify
actual legacy/New/test/evidence discrepancies before implementing only supported
contracts. No guessed behavior, fallback identities, unapproved schema changes,
or runtime capture dependency. Each domain needs deterministic tests and explicit remaining
gaps. Master may switch only when genuine supported-runtime regressions are
resolved and Windows then same-SHA Linux acceptance passes.

No production database/migration/deployment/service/network change or live client
use is authorized. The existing integration and transition evidence remains at
`docs/evidence/ZONEENGINE_NEW_FULL_INTEGRATION_20260908.md` and
`docs/project/ZONEENGINE_NEW_TRANSITION_PLAN.md`.

### Approved additive mission schema scope

The current SQL mission contract stores authored mission lifecycle/objectives/
rewards, but generated terminal offers and accepted ACG/destination bindings are
still file-backed. SQL-only preservation of that accepted behavior requires an
additive migration for frozen offers/batches, exact accepted quest/key/ACG/PF2
bindings, and objective/expiry/completion checkpoints. Mike explicitly approved
these additive mission schema/DAO changes with "yes make the changes" after the
scope was stated. Test migration execution only against disposable databases;
production remains separately prohibited. Preserve existing rows and explicit
operator-only migration ownership; no JSON/opaque-flag parallel mission store.

Team packet/chat/raid and exact-session ownership lifecycle routes are wired.
Inventory mutation, crystal upload, supported item actions and bag-retirement
transactions are connected. Real packaged nano catalog decoding, active effect
persistence, map/aura and supported vehicle morph paths are implemented.

Approved normalized mission tables/DAO own SQL offer identities, frozen offers,
fees, acceptance/key/object state, objective observations and completion. The
five selectable accepted ACG bundles now have concrete New NPC/world consumers,
reconnect restoration, exact frozen exits, lifecycle cleanup and mission-only
combat. Durable corpse currency, accepted raw corpse projection, progression
and token reconciliation are in the current validation batch.

Authored Stan/package and DOJA transaction services are present and their focused
tests pass, but trusted New NPC dialogue/trade activation remains missing. Generic
native NPC activation still lacks the accepted profile bridge; the implemented
mission NPC adapter is not general NPC/combat parity. Remaining supported nano
specialties are recorded explicitly in the nano matrix.

The final focused checkpoint passed 308/308 tests and startup validation.
This includes real-catalog eligibility, exact corpse wire/visibility, frozen token
progress, authored/DOJA transactions, and prospective equipment/nano contribution
planning for level-up refills. The fresh approved disposable run passed schema,
migration/rerun, mission/corpse/token/authored/inventory/nano rollback and restart
cases, runtime start/stop/restart and cleanup with no owned Docker residue or
production contact. DAO architecture guard PASS with no new violations. The final
completed-DOJA journal guard passes its regression; the full Windows Debug build
also passes. Source-level mandatory integration passes all 12 stages. Only final
EOF normalization and accepted provenance refresh follow that source-level gate;
post-commit exact-SHA acceptance covers the final committed bytes separately.

Domain records: `docs/evidence/ZONEENGINE_NEW_TEAMS_RECONCILIATION.md`,
`docs/evidence/ZONEENGINE_NEW_INVENTORY_RECONCILIATION.md`, and
`docs/evidence/ZONEENGINE_NEW_NPC_COMBAT_RECONCILIATION.md`,
`docs/evidence/ZONEENGINE_NEW_MISSIONS_RECONCILIATION.md`, and
`docs/evidence/ZONEENGINE_NEW_NANOS_RECONCILIATION.md`.
Post-commit Windows/Linux attestation is recorded in the acceptance artifacts for
the actual committed SHA. This checkpoint does not claim master-switch readiness.

Historical completed work remains in its existing evidence records and Git history;
this file tracks only the active ZoneEngine_New reconciliation.
