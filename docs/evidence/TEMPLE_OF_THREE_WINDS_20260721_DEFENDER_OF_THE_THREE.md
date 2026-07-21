# Temple of Three Winds: Defender of the Three

## Scope

This evidence record covers the dedicated playfield-647 `Defender of the Three`
encounter from finalized official-live captures:

- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260721-035526`
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260721-040249`
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260721-040324`

All three captures pass the available analyzer/lifecycle checks. The first and
third contain complete SCFU projections; the middle capture supplies the
continuation of the first corpse-loot observation.

## Captured encounter identity

- Playfield resource: `647`
- Name: `Defender of the Three`
- Spawn position: `(173.1958, 31.9949989, 266.324951)`
- Heading: `(0, 0.0569359064, 0, 0.99837786)`
- Level: `42`
- Health: `7091`
- MonsterData: `38394`
- Scale: `104`
- Run speed: SCFU base `144`; dossier value `145`
- Flags: `0x022A4A43`
- Character flags: `268964353`
- Appearance: `1227` (`side=3`, `fatness=1`, `breed=6`, `sex=0`, `race=1`)
- NPC family: `136`
- SCFU unknown block:
  `00000000000000000000000003010001000100010001000000020000`

The two complete SCFU observations have different dynamic identities but the
same captured encounter data. The second Defender appeared `600.193` seconds
after the prior NPC-despawn packet. Runtime therefore schedules replacement
`600` seconds after NPC despawn, preserving the engine's separate ten-second
death-to-NPC-despawn phase.

## Combat behavior

- Both observed fights were initiated by the player. Retaliation is proven;
  automatic or social aggro is not.
- Both landed Defender weapon hits dealt exactly `43` damage.
- Captured attack context: ammo `-1`, weapon slot `0`, unknown `0`, weapon tag
  `1465538645` (`0x575A5855`).
- Captured special-weapon row: low template `205877`, high template `205878`,
  tag `1465538645`, name `WZXU`, unknown tuple `239/239/239/25/0`.
- The only measurable attack-to-first-landed-hit interval is `10.915985`
  seconds. Runtime uses that value as a bounded private-server timing policy;
  repeat weapon cadence remains unresolved.
- Defender cast nano `205389` four times and nano `205561` eight times. Observed
  cast durations were approximately `5.28395` and `6.1904` seconds. Median
  cast-start spacing was approximately `10.272` seconds, and the earliest
  attack-to-first-cast delay was `1.147246` seconds.
- Runtime reproduces the captured cast/finish packet cycle and observed `4:8`
  composition. It does not invent a nano stat target or effect because the
  HealthDamage traffic could not be isolated safely to either nano.
- Defender chased at least `34.469125` horizontal units from the captured
  anchor. No reset/return boundary was captured. Runtime retains the generic
  `40`-unit leash policy, with no invented PF647 navigation geometry.

## Corpse and loot

- Corpse name: `Remains of Defender of the Three`
- Corpse CATMesh: `38265`
- Credits: `1450`
- MonsterData: `38394`
- Unlooted corpse lifetime: `120` seconds, from the provided official-live
  `Temporary: 2m` observation.
- Looted cleanup was observed at approximately `1.340` and `1.214` seconds
  after the final successful loot acknowledgement. Runtime uses their midpoint,
  `1.277` seconds.

The two complete first-open outcomes are retained as exact atomic snapshots:

1. `204750 x1` QL1 and `204649 x1` QL1, plus `1450` credits.
2. `204750 x2` QL1 and `204649 x1` QL1, plus `1450` credits.

The wider item pool and selection probabilities remain unresolved. Runtime
selects only between these two captured whole-corpse snapshots and does not
independently roll their item rows.

## Runtime boundary

Defender is owned by the dedicated Temple encounter, combat, and loot classes
under `Core/Playfields` and is keyed only to `totw.defender_of_the_three` on
playfield `647`. Subway encounter and loot definitions remain independently
scoped to their `subway.*` keys on playfield `127`; only generic runtime
infrastructure is shared.

## Remaining capture gaps

- exact nano effects and affected stats
- repeat weapon cadence
- automatic/social aggro threshold
- reset and return behavior
- PF647 collision/navigation geometry
- complete loot pool and snapshot probabilities
