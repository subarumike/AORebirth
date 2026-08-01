# Arete NPC-first Aggro Evidence — 20260722-152454

This report uses exact `Attack` packets where the captured source role is `enemy` and target role is `local-player`, resolved through complete-capture SCFU metadata.

- Exact enemy-to-local-player attack starts: **51**.
- No local-player attack to the same NPC in the preceding 30 seconds: **43**.
- Attack starts with a nearby decoded NPC path: **44**.

- Attack starts with direct NPC/local-player positions: **40**.

| NPC constraint | Starts | NPC first | Direct attack-start distance (m) | Nearby path spans (m) |
| --- | ---: | ---: | --- | --- |
| Angry Minibull (family=42, template=30360, level=8, pf=1044525) | 2 | 2 | none | 11.197..16.291; median 13.744 |
| Angry Minibull (family=42, template=30360, level=9, pf=1044525) | 2 | 2 | 3.659..16.404; median 10.031 | 15.847..15.847; median 15.847 |
| Angry Minibull (family=42, template=30360, level=10, pf=1044525) | 2 | 2 | 13.318..16.226; median 14.772 | 7.380..15.414; median 11.397 |
| Angry Minibull (family=42, template=30360, level=12, pf=1044525) | 2 | 2 | 1.738..13.393; median 7.566 | 6.022..13.048; median 9.535 |
| Angry Minibull (family=42, template=30360, level=13, pf=1044525) | 4 | 3 | 1.763..20.689; median 16.192 | 12.174..16.624; median 16.624 |
| Cleanmeister Intelligence Robot (family=1019, template=297023, level=2, pf=1044525) | 1 | 0 | 2.772..2.772; median 2.772 | 3.602..3.602; median 3.602 |
| Desert Reet (family=53, template=30365, level=5, pf=1044525) | 2 | 2 | 1.283..1.583; median 1.433 | 2.755..8.768; median 5.761 |
| Desert Reet (family=53, template=30365, level=6, pf=1044525) | 4 | 4 | 2.841..23.168; median 9.991 | 16.311..23.244; median 19.778 |
| Gnarl the Roller (family=55, template=17687, level=7, pf=1044525) | 2 | 2 | none | 13.765..13.765; median 13.765 |
| Kneebreaker Alfonzo Rizzolo (family=137, template=165196, level=4, pf=1044525) | 1 | 1 | none | none |
| Lolly the Reet (family=137, template=30365, level=10, pf=1044525) | 1 | 1 | 56.244..56.244; median 56.244 | 7.374..7.374; median 7.374 |
| Robotic Guard Dog (family=1019, template=17720, level=13, pf=1044525) | 2 | 2 | 7.096..10.535; median 8.816 | 8.768..10.453; median 9.611 |
| Rollerrat (family=55, template=17687, level=5, pf=1044525) | 11 | 9 | 3.003..16.639; median 4.093 | 2.459..18.823; median 11.203 |
| Rollerrat (family=55, template=17687, level=6, pf=1044525) | 11 | 9 | 1.996..18.747; median 12.980 | 3.665..16.438; median 14.364 |
| Supreme Collector of Waste (family=1019, template=17714, level=4, pf=1044525) | 1 | 1 | 9.241..9.241; median 9.241 | 8.086..8.086; median 8.086 |
| Violent Protester (family=103, template=203740, level=3, pf=1044525) | 1 | 1 | none | 2.889..2.889; median 2.889 |
| Waste Collector (family=1019, template=17714, level=2, pf=1044525) | 2 | 0 | 4.317..4.969; median 4.643 | 4.382..5.112; median 4.747 |

## Boundary

- NPC-first attack packets prove automatic hostility for the exact metadata constraint.
- Direct attack-start distance uses the latest preceding decoded outbound local-player `CharDCMove` coordinate and NPC `enemy-state` coordinate.
- NPC-first direct distances are lower-bound observations for automatic aggro; they do not prove a larger unobserved radius.
