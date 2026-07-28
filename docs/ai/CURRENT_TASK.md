# Current Task

## Active

### Mathematical Melded Patterns combat setup

Melded Patterns MonsterData `203747` now uses the bounded
`melded-patterns-saw-floor-11L-minus-2-over-2-plus-28-v1` setup generator.
Population state supplies actor level, QL, and the exact owner-linked equipped
weapon tuple. Across the proven L18..L25 domain:

```text
base = floor((11 * actorLevel - 2) / 2)
SAW = base, base + 28, base, base
```

All 13 raw SAW packets, all 11 complete semantic profiles, and six
leave-one-out evaluations are exact. Templates `121817/121818`,
`121818/121818`, and `121819/121820` remain separate QL-selected positions in
one `items.dat` interpolation family. Equipped mode, slot `6`, instance `0`,
one normal stream, action `0`, hit/damage wires `3/0`, and
`WIFU -> SAW -> Attack -> AttackInfo` remain capture-bound. Production retains
damage, range, cadence, health, Energy/ammunition, and mutable SAW state.

All ten active Melded Patterns actors are certified; the six starting
quarantined actors are restored. PF127/PF1931 coverage is `357` certified and
`132` quarantined of `489` unique actors (`270/52` in PF127 and unchanged
`87/80` in PF1931). The fixed 53-actor PF127 scope is `32/21`.

Evidence:

- `docs/evidence/SUBWAY_MELDED_PATTERNS_COMBAT_RESTORATION_20260727.md`
- `docs/generated/enemy_combat_setup_formula_dataset.json`
- `docs/generated/capture_backed_npc_combat_active_coverage.json`
- `docs/evidence/SUBWAY_REMAINING_COMBAT_COHORT_RESTORATION_20260726.md`
