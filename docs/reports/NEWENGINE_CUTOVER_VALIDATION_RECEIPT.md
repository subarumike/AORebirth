# NewEngine cutover validation receipt

## Exact source and result

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
