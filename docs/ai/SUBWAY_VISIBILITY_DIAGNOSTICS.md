# PF127 Visibility Isolation Diagnostics

## Purpose

This workflow determines whether the 38 quarantined Subway rows fail because of one or more client-incompatible enemy updates or because the complete initial visibility snapshot is too large.

It does not implement a production visibility cutoff, batching, throttling, pagination, packet-field change, or final fix. The normal default remains safe: all 38 rows stay quarantined and diagnostics remain disabled until an operator prepares a session explicitly.

This failure is separate from the repaired RoomSpace defect. The current PF127 failure occurs after the expanded initial visibility stream; the two open hypotheses are `ONE_OR_MORE_INVALID_ENEMY_UPDATES` and `AGGREGATE_SNAPSHOT_VOLUME`.

## Evidence and manifest

The authoritative source is capture `20260710-202132` and the committed population restore manifest. The stable diagnostic projection is:

```text
docs\generated\subway_pf127_visibility_diagnostic_manifest.csv
```

It contains deterministic ordinals `1..38`, exact source identity, name, family, classification, captured position, and source capture. Ordinals follow the committed population-manifest order. They do not depend on dictionaries or runtime spawn identities.

- 29 rows: `SUPPORTED_FAMILY_RESTORE`
- 9 rows: `ORDINARY_ENEMY_REGENERATE`
- 0 named bosses
- 0 owned summons

## Safety boundary

No `.local\subway-visibility\active-session.cfg` file means:

```text
diagnostics disabled
selected quarantined rows = 0
normal PF127 population unchanged
```

`ALL_38` is available only as an explicit diagnostic selection. Unknown identities, invalid ranges, malformed session configuration, paths outside the ignored diagnostic root, and count mismatches fail closed.

Session artifacts are written under:

```text
.local\subway-visibility\<session-id>\
```

The entire `.local` tree is ignored by Git.

## Operator commands

Run these from the repository root. The wrapper uses `cmd.exe`; it may call Python internally.

Prepare a safe control:

```cmd
tools\subway_visibility_diagnostic.cmd prepare --session-id pf127-vis-001 --slice NONE
```

Prepare broad groups:

```cmd
tools\subway_visibility_diagnostic.cmd prepare --session-id pf127-vis-002 --slice SUPPORTED_29
tools\subway_visibility_diagnostic.cmd prepare --session-id pf127-vis-003 --slice ORDINARY_9
```

Only one session may be active. Finish the current session before preparing the next:

```cmd
tools\subway_visibility_diagnostic.cmd status --session-id pf127-vis-001
tools\subway_visibility_diagnostic.cmd finish --session-id pf127-vis-001
```

After finishing the final session, restart the engines once more. The running ZoneEngine intentionally holds its startup selection for the life of that process; removing the active file takes effect on the next approved restart.

Other supported selectors:

```cmd
tools\subway_visibility_diagnostic.cmd prepare --session-id pf127-vis-004 --slice ALL_38
tools\subway_visibility_diagnostic.cmd prepare --session-id pf127-vis-005 --first 10
tools\subway_visibility_diagnostic.cmd prepare --session-id pf127-vis-006 --ordinal-range 11-20
tools\subway_visibility_diagnostic.cmd prepare --session-id pf127-vis-007 --identity-list 79557C09,79557C26
tools\subway_visibility_diagnostic.cmd prepare --session-id pf127-vis-008 --family "Stim Fiend"
```

After `prepare`, restart the engines through the existing approved wrapper:

```cmd
restart-engines.cmd
```

Then Mike performs one manual client login into PF127. The diagnostic tooling never launches the server or client and never infers a crash merely because a log stopped.

## Record the observed outcome

Allowed outcomes:

```text
PASS_LOGIN_STABLE
FAIL_CLIENT_CRASH
FAIL_CLIENT_DISCONNECT
FAIL_SERVER_EXCEPTION
INCONCLUSIVE
```

Example:

```cmd
tools\subway_visibility_diagnostic.cmd record --session-id pf127-vis-002 --outcome FAIL_CLIENT_CRASH --time-to-failure 15 --login-completed YES --world-rendered YES --movement-possible NO --note "Client closed after world rendered"
```

Unknown observations can be omitted or supplied as `UNKNOWN`. Outcome files explicitly record `client_state_source=operator_observed`.

## Analyze a session

```cmd
tools\subway_visibility_diagnostic.cmd analyze --session-id pf127-vis-002
```

The analyzer writes `analysis.json` and `analysis.md`, compares all completed local sessions, and may report:

```text
FAILURE_FOLLOWS_SPECIFIC_IDENTITY
FAILURE_FOLLOWS_GROUP
FAILURE_REQUIRES_COMBINATION
FAILURE_CORRELATES_WITH_NPC_COUNT
FAILURE_CORRELATES_WITH_BYTE_COUNT
SERVER_SEND_SEQUENCE_INCOMPLETE
SERVER_SEND_SEQUENCE_COMPLETE_BEFORE_CLIENT_FAILURE
INCONCLUSIVE
```

One failing login can establish `LAST_COMPLETED_BEFORE_FAILURE`; it cannot establish `PROVEN_CAUSAL_ENEMY`. Identity causality requires repeatable controlled-slice evidence.

## Recommended first sequence

1. `NONE` — safe control.
2. `SUPPORTED_29` — supported-family broad group.
3. `ORDINARY_9` — ordinary-enemy broad group.
4. If one broad group fails, use the analyzer's deterministic half-list recommendation.
5. If both broad groups pass, explicitly test `ALL_38`.
6. If only `ALL_38` fails, aggregate volume becomes the leading hypothesis; compare controlled combinations by NPC count and serialized-byte total.

For a failing group, test its first deterministic half. Test the complementary half only when needed, then continue halving the failing set until the smallest repeatable failing identity or combination is isolated.

## Runtime artifacts

Every prepared session contains:

```text
session.json
selected-identities.txt
operator-instructions.txt
runtime-events.jsonl
per-enemy-send-ledger.csv
snapshot-summary.jsonl
outcome.json
analysis.json
analysis.md
```

For every PF127 NPC in the initial visibility snapshot, the ledger records session and snapshot IDs, player and playfield identities, send ordinal, stable manifest ordinal when applicable, runtime and source identities, enemy type/instance, name, family, population group, capture, selected slice, position, level, exact serialized sizes, packet counts, cumulative counts/bytes, completion state, failure state, and elapsed time.

Completion events distinguish serialization start/completion, actual transport send start/completion, weapon-phase start/completion, enemy-sequence completion, enqueue completion, and complete snapshot transport completion. Exceptions record the active ordinal and identity before normal exception policy continues.

## Serialized-size measurement

Size is measured from the exact `byte[]` returned by `IMessageSerializer.Serialize(message)` and queued to the client's `ZlibStream`. This is the completed uncompressed AO message length at the nearest stable per-packet boundary. Streaming zlib does not expose a reliable independent compressed-wire length for each message.

Diagnostics observe that buffer and its length only. They do not alter the packet body, header, order, serializer, packet number, compression negotiation, or weapon definition. The existing order remains:

```text
SimpleCharFullUpdate
zero or more WeaponItemFullUpdate definitions
CharInPlay
```

## Evidence required before a production fix

Do not change production visibility behavior until controlled sessions show one of these repeatable results:

- failure follows the same identity while comparable slices without it pass;
- failure follows one family/group across repeated slices;
- both broad groups pass but their controlled combination fails;
- failure follows a repeatable count/byte boundary after controlling population composition;
- server serialization or transport consistently stops at the same packet and ordinal.

The final production repair is a separate task after that evidence exists.
