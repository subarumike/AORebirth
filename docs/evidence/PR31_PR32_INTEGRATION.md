# PR31 / PR32 reconciled integration

Date: 2026-10-09. Windows integration candidate; no master merge or deployment.

## Source identity and integration

- Branch: `codex/integrate-perk-aura-taunt`.
- Starting public master: `bddea56ca2d47c5eb64ed53f6e9efd279ff4f694`.
- PR31 foundation: `04f0c966d35c9d079264d0d741d42bcef2ce57a6`.
- PR32 selective input: `0c97b85942989aa0da1b771ad33e8fe9ac8820f4`.
- The ending commit is the commit containing this report; obtain it with
  `git log -1 --format=%H` on the integration branch.

PROVEN: the six runtime files changed by PR31 are byte-identical to its exact
head. ItemUseFunctions.cs is byte-identical to PR32's exact head. Public master
and the original checkout's unrelated mission branch/dirty work were preserved.

| Input | Incorporated changes |
| --- | --- |
| PR31 Player.cs | Selected-target preference for applicable perks; actual target stat/state readers for preparation, revalidation and failure feedback |
| PR31 ItemTemplate.cs | Related perk/nano/same-stat subject grouping; target state resolver; periodic normal spell evaluation; Skill dispatch |
| PR31 RequirementFeedback.cs | Matching target stat/state evaluation in requirement failure messages |
| PR31 StatModifierSpells.cs | Skill support in passive modifiers |
| PR31 Buff.cs | Data-defined repeated Hit and child-cast scheduling |
| PR31 NanoRuntime.cs | Active-instance rejection and normal periodic spell execution |
| PR32 ItemUseFunctions.cs | Positive TauntNpc through existing NpcBrain.AddThreat, for players and player-owned pets |
| PR32 CastNanoTests.cs | All original three taunt tests retained |

Additional test cases extend CastNanoTests.cs and add
PerkRequirementIntegrationTests.cs and BuffRepairIntegrationTests.cs.

### Conflict resolution

PR32's ItemTemplate.cs edits are excluded in their entirety: neither evaluator's
selector-reset removal nor the persistent-selector documentation was imported.
The shared file remains exactly PR31's version. Selective incorporation avoided
an unresolved textual merge.

This preserves Mongo's paired PsychologicalModification comparisons while
returning Opportunity Knocks' unrelated LastRnd check to the event reader.
No GameData, database, schema, configuration or balancing values changed.
Synthetic IDs/amounts occur only in tests; runtime uses loaded definitions.

## Executed validation

| Validation | Result |
| --- | --- |
| Approved Windows core-engine/preflight build | PASS |
| PerkRequirementIntegrationTests | PASS10 |
| BuffRepairIntegrationTests | PASS20 |
| CastNanoTests, including retained PR32, added taunt and periodic fanout cases | PASS37 |
| Focused total | PASS67 |
| Related regressions plus focused cases | 205 passed, 7 failed, 212 total |
| Untouched master, same available regression selection | 152 passed, 7 failed, 159 total |
| Failure-name and assertion-message comparison | Identical seven failures; zero new failures in the executed selection |
| Full NewEngine suite, starting master and integration | Compilation blocked by identical six missing-mission-type diagnostics; tests not executed |
| Opportunity Knocks on exact PR32 head, isolated negative control | Both cases fail as expected; the same two cases pass on the integration |
| git diff --check | PASS |
| Live AO client checks | UNVERIFIED; not executed |

The focused cases exercise actual Player.TryPreparePerkAction against selected
NPCs with family 96/97 and a distinct fighting NPC, invalid-family feedback,
target running-nano state/feedback, Dazzle rank branches, related running-nano
groups, and Opportunity Knocks rolls 25/26 with contradictory raw caster LastRnd.
Opportunity Knocks asserts actual SpecialHit health effects and one stored event
roll; its PvP-enabled test victim satisfies the existing damage-permission gate.

Taunt cases cover Mongo PM boundaries 0/49/50/100/149/150/200 with differing
recipient PM, area/range/attackability, accumulation, pet identity attribution,
pacification/evasion, unchanged immediate AI targeting, and invalid sources or
missing/nonpositive/nonnumeric amounts.

Buff cases cover both Skill paths, Skill damage bonuses exactly once across
landing/rebase/cancellation, all nine periodic function families' scheduling,
count/centisecond intervals, child refresh/expiry, parent cancellation, queued
stale-instance rejection, DoT/HoT effects, and periodic caster-rank/target-state
evaluation with application to the caster. State changes are reevaluated on ticks.
End-to-end team/area periodic cases also cover teammate/bystander selection,
team-parent cancellation and child expiry, recipient movement/range,
attackability, caster attribution and count exhaustion.

### Proven baseline exceptions

The following names and exact assertion messages reproduced unchanged on
starting master and the integration:

