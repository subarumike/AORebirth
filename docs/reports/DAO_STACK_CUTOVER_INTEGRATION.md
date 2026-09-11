# DAO stack cutover integration

Status: integration and supported runtime wiring pass pre-commit acceptance.
Exact-source Windows acceptance and Linux publication are pending.

## Scope and provenance

- Integration branch: `codex/dao-stack-cutover-integration-001`.
- Integration worktree: `C:\Users\Mike\Documents\AORebirth\tools-temp\dao-stack-integration001`.
- Actual starting cutover SHA: `827c7fb50a9d860b6f671c73baebc2447c282dff`.
  It matches the reviewed SHA. Tracked cutover files were clean; existing untracked
  investigation artifacts were left untouched.
- Unmodified baseline worktree: `C:\Users\Mike\Documents\AORebirth\tools-temp\dao-stack-baseline001`,
  detached at the same starting SHA.
- Integration checkpoint: `a8daaa5e74eb3a260d42150da16d84d167d1d16e`.
- Consumer implementation commit: `b8e8d227bddc4af210e70a8ae14a92973e43e0b3`.
- Tested SOURCE_SHA and final receipt SHA: pending final source acceptance.
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

## Supported consumer mapping

| Runtime caller | Shared DAO operation | Preserved responsibility |
| --- | --- | --- |
| `LoginDataDao.GetByUsername` | `IAccountDao.LoadByUsername` | Existing full account DTO, null and authentication policy |
| `LoginDataDao.GetByCharacterId` | `LoadByCharacterId` | Character-to-account resolution and missing-row behavior |
| `LoginDataDao.GetRegisteredCount` / Login startup connectivity | `CountRegisteredAccounts` | Connectivity check, not policy or migration |
| `LoginDataDao.Exists` / Login account availability | `UsernameExists` | Existing availability decision |
| `LoginDataDao.WriteLoginData` | `CreateGameAccount` | Existing password bytes; log and rethrow on failure |
| `LoginDataDao.WriteNewPassword` | `ChangePassword` | Existing password handling; log and return zero on failure |
| `LoginDataDao.SetExpansions` | `SetExpansions` | Existing log-and-swallow behavior |
| `CharacterDao.GetCharacterNameById` | `ICharacterDao.LoadById` | Missing/null name remains empty |
| `CharacterDao.ExistsByName` | `LoadByName` | Existing existence semantics |
| `CharacterDao.IsCharacterOnAccount` | `IsOwnedByAccount` | Exact pair check; no handoff authorization shortcut |
| Login `CharacterList.LoadCharacters` | `ListForAccount` | Directory ID/name/playfield; existing separate stat reads |
| Chat `AccountCharacterList` | `ListForAccount` | Existing per-character stat and online policy |
| Chat `PlayerNameLookup.Read` | `LoadByName` | Existing missing-name behavior |
| Chat `LoginCharacter.Read` / `CharacterBase` | `LoadById` | Names only; no partial gameplay character |
| Chat `LftSearch` | `LoadById` | Persisted playfield lookup only |
| `CharacterDao.IsOnline` | `LoadById` | Missing/null online value remains zero |
| `CharacterDao.SetOnline` / `SetOffline` | `MarkOnline` / `MarkOffline` | Existing caller contracts and ownership callbacks |
| `LoginDataDao.LogoffChars` console command | `ListForAccount` and guarded `MarkOffline` | Directory-only enumeration; active zone/bot leases prevent administrative offline cleanup |
| NewEngine `MySqlCharacterRepository.SetOnline` / `SetOffline` | `MarkOnline` / `MarkOffline` | Missing/affected-row failure stays fatal; ownership guards retained |
| NewEngine `ZoneAdmissionGate.Claim` | `LoadById` for current account owner | Existing ticket, endpoint, expiry and replay checks before full hydration |
| Legacy administrative `StaleOnlineRecovery.Execute` | `RecoverStaleOnline` | Exact database/port guards, LoginEngine and ZoneEngine process refusal, captured-character ownership fencing |

Every migrated operation has one DAO persistence path. There is no direct-SQL
fallback. Internal account adapter overloads exist solely to exercise injected
DAO failures without bypassing the configured-provider guard.

Full `CharacterDao.Get`, `GetByCharName`, `GetAllForUser`, and NewEngine
`MySqlCharacterRepository.GetById` remain complete-character loaders. They are
not replaced by partial directory DTOs. WebEngine's full logged-in-character
listing remains outside this task's explicit website boundary. `SetGM` is not
part of the imported account interface. Mission-owned account resolution remains
inside mission transaction policy. Existing aggregate snapshot/stat/item writes
and their online column updates remain part of the unchanged saving boundary.

