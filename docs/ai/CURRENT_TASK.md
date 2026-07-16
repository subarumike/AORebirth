# Current Task

## Current Focus

Bloodcreeper is now activated through the shared PF127 ordinary-enemy runtime from completed official-live captures. Private-client validation is next.

## Done in this slice

- Capture survey `20260709-222339` proves two ordinary Bloodcreeper spawns: level 25 with `724 HP`, and level 24 with `691 HP` plus its captured patrol path.
- Fight captures `20260716-033326` and `20260716-034104` prove proactive aggro, chase/retarget behavior, and independent Skinspider Bite (`SKW1`) and Skinspider Spit (`SKW2`) attack streams.
- The shared captured combat contract now rolls Bite damage `21..35` and Spit damage `21..41`, with their captured initial delays, roughly `7.4`-second independent cadence, exact special-attack templates, slots, and weapon-instance tags.
- Bloodcreeper was removed from the named-boss exclusion and regenerated as an ordinary captured-direct archetype with both exact spawn rows.
- A bounded private-server automatic-aggro radius of `7` units is enabled from the observed acquisition at about `6.25` units.
- The exact corpse CATMesh `26978` is mapped for MonsterData `30379`. Both level-24 fights prove `150` corpse credits; unobserved levels and item loot remain unresolved.
- Bloodcreeper uses the explicit private regular-enemy respawn policy of four minutes. This is a close-enough gameplay policy, not a claim of exact official-live timing.
- The combat evidence analyzer now retains the two new captures and reports both Bloodcreeper attack shapes instead of collapsing them into the older single-slot sample.
- The generated ordinary-content equivalence check, focused Subway tests, AORebirth.Core/ZoneEngine Debug build, and engine restart pass.

## Remaining

1. Private-client validate both Bloodcreeper spawns, proactive acquisition/chase, varied incoming damage, corpse visual, `150` level-24 credits, and four-minute respawn.
2. Keep Bloodcreeper outside the accepted whole-enemy gate until private validation succeeds and unresolved item-loot handling is explicitly accepted or extended from a larger kill sample.
3. Do not auto-attach or launch AO/capture tooling. Mike runs gameplay and supplies completed captures when requested.

## Constraints

- Bloodcreeper is ordinary content, not a unique boss or scripted encounter.
- Do not turn two observed empty item snapshots into proof of an empty item pool.
- The official-live respawn delay remains unresolved; the four-minute value is an explicit private-server regular-enemy policy.
- Existing working Subway combat, loot, corpse, respawn, navigation, and population behavior must remain unchanged.
