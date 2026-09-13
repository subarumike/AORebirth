# Cast interruption reconciliation

## Root causes

The accepted DAO-backed `NanoService` and Delmus's `NanoRuntime` are different implementations. Wholesale import would replace current nano persistence and morph/quest lifecycle behavior.

1. `CharacterActionMessageHandler` did not dispatch `InterruptNanoCasting` (108). Locally available raw captures contain 21 OUT requests using the character's identity, no target and zero parameters. Classification: **INTERRUPTION_NOT_DETECTED** for this request path.
2. Current `CharacterMotor` did not raise an interrupt on six translation/elevation start actions. Delmus already implements these hooks. They were adapted without importing other movement, collision or arrival changes. Rotation and stop input retain existing behavior; movement does not newly cancel pending inventory operations.
3. `NanoService.Cancel` cleared its pending cast without generating a client notification. Classification: **NOTIFICATION_NOT_CREATED**. Cancellation and completion remain owned by the existing service, exact Player/session references, atomic pending claim and DAO generation checks.
4. Delmus sends action 108 with `Parameter1=1`, `Parameter2=nanoId`. The available raw IN evidence uses **Parameter1=nanoId**, with Parameter2 values 7 and 4. This is a **CLIENT_EXPECTATION_MISMATCH** against those observed packets; it does not prove the local-player cancellation reason mapping.

## Exact packet evidence

`CAST_INTERRUPTION_CAPTURE_SCAN.json` inventories all 153 available raw-packets, enemy-combat and nano-event CSV artifacts under both established local capture roots. It retains 21 decoded observations and 23 raw action-108 packets. The raw evidence includes 21 OUT requests and two IN notifications. Historical generated Subway inventory also contains action-108 observations in 20260710-211430 and 20260717-214612; those historical raw paths are absent locally. Their records were retained as historical evidence, not discarded or labelled proof of a local-player IN response.

The two IN notifications are in `Captures/Sector 10 [PF 4374] - Mike 2022 - 20260830-035031/raw-packets.csv`:

| Raw row | Sequence | Actor | Nano ID / Parameter1 | Parameter2 | Preceding cast |
| --- | --- | --- | --- | --- | --- |
| 1802 | 1556 | 50000:243735873 | 250003 | 7 | sequence 1544, same actor and nano |
| 2144 | 1870 | 50000:243735873 | 253845 | 4 | sequence 1856, same actor and nano |

These are remote NPC casts observed by a client. They establish field layout and observed values, not which code represents a local player's movement, jump, manual cancel or failed target condition. Raw sequence numbers and decoded observer sequence numbers use different counters; matches were recovered by raw packet type/fields, not by equating those counters.

## Implemented and deliberately incomplete behavior

The captured zero-parameter self request now reaches the existing service. A forged identity, nonzero request parameters, stale session or replacement actor cannot cancel another owner's cast. Movement starts cancel pending casts. An interrupted cast cannot complete or spend resources through that pending operation.

The existing Delmus typed-notification approach was adapted to the observed field ordering and captured session recipient. The reason code is an explicit evidence-policy input. **No runtime policy supplies a code yet**, so the production-default candidate sends no guessed action-108 notification. No generic code 4, 7, 1 or nano-ID-as-reason fallback exists. Disconnect/leave/death/replacement behavior remains silent as before.

Tests inject a fixture-only code to prove creation, exact owner delivery, atomic/idempotent cancellation, ordering, no completion/resource side effects, codec serialization and byte equality with both raw IN bodies. This is not official-client acceptance or approval to map player movement to either captured code.

## Remaining exact gap

Required: a retail IN action-108 notification for a known **local-player** cast interrupted by a known condition, correlated to the cast-start and movement/manual-cancel sequence and the observed UI result. The available evidence proves outgoing cancel requests and remote NPC notices, but does not establish that mapping. No client launch, new capture or production change was performed. Cast interruption reconciliation is **PARTIAL**.
