# Current Task

## Active

### Capture-backed active enemy combat restoration

The full Filth Flea corpus proves that the level-5 profile contains two
scheduled weapon phases plus one terminal result variant, not three independent
cadences. Generated profile `218eb3509f2be66b-12f99a4c2f732061` now marks the
single slot-0 `damageTypeWire=4` observation as terminal-only because its raw
`AttackInfo` is followed at the identical timestamp by target
`CharacterAction action=99`. The shared health/combat path emits that exact
field only for a lethal AZUS hit; ordinary AZUS hits remain wire value `0`.
This restores all 12 active level-5 actors without adding a timer or fallback.

The authoritative PF127/PF1931 denominator remains `489` unique actors: `325`
are certified and `164` are quarantined. PF127 is `238/84`; PF1931 remains
`87/80`. Filth Flea is `42` certified and `9` quarantined. The remaining level
7, 8, 14, and 15 actors have no complete same-generation combat chain and stay
fail-closed. See
`docs/evidence/SUBWAY_FILTH_FLEA_COMBAT_RESTORATION_20260726.md`.
