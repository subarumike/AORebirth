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

## Remaining

1. Restart engines, live-validate: Insurance Terminal → `/terminate` Yes → Die → respawn at bind.
2. Live-validate death pool recover: kill mobs, pool shrinks 5%/kill + mob XP (char level &lt; 220).
3. Shadowknowledge amount in “Character stored” still 0.
4. Commit this work (previous “Fixed Insurance Terminal” commit only had `.vsidx` junk).
5. Continue Subway when Mike returns that priority.

## Constraints

- Do not invent fee formulas beyond capture-backed level×100 without a second capture.
- Do not change database schemas.
