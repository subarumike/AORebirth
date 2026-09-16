# Legacy ZoneEngine retirement

## Reason and scope

Mike authorized removal after the NewEngine content cleanup reached master at
`c5af4ac18a1378dc41c37d31b9ac62ac46c5f8a0`. Work is on
`codex/retire-legacy-engine-20260915` in the existing integration worktree.

No live application requires the old ZoneEngine executable. Remaining references
were shared source ownership, editable assets, regression fixtures, build/launch
selection and an obsolete documentation-tool project reference. The documentation
tool already referenced the independent PlayfieldLoader library directly.

This task removes the engine implementation, project and fresh build/publish/
launch routes. It does not change production, database schemas, player DAO
semantics or Delmus's branch. Archived release packages and their database-restore
recovery controls remain preserved outside the current engine source tree.

Inspection covered the retired `Program.cs` and project, all source/content
consumers of that tree, the solution and shared source inventory, engine launch/
restart/stop/status wrappers, DAO and runtime-source guards, offline generators,
and their affected tests. The local move, reconciliation, test-retirement and
acceptance-filter receipts under `tools-temp/content-cleanup-001` record the exact
file mappings and retired assertions. The source-ownership table below summarizes
the changed files; the socket repair is identified separately in validation.

## Source ownership

The move manifest records source/destination paths and verification for 276
retained files. Every move was reconciled against the starting commit; 623
unchanged obsolete tracked files were then removed individually. No recursive
delete outside the owned worktree was performed.

| Retained purpose | Files | Current owner |
| --- | ---: | --- |
| Shared Core entities used by Login and libraries | 21 | `AORebirth.Core/Entities` |
| Generic NewEngine mechanics/models/loaders | 57 | `ZoneEngine_New/SharedGameplay` |
| Offline capture analysis helpers | 4 | Offline analyzer support |
| Historical gameplay test fixtures | 137 | `Tests/Fixtures/Gameplay` |
| DAO recovery test fixture | 1 | `Tools/CharacterDaoValidation` |
| Editable runtime content | 44 | `ZoneEngine_New/Content` |
| Offline content evidence | 6 | Offline/test evidence |
| Mission reward data | 5 | NewEngine-owned data |
| Mission-level source data | 1 | Offline mission data |

No compiled per-NPC/vendor/quest content was reintroduced into NewEngine. Its
runtime content guard still audits 716 files with zero violations and no missing
project inputs. The dependency inventory now reports zero Legacy edges.
Existing namespaces on retained shared contracts are preserved for compatibility;
they do not select, build or load the removed engine executable.

## Build, launch and recovery

The normal Windows build and solution use NewEngine only. Legacy engine selectors
fail before database preflight, process startup or shutdown. Process ownership,
schema readiness, idempotent startup and cleanup checks remain. A pre-existing
unmanaged process occupying the zone port is reported, not killed by name.

The validated private build candidate no longer compiles or publishes Legacy.
Historical release restoration remains a separate recovery path: restoring an
old package must retain its existing database-restore requirements. An executable
rollback alone is not established as valid after NewEngine writes.

## Evidence and tests

The earlier compiled-catalog reconciliation and activation scripts are retired.
Capture evidence remains offline; it is not permission to activate NPCs. The
historical Legacy population audit must not be relabelled current NewEngine
population. Current editable-content consumers receive a separate audit.

The offline historical roster remains 1,565 entries: 559 accepted and 1,006
unresolved, with 260 certified combat profiles. Its 101 definitions/96 profiles
are historical readiness evidence. The separate current content audit records
23 authored NPC definitions and six consumer classes; neither count establishes
live population or client acceptance.

Source-inspection tests tied exclusively to the removed engine are replaced by
current-consumer checks where applicable or explicitly retired. Packet, DAO,
shared mechanics and NewEngine behavior regressions remain required.
The retirement receipt records 205 removed engine-only methods and 70 mixed
methods retaining their independent assertions; the remaining AOtomation suite
passes all 923 tests.

Subway and Temple acceptance filters retain packet, evidence and shared-mechanic
tests. Retired engine-only filters are removed; a shared VSTest setting makes an
empty filter fail instead of silently succeeding. Current engine behavior remains
covered by the mandatory NewEngine suite.

## Validation

The first exact-source gate at `ecefa6737c6cd60853706e9599568f75c52ee0ee`
passed stages 1–9, then exposed a pre-existing ISCom socket-disposal race in
stage 10. The transport and its test were unchanged by the retirement. A peer
disconnect callback could close or replace the socket while `Dispose` was using
the same mutable field. The scoped follow-up makes ownership and disposal
idempotent and adds focused lifecycle regressions; packet behavior is unchanged.
The failed gate is retained as evidence and is not counted as acceptance.

