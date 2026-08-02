# AORebirth Baseline Reconciliation and Cleanup Evidence — 2026-08-01

## Purpose and scope

This record tracks the repository-wide baseline repair requested on 2026-08-01. The work is limited to AORebirth and covers credential hygiene, complete test-suite repair, generated combat-coverage reconciliation, safe storage cleanup, dependency advisories, workflow/status documentation, and final reproducibility gates. Existing unrelated work is preserved until its provenance is proven.

## Starting repository state

- Repository: `C:\Users\Mike\Documents\AORebirth`
- Branch: `master`
- Starting commit: `4c887a74573497086d6ec419459694a49352dfb7`
- Remote relationship: `master...origin/master`, ahead 0, behind 0
- Git LFS integrity: PASS
- Git object inventory: 5,180 loose objects (16.78 GiB), 10 packed objects (112.76 MiB), 61 garbage entries (56.45 GiB)
- Additional worktrees: three detached, clean baselines; every worktree commit is an ancestor of `master`

### Starting worktree changes

- Modified `AORebirth/Config/Config.xml`: local database credential; credential value intentionally omitted from all evidence and output.
- Modified `AORebirth.Tests/.../TempleOfThreeWindsOrdinaryContentTests.cs`: line-ending-only worktree difference.
- Untracked `AORebirth/Server/ZoneEngine/Content/Captured/Arete/movement/`: exact duplicate of the tracked generated Arete movement artifact.
- Untracked `Mission_Tables_Level_Restrictions_Teaming_Levels.ods`: documented mission-source artifact.
- Untracked `additional captures notprocessed.odt`: capture handoff inventory; all named captures are already represented, while the unnamed Slipknot of the Plains capture remains unresolved.
- Untracked `diagnostics/`: 74,049,151,688 bytes of diagnostic output, primarily crash dumps.
- Untracked `tools-temp/ProcDump/`: 4,121,565 bytes.

## Initial acceptance and generator evidence

- Complete AOtomation suite, run twice at the starting commit: **1005/1037 PASS**, with 32 failures.
- Persistent failures: 28 across damage calculation, generated combat fixtures, quest persistence, visibility, generic-command ownership routing, and lifecycle guardrails.
- Order-dependent failures: 4 across Arete movement, Windcaller Karrec content, and dungeon navigation.
- Arete acceptance: **60/60 PASS**.
- Arete combat: **57/57 PASS**.
- Arete active coverage: **8/8 PASS**.
- Arete loot: **14/14 PASS**.
- Subway acceptance: PASS.
- Temple acceptance: PASS.
- Mission graph reproducibility: PASS.
- Active coverage check: PASS (1,607 actors; 504 certified; 1,103 unresolved).
- Formula check: PASS (422 profiles; 67 active bindings).
- Full combat inventory check: FAIL because the checked-in inventory is stale.

### Combat inventory delta proved by isolated regeneration

The checked-in inventory contains 375 sessions; isolated regeneration from the current capture corpus contains 381. The six missing sessions are:

- `20260722-104809`
- `20260722-152454`
- `20260728-233312`
- `20260729-000735`
- `20260731-030702`
- `20260731-035230`

Regeneration changes canonical sessions from 359 to 365, certified profiles from 255 to 260, runtime-ready profiles from 95 to 96, certified definitions from 303 to 309, generated definitions from 273 to 279, runtime-ready definitions from 100 to 101, attack chains from 2,827 to 3,269, packets from 79,187 to 92,147, and unresolved observations from 1,404 to 1,486. These changes require source-evidence review before promotion.

## Storage evidence and initial classification

- `diagnostics/`: disposable generated diagnostics after manifest capture; approximately 74.05 GB.
- `.git/objects`: approximately 78.76 GB, including approximately 56.45 GB of reported garbage; cleanup must use Git-native maintenance after preserving reachable objects.
- Three clean detached baseline worktrees: approximately 1.31 GB total; removable because their commits are reachable from `master`.
- Duplicate untracked Arete movement tree: removable after hash manifest; byte-identical tracked source is `docs/generated/arete_20260722_152454_movement/runtime/patrol.csv`, SHA-256 `5ad51bde1210a146b85da336fe198dd62c9e50bffc7137c43fda5ce343d59d79`.
- `tools-temp/ProcDump/`: redistributable tool payload; removal requires confirming no repository workflow depends on the local copy.
- Mission ODS and capture-handoff ODT: preserve until each has a durable tracked provenance record.

### Capture-handoff ODT preservation

The ODT's unique operator notes are preserved here before removal of the loose office document:

