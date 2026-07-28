# Current Task

## Active

### PF1931 Temple ordinary-combat completion

All `80` actors in the starting PF1931 ordinary-combat quarantine were
re-evaluated against the existing generated profiles, exact active WIFU
loadouts, and bounded production formulas. `78` are restored:

- Cultist: `76/76`
- Eternal Sentinel L20 `0x7983FA26`: restored
- Murial the Faithful `0x7987F12D`: restored

The seven Cultist MonsterData families use their exact equipped weapon
families and one-stream packet semantics. Production derives only the bounded
L20..L35 SpecialAttackWeapon numeric setup; active WIFU data continues to own
template pair, QL, Energy, and slot. The two L18 Eternal Sentinels remain
quarantined because no complete same-level landed normal AttackInfo contract
exists for either active weapon loadout.

The fixed PF127/PF1931 checkpoint is now `455/34` of `489`: PF127 remains
`290/32`, while PF1931 moves from `87/80` to `165/2`.

Evidence:

- `docs/evidence/TEMPLE_ORDINARY_COMBAT_COMPLETION_20260728.md`
- `docs/evidence/TEMPLE_CULTIST_COMBAT_QUARANTINE_20260726.md`
- `docs/generated/enemy_combat_setup_formula_dataset.json`
- `docs/generated/capture_backed_npc_combat_active_coverage.json`
