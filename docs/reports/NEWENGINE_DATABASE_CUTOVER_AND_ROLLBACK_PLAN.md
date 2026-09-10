# Database cutover and rollback plan

No production database was accessed or modified. No schema file was changed.
This is a proposal for a later acknowledged administrative cutover.

## Required schema

`AORebirth/Libraries/Source/AORebirth.Database.Schema/SchemaContract.cs` is the
authority. It currently declares these four migrations:

1. 20260904_item_instances_from_legacy.sql
2. 20260905_item_instance_id_sequence.sql
3. 20260906_item_instances_source.sql
4. 20260908_generated_mission_state.sql

MIGRATION_REQUIRED=CONDITIONAL_ON_CURRENT_SCHEMA; actual production state was
not inspected. The current contract checks required columns, unique indexes,
migration records, sequence floor, and transactional table engines. It includes
baseline-owned authored mission/active-nano tables; startup does not create them.
MIGRATION_TOOL=AORebirth.Database.Migrations.MigrationCommand and governed SQL assets
(existing status/plan/validate/migrate administrative CLI).
SCHEMA_VERSION_OR_CHECK=SchemaContract + explicit --validate-database.
FAIL_CLOSED_SCHEMA_CHECK=implemented; fresh disposable execution blocked here.

Use the existing documented migration tool status/plan first against a
disposable copy, then its acknowledged administrative apply path. Do not restore
startup auto-migration. Service startup validates required schema and stops on
incompatibility/unreachability; it must not fall back to Legacy.

## Disposable rehearsal before production approval

Use the existing `Tools/run_zoneengine_schema_validation.cmd --run-disposable
--engine <absolute-built-ZoneEngine_New.dll>` workflow. Its database is labelled,
loopback-only and owned by the fixture; application connection strings are not
accepted as targets. Rehearse a production-like baseline, negative missing/
inconsistent schema startup, explicit migrations and migration failure cases,
then transactional mutation and clean restart. Preserve the exact source,
package and migration hashes in the receipt.

The added exact-state fixture checks that NewEngine writes to item_instances do
not synchronize the historical items/instanceditems representation. Existing
fixtures exercise identity migration, sequence bounds, source metadata,
character/trade/inventory/nano/mission transaction failure and schema mismatch.
The full account-to-reconnect lifecycle is separately exercised by the connected
fixture, including a distinct-process restart and exact state comparisons.

Current disposable execution: PASS for migration, transactional failure rollback,
durable repository reload and process restart. The earlier Docker failure is
historical. Connected positive lifecycle also passes, but direct unauthenticated
zone admission fails its negative gate; production cutover remains blocked.

## Rollback decision

| Flag | Current result |
| --- | --- |
| PRE_NEWENGINE_ROLLBACK_SAFE | UNKNOWN; exact previous release+restored snapshot requires rehearsal |
| POST_NEWENGINE_WRITE_ROLLBACK_SAFE | NO for executable-only return; stale Legacy item tables proven in disposable execution |
| ROLLBACK_REQUIRES_DATABASE_RESTORE | YES for the proposed return-to-Legacy recovery path |
| ROLLBACK_REQUIRES_FORWARD_FIX | NO if accepting snapshot restoration; otherwise a validated forward fix/reconciliation is required to retain new writes |

Legacy and NewEngine use different durable inventory representations. NewEngine
does not update Legacy item tables in the inspected transaction paths. Do not
infer rollback safety from a successful build or from leaving Legacy binaries
available. The fixture emitted POST_NEWENGINE_WRITE_LEGACY_ROLLBACK_SAFE=NO
after its exact-state and stale-table assertions passed. This proves divergence,
not successful restoration of a production backup. The recovery plan requires a
validated snapshot restoration or a forward fix/reconciliation preserving writes.

## Concrete later production procedure

1. Accept the exact candidate only after connected durable lifecycle,
   authenticated zone admission isolation and disposable schema/transaction tests
   pass. Record the exact prior release and
   both packages' hashes.
2. Schedule downtime, stop all writers and capture a restorable consistent
   database snapshot plus configuration. Prove restoration on a disposable
   database before treating it as a recovery artifact.
3. Run explicit status/plan and acknowledged migrations while writers remain
   stopped. Validate schema and NewEngine readiness, then the agreed integrity
   acceptance. Keep writes stopped if checks fail.
4. Enable the accepted NewEngine release and watch the declared integrity checks.
   Ordinary unsupported gameplay may remain unavailable, with rejection before
   durable mutation.
5. If recovery is necessary, stop writes first. Either forward-fix and validate
   the NewEngine state, or restore the pre-cutover snapshot and exact previous
   release, validate restored state, and then resume writes.

Snapshot restoration discards writes after the snapshot. Retaining those writes
requires a separate validated reconciliation/forward-fix plan; no reverse data
conversion or destructive SQL is authorized by this document. Never just switch
the executable back after NewEngine has written inventory.