Chat session registration/disconnect now serialize ownership checks and writes.
A delayed callback cannot remove its replacement or clear that replacement's
online flag. A retired connection cannot register late. Current Chat cleanup
also honors the existing cross-process ZoneEngine ownership guard. Registered
bots additionally retain that existing ownership lease for their session lifetime,
so an administrative recovery process cannot mistake an active bot for a stale
character. These rules do not change credential validation.

LoginEngine selection also marks a character online before sending ZoneInfo,
while its handoff is pending. Administrative stale recovery now refuses to open
the database while LoginEngine is running, even if there are currently zero
online rows. This deliberately requires stopping LoginEngine before that
maintenance operation; it avoids clearing a valid pending handoff without
inventing a new handoff protocol. As before, maintenance requires exclusive
startup/process authority: do not start LoginEngine during recovery. World and
bot sessions are additionally protected by per-character ownership leases.

## Fresh baseline lifecycle failures and fixture corrections

The unchanged baseline's connected gate failed before zone readiness because its
disposable bootstrap omitted the canonical `teleports` table now required by
cutover routing. The separate schema/restart gate failed
`cutover-fresh-character-reload`: its two-stat character seed did not meet the
current 26-stat spawn validation. Both fixtures were older than the cutover
requirements. All preceding schema/transaction gates passed, and owned fixture
cleanup passed. Evidence is in baseline `connected-acceptance-packaged.log` and
`schema-acceptance.log`; `connected-acceptance.log` records the earlier missing
package failure. All are under `build-verify/dao-stack-baseline/` in the baseline
worktree.

The candidate uses the existing canonical teleports table definition only in its
disposable bootstrap and valid spawn-stat seeds. Runtime hydration validation,
schema definitions and production databases are unchanged. Connected comparisons
now cover complete character rows, every stat row and every seeded item column,
including an equipment row. The only planned persistence differences are online
transitions and the explicit existing inventory move. Final results follow after
execution; these fixture repairs alone are not lifecycle acceptance.

### Connected compatibility diagnosis

The corrected fixture exposed a candidate regression before character selection:
Windows LoginEngine's Dapper 1.13 throws `DataException` while reading directory
column 6, `Online`, from the existing SMALLINT column into `int?`. Foundation
tests run with newer Dapper, so they did not expose this older-reader conversion.
The same corrected fixture against unchanged `827c7fb5` binaries authenticated,
entered the zone, reloaded equipment and moved the item successfully. Temporary
non-secret stage/column diagnostics localized the failure; they are removed from
the final implementation. The fix explicitly converts the raw online value in
the DAO while preserving the existing nullable DTO, SQL and schema.
Malformed or overflowing provider values still surface as `DataException`.
The existing malformed-reader assertion was retained, and all 540 character
checks passed after this compatibility repair. Two simultaneous invocations of
the fixed-name disposable character fixture collided during setup; neither ran
assertions. Those failure logs are retained and subsequent runs are serialized.

The baseline differential also proved that first logout materializes eight
existing default/derived stat rows. The fixture now seeds those exact measured
values (stat IDs 180, 213, 350, 521, 57, 585, 587 and 6), bringing its snapshot to
40 stat rows. Comparisons still permit no stat changes or extra rows; there is
no general drift allowance. Evidence:
`build-verify/dao-stack/baseline-binaries-corrected-fixture-connected.log` and
`connected-lifecycle-column-diagnostic.log`.

## Runtime checkpoint before SOURCE_SHA

| Gate | Fresh result | Evidence |
| --- | --- | --- |
| Normal Windows build | PASS | `build-verify/dao-stack/final-compatible-windows-build.log` |
| NewEngine build | PASS | `build-verify/dao-stack/final-compatible-newengine-build.log` |
| NewEngine regressions | 530/530 PASS, zero skipped | `build-verify/dao-stack/consumer-all-newengine-tests.log` |
| Account consumers | 297 checks PASS, cleanup PASS | `tools-temp/account-consumer-validation.log` |
| Character consumers/recovery, twice | 540 checks each PASS, cleanup PASS | `build-verify/dao-stack/character-final-1.log`, `character-final-2.log` |
| Schema/transaction/restart, final fixture | PASS | `build-verify/dao-stack/schema-lifecycle-final.log` |
| Connected final candidate | PASS | `build-verify/dao-stack/connected-lifecycle-final.log` |
| Full source inventory | PASS | `build-verify/dao-stack/consumer-source-inventory-check.log` |
| Cutover inventories write/check | PASS | `build-verify/dao-stack/consumer-cutover-inventory-write.log`, `consumer-cutover-inventory-check.log` |

Connected checks compare every character column, all 40 stat rows, and all four
seeded item rows across login, inventory move, logout, fresh authentication,
concurrent handoff claims, clean stop, process restart, morph cancellation and
final stop. Post-move offline snapshots are identical after every subsequent
logout/restart. Wrong password, wrong owner, forged/expired/consumed handoffs and
replays remain rejected. Equipment evidence proves seeded storage and wire
reload, not player-driven equip legality. Fresh-authentication reconnect does
not establish arbitrary TCP-loss recovery. Schema/repository checks separately
preserve 40 stat rows and two item rows across two process restarts.

