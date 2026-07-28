# Current Task

## Active

### Fixed-scope Subway combat completion

The remaining fixed 15-actor checkpoint is resolved. Fourteen actors are
restored:

- Incomplete Rebuild: `6/6`
- Workman Striker: `3/3`
- Molested Molecules: `3/3`
- Bloodcreeper: `1/1`
- Redundant Scan: `1/1`

Incomplete Rebuild and Molested Molecules use bounded equipped formula domains.
Workman Striker and Redundant Scan use the exact active atomic generation.
Bloodcreeper uses its captured Bite/Spit dual-stream contract with
production-owned timing/range and mutable state. Exact weapon family, mode,
slot, instance, stream structure, action, hit/damage type, and packet order
remain capture-bound.

Stim Fiend `0x7957E415`, MD `203739`, L9 remains quarantined. Capture
`20260710-202132` contains SCFU `IN 1016` and movement/despawn only; WIFU, SAW,
Attack, AttackInfo, and MissedAttackInfo are absent. The existing Stim formula
remains bounded to L10..L17.

PF127/PF1931 coverage is now `377/112` of `489` unique actors (`290/32` and
unchanged `87/80`). The fixed 53-actor Subway checkpoint is `52/1`.

Evidence:

- `docs/evidence/SUBWAY_FIXED_SCOPE_COMBAT_COMPLETION_20260728.md`
- `docs/generated/enemy_combat_setup_formula_dataset.json`
- `docs/generated/capture_backed_npc_combat_active_coverage.json`
- `docs/evidence/SUBWAY_REMAINING_COMBAT_COHORT_RESTORATION_20260726.md`
