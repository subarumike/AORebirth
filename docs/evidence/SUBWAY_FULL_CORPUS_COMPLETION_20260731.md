# Subway authoritative acceptance matrix — 2026-07-31

## Selection decision

PF127 Subway is the highest-priority incomplete playfield after Arete. It is a
starter dungeon with a broad gameplay surface, the repository already identifies
it as the active playfield priority, and its capture corpus is substantially more
complete than the remaining alternatives.

| Rank | Playfield | Gameplay impact | Evidence completeness | Implementation readiness | Decision |
|---:|---|---:|---:|---:|---|
| 1 | PF127 Subway | 5/5 | 5/5 | 5/5 | **Selected.** Complete the supported surface and its acceptance ownership. |
| 2 | PF1931 Temple of Three Winds | 5/5 | 4.5/5 | 4.5/5 | Strong next candidate, but several nano, proc, resist, stun, and area-cast semantics remain genuinely unsupported. |
| 3 | Generated PF2 mission interiors | 4/5 | 4/5 | 3/5 | Captured mission bundles are mature; collision, navigation, visible corpse, chest inventory, and recovery evidence are less complete. |
| 4 | Nascence/Elysium | 5/5 | 2/5 | 2/5 | High impact, but the active-coverage inventory still has 877 Nascence actors and 40 core Hecklers without certified combat ownership. |

## Acceptance decision

Subway is complete for every behavior whose identity, lifecycle, and trigger
contract is supported by the complete existing repository and capture corpus.
Partial observations remain explicit evidence gaps instead of being promoted
through inferred selectors or triggers. The implementation completed by this
acceptance pass is deterministic reconciliation: the active-coverage projection
now recognizes the exact source-bound PF127 profile resolver now used by
production instead of requiring a direct regenerated runtime-identity
packet binding. The corrected 165-row boundary is exact: 29
`subway.supported.17720` Discarded Pets, five `subway.supported.203734` Muggers,
and 131 `subway.ordinary.*` actors. It requires the configured source identity
to equal the runtime source hint and every concrete source/level/generation
variant to resolve to a final combat-ready contract. It promotes only the exact
retaliation eligibility needed for that contract resolution; aggro radii and
combat-activation policy remain separately owned.

The accepted boundary is `322/322` active ordinary actors, zero ordinary
quarantine, four dedicated named encounters plus Abmouth-owned adds, six exact
vendors with the canonical 202-row runtime stock and a separate exact 203-row
evidence snapshot, Tailor interaction/dialogue/rewards, PF127 geometry and shared
navigation, six exact closed door statuses on external PF127 arrival plus 18
separate door-state identities retained without an invented transition trigger,
exact supported
lifecycle/loot, and the linked PF655 Karrec/gateway flow. Unknown probabilities,
unseen branches, and unmeasured values below are
explicit evidence gaps. They do not downgrade or block independently proven
behavior.

## Complete evidence corpus searched

- All `313` inventoried AOSharp capture folders through 2026-07-31, including
  `44` Subway-only and `34` mixed Subway sessions; `74` of the `78`
  Subway-bearing sessions contain raw packet rows.
- Raw packet logs and generated packet projections: SCFU, movement, combat,
  state, lifecycle, corpse, inventory, vendor, dialogue, trade, quest, zoning,
  geometry, line-of-sight, and door-state outputs. The PF127 door corpus includes
  61 raw DoorStatusUpdate packets and a 1,134-row state projection over 18
  identities. All 18 are observed closed and five are also observed open. The
  ten-row `playfield-activation` batch in
  `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260714-202820/pf127-door-state.csv`
  is an analyzer collector label, not a server lifecycle event: the enclosing
  `capture_info.json` is unarmed, unfinalized, unstable, and zoning-blocked.
  Capture `20260717-012522` independently proves six closed DoorStatusUpdate
  packets after the PF127 arrival lifecycle. Captured names and positions map
  unambiguously to official statels `C006007F`, `C007007F`, and `C00A007F` through
  `C00D007F`: maximum error is `0.000056m` and the nearest alternative is
  `7.996m`. Regenerated runtime identities are not used.
