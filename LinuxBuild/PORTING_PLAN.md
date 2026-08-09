# AORebirth Linux parallel-build plan

## Boundary

The existing .NET Framework solution and Windows wrappers remain the reference
build. Linux work uses sibling SDK-style projects, links the existing source
inventory, and enables compatibility code only with `AOREBIRTH_LINUX`. No
database schema or packet behavior changes are part of the port.

ChatEngine is the first deployable engine milestone. LoginEngine and ZoneEngine
follow only after the shared library lane and ChatEngine runtime are proven.

## Stages

| Stage | Scope | Main portability work | Exit gate |
| --- | --- | --- | --- |
| 0 | Messaging, Cell.Util, MsgPack.Mono, Translations | SDK overlays, exact inventories, Reflection.Emit in-memory support | Windows wrapper, Linux wrapper, assembly identity, MsgPack byte vector and resources pass |
| 1 | Cell.Core, Utility, and Ionic.Zlib adapter | Modern NLog, portable CPU/RAM metrics, modern resource handling, unsafe parser build | Two-way list compression parity, legacy dictionary ingestion, and runtime tests pass on Windows and Ubuntu |
| 2 | Enums, Exceptions, Interfaces, ObjectManager | Preserve assembly boundaries and shared assembly metadata | Full contract closure builds with no framework fallback packages |
| 3 | Database and Stats | Modern Dapper/MySqlConnector, replace `System.Data.Linq.Binary`, keep schemas unchanged | Read/write/query parity against a disposable test database |
| 4 | Communication and Core dependency audit | Adapt Communication's inert MemBus boundary without changing ISCom ordering; identify the smallest Core slice needed by Chat | ISCom framing/FIFO and dynamic-message resolution parity; guarded full-Core exclusion |
| 5 | ChatEngine | Remove NBug WinForms startup, extract the three required authentication sources, omit the unused PlayfieldLoader cache, deploy `Config.xml`, fix Linux paths, add service shutdown | Chat contracts/offline startup/publish parity and clean shutdown on Ubuntu |
| 6 | Ubuntu ChatEngine database acceptance | Isolated MySQL 8.4 target, governed schema bootstrap, restricted runtime account, live DB preflight and bounded login harness | Exact 34-table import, production Connector/DAO/encrypted-login parity, zero fixture residue, disabled systemd readiness and shutdown pass |
| 7 | LoginEngine | Audit its exact dependency closure, add a guarded SDK overlay, remove Windows-only startup dependencies, and reuse the strict config/database/service patterns | Windows/Linux contracts plus listener-free and loopback Ubuntu lifecycle pass |
| 8 | ZoneEngine and persistent stack | Port the full Core/PlayfieldLoader/data closure, coordinate multi-engine readiness and bounded shutdown | Restart/reboot recovery and sustained multi-player soak test |

Current status: Stages 0 through 4 pass their Windows-hosted compile, contract,
offline runtime, publish-artifact, and compatibility gates. Stage 3 preserves
Database/Stats API and mapping contracts, proves Dapper binary conversion and
safe Stats behavior without a live database, and carries all 34 SQL assets
exactly. Stage 4 preserves Communication API/wire/framing behavior with bounded
loopback coverage and an inert identity-compatible MemBus adapter. Stage 5 now
builds and publishes ChatEngine with strict configuration, private ISCom bind,
headless exception logging, env-only deployment secrets, systemd readiness,
and coordinated shutdown. PlayfieldLoader and full Core are deferred, while the
exact three Core authentication sources are kept in a contained Linux assembly.
Strict Stage 5 contract and offline artifact gates now pass locally; native
Ubuntu 24.04.4 x86_64 apphost, listener-free lifecycle, systemd readiness, both
loopback listeners, and SIGTERM shutdown also pass. Stage 6 now passes an exact
governed 34-table import into a uniquely named/labeled MySQL 8.4 target, a
restricted runtime account, production Connector/DAO/password/encrypted-login
behavior, negative authentication, zero-residue cleanup, and the service's new
read-only live database `ExecStartPre`. Stage 7 now builds LoginEngine from its
exact 35-source inventory plus a contained identity-compatible Core slice. The
unchanged legacy adapters use pinned MemBus 4.0.1 for active six-handler MEF
dispatch. Windows/Linux contracts, offline lifecycle, native apphost structure,
Ubuntu live database preflight, `Type=notify` readiness, exact PID-owned
`127.0.0.1:7500`, and clean SIGTERM pass. Both services remain disabled and all
player/ISCom/database listeners remain loopback-only.

## Rules for each stage

1. Add each Linux project to `source-inventory/inventory.json` and import its
   generated inventory; default compile/resource globs remain disabled.
2. Run `build-linux.cmd` during Windows development and `build-linux.sh` on the
   Ubuntu VPS.
3. Run the existing Windows build and focused tests after any shared-source
   compatibility change.
4. Do not accept `NU1701` or another silent .NET Framework package fallback.
5. Do not remove a dependency or startup action until its behavioral parity
   test passes.

## Remaining engine acceptance work

- Native listener-free startup/lifecycle, live database preflight, bounded
  authentication, systemd readiness, and SIGTERM now pass; the full contract
  tools remain reproducibly covered by the Windows-hosted .NET 10 lane.
- Legacy per-channel chat logging remains disabled because its writer is not
  concurrency-safe; journald server logging is the supported first deployment path.
- Player disconnect persists offline state synchronously, so shutdown can still
  exceed the 45-second systemd limit under heavy load or a slow database.
- Stage 7's narrow active Core/MemBus/MEF closure is proven only for LoginEngine;
  full Core, PlayfieldLoader, MathNet replacement, and ZoneEngine data assets
  remain Stage 8 work.
- LoginEngine TCP 7500 remains disabled and loopback-only. Public exposure is
  blocked on an authenticated per-connection state machine, ownership checks for
  character mutations, and a cryptographically secure server salt.
- LoginEngine shutdown does not yet drain outstanding asynchronous MemBus work,
  and its live readiness gate does not yet fingerprint every runtime table
  column/index or exercise the full create/delete DAO graph.

The Stage 1 Linux lane now replaces Utility's Windows performance counters and
uses canonical `Config.xml` casing without changing the Windows code path.

## Ubuntu test boundary

The Ubuntu 24.04 test VPS and SSH key are configured outside the repository.
Database credentials remain root-owned on the VPS and are never committed or
printed. The disposable database uses a loopback-only host binding and
`--restart=no`; ChatEngine and LoginEngine remain disabled until a later
player-test approval.
