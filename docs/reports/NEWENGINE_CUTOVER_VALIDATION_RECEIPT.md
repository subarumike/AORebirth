# NewEngine cutover validation receipt

## Secure handoff candidate

Starting at `b87faf8b6de31d22f79d8f469990c27592bab6f9`, the recovered-cookie
authority and connected negative/concurrent/restart tests pass in development.
Final exact-source execution is pending. The receipt below is preserved as
history of the admission failure; it is not the current implementation result.
See `NEWENGINE_ZONE_HANDOFF_SECURITY.md`.

## Historical connected acceptance receipt

SOURCE_SHA=eddd90e73fdf912f766c5218acd4c07ec2972980
STARTING_SHA=de764881cfae677bd0b2975499e8ad6cb5944c4a
BRANCH=codex/newengine-production-cutover-001
ORIGIN_MASTER_SHA=6e90dda030774726aa2060acb9edb756ea1f635c
DEVELOPER_REF_SHA=53c858d9900266fb6740975dbb2b5011a5792e66

Worktree: `C:\Users\Mike\Documents\AORebirth\tools-temp\cutover001`.
The source commit was pushed and all final execution below used that clean
committed source. The following receipt commit changes documentation only;
it must not be substituted for the actual tested source SHA above.

**Decision: operational cutover NO.** Positive authenticated connected lifecycle
passes. Direct unauthenticated zone admission also succeeds, which fails the
negative admission gate. No failed admission result is overridden by unit tests
or positive login evidence. Production, master and developer branch are untouched.

| Evidence category | Final result |
| --- | --- |
| STATIC/BUILD EVIDENCE | Windows Legacy + NewEngine build PASS; exact-SHA acceptance PASS; NewEngine 485/485, AOtomation 1129/1129; mandatory stages 12/12; source/default/SQL/backend guards PASS; inventory check PASS, 93 links / 99 method rows |
| REPOSITORY PERSISTENCE EVIDENCE | Fresh disposable migrations, negative schema/startup, injected transaction failures, exact inventory/stat/nano/mission persistence and CutoverDurableReloadSmoke PASS |
| PROCESS LIFECYCLE EVIDENCE | Real LoginEngine and NewEngine start; normal logout; clean zone shutdown and exit; distinct process restart; same Debug binary; owned process/container/network cleanup PASS |
| AUTHENTICATED WIRE EVIDENCE | Credentials/list/selection/world entry, acknowledged inventory move, fresh authenticated reconnect, fresh authenticated reconnect after restart, exact state and connected morph cancellation PASS; unauthenticated zone admission FAIL |

### Exact connected execution

```text
DATABASE_FIXTURE_ID=aorebirth-zone-schema-9d25823d58a04451b110fb1678b06994
ACCEPTANCE_TIMESTAMP=2026-09-10T01:27:45.6844450Z
SOURCE_WORKTREE_TRACKED_CLEAN=YES
ZONEENGINE_NEW_BINARY_SHA256=88d1d8686cce71d6c5a51416da8cf1e575e5f86cfcb84ede83127f706b6a50e2
LOGINENGINE_BINARY_SHA256=1be83a560282af4adb40c363e34d92c26379dcdc4a8e82f2b0867575a0378bb8
ENGINE_PID_BEFORE=16372
ENGINE_PID_AFTER=29964
ENGINE_RESTART_PROVEN=YES
CONNECTED_POSITIVE_LIFECYCLE=PASS
CONNECTED_WRONG_PASSWORD_FAIL_CLOSED=PASS
CONNECTED_INVALID_INVENTORY_SOURCE_FAIL_CLOSED=PASS
UNAUTHENTICATED_ZONE_CHARACTER_ACCESS=REPRODUCED
ZONE_HANDOFF_FAIL_CLOSED=FAIL
CONNECTED_MORPH_CANCEL=PASS
BASELINE_RESTORATION=PASS
MORPH_CANCEL_AUTHENTICATED_RECONNECT=PASS
DISPOSABLE_CLEANUP=PASS
```

