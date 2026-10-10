# Mission-roll destination eligibility repair

Date: 2026-10-09. Branch: `codex/mission-destination-integration`.
Baseline: `30ab16d0de180d48f2323ede4811602b0e21ab7c`.

## Root cause

PROVEN: destination generation used an atomic dictionary lookup over the entire
capture observation tuple: character level, static expected QL, difficulty,
faction, breed, profession, terminal playfield, terminal type/instance, six raw
secondary slider bytes and mission type. This implemented exact capture coverage
as a mandatory gameplay eligibility rule. The original evidence does not prove
independent destination exclusions for those other fields.

The reported character is level25, Neutral (side0), breed2, profession15, rolling
in Borealis (PF800). A read-only local character lookup and server log agree on
level/profession. Every captured faction is Omni (side2); only breeds1/3 were
sampled. Every PF800 condition is level35/breed3/profession12. Consequently, the
old lookup rejects this character for every valid difficulty, regardless of the
unrecorded secondary sliders or terminal instance. This is a proven metadata
mismatch, not proof that retail excludes those destinations.

There is a second, independently reproduced integration defect even when all
metadata matches: the preserved type selector chooses among older level60/QL42
neutral cohorts, all containing Find Item. Every captured centered request's
destination sets contain only Kill Person, Repair and Return Item. The old
type-conditioned destination lookup therefore rejects every centered captured
request. The original passing tests used a different slider vector and did not
exercise this case or the live handler's charging boundary.

## Baseline reproduction and request evidence

Before production edits, a new regression used this exact captured request:

| Field | Value |
| --- | --- |
| Character level / expected QL | 35 / 35 |
| Difficulty detent | 6 |
| Faction / breed / profession | 2 / 3 / 12 |
| Terminal | PF800, `0xDAC1:0xC0000320` |
| Secondary raw bytes | `[255,255,255,255,255,255]` |
| Per-type destination counts | Kill1, Find Person0, Find Item0, Repair3, Return Item1 |
| Result at baseline | `NotSupportedException`, `GeneratedMissionRollService.cs:48` |

The baseline focused run was 40 PASS / 1 FAIL. Its new assertion required a valid
five-offer cohort; the failure was recorded before runtime changes.

OBSERVED: the original local server received three QuestAlternative requests at
21:49:34, 21:49:44 and 21:49:46 on October9. That version logs only the message
name, not payload fields. Original difficulty, requested expected QL, exact
terminal instance/coordinates and secondary slider bytes are therefore UNKNOWN.
The character's persisted position is not substituted for terminal coordinates.
Centered sliders in the new character-profile regression are explicitly a
reproduction assumption, not a recovered original request.

All eleven possible level25 difficulty detents have positive QL observations:

| Detent | Expected QL | Distinct observed destinations |
| --- | --- | --- |
| 1 | 17 | 115 |
| 2 | 18 | 121 |
| 3 | 20 | 121 |
| 4 | 21 | 121 |
| 5 | 22 | 125 |
| 6 | 25 | 124 |
| 7 | 27 | 244 |
| 8 | 30 | 227 |
| 9 | 32 | 195 |
| 10 | 37 | 227 |
| 11 | 44 | 209 |

For the possible centered QL25 request, an explanatory old-policy intersection
is `2242 physical -> 812 observed -> 124 QL25 -> 121 level25 -> 0 Neutral`.
Within that QL, Neutral, breed2, profession15 and PF800 each independently have
zero captured observations. These diagnostic intersections are not proof of
exclusion and are not the runtime order: the old lookup was atomic.

The corrected path is `2242 physical -> 812 observed with validated WorldPos ->
exact requested QL pool -> five selections with replacement`. Other metadata
does not reduce the destination pool. Centered QL25 has124 candidates; the exact
captured QL35 reproduction has184. Unsupported QLs have zero candidates.

## Evidence reviewed and policy correction

The complete pinned public evidence at
`f07bb3c1a99218433e7d459c95ba29ccb22c36b2` was inspected, including all 93,185 offer
audit rows: 92,830 raw-backed observations, 355 missing-raw exclusions and 812 exact
destination identities. The compact selection retains 180 complete request
conditions, 547 condition/type sets and 45 observed expected QLs.

Relevant original conclusions are in
`docs/evidence/MISSION_DESTINATION_ELIGIBILITY_FROM_RESOLVED_CAPTURE_CORPUS.md`
at that pinned commit: lines9-15 distinguish positive observations from missing
coverage;136-165 explain static expected QL and unresolved level effects;
167-185 establish absent faction/terminal controls;189-211 leave type/slider
restrictions unproven;269-282 identify missing controls. The foundation and
duplicate-audit reports, raw WorldPos records and complete eligibility inventory
were also reviewed. No available raw-backed population was discarded.

