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
- Patrol and chase movement are outside this combat-identity repair.

## Delivery acceptance

- Generated combat cohort: 1,534 actors, 1,520 bindings, maximum actor index
  1,536; 559 certified and 975 unresolved; Arete family 52/96 and additional
  Arete bindings 4/17.
- Arete behavioral combat gate: PASS (5/5).
- Active-coverage acceptance: PASS (7/7).
- Combat-profile catalog acceptance: PASS (51/51).
- Complete AOtomation suite: PASS (998/998).
- Debug server build: PASS.
- Chat/Login/Zone restart: PASS (Chat 6996/7012, Login 7500, Zone 7501).
- Original-client smoke testing remains a focused manual follow-up; the agent
  did not launch the AO client.
