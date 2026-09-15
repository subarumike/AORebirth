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

## Build, launch and recovery

The normal Windows build and solution use NewEngine only. Legacy engine selectors
fail before database preflight, process startup or shutdown. Process ownership,
schema readiness, idempotent startup and cleanup checks remain. A pre-existing
unmanaged process occupying the zone port is reported, not killed by name.

The private build no longer needs to compile or publish a new Legacy engine.
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

Completed during implementation:

- NewEngine standalone build: PASS.
- Full Windows build without the retired engine: PASS.
- NewEngine behavior tests: PASS, 739/739; offline startup validation: PASS.
- AOtomation packet, evidence, DAO and shared-mechanic tests: PASS, 923/923.
- Runtime content architecture: PASS, 716 files, zero violations/unresolved inputs.
- NewEngine Legacy dependency inventory: PASS, zero edges.
- DAO architecture guard: PASS, zero new violations and zero mission runtime SQL.
- Engine ownership/selection fixtures: PASS, 30/30.
- Engine management contracts: PASS, including rejection of removed engine paths.
- Canonical content generation and offline governance tests: PASS, 46/46.

The earlier `NEWENGINE_RUNTIME_CONTENT_HARDCODE_AUDIT.json` is an explicitly
historical receipt for the accepted content cleanup. Its original paths and
native assembly are preserved as evidence; the current runtime graph is in
`NEWENGINE_CONTENT_ARCHITECTURE_GUARD.json`.

Full source acceptance and private-platform results will be recorded after the
generator/test migration completes. No production operation has been performed.
