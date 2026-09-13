# Delmus NPC content integration

## Exact integration state

```text
STARTING_SHA=2dc701f1e114cdd26dbb1e13d89acd4aa3b77a76
ORIGIN_MASTER_SHA=7be49b22b4c0116af9c48dc88a44f86ba802f7c7
BRANCH=codex/delmus-npc-content-001
WORKTREE=C:\Users\Mike\Documents\AORebirth\build-verify\delmus-npc-content-001
DELMUS_REF=origin/New-ZoneEngine
DELMUS_SHA=8b9d36090986dc3ad89b9545a23ff38016c626ab
PRODUCTION_MODIFIED=NO
DELMUS_BRANCH_MODIFIED=NO
MASTER_MODIFIED_DIRECTLY=NO
```

This is a selective candidate based on the accepted arrival-facing source and receipt. No master merge, developer branch edit, production operation, schema change or client operation is part of this task.

## Imports and conflicts

`DELMUS_NPC_IMPORT_MATRIX.json` classifies all 110 changed developer files into content, generic mechanics, tests, generated data, conflicting and unrelated work. Three stat-composition source files were imported with the arbitrary missing-family fallback removed. Four content changes were reconciled; other changes were skipped or locally adapted only at the scoped consumer boundary.

The 34 developer templates and sample family/overlay inputs are retained verbatim under `docs/accepted/npc/delmus` as **reviewed source snapshots, not combat acceptance**. The current Antonio template is preserved. Thirty-two Subway templates and the unresolved placeholder are loaded as blocked candidate data in `GameData/MobTemplates.json`. Source weapons, appearances, hashes, sparse overrides and missing template IDs are fully enumerated in the reconciliation JSON artifacts. No per-NPC C# content was added.

`GameDataStore` composes accepted family base -> optional overlay -> individual override into a fresh dictionary. Missing family/overlay data is an error, never a default-stat substitution. Runtime family/overlay files remain empty until authoritative definitions can be promoted; the supplied sample families do not cover the Subway IDs. The existing exact-level guard remains in force.

## Population and AAAA policy

`AAAA_POLICY=BLOCK_SPAWN`. The baseline already contained an AAAA appearance template, but the current placement guard prevented the developer's fallback behavior. The placeholder is now explicitly unresolved, non-attackable, without combat AI or concurrent weapons, and rejected by `SpawnService` before identity allocation. It cannot become an authorized actor just because its hash is present in a template file.

The official placement guard is retained. All 326 official PF127 placements remain without the complete accepted NewEngine identity/behavior/template bridge. A developer hash match is recorded as a candidate, not proof of an exact official identity or captured actor binding. Existing accepted capture population remains intact: the approved export verified 322 Subway and 167 Temple bindings.

Pink diagnosis: one reviewed AAAA placeholder definition; no visible placeholders activated. Missing visual assets and other pink causes are **UNPROVEN**, not assumed zero. A blocked spawn cannot itself render pink. No official-client appearance test was performed.

## Content acceptance

- 32 Subway templates imported, including Eumenides, Vergil Aeneid and Strike Foreman.
- Family IDs: 0, 3, 63, 138, 148, 149, 150, 151. Eight missing accepted family definitions; no fabricated curves.
- 53 source weapon definitions: 25 match captured endpoint pairs; 28 are unresolved. Zero exact duplicate endpoint definitions. Quality and owner associations are preserved as alternatives, with ambiguous/missing quality selection rejected.
- Zero newly combat-accepted NPCs. Existing captured combat readiness does not by itself supply the missing bridge to these official placements.

See `SUBWAY_NPC_COMBAT_RECONCILIATION.md` and its JSON companion for the per-NPC matrix. `NPC_HASH_RESOLUTION_MATRIX.json`, `NPC_FAMILY_STAT_COVERAGE.json`, and `NPC_WEAPON_RECONCILIATION.json` retain every placement, family and weapon disposition.

## Cast interruption

The current service missed incoming action-108 requests and movement-start cancellation. Those server paths are repaired while preserving DAO ownership and lifecycle. A typed notifier is tested against actual retail packet bodies, but its runtime reason policy remains unset because the available packets do not prove a local-player interruption-code mapping. See `CAST_INTERRUPTION_RECONCILIATION.md`. Client-visible interruption is not claimed fixed.

## Validation

Validation receipts will record the final tested source SHA and exact results. Initial focused NPC/stat/weapon/content tests and nano-service tests pass. The standard Legacy build and accepted binding export pass. An initial full run exhausted disk during duplicate playfield copying; only identical task-owned copies were replaced with hard links to the verified package. No user capture or source data was deleted. A cancellation regression found by the morph suite was corrected by retaining silent disconnect/lifecycle cancellation.

Final exact-source Windows, Linux publication, mandatory, messaging, DAO and connected gates are pending. This candidate is not approved for deployment.

## Reproduction

Use the installed Node runtime to run `Tools/reconcile_delmus_npc_content.cjs --write` or `--check`. It reads pinned reviewed snapshots, accepted repository evidence and exact Git source identities; it does not require raw capture folders, access a database, mutate the developer branch or activate content. Canonical source/evidence SHA-256 values and the deterministic import matrix are recorded in the generated artifacts.