The connected wrapper's shell invocation returned nonzero, as required by the
reproduced admission failure. No exception or setup failure occurred in the final
positive lifecycle. The fixture identifies its negative failure explicitly.

The exact Windows command was:
`cmd /d /c Tools\accept_windows_source.cmd --expected-sha eddd90e73fdf912f766c5218acd4c07ec2972980 --mandatory-gate`.
Its receipt is `build-verify/windows-acceptance-eddd90e7.env`.
Placement manifest SHA256:
`a5a0b2efc51508500f4542e8403389b95946408344ed31eab06739cbf3465108`.

Connected binaries were the accepted Windows build's
`AORebirth/Built/Debug/ZoneEngine_New/ZoneEngine_New.dll` and
`AORebirth/Built/Debug/LoginEngine.exe`. The independent full schema wrapper used
the same committed source's Release ZoneEngine_New.dll. Run commands are recorded
in WORKFLOW.md; both wrappers require explicit binary paths and own their DB.

Linux command: `cmd /d /c LinuxBuild\publish-zoneengine.cmd linux-x64 true`.
Self-contained publication, offline startup validation, default-engine parity,
source inventory, SQL parity and backend guards PASS. Package negatives 4/4 and
source-omission negatives 2/2 PASS. This is Linux-target publication on Windows;
fresh Linux-host service/runtime acceptance was NOT RUN. No deployment occurred.

### Persistence and rollback interpretation

Connected inventory movement and morph cancellation are proven mutations.
Nano activation and authored/generated mission progress are pre-start seeds,
verified over authenticated wire and read-only durable state after reconnect and
restart. Credits are preserved at 1234; no connected credit reward is claimed.
The exact state table and packet limitations are in NEWENGINE_CONNECTED_ACCEPTANCE.md.

The separate full disposable suite returned exit zero and reported:

```text
SCHEMA_NEGATIVE_TEST=PASS
SCHEMA_CURRENT_TEST=PASS
EXPLICIT_MIGRATION_TEST=PASS
COPY_FAILURE_ROLLBACK=PASS
CUTOVER_DAO_FRESH_RELOAD=PASS
CUTOVER_EXACT_ITEM_STATE=PASS
CUTOVER_PERSISTED_STATE_PROCESS_RESTART=PASS
POST_NEWENGINE_WRITE_LEGACY_ROLLBACK_SAFE=NO
RUNTIME_RESTART=PASS
PRODUCTION_CONTACT=NO
DISPOSABLE_CLEANUP=PASS
```

Legacy inventory tables remain stale after NewEngine writes. Executable-only
rollback is unsafe; a return to Legacy requires validated database restoration
or a separately validated reconciliation. This does not claim a production backup
has been restored. Snapshot restoration would discard later writes.

### Retained evidence hashes

Logs are ignored local artifacts in this worktree, not committed credentials.

| Artifact | SHA256 |
| --- | --- |
| build-verify/connected-final.log | 8b987f0223ecaf3ea13096ad9307c524622c15362c36d985fa74842d13a2496e |
| build-verify/connected-schema-final.log | 4aa43e1049b98d3d74d82b57f909e74028fc5fdf5a87fc9c9bb8bd38fb9c5637 |
| build-verify/connected-windows-acceptance.log | cac37fc9609aea509690aa6f1db6fefa2838d4eb7dd845c3202f8a224d6e6347 |
| build-verify/connected-linux-publish.log | 264c5fce99f88943dc4ee6c33796e5c915241ecf919f84f53862c1dbfdccdf76 |

### Files changed in this task

Six added, fifteen modified relative to starting de764881. Generated builds,
logs, package outputs and process logs stay ignored. The two inventory JSONs
are regenerated tracked artifacts; dependency/method counts are unchanged.

Added:

