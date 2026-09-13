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
- Exact Linux acceptance at the same source: PASS, including 551 NewEngine tests.
  Source is pushed on `codex/reset-transfer-movement-input` and included in local
  master; remote master was not moved.
- Mike explicitly authorized stopping the live server with players online.
  LoginEngine and then NewEngine shut down gracefully; persisted online count
  reached zero while all 280 inventory rows and 28 characters remained present.
  No manual online-state clearing, schema change or operator row edit was used.
  The existing stopped-pair deployment transaction is used for this maintenance.
- Live deployment, candidate database validation, exact runtime provenance and
  post-start stability: PASS. LoginEngine and NewEngine run source `70147890`
  with zero restarts. ChatEngine and the account broker remain active. The four
  existing migration ledger entries are unchanged; all 280 inventory rows and
  28 characters remain present. Official-client visual verification is pending.
- The repository-wide secret scan still reports seven pre-existing untracked
  `tools-temp/linux-staging-50d` environment files. They are excluded from the
  release and this commit. No values are copied into this receipt.

## Production receipt

Accepted and deployed source: `701478901f564b31abb5bbe8a96c74ccb05d794e`.
Deployment completed on 2026-09-13 UTC (2026-09-12 local).
The prior NewEngine source `517f579400b1901db2b4968d5c5393376791803e`
is retained. Rollback snapshot:
`/opt/ao-rebirth/deployment-snapshots/release-701478901f564b31abb5bbe8a96c74ccb05d794e-20260913T015828Z-2494538`.
No rollback or database restore was needed. This movement repair introduces no
database format change; the historical Legacy rollback restrictions remain in force.

Private release evidence is retained under `build-verify/live-70147890`, including
the accepted Windows/Linux evidence, release manifest, archive checksum, full
dry-run/deployment logs and runtime verification. `evidence-index.json` SHA-256:
`51a3f5b49c7e940543d6ff443e394f1797755930aed20b1c123e5c44591ef796`.
The remote stage is `/srv/aorebirth-release-70147890-20260913`.
The graceful shutdown receipt is
`build-verify/live-bb5dd916/movement-maintenance-stop.log`.

`LinuxBuild/deployment/production-release/README.md` now records Mike's explicit
maintenance policy: online characters do not block an authorized server stop and
fix. Graceful shutdown, saved-state validation and exact release safeguards remain
required; manual online-row clearing is not permitted.

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

The reported visual symptom needs Mike's held-key/release retest on the deployed
`70147890` build, including another building entrance for comparison. Automated
server-state acceptance does not establish official-client animation acceptance.
