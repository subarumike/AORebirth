# Connected Windows mission acceptance follow-up

Date: 2026-09-26 local (final run completed 2026-09-27 UTC).
Branch: `codex/mission-typed-content`; public base
`3c904f760513af8110b78e353c09a9296a0d76a9`.

## Root cause

**PROVEN:** Docker Desktop startup was blocked by its own inaccessible runtime
endpoint `%LOCALAPPDATA%\Docker\run\dockerInference`. It was a zero-byte
reparse-point entry; both reparse-point and ACL queries failed with Windows
error 1920. The failure happened before the disposable fixture could contact
Docker. Docker settings JSON was valid, the volume reported healthy, and no
invalid fixture volume/path or inference configuration was established.
This is Docker Desktop runtime state, not a mission candidate defect.

**INFERRED, not established locally:** a Windows/Docker socket interoperability
issue may explain why this entry became inaccessible. Similar reports are in
[Docker desktop-feedback #448](https://github.com/docker/desktop-feedback/issues/448)
and [#527](https://github.com/docker/desktop-feedback/issues/527).
No kernel corruption, bad shutdown or exact OS update is claimed as proven.

After `docker desktop stop --timeout 30`, all processes rooted in the Docker
installation directory were confirmed absent. The normal parent `run` directory
was renamed to `run.preserved-mission-acceptance-20260926`. Its three socket
entries remain preserved. Docker Desktop was then started hidden and recreated
its runtime directory. Server version `29.5.3` answered successfully; disposable
MySQL creation, migration, connected execution and cleanup subsequently worked.
No Docker reinstall/reset, settings change, image/volume deletion or machine-wide
path change was performed.

Two independent fixture defects were exposed after Docker recovered:

- `Tools/ZoneEngineSchemaValidation/Program.cs:305`: migration smoke launched a
  copied migration assembly using incompatible combined fixture dependencies.
  It now launches the separately built DatabaseMigrationTool package. The old
  launcher failed resolving Logging.Abstractions 8.0.2 on unchanged public code.
- `Tools/ZoneEngineSchemaValidation/Program.cs:138`: disposable baseline omitted
  three shop tables required by full world login. It now reads their existing
  canonical CREATE TABLE statements into the disposable database before startup.
  Production schema files and production databases were not changed.

The broad connected test then failed `connected-persisted-active-nano-exact-expiry`.
**PROVEN baseline:** the identical assertion failed on unchanged public runtime
at the base SHA, with legacy format-1 public mission data and the same fixture
packaging/schema repairs. No assertion was removed or relaxed. The added
mission-only scenario is separately selected and does not claim the broader
nano/inventory test passes.

## Files changed in this follow-up

- `Tools/ZoneEngineSchemaValidation/Program.cs`: fixture package/schema repairs
  and dispatch of the explicit mission matrix.
- `Tools/ZoneEngineSchemaValidation/ConnectedMissionMatrix.cs`: authenticated
  connected mission lifecycle scenario with pre-start disposable account setup.
- `Tools/ZoneEngineSchemaValidation/ConnectedAcceptanceSmoke.cs`: existing
  authentication helper exposed internally for reuse; original assertions intact.
- `Tools/ZoneEngineSchemaValidation/ConnectedWireClient.cs`: independent outgoing
  compressed sequence verification and narrowly matched, fully verified opaque
  corpse packets. Unknown packets still use the normal decoder and fail normally.
- `Tools/run_newengine_connected_acceptance.cmd`: optional `--mission-matrix`
  suffix for the existing explicit normalized GameData mode.
- `docs/ai/WORKFLOW.md`, `docs/ai/CURRENT_TASK.md`, this report, the original
  report's follow-up link and `connected-validation.json`.

**PROVEN:** hashes of all 2,332 protected candidate files under `AORebirth/`
and `Tests/Fixtures/` match the pre-follow-up manifest. No completed runtime
implementation or production test assertion was changed in this phase.

## Validation performed

### Connected matrix: PASS

Actual LoginEngine and ZoneEngine_New processes connected to a disposable MySQL
container. Fixture account/stats/schema setup completed before engine startup.
Subsequent mutations were authenticated TCP protocol actions; SQL was read-only.

| Required stage | Result and observed boundary |
|---|---|
| Full startup | Both engine processes reached ready state |
| Login | Real password challenge, character selection and zone handoff |
| Roll and offer serialization | Five network offers; unique identities and durable serialized offers |
| Acceptance | Network request created accepted binding matching offered identity |
| Entry | Physical mission key and normal entrance Use; redirect and new zone connection |
| NPC creation | Materialized durable object count and received NPC spawn |
| Appearance | Scale, head, textures and content meshes matched typed appearance |
| Weapon behavior | NPC weapon message and NPC attack against the player |
| Static objects | Door and chest messages received in the accepted mission |
| Combat/death | Player attacked NPC; durable NPC health/death state updated |
| Corpse projection | Exact typed projection bytes, independently verified transport sequence |
| Logout | Logout protocol and durable Online=0 |
| Reconnect/restoration | Real login again; accepted quest, same bundle/hash/live PF and dead NPC restored |

The successful run selected `FindPerson`, bundle
`capture-20260728-012547-00D734E7-0016700C-d5413273f69b018b`, durable hash
`d5413273f69b018b66fcd6fe31bfa7be15b338cb6cb8fd17d83f7e14c4e4be82`.
That retained bundle name is a durable content identity, not a filesystem read.
The connected corpse was 430 bytes and was verified again after reconnect.

An earlier fixture mismatch was isolated to offsets 0 and 1: expected `DF DF`,
actual compressed sequence `00 62`. The other 421 bytes matched exactly.
`ZoneSession.cs:759-777` starts the sequence at 1 and stamps those bytes on every
compressed outbound packet. The fixture now independently checks every sequence
number and then compares the complete expected corpse packet. It does not mask
body differences. The generic CFU decoder's inability to parse the established
corpse name/material tail was a fixture decoding limitation, not a runtime change.

Travel to the exterior entrance used the existing GM teleport command; proximity
to the NPC used an ordinary movement packet. Synthetic fixture stats make combat
bounded. These checks do not claim client rendering, navigation/pathfinding,
every mission subtype, every weapon family, or mission objective completion.
Mike's AO client was not launched or controlled.

### Historical input boundary

The full run selected only the isolated root:
`C:\Users\Mike\.codex\worktrees\mission-typed-content\AORebirth-fresh\build-verify\mission-runtime\GameData`.

**PROVEN:** `RollBodies.json`, `RollTemplate.json` and `Provenance.json` are absent
throughout that isolated tree. `MissionOffers.json` and `Layouts.json` contain
none of the historical packet field aliases, checked recursively and by the
fixture's fail-closed pre-start guard. Exact normalized file hashes are in
`connected-validation.json`. The fixture clears inherited AO_REBIRTH settings
and supplies the existing AO_REBIRTH_GAMEDATA_PATH configuration explicitly.

**PROVEN source/component evidence:** the production mission/NPC paths have no
RollTemplate, RawPacketHex, PacketHex, AORebirthCaptures, AOSharpLiveCapture or
capture-root.path references. The existing production-source guard and typed
normalization equivalence tests passed in the full NewEngine suite. No capture
location fallback was added. Freshly persisted offer serialization and the
permanent Corpse.json serialization template remain legitimate runtime inputs;
this phase did not broaden the five-path remediation scope.

**UNVERIFIED OS access claim:** historical capture directories elsewhere on the
host remained intact and accessible. `wpr -start FileIO -filemode` was denied
with `0x80070005`; no trace ran. Therefore this report does not claim an observed
zero count of historical-directory opens or an OS-enforced denial of those
locations. The successful input-absence run and source guards support packet
independence, but do not substitute for that stronger filesystem-access proof.
Neither private GameData nor `D:\AORebirthCaptures` was modified.

### Windows gates

- Governed Windows build: **PASS**.
- NewEngine suite: 867 passed / 2 failed / 869 total. Exactly the established
  baseline `Pf2064_return_exit_matches_the_legacy_statel_collision_envelope` and
  `Pf127_AbmouthReachesThePlayerStandingOnTheFloorAbove` failures.
- AOtomation messaging suite: 632 passed / 2 failed / 634 total. Exactly the
  established baseline `CurrentConsumerAuditSeparatesHistoricalEvidenceFromRuntimeActivation`
  and `CellHeatSchedulingIsFailSafeAndExplicitlyOptIn` failures.
- Secret scan and `git diff --check`: **PASS**.
- `Tools/accept_windows_source.cmd --expected-sha 3c904f760513af8110b78e353c09a9296a0d76a9`:
  refused with `TRACKED_SOURCE_CLEAN=FAIL`, as required for the uncommitted candidate.
  No guard bypass or false exact-SHA acceptance receipt was created.

The legacy messaging wrapper ran in the existing same-volume validation clone,
with all candidate changed/new files copied and SHA-256 verified first. The
original managed-worktree generated-artifact lease was not bypassed.
The four established suite failures and additional broad connected baseline
failure were not repaired. No candidate-specific failure was found.

Tested and rebuilt engine hashes remained identical:

- ZoneEngine_New.dll: `0edccb79623ae03ae55041b033e27328b39d090fad4d3f405184cc2391de67b1`
- LoginEngine.exe: `095f69449814e6b375d4b337f3bce9390e6ef333899b4b45a20901a668d9d618`

## Commit and remaining risks

No commit, push, merge, Linux deployment or production modification occurred.
The scoped connected matrix is ready for review; **full acceptance clearance is
not claimed** because OS-level historical-directory access proof is still
unavailable, exact-SHA acceptance requires a later authorized committed clean
candidate, and the established baseline gates remain red. Under a requirement
for all requested proof to be complete, the candidate is not yet fully cleared
for commit. There is no evidence here requiring a runtime candidate repair.

Disposable container and network cleanup passed. The approved stop-engines
wrapper confirmed all governed engine processes absent and ports clear before
final handoff. WPR confirmed no recording active. Docker remains operational.

## Evidence locations

In this candidate checkout under ignored `build-verify/connected-mission-followup/`:
`mission-matrix-sequence.log`, `windows-build.log`, `normalized-tests.log`,
`aotomation-tests.log`, `secret-scan.log`, `windows-source-acceptance.log`,
`candidate-source-before.json`, `candidate-source-after.json`, `clone-sync.json`,
and the narrowly captured corpse diagnostic directory.
Baseline connected receipt:
`C:\Users\Mike\.codex\worktrees\mission-validation-baseline\AORebirth-fresh\build-verify\connected-baseline\acceptance.log:360`.
The checked-in-ready summary receipt is `connected-validation.json`; raw logs
and disposable diagnostic data are not being promoted to public content.