Accepted public runtime source:
`bf7ce16bbbdc6fd7d5baad5cfe31ebfd781ddfdb`.
Windows receipt: `build-verify/windows-acceptance-bf7ce16b.env`.
The repaired revision passed all 12 mandatory stages with a clean worktree.

Completed validation:

- NewEngine standalone build: PASS.
- Full Windows build without the retired engine: PASS.
- NewEngine behavior tests: PASS, 743/743; offline startup validation: PASS.
- AOtomation packet, evidence, DAO and shared-mechanic tests: PASS, 923/923.
- Runtime content architecture: PASS, 716 files, zero violations/unresolved inputs.
- NewEngine Legacy dependency inventory: PASS, zero edges.
- DAO architecture guard: PASS, zero new violations and zero mission runtime SQL.
- Engine ownership/selection fixtures: PASS, 30/30.
- Engine management contracts: PASS, including rejection of removed engine paths.
- Canonical content generation and offline governance tests: PASS, 46/46.
- Exact-source Windows acceptance: PASS, all 12 mandatory stages.
- Focused transport lifecycle regressions: PASS, 5/5 including 25 race iterations.
- Disposable schema and restart acceptance: PASS. Negative/current schema checks,
  explicit disposable migration, copy-failure rollback, fresh DAO reload and two
  process restarts passed. All 40 stats and two items retained the same snapshot
  hash; the runtime database fingerprint was unchanged. Disposable container and
  network residue: none. Production contact: none.
- Connected acceptance: PASS. Login wire admission, the positive lifecycle,
  actual zoning through `4582 -> 800 -> 4582`, durable state and engine restart
  passed. Final durable state contained 44 stat rows and six item rows, with the
  character offline. Disposable container and network residue: none.

The accepted Debug output was frozen separately before disposable persistence
validation. All 10,485 copied files matched their source SHA256 hashes. Frozen
engine identities:

- NewEngine: `f8393cbcacdbf8458d947515df4f02faa61c646e6fec50aee2f8c49c1782ea00`.
- LoginEngine: `5410f0606f904179860e3c7e9a3a138cfd192d514d9a8b6e7a64c5205f39df8b`.

Both frozen engine hashes were unchanged after the disposable gates; harness
rebuilds of normal output did not replace the tested binaries. The connected
receipt records a dirty worktree because these documentation receipts were being
written. The earlier exact-source and freeze records prove clean source, and the
only intervening tracked edits were documentation.

The earlier `NEWENGINE_RUNTIME_CONTENT_HARDCODE_AUDIT.json` is an explicitly
historical receipt for the accepted content cleanup. Its original paths and
native assembly are preserved as evidence; the current runtime graph is in
`NEWENGINE_CONTENT_ARCHITECTURE_GUARD.json`.

Native private-platform acceptance also passed on assembly
`1d8521b8f11c4e842bfdf3f16731804b084824e7`, composed from the accepted public source
and private operations `2a9287d4d72367f86d376b80183b7620d37ef554`:

- Native restore, build, 743 NewEngine tests, publication and release manifest: PASS.
- Private retirement/content/package/deployment fixtures: PASS, respectively
  6, 9, 21 and 81 tests; editable-deployment and historical-artifact fixtures:
  PASS, 11 and eight tests. Compatibility and source guards: PASS.
- Package: 6,525 archive members, zero Legacy executable/project artifacts.
- Package SHA256: `3c1732cb2e9f3fbdee4a9371975670e2d969f90a86d5c81001f8523d1501134d`.
- Native NewEngine SHA256: `dcbb8c877af58ab48afb1e3815f4e0cca872e17926f0ea4d440d5e88216d5574`.

An earlier native attempt stopped on a private test-layout assumption before
compilation. The corrected fixture positively verifies assembly provenance and
retirement in assembled mode, while retaining patch inspection in the tooling
checkout. Both modes and the fresh complete native run passed. The failed attempt
is retained separately and is not an accepted package.

After Mike explicitly approved updating the private Linux build tools, the private
operations default was fast-forwarded from `6d16c8a07cac0bfcb99f85486fe26b786939acce`
to accepted `2a9287d4d72367f86d376b80183b7620d37ef554`. The remote SHA was verified;
the candidate and default are synchronized. The initial automatic approval block
and subsequent authorization remain recorded in the local receipt. No production
operation has been performed; staging/client acceptance and deployment are
separate from this source retirement.
