# Def-Agg Tutorial Trigger Analysis - 2026-07-08

## Scope

This report isolates the remaining Def-Agg tutorial trigger after three insufficient fixes:

- `55e2d1c7`: player attack echo `AttackMessage.Unknown = 0`
- `0c711de0`: player and NPC `AttackInfoMessage.Unknown = 0`
- `2e779266`: non-robot NPC first combat tick delayed instead of immediate

No combat packet field or timing was changed in this analysis because the exact remaining trigger is still not proven by the captures currently available on disk.

## Captures inspected

- Official live reference: `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260708-143600`
- Fresh AORebirth Subway combat capture before `2e779266`: `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260708-223814`
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

Capture `20260708-223814` is a usable Subway Thief combat-start capture, but it was the evidence used before `2e779266`. It is not a post-`2e779266` retest capture.

The capture folder list currently shows no newer completed capture after `20260708-223814`. Because of that, there is no current post-`2e779266` AORebirth packet timeline on disk showing the Subway Thief attack where Mike still saw the tutorial line.

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

## Fresh AORebirth Subway capture before `2e779266`

Reference: `20260708-223814/events.log`.

Timeline:

1. `03:38:25.6409478Z` OUT `CharacterAction InfoRequest`
   - source `SimpleChar:0014`
   - target `SimpleChar:F4258`
2. `03:38:25.6409478Z` OUT `LookAt`
3. `03:38:25.6499474Z` IN `InfoPacket`
   - target `SimpleChar:F4258`
4. `03:38:26.0059901Z` IN `Attack`
   - source `SimpleChar:0014`
   - target `SimpleChar:F4258`
   - `Unknown1 = 0`
   - base `Unknown = 0`
5. `03:38:26.0059901Z` OUT `Attack`
   - source `SimpleChar:0014`
   - target `SimpleChar:F4258`
   - `Unknown1 = 0`
   - base `Unknown = 0`
6. `03:38:26.0256754Z` IN `SetPos`
   - source `SimpleChar:F4258`
   - base `Unknown = 1`
7. `03:38:26.0266754Z` IN `AttackInfo`
   - source `SimpleChar:F4258`
   - target `SimpleChar:0014`
   - `Amount = 5`
   - `WeaponSlot = 0`
   - base `Unknown = 0`
8. `03:38:26.0266754Z` IN `Stat`
   - source `SimpleChar:0014`
   - `Health = 74`
   - base `Unknown = 1`
9. `03:38:26.0266754Z` IN `FollowTarget`
   - source `SimpleChar:F4258`
10. `03:38:26.0266754Z` IN `AttackInfo`
    - source `SimpleChar:0014`
    - target `SimpleChar:F4258`
    - `Amount = 15`
    - base `Unknown = 0`
11. `03:38:26.0266754Z` IN `Stat`
    - source `SimpleChar:F4258`
    - `Health = 100`
    - base `Unknown = 1`

The fresh AORebirth capture had only one `SpecialAttackWeapon` before combat in the ready/login block. It did not show the live combat-start sequence:

`OUT Attack -> IN SpecialAttackWeapon(player) -> IN Attack(player echo) -> IN player AttackInfo -> IN SpecialAttackWeapon(target) -> IN Attack(target) -> IN NumFightingOpponents=1`

## Packet differences established by available evidence

The two already-fixed fields were real live-vs-AORebirth differences:

- AORebirth attack echo base `Unknown = 1`; live uses `Unknown = 0`
- AORebirth player `AttackInfo` base `Unknown = 1`; live uses `Unknown = 0`

Mike's retest after both fixes proves those differences were not the complete trigger.

The third change, `2e779266`, targeted another real live-vs-AORebirth difference in `20260708-223814`: the server produced target `AttackInfo` and player `Health` damage almost immediately after combat start, while the official live capture establishes a combat-start context before the target's sustained attack state. Mike's retest after `2e779266` proves this was also not the complete Def-Agg trigger.

Other observed differences remain candidates only:

- Live sends `SpecialAttackWeapon` for the player before the local-player attack echo; the older AORebirth combat capture did not.
- Live sends `SpecialAttackWeapon` for the target before target attack state; the older AORebirth combat capture did not reach an equivalent normal target counterattack because the test leet died immediately.
- Live sends `NumFightingOpponents = 1` after the target starts attacking; the older AORebirth instant-kill sequence did not have the same sustained-combat state.
- The current post-fix Subway Thief retest packet sequence is not present in a stored capture, so these candidates cannot be confirmed against current server behavior.

## Why the previous fixes failed

The previous fixes were based on live-compatible differences, but they targeted packet fields or timing that were not proven to be the Def-Agg tutorial trigger. Mike's in-client retests disproved these hypotheses:

- `AttackMessage.Unknown = 0` alone does not stop the tutorial line.
- `AttackInfoMessage.Unknown = 0` alone does not stop the tutorial line.
- Delaying the non-robot NPC first combat tick alone does not stop the tutorial line.

These changes should not be treated as the Def-Agg fix. They may remain only as live-compatible packet/timing alignment until a fresh post-`2e779266` capture proves they are harmful or unnecessary.

## Revert/quarantine decision

No automatic revert is recommended from the currently available evidence:

- `AttackMessage.Unknown = 0` remains directly live-compatible.
- `AttackInfoMessage.Unknown = 0` remains directly live-compatible.
- Non-robot NPC immediate first-hit suppression remains directionally live-compatible with the observed live combat-start window, but the exact delay value is not proven as the Def-Agg trigger.

The failed assumptions are quarantined here: future work must not build on them as Def-Agg root cause proof. The next code change should target only the earliest remaining post-`2e779266` server-to-client packet difference from a fresh capture.

## Current conclusion

The exact trigger is unresolved.

The most likely remaining candidates, in priority order, are:

1. Missing or mismatched player `SpecialAttackWeapon` context immediately before attack echo.
2. Missing or mismatched target `SpecialAttackWeapon`/target attack-start context before the first target hit.
3. Missing or late `NumFightingOpponents` client state update for sustained combat.
4. Remaining packet order mismatch where AORebirth sends player attack echo before the captured live `SpecialAttackWeapon(player)` context.
5. A login/full-character/tutorial client-state condition that is not visible in the currently inspected attack-start packets.

No code should be changed from this report alone. The next useful evidence is a fresh AORebirth capture, after commit `2e779266`, of exactly:

1. Zone into Subway.
2. Target the Subway Thief.
3. Start attack.
4. Stop capture after the Def-Agg tutorial line appears and before additional unrelated actions.

That capture should be compared packet-by-packet against the `20260708-143600` live timeline above before changing another combat packet field.
