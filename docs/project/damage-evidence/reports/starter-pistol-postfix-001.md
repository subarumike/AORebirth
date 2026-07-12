# Starter Pistol Postfix Evidence Session `starter-pistol-postfix-001`

Scope: corrected legacy behavior check only. This report does not prove original AO AR, AC, critical, add-damage, AMS-cap, PvP, reflect, absorb, or special-attack formula parity.

## Session

- Session id: `starter-pistol-postfix-001`
- Commit under test: `d4b152f98f303578567fd8ccf106901f9d65958f`
- Weapon: QL1 Solar-Powered Pistol
- Template: `121567`
- Resolved damage range: `2-18`
- Target type: Arete `Malfunctioning Cleaning Robot`
- Raw private evidence path: `.local\weapon-damage-evidence\starter-pistol-postfix-001\`

## Result

- Captured raw events: `13`
- Valid observations after overkill-aware validation: `13`
- Incomplete observations: `0`
- Rejected observations: `0`
- Observed emitted damage values: `9, 18, 6, 18, 5, 17, 15, 8, 7, 8, 11, 2, 11`
- Observed emitted damage range: `2-18`
- Every valid hit remained within corrected legacy range `2-18`: yes
- `legacyDamageBonus` in the active evidence rows: `0`
- Valid equipped-weapon range bypassed the player fallback floor: yes
- Health deltas matched emitted damage after accounting for lethal overkill clamping to zero HP: yes
- Duplicate or overlapping events observed: no
- Remaining production damage defect proven: no

## Notes

The evidence tool was corrected during this session to treat lethal overkill as valid when `targetHealthAfter=0` and the health delta equals `min(observedDamage, targetHealthBefore)`. This is a tooling-only validator correction; production damage was not changed by that validator update.

The report-only candidate comparison remains underdetermined because all inactive candidate formulas still match this low-AR, zero-AC sample. More distinguishing observations would be required before making any original-AO formula claim.