The 93 Legacy dependency edges remain unchanged. The generated DAO inventory has
100 syntactic method rows, not 100 remaining migrations. It now recognizes
`SetOnlineState` as an `ICharacterDao` consumer. Its conservative constructor
classification also counts deferred MySQL connection creation as SQL access;
that is not an executed query or a direct-SQL fallback.

## Candidate build and launch paths

From the integration worktree, existing entry points are
`Tools\build_aorebirth_debug.cmd`, `NewZoneEngineBuild\build.cmd`, and
`restart-engines.cmd`. The managed restart wrapper uses configured database and
service settings; it was not invoked by this task. Tests launched only their
owned disposable engines through `Tools\run_newengine_connected_acceptance.cmd`
and `Tools\run_zoneengine_schema_validation.cmd`.

Candidate binaries:
`AORebirth\Built\Debug\LoginEngine.exe`,
`AORebirth\Built\Debug\ChatEngine.exe`, and
`AORebirth\Built\Debug\ZoneEngine_New\ZoneEngine_New.dll`.

## Remaining scope

Character aggregate/stat/inventory writes remain the next implementation
milestone; preserving their existing state is not a DAO conversion claim. Vendor
buying/selling, player trade implementation, website/Broker ownership, collision
commit `5d663b7a`, deployment and arbitrary transport-loss recovery are outside
this delivery. Linux-host runtime acceptance must remain NOT RUN unless executed
through its established workflow; Windows publication alone cannot supply it.

The existing `LoginDataDao.SetGM` helper is outside `IAccountDao` and remains
unchanged. Its SQL updates `login.GM` without a username predicate, despite
accepting a username argument (`LoginDataDao.cs`, original line 119). The console
command therefore remains a separate unsafe administrative path requiring a
scoped repair before use; this integration does not claim that command is safe.

## Exact-source acceptance history

Before the final source commit, the ownership review additionally repaired bot
lifetime leases, repeated Chat character selection and guarded account console
logoff. The full NewEngine suite passes 534 tests, including eight Chat ownership
tests (`newengine-bot-final.log`). The Account suite passes 303 checks, including
real-database guarded account logoff and propagated provider failures
(`account-logoff-ownership-validation.log`), with fixture cleanup PASS. The final
normal Windows build and rebuilt Stage 5 contracts pass
(`ownership-windows-build.log`, `stage5-final-ownership-verify.log`). Cutover
inventory regeneration/check retains 93 edges and 100 scan rows without another
generated JSON delta. These logs are under `build-verify/dao-stack/`.

The first exact-source attempt at `b8e8d227bddc4af210e70a8ae14a92973e43e0b3`
verified the SHA, clean entry tree and normal Windows build, then failed public
contract comparison because the checked-in manifest predated the imported
account/character types. The mandatory suite was not reached. This failure is
retained at `build-verify/dao-stack/windows-exact-source.log`. The affected
manifest is regenerated through the existing workflow and reviewed before a
new SOURCE_SHA is committed; comparisons are not relaxed.
The generated delta adds 219 lines for six account types and four character
types, with no removals or changes to existing API declarations. Legacy public
contract verification and Linux compatibility smoke pass. Evidence:
`build-verify/dao-stack/stage2-manifest-write.log` and
`build-verify/dao-stack/stage2-manifest-verify.log`.

The second attempt at `ef3ac3dfb91cb851731b042087cf40619b1ec62c`
passed the same SHA/build checks and Stage 2, then failed Stage 3's older DAO
contract manifest. The mandatory suite was again not reached. Its receipt is
`build-verify/dao-stack/windows-exact-source-final.log`; the filename does not
indicate acceptance. Stage 3 regeneration adds 37 lines for the DAO factories,
implementations and recovery ownership API. Stage 5 regeneration records the
readonly Chat client view, its Interfaces dependency and the generator's
synthetic authentication test vector. Correct/wrong-password validation remains
unchanged. Stages 2, 3, 4, 5 and 7 now pass their comparisons; Stages 4 and 7
needed no manifest change. Receipts are
`build-verify/dao-stack/stage{2,3,4,5,7}-manifest-verify.log`.

The third source attempt at `039b3dc769a6d3090c6ef08451e7352437df0993`
passed clean-source, Windows build and cross-platform contract validation, then
failed the first mandatory gate: the secret scanner rejected an unused synthetic
password field in an offline test connection string. Its failure is preserved at
`build-verify/dao-stack/windows-accepted-source.log`; no password was needed or
used to connect. The unnecessary field was removed, with the three focused
character consumer tests passing in `character-consumer-fixture-final.log`.
The scanner and its policy were not changed.

This milestone does not migrate character/stat/inventory saves, vendor or trade
systems, Account Broker/unified identity, or every remaining Legacy consumer.
