# Door arrival heading

## Root cause

Door zoning resolved a destination position but discarded the destination door's
heading. `PlayfieldWorldSimulation` used the position-only session transfer, which
preserves the source character's world heading. Preserving that heading across
entrances with different orientations can leave the character facing a wall.

## Files changed and inspected

- `Core/WorldSimulation/PortalDoorLandingResolver.cs`: return the same destination
  door quaternion already used to offset the accepted landing away from its frame.
- `Core/WorldSimulation/PlayfieldWorldSimulation.cs`: carry that heading through
  portal entry and recorded-door return; border and destination-line crossings
  retain the character's heading.
- `Core/Network/IZoneSession.cs`: expose the existing explicit-heading transfer.
  Implementations without support reject it instead of silently discarding it.
- `ZoneEngine_New.Tests/TeleportDestinationCatalogTests.cs`: check actual DAO
  Fair Trade and implant-shop entries and returns face along their landing clearance.
- `ZoneEngine_New.Tests/PlayfieldTransferTests.cs`: check explicit heading and
  preserved-heading transfers, source stability, destination state and N3Teleport.

Runtime paths above are under `AORebirth/Server/ZoneEngine_New`; test paths are
under `AORebirth/Server`. Also inspected `PlayfieldTransfer`, `ZoneSession`,
`SpawnService`, `BuildingExitProxyTests`, the existing interior-door and movement
reports, playfield names, and repository governance/workflow sections.

## Evidence boundary

This implements Mike's requested forward-facing door arrival using existing
extracted geometry and DAO routing. It adds no packet fields, per-zone angles,
door position edits, database writes or schema changes. Historical PF1186 captures
were unavailable locally during the preceding repair, as recorded in
`NEWENGINE_TRANSFER_MOVEMENT_RESET.md`; no new capture or client was launched.
This is a repository-data-backed orientation policy, not a claim that retail
captures prove every destination heading. The precise backyard Mike encountered
is still unconfirmed.

## Validation performed

NewEngine build: PASS. Direct test execution found missing test-output playfield
assets in 11 existing geometry tests and a float-precision assertion in the two
new transfer cases. The assertion now compares the stored float representation.
Exact Windows acceptance with the packaged world, Linux acceptance and live
client validation remain pending.
