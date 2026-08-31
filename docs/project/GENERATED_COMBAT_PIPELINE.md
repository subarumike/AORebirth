# Generated Combat Cohort Pipeline

## Purpose and authority

The generated-combat pipeline publishes one internally consistent eight-file cohort. It is the only supported way to check or replace the governed generated-combat outputs. Generated output must not be edited by hand.

Promoted repository data and runtime source are the authoritative inputs for ordinary regeneration. Historical raw capture directories are development evidence used only by explicit ingestion, promotion, or provenance-verification modes. Deleting those directories must not invalidate accepted state, generation, build, deployment, or runtime.

The implementation is split between:

- `Tools/generated_combat_pipeline.py`: cohort orchestration, input freezing, fixed-point generation, manifest construction, comparison, and validation.
- `Tools/generated_artifact_transaction.py`: the shared lease, staged publication, journal, rollback, and recovery mechanism.
- `tools-temp/AOSharpCaptureAnalyzer/extract_capture_backed_npc_combat.py`: primary capture aggregation.
- `tools-temp/AOSharpCaptureAnalyzer/generate_capture_backed_npc_active_coverage.py`: active-runtime coverage projection.
- `tools-temp/AOSharpCaptureAnalyzer/analyze_enemy_combat_setup_formula.py`: setup-formula projection.

## Published cohort

The publication order is the seven payloads below followed by the manifest. The manifest is the eighth file and the transaction commit marker.

| Role | Governed path |
| --- | --- |
| Inventory | `docs/generated/capture_backed_npc_combat_inventory.json` |
| Runtime catalog | `AORebirth/Server/ZoneEngine/Core/Playfields/CapturedEnemyCombatProfileCatalog.g.cs` |
| Test fixtures | `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging.Tests/CapturedEnemyCombatProfileCatalogFixtures.g.cs` |
| Active coverage | `docs/generated/capture_backed_npc_combat_active_coverage.json` |
| Formula dataset | `docs/generated/enemy_combat_setup_formula_dataset.json` |
| Attack-range provenance audit | `docs/generated/capture_backed_npc_attack_range_audit.json` |
| Secondary-evidence provenance audit | `docs/generated/capture_backed_npc_secondary_evidence_audit.json` |
| Generation manifest | `docs/generated/capture_backed_npc_combat_generation_manifest.json` |

The manifest records the exact seven payload paths, byte lengths, SHA-256 values, acceptance counts, generator hashes, the cross-platform deterministic-runtime contract, durable promotion provenance, and generation identity. The manifest is deliberately not self-hashed inside itself. It is written last so its presence describes a complete cohort rather than a partially replaced set.

## Dependency direction and consumer classes

The only supported direction is:

`raw capture -> analysis/reconciliation -> explicit promotion -> canonical accepted repository data -> generated/runtime artifacts -> runtime`

Reverse dependencies are forbidden. Consumers are classified as follows:

- Class A, ingestion and analysis: the primary extractor, capture inventory/analyzers, scoped raw-capture audits, and `--promote-raw-evidence`. Raw capture files are required and missing evidence fails promotion closed.
- Class B, optional provenance verification: `--validate-current` and the raw attack-range/secondary-evidence audit producers. Missing raw evidence reports `EVIDENCE_NOT_LOCALLY_AVAILABLE`; it does not mutate or invalidate the accepted cohort.
- Class C, canonical generation/build/runtime: `--write`, `--refresh-accepted-coverage`, `--check`, read-lease build/test gates, deployment, and ZoneEngine runtime. Raw capture filesystem access is forbidden.

The canonical formula packet evidence is `docs/accepted/combat/enemy_combat_formula_packet_evidence.json`. It retains reviewed packet bytes, hashes, source identities, and Temple loadouts after promotion. `Tools/promote_enemy_combat_formula_packet_evidence.cmd` is an explicit promotion tool; ordinary generation only reads the accepted file. Provenance strings embedded in generated catalogs are durable metadata, not filesystem paths consumed by runtime.

## Authoritative input snapshots

Generation binds two independent input sets.

### Primary capture snapshot

This snapshot exists only for Class A raw promotion. The primary extractor discovers the capture set once and constructs an immutable, validated source plan. Each capture is parsed once into a shard. The metadata index and packet correlation both consume that same shard; they never repeat live capture parsing in separate passes.

The exported primary snapshot records the capture schema, capture paths and source-file descriptors, generator-source descriptors, and a portable snapshot identity. Before publication, the coordinator invokes the frozen primary generator's private snapshot validator against the live repository. Capture discovery, existence, byte length, and SHA-256 must still match the exported snapshot.

### Auxiliary snapshot

Under the writer lease, the coordinator freezes every canonical file that can affect ordinary regeneration. That set includes:

