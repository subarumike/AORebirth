# Current Task

## Active

### Capture-backed active enemy combat restoration

Slum Runner and Shadow now reuse their exact DBPW and DMXF natural packet
archetypes across every compatible active level, restoring `18` and `8`
previously quarantined actors. Existing production owners retain level, health,
damage, range, cadence, loadout, ammunition, and mutable combat state. Active
PF127/PF1931 certification is now `330` ready and `159` quarantined. Continue
with the next quarantined family whose only blocker is production-owned or
mutable data being treated as identity; real weapon, stream, mode, special,
nano, slot, hit-type, and damage-type differences remain fail-closed.
