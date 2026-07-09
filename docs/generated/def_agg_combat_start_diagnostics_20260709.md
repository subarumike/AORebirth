# Def-Agg Combat Start Diagnostics - 2026-07-09

## Scope

The global repeated Def-Agg tutorial line is still unresolved. This slice added opt-in server diagnostics to capture outbound combat-start packets without changing packet payloads or gameplay behavior.

## Diagnostic Activation

Diagnostics are enabled when either condition is true:

- environment variable `AOREBIRTH_COMBAT_PACKET_DIAGNOSTICS=1`
- flag file exists beside the built engine: `AORebirth/Built/Debug/combat-packet-diagnostics.enabled`

The flag file was created for the current local run after the diagnostic build.

## Logged Prefix

All diagnostic lines use:

`COMBAT_START_DIAG`

## Logged Evidence

When enabled, the server logs:

- inbound player attack command context from `AttackMessageHandler.Read`
- outbound `Attack` echo state from `AttackMessageHandler.SendAttackState`
- outbound N3 messages through `Playfield.Announce`
- outbound N3 messages through `Playfield.Send`
- stat bulk sends through `StatMessageHandler.SendBulk`

Logged fields include UTC timestamp, direction, route, message type, source identity, target identity where present, recipient identity where present, key packet fields, and `len=unavailable` because byte length is not available at these high-level send sites without reserializing.

## Current Live-Tail Result

After enabling diagnostics and restarting engines, two 120-second log tails produced no new `COMBAT_START_DIAG` lines. No generic enemy fight occurred during those tail windows, so no combat-start sequence was captured in this slice.

## Next Required Evidence

With diagnostics enabled, run one clean fight against a repeatable non-Subway enemy, such as a leet or Malfunctioning Cleaning Robot, then inspect `AORebirth/Built/Debug/ZoneEngineLog.txt` for `COMBAT_START_DIAG`.

The needed comparison is the first server-to-client sequence from player attack command through first hit/miss:

1. player `Attack` command context
2. outbound `SpecialAttackWeapon`, if any
3. outbound player `Attack` echo
4. outbound `AttackInfo`
5. outbound stat updates, especially fighting-opponent, state/current-state/action-category, and AggDef-related fields
6. outbound `CharacterAction`, `FormatFeedback`, or any other N3 packet around combat start

Do not apply another combat-field fix until this diagnostic output identifies the actual shared trigger.
