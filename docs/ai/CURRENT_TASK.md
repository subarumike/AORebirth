# Current Task

## Active

### Capture-backed active enemy combat restoration

The three active level-32 `MonsterData 26149` Temple Cultists remain certified
through generated profile `b07cc6a46f13664f-8e90c740f8e6bbe0` from capture
`20260721-052115`. The focused `MonsterData 26137` production-source gate
rejected restoration: canonical item template `204747/204747` is one fixed QL1
record, ordinary spawn construction supplies no SAW values, equipped weapon
state supplies only WIFU/item state, and the shared packet factory copies
`Unknown1..4` from the selected capture contract. No existing authoritative
source reproduces the six captured initializations at levels 21, 23, 26, 28,
29, and 30, so all ten uncaptured-level actors remain quarantined.

Actor-based reconciliation now supersedes the stale incremental checkpoint.
The authoritative PF127/PF1931 denominator is `489` unique actors: `313`
certified and `176` quarantined. PF1931 contains `149` unique Cultists:
`73` certified and `76` quarantined, with exactly one rejection row per
quarantined actor. The prior `70` Cultist and `376/113` totals were stale
documentation, not duplicate runtime actors or inflated rejection rows. See
`docs/evidence/TEMPLE_CULTIST_COMBAT_QUARANTINE_20260726.md`.
