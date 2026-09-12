# bb5dd916 production cutover receipt

Date: 2026-09-12. Result: **DEPLOYED; automated operational acceptance PASS.**

## Authority and exact identities

Mike explicitly approved application build `bb5dd916` and only the four required
inventory/mission migrations, with verified backup restoration, preservation of
all existing production data, migration validation before NewEngine startup, and
database restoration before any Legacy rollback.

- Running application SOURCE_SHA: `bb5dd9165c6201b15ef1e0cfbfd788a3e205486a`.
- Deployment tooling repair: `7391bd30f3a34676a6257837180bb894b85d0bfc`, pushed to
  `origin/codex/bb5dd916-production-cutover`. Runtime binaries and migration SQL
  remain from the approved application SHA; their acceptance was not relabeled.
- Accepted artifact checks: all 7,315 files verified; release archive SHA256
  `4a7933b1b68d7981d304a7b70608a6a226093de00c8d87d6d8ad2db6fc4fae32`.
- Operator package, built from the clean accepted tool source: SHA256
  `bb355272139c9262518aa43029b5979da00f4736cd0c959f10a342e089e9dc10`.

| Approved migration | Normalized embedded SQL SHA256 |
| --- | --- |
| `20260904_item_instances_from_legacy.sql` | `5eb2c298948fac84804ae602133586e0941d2d09fe424e8355ee9629cf252891` |
| `20260905_item_instance_id_sequence.sql` | `786e758760a200af8674760b7cc24b03cf3918b1733156cc5bd055f2ecc7e149` |
| `20260906_item_instances_source.sql` | `2a57b9e3a51171af622479de61d8189f73b46608dc44ae9c35ca82f77111859b` |
| `20260908_generated_mission_state.sql` | `0688dc60cee5572c396b658c9b4bc21a9e871ed7eb2f1ee9e060f784584c9be8` |

The operator's production `plan` matched these identities before execution.

## Repair and verification

The original deployment preflight ran systemd verification against the current
Legacy release, where `ZoneEngine_New` cannot exist yet. It now verifies temporary
copies of the reviewed units against staged executables, installs the original
hash-checked units, and verifies their actual deployment paths after installation.

`--prepared-schema-cutover` supports the separately approved migration with both
services already stopped and both exact previous release paths pinned. It retains
frozen state, zero-online, configuration, artifact, schema and readiness gates.
Different prior Login/Legacy source SHAs are allowed without fabricating a previous
deployment marker. Failure restores the previous artifacts/units and leaves both
engines stopped for database restoration.

Windows Bash syntax validation, 64 Linux deployment regression cases, actual
systemd preflight, isolated migration and restore-after-migration rehearsal: PASS.

The first fresh-backup restore comparison stopped the cutover before any live
migration: restored MySQL DDL spelled out `CHARACTER SET latin1` where the original
six mission tables specified the already equivalent `latin1_general_ci` collation.
Every data fingerprint matched. The comparison now normalizes only this exact
redundant declaration; it retains every other DDL token and all row comparisons.
All 40 restored tables then matched. Legacy services were restarted during that
investigation, and a separate fresh backup was taken for the successful attempt.

## Database preservation

Database: `aorebirth_chatengine_stage6`. Login admission, ZoneEngine, ChatEngine
and the account broker were stopped before the final backup/migration. No enabled
database event writers were present. Zero online characters was verified at the
controlled boundaries.

- Final backup restored into a network-isolated MySQL instance: PASS.
- Complete row and schema fingerprints of all 40 original tables: preserved.
- Legacy `items`: 254 rows retained. Legacy `instanceditems`: 26 rows retained.
- Unified `item_instances`: 280 rows; zero field mismatches against the complete
  normalized source set. Instanced IDs, locations, templates, qualities and stack
  counts were checked, including the reviewed default Source value.
- Exactly four migration ledger entries; unique location constraints and sequence
  allocation above the imported maximum: PASS.
- Exactly ten approved new tables: unified inventory, its sequence, migration
  ledger, and seven generated-mission tables. No unrelated schema changes.
- Existing characters: 28. `SCHEMA_CURRENT` before startup and after startup: PASS.

## Live runtime

Both current symlinks select `release-bb5dd9165c6201b15ef1e0cfbfd788a3e205486a`
under their respective `/opt/ao-rebirth/{loginengine,zoneengine}/releases/` trees.
The actual `/proc/<pid>/exe` targets and apphost/DLL hashes matched accepted files.

| Service | PID at verification | Restarts | Listener |
| --- | --- | --- | --- |
| LoginEngine | 2424536 | 0 | public 7500 |
| ZoneEngine_New | 2424599 | 0 | public 7501 |
| Existing ChatEngine | 2422956 | 0 | public 7012; private 127.0.0.1:6996 |

ChatEngine's existing release was retained and restarted. Account broker is active.
The governed prepared dry run, transactional release and ten-second post-start
stability check passed. `ROLLBACK_REQUIRED=NO`. The isolated rehearsal container
was stopped after evidence retention. No AO client was launched or controlled.

## Retained backup and rollback

Final backup directory:
`/srv/aorebirth-cutover-bb5dd916-final-20260912-attempt2`

`database.sql.gz`: 1,864,500 bytes, mode 0600 under root-owned mode 0700 directory.
SHA256: `74062e1d3e615af6e9f7c7c685cae8d1282149ee34bddd7985a62d2d542334d6`.
It contains the original database, including routines, triggers and events. The
directory also retains configuration/unit backups, before/after fingerprints,
operator plan, migration validation, and deployment logs.

The retained restore operator is
`/srv/aorebirth-release-bb5dd916-20260912/restore-reviewed-backup.sh`.
It requires the exact approved backup, verifies its digest, requires all four
writers and game listeners stopped for production restoration, retains the failed
database state, restores only the named database, and compares all original tables
and row/schema fingerprints. Its restore-after-migration behavior was proven in
isolation. **No production database restore was needed.**

After NewEngine writes, executable-only rollback to Legacy is unsafe. A later
rollback must stop writers, preserve subsequent data, restore/reconcile the
database and validate it before Legacy restarts. This receipt does not authorize
discarding player changes made after this successful cutover.

## Evidence and files

Non-secret evidence archive SHA256:
`748235ad2cef92b694c514e63c024044699c39f93163f2b50282b58868c6790d`.
Local evidence and operator scripts: ignored `build-verify/live-bb5dd916/`.
Remote evidence archive:
`/srv/aorebirth-release-bb5dd916-20260912/final-evidence.tar.gz`.
Database dumps and configuration secrets were not downloaded or committed.

Inspected: startup/governance and deployment documents, accepted build receipt,
manifest creator, deployment script/tests and units, schema readiness/contract,
operator tool and the four SQL assets, actual live services, database metadata,
full preserved-table fingerprints and migration/release evidence.

Changed tracked files: production upgrader, its tests/README, CURRENT_TASK,
PROJECT_STATE and this receipt. No application code or migration SQL changed.
Tooling branch publication did not move remote master.

Remaining acceptance: Mike's official-client login, zoning, inventory/reconnect
and clean-restart gameplay checks on this exact live build. Automated operational
acceptance does not establish those client results.

Discord-ready: NewEngine is live on Linux. The DAO inventory/mission migrations
are complete, all 280 existing inventory rows are preserved, and server health
checks passed. Backups and database rollback were verified. Ready for live testing.
