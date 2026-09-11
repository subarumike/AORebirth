# DAO stack cutover integration

Status: implementation and acceptance in progress. No runtime-completion claim.

## Scope and provenance

- Integration branch: `codex/dao-stack-cutover-integration-001`.
- Integration worktree: `C:\Users\Mike\Documents\AORebirth\tools-temp\dao-stack-integration001`.
- Actual starting cutover SHA: `827c7fb50a9d860b6f671c73baebc2447c282dff`.
  It matches the reviewed SHA. Tracked cutover files were clean; existing untracked
  investigation artifacts were left untouched.
- Unmodified baseline worktree: `C:\Users\Mike\Documents\AORebirth\tools-temp\dao-stack-baseline001`,
  detached at the same starting SHA.
- Integration checkpoint, tested SOURCE_SHA and final receipt SHA: pending.
- The task explicitly requires local-only commits. No push, merge back, deployment,
  production database modification or collision-fix import is authorized.

## Imported stack

The common ancestor is `cf1e12b894b1247b34f96f832b217c1cfb828213`.
Parent ancestry and `git cherry` verified the five commits below form one linear
stack and were absent from the starting cutover by patch equivalence. The stack
was imported once with a merge of its tip; its original commit identities remain
in the integration history.

| Source commit | Work |
| --- | --- |
| 3b58aa7e02636f99d63b1907c5b2bfbc5815f705 | Mission transaction hardening and isolated validation |
| 19f6122a0e19e17a1db017675b386a2506fc81cf | Mission rollback diagnostics and build acceptance |
| 522cbf3a618d859efce62562d7c9e227bdcb4309 | Mission contracts, provider boundary and start-area shim |
| e3acc4c58132809fd67bd2fe8aa58939109fe0dc | Account DAO contracts, implementation and disposable suite |
| a4f6be03b713b2e88421a1b9d51f318110af678f | Character directory/ownership/online DAO and disposable suite |

The older mission file is not accepted wholesale: current generated-mission and
inventory partial implementations, persisted fields and transaction ownership
must coexist with incoming hardening. Source inventories are reviewed as additive
changes, then checked with established generators.

## Acceptance evidence

Fresh baseline logs are under `build-verify/dao-stack-baseline` in the detached
baseline checkout. Fresh candidate logs will be under `build-verify/dao-stack`
in the integration checkout. Historical imported acceptance receipts retain their
original scope and are not fresh candidate test results.

### Foundation results, before consumer edits

The baseline Windows build and AOtomation suite (1,129 tests) passed. Initial
baseline and integration NewEngine runs each passed 512 and failed the same 11
building/teleport tests because these fresh worktrees lacked the ignored pinned
playfield package. Baseline connected startup independently rejected the missing
package. No assertions or runtime checks were relaxed. The documented package
export/import workflow supplied the existing accepted structural package, hash
`6b3c06c234923d07a8d476b710d650e620aafd0775602c2e38160af8bdc69b26`.
The packaged integration build and all 523 NewEngine tests passed.
The unchanged packaged baseline also passed all 523 tests, all 12 mandatory
stages and exact-source Windows acceptance at `827c7fb5` (baseline evidence:
`build-verify/dao-stack-baseline/windows-acceptance-packaged.log`).
Foundation checkpoint: PASS, recorded in the merge commit containing this
section, before any consumer source edits. Full connected acceptance continues
as a separate runtime gate.

Fresh disposable integration results (these are not the imported old receipts):

| Command | Results | Evidence under `build-verify/dao-stack/` |
| --- | --- | --- |
| `Tools\run_account_dao_validation.cmd` twice | 273 checks each, PASS; disposable cleanup PASS | `account-foundation-1.log`, `account-foundation-2.log` |
| `Tools\run_character_dao_validation.cmd` twice | 529 checks each, PASS; disposable cleanup PASS | `character-foundation-1.log`, `character-foundation-2.log` |
| `Tools\run_mission_dao_validation.cmd` | 261 checks, PASS | `mission-reconciliation-full.log` |
| `Tools\run_mission_dao_validation.cmd --isolated-sources` | 275 checks, PASS | `mission-reconciliation-isolated.log` |
| `NewZoneEngineBuild\build.cmd` | PASS | `foundation-build-packaged.log` |
| `dotnet test AORebirth\Server\ZoneEngine_New.Tests\ZoneEngine_New.Tests.csproj -c Debug --nologo -v:q` | 523/523 PASS | `foundation-newengine-tests-packaged.log` |
| `Tools\generate_newengine_cutover_inventory.cmd --check` | PASS | `foundation-cutover-inventory.log` |

Mission tests initially exposed fixture assumptions from the older branch:
missing durable owner and unseeded generated-offer allocator. The disposable
fixtures now seed their required rows and explicitly assert missing-owner
rejection. A startup-timeout receipt is retained separately. Commit-failure tests
retain durable-state assertions and expect the current cutover's unknown-outcome
exception; they do not assume a failed acknowledgement means rollback.

The mission resolution preserves current generated/inventory partials and all
frozen generated-offer fields, repeatable-read authored owner locking, generated
owner locking, and uncertain-commit semantics. It adds incoming connection/provider
validation, transaction poisoning/lifetime, rollback diagnostics, and known-rollback
DTO version restoration. Real MySQL failures cover generated transactions and
partially applied authored inventory grants. The default configured provider stays
in the Legacy constructor partial, preserving the Connector-free NewEngine lane.

Consumer mapping, focused regressions, lifecycle/persistence comparisons,
exact-source Windows acceptance, Linux publication and remaining risks will be
recorded after execution.

This milestone does not migrate character/stat/inventory saves, vendor or trade
systems, Account Broker/unified identity, or every remaining Legacy consumer.