| Test | Failure |
| --- | --- |
| NanoDelayCalculatorTests.AggDefIsClampedAndDefensiveStanceLengthensTheDelay | Expected 600, actual 750 |
| NpcBrainTests.ProximityAggroAddsOneHateWhenBreedHostilityIsPositive | Expected 1, actual 0 |
| NpcBrainTests.ProximityAggroDoesNotStackOrFireWhenStatIsZeroOrPlayerIsFar | Expected 1, actual 0 |
| NpcBrainTests.LeashResetFinishesTheTickInsteadOfSpinning | Assert.IsFalse |
| NpcBrainTests.ResetWithHomeWalksThenHeals | Assert.IsTrue |
| NpcBrainTests.HigherThreatSwitchesCombatTargetWhileStillChasing | Expected CanbeAffected:42, actual None:0 |
| NpcBrainTests.EvadingNpcIgnoresThreatAndDamageUntilItIsHome | Assert.IsFalse |

Full-project CS0246 diagnostics concern MissionNpcContent,
MissionAcgMaterializedInstance, GeneratedMissionWorld, MissionAcgLayoutBundle and
GeneratedMissionCorpseDynel in MissionContentEditabilityTests.cs,
GeneratedMissionMaterializationTests.cs and GeneratedMissionCorpseInteractionTests.cs.

An initial broader selected compilation also proved stale references on exact
master in HitHealthDamageTests (ExecuteHitProjection), BuffEndSpawnTests
(MobTemplate.Attackable) and TeamServiceTests (TeamChatMessageHandler constructor,
ClientFeedback.Channel). These classes were not executed. Their expectations
and production APIs were not modified.

### Reproduction

Run from the repository root in CMD. The first command is the approved build.
The ordinary test command intentionally retains the baseline compilation failure.

```cmd
cmd /d /c tools\build_aorebirth_debug.cmd
dotnet test AORebirth\Server\ZoneEngine_New.Tests\ZoneEngine_New.Tests.csproj
```

The opt-in targets file selects the 18 related test classes plus TestDoubles and
keeps SDK-generated sources. It changes no normal project configuration or
runtime behavior and retains the seven failing baseline cases in the selection.
The underlying existing project and MSTest runner remain in use.

```cmd
dotnet test AORebirth\Server\ZoneEngine_New.Tests\ZoneEngine_New.Tests.csproj -p:CustomAfterMicrosoftCommonTargets="%CD%\AORebirth\Server\ZoneEngine_New.Tests\Pr31Pr32Validation.targets" --logger trx --results-directory build-verify\committed-selection
dotnet test AORebirth\Server\ZoneEngine_New.Tests\ZoneEngine_New.Tests.csproj --no-build --filter FullyQualifiedName~IntegrationTests
dotnet test AORebirth\Server\ZoneEngine_New.Tests\ZoneEngine_New.Tests.csproj --no-build --filter FullyQualifiedName~CastNanoTests
```

The selected run exits nonzero because the seven baseline cases still fail.
The two filtered runs execute the built selection and pass 30 and 37 cases.

Local ignored receipts in the integration worktree's build-verify directory:
pr31-pr32-build.log, baseline-full-tests.log, baseline-selected-tests.log,
full-integration.log, committed-selection.log, focused-integration.log,
focused-taunt.log, pr32-negative-control.log, baseline-results,
negative-control-results and pr31-pr32-test-comparison.json. Raw logs and local
machine paths are not part of the public commit.

## Remaining acceptance and unsupported behavior

UNVERIFIED: live AO client acceptance. This session has no native AO client
control surface; automated runtime fixtures are not live client evidence.
The approved build stopped the positively identified original-checkout engines
through ACTIVE_CHECKOUT_WINS. No engines were started or restarted for this task.

Mike's remaining client checks: selected Ken Fi and Dazzle rank behavior,
Opportunity Knocks damage/miss outcomes, Mongo bands and pet aggro, Gazump and
Challenger damage bonuses, aura refresh/cancel/child expiry, and normal DoT/HoT.
GameData-defined values remain authoritative during those checks.

Negative detaunts, broad mixed-stat selector patterns and persisted aura pulse
phase/count remain separate follow-up work. Automated periodic team/area fanout
fixtures pass; live acceptance remains outstanding. No new failure observed in
this selection is not proof of complete gameplay correctness.

## Files inspected and changed

Inspected current startup/authority/workflow/testing/code-standard documents,
the seven changed runtime files, Character.cs, StatCollection.cs, ItemSpells.cs,
NpcBrain.cs, hate/pet/cast helpers and existing tests/TestDoubles. Review remained
within the active ZoneEngine_New ownership boundary.

Changed: the seven runtime files listed above; CastNanoTests.cs; the two new
integration test files; Pr31Pr32Validation.targets; this report; CURRENT_TASK.md;
and PROJECT_STATE.md. No unrelated implementation, private GameData, database,
schema, Linux infrastructure, master merge or deployment is included.