- `docs/generated/aosharp_capture_inventory.{md,csv}` and
  `docs/generated/aosharp_subway_capture_content.{md,csv}`, including the one
  official unreferenced session `20260716-220255`. Its 67 chase samples do not
  establish a new route category or trigger; the shared chase owner already
  consumes the proven behavior.
- Generated combat inventory, formula, active-coverage, loot, population,
  lifecycle, visibility, and geometry artifacts under `docs/generated/`.
- Both complete Subway vendor observations, including
  `docs/evidence/data/subway-vendors-20260719-021611.csv`; all 238 unique
  low/high item identities in its 203 rows resolve in production `items.dat`.
- All Subway evidence notes under `docs/evidence/`, especially
  `FINAL_ORDINARY_DUNGEON_COMBAT_COMPLETION_20260728.md`,
  `DUNGEON_GAMEPLAY_COMPLETION_20260728.md`,
  `DUNGEON_NAMED_ENCOUNTER_COMPLETION_20260728.md`,
  `DUNGEON_NAMED_LIFECYCLE_COMPLETION_20260729.md`,
  `SUBWAY_NAMED_BOSS_LIFECYCLE_20260716.md`,
  `SUBWAY_TAILOR_AND_VENDORS_20260719_021611.md`, and
  `WINDCALLER_KARREC_SUBWAY_ENTRANCE_QUEST_20260717_223626.md`.
- Current production code, checked-in content, focused tests, project state,
  current-task handoff, acceptance suites, and evidence backlog.

Private AORebirth validation captures were used only as implementation
verification. They were not promoted as original-client behavior evidence.
Incomplete captures contributed only independently complete identity-linked
observations; an incomplete session was never treated as a complete denominator.

## Implementation plan and result

1. Reconcile all PF127 ordinary actors through the same exact source-bound
   profile, level/generation variant, weapon, and final combat resolver chain
   production uses. The correction admits only the two exact supported-family
   selectors for Discarded Pet and Mugger plus exact `subway.ordinary.*`
   profiles, and every concrete variant must finish combat-ready.
   **Implemented.**
2. Lock every supported Subway capability to a named production owner and
   focused test. **Implemented by the matrix and acceptance suite.**
3. Retain unsupported facts as explicit non-blocking evidence gaps and keep
   their runtime paths fail-closed. **Implemented.**
4. Remove stale project-state blockers contradicted by later capture-backed
   completion evidence. **Implemented.**
5. Promote exactly the six closed PF127 statuses proven on external arrival,
   using name/template/position mapping to official statels; suppress them on
   same-playfield death respawn. Preserve the separate 18-identity state
   projection as evidence only, reject its analyzer collector label as a server
   trigger, and do not import PF1931's proximity rule or invent a cadence.
   **Implemented.**
6. Preserve the exact 203-row `20260719-021611` vendor observation beside the
   canonical 202-row snapshot and collapse identical Pharmacist/Container
   evidence. **Implemented as an evidence archive mapped to the established
   canonical owners; regenerated capture-session identities are not runtime
   selectors. The canonical runtime stock is unchanged because selector,
   weights, and refresh timing are unresolved.**

## Authoritative acceptance matrix

