# Governed active production release

This workflow deploys LoginEngine, ZoneEngine_New, and both repository-controlled
systemd units as one transaction from one accepted source SHA. It preserves the
current immutable release pair and installed units in a rollback snapshot before
stopping either service.

## Build and validate the deployment archive

After exact-source Windows and native Linux acceptance, use the tracked builder
from the accepted native Linux checkout. The input manifest must pin both previous
release directories for the authorized transaction. The builder preserves those
pins and binds candidate paths to the supplied extraction destination:

```bash
python3 LinuxBuild/deployment/production-release/package-release.py \
  --expected-sha <accepted-public-master-sha> \
  --runtime-source-sha <accepted-public-master-sha> \
  --manifest LinuxBuild/artifacts/production-release/release.manifest \
  --destination /srv/aorebirth-release-<corrected-release-short-sha> \
  --output /evidence/linux-release-<corrected-release-short-sha>.tar.gz
```

`PACKAGE_IDENTITY.json` records identical runtime and release/tooling source
SHAs and lists every transitive deployment dependency, its
consumer, archive path, hash and executable requirement. Shell `source` directives
are followed recursively; unresolved expressions and missing helpers fail closed.
Both complete published engine directories include runtime libraries, configuration
defaults, acceptance/source receipts, GameData and editable content integrity manifests. New releases use manifest format 3
with `ZONEENGINE_IMPLEMENTATION=new`. The four `CONTENT_*` fields bind the accepted
content manifest, file count and bytes to this artifact; they do not select or authorize
NPC content. Historical format 2 artifacts retain their original placement checks.
`PACKAGE_CONTENTS.tsv` inventories all payload hashes and modes, including the
bound release manifest. Packaging is reproducible for the same accepted inputs.
The archive is independently extracted, hash/mode checked and its real deployment
entry point executed without a source checkout before the builder succeeds.

Extract the verified archive into its exact destination in an isolated Linux
container first, then run the same package-only preflight on the production host
before stopping services:

```bash
bash LinuxBuild/deployment/production-release/upgrade-active-services.sh \
  --manifest LinuxBuild/artifacts/production-release/release.manifest \
  --expected-sha <corrected-release-sha> --package-preflight
```

This validates the complete package and native content/artifact provenance,
without accessing production services or the database. It does not replace the
existing production `--dry-run`. Normal deployment also verifies the package, then
checks every installed engine file against its inventory before either service
starts. Unit hashes remain enforced by the transaction. Runtime sources must be
unchanged from the separately recorded accepted runtime commit.

Package construction needs Python 3's standard library, Git, GNU tar-compatible
archive semantics and Bash. Deployment needs Bash, GNU coreutils/findutils,
grep/awk, systemd, Docker/MySQL, `ss`, `runuser`, and the existing root-controlled
external configuration and protected shared handoff directory. These are host
prerequisites, not files to copy from a developer checkout. The production archive
contains no test-only runtime sources, private configuration, database or ChatEngine
replacement. The prior rollback archive separately preserves the exact ChatEngine.

The candidate apphost must be `ZoneEngine_New`; its default unit performs only
read-only schema preflight before starting. Schema changes require the separate
migration tool and are not part of this transaction. The captured previous
artifact may be `ZoneEngine_New` or an archived legacy `ZoneEngine`; historical recovery restores that
exact apphost identity and prior unit together. Missing or ambiguous previous
apphosts fail closed. See [backend transition prerequisites](../ZONEENGINE_NEW_BACKEND.md).

`LinuxBuild/accept-linux-sha.sh` publishes LoginEngine and NewEngine only, runs the
fixture-backed deployment failure suite, writes accepted provenance into both
artifacts, and generates:

```text
LinuxBuild/artifacts/production-release/release.manifest
```

The production precondition is zero rows matching
`characters.Online IS NOT NULL AND Online <> 0`. There is no implicit player
disconnect or invented drain path. LoginEngine stops first so it cannot accept a
new session; ZoneEngine then stops. Startup is LoginEngine followed by ZoneEngine,
matching the ZoneEngine systemd dependency.

Before validation, each root-controlled environment file must contain exactly one
`AO_REBIRTH_CONFIG_PATH` assignment. LoginEngine must name
`/etc/ao-rebirth/loginengine/Config.xml`, and ZoneEngine must name
`/etc/ao-rebirth/zoneengine/Config.xml`. Candidate validation uses those preserved
external configurations because they are the configurations systemd will use in
production; the portable artifact configurations intentionally remain loopback
safe. Missing, duplicate, or divergent assignments fail before service or release
mutation.

Run non-mutating validation first:

Both installed units must have a governed regular unit file, valid definition,
expected fragment path and known service state. The dry-run records runtime
residency separately from `LoadState`: querying properties can load an inactive
unit that systemd immediately garbage-collects again. After unit installation and
`daemon-reload`, startup uses the same validation in forward and rollback paths.
Only failed units or units with nonzero restart counters receive `reset-failed`;
reset failure is fatal. Valid inactive units with zero counters proceed to
`start`, which loads a collected unit as needed. Missing, masked, invalid and
transitioning units fail closed. Controlled-start restart baselines are recorded
after the reset, before startup, so new restarts remain detectable.