Mike explicitly approved provisional reuse of every exact entrance positively
observed at the requested expected QL. The catalog now builds that deduplicated,
immutable union from its validated condition sets. The generator queries it
once and uses the same pool for all five offers. It does not use observation
frequency as probability, enforce cohort uniqueness, choose neighboring QLs or
choose unobserved placements. The four JSON files and their provenance are
unchanged, including the original full-condition research lookup.

This is a provisional gameplay policy, not proof of original AO cross-condition
eligibility. The request logger states `OBSERVED_EXPECTED_QL_REUSE` and
`crossConditionEligibility=UNPROVEN`, records level/expected QL/faction/breed/
profession, actual terminal identity/PF/XYZ, difficulty and all raw sliders,
and records physical, observed, valid-WorldPos and expected-QL candidate counts.
Unsupported feedback now identifies the uncovered expected QL explicitly.

Historical mapping limitation: level13/detent11 currently maps to expectedQL23,
but ten old raw-backed observations were labeled static expectedQL22 and 1,245
were labeled23. Neither label was decoded from a live response QL. Both original
labels/provenance remain intact; no table or observation was silently relabeled.
This discrepancy does not cause the reported level25 failure.

## Preserved pipeline and validation

Session/character ownership, persistence quarantine, registered terminal type,
interaction range, synchronized clock, existing terminal side-access policy,
and valid difficulty/secondary wire settings still run before generation.
Character level still computes expected QL and existing fee/reward/type behavior;
it no longer independently filters destination membership.

Generated offers retain full ACGEntrance identity and bit-exact WorldPos.
Acceptance, persisted quest parameters, keys, dungeon construction/entry/exit
and DAO/schema code are unchanged by this repair. Five durable IDs are still
reserved before generation; an unsupported request may consume that reservation.
It does not publish offers or reach the transactional fee deduction.

| Validation | Result |
| --- | --- |
| Baseline exact captured centered request | Reproduced failure before runtime edits: 40 PASS / 1 FAIL. |
| Approved Windows runtime build | PASS. |
| Destination, acceptance/key/dungeon, handler/fee and QL regressions | PASS 61/61; zero skips. |
| All 45 represented expected QLs | Each generates five exact destinations with altered observational metadata. |
| Unsupported QL and rejected/throwing publication | No new offers or cash deduction; runtime handler/service exercised. |
| Destination repeats | Permitted; deterministic seed9 demonstrates a repeated identity. |
| Real disposable-MySQL high-bit identity validation | PASS 74 checks, including 0xC00001F9 and other positive-only contracts. |
| Full disposable DAO regression | PASS 275 checks, including fee transactions, rollback and concurrency, with the compiler override described below. |
| Live original AO client | Pending local rebuild and manual retry. |

The default full DAO wrapper initially failed with four CS8370 diagnostics:
its C#7.3 setting cannot compile current nullable source. A task-local invocation
uses the same `IsolatedMissionSources=true` and `LangVersion=latest` overrides
already used by the approved identity route. No production or tracked test-tool
code was changed to bypass that compile failure.

The previously recorded six CS0246 errors in the full ZoneEngine_New test project
remain outside this repair. The focused selection adds the existing mission QL
graph tests. An attempted optional login-test inclusion exposed two stale
`ApplyMissionLoginPlan` references; that optional addition was removed, with
the stale test file/runtime untouched. Existing dungeon tests already cover
stored-identity login restoration.

Receipts are ignored under `build-verify/mission-roll-eligibility-*`; the private
investigation JSON records complete known-factor funnels, supported QL counts,
source hashes and unknown original fields. Original local database inspection
used guarded SELECT-only queries; credentials were not printed or copied.
Read-only investigation command errors involved a glob passed as an rg path,
a PowerShell foreach pipeline and an overquoted Python selector. They were
corrected with explicit file reads and the task script invoked as
`%AO_REBIRTH_PYTHON% build-verify\mission-roll-eligibility-investigation.py`.
The failed commands changed no runtime files or database rows.

## Files changed and remaining limits

Runtime changes are limited to `MissionDestinationCatalog.cs` (QL union query),
`GeneratedMissionRollService.cs` (QL-only selection) and
`QuestAlternativeMessageHandler.cs` (request/count diagnostics and QL feedback).
Tests change `MissionDestinationIntegrationTests.cs` and the focused compile list
in `ZoneEngine_New.Tests.csproj`. Task/project notes and this report describe the
new approved boundary; the original integration report remains historical.

There is no new provider, schema, DAO change, generated content, probability
model, old 140-row pool, capture-directory runtime dependency, objective/reward
repair or Linux deployment. Unobserved QLs fail closed; uncaptured combinations
within observed QLs remain explicitly unproven as retail behavior. Handler fee
tests use a DAO double, complemented by the real disposable MySQL checks.
