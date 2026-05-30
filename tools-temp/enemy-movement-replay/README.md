# Enemy Movement Replay Fixture

This is a small contract guard for the current enemy movement rule set. It is not final pathfinding and it is not a new AI system.

It protects the behavior Mike called out during playtest:

- If the target is out of attack range, keep chasing with coordinate `FollowTarget`.
- If the target is in melee range, melee attacks can happen while moving.
- If the target is in ranged or nano range, stop and attack.
- Player `StopFight` does not clear NPC retaliation by itself.
- Hard correction preserves chase and must use the captured correction sequence.
- Clear target only on target invalid, death, zoning, or hate wipe style events.

The sample CSV uses:

- Official live chase evidence from `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260529-212034`.
- The correction sequence documented in `tools-temp/live-combat-chase-observations/README.md`.
- Contract-only rows for ranged-vs-melee behavior until a focused ranged enemy capture is converted.

Run it from repo root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools-temp\enemy-movement-replay\Test-EnemyMovementReplay.ps1
```

The useful next step is to mine one real combat window into this CSV format, then wire runtime movement decisions to this contract shape instead of patching chase code during live playtest.
