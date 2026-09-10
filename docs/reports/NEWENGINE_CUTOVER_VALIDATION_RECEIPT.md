# NewEngine cutover validation receipt

## Retail ZoneInfo and redirect acceptance (supersedes the limits below)

The official retail capture and recovered client evidence close the previously
missing ZoneInfo layout and normal redirect sequence. Final exact-source Windows
acceptance passes at `75a78a88535bffc321fe82c5ab824852c4636b47`. The connected
LoginEngine/NewEngine fixture passes against the identical runtime code at
`75c8a784ad99be48cf51f9d1652e73d2791c3f5c`; `75a78a88` adds only regenerated
DAO inventory metadata.

```text
RETAIL_CAPTURE=C:\Users\Mike\Documents\AORebirth\tools-temp\live-pcaps\retail-handshake\20260910-041844-14c0d788
RETAIL_PCAP_SHA256=8F38383A7727DE8142B3182FD2717E84882B4D0FAE95934330B65CD93EB79171
CLIENT_STATIC_EVIDENCE_COMMIT=67aa6f36b030a99c73736acbc33bb3ec301bce10
ZONEINFO_FRAME_SIZE=46
ZONEINFO_BODY_SIZE=26
ZONEINFO_EVENT_SERVER_TYPE=1
ZONEINFO_PLAYER_ID=0 conservative unresolved semantic
INITIAL_ZONELOGIN_COOKIE_REUSE=PROVEN
REDIRECT_TYPE_0X3C_ENDPOINT=PROVEN
REDIRECT_ZONELOGIN_COOKIE_REUSE=PROVEN
REDIRECT_AUTHORITY=endpoint-bound expiring one-use server authorization
UNEXPECTED_LOSS_AUTOMATIC_RECONNECT=NOT_SUPPORTED_BY_STATIC_CLIENT_EVIDENCE
NEWENGINE_TESTS=PASS 505/505
AOTOMATION=PASS 1129/1129
INTEGRATION_GATES=PASS 12/12
WINDOWS_ACCEPTANCE=PASS
CONNECTED_LOGIN_ACCEPTANCE=PASS
CONNECTED_REDIRECT_WIRE_ACCEPTANCE=NOT_RUN
PLAYFIELD_TRANSFER_REDIRECT_ACCEPTANCE=PASS
CUTOVER_INVENTORIES=PASS LEGACY_DEPENDENCY_EDGES=93 PERSISTENCE_METHOD_ROWS=99
DAO_ARCHITECTURE_GUARD=PASS NEW_VIOLATIONS=0 LEGACY_BASELINE_EXCEPTIONS=7
RETAIL_CLIENT_RUNTIME_ACCEPTANCE=NOT_RUN
PRODUCTION_DEPLOYMENT=NOT_RUN
DEVELOPER_REF_CURRENT=ced79688bc9ad1011119b77e033e37530bd56941
DEVELOPER_NEW_COMMIT_IMPORTED=NO
DEVELOPER_SINGLE_COMMIT_MERGE_SIMULATION=CONFLICT
```

One mandatory-gate attempt hit the existing nondeterministic Buckethead world and
stock admission test. The isolated test passed immediately and the complete
mandatory gate then passed 505/505 without source changes. Earlier Stage 3 and
Stage 7 contract failures were real manifest drift from the new public API and
46-byte ZoneInfo shape; both manifests were updated and subsequently passed.

The remaining gates are an official-client run against the AORebirth candidate,
deployed Linux service identity/shared-directory inspection, and database
backup/restore readiness. PlayerID's runtime meaning is still unknown; zero is
sent rather than inventing identity semantics. Master merge and Legacy deletion
should follow staging acceptance and a rollback rehearsal. Full DAO conversion
is not complete: the current inventory has 99 persistence method rows and the
architecture guard retains seven reviewed Legacy baseline SQL exceptions.
The current developer-branch tip was inspected read-only after the final fetch.
Its new NPC/pathing commit conflicts with the cutover runtime and is quarantined
from this candidate; it is not a login/redirect dependency.

## Final secure handoff acceptance

