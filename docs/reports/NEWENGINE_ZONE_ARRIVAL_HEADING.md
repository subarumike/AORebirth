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
Final exact Windows/Linux acceptance and live client validation remain pending.
