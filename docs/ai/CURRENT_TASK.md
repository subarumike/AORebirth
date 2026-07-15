# Current Task

## Current Focus

Insurance Terminal + `/terminate` death XP pool (restored after git wipe) + respawn at bind.

## Done in this slice

- Restored files lost when git pull overwrote uncommitted Insurance/`terminate` work:
  - `savechar.cs` (SaveChar 53032), `ChatCommandTerminate.cs`, `ForcePlayerDeath`, Statel ACK, no debug Statel chat.
- `/terminate` Yes → uninsured XP loss + force death (no pre-zero HP / no “already dead” skip loop).
- Level &lt; 220: lost XP goes into UnsavedXP death pool; each kill awards mob XP + **5% of remaining pool** until pool is 0.
- Level 220+: still clips XP to insurance watermark; no death-pool recovery.
- SavedXP watermark preserved across kill/login normalize.
- Death XP loss also applied on normal combat player deaths.
- Captured two matching official-live client dumps for the `VetoPosition -> PosToRoom` crash, proved the exception is `std::bad_cast` (`Bad dynamic_cast!`), and extended the existing non-throwing RoomSpace wrapper to the fifth audited callsite in both client profiles. The rebuilt proxy package is installed in both approved client directories; in-game regression validation remains pending.
- Restarted Chat, Login, and Zone successfully after the client-guard build.

## Remaining

1. Live-validate: Insurance Terminal → `/terminate` Yes → Die → respawn at bind.
2. Live-validate death pool recover: kill mobs, pool shrinks 5%/kill + mob XP (char level &lt; 220).
3. Re-test the official-live location that repeatedly produced `N3!n3Playfield_t::PosToRoom+0x44` and confirm the fifth RoomSpace callsite prevents the crash.
4. Shadowknowledge amount in “Character stored” still 0.
5. Continue Subway when Mike returns that priority.

## Constraints

- Do not invent fee formulas beyond capture-backed level×100 without a second capture.
- Do not change database schemas.
