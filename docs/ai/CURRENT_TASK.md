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
  `91cbc7ef749c6f1a66f1d527d227105c30130ba0e961099996e62b36f1059a37`
- Combined input identity:
  `fd60ad21be455b7c91a0f03dfecbe9fb756c3a5cd093e9bc1a9827581be835ec`
- 381 capture sessions, 365 canonical sessions, 3,269 complete attack chains,
  260 certified profiles, 96 runtime-ready profiles, 309 semantic definitions,
  101 runtime-ready definitions, and 1,486 unresolved profiles.
- 1,607 fixed actors: 504 accepted and 1,103 quarantined. Formula coverage has
  422 profiles and 67 active bindings. Generator errors are zero.

## Final delivery acceptance

- The pre-repair clean-worktree stress matrix passed. A later gate exposed an
  intermittent formula ItemDb allocation crash; the streaming/selective loader
  repair now needs the same complete stress matrix on the final commit.
- The repaired loader retains 42 referenced templates instead of 120,842 and
  reduced measured peak Python allocation from 422,936,105 to 11,169,393 bytes
  while keeping the formula dataset byte-identical.
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
