# Def-Agg Tutorial Trigger Analysis - 2026-07-08

## Scope

This report isolates the remaining Def-Agg tutorial trigger after two live-compatible packet field fixes:

- `55e2d1c7`: player attack echo `AttackMessage.Unknown = 0`
- `0c711de0`: player and NPC `AttackInfoMessage.Unknown = 0`

No combat packet field was changed in this analysis because the exact remaining trigger is not proven by the captures currently available on disk.

## Captures inspected

- Official live reference: `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260708-143600`
- Latest stored AORebirth Subway capture: `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260708-185543`
- Older AORebirth combat reference, before the two fixes: `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260707-172254`

## Important capture availability finding

The latest stored AORebirth Subway capture, `20260708-185543`, is not a usable post-fix combat-start capture:

- `capture_info.json` reports `enemyTrackedEntities = 0`
- `capture_info.json` reports `enemyCombatRows = 0`
- `capture_info.json` reports `enemyFightCaptureStarted = false`
- `events.log` has no `type=Attack` rows
- `events.log` has no `type=AttackInfo` rows
- `system-messages.log` has no Def-Agg tutorial text

Because of that, there is no current post-`0c711de0` AORebirth packet timeline on disk showing the Subway Thief attack where Mike still saw the tutorial line.

## Official live combat-start timeline

Reference: `20260708-143600/events.log`, first local-player attack sequence.

Timeline:

1. `19:36:05.3774665Z` OUT `CharacterAction InfoRequest`
   - source `SimpleChar:7944C065`
   - target `SimpleChar:794DF18C`
2. `19:36:05.3774665Z` OUT `LookAt`
3. `19:36:05.4539655Z` IN `InfoPacket`
   - target `SimpleChar:794DF18C`
4. `19:36:06.3332987Z` OUT `Attack`
   - source `SimpleChar:7944C065`
   - target `SimpleChar:794DF18C`
   - `Unknown1 = 0`
   - base `Unknown = 0`
5. `19:36:06.3833560Z` through `19:36:06.3833560Z` IN unrelated NPC `FollowTarget` patrol packets
6. `19:36:06.5639184Z` IN `SpecialAttackWeapon`
   - source `SimpleChar:7944C065`
7. `19:36:06.5639184Z` IN `Attack`
   - source `SimpleChar:7944C065`
   - target `SimpleChar:794DF18C`
   - `Unknown1 = 0`
   - base `Unknown = 0`
8. `19:36:07.1484068Z` IN `Stat`
   - source `SimpleChar:7944C065`
   - `CurrentNano = 167`
9. `19:36:07.9337305Z` IN `AttackInfo`
   - source `SimpleChar:7944C065`
   - target `SimpleChar:794DF18C`
   - `Amount = 22`
   - `WeaponSlot = 8`
   - `Unk1 = 0`
   - base `Unknown = 0`
10. `19:36:07.9337305Z` IN `SpecialAttackWeapon`
    - source `SimpleChar:794DF18C`
11. `19:36:07.9337305Z` IN `Attack`
    - source `SimpleChar:794DF18C`
    - target `SimpleChar:7944C065`
    - base `Unknown = 0`
12. `19:36:07.9337305Z` IN `Stat`
    - source `SimpleChar:7944C065`
    - `NumFightingOpponents = 1`
    - base `Unknown = 1`

The official live capture does not contain the Def-Agg tutorial text in `events.log` or `system-messages.log`.

## Older AORebirth combat-start timeline

Reference: `20260707-172254/events.log`, before commits `55e2d1c7` and `0c711de0`.

Timeline:

1. `22:24:47.9818715Z` OUT `CharacterAction InfoRequest`
   - source `SimpleChar:0014`
   - target `SimpleChar:782DE571`
2. `22:24:48.6208827Z` OUT `Attack`
   - source `SimpleChar:0014`
   - target `SimpleChar:782DE571`
   - `Unknown1 = 0`
   - base `Unknown = 0`
3. `22:24:48.6313904Z` IN `Attack`
   - source `SimpleChar:0014`
   - target `SimpleChar:782DE571`
   - `Unknown1 = 0`
   - base `Unknown = 1`
4. `22:24:48.6418959Z` IN `Stat`
   - source `SimpleChar:782DE571`
   - `Health = 0`
5. `22:24:48.6418959Z` IN `AttackInfo`
   - source `SimpleChar:0014`
   - target `SimpleChar:782DE571`
   - `Amount = 15`
   - `WeaponSlot = 0`
   - `Unk1 = 0`
   - `WeaponInstance = 100`
   - base `Unknown = 1`
6. `22:24:48.6418959Z` IN `CharacterAction Death`
7. `22:24:48.6418959Z` IN `Stat`
   - source `SimpleChar:0014`
   - `XP = 60`
   - `UnsavedXP = 60`

This older AORebirth capture does not contain the Def-Agg tutorial text in `events.log` or `system-messages.log`.

## Packet differences established by available evidence

The two already-fixed fields were real live-vs-AORebirth differences in the older private capture:

- AORebirth attack echo base `Unknown = 1`; live uses `Unknown = 0`
- AORebirth player `AttackInfo` base `Unknown = 1`; live uses `Unknown = 0`

Mike's retest after both fixes proves those differences were not the complete trigger.

Other observed differences remain candidates only:

- Live sends `SpecialAttackWeapon` for the player before the local-player attack echo; the older AORebirth combat capture did not.
- Live sends `SpecialAttackWeapon` for the target before target attack state; the older AORebirth combat capture did not reach an equivalent normal target counterattack because the test leet died immediately.
- Live sends `NumFightingOpponents = 1` after the target starts attacking; the older AORebirth instant-kill sequence did not have the same sustained-combat state.
- The current post-fix Subway Thief retest packet sequence is not present in a stored capture, so these candidates cannot be confirmed against current server behavior.

## Why the previous fixes failed

The previous two fixes were based on real live-compatible field differences, but they targeted packet fields that were not proven to be the Def-Agg tutorial trigger. Mike's in-client retest after `55e2d1c7` and `0c711de0` disproved the hypothesis that either `AttackMessage.Unknown` or `AttackInfoMessage.Unknown` alone caused the tutorial line.

## Current conclusion

The exact trigger is unresolved.

The most likely remaining candidates, in priority order, are:

1. Missing or mismatched player `SpecialAttackWeapon` context immediately before attack echo.
2. Missing or mismatched target `SpecialAttackWeapon`/target attack-start context before the first target hit.
3. Missing or late `NumFightingOpponents` client state update for sustained combat.
4. A login/full-character/tutorial client-state condition that is not visible in the currently inspected attack-start packets.

No code should be changed from this report alone. The next useful evidence is a fresh AORebirth capture, after commit `0c711de0`, of exactly:

1. Zone into Subway.
2. Target the Subway Thief.
3. Start attack.
4. Stop capture after the Def-Agg tutorial line appears and before additional unrelated actions.

That capture should be compared packet-by-packet against the `20260708-143600` live timeline above before changing another combat packet field.
