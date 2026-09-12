# Delmus ZoneEngine Windows and Linux integration acceptance

Date: 2026-09-08

## Provenance

- Starting reconciled master SHA: `6e90dda030774726aa2060acb9edb756ea1f635c`
- Cross-platform validated integration SHA: `108798f6c29f1fbdc79fcfe8b14bd975dfdebdec`
- Branch: `codex/windows-linux-integration-6e90dda`
- Windows worktree: `C:\Users\Mike\Documents\AORebirth-windows-linux-integration-6e90dda`
- Controlled Linux workspace: `/srv/ao-rebirth-linux-acceptance`
- Windows and Linux placement manifest SHA-256: `484f755e774d848911158ff7509439f549f8bd3d3784f2e0bd00cf1bf59976db`
- Generated-combat accepted integrity identity: `9d7fe7bd3b8a4808dde4b999fd3ce009db33f83e1c299e753b92a88b299b01a6`

## Integration repairs

The exact Delmus SHA passed Windows acceptance but failed closed on Linux before publication because generated Linux source inventories had not been reconciled with the new source and SQL assets. The inventory generator produced two required changes:

- `AORebirth.Enums.CompileItems.props` now includes `ItemSource.cs`, `ItemClass.cs`, `MeshLayer.cs`, and `WeaponFlags.cs`, and no longer references removed `ItemType.cs`.
- `AORebirth.Database.ContentItems.props` now packages `SqlTables/item_instances.sql`.

After that repair, Linux compilation passed and the offline artifact contract failed closed because two checks still expected 35 packaged SQL assets. Both checks now require exactly 36 assets and explicitly require `SqlTables/item_instances.sql` exactly once. The governed Stage 6 schema-application list was deliberately not changed. No schema was applied.

## Windows acceptance

At exact SHA `108798f6c29f1fbdc79fcfe8b14bd975dfdebdec`:

- Legacy Debug build: PASS.
- `ZoneEngine_New` Debug build: PASS.
- `ZoneEngine_New.Tests`: PASS, 47/47.
- Complete AOtomation suite through the mandatory repository gate: PASS.
- Mandatory repository gate: PASS, 12/12 stages.
- DAO architecture guard: PASS; 8 production SQL sites, 8 baseline exceptions, 0 new violations, 0 mission runtime direct SQL sites.
- Generated-combat integrity: PASS.
- Exact-SHA Windows acceptance: PASS.
- Evidence: `build-verify/windows-acceptance-108798f6.env` (ignored build artifact).

## Linux acceptance

The approved acceptance workflow fetched and detached the controlled repository at exact SHA `108798f6c29f1fbdc79fcfe8b14bd975dfdebdec`. The tracked source remained clean.

- Legacy LoginEngine and ZoneEngine Release builds: PASS.
- Self-contained `linux-x64` publish: PASS.
- Stage 7 LoginEngine offline structure test: PASS.
- Stage 8 ZoneEngine offline smoke: PASS.
- Production deployment workflow fixture tests: PASS, 56/56.
- Placement artifact provenance tests: PASS, 8/8.
- Release manifest provenance: PASS at the exact accepted SHA.
- `ZoneEngine_New` Release build: PASS.
- `ZoneEngine_New.Tests`: PASS, 47/47.
- Exact-SHA Linux acceptance: PASS.
- Legacy LoginEngine artifact: `/srv/ao-rebirth-linux-acceptance/repo/LinuxBuild/artifacts/loginengine/linux-x64/self-contained`.
- Legacy ZoneEngine artifact: `/srv/ao-rebirth-linux-acceptance/repo/LinuxBuild/artifacts/zoneengine/linux-x64/self-contained`.
- Release manifest: `/srv/ao-rebirth-linux-acceptance/repo/LinuxBuild/artifacts/production-release/release.manifest`.

## Production and runtime boundaries

- Legacy ZoneEngine remains the default Windows launcher route. `ZoneEngine_New` requires the explicit `-NewZoneEngine` switch.
- The governed Linux systemd unit still executes `/opt/ao-rebirth/zoneengine/current/ZoneEngine`, the legacy engine.
- `ZoneEngine_New` was built and tested but not started. Its startup path calls `DatabaseMigrationRunner.TryApplyPendingMigrations()`, which can create migration tracking, bootstrap missing tables, and offer pending migrations. Runtime isolation therefore remains blocked until a separately reviewed schema-safe startup mode or explicit database authorization exists.
- No production deployment was performed.
- No production service was started, stopped, restarted, reloaded, or reconfigured.
- No database migration or schema change was applied, and no production database was accessed or modified.
- No live AO client was launched or controlled.
- No public bind, firewall, DNS, proxy, or other network exposure was changed.

## Known warnings and remaining blockers

- `DotNetZip` 1.16.0 reports a high-severity vulnerability warning (`NU1903`).
- `System.Drawing.Common` 4.7.0 reports a critical-severity vulnerability warning (`NU1904`).
- `System.Text.Encoding.CodePages` is a redundant package reference (`NU1510`).
- Existing obsolete API, nullability, casing, and platform analyzer warnings remain in legacy and shared code.
- These warnings did not fail the build, but the dependency vulnerabilities require separate remediation before declaring `ZoneEngine_New` production-ready.
- `ZoneEngine_New` is build-ready on Windows and Linux, not production-enabled or runtime-accepted.

## Files inspected

- `AI_START_HERE.md`
- `docs/project/DEVELOPMENT_AUTHORITY.md`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/KNOWN_DECISIONS.md`
- `docs/ai/SUBSYSTEMS.md`
- `docs/ai/ARCHITECTURE.md`
- `docs/ai/WORKFLOW.md`
- `LinuxBuild/accept-linux-sha.sh`
- `LinuxBuild/publish-zoneengine.sh`
- `LinuxBuild/Tools/SourceInventoryGuard/Program.cs`
- `LinuxBuild/source-inventory/inventory.json`
- `LinuxBuild/deployment/systemd/ao-rebirth-zoneengine.service`
- `LinuxBuild/deployment/mysql-stage6/apply-governed-schema.sh`
- `start-engines.ps1`
- `AORebirth/Server/ZoneEngine_New/Program.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Data/DatabaseMigrationRunner.cs`

## Execution errors

Two read-only targeted `rg` calls referenced a nonexistent smoke-script path and an invalid Windows glob. Both failed without modifying the repository; inspection was repeated with exact existing paths.

## Conclusion

The reconciled source required four narrowly scoped Linux acceptance repairs. Exact SHA `108798f6c29f1fbdc79fcfe8b14bd975dfdebdec` is reproducible and accepted on Windows and Linux for legacy build/publish and for `ZoneEngine_New` build/tests. It is build-ready, not production-ready: Legacy remains the only default and governed production route, and new-engine runtime acceptance remains blocked by its migration-capable startup path and unresolved dependency advisories.
