# NewEngine live character login repair

Application repair commit: `e323958180ee3a0bacbe3ad37cf50818e01da27e`.
Final release source: `517f579400b1901db2b4968d5c5393376791803e`.
The latter changes only the Linux smoke test's expected deployment-suite count.
Both commits are pushed on `codex/bb5dd916-production-cutover` and included in
local master. Remote master is unchanged.

## Root cause

Production LoginEngine authenticated and handed off characters 34, 39 and 49.
NewEngine rejected them before world publication. Its hydration validator treated
Legacy's sparse saved stats as incomplete and compared current nano against the
saved breed-only nano base. Legacy intentionally omits default-valued stat rows;
its effective vital calculations also include ability trickle and a GM health
override. This was a character-loading compatibility failure.

## Files changed

- `AORebirth/Server/ZoneEngine_New/Core/Characters/CharacterHydrationService.cs`:
  project only missing Race=1, VisualFlags=31, Side=0 and RunSpeed=5 from proven
  Legacy defaults; preserve explicit values, duplicates and invalid sentinels.
- `Core/Characters/CharacterHydrationValidator.cs`,
  `Core/Characters/PlayerSpawnPayloadValidator.cs` and `Core/Playfield/SpawnService.cs`
  beneath the same NewEngine directory: retain strict raw identity/value checks,
  and enforce effective vital bounds after equipment and active-nano hydration,
  before player registration and packet publication.
- `Core/Helpers/VitalSkillTrickle.cs`, `MaxHealthCalculator.cs`,
  `MaxNanoCalculator.cs` and `Core/Nanos/NanoDerivedStats.cs`: restore canonical
  BodyDevelopment/NanoPool ability trickle and the existing player GM health rule.
- `AORebirth/Server/ZoneEngine_New.Tests/CharacterSpawnHydrationTests.cs`,
  `Fixtures/LegacyProductionCharacterStats.json` and `ZoneEngine_New.Tests.csproj`:
  embed the three production stat snapshots and verify stable derived vitals,
  preserved raw values, valid spawn packets and fail-closed invalid inputs.
- `LinuxBuild/Tools/Stage8OfflineSmokeTests/ProductionDeploymentWorkflowContractTests.cs`:
  align its stale 56-case assertion with the already implemented 64-case suite.
- `docs/ai/CURRENT_TASK.md`, `docs/project/PROJECT_STATE.md` and this receipt:
  record repair and release state.

## Validation performed

| Production snapshot | Effective maximum health | Effective maximum nano | Spawn validation |
| --- | ---: | ---: | --- |
| 34 | 2,000,000,000 | 20 | PASS |
| 39 | 65 | 126 | PASS |
| 49 | 2,000,000,000 | 36 | PASS |

The fixtures retain saved current vitals and stat values. No SQL update, schema
change, character reseed, inventory rewrite or credential change is part of this
repair.

- Exact Windows acceptance at `517f5794`: PASS, 549 NewEngine tests.
- Full Windows mandatory acceptance at application repair `e3239581`: PASS.
  The subsequent commit changes only the Linux deployment smoke assertion.
- Exact Linux NewEngine tests at `517f5794`: PASS, 549 tests.
- Final Linux acceptance: PASS, including all 64 deployment regression cases,
  package validation, placement parity and exact-source provenance.
- Production dry-run, transaction, runtime hashes/readiness and stability: PASS.

The first Linux run needed Git LFS added to the disposable build container. The
next run exposed the stale deployment-suite assertion; it was fixed in committed
source before restarting exact acceptance. Neither failed attempt was deployed.
The primary checkout's earlier 11 Playfields-input failures do not occur in the
fully supplied controlled acceptance checkout.

## Production result and remaining risks

Deployed on 2026-09-13 UTC. LoginEngine and ZoneEngine_New both run
`release-517f579400b1901db2b4968d5c5393376791803e`, with matching accepted apphost
and assembly hashes, active readiness and zero restarts. The existing ChatEngine
and account broker remain healthy. Public Login/Zone/Chat listeners and private
Chat listener match the governed configuration.

Before and after deployment: 28 characters, 254 legacy items, 26 legacy
instanced items and 280 unified inventory rows. There were zero online characters
at the deployment boundary and verification. The migration ledger still contains
only the four cutover migrations. No schema or operator data changes were made.

The normal NewEngine-to-NewEngine transaction retained the previous `bb5dd916`
release pair and units in
`/opt/ao-rebirth/deployment-snapshots/release-517f579400b1901db2b4968d5c5393376791803e-20260913T010043Z-2469934`.
Rollback was not required. The earlier verified pre-cutover database backup and
restore evidence remain retained; no old backup was restored for this repair.

Transferred archive SHA-256:
`071229368e16648aa5cd951cb5070d46033e70ed2a9ac7fe38e3e0734fc80b84`.
The retained local evidence index is
`build-verify/live-e3239581/evidence-index.json`, SHA-256
`193ea127b0a072de8cbe8d4ef33a4943bd702a4db15c6934632d138a2f194857`.
Remote manifest and deployment logs are retained under
`/srv/aorebirth-release-517f5794-20260913`.

Official-client world entry on the repaired live build must be confirmed by Mike;
the agent does not launch or control the client. This repair introduces no schema
or persistence-format migration. A return to Legacy still requires the governed
database restore/reconciliation path after NewEngine writes.

## Files and evidence inspected

Repository startup/governance and release documentation; Legacy `Stats.cs`,
`StatNamesDefaults.cs`, `SpecialStats/StatSkill.cs`, `SpecialStats/StatLife.cs`,
`SpecialStats/StatMaxNanoEnergy.cs` and `SkillTrickleTable.cs`; the NewEngine
hydration, spawn, vital-calculation and nano paths above; Linux deployment
contracts, manifest and fixture suite; production service journals and read-only
stat/inventory queries. Private operational evidence is retained under ignored
`build-verify/live-bb5dd916` and `build-verify/live-e3239581`.
