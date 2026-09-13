# Zone arrival heading

## Root cause

Door zoning resolved a destination position but discarded the destination door's
heading. `PlayfieldWorldSimulation` used the position-only session transfer, which
preserves the source character's world heading. Preserving that heading across
entrances with different orientations can leave the character facing a wall.
Live character-34 logs additionally identify PF800 -> PF3081 -> PF954 backyard
transitions. PF800 -> PF3081 is an explicit LineTeleport, so its destination-line
clearance must supply the arrival direction rather than searching for a door.

## Files changed and inspected

- `Core/WorldSimulation/PortalDoorLandingResolver.cs`: return the same destination
  door quaternion already used to offset the accepted landing away from its frame.
  For LineTeleport, use the existing left-normal landing offset and CharacterMotor's
  yaw convention to face into the destination, away from the line.
- `Core/WorldSimulation/PlayfieldWorldSimulation.cs`: carry that heading through
  portal entry, explicit LineTeleport and recorded-door return. Ordinary wall-border
  crossings retain the character's heading.
- `Core/Network/IZoneSession.cs`: expose the existing explicit-heading transfer.
  Implementations without support reject it instead of silently discarding it.
- `ZoneEngine_New.Tests/TeleportDestinationCatalogTests.cs`: check actual DAO
  Fair Trade and implant-shop entries and returns face along their landing clearance.
  Also verify the observed PF800 -> PF3081 and PF3081 -> PF954 backyard routes.
- `ZoneEngine_New.Tests/PlayfieldTransferTests.cs`: check explicit heading and
  preserved-heading transfers, source stability, destination state and N3Teleport.

Runtime paths above are under `AORebirth/Server/ZoneEngine_New`; test paths are
under `AORebirth/Server`. Also inspected `PlayfieldTransfer`, `ZoneSession`,
`SpawnService`, `BuildingExitProxyTests`, the existing interior-door and movement
reports, playfield names, and repository governance/workflow sections.

## Evidence boundary

This implements Mike's requested forward-facing portal arrival using existing
extracted geometry and DAO routing. It adds no packet fields, per-zone angles,
door position edits, database writes or schema changes. Historical PF1186 captures
were unavailable locally during the preceding repair, as recorded in
`NEWENGINE_TRANSFER_MOVEMENT_RESET.md`; no new capture or client was launched.
This is a repository-data-backed orientation policy, not a claim that retail
captures prove every destination heading. `build-verify/live-d23858a0/recent-zoning.log`
records the backyard routes used for the additional checks; attribution to the
specific reported visual occurrence remains unconfirmed.

## Validation performed

NewEngine build: PASS. Direct test execution found absent geometry in 11 tests
because the primary checkout's local Playfields tree differs from the pinned
package; no existing local data was moved or overwritten. The two new transfer
assertions now compare the stored float representation. An isolated worktree
with the verified package passed exact Windows acceptance at the initial
door-only source `d23858a0` (553 tests).

The additional backyard checks proved the need to handle explicit LineTeleport.
The preliminary Linux build was stopped before completion or deployment. After
the line-heading extension, both backyard cases passed; the 555-case suite had
one unrelated ISCom socket-disposal failure, which passed in its focused retry.
Final source `cb12160c37507f8318b7e9e39f424f9bf13faa23` passes exact Windows
acceptance with all 555 tests. The Windows evidence is retained in
`build-verify/live-cb12160c/windows-acceptance.env`.

The first final-source Linux run timed out in the packaged validator on the
Windows-shared filesystem. On native Docker storage the same code passed all
555 tests, packaged startup validation, 64 deployment tests and 8 artifact tests,
but the operator runner had retained an earlier build's expected placement digest.
The final provenance gate correctly rejected it. The runner now uses the accepted
Windows digest `8519d7eb120c6e2af5473ac5c102f4dd2b6d2e783be34ae2eb3c57a1e7757d25`
and checks that input before launch; no validation timeout or guard was weakened.
Corrected full Linux acceptance: PASS (555 NewEngine tests, packaged startup,
64 deployment cases and 8 artifact provenance cases). The accepted artifacts were
exported from native Docker storage, checksum-verified, and imported only into
the controlled release workspace; the earlier failed artifacts were retained in
separate backup directories. Evidence is retained under `build-verify/live-cb12160c`.

## Production receipt

Live source: `cb12160c37507f8318b7e9e39f424f9bf13faa23`, deployed on
2026-09-13 UTC (2026-09-12 local). Source is pushed on
`codex/reset-transfer-movement-input` and included in local master; remote master
was not moved.

The release archive and manifest were verified before maintenance. LoginEngine
and NewEngine then stopped gracefully, leaving zero online characters. Both
engines started on the exact accepted artifacts and passed runtime provenance
and stability checks with zero restarts. ChatEngine and the account broker remain
active. All 280 inventory rows and 28 characters remain present. The four existing
migration ledger entries are unchanged; no schema or operator row changes were made.

The prior NewEngine release `701478901f564b31abb5bbe8a96c74ccb05d794e` is retained.
Rollback snapshot:
`/opt/ao-rebirth/deployment-snapshots/release-cb12160c37507f8318b7e9e39f424f9bf13faa23-20260913T031617Z-2529644`.
No rollback or database restore was required. The historical Legacy database
restore/reconciliation requirement remains unchanged.

The private `build-verify/live-cb12160c/evidence-index.json` records the accepted
Windows/Linux logs and environment files, native artifact export, release manifest,
transfer checksum, graceful shutdown, dry-run, deployment and runtime verification.
Index SHA-256: `be0b7f5aa2fdf123770af67e801b8370688b7d4425db6bdd14767b988d72e5d2`.
Remote release stage: `/srv/aorebirth-release-cb12160c-20260913`.

## Client acceptance

Mike confirmed the reported arrival-facing issue fixed in the official client on
live source `cb12160c37507f8318b7e9e39f424f9bf13faa23`. This closes the reported
issue; the confirmation does not establish separate manual coverage of every
zone entrance and exit.
