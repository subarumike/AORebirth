# Current Task

## Active

TASK ID: POST-BASELINE-HANDOFF-20260801

The repository-wide baseline reconciliation is complete. No additional gameplay
or feature implementation is authorized by this task. The next gameplay change
must be selected explicitly and grounded in the complete available capture
corpus; incremental PF127 Subway work remains the repository priority when new
work is requested.

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

## Current blockers and debt

- The previously exposed local database credential must be rotated externally.
  Repository configuration contains placeholders only; Codex did not and cannot
  claim external rotation without authorized access and verification.
- WebEngine's optional bootstrap still downloads obsolete PHP 5.5.10 and a
  `php.ini` file over HTTP. It remains disabled from normal engine startup and
  requires a separately approved replacement/disposition.
- `_tmp_mail_recovery` remains retained because its unique recovery value has
  not been disproved. It is not an accepted runtime or generator dependency.
- Unsupported gameplay systems and evidence gaps remain fail-closed; see the
  concise project state and subsystem completion matrices.

## Authoritative evidence

- `docs/evidence/BASELINE_CLEANUP_20260801.md`
- `docs/evidence/ARETE_FULL_CORPUS_COMPLETION_20260731.md`
- `docs/evidence/SUBWAY_FULL_CORPUS_COMPLETION_20260731.md`
- `docs/evidence/TEMPLE_FULL_CORPUS_COMPLETION_20260801.md`
