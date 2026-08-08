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
run assembly-identity, MsgPack byte-vector/round-trip, resource, portable
metrics, and compression smoke checks. The current foundation compiles
`SmokeLounge.AOtomation.Messaging`, `Cell.Util`, `MsgPack.Mono`, `Translations`,
`Cell.Core`, and `Utility` from guarded legacy source/resource inventories.
Utility uses a Linux-only source for portable CPU/RAM metrics and references a
separate `Ionic.Zlib` compatibility assembly, preserving the original external
type boundary while using modern .NET compression. It does not yet produce a
deployable engine.

The compatibility assembly preserves the legacy `Ionic.Zlib` simple name and
version but is intentionally unsigned. It is not binary-interchangeable with
the strong-named Windows package; every Linux consumer must be rebuilt against
the Linux lane, and Windows/Linux binaries must not be mixed.

Checked-in list and dictionary fixtures were produced by the legacy Windows
Utility/Ionic.Zlib build. The normal compatibility smoke test proves that the
Linux reader accepts both legacy formats; their byte lengths and SHA-256 hashes
are pinned in `Fixtures/LegacyUtilityFixtures.manifest`. After the approved
Windows debug build, run the reverse compatibility gate to prove the legacy
reader accepts a Linux-produced list file—the only format used by current data
loaders:

```bat
LinuxBuild\verify-legacy-compression.cmd
```

`Tools/LegacyUtilityFixtureTool` is the reproducible .NET Framework fixture
writer/list verifier; it is intentionally excluded from the Linux solution.
The legacy dictionary reader calls an unsupported zlib seek operation and
cannot read its own fixture; no current runtime source calls that overload.

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