- Talonshred the Dark Flesh-tearer, level 158: 30-minute corpse despawn with loot, 10-minute respawn; capture `20260718-003251`.
- Slipknot of the Plains, level 150: 30-minute corpse despawn with loot, 10-minute respawn; capture remains unknown and the behavior remains unresolved.
- Voron the Web Lord, level 145: 30-minute corpse despawn with loot, 10-minute respawn; capture `20260718-033000`.
- Blind Cultist exchanges Exarch robes for sealed Inner Sanctum passes and is not treated as an ordinary quest NPC; capture `20260722-041602`.
- Steps of Madness transport NPC; capture `20260722-051643`.
- Partial Steps of Madness evidence; capture `20260722-051737`.

The named capture folders above already exist in the repository corpus. No runtime behavior is promoted from the unresolved Slipknot note.

## Security boundary

The locally modified connection string must never be printed. The tracked starting commit contains a placeholder, and current evidence does not show the local credential committed in Git history. Repository remediation will provide an ignored or environment-based local override, restore the tracked placeholder, and add a repeatable secret scan. Revocation or rotation of any credential already exposed outside the repository is an external operator action and will not be reported as completed unless independently confirmed.

## Repository and tool cleanup performed

- Removed the 449-file disposable storage set recorded in `baseline_cleanup_storage_manifest_20260801.csv`: 74,054,821,216 bytes from diagnostics, ProcDump, the duplicate Arete patrol tree, and the retired capture-handoff ODT.
- Removed 1,877 tracked temporary probes, duplicate analyzer copies, decompilations, and loose third-party binaries: 19,926,680 bytes. Every removed path, size, and SHA-256 is recorded in `baseline_cleanup_tool_removals_20260801.csv`.
- Retained the authoritative capture analyzers, approved live/mission tooling, capture corpus, durable evidence, database recovery material, and `_tmp_mail_recovery` pending a separate unique-work review. The classification of every retained top-level group is in `baseline_cleanup_tool_inventory_20260801.csv`.
- Removed the 49-file `AORebirth/Cursor` handoff snapshot after confirming it is not referenced by a solution, project, build, generator, test, or runtime path. The folder describes itself as a copy/export handoff, current production contains the accepted pet functionality, and the complete AOtomation suite passes. Per-file production comparisons and SHA-256 values are recorded in `baseline_cleanup_cursor_inventory_20260801.csv`.
- Removed the three clean detached worktrees only after rechecking that every tip is an ancestor of `master`.
- A full Git object check found five corrupt loose objects. Four were unreachable. The fifth was reachable only from local `refs/codex/turn-diffs` snapshots of the removed diagnostics tree; all affected turn-diff refs were manifested before removal. The exact corrupt objects were quarantined, all remaining refs passed full verification, and only then were the corrupt quarantine copies discarded.
- Git-native garbage collection reduced the object store from 5,180 loose objects plus 56.45 GiB of garbage to one 128.83 MiB pack, zero loose objects, and zero reported garbage. The post-cleanup `git fsck --full --no-dangling` result is PASS.

## Dependency advisory disposition

- Removed DotNetZip 1.9.2 from all projects. WebEngine archive extraction now uses the framework ZIP implementation with an explicit canonical-path containment check. Zlib-only callers now use the separate `Ionic.Zlib` package, which does not expose ZIP extraction.
- Upgraded Npgsql from 2.0.14.3 to 4.0.14, the first patched release in the compatible 4.0 line, without database schema changes.
- Debug server build after both dependency changes: PASS.
- WebEngine still contains an obsolete HTTP bootstrap for PHP 5.5.10 and `php.ini`. It is not changed without an authoritative replacement source and remains explicit security/operational debt.

## Approved workflow commands

All operational commands for this task use the repository-approved Windows CMD wrappers documented in `docs/ai/WORKFLOW.md`. The AO client will not be launched. Build, validation, engine stop/restart, generated-artifact checks, and final acceptance will use their maintained wrappers rather than ad-hoc replacements.

## Completion ledger

This section is updated as each phase is completed.

| Phase | Status | Evidence |
|---|---|---|
| Credential remediation and secret scan | Complete | Tracked placeholder restored; environment override and value-redacting scan added. External credential rotation remains required. |
| Complete suite repair | Complete | 1037/1037 PASS once; two unchanged-final-tree gate runs remain. |
| Combat inventory reconciliation | Complete | Inventory and all dependent artifacts regenerated; deterministic checks pass. |
| Line-ending normalization | Pending | One line-ending-only phantom change and missing editor policy identified. |
| Storage and worktree cleanup | Complete | 449 disposable files removed; three reachable clean worktrees removed; Git object store verified and reduced to 128.83 MiB with zero garbage. |
| Tool/Cursor cleanup | Complete | 1,877 manifested temporary files and the 49-file unowned Cursor export removed; maintained tools/evidence retained. |
| Dependency advisory repair | Complete | DotNetZip removed, archive traversal rejected, Zlib isolated, Npgsql upgraded to 4.0.14; build PASS. |
| Documentation/status/gate repair | Pending | Engine status wrapper is missing; task/state documents contain stale status. |
| Final gate, two unchanged runs | Pending | Must reach 1037/1037 with all generated checks internally consistent. |
