# Current Task

## Active

### Mathematical ordinary-enemy combat setup

Disobedient Bot MonsterData `17649` now uses the bounded
`disobedient-bot-siw1-floor-19L-plus-28-over-4-v1` setup generator. The actor
level comes from the existing PF127 population row; no actor identity or
user-supplied level participates. For levels `5..10`, SAW numeric fields 1-4
are `floor((19 * level + 28) / 4)`, reproducing captured L5/L6/L8/L9/L10
values exactly and generating L7=`40`. The generator fails closed outside the
family, level, and exact SIW1 categorical domain.

All 12 previously quarantined active L6/L7/L9/L10 Disobedient Bots now resolve
the exact L8 capture-backed SIW1 packet archetype through the shared combat
path. PF127/PF1931 coverage is `345` certified and `144` quarantined of `489`
unique actors (`258/64` in PF127 and unchanged `87/80` in PF1931). The fixed
53-actor PF127 scope is now `20` certified and `33` quarantined.

Evidence:

- `docs/evidence/ENEMY_COMBAT_SETUP_FORMULA_20260727.md`
- `docs/generated/enemy_combat_setup_formula_dataset.json`
- `docs/evidence/SUBWAY_REMAINING_COMBAT_COHORT_RESTORATION_20260726.md`
