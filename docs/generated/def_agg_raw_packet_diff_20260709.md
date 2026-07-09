# Def-Agg Raw Packet Diff Supplement - 2026-07-09

## Scope

Active issue: every player-vs-enemy combat start can trigger the client-local tutorial line:

`Use the Def-Agg slider in the Stats view to change between defensive and aggressive.`

This report uses completed capture evidence only. It does not make a gameplay change.

Captures compared:

- AORebirth reproduced bug: `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260708-225850`
- Official live reference: `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260708-143600`

## Raw Timeline: AORebirth 20260708-225850

Target: `Thief`, `(SimpleChar:794F6173)`, monsterData `26092`.

| Delta | Direction | Seq | Len | Raw opcode | Decoded | Source | Target | Important fields | Raw excerpt |
| --- | --- | ---: | ---: | --- | --- | --- | --- | --- | --- |
| +0.000s | OUT | 20 | 38 | `28494070` | `Attack` | `SimpleChar:7944C065` | `SimpleChar:794F6173` | `Unknown=0`, `Unknown1=0` | `0000000A000000007944C06500000002284940700000C3507944C065000000C350794F617300` |
| +0.091s | IN | 440 | 101 | `1D3C0F1C` | `SpecialAttackWeapon` | `SimpleChar:7944C065` | n/a | player weapon context, live-shaped | `0087000A0001006500000DB97944C0651D3C0F1C0000C3507944C0650000000FC4...` |
| +0.091s | IN | 441 | 38 | `28494070` | `Attack` | `SimpleChar:7944C065` | `SimpleChar:794F6173` | player attack echo, live-compatible base flag | `0088000A0001002600000DB97944C065284940700000C3507944C065000000C350794F617300` |
| +1.501s | IN | 460 | 61 | `46002F16` | `AttackInfo` | `SimpleChar:7944C065` | `SimpleChar:794F6173` | amount `55`, ammo `-1`, slot `8`, hit type `4`, weapon instance `0`, base `Unknown=0` | `009B000A0001003D00000DB97944C06546002F160000C3507944C0650000000037...` |
| +1.701s | IN | 461 | 53 | `1D3C0F1C` | `SpecialAttackWeapon` | `SimpleChar:794F6173` | n/a | `Specials=count=0[]`, `Unknown1..4=32`, `Unknown5=0` | `009C000A0001003500000DB97944C0651D3C0F1C0000C350794F617300000003F10000002000000020000000200000002000000000` |
| +1.701s | IN | 462 | 38 | `28494070` | `Attack` | `SimpleChar:794F6173` | `SimpleChar:7944C065` | target attack state | `009D000A0001002600000DB97944C065284940700000C350794F6173000000C3507944C06500` |
| +1.701s | IN | 463 | 41 | `2B333D6E` | `Stat` | `SimpleChar:7944C065` | n/a | `NumFightingOpponents=1` | `009E000A0001002900000DB97944C0652B333D6E0000C3507944C06501000000010000019A00000001` |

The Def-Agg text is not present in `events.log` or `system-messages.log`, so it remains client-local UI behavior triggered by packet/state conditions.

## Raw Timeline: Official Live 20260708-143600

First captured fight target: `Filth Flea`, `(SimpleChar:794DF18C)`, monsterData `17657`.

| Delta | Direction | Seq | Len | Raw opcode | Decoded | Source | Target | Important fields | Raw excerpt |
| --- | --- | ---: | ---: | --- | --- | --- | --- | --- | --- |
| +0.000s | OUT | 4 | 38 | `28494070` | `Attack` | `SimpleChar:7944C065` | `SimpleChar:794DF18C` | `Unknown=0`, `Unknown1=0` | `0000000A000000007944C0650000000228494070000C3507944C065000000C350794DF18C00` |
| +0.232s | IN | 83 | 101 | `1D3C0F1C` | `SpecialAttackWeapon` | `SimpleChar:7944C065` | n/a | player weapon context | `06CB000A0001006500000DB97944C0651D3C0F1C0000C3507944C0650000000FC4...` |
| +0.232s | IN | 84 | 38 | `28494070` | `Attack` | `SimpleChar:7944C065` | `SimpleChar:794DF18C` | player attack echo | `06CC000A0001002600000DB97944C065284940700000C3507944C065000000C350794DF18C00` |
| +0.850s | IN | 91 | 41 | `2B333D6E` | `Stat` | `SimpleChar:7944C065` | n/a | `CurrentNano=167`; likely heartbeat/noise, not combat-specific | `06D3000A0001002900000DB97944C0652B333D6E0000C3507944C0650000000001000000D6000000A7` |
| +1.601s | IN | 102 | 61 | `46002F16` | `AttackInfo` | `SimpleChar:7944C065` | `SimpleChar:794DF18C` | amount `22`, ammo `-1`, slot `8`, hit type `3`, weapon instance `0`, base `Unknown=0` | `06DE000A0001003D00000DB97944C06546002F160000C3507944C0650000000016...` |
| +1.601s | IN | 103 | 85 | `1D3C0F1C` | `SpecialAttackWeapon` | `SimpleChar:794DF18C` | n/a | `Specials=count=2[]`, `Unknown1..4=33`, `Unknown5=0`; includes weapon tag data | `06DF000A0001005500000DB97944C0651D3C0F1C0000C350794DF18C0000000BD300031163000311644550414845504148...` |
| +1.601s | IN | 104 | 38 | `28494070` | `Attack` | `SimpleChar:794DF18C` | `SimpleChar:7944C065` | target attack state | `06E0000A0001002600000DB97944C065284940700000C350794DF18C000000C3507944C06500` |
| +1.601s | IN | 105 | 41 | `2B333D6E` | `Stat` | `SimpleChar:7944C065` | n/a | `NumFightingOpponents=1` | `06E1000A0001002900000DB97944C0652B333D6E0000C3507944C06501000000010000019A00000001` |