- the coordinator, shared transaction module, primary generator, active-coverage generator, formula generator, capture discovery code, and capture decoder;
- `tools-temp/AOSharpCaptureAnalyzer/bin/Debug/AOSharpCaptureAnalyzer.exe`;
- the AO item database at `AORebirth/Datafiles/items.dat`;
- the relevant `AORebirth/Server/ZoneEngine/Core` C# source set;
- the accepted formula packet/loadout evidence; and
- the formula's static provider/evidence inputs.

Class A promotion additionally freezes the four available raw source files for every referenced capture: `capture_info.json`, `packets.hex.log`, `raw-packets.csv`, and `scfu-appearance.csv`. These files are never part of the Class C canonical snapshot.

Snapshot identities use normalized logical paths plus content descriptors. They do not depend on repository location, enumeration order, or Python hash seed. Live and frozen copies are revalidated before use and again at the transaction's publication boundaries.

## Candidate generation

In ordinary `--write`, the committed accepted inventory is copied byte-for-byte into the candidate. The frozen primary renderer derives the runtime catalog and fixtures from that inventory without capture discovery. Active coverage and formula data derive from accepted inventory, accepted formula evidence, the item projection, and current runtime source. Accepted provenance-audit snapshots are retained unchanged because they are Class B evidence records, not Class C generation inputs.

In explicit `--promote-raw-evidence`, the primary generator and analyzer dependencies execute from the frozen auxiliary tree while capture reads are bound to the immutable source plan. That Class A path may replace the accepted inventory and provenance audits only after the complete candidate validates.

The coordinator then copies those three primary outputs into the frozen tree and derives separate exact private projections for active and formula generation from the validated authoritative inventory. Each projection is written durably, read back, and bound to the SHA-256 and byte length verified by its child over the exact bytes decoded. Projection preserves every consumed value, including complete `attackInfoPacketIds` arrays; it does not replace packet evidence with samples or counts. The full inventory remains the published authority and the source named and hashed by generated output.

Active coverage and formula data are generated as a pair that must reach a byte-identical fixed point. After a completed transition, formula equality proves the next active result is identical because active generation receives the same formula bytes, and therefore proves the next formula result and complete pair are identical as well. Only that proven identity transition is memoized; its convergence round remains counted while both redundant children are skipped. A repeated non-identical state is rejected as a deterministic cycle, and failure to converge within the configured bound is rejected. The reconciled cohort converges in three rounds.

Formula generation does not parse the full `items.dat` in Python. The coordinator
verifies the frozen ItemDb descriptor, then invokes the repository's C#
`MessagePackZip` reader to extract exactly the item templates referenced by the
governed PF127 and PF1931 profiles. The resulting private JSON projection is
canonicalized and bound to the SHA-256 and byte length verified by every formula
child. The C# reader is the same typed MessagePack path used by the runtime and
retains support for the repository's legacy `Z_SYNC_FLUSH` slice framing.

The projection reader's governed identity comes from its tracked project,
source, serialization, and typed-model inputs. Ignored `bin`/`obj` outputs are
not generator inputs because .NET Framework executables and PDBs embed absolute
checkout paths and cannot reproduce across developer clones. Current-cohort
validation and normal server builds therefore require only tracked source.
Actual `--check` and `--write` generation still fail closed unless the analyzer
executable has first been built through the documented MSBuild command.

All children run isolated and unbuffered with Python fault handling enabled. A bounded timeout terminates the complete child process tree and reports the stage label and process identity. Verified UTF-8 JSON may receive bounded retry for `JSONDecodeError` and for impossible stdlib `TypeError` or `AttributeError` failures only when the traceback proves `json.decoder`/`json.scanner` ownership. Deterministic validation failures and unrelated exceptions fail closed. Candidate JSON, UTF-8 outputs, descriptors, counts, hashes, identities, and location independence are validated before publication.

Repository-owned generated-combat, acceptance, build, and test wrappers call
`Tools/select_python_runtime.cmd`. It selects a non-embedded 64-bit CPython
3.13.14 runtime, accepts an explicit `AO_REBIRTH_PYTHON` override, and rejects
the Windows embeddable package because its isolated module path blocks
repository-local imports. The recommended Windows installer and the CPython
NuGet distribution are supported. The selected executable performs generation,
while the manifest records a stable Python 3 / UTF-8-LF determinism contract so
Windows and Linux produce identical governed bytes.

## Concurrency and read consistency

The shared generated-artifact domain uses a bounded multi-reader/single-writer lease:

- any number of readers may hold the cohort stable together;
- one writer excludes all readers and other writers;
- the writer runs recovery before generation or publication;
- owner records bind repository root, process ID, generation, mode, and a random token;
- delegated children must prove the live token, process ownership, required mode, domain, and the same checkout root; and
- direct governed generation is rejected unless the caller has valid coordinator delegation.

