# AORebirth Project State

Updated: 2026-08-02

This file is the concise current source of truth. The pre-cleanup long-form
state is preserved at
`docs/archive/project/PROJECT_STATE_PRE_BASELINE_CLEANUP_20260801.md`; subsystem
completion matrices and dated evidence retain detailed provenance.

## Acceptance baseline

- Complete AOtomation suite: 1037/1037 PASS with no skipped, hidden, or
  reclassified failures.
- Arete acceptance: 60/60 PASS.
- Arete combat catalog: 57/57 PASS.
- Arete active coverage: 8/8 PASS.
- Arete loot foundation: 14/14 PASS.
- PF127 Subway acceptance: PASS.
- PF1931 Temple acceptance: PASS.
- Generated mission graph and mission reproducibility: PASS.
- Debug server build: PASS.
- Git LFS and Git object integrity: PASS.
- WebEngine offline PHP/WebCore boundary: PASS. The official PHP 8.5.9 x64 NTS
  VS17 runtime and hardened INI are exact-manifest validated; the complete
  7,140-file WebCore corpus and all 25 PHP files are audited, deterministically
  patched, final-manifest validated, and PHP 8.5.9 lint clean.

## Generated combat authority

The capture corpus and production runtime are authoritative; checked-in
generated projections must reproduce from them and are never edited by hand.
The current deterministic inventory contains 381 sessions, 365 canonical
sessions, 3,269 complete attack chains, 260 certified profiles, 96 runtime-ready
profiles, 309 semantic definitions, 101 runtime-ready definitions, and 1,486
explicitly unresolved observations with zero generator errors.

The active-coverage projection contains 1,607 fixed actors: 504 certified and
1,103 explicitly unresolved. Unsupported or conflicting observations remain
fail-closed. The 2026-08-01 reconciliation promoted only Cedric Harding's exact
PF6553 source-bound profile; no unsupported runtime behavior was added.

## Supported playfields

- Arete is complete for behavior supported by the complete repository and
  capture corpus. Unknown probabilities, unseen branches, and unmeasured values
  remain explicit evidence gaps.
- PF127 Subway is complete for its current capture-backed population,
  navigation, combat, lifecycle, loot, vendor, Karrec, zoning, and teardown
  contracts. New behavior still requires capture evidence.
- PF1931 Temple is complete for its current ordinary/named population, dynamic
  doors, combat, lifecycle, loot, and navigation contracts. Unsupported nano
  selectors and unseen loot outcomes remain fail-closed.

## Repository health

- Tracked configuration contains placeholders only and supports the ignored
  `AO_REBIRTH_MYSQL_CONNECTION` local environment override.
- The read-only database preflight uses the production configuration/connector
  path, verifies the exact database and 34-table contract, and blocks startup
  when any character is still marked online.
- Engine health is PID-owned: listener PIDs must resolve to the exact expected
  executables. Managed startup and shutdown never kill by process name alone.
- The mandatory secret scanner rejects likely credentials without echoing
  values. Any credential exposed outside the repository still requires external
  rotation.
- WebEngine no longer downloads PHP, `php.ini`, or WebCore assets. It requires
  the complete official PHP 8.5.9 x64 NTS VS17 archive tree, the exact hardened
  INI, and an offline-imported final `htdocs` tree for CellAO WebCore commit
  `765c3850767b63af1cd259bab7f2f7ca3e97adf9`. PHP and WebCore are held under
  exclusive process-lifetime leases and revalidated before listener creation.
- The pinned WebCore archive is identified by SHA-256
  `ef297e623040b375e64c543568ca94e44ed7cc59de6fe826ed5e42db95c020ab`;
  its manifest covers 7,140 files and 26,648,501 bytes and has SHA-256
  `85c1515d274c2e4051013e89ca6d2a355365d5d01df7d621cc060dfa84e38463`.
- DotNetZip was removed. Archive extraction uses canonical-path containment;
  Zlib-only runtime paths use the isolated Ionic.Zlib package. Npgsql is 4.0.14.
- Three obsolete detached worktrees, the unowned Cursor export, 1,877 tracked
  temporary/decompiled files, and 74,054,821,216 bytes of disposable diagnostics
  and tools were removed after manifests and reachability checks.
- Git contains one 128.83 MiB pack, no loose objects, and no reported garbage
  after full integrity verification and native garbage collection.
- Line endings are explicit: maintained source/data use LF, Windows CMD/BAT
  launchers use CRLF, and binary formats are never normalized.

## Remaining debt boundary

- Rotate the previously exposed database credential externally.
- Perform authorized live WebEngine verification only after a valid disposable
  database credential is supplied. Current validation intentionally makes no
  live database connection and invents no credential.
- Replace or front the plaintext HTTP listener before considering secure-only
  cookies or production exposure. WebEngine remains development-only.
- Resolve the pinned WebCore snapshot's licensing before redistribution or
  production use. No license file was found upstream; integrity validation does
  not grant redistribution rights.
- Review `_tmp_mail_recovery` before any removal.
- Continue catalogued unsupported gameplay only with authoritative evidence;
  do not bulk-implement `NotImplementedException` paths or invent defaults for
  chase, quest deletion, action 59, anarchy playfields, perks, research, PvP,
  towers, teams, organizations, missions, quests, or pets.

## Operational workflow

- Run the complete local gate with `tools\run_mandatory_integration_gate.cmd`.
- Query live engine process/port health with `status-engines.cmd`.
- Validate database readiness with `preflight-database.cmd` before startup.
- Build with `tools\build_aorebirth_debug.cmd`.
- Stop/restart only with the approved root CMD wrappers.
- Supply WebCore assets only through the offline import and validation workflow
  in `docs/project/WEBCORE_ASSET_SUPPLY.md`; never restore a URL-backed archive
  bootstrap.
- Start optional WebEngine only with `start-web-engine.cmd`; it validates the
  database, binary, PHP runtime, and WebCore assets before launch. Stop it with
  `stop-web-engine.cmd`.
- Do not launch the AO client unless Mike explicitly requests it.
