# Arete Movement Live Verification — 20260731-035230

This report reconciles the complete live observation capture against every behavior-specific promoted runtime row. Regenerated live identities are evidence labels only and are not used as promotion keys.

## Verdict

- Promoted runtime rows loaded: **8,121**.
- Live NPC identities with complete stable metadata: **9**.
- Live identities rejected as incomplete or regenerated/conflicting: **0**.
- Live identities with any exact promoted metadata constraint: **5**.
- Live identities within the 6 m patrol activation radius: **3**.
- Live identities that emitted `FollowTarget/NpcPath`: **2**.
- Live path packets reconciled: **346 / 346**.

## Exact identity outcomes

| Reason | Identities |
| --- | ---: |
| `no_exact_promoted_metadata_constraint` | 4 |
| `eligible_selected_patrol_but_no_live_movement_packet` | 2 |
| `live_movement_packet_observed` | 1 |
| `no_source_variant_within_6m` | 1 |
| `selected_source_variant_has_no_patrol_observation` | 1 |

## Family and constraint coverage

| Exact constraint | Live identities | Metadata candidates | Bindable patrol | Packet emitters |
| --- | ---: | ---: | ---: | ---: |
| 32-V Docker (family=1019, template=17649, level=3, pf=6553) | 1 | 0 | 0 | 0 |
| Eliseo Ye (family=103, template=26139, level=19, pf=6553) | 1 | 0 | 0 | 0 |
| Furniture Merchant (family=0, template=26137, level=104, pf=6553) | 1 | 0 | 0 | 0 |
| Garbage Flea (family=25, template=17657, level=5, pf=6553) | 1 | 1 | 1 | 1 |
| Garbage Flea (family=25, template=17657, level=6, pf=6553) | 3 | 3 | 2 | 1 |
| Janae Seaman (family=103, template=26149, level=23, pf=6553) | 1 | 1 | 0 | 0 |
| Omni-Pol Guard (family=2, template=26097, level=20, pf=6553) | 1 | 0 | 0 | 0 |

## Live packet route comparison

| Result | Packets |
| --- | ---: |
| `live_identity_metadata_unresolved` | 343 |
| `exact_promoted_route` | 3 |

- Comparable promoted timing matches: **0**.
- Comparable promoted timing deviations: **1**.
- Timing tolerance: **±0.250 seconds**.

## Manual original-client observations

- Visible NPC movement was confirmed.
- Attack behavior result was not supplied to this verifier.
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
