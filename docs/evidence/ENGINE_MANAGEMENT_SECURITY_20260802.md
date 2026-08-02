# Engine Management Security Reconciliation

Date: 2026-08-02

## Scope and boundaries

This reconciliation changes only database preflight, engine lifecycle/status,
WebEngine runtime safety, deterministic tests, build/gate wiring, and workflow
documentation. It does not change gameplay, packets, generated artifacts,
capture-backed content, database schemas, migrations, or persisted data. No
valid database credential was available and no real engine was started.

## Authoritative engine ownership

Source/config listener initialization confirms:

| Engine | Canonical executable | Required ports | Default policy |
| --- | --- | --- | --- |
| ChatEngine | `AORebirth\Built\Debug\ChatEngine.exe` | `6996`, `7012` | Required |
| LoginEngine | `AORebirth\Built\Debug\LoginEngine.exe` | `7500` | Required |
| ZoneEngine | `AORebirth\Built\Debug\ZoneEngine.exe` | `7501` | Required |
| WebEngine | `AORebirth\Built\Debug\WebEngine.exe` | `8181` | Optional |

`status-engines.cmd` delegates to a read-only Windows Script Host probe. The
probe uses numeric CIM listener state, de-duplicates same-PID dual-stack rows,
and requires every listener PID to resolve to the exact expected executable
path. It fails on wrong or unresolved owners, conflicting listener PIDs,
duplicate expected processes, split ownership of ChatEngine's two ports, closed
required ports, or a partial optional-Web state.

## Database preflight

`preflight-database.cmd` requires `AO_REBIRTH_MYSQL_CONNECTION` in the current
process environment, validates format without printing values, confirms that
the production configuration loader selected the same override, and opens via
`AORebirth.Database.Connector`. It performs only read operations:

- confirms active database identity is exactly `cellao_codex_clean`;
- confirms the fixed 34-table startup schema contract and
  `characters.Online` column;
- executes a zero-row read probe against every required table;
- requires `SELECT COUNT(*) FROM characters WHERE Online <> 0` to return zero.

Exit codes are: `10` missing override, `11` invalid format, `12` network,
`13` authentication, `14` wrong database, `15` missing schema, `16` read
failure, `17` online characters present, and `18` internal contract failure.
Exception details and configuration values are never emitted.

## Startup and rollback

- `start-engines.cmd` runs preflight before invoking the normal Chat, Login,
  Zone order.
- `restart-engines.cmd` runs preflight before stopping anything, then uses the
  guarded start workflow.
- Startup requires a clear prestart state or an already healthy exact engine,
  verifies the PID launched by that invocation, and on failure stops only its
  own launched PIDs before verifying port release.
- Managed shutdown trusts PID metadata only when engine name, canonical path,
  PID, and process start time agree. It has no process-name kill fallback.

## WebEngine boundary

The PHP 5.5.10 ZIP and remote `php.ini` downloader were removed. `checkphp` is
now local validation only. WebEngine startup and PHP request handling both
canonicalize the configured path, reject URI/UNC/network or invalid executable
paths, and require a local `php-cgi.exe`. No PHP executable or archive is
tracked. Normal startup still excludes WebEngine; `start-web-engine.cmd` is the
explicit opt-in and `stop-web-engine.cmd` is its managed stop path.

The old downloader pin does not prove which PHP version the web assets support.
WebEngine therefore remains optional and not production-safe until a compatible
maintained local runtime is independently established.

## Deterministic validation

- PID/listener ownership evaluator: 22/22 PASS.
- Database preflight fake-source exit/read/redaction contracts: PASS.
- Missing-environment preflight/start/restart/Web-start guards: PASS with exit
  `10`; no engine lifecycle implementation was reached.
- PHP runtime validator: 7/7 PASS with temporary non-executable marker files;
  no PHP process or network request occurred.
- Engine-management source/sequencing contract: PASS.
- Secret scan: PASS.
- Complete mandatory integration gate: PASS twice on the unchanged final tree,
  including AOtomation 1037/1037, Arete 60/60, combat 57/57, active coverage
  8/8, loot 14/14, Subway, Temple, missions, generated artifacts, Git LFS,
  debug build, and clean worktree.

## Remaining blockers

- A valid MySQL credential must be installed externally before live preflight
  and engine verification.
- The previously exposed credential still requires external rotation; no
  rotation is claimed here.
- A compatible supported local PHP runtime has not been proven.
