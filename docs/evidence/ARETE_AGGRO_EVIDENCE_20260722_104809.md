# Arete NPC-first Aggro Evidence — 20260722-104809

This report uses exact `Attack` packets where the captured source role is `enemy` and target role is `local-player`, resolved through complete-capture SCFU metadata.

- Exact enemy-to-local-player attack starts: **18**.
- No local-player attack to the same NPC in the preceding 30 seconds: **7**.
- Attack starts with a nearby decoded NPC path: **10**.

- Attack starts with direct NPC/local-player positions: **15**.

| NPC constraint | Starts | NPC first | Direct attack-start distance (m) | Nearby path spans (m) |
| --- | ---: | ---: | --- | --- |
| 32-V Docker (family=1019, template=17649, level=3, pf=1044525) | 1 | 0 | 5.617..5.617; median 5.617 | 3.996..3.996; median 3.996 |
| Cleaning Robot (family=1019, template=297023, level=1, pf=1044525) | 4 | 0 | 1.814..5.577; median 3.687 | 5.114..5.635; median 5.374 |
| Cleanmeister Intelligence Robot (family=1019, template=297023, level=2, pf=1044525) | 1 | 1 | 0.847..0.847; median 0.847 | 7.700..7.700; median 7.700 |
| Dockworker (family=137, template=26137, level=3, pf=1044525) | 1 | 0 | 2.372..2.372; median 2.372 | none |
| Garbage Flea (family=25, template=17657, level=1, pf=1044525) | 3 | 3 | none | 8.707..8.707; median 8.707 |
| Garbage Flea (family=25, template=17657, level=2, pf=1044525) | 1 | 1 | 15.576..15.576; median 15.576 | 2.087..2.087; median 2.087 |
| Supreme Collector of Waste (family=1019, template=17714, level=4, pf=1044525) | 1 | 1 | 4.145..4.145; median 4.145 | 3.081..3.081; median 3.081 |
| Waste Collector (family=1019, template=17714, level=2, pf=1044525) | 6 | 1 | 1.324..15.053; median 3.124 | 3.906..13.511; median 4.325 |

## Boundary

- NPC-first attack packets prove automatic hostility for the exact metadata constraint.
- Direct attack-start distance uses the latest preceding decoded outbound local-player `CharDCMove` coordinate and NPC `enemy-state` coordinate.
- NPC-first direct distances are lower-bound observations for automatic aggro; they do not prove a larger unobserved radius.
