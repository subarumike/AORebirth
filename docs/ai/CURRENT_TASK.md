# Current Task

## Active

Reconcile the preserved database/server worktree repair without changing the
completed Arete runtime or generated cohort.

## Accepted scope

- Keep the secure `AO_REBIRTH_MYSQL_CONNECTION` process-environment override;
  tracked configuration must contain placeholders only.
- Keep database preflight fail-closed for missing credentials, connection,
  database identity, schema/read access, and nonzero `characters.Online` state.
- Repair managed start/stop status-probe calls so every invocation supplies the
  repository configuration and engine directory required by the probe.
- Keep PowerShell boolean continuation syntax parseable in the PID-metadata
  trust check.
- Treat capture-decoder internal `TypeError`/`AttributeError` tracebacks as the
  same bounded transient interpreter corruption already retried inside the
  frozen analyzer; deterministic schema failures remain non-retryable.
- Preserve exact PID, executable, start-time, and listener ownership; never use
  process-name fallback termination.

## Rejected preserved changes

The tracked private connection string, configuration fallback, secret-scanner
exemption, weakened missing-credential tests, and documentation describing that
model were rejected and restored to the accepted secure baseline.

## Delivery acceptance

- Focused database-preflight, engine-management, and secret-scan tests pass.
- Complete mandatory integration gate passes twice from the unchanged final
  commit and leaves the worktree clean.
- Debug build passes, database preflight passes, Chat/Login/Zone are restarted
  through approved wrappers with exact port ownership, and optional WebEngine
  remains inactive.
- Audit evidence: `docs/evidence/DATABASE_SERVER_REPAIR_20260804.md`.
