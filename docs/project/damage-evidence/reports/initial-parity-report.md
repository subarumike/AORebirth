# Initial Weapon Damage Parity Report

Status: underdetermined.

No live ordinary weapon-hit observations were fabricated for this report. The repository now has a report-only observation schema, validator, candidate evaluator, and synthetic fixtures for deterministic test coverage, but those fixtures are not AO formula proof.

## Candidate families represented

- AR ordering: `base + truncate(base * AR / 400)`, `truncate(base * (400 + AR) / 400)`, and explicit multiplier.
- AC ordering: absent, subtract truncated AC/10 before floor, subtract after floor, and critical-bonus interaction tags.
- Minimum floor: after-AC floor versus disabled/report-only alternatives.
- Add damage: before/after/floor-adjacent tags plus AR-scaled and non-AR-scaled variants.
- Critical: maximum-plus-bonus, roll-plus-bonus, scaled/unscaled bonus, AC-reduced bonus, and critical floor tags.
- AMSCap: missing means no cap, zero means no cap, zero means literal zero, negative invalid, and cap-before-post-1000 tags.

## Current conclusion

No ordinary weapon formula is proven.

The captured Subway Thief remains a fixed captured behavior at `9` damage and is not used to infer AR, AC, critical, or add-damage formulas.

## Missing observations

- base roll variation
- attack-rating variation
- target AC variation
- minimum-floor boundary
- critical versus normal
- type-specific add damage
- possible universal add damage
- AMSCap boundary
- single-skill weapon
- multi-skill weapon
- AR below 1000
- AR exactly 1000
- AR above 1000

## Promotion risk

Activating a formula before these observations are complete risks changing Subway Thief, NPC, pet, or player damage without proof. Production must remain legacy/fixed until the promotion gate in `operator-observation-matrix.md` is satisfied.
