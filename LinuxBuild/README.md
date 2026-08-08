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
`Cell.Core`, `Utility`, `AORebirth.Enums`, `AORebirth.Core.Exceptions`,
`AORebirth.Interfaces`, `AORebirth.ObjectManager`, `AORebirth.Database`, and
`AORebirth.Stats`, and `AORebirth.Communication` from guarded legacy
source/resource inventories. Database's 34 SQL assets are guarded from the
legacy Content inventory and copied exactly to build and publish outputs.
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

Stage 2 public API parity is pinned in
`Tools/CompatibilitySmokeTests/Fixtures/LegacyStage2PublicContracts.manifest`.
It exhaustively fingerprints the four legacy assemblies' metadata, enums,
interfaces, exceptions, and ObjectManager public surface. After the approved
Windows Debug build, verify both the legacy and Linux assemblies with:

```bat
LinuxBuild\verify-stage2-contracts.cmd
```

`Tools/LegacyStage2ContractTool` reproducibly generates/verifies that baseline
from the .NET Framework binaries and is intentionally excluded from the Linux
solution.

The Linux Interfaces and Exceptions overlays deliberately omit the source-unused
legacy MemBus and NLog references; the contract smoke fails if either dependency
reappears.

Stage 3 preserves the Database and Stats public/runtime contracts while using
net10-compatible Dapper, MySqlConnector, Npgsql, and Microsoft.Data.SqlClient.
The Linux-only `System.Data.Linq.Binary` type matches the .NET Framework public
API and byte/hash/serialization behavior. Run the legacy/Linux semantic gate
and the database-free runtime/artifact gate after the approved Windows build:

```bat
LinuxBuild\verify-stage3-contracts.cmd
LinuxBuild\verify-stage3-offline.cmd
```

The offline gate exercises Dapper binary parameters/materialization, closed
provider construction, SQL generation, and safe Stats behavior without opening
a connection. It also verifies the exact 34 SQL assets by name, case, length,
and SHA-256 in source, build, and `linux-x64` publish output. MySQL remains the
only operationally supported schema dialect; PostgreSQL and SQL Server are
compile-covered only.

Stage 4 replaces Communication's source-dead net40-only MemBus dependency with
an identity-compatible inert Linux assembly and configures TCP keepalive with
portable socket options while leaving the Windows IOControl path unchanged.
Its strict contract gate covers public/protected metadata, defaults, dependency
identities, legacy MsgPack goldens, production ISCom framing, culture/thread
stability, and DynamicMessage resolution. Its bounded offline gate uses only an
ephemeral IPv4 loopback listener:

```bat
LinuxBuild\verify-stage4-contracts.cmd
LinuxBuild\verify-stage4-offline.cmd
```

Stage 5 builds ChatEngine as a .NET 10 executable. `PlayfieldLoader` and its
unused startup cache remain excluded. Chat authentication did contain a hidden
Core dependency through the legacy `AO.Core.Encryption` namespace, so the Linux
lane links the exact `BigInteger`, `PasswordHash`, and `LoginEncryption` sources
into a contained `AORebirth.Chat.Authentication` assembly instead of publishing
a misleading partial `AORebirth.Core` assembly. Full Core remains in scope for
later LoginEngine/ZoneEngine work.

The Linux entrypoint excludes NBug/WinForms, logs to the console for journald,
strictly validates exact-case configuration, keeps ISCom loopback-only by
default, requires the MySQL secret from the environment, detects listener bind
failures, reports readiness to systemd only after both listeners pass, and shuts
down both listeners through SIGTERM/SIGINT without `Environment.Exit`. Offline
startup validation constructs only closed provider objects and listener-free
topology:

```bat
set AO_REBIRTH_CONFIG_PATH=%CD%\LinuxBuild\Projects\bin\ChatEngine.Linux\Release\net10.0\Config.xml
set AO_REBIRTH_MYSQL_CONNECTION=Server=127.0.0.1;Database=offline;Uid=offline;Pwd=offline
set AO_REBIRTH_CHAT_LISTEN_IP=127.0.0.1
set AO_REBIRTH_ISCOM_LISTEN_IP=127.0.0.1
set AO_REBIRTH_REQUIRED_SQL_TYPE=MySql
dotnet LinuxBuild\Projects\bin\ChatEngine.Linux\Release\net10.0\ChatEngine.dll --validate-startup
```

Publish the first Ubuntu package with:

```bat
LinuxBuild\publish-chatengine.cmd linux-x64 false
```

Pass `true` as the second argument for a self-contained package. Deployment
layout, secret handling, systemd, and firewall boundaries are documented in
[`deployment/README.md`](deployment/README.md).

Linux projects import checked-in source inventories generated directly from
the legacy project files. Validate all inventories independently with:

```sh
dotnet run --project LinuxBuild/Tools/SourceInventoryGuard/SourceInventoryGuard.csproj -- \
  --repository-root . \
  --manifest LinuxBuild/source-inventory/inventory.json \
  --check
```

This checkpoint includes a publishable ChatEngine build plus passing
Windows/Linux contract, authentication, negative configuration, lifecycle, and
publish-structure gates. Native Ubuntu 24.04.4 x86_64 apphost execution,
listener-free validation, systemd readiness with both loopback listeners, and
real SIGTERM delivery also pass. Live database parity, boot persistence, and
player traffic remain pending.
The staged dependency and Ubuntu deployment path is recorded in
[`PORTING_PLAN.md`](PORTING_PLAN.md).
