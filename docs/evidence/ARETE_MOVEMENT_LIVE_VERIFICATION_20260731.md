# Arete Movement Live Verification — 20260731-030702

> Historical classifier notice: the 6 m activation and 2.5 m continuation rules below were false runtime assumptions. Exact packet/manual observations remain evidence; distance-gated eligibility and rejection conclusions are superseded by the corrected verifier and full-corpus completion report. Rows labeled `exact_legacy_robot_route` below were subsequently normalized into schema-4 `movement-full` and are no longer a separate runtime path.

This report reconciles the complete live observation capture against every behavior-specific promoted runtime row. Regenerated live identities are evidence labels only and are not used as promotion keys.

## Post-fix result — capture `20260731-035230`

- The original client was observed only; no client input was automated.
- Mike visually confirmed that NPCs now move.
- Garbage Flea `SimpleChar:000F42D1` entered captured patrol from an idle controller and emitted the exact promoted route `m09445`: `(3453.60864, 0.01, 875.229919)` to `(3453.51245, 0.636055, 879.729492)`.
- Three visible Garbage Fleas matched exact metadata. One emitted the exact promoted route; two emitted no path during the 30-second window. That short-window absence is retained as an observation and does not reject their existing corpus-backed patrol evidence.
- Post-fix packet reconciliation: **346 / 346** packets. Exact promoted routes: recorded in [`arete-movement-live-paths-20260731-post-fix.csv`](arete-movement-live-paths-20260731-post-fix.csv).
- Exact identity constraints, historical source selections, and packet counts are recorded in [`arete-movement-live-identities-20260731-post-fix.csv`](arete-movement-live-identities-20260731-post-fix.csv). Its distance-gated decision columns are superseded by the corrected verifier.
- The proven runtime defect was the circular requirement that an NPC already be in `Patrolling` state before captured patrol could start. That gate was removed.
- No stuck or invalid captured movement was observed for the exact promoted Garbage Flea route.

## Captured automatic-aggro evidence

- The reconciled Arete corpus contains **69** enemy-to-player attack starts and **50** NPC-first starts across **19** exact name/family/template/level constraints.
- **14** constraints have measured lower-bound radii. The remaining **5** prove automatic-aggro eligibility but not an exact radius; runtime promotes only a contact-safe floor for those five and does not claim a captured radius.
- Exact aggregate events and derivation are documented in [`ARETE_AGGRO_EVIDENCE_AGGREGATE_20260722.md`](ARETE_AGGRO_EVIDENCE_AGGREGATE_20260722.md) and the per-capture event CSVs.
- This post-fix movement capture did not exercise combat. The aggro promotion is source-capture-backed and build/test verified, but not represented as post-deployment live combat verification.

## Verdict

- Promoted runtime rows loaded: **8,121**.
- Live NPC identities with complete stable metadata: **98**.
- Live identities rejected as incomplete or regenerated/conflicting: **0**.
- Live identities with any exact promoted metadata constraint: **29**.
- Live identities within the 6 m patrol activation radius: **15**.
- Live identities that emitted `FollowTarget/NpcPath`: **2**.
- Live path packets reconciled: **695 / 695**.

## Exact identity outcomes

| Reason | Identities |
| --- | ---: |
| `no_exact_promoted_metadata_constraint` | 69 |
| `eligible_selected_patrol_but_no_live_movement_packet` | 10 |
| `no_source_variant_within_6m` | 9 |
| `selected_source_variant_has_no_patrol_observation` | 5 |
| `selected_patrol_start_exceeds_2_5m_continuation` | 4 |
| `live_movement_packet_observed` | 1 |

## Family and constraint coverage

