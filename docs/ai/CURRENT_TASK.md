# Current Task

## Active

TASK ID: ENGINE-MANAGEMENT-SECURITY-20260802

The post-baseline engine-management security repair is complete. No gameplay,
packet, generated artifact, or database-schema behavior changed. Engine status
now proves PID-to-port ownership, startup is guarded by a read-only database
preflight, managed rollback/shutdown is PID-scoped, and WebEngine requires an
explicit local PHP runtime without downloading one.

Current acceptance authority:

- Complete AOtomation suite: 1037/1037 PASS.
- Arete acceptance: 60/60 PASS.
- Combat inventory: 381 sessions, 365 canonical sessions, 260 certified
  profiles, 96 runtime-ready profiles, 309 semantic definitions, 101
  runtime-ready definitions, 1,486 unresolved observations, zero generator
  errors.
- Active coverage: 1,607 actors, 504 certified, 1,103 explicitly unresolved.
- Subway, Temple, mission graph, generated mission, Git LFS, Git object
  integrity, secret scan, and debug build gates: PASS.
- Deterministic engine-management contracts: PASS without credentials, live
  database access, PHP, network downloads, or real engine startup.

## Current blockers and debt

- The previously exposed local database credential must be rotated externally.
  Repository configuration contains placeholders only; Codex did not and cannot
  claim external rotation without authorized access and verification.
- No valid local MySQL credential is installed, so live database and engine
  startup verification remains blocked. The environment override must be
  installed externally before running `preflight-database.cmd`.
- WebEngine remains optional and not production-safe. Its network downloader is
  removed, but no supported local PHP version has been proven against the
  historical web assets.
- `_tmp_mail_recovery` remains retained because its unique recovery value has
  not been disproved. It is not an accepted runtime or generator dependency.
- Unsupported gameplay systems and evidence gaps remain fail-closed; see the
  concise project state and subsystem completion matrices.

## Authoritative evidence

- `docs/evidence/ENGINE_MANAGEMENT_SECURITY_20260802.md`
- `docs/evidence/BASELINE_CLEANUP_20260801.md`
- `docs/evidence/ARETE_FULL_CORPUS_COMPLETION_20260731.md`
- `docs/evidence/SUBWAY_FULL_CORPUS_COMPLETION_20260731.md`
- `docs/evidence/TEMPLE_FULL_CORPUS_COMPLETION_20260801.md`