The deterministic transaction suite retains 81 historical cases and adds 11 editable
content cases covering deployment, idempotency, integrity failures and paired rollback. The additional real-manager
regression is explicitly restricted to a disposable systemd container:

```bash
AO_REBIRTH_ISOLATED_SYSTEMD_FIXTURE=YES bash LinuxBuild/deployment/production-release/tests/test-systemd-unloaded-real.sh
```

Never run that fixture on a production host. It creates only the dedicated
`aorebirth-systemd-fixture.service` and rejects an existing file with that name.

Pre-install systemd validation uses temporary copies of the reviewed units with
executable paths rebased to the staged artifacts. This supports a first cutover
from Legacy without modifying the active release. Original unit hashes remain
enforced; after installation, systemd validates the original deployment paths.

```bash
bash LinuxBuild/deployment/production-release/upgrade-active-services.sh \
  --manifest LinuxBuild/artifacts/production-release/release.manifest \
  --expected-sha <exact-accepted-sha> \
  --dry-run
```

Only after dry-run passes, run the same command without `--dry-run`. Any failure
after the snapshot transaction begins restores both previous release symlinks and
both previous units, reloads systemd, and starts the prior pair. An already-current
release performs health validation without rotating snapshots or restarting.

If ZoneEngine is already stopped because the active LoginEngine/ZoneEngine pair
is provably incompatible with the current database schema, use the same commands
with `--recover-zone-outage`. This mode requires LoginEngine to remain healthy,
ZoneEngine to remain in an exact stopped state with an unchanged restart count,
port 7501 to remain closed, zero online characters, and both candidate binaries
to pass startup/database validation against production before either current
symlink is changed. Zero-online is rechecked before admission closes and again
after LoginEngine stops while ZoneEngine state is still intact, then both closed
listeners are verified immediately before release mutation. ZoneEngine state,
listener ownership, and restart count must remain unchanged around that Online
query;
players who connect after the recovered listeners open do not invalidate health.
Recovery always starts and stability-checks the candidate,
even when its artifacts are already current, after resetting the historical
ZoneEngine failure counter to establish a zero controlled-start baseline. Because
the prior pair is known to be incompatible, a failed recovery restores its exact
artifacts and units but leaves both engines stopped instead of starting an invalid
rollback generation.

If a governed recovery attempt fails and its rollback leaves both engines stopped,
retry with `--recover-zone-outage --resume-stopped-recovery`. The resume modifier
is accepted only with outage recovery and requires both services and listeners to
remain stopped, unchanged restart counts, zero online characters, and exact
agreement between the deployed-release marker and both rollback artifacts, units,
and source SHAs. It never takes the already-deployed no-op path. A retry failure
restores the exact prior pair and leaves both engines stopped again.

### Explicitly authorized maintenance with players online

When Mike authorizes stopping the live server to deploy a fix, online characters
must not block that maintenance or require a separate manual logout. Gracefully
stop LoginEngine first to close admission, then ZoneEngine to disconnect players
and save their state. Verify both services and listeners are stopped and persisted
online state is zero. Never manually clear online rows or bypass persistence checks.

For this intentionally stopped pair, use the existing
`--recover-zone-outage --resume-stopped-recovery` flags with the exact accepted
manifest and SHA, first with `--dry-run`, then without it after validation passes.
This reuses the stopped-pair transaction; it does not imply a schema outage.
The prior deployed marker, artifacts, units and source identities must agree, and
candidate database validation and rollback snapshots remain mandatory. Failure
restores the prior pair but leaves services stopped; diagnose the failure and
confirm prior NewEngine compatibility before restarting. Do not use
`--prepared-schema-cutover` for ordinary maintenance. Without explicit maintenance
authorization, the normal zero-online deployment gate remains unchanged.

The transaction does not modify production environment files, configuration,
database schema, bind policy, lifecycle behavior, recovery behavior, or gameplay.
It recognizes and transactionally removes only the pinned obsolete
`10-type-simple.conf` ZoneEngine override, proves the effective unit is
`Type=notify`/`NotifyAccess=main`, preserves the governed daily-login drop-in, and
restores the obsolete override byte-for-byte if rollback is required.

For a separately approved schema migration, after stopping all writers, verifying
the backup by restoration, applying only the approved operator migrations and
validating complete data preservation, use `--prepared-schema-cutover` instead of
the outage-recovery flags. Both services and ports must already be stopped, both
previous release paths must be pinned in the accepted release manifest, and both
previous artifacts must carry valid source identities. Different prior engine
SHAs are permitted; no fabricated deployed-release marker is needed. All frozen
state, online, artifact, configuration and candidate database gates still apply.
Run with `--dry-run` first. On failure the exact previous artifacts and units are
restored but services stay stopped. Restore and validate the pre-cutover database
before restarting Legacy; executable-only rollback is never sufficient after
NewEngine writes. This option does not execute or approve any database operation.

### Legacy engine retirement

Fresh Legacy compilation and publication are removed. Normal releases select NewEngine only. Existing historical release directories, rollback snapshots, archived-artifact provenance checks and the separately guarded recovery unit are preserved. Historical recovery may restore an already verified old backup with its exact unit and required database restore; it cannot produce a new Legacy build. This preserves recovery evidence without keeping the obsolete engine in the current build graph.
