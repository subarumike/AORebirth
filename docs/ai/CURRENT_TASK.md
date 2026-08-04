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
  `6b2d5cc4e45397ba034d05d619ef5bbee6ba1023ae17d40616ae0aca55e0b8ae`
- Combined input identity:
  `ca1c9a76d58eada2a2c7ddf26aeec52583905a1c32da5c23d42a1ccce1d18974`
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
  standard library's pure-Python scanner. Active and formula children receive
  separate exact private projections with independent fsynced, exact-readback,
  SHA/length-verified bytes; the projection keeps complete
  `attackInfoPacketIds` evidence arrays rather than reducing them to samples or
  counts.
- Cohort validation binds each JSON parse to the manifest SHA/length, parses
  each governed artifact once, and retries `JSONDecodeError` up to three times
  against the identical verified UTF-8 string. The same bound applies to
  impossible stdlib JSON `TypeError` or `AttributeError` failures only when the
  traceback proves `json.decoder`/`json.scanner` ownership; deterministic and
  unrelated failures remain fail-closed without retry.
- Mandatory, build, test, and generated-combat wrappers select the installed
  CPython 3.13.14 runtime through one shared overrideable selector. This excludes
  the local Python 3.12 installation that repeatedly failed in `python312.dll`
  with native `0xc0000005` access violations before stress phases began.
- The 2,466,207-byte ItemDb is bound to its frozen auxiliary snapshot SHA/length.
  The repository's C# `MessagePackZip` reader extracts exactly the 42 PF127/PF1931
  templates into a private JSON projection. Formula children verify that small
  projection's SHA/length and no longer parse the full ItemDb in Python. The
  governed formula values are unchanged; its generated diff records only the
  rebuilt analyzer provenance SHA.
- After a completed transition, byte-identical formula output proves that the
  next pair transition is the current pair itself. That proven identity
  transition is memoized for the convergence round, so both redundant children
  are skipped without changing the fixed-point round count or output bytes.
- The portable input descriptor is schema 2. It commits durable capture source,
  plan, capture identity, and session state while excluding private shard
  path/hash/length fields that are validated before normalization and are not
  publication inputs.
- The final focused transaction and pipeline suite is **71/71 PASS**. The final
  coordinated `--write` and `--validate-current` pass with three fixed-point
  rounds. The generated runtime catalog and fixtures remain byte-identical;
  inventory, active coverage, and formula diffs are limited to reconciled
  producer/input descriptors and analyzer provenance. The final clean-commit
  stress matrix supplies the real-corpus reproducibility checks.
- The standalone secret scan and Debug build passed without starting an engine.
- The complete Arete acceptance wrapper reports **60/60 PASS** against the final
  reconciled cohort. Delivery still requires the scoped commit/push and engine
  restart.
- One read lease now covers each complete mandatory gate, so its many filtered
  AOtomation/build invocations validate delegation instead of reparsing the
  124 MB inventory. The approved gate supervisor has an explicit four-hour
  bound; generated children retain their existing 30-minute bound.
- Remove only task-owned temporary evidence, push `master`, and report the
  final commit and gate results.

## Authoritative evidence

- `docs/project/GENERATED_COMBAT_PIPELINE.md`
- `docs/evidence/GENERATED_COMBAT_CONCURRENCY_20260802.md`
- `docs/generated/capture_backed_npc_combat_generation_manifest.json`
