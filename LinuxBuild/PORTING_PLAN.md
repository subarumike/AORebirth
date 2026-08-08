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
| 6 | Ubuntu service package | `linux-x64` publish, unprivileged service account, systemd unit, logs, backups and firewall | Restart/reboot recovery and sustained multi-player soak test |

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
loopback listeners, and SIGTERM shutdown also pass. Authorized disposable-MySQL
CRUD parity is still required before the full cross-platform exit gates are
complete.

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

## Remaining ChatEngine acceptance work

- Provision or authorize an isolated disposable MySQL target and verify
  connection/schema readiness before any player authentication test.
- Native listener-free startup/lifecycle and systemd readiness/SIGTERM now pass;
  the full contract tools remain reproducibly covered by the Windows-hosted
  .NET 10 lane.
- Live Database read/write/query parity still requires an authorized disposable
  MySQL schema; offline gates intentionally never open a connection.
- Legacy per-channel chat logging remains disabled because its writer is not
  concurrency-safe; journald server logging is the supported first deployment path.
- Player disconnect persists offline state synchronously, so shutdown can still
  exceed the 45-second systemd limit under heavy load or a slow database.
- Full Core/PlayfieldLoader work remains required for LoginEngine and ZoneEngine,
  including active MemBus, MEF discovery, MathNet replacement, and data assets.

The Stage 1 Linux lane now replaces Utility's Windows performance counters and
uses canonical `Config.xml` casing without changing the Windows code path.

## Ubuntu test input still needed

Before Stage 6 validation, provide the VPS host, SSH port, SSH user, and the
local path to the SSH key (or confirm another authentication method). Database
credentials should be placed directly on the server as protected environment
or configuration files and must not be committed or pasted into build logs.
