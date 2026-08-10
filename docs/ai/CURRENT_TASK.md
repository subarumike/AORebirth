# Current Task

## Active

Restore capture-backed regular-mob combat in Arete without reverting the
current runtime or weakening fail-closed behavior.

## Supported exact combat profiles

- Alex area: 15 level-2 Waste Collectors, three level-2 Garbage Fleas, and one
  level-2 Cleanmeister.
- Junkyard: 14 level-1 Cleaning Robots.
- Lorelei Oasis: ten level-6 Desert Reets and nine level-5/6 Rollerrats.
- Landing: four ICC Peacekeepers.
- Runtime actors retain their runtime identities. Explicit capture source and
  combat-profile selectors are resolver and loot-routing evidence only.

## Expected exclusions

- Engineer Automaton I remains intentionally quarantined because the corpus has
  no exact certified combat profile; it cannot inherit Docker combat or loot.
- Robotic Guard Dog and the incomplete cleaning-robot variants remain passive
  where exact range, cadence, damage, or complete combat-state evidence is
  absent.
- Patrol movement remains outside this slice. Melee chase/range behavior is
  covered for the supported Arete regular-mob combat contracts.

## Delivery acceptance

- Generated combat cohort: identity
  `041b9dc66bed5ddf2b50277d54232173a1b1d2f80196e721f50c38f138f1f1d5`;
  1,534 actors, 1,520 bindings, maximum actor index 1,536; 559 certified and
  975 unresolved. PF6553/PF8009 active coverage has 100 bindings / 113 actors;
  43 certified bindings cover 56 actors and 57 bindings remain explicit
  unresolved/quarantined exclusions.
- Arete behavioral combat gate: PASS (7/7).
- Active-coverage acceptance: PASS (7/7).
- Combat-profile catalog acceptance: PASS (53/53).
- Complete AOtomation suite: PASS (1003/1003).
- Debug server build: PASS.
- Chat/Login/Zone restart: PASS (Chat 6996/7012, Login 7500, Zone 7501).
- Original-client smoke testing remains a focused manual follow-up; the agent
  did not launch the AO client.
