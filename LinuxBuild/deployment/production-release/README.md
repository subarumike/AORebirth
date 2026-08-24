# Governed active production release

This workflow deploys LoginEngine, ZoneEngine, and both repository-controlled
systemd units as one transaction from one accepted source SHA. It preserves the
current immutable release pair and installed units in a rollback snapshot before
stopping either service.

`LinuxBuild/accept-linux-sha.sh` publishes both self-contained engines, runs the
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

Run non-mutating validation first:

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

The transaction does not modify production environment files, configuration,
database schema, bind policy, lifecycle behavior, recovery behavior, or gameplay.
