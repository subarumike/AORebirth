# Current Task

## Active

### Mathematical Stim Fiend combat setup

Stim Fiend MonsterData `203739` now uses the bounded
`stim-fiend-siw1-floor-11L-minus-2-over-2-v1` setup generator. The actor level
comes from the existing PF127 population row; no actor identity or
user-supplied level participates. For levels `10..17`, SAW numeric fields 1-4
are `floor((11 * level - 2) / 2)`, reproducing captured L10/L11/L12/L13/L14
values exactly and generating L15=`81`, L16=`87`, and L17=`92`. The generator
fails closed outside the family, level, and exact SIW1 categorical domain.

Six of the seven fixed-scope Stim Fiends now resolve the capture-backed SIW1
packet archetype through the shared combat path. L9 remains fail-closed outside
the proven domain. The full active Stim family is `14/1`. PF127/PF1931 coverage
is `351` certified and `138` quarantined of `489` unique actors (`264/58` in
PF127 and unchanged `87/80` in PF1931). The fixed 53-actor PF127 scope is now
`26` certified and `27` quarantined.

Evidence:

- `docs/evidence/STIM_FIEND_COMBAT_SETUP_FORMULA_20260727.md`
- `docs/evidence/ENEMY_COMBAT_SETUP_FORMULA_20260727.md`
- `docs/generated/enemy_combat_setup_formula_dataset.json`
- `docs/evidence/SUBWAY_REMAINING_COMBAT_COHORT_RESTORATION_20260726.md`
