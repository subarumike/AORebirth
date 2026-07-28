# Current Task

## Active

### Mathematical Fragmented Soul combat setup

Fragmented Soul MonsterData `203729` now uses the bounded
`fragmented-soul-saw-6L-minus-1-plus-2-floor-L-over-2-v1` setup generator.
Population state supplies actor level, QL, and the exact owner-linked equipped
weapon tuple. Across the proven L17..L21 domain:

```text
base = 6 * actorLevel - 1
SAW = base, base, base, base + 2 * floor(actorLevel / 2)
```

All 21 unique raw SAW packets, all eight complete semantic profiles, and five
leave-one-out evaluations are exact. Templates `123685/123686`,
`123686/123686`, `123687/123687`, and `123687/123688` remain separate
QL-selected positions in one `items.dat` interpolation family. Equipped mode,
slot `6`, instance `0`, one normal stream, action `0`, hit/damage wires `3/0`, and
`WIFU -> SAW -> Attack -> AttackInfo` remain capture-bound. Production retains
damage, range, cadence, health, Energy/ammunition, and mutable SAW state.

All ten active Fragmented Soul actors are certified; the six starting
quarantined actors are restored. PF127/PF1931 coverage is `357` certified and
`132` quarantined of `489` unique actors before this slice and `363/126`
afterward (`276/46` in PF127 and unchanged `87/80` in PF1931). The fixed
53-actor PF127 scope moves from `32/21` to `38/15`.

Evidence:

- `docs/evidence/SUBWAY_FRAGMENTED_SOUL_COMBAT_RESTORATION_20260728.md`
- `docs/generated/enemy_combat_setup_formula_dataset.json`
- `docs/generated/capture_backed_npc_combat_active_coverage.json`
- `docs/evidence/SUBWAY_REMAINING_COMBAT_COHORT_RESTORATION_20260726.md`
