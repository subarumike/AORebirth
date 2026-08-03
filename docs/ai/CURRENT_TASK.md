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
  `3e99136015fb50d47fa9e63732050e09343ecf172b3b6127cd9badd48877b760`
- Combined input identity:
  `e2a9e28803e638343bb98ac80c519c596bb0695686b7dc03761153a6cf5ff08f`
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
  interpreter-internal failures. Large governed JSON inputs initialize only the
  standard library's pure-Python scanner, and every active/formula child parses
  its own fsynced, exact-readback, SHA/length-verified projection copy.
- Cohort validation binds each JSON parse to the manifest SHA/length, parses
  each governed artifact once, and retries only `JSONDecodeError` up to three
  times against the identical verified UTF-8 string. Other failures remain
  fail-closed without retry.
- The 2,466,207-byte ItemDb is bound to its frozen auxiliary snapshot SHA/length,
  retained as verified parent bytes, and rematerialized independently for every
  formula round. Integrity mismatches fail closed; only decode failures after a
  matching descriptor receive bounded fresh-process retry. Unrequested nested
  MessagePack values are skipped iteratively so deep containers cannot exhaust
  the Python call stack; fatal diagnostics retain both header and terminal frames.
- The portable input descriptor is schema 2. It commits durable capture source,
  plan, capture identity, and session state while excluding private shard
  path/hash/length fields that are validated before normalization and are not
  publication inputs.
- The final focused transaction and pipeline suite is **70/70 PASS**. The final
  coordinated `--write` and `--validate-current` pass with three fixed-point
  rounds and byte-identical runtime-facing artifacts. The final clean-commit
  stress matrix supplies the real-corpus reproducibility checks.
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
