# Temple of Three Winds: Yatila Through Betany

Scope: PF1931 capture-backed room slice after Defender of the Three. PF647 is the preceding transfer/gateway. The runtime content in this slice is owned by the Temple module and does not share Subway or ICC encounter definitions.

## Capture set

- `20260721-041439`: Windcaller Yatila, local Eternal Sentinels, combat, nanos, chase/reset, corpse, loot.
- `20260721-042139`: Reverend Gulard, combat, nano, two chase/reset passes, two corpses, two loot snapshots.
- `20260721-042705`: level-59 Re-Animator generation and reset evidence.
- `20260721-043204`: exact level-60 Re-Animator generation, room adds, two reanimation casts, death, corpse, loot.
- `20260721-044256`: Acolyte Betany combat, nanos, chase/reset, corpse, loot.
- `20260721-052115`: broader later-room survey retained as evidence only; incidental deeper bosses are not activated by this slice.

All six captures passed strict SCFU decoding with zero failures. Lifecycle decoding allowed processing for every capture, with zero decode errors and every observed corpse/loot join linked.

## Activated named encounters

| Encounter | Exact generation | Aggro | Leash/runtime boundary | Captured nanos | Loot snapshots |
| --- | --- | --- | --- | --- | --- |
| Windcaller Yatila | L56, HP 13863, MD 26151 | proactive | 40-unit policy from two observed returns near 38-40 units | 205600, 205594, 205592 | 1 |
| Reverend Gulard | L38, HP 3052, MD 26147 | proactive | 40-unit policy from two observed returns near 38-40 units | 205584 | 2 identical |
| The Re-Animator | L60, HP 12441, MD 26155 | retaliation | 40-unit policy from captured reset | 205604 | 1 |
| Acolyte Betany | L32, HP 1734, MD 26143 | proactive | conservative 40-unit policy; capture proves chase beyond 35 units | 205383 | 1 |

Named respawn is an explicit Temple policy of 600 seconds after dead-NPC despawn where the new captures do not contain a complete respawn interval. Defender retains its capture-proven 600.193-second result. The shared 120-second unlooted named-corpse policy is explicit; Mike directly identified Defender and Re-Animator remains as `Temporary: 2m`.

## Re-Animator room behavior

The level-60 generation is the active atomic profile. The earlier level-59 observation remains evidence only.

Two level-18 Reanimated Corpse adds are encounter-owned at their captured room anchors. Nano `205604` requests one missing add replacement. The capture links each cast to a dead-add position and shows living replacement adds disappearing when The Re-Animator dies. Runtime cleanup therefore removes living encounter adds on boss death or leash reset.

The capture also shows one Eternal Sentinel appearing at a prior dead-sentinel position at a cast boundary. That wider corpse-selection behavior is not isolated well enough to enable; runtime reanimation is limited to the two proven Reanimated Corpse slots.

## Ordinary room content

Three captured Eternal Sentinel spawns are active:

- L18, HP 247 at `(92.95905, 12.187273, 290.411774)`.
- L20, HP 280 at `(89.83454, 11.4112511, 306.880341)`.
- L18, HP 247 at `(59.7886162, 13.16832, 283.302765)`.

They use the exact MD 41690 / mesh 81804 / CATMesh 41664 profile, captured 17-18 normal damage, and captured empty-loot evidence. The existing PF1931 ordinary 300-second post-NPC-despawn policy is reused and remains policy rather than a new respawn claim.

## Deliberately unresolved

- The reported `23 points of poison damage` is present as a HealthDamage row, but the packet source is the local player targeting The Re-Animator. It is not safe to implement as boss poison damage.
- Gulard's nearby positive health changes are not isolated well enough to assign a heal effect to nano `205584`; the cast and finish sequence is replayed without an invented stat delta.
- Named loot tables replay exact observed corpse snapshots. Drop probabilities and any wider pools remain unresolved.
- The broad `20260721-052115` survey does not authorize incidental deeper boss activation.
