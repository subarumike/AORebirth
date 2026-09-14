# Connected position persistence reconciliation

## Root cause

`TEST_EXPECTATION_DEFECT`. The connected fixture compared the final character row
and reconnect SCFU against the original seed `(100,0,100)` after asking the real
server to execute `.tp 100 0 100 800`, then `.tp 100 0 100 4582`.
The owning motor continues simulating after arrival. The requested transfer
coordinate is not the final position that logout must persist.

Production is recorded as `cb12160c37507f8318b7e9e39f424f9bf13faa23`; no production
inspection, deployment, migration, client launch or Legacy retirement is part of
this task. Input is `29fde403eae3f305e71a69e186587c8d2f15980f` on
`codex/delmus-npc-content-001`, in `build-verify/delmus-npc-content-001`.
Recorded origin/master is `7be49b22b4c0116af9c48dc88a44f86ba802f7c7`.

Control is `cb12160c37507f8318b7e9e39f424f9bf13faa23`, chosen from the integration
receipt and verified ancestry. The integration starts at `2dc701f1`, whose only
changes from that control are three documentation files. The control therefore
contains the same pre-integration runtime, including movement reset and arrival
heading. It is also the common ancestor of the tested control and candidate.
Neither master nor Delmus's branch is modified.

## First divergence and movement semantics

`SpawnService.ArriveFromTransfer` calls `CharacterMotor.ResetForPlayfieldTransfer`.
It clears inputs and paths, sets the requested position and sets vertical velocity
to the existing `MovementConfig.GroundStickVelocity = -2f`.
The first PF4582 `CharacterMotor.Tick` finds support at Y=0 and constructs the
movement endpoint as `groundY + verticalVelocity * dt`. The collision resolver
leaves that endpoint unchanged in this fixture.

Temporary diagnostic logging measured this exact first step:

```text
before=0
dt(float32 display)=0.031404
grounded=True
ground=0
verticalVelocity=-2
proposed=-0.06280799955129623
after=-0.06280799955129623
world=True
```

The next tick measured surface Y=`7.450580596923828E-09`, then final
Y=`-0.06299199163913727`. The runtime snapshot and binary MySQL row had exactly
that value. The geometry data is the existing PF4582 package through
`GameDataStore.GetPlayfieldGeometry`, `PlayfieldWorldSimulation.TryRaycastDown`
and `TryMoveCapsule`. PF800 reports no geometry below `(100,0,100)` and holds
altitude. This is not an inferred floor offset or a fabricated placement.

The accepted arrival-heading repair changes rotation, not this GM route's height.
Movement reset explains the initial negative vertical velocity. No movement,
grounding, collision, transfer, snapshot, DAO or hydration runtime code is changed
by the final fix. Temporary logging was removed after retaining the evidence.

Wall-clock tick duration varies between runs, so baseline and candidate do not
have numerically identical final Y values. They exercise the same unchanged
position code and first divergence stage. Equal failures alone were not used to
accept the candidate. Controlled regression ticks of 0.015625 and 0.03125 seconds
use the actual PF4582 collision geometry and verify the precise motor result and
snapshot ownership without a tolerance.

## Position authority and inspected files

Paths below are repository-relative. No unrelated gameplay systems were changed.

| Component | Reads | Writes | Can alter Y? |
| --- | --- | --- | --- |
| `Tools/ZoneEngineSchemaValidation/ConnectedCharacterPersistence.cs` | Fixture route | GM request `(100,0,100)` | Defines the request only |
| `Core/Network/ZoneSession.cs` | Request and current player | Queued transfer and N3Teleport | No normalization |
| `Core/Playfield/PlayfieldTransfer.cs` | Requested landing | Destination owner | Copies request |
| `Core/Playfield/SpawnService.cs` | Transfer landing / hydrated row | `Player.Position`, motor reset | Sets requested/loaded state |
| `Core/Movement/CharacterMotor.cs` | Current position, dt, floor/capsule queries | Authoritative player position | Yes, first observed delta |
| `Core/Movement/MovementConfig.cs` | Existing movement constants | Motor calculation inputs | Ground-stick velocity is -2 |
| `Core/WorldSimulation/PlayfieldWorldSimulation.cs` | Extracted geometry and query position | Ground/collision query result | Supplies measured surface |
| `Core/Entities/Character.cs` and `Player.cs` | Position and owning tick | Motor dispatch and SCFU | Motor changes it; SCFU casts to float |
| `Core/Characters/CharacterSnapshotService.cs` | Final `Player.Position` | Float32 `CharacterRecord` | Cast only; does not move player |
| `Core/Data/MySqlCharacterRepository.cs` | Snapshot record | Shared persistence DAO | Field mapping only |
| `ICharacterPersistenceDao` / `MySqlCharacterPersistenceDao` | DTO float values | Atomic location/stats SQL and loaded DTO | No spatial adjustment |
| `Core/Characters/CharacterHydrationService.cs` | DAO character record | Hydration result | No spatial adjustment |
| `Core/MessageHandlers/CharacterActionMessageHandler.cs` | Logout request | Existing logout/snapshot call | No coordinate write |
| `Tools/ZoneEngineSchemaValidation/ConnectedWireClient.cs` | TCP messages | Serialized/deserialized message objects | No physics simulation |
| `Tools/ZoneEngineSchemaValidation/LifecyclePersistenceEvidence.cs` | Seed plus proven runtime snapshot | Expected-state comparison | Previously kept stale seed Y |
| `Tools/ZoneEngineSchemaValidation/ConnectedPositionEvidence.cs` | Runtime snapshot log, binary/text SQL, DAO, SCFU | Assertions/evidence only | No runtime/database writes |

