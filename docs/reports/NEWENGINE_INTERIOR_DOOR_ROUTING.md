# NewEngine interior door routing repair

## Root cause

NewEngine baked TeleportProxy destinations directly from RDB Dynels.dat and
omitted the existing teleports DAO overrides applied by Legacy PlayfieldLoader.
The same raw destination then determined the synthesized reverse exit.

The running staging database confirms these mappings:

| Source PF / door | Destination PF | Raw RDB door | DAO destination door |
| --- | --- | --- | --- |
| 800 / C0150320, C0160320, C0170320 | 1186 (Fair Trade) | C00004A2 | C00404A2 |
| 800 / C0030320 | 2064 (implant shop) | C0000810 | C0010810 |

Mike's EP1 Shift+F9 report places the failed Fair Trade arrival at
202.8, 5.0, 125.0 in room Nano Programs. That matches the raw door-0 landing.
DAO door 4 resolves to 175.00107, 5.01, 113.01496 with the existing entry clearance.
The implant-shop DAO landing is 191.00363, 5.010227, 158.98544.

Fair Trade Dynels.dat extracted read-only from E:\Anarchy Online exactly matches
the repository data (SHA256 C43C573104BA1F9EC9F93486EA533F8487BF35DCDD32DB56F30DB97194571844).
This is missing server routing configuration, not a proven trigger-size fault.

## Repair

- Read the existing teleports table once through TeleportRoutingDao using the
  configured connection. No schema or database content changes.
- Apply the snapshot to proxy arguments before both geometry caching and global
  reverse-exit discovery. Preserve proxy type, event, clearance and return semantics.
- Match source playfield, identity type and the complete unsigned instance.
- Reject conflicting rows. Disable zero/unsupported mapped destinations locally;
  never silently fall back to the raw destination after an override is found.
- Permit a synthesized return exit only when it matches the source door recorded
  for this player. Other raw incoming routes must not make the nano-room door
  function as the Fair Trade return exit.

## Evidence and validation

Inspected GameDataStore, PortalDoorLandingResolver, ExitProxyDoorCatalog,
PlayfieldWorldSimulation, Legacy PlayfieldLoader, teleports.sql, the existing
teleport audit, staging zoneengine.log, the current 1,245-row DAO snapshot, and
the client/repository PF1186 resource. Historical PF1186 captures are indexed
but were not present at their recorded location or in the configured Captures
root; no new retail packet behavior is inferred from them.

Regression tests cover both DAO arrivals and returns, rejection of unrelated-room
exits, identity-type collisions, conflicting mappings, and disabled/invalid targets.
Final NewEngine test suite: PASS (523/523). Cutover inventory check and Linux
publication with source-inventory and SQL-package parity guards: PASS.
Offline hydration of the complete current routing snapshot succeeds. Two existing
zero-target routes stay disabled; the PF790 C0050316 mapping to type 51005 remains
unsupported and disabled. No speculative handling for that destination was added.

Official-client acceptance remains required after deployment: enter Fair Trade
from PF800, verify the main entrance room, walk through internal doors, and exit
to PF800. Repeat the implant-shop entrance and exit. The earlier rapid return loop
must also be checked in the deployed log; it is not independently claimed repaired.
