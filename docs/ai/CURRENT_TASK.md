# Current Task

## Active

Complete and publish the generated-combat concurrency reconciliation without
changing supported runtime behavior. The capture corpus and current runtime are
authoritative; inventory, catalog, fixtures, active coverage, formula data, and
their generation manifest are one coordinated generated cohort.

## Reconciliation scope

- Parse each primary capture once into an immutable, validated shard and reuse
  that shard for metadata indexing and packet correlation.
- Run the primary, decoder, SCFU analyzer, active-coverage generator, and formula
  generator from immutable input bytes. The frozen Windows analyzer is copied
  byte-for-byte to a short owned temporary path before process launch.
- Serialize writers and protect supported readers with the generated-artifact
  lease. Publish the complete five-artifact cohort transactionally, with the
  manifest last, rollback before commit, and validated crash recovery.
- Reject stale, mixed, partial, location-dependent, or input-mutated candidates
  with the exact phase and changed-input diagnostic.
- Keep direct generator writes away from governed outputs and route supported
  build, AOtomation, and acceptance readers through a shared read lease.

## Current deterministic cohort

- Generation identity:
  `e1c4dc9b66ca46c2d4d4913243511502df7b29f33368b3ca7c9f67599147f0ab`
- Combined input identity:
  `053c6b3b9efee2a8854c189abaa499ef84a1c579622495431a52ab1b63e02d82`
- 381 capture sessions, 365 canonical sessions, 3,269 complete attack chains,
  260 certified profiles, 96 runtime-ready profiles, 309 semantic definitions,
  101 runtime-ready definitions, and 1,486 unresolved profiles.
- 1,607 fixed actors: 504 accepted and 1,103 quarantined. Formula coverage has
  422 profiles and 67 active bindings. Generator errors are zero.

## Final delivery acceptance

- The pre-repair clean-worktree stress matrix passed. Later gates exposed
  intermittent Python failures in the formula ItemDb loader, the active-coverage
  initializer hot loop, capture-shard publication, and JSON decoding. The
  bounded repairs now need the same complete stress matrix on the final commit.
- The repaired loader retains 42 referenced templates instead of 120,842 and
  reduced measured peak Python allocation from 422,936,105 to 11,169,393 bytes
  while keeping the formula dataset byte-identical.
- Active initializer comments are parsed without the repeated regex hot loop;
  capture shards publish atomically with read-back validation and bounded
  materialization retry; isolated children retry only recognized native or
  interpreter-internal failures. Large governed JSON inputs use the standard
  library's pure-Python scanner after repeated C-scanner corruption.
- The final focused transaction and pipeline suite is **67/67 PASS**. The final
  coordinated `--write` and real-corpus `--check` both pass with three
  fixed-point rounds and byte-identical runtime-facing artifacts.
- The standalone secret scan and Debug build passed without starting an engine.
- The final handoff must report five consecutive complete mandatory integration
  gates from one unchanged final commit. Those results are not written back to
  tracked files so recording them cannot dirty the commit that they validate.
- Remove only task-owned temporary evidence, push `master`, and report the
  final commit and gate results.

## Authoritative evidence

- `docs/project/GENERATED_COMBAT_PIPELINE.md`
- `docs/evidence/GENERATED_COMBAT_CONCURRENCY_20260802.md`
- `docs/generated/capture_backed_npc_combat_generation_manifest.json`
