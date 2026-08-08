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
| 4 | Communication and Core | Replace or adapt MemBus without changing ordering; retain MEF discovery | ISCom ordering/concurrency and dynamic-message resolution parity |
| 5 | PlayfieldLoader and ChatEngine | Remove NBug WinForms startup, deploy `Config.xml`, package `playfields.dat`, fix Linux paths, add service shutdown | Chat startup/login/chat/channel packet parity and clean shutdown on Ubuntu |
| 6 | Ubuntu service package | `linux-x64` publish, unprivileged service account, systemd unit, logs, backups and firewall | Restart/reboot recovery and sustained multi-player soak test |

Current status: Stages 0 through 2 pass their Windows-hosted build and
compatibility gates. Stage 2 has exact legacy/Linux public-contract parity and
ObjectManager runtime smoke coverage. Native Ubuntu execution is still required
before the full cross-platform exit gates are complete.

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

## Remaining known ChatEngine blockers

- NBug 1.2.2 selects WinForms and must be replaced with headless exception
  logging in the Linux lane.
- Database entity blobs use `System.Data.Linq.Binary`, which modern .NET does
  not provide.
- Communication uses the legacy MemBus `Net40-Client` asset.
- Chat log and datafile paths assume Windows separators/current directory.
- `playfields.dat` is required at startup but is not currently a publish asset.
- systemd shutdown needs a reliable stop path; the existing shutdown-file
  mechanism can be used for the first smoke deployment.

The Stage 1 Linux lane now replaces Utility's Windows performance counters and
uses canonical `Config.xml` casing without changing the Windows code path.

## Ubuntu test input still needed

Before Stage 6 validation, provide the VPS host, SSH port, SSH user, and the
local path to the SSH key (or confirm another authentication method). Database
credentials should be placed directly on the server as protected environment
or configuration files and must not be committed or pasted into build logs.
