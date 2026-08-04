# Database and Server Repair Reconciliation - 2026-08-04

## Starting state

- Branch: `master`
- Starting commit: `05782be291acba9eba19596734cd0e9fb824b526`
- `origin/master`: identical to the starting commit; divergence `0/0`
- Worktree: ten modified tracked files, with no added, deleted, renamed, or
  untracked files
- Preserved diff: ignored repository-local evidence at
  `.git/aorebirth-audits/database-server-repair-20260804-starting.diff`
- Preserved diff SHA-256:
  `5a1412312c74f71b0b08c33c206fcb9b64c543dcd8d443bd1ba14da05403db26`
- Initial engine ownership: ChatEngine PID 21976 owned ports 6996 and 7012;
  LoginEngine PID 28528 owned port 7500; ZoneEngine PID 13012 owned port 7501;
  optional WebEngine was absent and port 8181 was closed.

## Original ten-file inventory

All ten files were modified tracked files.

| File | Bytes | Starting SHA-256 | Classification | Decision |
| --- | ---: | --- | --- | --- |
| `AORebirth/Config/Config.xml` | 1,441 | `5f89f9068e7ab9e8169b56cc91ae823abd24b3c6c2ac3ec6e8ee782a8428120e` | Tracked runtime configuration containing a private connection value | Discarded; restored placeholder |
| `Tools/DatabasePreflight/DatabasePreflightCommand.cs` | 20,145 | `6d0c8d80390eed05ceb0dff789a9fc2b20388690810bddcc9d367e4d5d951b2c` | Production preflight command | Discarded configuration fallback |
| `Tools/DatabasePreflight/Program.cs` | 1,107 | `225b3cc97cd9dd94c3ff0af0eae0e3e92365c59b884a5e0ad2caa7389f3affcd` | Production preflight entry point | Discarded configuration fallback |
| `Tools/run_database_preflight_tests.cmd` | 1,391 | `107bcf9c534f5ba94e17dff4ed87bc1baf2d3550e842d158bc86084a32b4de6e` | Focused test wrapper | Discarded weakened missing-credential expectation |
| `Tools/run_engine_management_tests.cmd` | 775 | `4bca9b32fb10d06c121cd1983c9752e2420e67b121c864187a0551728f97e24c` | Focused test wrapper | Discarded removal of fail-closed startup tests |
| `Tools/scan_secrets.py` | 2,748 | `8c84b55ff0040c3c064b94018b721cc1926140fd9fd9791d6246e40c20883d14` | Mandatory security check | Discarded tracked-config exemption |
| `docs/ai/WORKFLOW.md` | 34,266 | `5526c5ee3cf6448b24c562146b28e80b998d4eda7c79b33ca61e240fef10712b` | Workflow documentation | Discarded insecure configuration guidance |
| `docs/project/PROJECT_STATE.md` | 10,561 | `e95108a08c1be59980cbf02465bfbd5d4214e493c577e7da7f4e4bcf7fd3ae58` | Project-state documentation | Corrected to the secure model and accepted lifecycle repair |
| `start-engines.ps1` | 10,067 | `ef2a0d4f94152a6ec5791046d1cb56b8c0ad25f38760dd6a681b2629960099f5` | Managed startup implementation | Retained required status-probe arguments |
| `stop-engines.ps1` | 6,228 | `e6484a75aa6b2a9d6b88e2be729f16f90644b13e3e94f6560931355c569a9cdb` | Managed shutdown implementation | Retained probe arguments and parse repair |

The starting diff contained no generated output, database dump, runtime log, or
temporary diagnostic file. Runtime logs were preserved only in the ignored
audit location and were not committed. No credential value is reproduced here.

## Root cause and reconciliation

The preserved work combined two unrelated concerns. The valid concern was a
lifecycle-wrapper defect: `Tools/engine_status_probe.js` requires both
`--config` and `--engine-dir`, but direct calls made by managed start and stop
omitted them. The shutdown trust condition also placed PowerShell `-and`
operators at the beginning of continuation lines, which caused a parse failure.
This could prevent safe managed shutdown and make ownership verification fail
before or after launch.

The other concern attempted to persist a private database connection in tracked
configuration, make preflight fall back to it, exempt the file from secret
scanning, remove fail-closed missing-credential tests, and document the weaker
model. That concern was rejected. It conflicts with the established
process-environment credential boundary and would conceal a tracked secret.

The accepted repair supplies the required immutable repository config/engine
paths to every start/stop status-probe call and keeps continuation operators on
the preceding PowerShell condition lines. Focused contracts now pin both
requirements. No engine, gameplay, packet, generated, WebEngine, or database
schema behavior was changed.

## Database and online-state safety

Database preflight remains read-only and fail-closed. It requires the
`AO_REBIRTH_MYSQL_CONNECTION` value in the approved process environment,
authenticates to the expected database, verifies all 34 required tables and
their read access, verifies the `characters.Online` column, and blocks startup
when any row has a nonzero online value. It does not migrate schema or mutate
data.

ZoneEngine still calls `Misc.LogOffAll()` during initialization, which executes
a broad `UPDATE characters SET Online=0`. The ten-file repair does not change
that behavior. The approved startup/restart path runs preflight first, so the
update is idempotent on accepted startup state. A second managed ZoneEngine is
blocked by exact process/listener ownership before launch. Direct unmanaged
ZoneEngine execution remains outside that wrapper guarantee; changing the
historical reset without runtime evidence would change supported behavior, so
no runtime change was made.

## Startup and shutdown safety

Startup checks each selected engine with ownership-safe prestart validation,
records the exact launched PID, executable, start time, arguments, ports, and
logs, verifies the launched PID owns its required listeners, and rolls back only
processes launched by that invocation. Shutdown trusts PID metadata only when
engine identity, repository executable path, and start time match, then verifies
listener release. Neither path terminates by process name.

## Validation results

- Database preflight self-tests and wrapper: PASS
- Engine-status deterministic cases: 22/22 PASS
- Engine-management contracts: PASS
- Secret scan: PASS
- Live read-only database preflight: PASS; 34 required tables verified and
  online-character count zero
- Full acceptance and exact-commit gate results: recorded after final commit
  verification below

## Engine actions and final state

Before build, the current engine PID metadata, executable paths, start times,
listeners, and logs were preserved. Engines are stopped and restarted only with
the approved PID-scoped wrappers. Final post-restart PIDs, listener ownership,
preflight, log inspection, acceptance totals, and exact-commit gate identities
are appended after validation.

## Remaining risk

The historical ZoneEngine broad online reset is safe under the approved
preflight and duplicate-start guard, but an operator can bypass those guarantees
by executing the engine binary directly. No evidence in this repair justifies a
runtime semantic change. Optional WebEngine remains deliberately inactive.
