# Current Task

## Active

### Capture-backed active enemy combat restoration

The three active level-32 `MonsterData 26149` Temple Cultists remain certified
through generated profile `b07cc6a46f13664f-8e90c740f8e6bbe0` from capture
`20260721-052115`. A focused audit of every currently resolver-rejected Cultist
found no additional safely reusable cohort: 72 active rows have no complete
generated profile at their exact MonsterData/level, two have unresolved
same-template QL initialization ambiguity, and two have genuine cross-weapon
ambiguity. The largest structure-similar cohort (`MonsterData 26137`, 10 actors)
has six captured levels with six different exact `SpecialAttackWeapon`
initializations, so no adjacent level was substituted. The resolver enumerates
76 rejected Cultist rows, exposing a pre-existing six-row discrepancy with the
accepted 70-Cultist checkpoint; no production behavior or accepted
`376`-ready/`113`-quarantined metric changed in this evidence-only slice. See
`docs/evidence/TEMPLE_CULTIST_COMBAT_QUARANTINE_20260726.md`.