- Tools/ZoneEngineSchemaValidation/ConnectedAcceptanceSmoke.cs
- Tools/ZoneEngineSchemaValidation/ConnectedEngineProcess.cs
- Tools/ZoneEngineSchemaValidation/ConnectedMissionSeed.cs
- Tools/ZoneEngineSchemaValidation/ConnectedWireClient.cs
- Tools/run_newengine_connected_acceptance.cmd
- docs/reports/NEWENGINE_CONNECTED_ACCEPTANCE.md

Modified:

- AORebirth/Server/ZoneEngine_New.Tests/GeneratedMissionRollProjectionTests.cs
- AORebirth/Server/ZoneEngine_New/Core/Missions/GeneratedMissionAcgService.cs
- AORebirth/Server/ZoneEngine_New/Properties/IntegrationTestVisibility.cs
- Tools/ZoneEngineSchemaValidation/CutoverDurableReloadSmoke.cs
- Tools/ZoneEngineSchemaValidation/Program.cs
- Tools/ZoneEngineSchemaValidation/ZoneEngineSchemaValidation.csproj
- docs/ai/CURRENT_TASK.md
- docs/ai/WORKFLOW.md
- docs/project/PROJECT_STATE.md
- docs/reports/NEWENGINE_CUTOVER_HANDOFF.md
- docs/reports/NEWENGINE_CUTOVER_VALIDATION_RECEIPT.md
- docs/reports/NEWENGINE_DAO_GAP_INVENTORY.json
- docs/reports/NEWENGINE_DATABASE_CUTOVER_AND_ROLLBACK_PLAN.md
- docs/reports/NEWENGINE_LEGACY_DEPENDENCY_INVENTORY.json
- docs/reports/NEWENGINE_OPERATIONAL_ACCEPTANCE.md

Runtime changes are limited to canonical accepted-bundle hashes and test assembly
visibility. No Legacy removal, DAO consolidation, schema edit, master merge,
developer change, production database operation or production service operation.
The prior receipt below is historical; its Docker and instrumentation failures
no longer describe current execution.

## Historical foundation source and result

Date: 2026-09-09. Accepted source/tool/test commit:
`d1c6d01e976a841dd1cdb85c6f31a62aa4ddf767`.

Branch: `codex/newengine-production-cutover-001`.
Master baseline: `6e90dda030774726aa2060acb9edb756ea1f635c`.
Reconciled baseline: `4dac603b82dfe64206b155e7e6c0499a9f8ad7f8`.

The final receipt commit changes reports only. Runtime, tools and tests are those
at the exact accepted source above; do not relabel the receipt commit as the SHA
passed to the Windows acceptance command.

| Validation | Result |
| --- | --- |
| Normal Windows Legacy + NewEngine build | PASS |
| Exact-SHA Windows acceptance | PASS |
| NewEngine tests | PASS 484/484, 0 skipped |
| AOtomation messaging | PASS 1129/1129 |
| Mandatory integration stages | PASS 12/12, including clean worktree |
| NewEngine offline startup/package | PASS |
| Linux self-contained publication on Windows | PASS |
| Default engine/source/SQL/backend parity guards | PASS |
| Package negative fixtures | PASS 4/4 |
| Source omission negative fixtures | PASS 2/2 |
| Deterministic inventories after final build | PASS, 93 links / 99 method rows; Roslyn errors 0 |
| Fresh disposable schema/migration/transaction/restart | FAIL: ENVIRONMENTAL, docker-image-failed |
| Full authenticated login/mutation/logout/reconnect/restart | NOT PROVEN; connected fixture incomplete |
| Fresh Linux-host runtime acceptance | NOT RUN; cross-publication is not host acceptance |

The Windows command returned exit 0:
`cmd /d /c Tools\accept_windows_source.cmd --expected-sha d1c6d01e976a841dd1cdb85c6f31a62aa4ddf767 --mandatory-gate`.

Source receipt: `build-verify/windows-acceptance-d1c6d01e.env`.
The aggregate gate explicitly reported DEFAULT_ZONEENGINE=ZoneEngine_New and
ZONEENGINE_NEW_ACCEPTANCE=PASS. This means offline acceptance, not the stronger
connected operational gate specified by Mike.

