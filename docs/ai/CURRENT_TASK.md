# Current Task

## Active

### Capture-backed active enemy combat restoration

The full Violent Vagabond review found a generic miss-correlation defect:
`MissedAttackInfo` carries the observer/defender in its N3 source and the actual
attacker in a later embedded identity. The extractor now attributes and retains
that exact attacker, defender, SAW, Attack, miss shape, and packet order.
Regeneration recovers 41 raw observations, or 40 distinct misses after one
declared overlapping logger capture is deduplicated.

The authoritative PF127/PF1931 denominator remains `489` unique actors: `325`
are certified and `164` are quarantined. PF127 is `238/84`; PF1931 remains
`87/80`. Violent Vagabond remains `0/22`: the complete corpus contains no
Vagabond-owned landed, critical, or terminal `AttackInfo`, so miss evidence
cannot prove hit type, damage type, landed weapon slot/instance, or lethal
result semantics. All 22 actors remain fail-closed with zero compatible
generated contracts. See
`docs/evidence/SUBWAY_VIOLENT_VAGABOND_COMBAT_RESTORATION_20260726.md`.
