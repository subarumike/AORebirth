# Cross-playfield movement reset

## Root cause

Mike reports intermittent running in place after entering Fair Trade while holding
the movement key through loading. Releasing the key sometimes stops the animation
and sometimes requires another key press. Production logs confirm character 34
successfully enters PF1186 and completes the reconnect at the accepted DAO landing.

The retained character motor carries its old directional flags and path into the
destination. `CharDCMoveMessageHandler` correctly ignores input while the session
is Loading. A release received in that interval therefore cannot clear the old
forward flag. The reconnect SCFU obtains movement status from that same motor and
can advertise forward movement again. This server-state defect is reproduced in
an offline transfer test; the exact timing of Mike's production key-up packet has
not been captured.

Mike also supplied Delmus's confirmation that SCFU now reports active motor inputs
and that clearing CharacterMotor input flags on zoning was a known remaining fix.

## Files changed

- `AORebirth/Server/ZoneEngine_New/Core/Movement/CharacterMotor.cs` adds an explicit
  transfer reset using existing path cancellation, input clearing and warp reset.
- `AORebirth/Server/ZoneEngine_New/Core/Playfield/SpawnService.cs` invokes it on
  cross-playfield arrival, before reconnect can publish the retained motor state.
- `AORebirth/Server/ZoneEngine_New.Tests/PlayfieldTransferTests.cs` reproduces a
  release during Loading and checks idle spawn status, new input after arrival,
  path/strafe/turn/jump clearing and preservation of the selected speed mode.

The reset uses existing movement encoding and applies at the shared transfer
boundary. No Fair Trade-specific packet values, database edits or schema changes
are introduced.

## Validation performed

- Before repair: the regression test fails because the arriving motor still moves.
- After repair: transfer regression suite PASS.
- Exact Windows acceptance at `701478901f564b31abb5bbe8a96c74ccb05d794e`:
  PASS, including all 551 NewEngine tests. The optional mandatory integration suite
  was not rerun. The focused transfer suite passes all 15 tests.
- Linux acceptance, live deployment and official-client visual verification:
  pending. Source is pushed on `codex/reset-transfer-movement-input`.

## Files and evidence inspected

Read the repository startup/governance documents, existing interior-door routing
report, capture inventory, `PlayfieldWorldSimulation`, `ZoneSession`,
`PlayfieldTransfer`, `SpawnService`, `Player`, `Character`, `CharacterMotor`,
`MovementTypes`, `CharDCMoveMessageHandler` and transfer tests. The capture
inventory contains 14 PF1186 entries; none of their recorded packet logs is locally
present, and the repository Captures directory has no Fair Trade/PF1186 folder. Available
server logs and a deterministic reproduction provide the fallback evidence; no
client or capture injector was launched. Private evidence is retained under
`build-verify/fair-trade-movement` and
`build-verify/live-bb5dd916/fair-trade-animation.log`.

## Remaining risks

The reported visual symptom needs a held-key/release retest after deployment,
including another building entrance for comparison. Until then the live server
continues to run the accepted login repair at `517f5794`.
