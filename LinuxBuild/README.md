# AORebirth parallel Linux build

This directory adds a Linux-targeted build lane alongside the existing
Windows/.NET Framework solution. The existing project files, lifecycle
wrappers, packet behavior, gameplay source, and database schema remain the
Windows reference authority.

The Linux lane starts with leaf libraries and advances toward ChatEngine,
LoginEngine, and ZoneEngine only after each dependency builds and passes
cross-runtime parity validation.

Build with the repository-selected .NET 10 SDK. On Windows:

```bat
LinuxBuild\build-linux.cmd
```

On Linux:

```sh
./LinuxBuild/build-linux.sh
```

Both wrappers verify every checked-in source inventory before building, then
run assembly-identity, MsgPack byte-vector/round-trip, and translation-resource
smoke checks. The current first slice compiles
`SmokeLounge.AOtomation.Messaging`, `Cell.Util`, `MsgPack.Mono`, and
`Translations` from the same source and resource files used by the legacy
projects. It does not yet produce a deployable engine.

Linux projects import checked-in source inventories generated directly from
the legacy project files. Validate all inventories independently with:

```sh
dotnet run --project LinuxBuild/Tools/SourceInventoryGuard/SourceInventoryGuard.csproj -- \
  --repository-root . \
  --manifest LinuxBuild/source-inventory/inventory.json \
  --check
```

This checkpoint proves modern-.NET compile feasibility only. It is not yet a
ChatEngine build, Linux runtime validation, packet-parity proof, or deployment.
The staged dependency and Ubuntu deployment path is recorded in
[`PORTING_PLAN.md`](PORTING_PLAN.md).
