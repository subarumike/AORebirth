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

The transaction does not modify production environment files, configuration,
database schema, bind policy, lifecycle behavior, recovery behavior, or gameplay.