| Exact constraint | Live identities | Metadata candidates | Bindable patrol | Packet emitters |
| --- | ---: | ---: | ---: | ---: |
| 32-V Docker (family=1019, template=17649, level=3, pf=6553) | 13 | 0 | 0 | 0 |
| Alex Gibbs (family=137, template=263050, level=20, pf=6553) | 1 | 0 | 0 | 0 |
| Antonio Stacklund (family=137, template=26088, level=20, pf=6553) | 1 | 0 | 0 | 0 |
| Barry the Food Vendor (family=0, template=26139, level=10, pf=6553) | 1 | 0 | 0 | 0 |
| Bruiser (family=103, template=26088, level=5, pf=6553) | 1 | 0 | 0 | 0 |
| Carol Schieffer (family=103, template=26090, level=21, pf=6553) | 1 | 0 | 0 | 0 |
| Cedric Harding (family=0, template=165188, level=6, pf=6553) | 1 | 0 | 0 | 0 |
| Chauncey Varela (family=103, template=26139, level=1, pf=6553) | 1 | 0 | 0 | 0 |
| Clan Protester (family=104, template=26090, level=20, pf=6553) | 1 | 0 | 0 | 0 |
| Clan Protester (family=104, template=26103, level=20, pf=6553) | 1 | 0 | 0 | 0 |
| Clan Protester (family=104, template=26139, level=20, pf=6553) | 1 | 0 | 0 | 0 |
| Cleaning Robot (family=1019, template=297023, level=1, pf=6553) | 1 | 1 | 0 | 0 |
| Dion Giscombe (family=103, template=26097, level=7, pf=6553) | 1 | 0 | 0 | 0 |
| Dr. Mason (family=137, template=26147, level=20, pf=6553) | 1 | 0 | 0 | 0 |
| Eliseo Ye (family=103, template=26139, level=19, pf=6553) | 1 | 0 | 0 | 0 |
| Engineer Automaton I (family=95, template=17649, level=5, pf=6553) | 1 | 0 | 0 | 0 |
| Food Provider (family=0, template=26090, level=10, pf=6553) | 1 | 0 | 0 | 0 |
| Furniture Merchant (family=0, template=26137, level=104, pf=6553) | 1 | 0 | 0 | 0 |
| Garbage Flea (family=25, template=17657, level=1, pf=6553) | 2 | 2 | 2 | 0 |
| Garbage Flea (family=25, template=17657, level=2, pf=6553) | 2 | 2 | 2 | 0 |
| Garbage Flea (family=25, template=17657, level=5, pf=6553) | 2 | 2 | 1 | 0 |
| Garbage Flea (family=25, template=17657, level=6, pf=6553) | 6 | 6 | 6 | 0 |
| Gnarl the Roller (family=55, template=17687, level=7, pf=6553) | 1 | 1 | 0 | 0 |
| ICC Immigration Officer Bill (family=137, template=26088, level=25, pf=6553) | 1 | 0 | 0 | 0 |
| ICC Peacekeeper (family=0, template=26092, level=40, pf=6553) | 4 | 4 | 4 | 2 |
| IIV-X Advanced Docker (family=1019, template=17649, level=4, pf=6553) | 1 | 1 | 0 | 0 |
| Jamison Clasen (family=103, template=26097, level=6, pf=6553) | 1 | 0 | 0 | 0 |
| Janae Seaman (family=103, template=26149, level=23, pf=6553) | 1 | 1 | 0 | 0 |
| Janee Forejt (family=103, template=26090, level=6, pf=6553) | 1 | 1 | 0 | 0 |
| Joseph Schuemann (family=103, template=26139, level=8, pf=6553) | 1 | 0 | 0 | 0 |
| Keesha McKesson (family=103, template=26149, level=19, pf=6553) | 1 | 0 | 0 | 0 |
| Lady Sheila Black (family=137, template=26137, level=15, pf=6553) | 1 | 0 | 0 | 0 |
| Leonora Marty (family=137, template=26125, level=10, pf=6553) | 1 | 1 | 0 | 0 |
| Logistics Manager Fausto (family=137, template=26101, level=20, pf=6553) | 1 | 0 | 0 | 0 |
| Lorelei the Bartender (family=0, template=26137, level=10, pf=6553) | 1 | 0 | 0 | 0 |
| Luna Erke (family=103, template=26090, level=21, pf=6553) | 1 | 0 | 0 | 0 |
| Marco Spida (family=0, template=26092, level=10, pf=6553) | 1 | 0 | 0 | 0 |
| Max Barchus (family=103, template=26097, level=20, pf=6553) | 1 | 0 | 0 | 0 |
| Mitchell Dorph (family=103, template=26139, level=29, pf=6553) | 1 | 0 | 0 | 0 |
| Mutated Garbage Flea (family=25, template=17657, level=7, pf=6553) | 1 | 1 | 0 | 0 |
| Neutral Clothing Salesman (family=0, template=26092, level=10, pf=6553) | 1 | 0 | 0 | 0 |
| Omni-AF Officer Milne (family=2, template=165186, level=35, pf=6553) | 1 | 0 | 0 | 0 |
| Omni-AF Private (family=2, template=26151, level=10, pf=6553) | 1 | 0 | 0 | 0 |
| Omni-Med Guard (family=2, template=26139, level=20, pf=6553) | 1 | 0 | 0 | 0 |
| Omni-Med Surgeon (family=105, template=26092, level=20, pf=6553) | 1 | 0 | 0 | 0 |
| Omni-Pol Guard (family=2, template=26097, level=20, pf=6553) | 1 | 0 | 0 | 0 |
| Omni-Trans Equipment Vendor (family=88, template=250380, level=40, pf=6553) | 1 | 0 | 0 | 0 |
| Patrick Sun (family=137, template=26092, level=20, pf=6553) | 1 | 0 | 0 | 0 |
| Protester (family=103, template=203740, level=2, pf=6553) | 1 | 1 | 0 | 0 |
| Rashida Ardman (family=103, template=26149, level=20, pf=6553) | 1 | 0 | 0 | 0 |
| Remi Gallois (family=137, template=26084, level=10, pf=6553) | 1 | 0 | 0 | 0 |
| Robotic Guard Dog (family=1019, template=17720, level=13, pf=6553) | 1 | 1 | 0 | 0 |
| Rollerrat (family=55, template=17687, level=5, pf=6553) | 3 | 3 | 0 | 0 |
| Rollerrat (family=55, template=17687, level=6, pf=6553) | 1 | 1 | 0 | 0 |
| Russel Aronstein (family=103, template=26139, level=1, pf=6553) | 1 | 0 | 0 | 0 |
| Sarah Greene (family=137, template=295889, level=20, pf=6553) | 1 | 0 | 0 | 0 |
| Secondhand Peddler (family=0, template=26090, level=200, pf=6553) | 1 | 0 | 0 | 0 |
| Shady Guy (family=137, template=26074, level=20, pf=6553) | 1 | 0 | 0 | 0 |
| Shane Streller (family=103, template=26097, level=8, pf=6553) | 1 | 0 | 0 | 0 |
| Sherwood Bannister (family=103, template=26097, level=21, pf=6553) | 1 | 0 | 0 | 0 |
| Shipping Manifest Terminal (family=0, template=279184, level=25, pf=6553) | 1 | 0 | 0 | 0 |
| Stanley Goodman (family=137, template=26084, level=20, pf=6553) | 1 | 0 | 0 | 0 |
| Tailor (family=0, template=26076, level=122, pf=6553) | 1 | 0 | 0 | 0 |
| Trinh Alsaqri (family=103, template=26149, level=28, pf=6553) | 1 | 0 | 0 | 0 |
| Vaughn Hammond (family=137, template=281855, level=25, pf=6553) | 1 | 0 | 0 | 0 |
| Velva Age (family=103, template=26149, level=30, pf=6553) | 1 | 0 | 0 | 0 |
| Vernon Godfray (family=137, template=295564, level=15, pf=6553) | 1 | 0 | 0 | 0 |
| Waste Collector (family=1019, template=17714, level=2, pf=6553) | 6 | 0 | 0 | 0 |

## Live packet route comparison

| Result | Packets |
| --- | ---: |
| `coordinates_not_in_promoted_dataset` | 582 |
| `exact_legacy_robot_route` | 62 |
| `live_identity_metadata_unresolved` | 26 |
| `exact_promoted_route` | 25 |

- Comparable promoted timing matches: **0**.
- Comparable promoted timing deviations: **25**.
- Timing tolerance: **±0.250 seconds**.

## Manual original-client observations

- No visible enemy displacement was observed.
- No enemy attacks were observed.
- Original-client input was manual; this verifier performs no client automation.

## Evidence boundary

- `PatrolActivationEligible` records whether any patrol row is within the 6 m activation radius.
- `FirstPatrolDecisionEligible` reproduces source-variant selection for spawn generation 1 and the runtime's 2.5 m continuation check.
- A loaded row is not considered active merely because it exists in the dataset.
- Spawn, chase, flee, and leash rows are reported as exact metadata evidence; this baseline did not force their lifecycle conditions.
- Attack absence is recorded separately because movement datasets do not prove aggression or attack initiation semantics.

Detailed deterministic evidence:

- Identity reconciliation: `arete-movement-live-identities.csv`
- Packet reconciliation: `arete-movement-live-paths.csv`