| Capability | Captured evidence and accepted boundary | Production owner | Focused acceptance test | Acceptance |
|---|---|---|---|---|
| NPC identity and ordinary population | **Exact:** 322 source-local PF127 spawn rows across 26 profiles preserve source identity, family, template/MonsterData, level or bounded generation set, appearance, position, heading, and captured waypoint facts. Runtime identities are generation-local and never used as archetype selectors. | `CapturedSubwayContentProvider`, `CapturedSubwayOrdinaryContentProvider`, `OrdinaryEnemyCatalog`, `WorldPopulationController`, `SubwayContentModule` | `SubwayAcceptanceMatrixTests.AllSupportedOrdinaryActorsReconcileThroughProductionOwners`; `WorldPopulationFoundationTests.CapturedOrdinaryExceptionsAndPopulationBoundaryRemainStable` | **Accepted — 322/322, zero quarantine** |
| Spawn, idle, and patrol movement | **Exact:** source-bound initial positions, captured waypoint lists, and complete replay segments retain coordinate order and measured timing. Actors without a captured patrol remain idle; no route is borrowed or invented. | `CapturedSubwayContentProvider`, `CapturedSubwayOrdinaryContentProvider`, `NpcPatrolReplayCoordinator`, `NPCRuntimeService` | `SubwayAcceptanceMatrixTests.PopulationMovementAggroCombatAndLifecycleHaveFocusedCoverage`; patrol/lifecycle cases in `PlayfieldLifecycleTraceTests` and `WindcallerKarrecNpcContentTests` | **Accepted — exact observed routes only** |
| Chase, line of sight, and leash | **Exact/proven directional boundary:** PF127 collision geometry owns attack-line and movement obstruction. Shared chase routing follows collision-valid paths; direct chase resumes when clear. The accepted Subway home-boundary leash resets and routes home without importing another playfield's rule. | `Pf127CollisionGeometryLoader`, `Pf127ChaseNavigationProvider`, `NpcChaseNavigationRuntimeService`, `NpcCombatLeashPolicy` | `NpcChaseNavigationTests` PF127 blocked/open/route/return/lifecycle cases; `PlayfieldCollisionGeometryTests.ReviewedPf127AssetLoadsAndReplaysCapturedVergilClearAndBlockedSegments` | **Accepted — measured geometry and supported lifecycle; unmeasured thresholds remain policy-labeled** |
| Automatic and social aggro | **Exact where measured; proven eligibility otherwise:** capture-backed per-profile acquisition and same-profile social behavior remain source/profile scoped and LOS gated. Missing radii do not inherit an asserted official value. | `OrdinaryEnemyRuntimeService`, `CapturedSubwayEncounterRuntimeService`, `NpcCombatLeashPolicy`, PF127 collision owner | `SubwayAcceptanceMatrixTests.PopulationMovementAggroCombatAndLifecycleHaveFocusedCoverage`; Mugger/ordinary and named cases in `PlayfieldLifecycleTraceTests`, `NpcChaseNavigationTests`, and `AbmouthEncounterRuntimeServiceTests` | **Accepted — exact/range/eligibility boundaries retained** |
| Ordinary combat | **Exact production ownership:** all 322 actors finish the production combat resolver chain ready under their captured PF127 profile and exact configured/runtime source identity. The circular-gate correction covers exactly 165 rows: 29 Discarded Pets through `subway.supported.17720`, five Muggers through `subway.supported.203734`, and 131 actors through `subway.ordinary.*`. Every concrete source/level/generation variant must end `IsCombatReady`; the helper promotes only exact retaliation eligibility needed to reach that contract. Existing exact-source, capture-safe archetype, and bounded mathematical contracts remain unchanged. It does not assert a new per-source packet observation, aggro radius, or automatic attack-on-sight policy. Unsupported tuples fail closed. | `OrdinaryEnemyCatalog`, `CapturedSubwayCombatCatalog`, `CapturedEnemyCombatProfileCatalog`, `CapturedSubwayRetaliationEligibilityResolver`, `CapturedEnemyCombatRuntime`, `OrdinaryEnemyRuntimeService` | `CapturedEnemyCombatActiveCoverageTests.Pf127OrdinaryCoverageIsCompleteThroughExactProductionOwnedProfileResolution`; `Pf127ProfileResolutionReproducesTheExactProductionContractPath`; `CapturedSubwayRetaliationEligibilityResolverTests.ExactPf127BindingsResolveAllTwentyNineDiscardedPetsAndFiveMuggers`; `CapturedEnemyCombatProfileCatalogTests.FinalOrdinaryDungeonCombatCompletionReconcilesAllTwentyFiveActorsAndAll489Resolve`; `SubwayAcceptanceMatrixTests.AllSupportedOrdinaryActorsReconcileThroughProductionOwners` | **Accepted — exact 322/322 combat ownership; automatic aggro radii and activation policy remain separately owned** |
| Named encounters and scripted combat | Abmouth Supremus, Vergil Aeneid, Eumenides, and Strike Foreman have dedicated PF127 owners. Abmouth add caps/refill/cleanup, captured independent streams, warp timing, Vergil healing, named respawn, corpse, and atomic loot behavior activate only in their matching encounter state. | `CapturedSubwayEncounterRuntimeService`, `DungeonNamedLifecycle`, `NPCRuntimeService`, `GlobalLootRuntimeService` | `AbmouthEncounterRuntimeServiceTests`; PF127 cases in `DungeonNamedEncounterCompletionTests` and `DungeonNamedLifecycleCompletionTests` | **Accepted — exact supported stages and effects** |
| Death and corpse lifecycle | Death retires combat, patrol, chase, encounter, and generation ownership before one corpse is created. Exact corpse visual/CATMesh and identity-linked generation are retained; duplicate death/corpse ownership fails closed. | `NPCRuntimeService`, `NpcCorpseLifecycleCoordinator`, `CorpseRuntimeService`, `GlobalLootRuntimeService` | `DungeonNamedLifecycleCompletionTests.EveryNamedDeathCreatesAtMostOneCorpse`; ordinary lifecycle cases in `PlayfieldLifecycleTraceTests`; `GlobalLootFoundationTests` | **Accepted — exact supported lifecycle** |
| Respawn and cleanup | Regular Subway enemies use the accepted explicit 240-second policy; loot-bearing regular corpses persist for 60 seconds, while born-empty and fully emptied regular corpses clean up immediately. Named bosses respawn 600 seconds after death independently of a retained 1,800-second loot corpse; empty-corpse cleanup is three seconds where captured. Generation replacement is single-owner and duplicate-safe. | `WorldPopulationController`, `WorldRespawnScheduler`, `DungeonNamedLifecycle`, `CapturedSubwayEncounterRuntimeService`, `CorpseRuntimeService` | `WorldPopulationFoundationTests` respawn/generation tests; `AbmouthEncounterRuntimeServiceTests.NamedBossesRespawnTenMinutesAfterDeathIndependentlyOfCorpses`; `DungeonNamedLifecycleCompletionTests` | **Accepted — exact or explicitly policy-owned per row** |
| Loot and credits | Strict first-open corpse observations and named snapshots remain identity-linked atomic outcomes. Exact items, QL, quantities, empty outcomes, CATMesh, and credits are preserved; reopening never rerolls and unrelated corpses never contaminate a pool. | `CapturedSubwayOrdinaryContentProvider`, named loot definitions, `SubwayLootPoolRules`, `GlobalLootRuntimeService` | `SubwayEnemyLootEvidenceTests`; `SubwayLootPoolRulesTests`; `GlobalLootFoundationTests` PF127 cases | **Accepted — observed outcomes; official probability and wider pools unresolved** |
| Vendors | Six captured NPC owners map to six exact shop endpoints. The canonical 202-row snapshot remains the only production runtime stock. The 203-row `20260719-021611` observation is retained atomically with exact ordering, slots, item identities, and QL, mapped to the established canonical owner/terminal/template tuples. Its regenerated session identities remain provenance in the evidence note and cannot become runtime selectors. Identical Pharmacist and Container observations reuse canonical immutable stocks. No runtime selector, weighting, replacement, or refresh behavior is inferred from the two observations. | `CapturedSubwayVendorContentProvider` owns the canonical definitions and alternate evidence archive; `CapturedSubwayVendorRuntimeRegistry`, `CapturedSubwayVendorRuntimeService`, and `CapturedSubwayVendorInteractionHandler` consume only the canonical definitions | `SubwayVendorContentTests.CaptureDefinesSixNpcOwnersAndSixResolvedShopEndpoints`; `CapturedShopStockFingerprintMatchesAuthoritativeCsv`; `AlternateCapturedShopSnapshotIsAtomicAndMatchesAuthoritativeCsv`; `AlternateCapturedSnapshotDoesNotReplaceCanonicalRuntimeStock`; `CapturedSnapshotResolutionFailsClosedOutsideExactEvidence` | **Accepted — canonical 202-row runtime stock; alternate 203-row snapshot evidence-only** |
| Tailor interaction and dialogue | Captured greeting/reopen nodes and eight measurement choices grant only the eight exact QL1 rewards. Unsupported replies and variable merchant-stock inference are excluded. | `CapturedSubwayTailorDialogueContent`, `CapturedSubwayTailorDialogueRuntime`, dialogue session and vendor dispatch | `SubwayVendorContentTests.TailorMeasurementChoicesMapToEightCapturedQlOneItems`; `TailorFirstOpenAndReopenResolveToCapturedGreetingNodes` | **Accepted — exact captured branches** |
| Karrec quest, trade, reward, and gateway | The linked PF655 boundary owns three captured NPCs, exact dialogue/content, two-item trade qualification, mission state, one-level XP policy, side-token tiers, retry-safe reward handoff, and successful gateway use. The compiled quest, trade adapter, packet sender, runtime service, and gateway handler are all production-reachable. | `WindcallerKarrecNpcContent`, `WindcallerKarrecNpcRuntimeService`, `WindcallerKarrecQuestRuntime`, `WindcallerKarrecTradeAdapter`, `WindcallerKarrecPacketSender`, `TotwGatewayInteractionHandler` | `WindcallerKarrecNpcContentTests`; `WindcallerKarrecInteractionRulesTests`; `QuestRuntimePersistenceTests.KarrecProgressRewardsAndAccountAccessAreScopedAndRetrySafe`; `SubwayAcceptanceMatrixTests.VendorDialogueQuestAndGatewayOwnersAreCompiledAndFocused` | **Accepted — supported success path; official storage field and unseen branches unresolved** |
| Door state and external-arrival snapshot | **Exact:** raw capture `20260717-012522` sends six closed statuses after external PF127 arrival. Captured name/template/position maps them unambiguously to official statels `C006007F`, `C007007F`, `C00A007F`, `C00B007F`, `C00C007F`, and `C00D007F`; all six and only those six are emitted on that lifecycle. Same-playfield death does not replay them. A separate 18-identity projection retains closed/open state evidence, but its analyzer collector label is not promoted as a trigger. PF1931's proximity rule and unsupported transitions remain excluded. | `CapturedSubwayArrivalDoorEvidenceSet`, `CapturedSubwayDoorSnapshotEvidence`, `CapturedPlayfieldDoorStatusRuntimeService`, `Playfield.SendStaticDynelsToClient`, `ClientConnected` | `TempleDoorStatusRuntimeTests.SubwayExternalArrivalEvidenceMapsExactlySixOfficialStatels`; `SubwayExternalArrivalSendsOnlySixCapturedClosedStatuses`; `SubwayDoorRuntimeDoesNotReplayOnDeathOrInventProximity`; `SubwayDoorEvidencePreservesExactCapturedIdentityAndStateCoverage` | **Accepted — exact six-door external-arrival snapshot; other transitions evidence-only** |
| Playfield geometry, entry, exit, and teardown | PF127 uses the reviewed geometry asset, separate attack/movement probes, exact captured main entry and exit landings/headings, post-zone grace, edge-triggered contact suppression, and playfield-scoped cleanup for population, vendors, route state, corpses, and visibility. | `Pf127CollisionGeometryLoader`, `Pf127ChaseNavigationProvider`, `SubwayTeleportProxyDestinationRules`, `PlayfieldStatelTransitionRuntimeService`, `PlayfieldRuntimeSystems` | `PlayfieldCollisionGeometryTests`; PF127 cases in `NpcChaseNavigationTests`; `PlayfieldLifecycleTraceTests.SubwayProxyExitUsesOfficialLandingAndSuppressesDelayedEntryBounce`; `SubwayAcceptanceMatrixTests.PlayfieldGeometryZoningAndTeardownHaveFocusedCoverage` | **Accepted — exact supported geometry and zoning** |