**Operational candidate: YES under the scoped connected-integrity gates.**
The historical unauthenticated admission failure is closed. This accepts the
automated synthetic-client candidate; it does not claim a live deployment,
complete original-retail wire compatibility or fresh Linux-host execution.
The exact remaining ZoneInfo/redirect evidence and deployment work are in
[the handoff security report](NEWENGINE_ZONE_HANDOFF_SECURITY.md#what-is-still-needed-and-how-to-get-it).
Full gameplay parity is not required for this scoped decision.

```text
STARTING_SHA=b87faf8b6de31d22f79d8f469990c27592bab6f9
SOURCE_SHA=65f7e3c9e2d13b37a58f27bd1dfd72b1917ea80d
IMPLEMENTATION_COMMIT=c6c7a76736f0f779c447c2dd0d48e2699cea0993
CONTRACT_COMMIT=65f7e3c9e2d13b37a58f27bd1dfd72b1917ea80d
BRANCH=codex/newengine-production-cutover-001
WORKTREE=C:\Users\Mike\Documents\AORebirth\tools-temp\cutover001
ORIGIN_MASTER_SHA=6e90dda030774726aa2060acb9edb756ea1f635c
DEVELOPER_REF_SHA=53c858d9900266fb6740975dbb2b5011a5792e66
DEVELOPER_BRANCH_TOUCHED=NO
MASTER_MODIFIED_DIRECTLY=NO
PRODUCTION_DATABASE_MODIFIED=NO
PRODUCTION_SERVICE_MODIFIED=NO
NEWENGINE_DEFAULT=YES
LEGACY_PRESENT=YES
LEGACY_REMOVED=NO
FULL_DAO_CONVERSION_PERFORMED=NO

HANDOFF_MECHANISM=two recovered opaque uint32 cookies; CSPRNG 64-bit value; stored SHA256 digest
HANDOFF_AUTHORITY=shared private ZoneHandoffStore files; cross-process atomic claim
HANDOFF_ACCOUNT_BOUND=YES
HANDOFF_CHARACTER_BOUND=YES
HANDOFF_EXPIRATION_ENFORCED=YES
HANDOFF_SINGLE_USE_OR_OWNERSHIP_ENFORCED=YES
HANDOFF_ATOMIC_CLAIM=YES
NO_HANDOFF_REJECTION=PASS
UNKNOWN_HANDOFF_REJECTION=PASS
EXPIRED_HANDOFF_REJECTION=PASS
CONSUMED_HANDOFF_REJECTION=PASS
REPLAY_REJECTION=PASS
WRONG_ACCOUNT_REJECTION=PASS
WRONG_CHARACTER_REJECTION=PASS
CONCURRENT_REPLAY_REJECTION=PASS
STALE_HANDOFF_REJECTION=PASS
DIRECT_ZONE_SOCKET_CONNECTED=YES
DIRECT_ZONE_SESSION_ADMITTED=NO
DIRECT_ZONE_WORLD_ENTRY=NO
DIRECT_ZONE_DURABLE_MUTATION=NO

AUTHENTICATED_LOGIN=PASS
CHARACTER_LIST=PASS
CHARACTER_SELECTION=PASS
ZONE_HANDOFF_VALIDATION=PASS
ZONE_SESSION=PASS
WORLD_ENTRY=PASS
CONNECTED_DURABLE_MUTATION=PASS
CHARACTER_SAVE=PASS
INVENTORY_IDENTITY=PASS
CREDITS_PERSISTENCE=PASS
AUTHENTICATED_RECONNECT=PASS
STATE_AFTER_RECONNECT=PASS
CLEAN_ENGINE_RESTART=PASS
AUTHENTICATED_RECONNECT_AFTER_RESTART=PASS
STATE_AFTER_RESTART=PASS
ACTIVE_NANOS_RELOAD=PASS
MORPH_RELOAD=PASS
AUTHORED_MISSION_RELOAD=PASS
GENERATED_MISSION_RELOAD=PASS
UNSUPPORTED_ACTION_FAIL_CLOSED=PASS
TRANSACTIONAL_INTEGRITY=PASS
SCHEMA_VALIDATION=PASS
DURABLE_RELOAD_SMOKE=PASS
POST_NEWENGINE_WRITE_EXECUTABLE_ONLY_ROLLBACK_SAFE=NO
ROLLBACK_DATABASE_RESTORE_REQUIRED=YES

NEWENGINE_TESTS=PASS 498/498
AOTOMATION=PASS 1129/1129
INTEGRATION_GATES=PASS 12/12
WINDOWS_ACCEPTANCE=PASS
LINUX_SOURCE_OR_PUBLISH_ACCEPTANCE=PASS linux-x64 self-contained publication on Windows
FRESH_LINUX_HOST_RUNTIME_ACCEPTANCE=NOT_RUN
RETAIL_CLIENT_RUNTIME_ACCEPTANCE=NOT_RUN
SOURCE_WORKTREE_TRACKED_CLEAN=YES
ZONEENGINE_NEW_BINARY_SHA256=296788080dfac666689e0218066a77b229ebd2877a46d9f2d63e06f424d8f1e9
LOGINENGINE_BINARY_SHA256=6f451201fbb61ece5a5ef7a83c685e52478f34caf7ee5dcdef80f6f6756c358a
DATABASE_FIXTURE_ID=aorebirth-zone-schema-0574fa378d4741a9972445822161f818
ACCEPTANCE_TIMESTAMP=2026-09-10T03:52:24.9515398Z
ENGINE_PID_BEFORE=29680
ENGINE_PID_AFTER=25200
ZONE_HANDOFF_CONCURRENCY=PASS ATTEMPTS=8 ADMITTED=1 REJECTED=7
UNCONSUMED_HANDOFF_AFTER_PROCESS_RESTART=PASS
DISPOSABLE_CONTAINER_RESIDUE=NONE
DISPOSABLE_NETWORK_RESIDUE=NONE
DISPOSABLE_CLEANUP=PASS
SOURCE_COMMITS_PUSHED=YES
```

The direct-zone fields describe attempts without a valid unconsumed ticket.
Socket acceptance alone is not session/world admission. Every rejected wire
case compares complete disposable-table fingerprints before/after. The concurrent
winner is separately checked against exact character, inventory, credits, nano,
morph and mission expectations. Thirteen new tests cover the authority and
recovered wire layout; the prior 485 NewEngine tests remain in the 498 total.

Both source commits were pushed before final validation. This receipt is a
documentation-only follow-up; its commit must not replace SOURCE_SHA when
identifying the tested runtime. The final task reply identifies the ending
documentation commit. Final execution of each wrapper returned zero.

### Decision table

| Gate | Result |
| --- | --- |
| Authenticated LoginEngine path works | YES |
| Character selection works | YES |
| Zone handoff validated server-side | YES |
| No-handoff admission rejected | YES |
| Forged/unknown handoff rejected | YES |
| Expired handoff rejected | YES |
| Replayed handoff rejected | YES |
| Wrong-account use rejected | YES |
| Wrong-character use rejected | YES |
| Concurrent replay permits at most one owner | YES; 1 of 8 |
| Direct zone connection cannot establish player session | YES without valid ticket |
| Direct zone connection cannot mutate durable state | YES without valid ticket |
| Normal authenticated world entry still works | YES; synthetic connected client |
| Reconnect still works | YES; fresh authentication |
| Clean restart lifecycle still works | YES |
| Character/inventory persistence remains exact | YES |
| Unsupported gameplay mutations still fail closed | YES |
| Transactional integrity remains proven | YES |
| Full gameplay parity required | NO |
| Legacy removed | NO |
| Full DAO conversion performed | NO |
| Production modified | NO |
| Developer branch touched | NO |

### Exact commands and evidence

Commands ran in the worktree above, through the documented wrappers:

```cmd
cmd /d /c Tools\accept_windows_source.cmd --expected-sha 65f7e3c9e2d13b37a58f27bd1dfd72b1917ea80d --mandatory-gate
cmd /d /c Tools\run_zoneengine_schema_validation.cmd --run-disposable --engine C:\Users\Mike\Documents\AORebirth\tools-temp\cutover001\AORebirth\Built\Release\ZoneEngine_New\ZoneEngine_New.dll
cmd /d /c LinuxBuild\publish-zoneengine.cmd linux-x64 true
cmd /d /c Tools\run_newengine_connected_acceptance.cmd --engine C:\Users\Mike\Documents\AORebirth\tools-temp\cutover001\AORebirth\Built\Debug\ZoneEngine_New\ZoneEngine_New.dll --login-engine C:\Users\Mike\Documents\AORebirth\tools-temp\cutover001\AORebirth\Built\Debug\LoginEngine.exe
cmd /d /c Tools\generate_newengine_cutover_inventory.cmd --check
```

Windows acceptance includes the normal Legacy/NewEngine build, cross-platform
contracts, messaging and NewEngine tests, all mandatory stages and a clean-tree
check. Placement manifest SHA256:
`4874db0a15cfe1e576e6fb12408dfdd8e4614e07c6f4152707e5bdacfc119c57`.
Linux publication passes default/source/SQL/backend guards, offline startup,
4/4 package negatives and 2/2 source-omission negatives. The inventory remains
93 Legacy links and 99 persistence method rows, now scanning 217 NewEngine files.

| Ignored local artifact | SHA256 |
| --- | --- |
| build-verify/handoff-connected-final.log | 64d4c16fe83a08671cc64e6a5e23c20cbb2b0d431641c1eb62025eedec7be6fa |
| build-verify/handoff-schema.log | c6cd44515f9d8b64215e557d5e8e371fbe1eacc6e4fa9592e124d88faa575777 |
| build-verify/handoff-windows.log | ac0dce69fba2a467d7395c0fe212f60a62053fee1b4990781f06cc4d805630e9 |
| build-verify/handoff-linux.log | 8fbb10b411db42548876cce23617153869b03d58474972643a920e88f30ffb55 |
| build-verify/windows-acceptance-65f7e3c9.env | 21eb1dbdbb47a76499ad5300d18a9b4cc1a7cc809afe1a0520937286f459ca5f |

### Corrections and evidence limits

An early development negative test observed LoginEngine's normal asynchronous
Online cleanup during its fingerprint interval. The fixture now awaits that
cleanup before taking the baseline; no database writes or weaker assertions
were added. The first exact-source attempt also exposed the expected additive
Stage 3 public API drift. The existing LegacyStage3ContractTool regenerated the
manifest; review showed only the three new handoff types (35 added lines).
The following exact-source run passed both Windows and Linux contract verification.
No failing run was relabelled PASS.

Connected inventory movement and morph cancellation are proven mutations.
Active nano and mission state are seeded before startup, then proven to reload
over authenticated wire. Credits preservation is exact, not a credit-changing
action. Generic TCP-loss reconnect, type-0x3c cookie reuse and the original
retail 26-byte ZoneInfo tail remain explicitly outside this acceptance.

### Files inspected and changed

Inspected: LoginEngine authentication/selection/client lifecycle and CheckLogin;
AOtomation ZoneInfo/ZoneLogin contracts and serializer; NewEngine dispatcher,
session, login handler and composition; existing account DAO resolver and online
ownership guard; Linux service ownership-directory settings; connected/disposable
fixtures, build/contract inventory outputs, governing documents and the recovered
retail handshake report. Linked evidence is identified in the security report.

Six files added, twenty modified relative to the starting SHA. Added:

- AORebirth/Libraries/Source/AORebirth.Database/Dao/ZoneHandoffStore.cs
- AORebirth/Server/ZoneEngine_New/Core/Network/ZoneAdmissionGate.cs
- AORebirth/Server/ZoneEngine_New.Tests/ZoneHandoffStoreTests.cs
- AORebirth/Server/ZoneEngine_New.Tests/ZoneHandoffWireTests.cs
- Tools/ZoneEngineSchemaValidation/ConnectedHandoffAcceptance.cs
- docs/reports/NEWENGINE_ZONE_HANDOFF_SECURITY.md

Modified:

- AORebirth/Libraries/Source/AORebirth.Database/AORebirth.Database.csproj
- AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/SystemMessages/ZoneLoginMessage.cs
- AORebirth/Server/LoginEngine/CoreClient/Client.cs
- AORebirth/Server/LoginEngine/MessageHandlers/SelectCharacterHandler.cs
- AORebirth/Server/ZoneEngine_New/Core/MessageHandlers/ZoneLoginHandler.cs
- AORebirth/Server/ZoneEngine_New/Core/Network/ZoneMessageDispatcher.cs
- AORebirth/Server/ZoneEngine_New/Program.cs
- AORebirth/Server/ZoneEngine_New/ZoneEngine_New.csproj
- LinuxBuild/Tools/CompatibilitySmokeTests/Fixtures/LegacyStage3Contracts.manifest
- LinuxBuild/source-inventory/AORebirth.Database.CompileItems.props
- Tools/ZoneEngineSchemaValidation/ConnectedAcceptanceSmoke.cs
- Tools/ZoneEngineSchemaValidation/ConnectedEngineProcess.cs
- Tools/ZoneEngineSchemaValidation/ConnectedWireClient.cs
- docs/ai/CURRENT_TASK.md
- docs/project/PROJECT_STATE.md
- docs/reports/NEWENGINE_CONNECTED_ACCEPTANCE.md
- docs/reports/NEWENGINE_CUTOVER_HANDOFF.md
- docs/reports/NEWENGINE_CUTOVER_VALIDATION_RECEIPT.md
- docs/reports/NEWENGINE_DAO_GAP_INVENTORY.json
- docs/reports/NEWENGINE_OPERATIONAL_ACCEPTANCE.md

Generated tracked artifacts: Database source inventory, additive Stage 3 public
contract manifest and DAO inventory JSON. Builds, packages and validation logs
remain ignored; fixture-owned processes, directory, database container and network
were removed. Primary master checkout retains its original untracked work.

```text
NEWENGINE_OPERATIONAL_CUTOVER_READY=YES (scoped candidate acceptance)
ZONE_HANDOFF_FAIL_CLOSED=PASS
UNAUTHENTICATED_ZONE_ADMISSION_POSSIBLE=NO
CHARACTER_AND_INVENTORY_INTEGRITY_PROVEN=YES
AUTHENTICATED_CONNECTED_ACCEPTANCE_PROVEN=YES
FULL_GAMEPLAY_PARITY_REQUIRED_FOR_CUTOVER=NO
LEGACY_REMOVAL_PERFORMED=NO
FULL_DAO_CONVERSION_PERFORMED=NO
DEVELOPER_BRANCH_TOUCHED=NO
PRODUCTION_DATABASE_MODIFIED=NO
```

The receipt below is preserved history of the former admission failure.

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
