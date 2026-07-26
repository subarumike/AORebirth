# Current Task

## Active

### Capture-backed active enemy combat restoration

The three active level-32 `MonsterData 26149` Temple Cultists now use generated
profile `b07cc6a46f13664f-8e90c740f8e6bbe0` from capture
`20260721-052115`. The exact equipped-weapon and packet semantics remain
capture-bound while the existing item and combat systems own damage, range,
cadence, Energy, ammunition, and mutable weapon state. Active PF127/PF1931
certification is now `376` ready and `113` quarantined. Continue with the next
quarantined family whose only blocker is production-owned or mutable data being
treated as identity; real weapon, stream, mode, special, nano, slot, hit-type,
and damage-type differences remain fail-closed.
