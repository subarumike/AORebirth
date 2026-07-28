# Subway Filth Flea Combat Restoration - 2026-07-26

## Completion update - 2026-07-28

The nine actors previously left quarantined are now restored, so all `51/51`
active PF127 Filth Fleas are capture-certified:

- L7: `0x795317F5`, `0x7953AD30`, `0x7953AFAA`, `0x7953AFC4`
- L8: `0x7953AD2F`, `0x7953AD36`
- L14: `0x7953A9C6`
- L15: `0x7953A9C2`, `0x7953AA0B`

The reusable numeric setup is
`filth-flea-saw-bounded-level-piecewise-v1`, bounded to L4..L21:

```text
L4..L10: floor((21 * actorLevel + 28) / 4)
L11..L21: 6 * actorLevel - 1
```

It reproduces every stable captured `SpecialAttackWeapon.Unknown1..4`
observation exactly: L4 `28`, L5 `33`, L6 `38`, L10 `59`, L11 `65`, L12
`71`, L13 `77`, L16 `95`, L19 `113`, L20 `119`, and L21 `125`. The newly
bound levels produce L7 `43`, L8 `49`, L14 `83`, and L15 `89`. Leave-one-out
checks pass for every captured level. The independent L19 observation with
`Unknown2=141` is retained as ordered mutable generation-local state; the
independent same-level initial stream and the stable cross-level result are
`113`.

Capture remains authoritative for natural mode, EPAH `201059/201060`
tag/instance `1162887496` in slot `1`, AZUS `201056/201057` tag/instance
`1096439123` in slot `0`, hit wire `3`, normal damage wire `0`, terminal
damage wire `4`, N3/action fields `0`, the two scheduled streams, and
`SpecialAttackWeapon -> Attack -> AttackInfo` ordering. Production continues
to own level, damage, range, cadence, Energy/ammunition, and mutable SAW state.
No nearest-level selection, source-identity whitelist, generic fallback,
cross-family reuse, or duplicate contract was added.

The canonical semantic profile is
`218eb3509f2be66b-12f99a4c2f732061`. Formula evidence uses capture sessions
`20260708-004038`, `20260708-143600`, `20260709-193914`,
`20260709-225408`, and `20260720-051714`.

The fixed PF127/PF1931 checkpoint moves from `455/34` to `464/25` of `489`.
PF127 moves from `290/32` to `299/23`; PF1931 remains `165/2`.

The sections below preserve the earlier 2026-07-26 exact-level restoration
checkpoint and are superseded only where this completion update says so.

## Result

PF127 contains 51 active `Filth Flea` actors with `MonsterData 17657`.
Thirty actors were already certified. Twelve level-5 actors now resolve generated
profile `218eb3509f2be66b-12f99a4c2f732061`, bringing the family to 42 certified
and 9 quarantined actors.

The apparent third level-5 stream is not a third scheduled weapon. It is one
lethal `AttackInfo` outcome on the existing slot-0 AZUS arm stream. Raw packet
`20260708-004038 IN #9137` has `damageTypeWire=4`, slot `0`, instance
`1096439123`, and hit type `3`; at the identical timestamp it is followed by
`StopFight` for both actors and `CharacterAction action=99` for the target. The
next packets start a new SAW/Attack target context. The same terminal-result
field transition is present on the local player's final hits in
`20260708-004038`, `20260708-143600`, and `20260709-193914`. It therefore has
no independent cadence.

## Active actor reconciliation

All actors below are PF127, `Filth Flea`, `MonsterData 17657`.

| Level | Active identities | Result |
| --- | --- | --- |
| 4 | `7953AEEA`, `7953AEFC`, `7953AF18` | 3 certified |
| 5 | `795313FC`, `79531752`, `79531754`, `7953AD2C`, `7953AD3E`, `7953AEEE`, `7953AF04`, `7953AF10`, `7953AF57`, `7953AFAE`, `7953AFC6`, `7953AFCC` | 12 newly certified |
| 6 | `7953174B`, `7953AD2B`, `7953AF22`, `7953AF4A` | 4 certified |
| 7 | `795317F5`, `7953AD30`, `7953AFAA`, `7953AFC4` | 4 quarantined |
| 8 | `7953AD2F`, `7953AD36` | 2 quarantined |
| 10 | `7953AD73`, `7953AD78` | 2 certified |
| 11 | `7953AD70`, `7953AD71`, `7953AD75`, `7953A9E7`, `7953A9EA`, `7953A9FC`, `79545227` | 7 certified |
| 12 | `79513A87`, `79513A8F` | 2 certified |
| 13 | `7953AA0C`, `7953A9E1`, `79513AAF`, `79513AC2`, `79545223` | 5 certified |
| 14 | `7953A9C6` | 1 quarantined |
| 15 | `7953A9C2`, `7953AA0B` | 2 quarantined |
| 19 | `79545191`, `7953AF6D` | 2 certified |
| 20 | `7953AF76` | 1 certified |
| 21 | `79531120`, `79531122`, `7953AF71`, `795451A4` | 4 certified |

