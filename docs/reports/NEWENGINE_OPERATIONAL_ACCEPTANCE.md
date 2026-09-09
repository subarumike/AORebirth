# NewEngine operational acceptance

This is an evidence boundary, not a production acceptance certificate.
FAIL below means required fresh disposable/connected proof was not obtained; it
does not claim that every listed behavior is broken.

| Required acceptance | Result | Available evidence and missing proof |
| --- | --- | --- |
| Account login, character selection, world entry | FAIL | Session ownership/hydration unit tests exist; the fixture has no authenticated protocol sequence |
| Character loading | FAIL | Unit coverage; fresh real-MySQL reload fixture compiled but blocked |
| Character saving | FAIL | Snapshot transaction and failure tests; disposable runtime proof blocked |
| Reconnect | FAIL | Actor/transport lease tests; no connected durable reconnect sequence |
| Inventory preservation | FAIL | Exact instance/owner/slot/QL/count/source fixture added; not executed |
| Clean restart | FAIL | Process lifecycle fixture present; Docker prevents runtime execution |
| Transactional integrity | FAIL | Deterministic failure tests pass; fresh multi-record MySQL rollback execution blocked |
| Schema fail-closed | FAIL | Contract/startup unit and package guards; fresh schema mismatch runtime fixture blocked |
| Post-write Legacy rollback | UNKNOWN | Source shows stale Legacy inventory tables; the added disposable check did not execute |

No inference is made from historical baseline receipts to this branch's fresh
runtime acceptance. The provided baseline had 479 tests and earlier migration/
restart proof; those historical results are not relabelled current.

## What the new fixture actually tests

`Tools/ZoneEngineSchemaValidation/CutoverDurableReloadSmoke.cs` seeds only the
disposable fixture, uses actual character/inventory/stat repositories, swaps
two exact item identities, changes a stack, moves one item to wear storage and
persists cash and position. Fresh hydration validates both items' identities,
templates, quality, source metadata, owner, container and slot, plus cash and
position. It repeats the reload after each of two real engine process cycles,
compares whole database fingerprints and checks for phantom owned rows.

This does not send an equip request or prove equip legality. It does not log an
account in, select a character, exercise session save/logout or reconnect over the
protocol. Engine lifecycle connects a loopback socket only. Existing separate
nano/morph, mission, trade and inventory fixtures remain useful but are not a
substitute for the single connected lifecycle requested for cutover.

## Required connected acceptance completion

Use only a labelled disposable database and loopback endpoints. Build the client
fixture from the repository's actual login and zone packet contracts; do not
launch or control Mike's game client. Seed an account through the accepted
account boundary, authenticate, select a character and enter the world. Record
the authoritative pre-state. Perform supported move/swap/equip/unequip,
stack/consumption, credit and transactional actions. Include supported active
nano/morph and mission persistence; reject unsupported routes before mutation.
Save/logout, reconnect and assert exact committed state. Request clean shutdown,
restart the same NewEngine/database, reconnect and assert the same state again.

Assert item instance/template/QL/count/source and all container/slot/owner
coordinates, equipment, credits and relevant durable character/mission/nano
fields. Check absence of duplicates, lost/stale copies and phantom rows. Inject
definite pre-commit failures and ambiguous commit outcomes; require no partial
publication and quarantine ambiguous outcomes. Unit success alone is insufficient.

`ZoneLoginHandler` still contains an existing session-cookie validation TODO.
The credential-to-zone ownership handoff must be established from actual
contracts in this fixture; hydration by character ID must not be presented as
authentication proof.

## Available validation and failures

The local branch passed the normal Windows build (Legacy and NewEngine), 484
NewEngine tests and NewEngine offline startup readiness. Linux self-contained
cross-publication passed default-engine, source, SQL, package, backend and offline
startup guards. Package-negative fixtures passed 4/4, source-omission fixtures 2/2.
This publication ran on Windows; it is not Linux-host acceptance.

Initial AOtomation had 1128 pass / 1 fail from the inherited patrol path assertion.
The repaired focused test passed. Full post-commit counts and the exact tested
source SHA belong in the final validation receipt.

The real disposable command compiled the new fixture and failed before database
creation with `SCHEMA_VALIDATION=FAIL docker-image-failed`. Docker's backend
cannot initialize `.../Docker/run/dockerInference` on this host. This is
ENVIRONMENTAL, not a schema or gameplay failure. No production settings were used.

Logs are generated, ignored artifacts in `build-verify/cutover-*.log`.
The source/feature inventories have deterministic --write/--check validation.
Feature statuses describe scoped implementation, not a universal fail-closed
proof for all catalog routes. Global unsupported-action acceptance is therefore
not claimed; the new five-case generic durable-effect boundary is proven locally.