## Earliest Mismatch

The earliest strict packet difference after `OUT Attack` is live's `CurrentNano=167` stat refresh at `+0.850s`, which is absent from the AORebirth window. That packet is not combat-specific and is not a proven Def-Agg trigger.

The earliest combat-specific payload mismatch in the completed captures is the target-side `SpecialAttackWeapon` packet:

- AORebirth Thief: length `53`, `Specials=count=0[]`, `Unknown1..4=32`, `Unknown5=0`.
- Live first target Filth Flea: length `85`, `Specials=count=2[]`, `Unknown1..4=33`, `Unknown5=0`, weapon tag data present.

This is a real raw payload difference. It is not yet a proven trigger because the official live capture did not include a fight against the exact same `Thief`; it only contains live Thief spawn/SCFU evidence.

## Source Consistency Check

Current source search finds explicit `SpecialAttackWeaponMessage` construction only in:

- `AORebirth/Server/ZoneEngine/Core/PacketHandlers/ClientConnected.cs` for player login weapon context.
- `AORebirth/Server/ZoneEngine/Core/Playfields/NpcCombatTickCoordinator.cs` for captured cleaning robot NPC context.
- `AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs` for death-respawn ready block.

The current captured-cleaning-robot NPC path is gated by `IsCapturedCleaningRobot`, which requires:

- name `Malfunctioning Cleaning Robot`
- monsterData `297023`

The captured Thief is:

- name `Thief`
- monsterData `26092`

Therefore the completed `20260708-225850` Thief `SpecialAttackWeapon Unknown1..4=32` packet cannot be attributed to the current checked-out captured-cleaning-robot helper without a fresh current-source capture. It may reflect an older running binary, an under-searched generated/compiled path, or a capture made before the latest source state.

## Failed Fix Review

Keep/quarantine decisions from available evidence:

- `AttackMessage.Unknown=0`: keep as live-compatible, but not the Def-Agg fix.
- `AttackInfoMessage.Unknown=0`: keep as live-compatible, but not the Def-Agg fix.
- delayed non-robot first combat tick: quarantine as a failed Def-Agg hypothesis; it may still be live-directional timing alignment, but it is not proven root cause.
- login/actionable `State=0`: keep as live-compatible baseline, but not the Def-Agg fix.

## Current Conclusion

The exact trigger is still unresolved from completed captures alone.

Most likely remaining candidates:

1. Target-side `SpecialAttackWeapon` payload mismatch for ordinary NPCs, especially empty/default NPC weapon context values.
2. A login/full-character/tutorial state mismatch that primes the client to display the local Def-Agg tutorial on the next attack.
3. A stale-binary or source/capture mismatch, because the completed Thief capture includes a target `SpecialAttackWeapon` payload that the current checked-out source does not obviously construct for Thief.

No gameplay code should change from this report alone.

## Required Next Evidence

Before another fix, capture current-source AORebirth after a clean build/restart and fresh client login, then compare:

1. Full login/ready `FullCharacter` state, especially `AggDef=51`, `State`, `CurrentState`, `WaitState`, `CurrentMovementMode`, and combat/tutorial-adjacent stat fields.
2. First combat start against any enemy after login.
3. Every server-to-client packet from `OUT Attack` through `NumFightingOpponents=1`.
4. Raw `SpecialAttackWeapon` payload fields for both player and target.

If current-source AORebirth still emits ordinary-NPC target `SpecialAttackWeapon` with empty specials and default-looking values before the tutorial line, that becomes the first fix target. If it does not, the investigation must move earlier to login/full-character state.