## Retained local evidence

Logs are ignored/generated artifacts retained in this worktree. SHA256:

| Log | SHA256 |
| --- | --- |
| build-verify/cutover-windows-acceptance.log | 3ca52ee34aef087fafbf1a3286fed5e9d36ffe0e25d37424b1b6c2bf29019105 |
| build-verify/cutover-linux-publish.log | 37680ad46a21035fbfe248838da3909c1e91686d0b3fcc73991ad5f01b985b0e |
| build-verify/cutover-schema.log | 65e09325faea5c5ccbf9dd071e0669c9b3107c5f5635906ef1fdaa28d9fb539d |

The governed playfield package was imported into this worktree with the approved
package importer and manifest: 4710 files, 264151897 bytes. Archive SHA256:
`6b3c06c234923d07a8d476b710d650e620aafd0775602c2e38160af8bdc69b26`.
Input archive was read-only from the existing AORebirth integration package cache;
no client or capture was launched.

## Files changed relative to reconciled baseline

14 added, 8 modified; generated build outputs are excluded.

Added tools:

- Tools/NewEngineCutoverInventory/NewEngineCutoverInventory.csproj
- Tools/NewEngineCutoverInventory/Program.cs
- Tools/ZoneEngineSchemaValidation/CutoverDurableReloadSmoke.cs
- Tools/generate_newengine_cutover_inventory.cmd

Added reports under docs/reports:

- NEWENGINE_CUTOVER_HANDOFF.md
- NEWENGINE_CUTOVER_RECONCILIATION.json
- NEWENGINE_CUTOVER_VALIDATION_RECEIPT.md
- NEWENGINE_SUPPORTED_FEATURE_MATRIX.json
- NEWENGINE_OPERATIONAL_ACCEPTANCE.md
- NEWENGINE_LEGACY_DEPENDENCY_INVENTORY.json
- NEWENGINE_LEGACY_DEPENDENCY_INVENTORY.md
- NEWENGINE_DAO_GAP_INVENTORY.json
- NEWENGINE_DAO_GAP_INVENTORY.md
- NEWENGINE_DATABASE_CUTOVER_AND_ROLLBACK_PLAN.md

Modified:

- AGENTS.md
- AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging.Tests/PlayfieldLifecycleTraceTests.cs
- AORebirth/Server/ZoneEngine_New.Tests/InventoryActionTests.cs
- AORebirth/Server/ZoneEngine_New/Core/Inventory/ItemTemplate.cs
- Tools/ZoneEngineSchemaValidation/Program.cs
- docs/ai/CURRENT_TASK.md
- docs/ai/WORKFLOW.md
- docs/project/PROJECT_STATE.md

The two dependency/persistence JSON files are generated reproducibly. The
feature matrix is authored and schema/path checked; reconciliation records
actual Git provenance. No runtime content data or SQL schema asset was changed.

## Scope and remaining risks

The foundation commit was pushed to the same-named origin branch. No master merge,
developer branch edit, Legacy deletion, full DAO conversion or production
operation was performed. Primary master retains its earlier untracked work plus
the visible nested `tools-temp/cutover001/` worktree directory; that is intentional.
Newer developer changes remain 8 CONFLICTING and 37 UNKNOWN file footprints,
with zero newer commits imported.

The new generic item effect guard is covered by five deterministic cases. Global
unsupported-action integrity still requires the full declared-route acceptance;
the matrix does not certify every catalog operation. The blocking evidence gaps
are the unavailable disposable database execution and the incomplete connected
lifecycle proof. Missing ordinary gameplay content is not a cutover blocker.

Operational readiness and character/inventory integrity remain unproven.
Post-NewEngine-write Legacy rollback safety is UNKNOWN from fresh execution;
source demonstrates why a binary-only rollback must not be assumed safe.
