# Authoritative ZoneEngine backend and later deployment

Normal Windows and Linux startup use `ZoneEngine_New`. Linux publishes the same
SDK project and source files as Windows; no independent new Linux runtime fork
exists. `LinuxBuild/build-linux.sh` includes the shared project plus explicit
legacy rollback build coverage. `publish-zoneengine.sh linux-x64 true` produces
`LinuxBuild/artifacts/zoneengine/linux-x64/self-contained/ZoneEngine_New`.
The `.cmd` publisher uses the same selection and project.

The default service is `ao-rebirth-zoneengine.service`, running the apphost above
under `/opt/ao-rebirth/zoneengine/current`. It requires startup validation, a
read-only database validation, `Type=notify` readiness, SIGTERM support and the
governed shutdown file. Configuration remains external through
`AO_REBIRTH_CONFIG_PATH`, `AO_REBIRTH_MYSQL_CONNECTION`, `AO_REBIRTH_BIND_MODE`,
and the existing internal connection settings. Ports remain unchanged. Public
binding remains an explicit production environment choice; offline acceptance
forces loopback and uses no live client.

## Acceptance and package identity

`BackendIntegrationGuard` evaluates the shared project for `win-x64` and
`linux-x64`, compares exact C# identities and rejects runtime source omissions.
It verifies default publisher/service selection and explicit legacy selection.
The established `SourceInventoryGuard` still validates all legacy/shared enum,
source, SQL and copied-content inventories before builds. SQL package checks
compare exact identities, casing and file hashes; Stage 5/7 compare the actual
published set to the authoritative database project, rather than a fixed count.
Negative fixtures reject missing, unexpected, case-drifted and duplicate assets.

Exact acceptance is Windows first, then the same commit through
`LinuxBuild/accept-linux-sha.sh --expected-sha <approved-sha>
--expected-placement-manifest-sha <windows-manifest-sha256>
--workspace <controlled-acceptance-workspace>`. The wrapper builds the shared
New engine, runs its tests, publishes Chat/Login/New and the explicit rollback
package, validates source/package parity and offline startup/placement provenance,
and runs transactional deployment failure fixtures. The source must remain
tracked-clean. Accepted SHA, placement digest and apphost hash are recorded in
the package provenance and release manifest.

## Later production transition (not performed by integration)

1. Record the exact approved Git SHA from matching Windows/Linux acceptance,
   accepted release manifest and `ZoneEngine_New` artifact hash. Never substitute
   a newer branch tip. Verify artifact and unit hashes with the governed release
   manifest preflight.
2. Review the separately produced database migration plan. Verify the actual
   production schema read-only; repository state cannot establish live schema
   compatibility. Obtain the required database backup and prove restore access.
3. Close login admission and stop LoginEngine, then ZoneEngine after confirming
   the zero-online requirement. Retain ChatEngine unless its independent release
   requires a change. Schedule downtime for backup verification, explicit schema
   migration, validation, service start and live acceptance.
4. Apply only the separately approved explicit migration-tool command and exact
   migration identity set while game services are stopped. No runtime start or
   service `ExecStartPre` may perform schema changes. Validate schema status and
   backup evidence before installing the new release pair.
5. Follow `production-release/upgrade-active-services.sh` preflight and governed
   stopped-pair recovery procedure for the concrete live state. The release tool
   snapshots prior apphost identities, unit hashes and current symlink targets.
   It validates the candidate against the external production configuration and
   compatible schema before changing either release.
6. Start ChatEngine if needed, then LoginEngine, then ZoneEngine_New. Require
   systemd readiness, stable restart counters, expected private internal links,
   expected public endpoints, and a separately authorized live-client login,
   zoning, inventory/reconnect and trade acceptance.
7. Roll back if readiness, source/artifact provenance, schema compatibility,
   listener ownership or gameplay acceptance fails. Preserve logs. Restore the
   exact prior release pair and units together using the captured transaction
   state. Do not restart a prior engine against an incompatible migrated schema.

Application rollback does not reverse schema or item data. MySQL DDL is not
assumed transactional; migration rollback limitations and any backup restore
require the separate migration plan. An incompatible prior engine pair stays
stopped until a reviewed recovery restores compatibility.

## Explicit legacy selection

Publish with `LinuxBuild/publish-zoneengine.sh linux-x64 true legacy` (or the
same third argument to `.cmd`). This produces only
`LinuxBuild/artifacts/zoneengine-legacy/linux-x64/self-contained/ZoneEngine` and
runs the existing Stage 8 legacy compatibility smoke. For a deliberate standalone
rollback installation, use `ao-rebirth-zoneengine-legacy.service` with the legacy
package under `/opt/ao-rebirth/zoneengine-legacy/current`. The two unit templates
conflict so only one authoritative ZoneEngine may run. Restoring a previously
deployed release instead uses its captured exact unit and symlink, as above.
Historical Stage 9 live/disabled-service workflows require the explicit
`AO_REBIRTH_LEGACY_ROLLBACK=YES` selection and must not be used for NewEngine.

These commands and templates are repository readiness artifacts. This task does
not install units, replace production binaries, start/stop production services,
alter public networking, use a live AO client, or migrate production.