`Core/...` paths above are under `AORebirth/Server/ZoneEngine_New`.
The DAO implementation is under
`AORebirth/Libraries/Source/AORebirth.Database/Domain/Characters`.
Also inspected: `AORebirth.Core/Vector/Vector3.cs`, the `characters.sql` table
definition, `ConnectedAcceptanceSmoke.cs`, `ConnectedEngineProcess.cs`, and the
existing connected/DAO validation reports and workflow commands.

## Precision and storage contract

Runtime vectors hold doubles. Motor velocity/dt and collision queries use floats;
wire coordinates, snapshot DTOs and SQL `FLOAT` columns use float32. The snapshot
log prints round-trip float values. Prepared binary SQL reads prove that the
snapshot float bits are preserved in storage, with zero permitted error.

MySQL ordinary text reads can return fewer decimal digits. In the diagnostic run,
stored Y was `-0.06299199163913727`, whereas DAO-loaded Y was
`-0.06299199908971786`: error `-7.450580596923828E-09`, one float32 ULP.
This secondary serialization effect cannot explain the initial 0-to-negative
motor step. The ordinary SQL read, DAO DTO and reconnect SCFU agree exactly.
Raw binary storage and the text-decoded DAO float are therefore not always
bit-identical; the report does not conceal that difference.

The test uses no epsilon and implements no guessed MySQL formatting algorithm.
It independently checks snapshot -> binary stored bits, then ordinary SQL read
-> DAO DTO -> reconnect SCFU bits. Distinct query text prevents the connector's
prepared-statement cache from accidentally making the text comparison binary.
The exact observed read-conversion errors are retained in the trace artifact.

## Fix and regression contract

After each explicit logout, the fixture waits for the existing engine snapshot
log emitted for that logout. It checks the stored binary position against that
independent runtime observation before updating only the expected coordinate
fields. X/Z and playfield must still match the requested route. All other
character columns, every stat row and every inventory row retain strict equality.
The reconnect and distinct-process restart checks use the verified loaded DAO
position instead of the original seed. Logout/snapshot itself does not alter
position; motor ticks before snapshot can do so.

Two deterministic runtime regression cases cover transfer, actual collision,
post-arrival simulation and snapshot. Four negative fixture cases reject a
one-bit loss on each axis and replacement of authoritative height by requested
height. The connected lifecycle supplies the real MySQL/logout/reload/reconnect
and process-restart coverage. No independent fixture physics model was added.

## Validation receipt

Exact baseline/candidate XYZ traces, binary/text conversion evidence, source and
log hashes, validation counts, and final acceptance decision are recorded in
`NEWENGINE_CONNECTED_POSITION_PERSISTENCE_RECEIPT.json` from the completed final gates.
Earlier failed results remain in the original NPC validation receipt as history.
NPC completeness, full DAO conversion, gameplay parity and Legacy retirement
are not prerequisites for this focused durable-state acceptance.

<!-- FINAL_POSITION_RECEIPT -->

## Exact connected trace and final acceptance

Tested source: `ead455fd047d28f7835174dbce9261f0632d6079`. Final receipt changes are documentation only.

Immediate runtime arrival is `(100,0,100)` on both legs in both engine logs.
The SCFU rows below are later observations and may already include motor ticks.

| Stage | Baseline X | Baseline Y | Baseline Z | Candidate X | Candidate Y | Candidate Z |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Seed/start | 100 | 0 | 100 | 100 | 0 | 100 |
| Pre-zone runtime SCFU | 100 | 0 | 100 | 100 | 0 | 100 |
| Requested destination PF800 | 100 | 0 | 100 | 100 | 0 | 100 |
| PF800 arrival SCFU | 100 | 0 | 100 | 100 | 0 | 100 |
| Requested return PF4582 | 100 | 0 | 100 | 100 | 0 | 100 |
| PF4582 arrival SCFU after simulation | 100 | -0.0642239972949028 | 100 | 100 | -0.06309240311384201 | 100 |
| Pre-snapshot runtime / logout save | 100 | -0.05917899310588837 | 100 | 100 | -0.06201399117708206 | 100 |
| Persisted binary FLOAT | 100 | -0.05917899310588837 | 100 | 100 | -0.06201399117708206 | 100 |
| DAO reloaded (SQL text representation) | 100 | -0.059179000556468964 | 100 | 100 | -0.06201399862766266 | 100 |
| Post-relogin runtime SCFU | 100 | -0.059179000556468964 | 100 | 100 | -0.06201399862766266 | 100 |

JSON retains float bits and every logged lifecycle stage, including later logouts and process restart. Values are round-trip representations, not rounded presentation measurements.

Validation: NewEngine 580 tests; AOtomation 1,129 tests; 12 mandatory stages; character DAO 551 checks; mission DAO 261 checks; two deterministic position cases and four negative cases. All PASS. Exact-source Windows, Linux publication, corrected baseline/candidate connected acceptance and disposable schema/persistence validation PASS.

`INTEGRATION_DURABLE_STATE_ACCEPTANCE=PASS`. The 29fde403 height blocker is closed as a test expectation defect. Runtime position code and production remain unchanged. Full gameplay parity and Legacy retirement were not required.