Build, AOtomation, mandatory-gate, active-coverage, and formula reads self-route through a read lease. A boolean environment flag is not authority. Forged, missing, stale, cross-checkout, or wrong-mode delegation fails closed.

The mandatory gate holds one read lease across all 13 stages. Nested build and
AOtomation wrappers validate that live delegation instead of parsing the full
cohort again. Its supervised command has an explicit four-hour ceiling; normal
generated children retain the 30-minute process-tree timeout.

Lease and staging state lives below `.git/aorebirth-generated-artifacts`, not beside governed outputs. Cleanup is conservative: only structurally valid state owned by a confirmed-dead process is removed automatically. Live or malformed ownership is retained and reported instead of guessed away. Containment checks reject symlink or reparse-point escapes.

## Transaction, rollback, and recovery

Publication uses a unique transaction journal and unique staging root. Candidate bytes are frozen again, flushed durably, and validated before any governed file is replaced. Existing targets are backed up, payloads are replaced in explicit order, and the manifest is replaced last.

The exact primary and auxiliary inputs are revalidated immediately before the first replacement and again after all replacements but before commit. A failure at either point aborts publication. A failure after replacements have begun restores the prior complete cohort from backups. Only after the second validation succeeds does the journal become committed and backup retirement begin.

On the next writer acquisition, recovery interprets the journal and either restores an interrupted uncommitted transaction or finishes cleanup for a committed one. Missing files, mixed generations, partial JSON, invalid hashes, malformed manifests, candidate tampering, and unsafe transaction paths all fail closed.

## Supported commands

Run commands from the repository root with `cmd.exe`:

```bat
Tools\generate_capture_backed_npc_combat_inventory.cmd --check
Tools\generate_capture_backed_npc_combat_inventory.cmd --write
Tools\generate_capture_backed_npc_combat_inventory.cmd --promote-raw-evidence
Tools\generate_capture_backed_npc_combat_inventory.cmd --refresh-accepted-coverage
Tools\generate_capture_backed_npc_combat_inventory.cmd --validate-current
Tools\promote_enemy_combat_formula_packet_evidence.cmd --check
Tools\generate_capture_backed_npc_combat_inventory.cmd --self-test
Tools\run_generated_combat_concurrency_tests.cmd
Tools\stress_generated_combat_pipeline.cmd
```

With no arguments, `Tools\generate_capture_backed_npc_combat_inventory.cmd` performs `--check`.

- `--check` validates the committed accepted artifact cohort without reading historical raw captures. It does not publish.
- `--write` regenerates all Class C outputs from promoted repository data, validates them, and atomically publishes the complete cohort without raw captures.
- `--promote-raw-evidence` is the only full raw-ingestion/promotion writer. Missing raw evidence fails closed before publication.
- `--refresh-accepted-coverage` is a compatibility alias for full canonical `--write`; it no longer maintains a partial-generation path.
- `--validate-current` validates accepted integrity first, then performs optional local provenance availability checks. Missing historical evidence reports `EVIDENCE_NOT_LOCALLY_AVAILABLE` and returns with accepted state still valid.
- `Tools\promote_enemy_combat_formula_packet_evidence.cmd` promotes reviewed formula packet/loadout observations into canonical accepted data; it is not part of build or regeneration.
- `--self-test` runs the primary extractor's focused self-test, including its controlled missing-generation-key invariant.
- `Tools\run_generated_combat_concurrency_tests.cmd` runs transaction/coordinator unit coverage plus the isolated fixture concurrency scenario.
- `Tools\stress_generated_combat_pipeline.cmd` requires a clean worktree and exercises real sequential checks, concurrent checks, reader/writer contention, identity stability, status stability, and residue cleanup.

The complete integration gate calls `--check`; it therefore rejects stale or mixed generated-combat inputs before later build and acceptance stages. Neither the gate nor build/deployment/runtime reads raw captures.

## Failure contract

The pipeline returns failure and leaves the previously committed cohort authoritative when any required proof is missing. Important failures include:

- a capture packet references a metadata generation absent from its immutable shard;
- capture or auxiliary input changes during generation or either publication boundary;
- active/formula generation cycles or does not converge;
- a child fails, times out, or omits its staged output;
- a candidate or published artifact is malformed, stale, mixed, or path-dependent;
- lease delegation is missing, forged, stale, from another checkout, or has insufficient mode; or
- a transaction cannot prove safe containment, rollback, or recovery.

For a missing metadata generation, the primary extractor raises `CaptureAggregationInvariantError` with `missingGenerationKey`, `owningPacket`, `owningSession`, `snapshot`, and `phase`. It does not substitute defaults or continue with partial evidence.
