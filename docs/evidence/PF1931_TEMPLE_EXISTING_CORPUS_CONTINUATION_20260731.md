# PF1931 Temple existing-corpus continuation (2026-07-31)

> **PF1931 status authority (2026-08-01):** Historical evidence/provenance only. Current PF1931 status is the [Temple full-corpus completion matrix](TEMPLE_FULL_CORPUS_COMPLETION_20260801.md); any PF1931 completion, blocker, or test-count statement below is superseded by that matrix.

## Scope and baseline

- Starting revision: `811e023b67dbd39f6a98c2f7e5948c4b31775ec0`.
- Scope is Temple of Three Winds runtime resource `1931` only.
- No new capture was requested or started.
- Existing combat, nano, loot, named lifecycle, and navigation behavior was not redesigned.

## Complete existing-data audit

The repository-owned capture inventory workflow was run over the complete
`tools-temp/AOSharpLiveCapture/bin/Debug/captures` tree. Its isolated regenerated
inventory reported:

- `381` sessions discovered;
- `379` sessions with a raw sink;
- `365` canonical-valid sessions;
- `16` recapture-required sessions retained only for positive evidence;
- `92,147` decoded relevant NPC packets;
- `3,269` complete attack chains;
- `260` capture-certified profiles;
- `0` recoverable evidence blockers; and
- `0` decode or projection errors.

Runtime resource `1931` was selected through the existing captured-realm mapping
(`938000` and `477565` to `1931`). It spans `32` sessions. The three older
sessions without `raw-packets.csv` were searched through their complete
`packets.hex.log` sinks; the remaining sessions were searched through their raw
packet indexes.

The checked-in combat catalog and the isolated full-corpus regeneration each
contain `80` PF1931 blocks and have the same PF1931-only SHA-256:
`a6413e59fa48a6e0f30cadc6b5e37226662df585c2d13f6b5d42c0d97adbb427`.
The generator's overall stale result is caused by later non-Temple sessions;
there is no omitted PF1931 combat promotion.

## Recovered door-status contract

The raw-corpus packet-family audit found the omitted Temple world-state family:
`DoorStatusUpdate` (`0x4C7D403B`). Current-realm PF1931 sessions contain `263`
records over `43` distinct door identities. Every identity has a captured closed
snapshot. Three records also carry the mutable open byte for three doors. The
older complete raw sink contributes another `68` door-status records whose
identity allocation differs, proving that runtime statel identity must be used
instead of hard-coded captured instances.

The captured closed packet body for door `0x108CB77A` is:

`4C7D403B0000C748108CB77A000000000200000000000000000003F1`

The shared message definition had two derived fields assigned to member `4`.
`Unknown5` is now member `5`, which restores exact serialize/deserialize order.
The outbound handler preserves the categorical door identity separately from
the mutable state byte and emits the exact captured constants.

PF1931 entry now enumerates the official `playfields.dat` statels, selects each
distinct `IdentityType.Door`, and sends one captured closed-state packet per
door. It does not hard-code captured door instances. The same path is reused for
live entry/re-entry and current-playfield death respawn. It creates no timer,
worker, or retained runtime object.

## Other packet families

The `232` `ChestFullUpdate` rows in PF1931-associated raw indexes are all
`IdentityType.Container` and all use the `127`-byte owned-container shape. They
are not the `155`-byte world-chest shape used by playfield props, so they do not
support adding Temple world chests or a new loot contract.

No PF1931 combat catalog delta was found. Previously documented nano and loot
fail-closed boundaries therefore remain unchanged.

## Exact remaining blocker

The corpus proves closed and open `DoorStatusUpdate` snapshots, but it does not
prove the server-side open trigger, proximity radius, hold time, close cadence,
or recipient scope. The three open snapshots occur inside visibility floods and
have no captured client door-use request that uniquely owns the transition.
Official statel data supplies identity and position, not that runtime timing
law. Dynamic open/close transitions remain fail-closed rather than using a
guessed radius or timer.

## Validation

- `N3RecoveredContractTests`: `20/20` pass, including exact captured door body.
- `TempleDoorStatusRuntimeTests`: `2/2` pass.
- `TempleOfThreeWindsOrdinaryContentTests`: `7/7` pass.
- `DungeonNamedEncounterCompletionTests`: `11/11` pass.
- `DungeonNamedLifecycleCompletionTests`: `20/20` pass.
- `CapturedEnemyCombatPacketFactoryTests`: `38/38` pass.
- `CapturedEnemyCombatProfileCatalogTests`: `51/51` pass.
- `PlayfieldCollisionGeometryTests`: `17/17` pass.
- `NpcChaseNavigationTests`: `38/38` pass.
- Debug build: pass after the approved engine stop sequence released output DLLs.