## Explicit non-blocking evidence gaps

| Missing fact | Exact evidence searched | Proven boundary retained | Why unsupported |
|---|---|---|---|
| Official loot probabilities and unseen wider pools | All strict corpse opens, raw inventory transfers/reopens, lifecycle joins, loot projections, generated inventories, and runtime definitions | Every complete observed atomic outcome, including empty outcomes and credits, is active without cross-corpse mixing. | Outcomes prove membership and observed multiplicity, not the official random selector or exhaustiveness. |
| Exact aggro/leash/respawn values for unmeasured families or variants | Raw movement/combat/state/lifecycle streams, dossiers, generated combat reports, PF127 geometry, current policies | Exact measured values and proven eligibility/direction remain active; clearly labeled private-server policies remain policy, not capture claims. | No identity-linked event supplies the missing threshold or timer for those exact variants. |
| Unsupported nano/proc effects and schedules | Raw cast/start/finish/effect packets, combat projections, named/ordinary reports, current effect owners | Only complete target/effect/timing chains already owned by named encounters activate. | A packet occurrence alone does not prove target selection, effect, duration, stacking, refresh, or trigger probability. |
| Vendor stock weights, random selector, QL-generation rules, and refresh timing | Both exact vendor snapshots, VendorFull/ShopUpdate projections, 202-row baseline CSV, 203-row alternate projection, provider and tests | The canonical captured stock remains active; the complete alternate snapshot and exact duplicate stocks are retained as evidence without mixing or replacement. | Differing snapshots prove variability and exact outcomes, but not the official selector, probability, QL-generation algorithm, or in-place refresh interval. |
| PF127 dynamic door transitions and general static dynels | Complete capture inventory, 61 raw DoorStatusUpdate packets, the 1,134-row/18-door state projection, raw PF127 arrival capture `20260717-012522`, `playfields.dat`, geometry outputs, static-runtime code, and tests | The exact six-door closed external-arrival snapshot is active with official statel mapping. The separate 18-identity projection and its five observed-open flags remain evidence-only. No analyzer batch label, regenerated identity, proximity rule, or death replay is promoted as a trigger. | The corpus has no identity-linked open/close cause, lock, animation, cadence chain, or complete general static-dynel projection for PF127; Temple's distinct proximity rule is not reused. |
| Cross-elevation navigation/floor projection | PF127 triangle asset, captured LOS/path probes, route diagnostics, navigation tests | Collision-valid same-surface chase and leash routing remains active and fails boundedly when no valid route exists. | The corpus does not establish a general floor/portal graph for unsupported cross-elevation targets. |
| Karrec denial, failure, alternate, repeat, reconnect, team, and research-diversion semantics | Both Karrec captures, raw dialogue/trade/quest/stat/reward/zoning packets, quest content/runtime/tests, full-character stat handling | The captured successful hand-in, reward, token, persistence, and gateway path remains active. | No exact denial/failure/team/repeat packet chain or official persistent account-flag identity exists; personal research is an unimplemented expansion system. |

## Acceptance suite

The authoritative release gate is:

```cmd
tools\run_subway_acceptance_tests.cmd
```

It covers the acceptance owner, all PF127 ordinary population/combat resolvers,
patrol/chase/leash/LOS and geometry, named encounters and lifecycle, corpse/loot,
vendors/Tailor, Karrec quest persistence and gateway routing, zoning, teardown,
and the deterministic active-coverage projection. The approved debug build and
engine restart wrappers remain the final executable checks.

## Evidence-discipline confirmation

No available evidence was ignored because it lacked a regenerated runtime
identity, one exact packet subtype, a repeated route, a closed loop, a specific
pre-existing runtime state, or a second observation. No supported behavior was
downgraded because an unrelated probability or variant remains unknown. No
Temple rule, family default, unbounded nearest-level substitution, vendor
snapshot selector, proximity trigger, death replay, or private-client guess was
used to fill a missing PF127 fact.
