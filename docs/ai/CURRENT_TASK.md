# Current Task

Generated: 2026-06-14

## Current Objective

Finalize AO Rebirth licensing and third-party runtime attribution while making
documentation-only changes.

Scope:

- Scan runtime source, runtime project references, and `AORebirth/packages`.
- Document CellAO attribution, AO Rebirth proprietary ownership, and all detected third-party runtime dependencies.
- Verify that `tools-temp`, AOSharp, EasyHook, test packages, and historical captures are excluded from runtime distribution.
- Do not modify runtime code, SQL schemas/data, config connection strings, capture data, or tools.

## Current Implementation State

- Root `NOTICE` now contains the final licensing structure, CellAO attribution, AO Rebirth proprietary clarification, distribution exclusions, and runtime third-party notices.
- Runtime project closure was identified from `ChatEngine`, `LoginEngine`, `ZoneEngine`, and `WebEngine` plus referenced library projects.
- Bundled runtime source notices cover CellAO, `SmokeLounge.AOtomation.Messaging`, and `MsgPack.Cli` / `MsgPack.Mono`.
- Runtime NuGet notices cover Dapper, DotNetZip, Google.Protobuf, K4os packages, MathNet.Numerics, MemBus, Microsoft runtime support packages, MySqlConnector, NBug, NLog, Npgsql, and System.* support packages.
- AOSharp and EasyHook remain outside runtime distribution scope.

## Validation

- Documentation-only edit; no runtime build was required for behavior validation.
- Licensing/exclusion validation scans:

```powershell
rg -n "GPL|AGPL|LGPL|GNU General Public License|WCell|wcell\.org" AORebirth\Libraries\Source AORebirth\Server -S --glob '!**/bin/**' --glob '!**/obj/**' --glob '!**/SqlTables/**'
rg -n "AOSharp|EasyHook" AORebirth\AORebirth.sln AORebirth\Server AORebirth\Libraries -S --glob '!**/bin/**' --glob '!**/obj/**' --glob '!**/SqlTables/**'
rg -n "tools-temp|AOSharp|EasyHook" AORebirth\AORebirth.sln AORebirth\Server AORebirth\Libraries -S --glob '!**/bin/**' --glob '!**/obj/**' --glob '!**/SqlTables/**'
```

## Next Step

Commit the documentation-only licensing update after validation is complete.
