# Temple of Three Winds: Murial the Faithful Patrol

## Scope

- Resource/playfield: PF1931 (`Temple of Three Winds`).
- `20260721-232051` supplies Murial's exact SCFU, combat, and corpse evidence.
- `20260721-234614` supplies the identity-linked complete patrol path and timing.
- PF647 remains only the Temple transfer/gateway.

## Exact generation

- Identity: `(SimpleChar:7987F12D)`.
- Captured at `2026-07-22T04:36:41.2783126Z`.
- L34, 1,535 health, MonsterData `26090`, scale `102`, run-speed base `117`.
- Spawn: `(271.4782,14.8112507,445.842255)`.
- Heading: `(0,0.04537062,0,0.9989702)`.
- Appearance `1835`, head `40629`, textures `161711/161716/161706/161726`,
  and exact captured meshes `20091/40629/7818`.
- The runtime uses run-speed `118`, matching the existing captured-stat conversion
  used by the Temple ordinary provider.

## Complete patrol

`20260721-234614` contains 42 identity-linked `FollowTarget/NpcPath` movement
packets. They resolve to this 20-destination loop, repeated twice and beginning
a third time:

1. `(266.339355,16.0112476,513.76355)`
2. `(266.067688,16.611248,516.280029)`
3. `(269.56897,16.611248,519.147278)`
4. `(269.653076,16.611248,516.142517)`
5. `(269.670929,16.0112476,513.885925)`
6. `(269.878021,15.4112473,505.860809)`
7. `(270.092896,15.4112473,500.121277)`
8. `(270.125061,14.8112478,497.849426)`
9. `(270.672424,14.8112478,481.155884)`
10. `(270.995483,14.8112478,469.628174)`
11. `(272.536194,14.8112478,458.825562)`
12. `(271.761108,14.81108,446.147522)`
13. `(271.621277,14.8112478,459.03479)`
14. `(270.055664,14.8112478,469.769257)`
15. `(259.240417,14.8112478,474.301025)`
16. `(269.505005,14.8112478,481.484863)`
17. `(268.258362,14.8112478,497.378784)`
18. `(267.785797,15.4112473,500.433655)`
19. `(267.371582,15.4112473,505.721039)`
20. `(267.127625,16.0112476,508.234467)`

Waypoint 1 recurred at `04:46:16.2900537Z`, `04:48:00.5836473Z`, and
`04:49:47.6219091Z`. The two complete observed loops took `104.2935936` and
`107.0382618` seconds, mean `105.6659277` seconds. One duplicate emission of
waypoint 15 in the second loop is a movement update, not a new destination.

The private runtime uses the exact ordered spatial loop through the ordinary
waypoint patrol service. It does not use Subway's packet-timed patrol replay.
Therefore the official-live loop durations are preserved as evidence and need
private-client verification before exact timing reproduction can be claimed.
The captured clean SCFU flags were `0x020A4ACB`; runtime adds the established
`HasWaypoints` bit and emits `0x020B4ACB` for the active patrol.

## Combat and corpse boundary

- A later Murial generation in `20260721-232051` proactively attacked players
  and produced five exact 26-point normal hits at slot 6, ammo `-1`, weapon
  instance `0`.
- Attack start to first successful hit was about `1.5397` seconds. Observed
  repeat intervals were `3.8501`, `3.7214`, `3.7885`, and `3.7896` seconds;
  runtime uses the observed `3.7885` interval as its bounded cadence.
- Empty `SpecialAttackWeapon` metadata was `258/258/258/21/0`.
- Nano `70294` was observed, but its effect ownership remains unresolved and is
  not reproduced.
- Exact corpse CATMesh is `5927`. One unlooted corpse persisted for about
  `182.649` seconds, represented by the existing 180-second corpse lifetime.

## Unresolved

- Murial-specific respawn timing. Runtime uses the established shared Temple
  ordinary policy of 300 seconds after NPC despawn; that is policy, not a
  Murial-specific measurement. The policy is now assigned explicitly to
  Murial and resets his original anchor, health, movement, aggression, and
  single population-owned patrol worker.
- Loot contents and L34 credits. No identity-linked loot inventory was opened.
- Nano `70294` target choice, cadence, and downstream effect. Its captured
  packet identity is retained, but it remains unscheduled rather than being
  emitted with invented gameplay behavior.
- Exact official-live patrol-loop timing reproduction on the private runtime.
