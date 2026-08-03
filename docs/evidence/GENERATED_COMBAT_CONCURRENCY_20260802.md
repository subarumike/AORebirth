# Generated Combat Concurrency Reconciliation Evidence — 2026-08-02

## Scope and conclusion

This record covers the intermittent primary capture-aggregation failure, generated-combat input freezing, six-file cohort publication, and concurrency/recovery hardening. Evidence was gathered on 2026-08-02 and the post-review cohort below was published and revalidated on 2026-08-03.

The failure was a generator consistency defect, not a supported runtime behavior mismatch. The primary extractor parsed each live capture twice. Metadata indexing used objects from the first parse; packet correlation used objects from the second. If the observed live/projection generation changed between those passes, a packet's `MetadataGeneration.generation_key` was not present in the first-pass dictionary and the direct lookup raised `KeyError`.

The repair parses each capture once into an immutable validated shard and reuses that shard for metadata indexing and packet correlation. The broader cohort now uses frozen inputs, a shared reader/writer lease, a durable staged transaction, input validation at both publication boundaries, rollback, and recovery. The final portable input descriptor is schema 2: it is derived only after strict validation of the full internal snapshot, then hashes the durable source, plan, capture identity, and session-state projection without private shard path/hash/length fields. No supported runtime behavior was changed.

## Failure evidence boundaries

Two historical Python failures must not be conflated:

1. The exact intermittent key preserved by the task handoff is:

   ```text
   tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260529-212034|0x788DA39B|scfu=105263
   ```

   The key matches the extractor's `MetadataGeneration.generation_key` shape and the former direct dictionary lookup at the old `tools-temp/AOSharpCaptureAnalyzer/extract_capture_backed_npc_combat.py:3135`. No persistent traceback containing that exact `KeyError` was recovered from the repository or available temporary logs. The exact key is therefore handoff evidence, not an on-disk traceback claim.

2. `TestResults/db-startup-starting-gate.log:5-40` contains a different old failure: Python `SystemError` with `unknown opcode 204`. Its retry passed. That log does not prove, reproduce, or explain the missing-generation-key failure.

## Reproduction and controlled proof

Ordinary pre-fix runs did not reproduce the intermittent race:

| Run set | Result |
| --- | --- |
| Baseline generated-combat gate stage | PASS |
| Sequential primary check 1 | PASS |
| Sequential primary check 2 | PASS |
| Concurrent primary checks 1-4 | PASS |
| Total ordinary attempts | **7/7 PASS; intermittent failure not reproduced** |

The absence in seven attempts is not treated as proof that the historical failure was impossible. The old two-pass dataflow and direct dictionary lookup made the inconsistency structurally possible.

The primary self-test now injects the exact handoff generation key as a controlled missing-key fault. It passes only when the extractor fails through `CaptureAggregationInvariantError` and reports the missing key, owning packet, owning session, snapshot identity, and phase. This verifies the diagnostic and fail-closed path without inventing packet behavior or altering live capture evidence.

## Reconciled cohort

### Artifact hashes

The starting hashes were recorded before reconciliation. The final hashes are from `docs/generated/capture_backed_npc_combat_generation_manifest.json` after the post-review write.

| File | Starting SHA-256 | Final SHA-256 | Result |
| --- | --- | --- | --- |
| `docs/generated/capture_backed_npc_combat_inventory.json` | `e85ff2119427a740e1c629465895bf510e7ab019e857321dcfa5e1a0a0d3015e` | `8fa4476b7d99b139518d5a43ba654c3004807f715e55096b21de1f146fad0771` | Regenerated with the reconciled producer/input identity |
| `AORebirth/Server/ZoneEngine/Core/Playfields/CapturedEnemyCombatProfileCatalog.g.cs` | `553549e6296072653356bfa6b701a3ea7f09badd7cf12b17a22f20d63a890712` | `553549e6296072653356bfa6b701a3ea7f09badd7cf12b17a22f20d63a890712` | Byte-identical |
| `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging.Tests/CapturedEnemyCombatProfileCatalogFixtures.g.cs` | `26b3d5f69c8e976e78ada3b6562467aa093c9b01a51e144cc3beeb0493214793` | `26b3d5f69c8e976e78ada3b6562467aa093c9b01a51e144cc3beeb0493214793` | Byte-identical |
| `docs/generated/capture_backed_npc_combat_active_coverage.json` | `89b54335c7407d8cebdf3c4d6e07e2353fe2fcfc870a94e625d304dfcf328254` | `e8088b991e555fe9f46119550db9134f128c06ed27e4790e577fe1016b587078` | Only its inventory-source descriptor changed in the Git diff |
| `docs/generated/enemy_combat_setup_formula_dataset.json` | `ee121b35f7ccf2df2f6592389ae3674c94a04c772f95824fbd21250b10b71da0` | `ee121b35f7ccf2df2f6592389ae3674c94a04c772f95824fbd21250b10b71da0` | Byte-identical |
| `docs/generated/capture_backed_npc_combat_generation_manifest.json` | Not previously governed | `418b41ca45b75c4869b872e45c781980a599de061b75977ba33622d7f4e970b2` | Sixth-file commit marker after final generator hardening |