## Exact level-5 packet contexts

Complete level-5 chains occur in capture sessions `20260708-004038`,
`20260708-143600`, and `20260709-193914`, with evidence sources `794ADBC4`,
`794ADC23`, and `79531761`.

The exact SAW declares:

- EPAH `201059/201060`, tag/instance `1162887496`, slot `1`;
- AZUS `201056/201057`, tag/instance `1096439123`, slot `0`;
- SAW N3 `0`, `Unknown1..4=33`, and capture-ordered mutable `Unknown5`;
- Attack N3/action `0/0`;
- landed hit type `3`.

The two scheduled attack phases are:

1. EPAH opening attack, slot `1`, instance `1162887496`, normal
   `damageTypeWire=0`. First-hit observations are `3.870076`, `10.560202`,
   `11.729213`, `3.280340`, and `3.647454` seconds. It has no same-fight repeat
   interval because it is the opening phase.
2. AZUS repeating attack, slot `0`, instance `1096439123`, normal
   `damageTypeWire=0`. First-hit observations are `5.419731`, `1.770053`,
   `3.920014`, and `5.224046` seconds. Same-stream landed intervals are
   `2.410574`, `2.090391`, `2.220324`, `2.390060`, `7.179422`, `2.430294`,
   `2.799280`, `2.890694`, and `2.853283` seconds.

The apparent third signature is slot `0`, AZUS instance `1096439123`, hit type
`3`, `damageTypeWire=4`, amount `11`, and first-observed delay `12.560702`
seconds. It has one observation, no repeat interval, and an exact same-timestamp
target-death boundary. It is a terminal outcome variant of phase 2, not a
cadence-bearing stream.

Production remains the cadence owner through
`CapturedEnemySpecialAttackSequenceDefinition` and
`NpcCombatTickCoordinator`: initial phase `3.65` seconds, EPAH opening recharge
`1.58` seconds, and AZUS repeating recharge `2.8` seconds. No timer or
Filth-Flea-specific scheduler was added. The generated profile supplies the
exact terminal `AttackInfoUnknown=4`; the shared coordinator selects it only
when its existing health calculation proves the hit lethal. Normal AZUS hits
remain `AttackInfoUnknown=0`.

## Unsupported levels

The nine remaining actors stay fail-closed:

- Level 7: four actors. `795317F5` has an attack prefix in
  `20260709-225408` but dies before its own landed `AttackInfo`. Earlier
  evidence source `7947A4E3` has landed results in `20260709-210452` without a
  same-capture preceding SCFU/SAW/Attack chain. The other active level-7
  identities have state/patrol evidence only.
- Level 8: two actors. Evidence source `794A17D6` has an orphan SAW/Attack
  prefix in `20260708-004038` with no landed `AttackInfo`; the active identities
  have state evidence only.
- Level 14: one actor. `7953A9C6` has state evidence but no complete combat
  chain.
- Level 15: two actors. `7953A9C2` is killed in `20260709-225408` before its own
  landed hit and otherwise appears as a combat target; `7953AA0B` has state
  evidence only.

There is no complete generated level-7, level-8, level-14, or level-15 combat
profile. No nearest-level, shared-MonsterData, identity, weapon, cadence, or
packet fallback was introduced.

## Counts

| Scope | Before | After |
| --- | --- | --- |
| PF127 | 226 certified / 96 quarantined | 238 certified / 84 quarantined |
| PF1931 | 87 certified / 80 quarantined | unchanged |
| Combined PF127/PF1931 | 313 certified / 176 quarantined | 325 certified / 164 quarantined |
| Filth Flea | 30 certified / 21 quarantined | 42 certified / 9 quarantined |

The denominator remains 489 unique actors. The focused resolver records one
result per actor, so 164 quarantined actors equal 164 rejection rows.
