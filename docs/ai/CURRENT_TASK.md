# Current Task

## Active

TASK ID: WEBCORE-BOOTSTRAP-SECURITY-20260802

The optional WebEngine asset bootstrap reconciliation is complete without
changing HTTP serving or PHP execution behavior. The historical mutable
`master.zip` download is replaced by an offline-only operator import pinned to
CellAO WebCore commit `765c3850767b63af1cd259bab7f2f7ca3e97adf9`.
Startup requires the checked-in asset manifest and the imported local `htdocs`
tree to validate before ownership/port checks and launch.

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
- Deterministic WebCore asset validation: 36/36 PASS; checked-in manifest
  authority parsing: PASS; PHP runtime validation:
  7/7 PASS; engine-management ownership contracts: 22/22 PASS.
- Mandatory integration gate: 12/12 PASS twice from the unchanged final
  commit/tree without credentials, live database access, PHP, a network
  dependency, or real engine startup.

## Current blockers and debt

- The previously exposed local database credential must be rotated externally.
  Repository configuration contains placeholders only; Codex did not and cannot
  claim external rotation without authorized access and verification.
- No valid local MySQL credential is installed, so live database and engine
  startup verification remains blocked. The environment override must be
  installed externally before running `preflight-database.cmd`.
- WebEngine remains optional and not production-safe. No maintained PHP runtime
  has been proven against its obsolete PHP/MySQL/mcrypt/config assumptions, and
  the pinned upstream WebCore snapshot has no upstream license file. The
  offline import establishes provenance and integrity, not production approval.
- `_tmp_mail_recovery` remains retained because its unique recovery value has
  not been disproved. It is not an accepted runtime or generator dependency.
- Unsupported gameplay systems and evidence gaps remain fail-closed; see the
  concise project state and subsystem completion matrices.

## Authoritative evidence

- `docs/project/WEBCORE_ASSET_SUPPLY.md`
- `docs/evidence/WEBCORE_BOOTSTRAP_SECURITY_20260802.md`
- `docs/evidence/ENGINE_MANAGEMENT_SECURITY_20260802.md`
- `docs/evidence/BASELINE_CLEANUP_20260801.md`
- `docs/evidence/ARETE_FULL_CORPUS_COMPLETION_20260731.md`
- `docs/evidence/SUBWAY_FULL_CORPUS_COMPLETION_20260731.md`
- `docs/evidence/TEMPLE_FULL_CORPUS_COMPLETION_20260801.md`
