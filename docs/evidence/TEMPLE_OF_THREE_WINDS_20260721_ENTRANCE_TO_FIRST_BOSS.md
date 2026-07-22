# Temple of Three Winds entrance-to-first-boss capture evidence

## Scope

This first Temple of Three Winds implementation slice is limited to regular
Cultists observed from the entrance through the hallways leading into the first
boss room. It does not implement, simulate, or infer the first boss. The five
finalized official-live capture folders are:

- `20260721-030515`
- `20260721-031913`
- `20260721-032247`
- `20260721-032547`
- `20260721-033006`

All five captures pass the capture analyzer and NPC lifecycle decoder without a
recapture or offline-decode requirement. Private-server and official-live
extended-location evidence establishes resource `1931` as the Temple room
content binding. PF647 is the preceding transfer/gateway, while the observed
`Playfield2:938000` value is a live runtime instance and is not used as the
server content key.

## Dedicated dungeon boundary

Temple content is owned by
`CapturedTempleOfThreeWindsContentProvider`. It contributes only `totw.*`
profiles and spawns for playfield `1931`. Existing Subway construction remains
owned by its PF127 providers. The common ordinary-enemy catalog and lifecycle
services are shared infrastructure, not shared dungeon data.

Named NPCs, Acolytes, Caska, Defenders, and bosses seen in the capture stream
are deliberately excluded from this ordinary-enemy provider. They require
their own identity-linked behavior evidence and encounter modules.

## Captured population

The five sessions contain `155` unique live Cultist identities. Deduplicating
same-`MonsterData` positions within `1.5` world units produces `122` spawn
anchors. The initial provider preserves each anchor's captured position,
heading, level, health, scale, run speed, SCFU flags, exact SCFU unknown bytes,
and source capture. Sixteen anchors include an observed two-point movement
route and are represented as patrols; the other `106` are static anchors.

Seven exact Cultist visual profiles are retained:

| MonsterData | Appearance | Head mesh | Body mesh | Corpse CATMesh |
| ---: | ---: | ---: | ---: | ---: |
| 26074 | 1579 | 40691 | 204735 | 17532 |
| 26082 | 1835 | 40634 | 96330 | 17528 |
| 26103 | 1419 | 40103 | 30224 | 23365 |
| 26135 | 1611 | 40271 | 81802 | 23378 |
| 26137 | 1867 | 40209 | 204735 | 5934 |
| 26147 | 1643 | 40172 | 99144 | 17905 |
| 26149 | 1899 | 40151 | 99154 | 5941 |

All profiles preserve the common captured Cultist textures, NPC family, visual
flags, and exact SCFU appearance shape.

## Combat, aggression, and chase

- Sixty normal local-player hits establish a `15..32` ordinary damage envelope.
  Two criticals at `42..58` remain report-only.
- Twenty-four repeat intervals have a `4.635295`-second median. The captured
  AttackInfo shape is weapon slot `6`, unknown `0`, weapon instance `0`, and
  normal hit type `3`; the bounded shared packet shape uses ammo count `-1`.
- Cultist combat starts with an empty `SpecialAttackWeapon` packet followed by
  `Attack`, not a bare `AttackInfo`. All levels `20..35` have observed context:
  fields 2/3 are `305,320,336,351,367,382,400,416,434,450,468,484,502,518,535,552`
  by level; field 1 matches those values except MonsterData `26135`, which has
  the captured `+20`; field 4 is `12,12,13,13,14,14,16,16,17,17,18,18,19,19,20,20`;
  field 5 uses an observed zero state. Nineteen direct attack-start samples up
  to 3.2 seconds have a `2.129326`-second median to first successful hit.
- Omitting that pre-hit context or replacing slot `6` / instance `0` with
  generic unarmed-hand tags makes the current client report nanobot-driven
  `unknown damage`; those abbreviated packet paths are not capture parity.
- Enemy-first fights prove automatic aggression, chase, and return behavior.
  The exact acquisition threshold is unresolved, so the first private-server
  policy uses the existing conservative seven-meter ordinary aggro radius.
- Observed survivors chased as far as `60.421` world units and returned to
  approximately one unit from their spawn anchors.
- PF1931 has no promoted collision/navigation provider. Temple Cultists use the
  generic chase owner and do not reuse PF127 Subway collision assumptions.

## Respawn, loot, and credits

Capture `20260721-033006` contains seven complete death-to-new-identity
intervals from `309.935` through `310.408` seconds. Because the current engine
removes a dead NPC after ten seconds, the Temple policy schedules respawn `300`
seconds after `NpcDespawn`, preserving the observed approximately `310` seconds
from death to replacement.

Strict identity-linked first opens cover `74` Cultist corpses: `17` positive
and `57` empty. Item membership and weights are scoped to the exact
`MonsterData` profile. Captured items are IDs `204571`, `204711`, `204712`,
`204720`, and `204721`, all at QL1. Profiles with no positive captured result
remain empty-only rather than borrowing another Cultist profile's pool.

All `74` positive-credit corpse outcomes agree on the exact level mapping from
level 20 (`371`) through level 35 (`705`). Profile-level combinations directly
seen in the captures are marked observed; unobserved combinations use the
same-name 74-outcome mapping as an explicit policy.

## Deliberate limits requiring later evidence

- The first boss, named NPCs, their drops, and boss-room encounter scripting.
- Exact automatic and social aggro thresholds.
- PF1931 collision geometry and wall-aware line of sight/navigation.
- Exact leash/reset distance and timing beyond the captured chase/return proof.
- Temple-specific empty, unlooted, and looted corpse lifetimes; the current
  ordinary project defaults remain in use.
- Rooms and branches beyond this capture slice.
