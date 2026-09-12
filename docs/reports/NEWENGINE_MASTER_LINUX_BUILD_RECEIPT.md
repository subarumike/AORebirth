# Accepted integration merged to local master; Linux build receipt

Date: 2026-09-12. **Merge/build complete. Pre-deployment manual checks pending. No push or live deployment.**

## Source and preservation

| Identity | SHA/ref |
|---|---|
| MASTER_BEFORE_SHA | `7be49b22b4c0116af9c48dc88a44f86ba802f7c7` |
| ORIGIN_MASTER_SHA | `7be49b22b4c0116af9c48dc88a44f86ba802f7c7` |
| INTEGRATION_BRANCH | `codex/newengine-retail-hydration-dao-001` |
| INTEGRATION_SHA | `b48c22444aa11d8d77d653bcc5e8903b9a2d92e8` |
| COMMON_ANCESTOR | `6e90dda030774726aa2060acb9edb756ea1f635c` |
| RECOVERY_REF | `codex/pre-master-cutover-20260912` |
| MERGE_COMMIT | `bb5dd9165c6201b15ef1e0cfbfd788a3e205486a` |
| SOURCE_SHA | `bb5dd9165c6201b15ef1e0cfbfd788a3e205486a` |

Normal two-parent merge; both selected tips are ancestors. Master-only commit `7be49b22` is preserved. Its three capture-analyzer source files retain identical Git blob identities; its workflow/project-state additions are present. The candidate branch is unchanged. NewEngine remains the default and the explicit Legacy rollback target is retained. No runtime code, schema or assertions were changed to achieve acceptance. Three inherited trailing-space lines in the historical staging report were corrected.

## Validation

| Check | Result |
|---|---|
| Exact Windows source and clean checkout | PASS |
| Windows NewEngine tests | PASS: 544 |
| Windows AOtomation tests | PASS: 1,129 |
| Mandatory integration gates | PASS: 12/12 |
| Shared contracts, DAO guard, inventory/content/package checks | PASS; zero new direct SQL violations |
| Linux-host NewEngine tests | PASS: 544 |
| Linux build/publication | PASS: linux-x64, Release, self-contained |
| Linux source, SQL/package parity and offline acceptance | PASS |
| Linux deployment failure/provenance fixtures | PASS; isolated fixtures, no production operation |
| Isolated Linux server startup, database preflight and listeners | PASS |
| Real-client CharInPlay packet on merged source | PENDING_USER_ACTION |
| Official-client login/zoning/saved state after clean restart | PENDING_USER_ACTION |

Windows command: `Tools\accept_windows_source.cmd --expected-sha bb5dd9165c6201b15ef1e0cfbfd788a3e205486a --mandatory-gate`. Linux command: `bash LinuxBuild/accept-linux-sha.sh --expected-sha bb5dd9165c6201b15ef1e0cfbfd788a3e205486a --expected-placement-manifest-sha 19c53c46f657146f760b4ba5afcea97276501f7d54f1e557923c4f1eb6bf518a --workspace /srv/ao-rebirth-linux-acceptance`. The Linux container uses `mcr.microsoft.com/dotnet/sdk:10.0.302-noble`, a local clone of accepted Windows source, and the explicitly supplied pinned Playfields archive. No remote push was needed.

The first Windows run failed 11 building/teleport tests because the clean checkout lacked its ignored pinned Playfields input. The approved package export/import matched all 4,710 files and the fixed ZIP digest `6b3c06c234923d07a8d476b710d650e620aafd0775602c2e38160af8bdc69b26`; full Windows acceptance then passed on the same SHA. The first Linux invocation was refused for workspace layout; it performed no build. The task-owned checkout was arranged beneath the controlled workspace and the governed workflow passed. Both failed logs are retained.

## Linux artifacts

Built on Linux in Docker Desktop, not merely cross-published from Windows. Files are retained under `build-verify/master-cutover/linux-workspace/repo/LinuxBuild/artifacts`.

