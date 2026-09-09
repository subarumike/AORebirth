# NewEngine production cutover foundation

## Decision

NewEngine is the default on this Mike-owned branch. Keep Legacy present.
The foundation can be reviewed and tested now; production cutover is **not yet
accepted**. Full gameplay parity is not a release gate. Missing NPC bindings,
dialogue, pets and other catalog coverage are follow-up gameplay work.

Two operational gaps remain: the disposable MySQL runner cannot start Docker on
this machine, and the fixture does not exercise authenticated account login,
character selection and connected player mutations/reconnect across restart.
Repository reload tests and an empty process lifecycle do not prove that sequence.
No corruption was observed; the required proof is unavailable.

## Provenance and ownership

- Starting origin/master: `6e90dda030774726aa2060acb9edb756ea1f635c`.
- Reconciled baseline: `4dac603b82dfe64206b155e7e6c0499a9f8ad7f8`, on
  `codex/zoneengine-final-runtime-consumers`.
- Branch: `codex/newengine-production-cutover-001`.
- Worktree: `C:\Users\Mike\Documents\AORebirth\tools-temp\cutover001`.
- Master is an ancestor of the baseline. The integration was a fast-forward
  preserving all ten intervening commits, with no conflict resolutions.
- [Reconciliation JSON](NEWENGINE_CUTOVER_RECONCILIATION.json) records every imported
  commit and the newer developer change footprints.
- Developer ref inspected read-only: `origin/New-ZoneEngine` at
  `53c858d9900266fb6740975dbb2b5011a5792e66`; prior reconciled developer input
  `d2d9844673a766c02aa768623db9f0ab5b6ea01c`.
  Newer file changes are CONFLICTING where merge-tree reports a conflict and
  otherwise UNKNOWN. UNKNOWN does not mean safe or required. No newer developer
  commits were imported; no developer branch/worktree was changed.
- Primary checkout pre-existing untracked work was preserved:
  `Tools/NPC_Inspect/`, `docs/reference/enemy-templates/`,
  `quest example from PRK.txt`, and the three earlier branch/cutover reports in
  `tools-temp`. No stashing, cleaning, master merge, production operation or
  schema modification occurred.

## Implementation

`ItemTemplate.ExecuteOnUseSpells` previously prevalidated recognized functions,
then executed them sequentially without an aggregate transaction. A later effect
could fail after an earlier durable stat/upload effect had changed memory and
could subsequently be saved. The generic path now accepts only OpenBank and
SystemText after validating the complete graph. Durable effects must have a
dedicated transactional owner; unsupported graphs reject before any effect.
Five parameterized regressions cover Set, Hit, SetFlag, ClearFlag and UploadNano,
including no earlier message, item/count/credit mutation or persistence call.
Supported transactional crystal/stim and other dedicated routes remain covered
by the existing suite. Some generic item effects are intentionally unavailable.

The AOtomation patrol test now verifies the canonical path relative to the actual
repository and compares exact committed replay bytes. Its previous absolute-path
check incorrectly rejected a legitimate isolated worktree under `tools-temp`.

The disposable schema tool now contains `CutoverDurableReloadSmoke`: exact
instance/template/QL/source/owner/slot/stack/credits/location checks through actual
repositories, across two clean engine process cycles, plus a check that Legacy
inventory tables stay stale after NewEngine writes. It is compiled but has not
executed here. It explicitly emits LOGIN_WIRE_ACCEPTANCE=NOT_EXERCISED.

No DAO implementation was moved. No Legacy source was deleted. No NPC, item,
quest, vendor or other game content was added to runtime C#; fixture identities
exist only in tests. Inherited content carriers still need separation into
validated data during the dedicated extraction task.

## Defaults already supplied by the integrated baseline

| Surface | Evidence |
| --- | --- |
| Normal Windows build | `Tools/build_aorebirth_debug.cmd`; integrated build includes NewEngine |
| Dedicated build | `NewZoneEngineBuild/build.cmd:13` selects NewEngine |
| Development launch | `start-engines.ps1:21`; missing binary/readiness errors throw at 288–296 |
| Linux publish | `LinuxBuild/publish-zoneengine.cmd:8` selects NewEngine unless explicitly overridden |
| Linux service | `LinuxBuild/deployment/systemd/ao-rebirth-zoneengine.service:30` validates package and schema before NewEngine start |
| Integration defaults | Mandatory gate and Windows source acceptance include the NewEngine suite; default-engine parity guard passes |

Legacy is an explicit debug/rollback selection, not an automatic failure fallback.
The default-engine, package, SQL and source-inventory guards passed in local
cross-publication. This verifies branch/package configuration, not live deployment.

## Artifacts and acceptance

- [Supported feature matrix](NEWENGINE_SUPPORTED_FEATURE_MATRIX.json): 25 scoped
  capabilities, 8 supported, 16 partial fail-closed, 1 unsupported fail-closed.
- [Operational acceptance](NEWENGINE_OPERATIONAL_ACCEPTANCE.md): exact evidence
  boundaries and remaining connected acceptance sequence.
- [Legacy dependencies](NEWENGINE_LEGACY_DEPENDENCY_INVENTORY.md): 93 links, all retained.
- [DAO gaps](NEWENGINE_DAO_GAP_INVENTORY.md): 99 method rows, reviewed transaction owners.
- [Database and rollback plan](NEWENGINE_DATABASE_CUTOVER_AND_ROLLBACK_PLAN.md).
- Final gate receipts are recorded separately in
  `NEWENGINE_CUTOVER_VALIDATION_RECEIPT.md` when available, with the exact tested SHA.

Files inspected include the above defaults, schema contract, NewEngine
project/reference graph and all 216 NewEngine C# source files for static inventory,
the 93 linked Legacy source files, persistence implementations, item-use action
boundary, feature tests named in the matrix, disposable fixtures, governing AI
documents and newer developer Git changes. The JSON source hashes make the
inventory inputs reproducible. Static enumeration is not a semantic proof of
every method or every content route.

## Failure classification and execution corrections

- Docker fixture: ENVIRONMENTAL. `SCHEMA_VALIDATION=FAIL docker-image-failed`.
  Docker Desktop backend fails initializing its local Inference socket; no
  disposable database was created. Its attempted task-owned startup processes
  were stopped. No global Docker configuration was changed.
- Patrol test: PREEXISTING_BASELINE_FAILURE exposed by the isolated worktree path;
  repaired and focused test PASS.
- Incomplete connected acceptance harness: UNKNOWN acceptance, a concrete
  instrumentation gap, not an observed player-state loss.
- Initial schema command lacked required arguments and several source reads used
  incorrect paths. A PowerShell reconciliation expression also failed parsing.
  These were agent execution errors, not project blockers; failed reads/parser
  changed no repository state. Corrected forms use the documented
  `cmd /d /c Tools\run_zoneengine_schema_validation.cmd --run-disposable --engine <absolute-dll>`,
  simple `git log`/`git diff`/`git merge-tree` calls and structured JSON file writing.
  Repository paths were resolved using tracked files and targeted searches.

## Next bounded acceptance work

Provide a working disposable MySQL/Docker environment; run the existing explicit
schema wrapper on this exact branch. Then complete the authenticated protocol
fixture from repository packet contracts, including character selection and
supported durable actions before logout/reconnect and after clean restart.
Validate active nanos/morphs and authored/generated mission state with exact
persisted expectations as part of that connected sequence. Do not infer wire
behavior from DAO success. Only after these integrity gates pass is a separate
production cutover proposal ready. Legacy extraction and DAO consolidation remain
separate work with the inventories below.