The catalog, fixtures, and formula data stayed byte-identical. The active-coverage Git diff changes only its recorded inventory SHA-256. No supported runtime C# source was changed for this reconciliation, and the generated runtime catalog stayed byte-identical. Those facts are the boundary for the conclusion that runtime semantics did not change.

### Final identities

| Identity | Value |
| --- | --- |
| Generation identity | `9f0c9e2a49178135bb7d614534d01192d158273c79d65aa2700925097edf6e72` |
| Combined input identity | `fd5043547ae263085fadd4d8199f1c6740f55d88a119baa54f4137b892eb9971` |
| Rendered manifest SHA-256 | `418b41ca45b75c4869b872e45c781980a599de061b75977ba33622d7f4e970b2` |
| Primary capture snapshot identity | `cf8d193c23263a3797db2dbb25838658f40f826d26a2bf99604b4f6d8dea8056` |
| Primary capture manifest SHA-256 | `0ba2a6a5a1c02ed0468427f8d5bc20adf403c773b478426b02f6253980030b3d` |
| Primary capture manifest byte length | `402965` |
| Auxiliary snapshot identity | `b606ff5b04d8bfe966156e555e3d3cd2859d54e67adb7ab43338fc413d24b613` |
| Active/formula fixed-point rounds | `3` |

The generation identity covers the path-independent manifest identity payload. The separate manifest-file SHA-256 in the artifact table hashes the rendered sixth file and is not expected to equal the generation identity.

### Final counts

| Measure | Count |
| --- | ---: |
| Capture sessions | 381 |
| Canonical sessions | 365 |
| Complete attack chains | 3,269 |
| Certified profiles | 260 |
| Runtime-ready profiles | 96 |
| Semantic definitions | 309 |
| Runtime-ready definitions | 101 |
| Unresolved profiles | 1,486 |
| Initial actors | 1,607 |
| Accepted actors | 504 |
| Quarantined actors | 1,103 |
| Formula profiles | 422 |
| Formula bindings | 67 |
| Generator errors | 0 |

## Validation status at this evidence update

Completed:

- Baseline generated-combat suite: **13/13 PASS**; the same baseline run reported AOtomation **1037/1037 PASS** and Arete **60/60 PASS**.
- Ordinary missing-key reproduction matrix: **7/7 PASS**, meaning the intermittent failure did not recur naturally.
- Primary extractor controlled-fault self-test: **PASS**.
- Initial coordinated post-review `--write` and `--validate-current`: **PASS**, generation `408cd0101df2c7a61b63cfa651116a327f1615431eb27b27aa055bbfd8b6eb53`, input `5487cf9be991b26c34a076ba403b557802cd697c3a0fdafbabe5445c990cd1cb`, three fixed-point rounds.
- Initial focused transaction and pipeline suite: **62/62 PASS**.
- Isolated fixture concurrency sequence: **check PASS**, **write-a PASS**, **write-b PASS**, with cleanup assertions passing.
- Pre-repair clean-worktree real stress matrix: **PASS**. Sequential checks with traversal seeds 1 and 777 and both concurrent full checks reported generation `408cd0101df2c7a61b63cfa651116a327f1615431eb27b27aa055bbfd8b6eb53`, input `5487cf9be991b26c34a076ba403b557802cd697c3a0fdafbabe5445c990cd1cb`, and three fixed-point rounds. The held-reader/two-writer fixture serialized successfully with acquisition waits of 31 ms, 1,125 ms, and 1,593 ms and left no transaction residue.
- The first attempted final mandatory gate passed all 13 stages. The second attempt failed in generated-artifact reproducibility when formula round 3 exited with Windows access violation `0xC0000005`; the consecutive count was discarded rather than retried.
- ItemDb allocation repair: the loader now streams 13 MessagePack slices and retains only 42 referenced templates instead of all 120,842. Measured peak Python allocation fell from 422,936,105 to 11,169,393 bytes (97.36 percent). Four concurrent complete formula builds passed and the 9,000,177-byte formula artifact remained byte-identical at SHA-256 `ee121b35f7ccf2df2f6592389ae3674c94a04c772f95824fbd21250b10b71da0`.
- Repaired focused transaction and pipeline suite: **64/64 PASS**.
- Post-repair coordinated `--write`: **PASS**, generation `91cbc7ef749c6f1a66f1d527d227105c30130ba0e961099996e62b36f1059a37`, input `fd60ad21be455b7c91a0f03dfecbe9fb756c3a5cd093e9bc1a9827581be835ec`, three fixed-point rounds. A PC interruption left no partial published cohort; the next writer recovered its abandoned transaction and completed successfully.
- `--validate-current` against the repaired cohort: **PASS** with generation `91cbc7ef749c6f1a66f1d527d227105c30130ba0e961099996e62b36f1059a37`.
- Standalone repository secret scan: **PASS**.
- Standalone Debug build: **PASS**. No engine was started.
- A clean final-gate attempt exposed an active-coverage access violation in the
  repeated initializer-comment regex. The parser now uses a bounded linear
  scanner; the focused test proves that path no longer calls `re.sub`.
- A coordinated write then exposed one malformed capture-worker shard. Shards
  now publish through sibling temporary files with flush/fsync, exact-byte and
  JSON read-back validation, atomic replacement, and bounded materialization
  retry after frozen/live input revalidation. Semantic shard failures remain
  fail-closed without retry.
- Real-corpus runs exposed impossible CPython object-type mutations and
  `SystemError` failures in the capture decoder. Worker retry now recognizes
  decoder-local internal failures while ordinary validation errors remain
  deterministic and fail closed after the bounded attempt budget.
- Real-corpus runs proved that private capture-shard SHA/length values could
  differ while inventory, catalog, fixtures, active coverage, and formula bytes
  were identical. Internal shard descriptors and the raw snapshot identity stay
  fail-closed and strictly validated per attempt. The schema-2 portable input
  identity hashes the normalized durable projection, so ephemeral private shard
  serialization can no longer dirty an otherwise identical cohort.
- Three consecutive formula attempts then failed while parsing a shared private
  inventory projection that prior fixed-point children had already consumed.
  The coordinator, active-coverage generator, and formula generator now create
  the standard library decoder without initializing its compiled scanner. Each
  child receives a separate round-local fsynced projection copy, verifies exact
  readback plus expected SHA-256/length over the same buffer it decodes, and
  retains bounded fresh-process retry only for recognized interpreter faults.
- The formula ItemDb reader structurally validates unrequested templates and
  fully materializes only the 42 referenced templates. The 9,000,177-byte
  formula artifact remains byte-identical at SHA-256
  `ee121b35f7ccf2df2f6592389ae3674c94a04c772f95824fbd21250b10b71da0`.
- Final focused transaction and pipeline suite: **70/70 PASS**.
- Final coordinated `--write`, `--validate-current`, and real-corpus `--check`:
  **PASS**, generation
  `9f0c9e2a49178135bb7d614534d01192d158273c79d65aa2700925097edf6e72`, input
  `fd5043547ae263085fadd4d8199f1c6740f55d88a119baa54f4137b892eb9971`, three
  fixed-point rounds.

Final delivery-only results are deliberately not embedded in this tracked evidence file:

- the final clean-worktree real stress matrix after all bounded repairs; and
- five consecutive complete mandatory integration gates from one unchanged final commit.

Those checks must occur after the final documentation commit so that every run
validates one unchanged tree. The final delivery report is authoritative for
their results.

## Proven concurrency and failure properties

Focused tests cover:

- multi-reader/single-writer exclusion and bounded waiting;
- missing, forged, stale, wrong-mode, and cross-checkout lease delegation;
- hard-exit owner cleanup while retaining live or malformed owners;
- empty-domain recovery and every instrumented publication fault point;
- rollback when validation fails before replacement and after all replacements but before commit;
- frozen-candidate tampering, partial JSON, mixed identity, and transaction hardlink attacks;
- symlink/reparse containment;
- input mutation during generation/publication;
- hash-seed, path, and enumeration-order invariance;
- strict capture snapshot and auxiliary identity validation;
- formula/provider capture discovery across all four capture source-file types;
- child timeout with descendant process-tree termination; and
- fixture cleanup with no surviving lock, staging, or transaction state.

## Broader generated-pipeline audit and deferred migrations

This task changed only generated combat. The following independent pipelines have similar consistency risks or older publication patterns. They remain separate migrations because changing them here would broaden semantic and validation scope.

### Mission graph

- `Tools/generate_mission_level_graph.py:240-265` performs an unsynchronized in-place single-file write. It needs the shared lease/snapshot/transaction model before concurrent generation can be considered safe.

### Arete movement

The Arete movement chain reparses live inputs and uses fixed temporary or independently published multi-file outputs:

- `tools-temp/AOSharpCaptureAnalyzer/audit_movement_promotion_candidates.py:1233-1240`, `:1486-1497`, `:1563`, and `:1632-1637`;
- `tools-temp/AOSharpCaptureAnalyzer/promote_arete_legacy_robot_movement.py:269-375`, `:612-614`, `:663-668`, and `:691-706`;
- `tools-temp/AOSharpCaptureAnalyzer/aggregate_arete_movement_runtime.py:237-343` and `:686-707`; and
- `tools-temp/AOSharpCaptureAnalyzer/verify_arete_movement_runtime.py:168-276`, `:692-699`, and `:836-846`.

That chain needs one authoritative snapshot and one manifest-last multi-file transaction. No movement behavior should be changed as part of that migration without separate capture proof.

### Loot

- `tools-temp/AOSharpCaptureAnalyzer/generate_subway_ordinary_content.py:2042-2070`, `:2417-2420`, `:3767-3782`, `:3978-4075`, `:4619`, and `:4862-4882` rereads capture files throughout one generation.
- Its current single-output publication is already comparatively safe: unique temp file, flush/fsync, and replace at `tools-temp/AOSharpCaptureAnalyzer/generate_subway_ordinary_content.py:5395-5411`; check mode is read-only at `:5362-5390`.
- `Tools/Export-ObservedMobLootSeed.py:341-417` and `:420-433` uses older direct CSV/SQL output. It is review-only according to `docs/reference/loot/MobLootData.md:75-88`, but should use staged output if it becomes an active producer.

### Dialogue

No executable dialogue generator was found. Current code is read-only loading/validation:

- `AORebirth/Server/ZoneEngine/Core/Playfields/DialogueContentPackLoader.cs:29-41`;
- `AORebirth/Server/ZoneEngine/Core/Playfields/AreteContentManifestLoader.cs:15-44`; and
- `AORebirth/Server/ZoneEngine/Core/Playfields/AreteJsonContentFileLoader.cs:35-84`.

No transaction migration is required unless a dialogue producer is introduced.

### WebCore compatibility and PHP supply

- `Tools/webcore_php_compatibility.py:970-998`, `:1134-1166`, and `:1315-1340` has the same hash/reopen or live-input consistency class; its apply path at `:1353-1375` is already staged more safely.
- `Tools/php_runtime_supply.py:251-272` and `:392-422`, `AORebirth/Server/WebEngine/Runtime/PhpRuntimeValidator.cs:851-878` and `:1034-1048`, and `AORebirth/Server/WebEngine/Runtime/WebCoreCompatibilityManager.cs:94-107` and `:400-425` hash files and later reopen them, leaving a time-of-check/time-of-use window.
- Existing activation/publication mechanisms are already safer at `Tools/php_runtime_supply.py:85-124` and `:674-760`, plus `AORebirth/Server/WebEngine/Runtime/WebCoreAssetManager.cs:133-167`, `:260-324`, and `:353-362`.

These paths should share immutable input bytes or a validated snapshot lease before parsing. Their runtime behavior and asset semantics are outside this generated-combat reconciliation.