| Package | Apphost SHA-256 | Assembly SHA-256 | Files |
|---|---|---|---|
| chatengine | `a8baa80813624688fc6699150900e555c55a03d01dd433b56a28776b9a34c1ba` | `761e60528de388e9734b1689b584055032f0038c7634d9c73f6f852cf0d483fb` | 301 |
| loginengine | `e7a02b999eb07209f614b02bec2c3ea8f63fc3387c284163cb87734ecb93d849` | `66ef9fadbb816a0cbfd30f7a3a5b003b8cdc9f3d92437ccb3ce98ac7d88f394e` | 298 |
| zoneengine | `708d0be649ae7cf46e5e1b0c493fdc49832eed3e0a17b11b15f9413ab31292a2` | `d34e60d0671405b072a31bdce15647b9d49a49b2d48f7715aedd480027a80a9b` | 5687 |
| zoneengine-legacy | `a143bbf7c92884ebe1ea08d389da628c16abe6708dd35247e19bd96c9417b75c` | `4c4eb352baefa92854872eb3e1b7415e86403d89abdc55d9e67a8d525eec9fc6` | 1029 |

The JSON receipt records absolute paths, build-host details, package provenance and log hashes. Complete file hashes: `build-verify/master-cutover/linux-artifact-sha256.json` (SHA-256 `03961a5e877045ea33560525d195c5ce18ab157abbf8510d5e460b5b7e6b93c4`). Login/NewEngine packages carry accepted `SOURCE_SHA`, `BUILD_PROVENANCE.env` and `LINUX_ACCEPTANCE.env`; the governed production release manifest is retained with the artifacts.

## Focused client/restart session

The isolated test stack runs the accepted merged-source binaries against the same persistent test database. Executable paths and hashes were checked inside the container. A read-only baseline records character 9950, 44 stats, 1 carried item(s), item identity/ownership/quantity/slot data, cash stat presence/value, uploaded nanos and PF655 location. The baseline predates the requested normal gameplay/save sequence and does not establish post-restart recovery. No database reseed or restore was performed.

Capture file: `C:\Users\Mike\Documents\AORebirth\build-verify\master-cutover\retail-merged-1789250251047.pcapng`. CharInPlay direction/timestamp/session correlation and decoded packet bytes are pending actual client actions. Historical gameplay PASS remains evidence for source `032ee4cd`, not this merge. Captures and private local state are ignored evidence, never runtime inputs.

Mike action: use the merged E-client test launcher, log in, zone out and back, then log out and report completion. Codex will capture the durable post-save state, cleanly stop the test services, verify exited processes, restart the same binaries/database, and request the final login/zoning check. Only clean-restart recovery will be claimed.

## Final boundaries and evidence

`PUSH_PERFORMED=NO`; `LIVE_DEPLOYMENT_PERFORMED=NO`; `PRODUCTION_DATABASE_MODIFIED=NO`; `PRODUCTION_SERVICES_RESTARTED=NO`. Local master contains the history-preserving merge; a separate receipt-only commit follows the tested source. The Windows detached acceptance worktree and Linux source checkout are tracked-clean. Unrelated original untracked work and developer worktrees are preserved. Remaining checks are manual packet evidence and the clean-restart state comparison; they do not erase the completed merge/build.

Validation evidence and inspected files are enumerated in [the JSON receipt](NEWENGINE_MASTER_LINUX_BUILD_RECEIPT.json). Receipt files changed: this report, its JSON, `docs/ai/CURRENT_TASK.md`, and `docs/project/PROJECT_STATE.md`. Imported paths are recorded in the JSON merge inventory.

The main-checkout full secret scan flags seven pre-existing untracked staging environment files. They are preserved and excluded from the commit. The clean accepted checkout plus the exact receipt files passes the existing scanner; evidence: `build-verify/master-cutover/receipt-clean-secrets.log`.
