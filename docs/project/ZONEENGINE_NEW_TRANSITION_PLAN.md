# ZoneEngine_New schema and release transition plan

Status: BLOCKED, not an executable production approval. The candidate is based on
`307e87670f9d26b50b1ed26e019684726600c52c`; there is no approved application SHA,
matching Windows/Linux acceptance pair, or approved production artifact yet.
Do not replace those missing identities with a branch tip. No command in this
plan was executed against production.

## Schema ownership and compatibility matrix

Runtime `AORebirth.Database.Schema` exposes only SELECT-based readiness. NewEngine
does not reference the operator assembly. The old New startup migration/bootstrap
classes are removed; shared Legacy `Misc.CheckDatabase` also no longer executes
schema SQL or prompts to create missing tables.

| Contract | Runtime requirement | Operator action |
| --- | --- | --- |
| `characters` | Id/Playfield int; Name/FirstName/LastName varchar; position/heading float; Online smallint | Existing governed baseline; no bootstrap by this tool |
| `stats` | Type/Instance/StatId/StatValue int; unique Type,Instance,StatId | Existing baseline; no schema inference |
| `charactersuploadednanos` | CharacterId/NanoId int | Existing baseline |
| `itemnames` | Id int, Name varchar | Existing baseline; destructive `itemnames.sql` is never executed by the tool |
| `item_instances` | Signed integer identity/location/template/quality/stack; unsigned tinyint Source; unique InstanceId and full location | Ordered Delmus migrations below |
| `item_instance_id_sequence` | Id tinyint, NextInstanceId int; unique Id, row 1 above all stored IDs with lease capacity | Ordered Delmus sequence migration |
| `schema_migrations` | MigrationName varchar unique, AppliedAtUtc datetime | Operator-only ledger; completed steps only |

All transactional runtime tables must be InnoDB. The source-of-truth checks are
`AORebirth.Database.Schema/SchemaContract.cs` and `DatabaseSchemaReadiness.cs`.
Unknown/out-of-order migration identities, incompatible types/keys/engines, or a
missing/behind/exhausted sequence fail closed. Account token migrations are known
unrelated identities, not executed by this tool.

Readiness states: `SCHEMA_CURRENT`, `SCHEMA_MIGRATION_REQUIRED`,
`SCHEMA_INCOMPATIBLE`, `DATABASE_UNREACHABLE`. Errors never echo connection secrets.

## Exact migration order and operator commands

1. `20260904_item_instances_from_legacy.sql`: additive item table and copy from
   legacy `items` and `instanceditems`. Both copies plus their ledger row share one
   transaction. An untracked nonempty destination is refused, never silently skipped.
2. `20260905_item_instance_id_sequence.sql`: additive persistent ID lease sequence.
3. `20260906_item_instances_source.sql`: additive Source provenance field.

The tool embeds the governed scripts and prints normalized SHA-256 identities in
`plan`. SQL bootstrap counterparts are package/reference assets, not a second
migration authority. No unrelated baseline creation, DROP, implicit prune, or
production connection discovery occurs.

Use a separately supplied `AO_REBIRTH_MIGRATION_CONNECTION` secret. Runtime uses
`AO_REBIRTH_MYSQL_CONNECTION`; do not grant runtime accounts DDL authority.
Do not put either connection value in commands, reports, logs, or Git.

```cmd
dotnet run --project Tools\DatabaseMigrationTool\DatabaseMigrationTool.csproj --configuration Release -- status
dotnet run --project Tools\DatabaseMigrationTool\DatabaseMigrationTool.csproj --configuration Release -- validate
dotnet run --project Tools\DatabaseMigrationTool\DatabaseMigrationTool.csproj --configuration Release -- plan
```

The modifying command requires the exact, separately reviewed database name:
`migrate --expected-database NAME --acknowledge-backup --acknowledge-engines-stopped`.
`NAME` is intentionally not populated: live schema/database identity was not
inspected or approved by this task. The tool refuses a mismatch and holds a
database-scoped migration lock. Run from the exact accepted tool artifact, never
an unreviewed checkout. On Linux use the same .NET assembly and arguments.

## Production prerequisites and downtime

Before a separate production task, record actual read-only schema status,
application/tool SHA, script plan hashes, current service/release/unit hashes,
counts and complete item identity/location/stack/source snapshots. Verify a
recoverable database backup and restoration access. Stop admission and all writers
before backup/migration: LoginEngine first, then drain and stop ZoneEngine; also
stop other services that can write affected character/item data. Do not assume
an empty Online flag alone proves there are no writers.

Downtime must cover drain, backup verification, migration, complete data/schema
validation, service startup and separately approved gameplay checks. A duration
cannot be asserted without rehearsing the actual data volume. After migration,
validate exact ledger order/hashes, source-to-target row identities and locations,
stack preservation, uniqueness, sequence safety, and `SCHEMA_CURRENT`.

MySQL DDL can commit independently. Failure can leave additive empty tables even
when copy data/ledger rolls back. Inspect status and retained data before retry;
never invent ledger rows to bypass an incomplete import. No automatic down migration.

## Release and rollback gates

Required before selecting an approved SHA: complete Windows acceptance, then
Linux acceptance of that exact unchanged SHA and matching placement manifest;
versioned complete world package; resolved external gameplay routes; positive and
negative disposable runtime tests on both platforms. Record the native apphost,
release manifest and unit hashes. These identities are currently unavailable.

Normal Windows wrappers select New; explicit rollback uses `-LegacyZoneEngine`.
Linux normal publisher produces New; third argument `legacy` creates the separate
legacy package/unit. See `LinuxBuild/deployment/ZONEENGINE_NEW_BACKEND.md` for exact
package paths and the transactional release workflow. Do not install those units
or invoke a production updater as part of this task.

After approval and schema-current validation, start ChatEngine, LoginEngine, then
NewEngine. Require exact listener ownership, private ISCom relationship, systemd
readiness, stable restarts and clean shutdown/restart. Mike performs separately
authorized client login, zoning, inventory/reconnect, trade and affected gameplay
acceptance. A ready port is not gameplay acceptance.

Rollback triggers include any schema, identity, ownership, readiness, persistence
or gameplay failure. Stop admission/writers and preserve evidence. Restore the
captured prior application pair and units only against a proven compatible DB.
Once NewEngine changes item state, legacy tables are stale: selecting Legacy is
not a safe data rollback. A reviewed restoration/reconciliation may be required,
with explicit authorization for any data loss. Never auto-downgrade or drop tables.
