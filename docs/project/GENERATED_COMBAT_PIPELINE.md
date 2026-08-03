# Generated Combat Cohort Pipeline

## Purpose and authority

The generated-combat pipeline publishes one internally consistent six-file cohort. It is the only supported way to check or replace the governed generated-combat outputs. Generated output must not be edited by hand.

Runtime and capture evidence are authoritative inputs. In particular, an active-coverage hash that changes because a supported runtime source such as `AreteAlienAreaMobRuntime.cs` changed is reconciled by regenerating the cohort. The generated report does not authorize changing runtime behavior. Runtime code may be changed only when capture or other authoritative evidence proves a real behavior mismatch.

The implementation is split between:

- `Tools/generated_combat_pipeline.py`: cohort orchestration, input freezing, fixed-point generation, manifest construction, comparison, and validation.
- `Tools/generated_artifact_transaction.py`: the shared lease, staged publication, journal, rollback, and recovery mechanism.
- `tools-temp/AOSharpCaptureAnalyzer/extract_capture_backed_npc_combat.py`: primary capture aggregation.
- `tools-temp/AOSharpCaptureAnalyzer/generate_capture_backed_npc_active_coverage.py`: active-runtime coverage projection.
- `tools-temp/AOSharpCaptureAnalyzer/analyze_enemy_combat_setup_formula.py`: setup-formula projection.

## Published cohort

The publication order is the five payloads below followed by the manifest. The manifest is the sixth file and the transaction commit marker.

| Role | Governed path |
| --- | --- |
| Inventory | `docs/generated/capture_backed_npc_combat_inventory.json` |
| Runtime catalog | `AORebirth/Server/ZoneEngine/Core/Playfields/CapturedEnemyCombatProfileCatalog.g.cs` |
| Test fixtures | `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging.Tests/CapturedEnemyCombatProfileCatalogFixtures.g.cs` |
| Active coverage | `docs/generated/capture_backed_npc_combat_active_coverage.json` |
| Formula dataset | `docs/generated/enemy_combat_setup_formula_dataset.json` |
| Generation manifest | `docs/generated/capture_backed_npc_combat_generation_manifest.json` |

The manifest records the exact five payload paths, byte lengths, SHA-256 values, acceptance counts, generator hashes, Python runtime descriptor, primary capture snapshot identity, auxiliary snapshot identity, combined input identity, and generation identity. The manifest is deliberately not self-hashed inside itself. It is written last so its presence describes a complete cohort rather than a partially replaced set.

## Authoritative input snapshots

Generation binds two independent input sets.

### Primary capture snapshot

The primary extractor discovers the capture set once and constructs an immutable, validated source plan. Each capture is parsed once into a shard. The metadata index and packet correlation both consume that same shard; they never repeat live capture parsing in separate passes.

The exported primary snapshot records the capture schema, capture paths and source-file descriptors, generator-source descriptors, and a portable snapshot identity. Before publication, the coordinator invokes the frozen primary generator's private snapshot validator against the live repository. Capture discovery, existence, byte length, and SHA-256 must still match the exported snapshot.

### Auxiliary snapshot

Under the writer lease, the coordinator freezes every auxiliary file that can affect the cohort. That set includes:

- the coordinator, shared transaction module, primary generator, active-coverage generator, formula generator, capture discovery code, and capture decoder;
- `tools-temp/AOSharpCaptureAnalyzer/bin/Debug/AOSharpCaptureAnalyzer.exe`;
- the AO item database at `AORebirth/Datafiles/items.dat`;
- the relevant `AORebirth/Server/ZoneEngine/Core` C# source set;
- the formula's static provider/evidence inputs; and
- all four available source files for every capture referenced by formula/provider inputs: `capture_info.json`, `packets.hex.log`, `raw-packets.csv`, and `scfu-appearance.csv`.

Snapshot identities use normalized logical paths plus content descriptors. They do not depend on repository location, enumeration order, or Python hash seed. Live and frozen copies are revalidated before use and again at the transaction's publication boundaries.

## Candidate generation

The primary generator and its analyzer dependencies execute from the frozen auxiliary tree, while capture reads are bound to the primary source plan in the repository. The primary stage produces inventory, runtime catalog, fixtures, and the private capture snapshot manifest in a unique candidate directory.

The coordinator then copies those three primary outputs into the frozen tree and generates active coverage and formula data as a pair. The pair must reach a byte-identical fixed point. A repeated non-identical state is rejected as a deterministic cycle, and failure to converge within the configured bound is rejected. The reconciled cohort converges in three rounds.

Formula generation streams each top-level MessagePack array in the frozen
`items.dat` and retains only item templates referenced by the governed PF127 and
PF1931 profiles. It preserves duplicate-ID last-record-wins behavior and rejects
truncated sizes, invalid zlib data, malformed roots/templates, unused compressed
tails, trailing MessagePack bytes, and trailing database bytes. The repository's
legacy `Z_SYNC_FLUSH` slice framing remains accepted.

All children run isolated and unbuffered with Python fault handling enabled. A bounded timeout terminates the complete child process tree and reports the stage label and process identity. Candidate JSON, UTF-8 outputs, descriptors, counts, hashes, identities, and location independence are validated before publication.

## Concurrency and read consistency

The shared generated-artifact domain uses a bounded multi-reader/single-writer lease:

- any number of readers may hold the cohort stable together;
- one writer excludes all readers and other writers;
- the writer runs recovery before generation or publication;
- owner records bind repository root, process ID, generation, mode, and a random token;
- delegated children must prove the live token, process ownership, required mode, domain, and the same checkout root; and
- direct governed generation is rejected unless the caller has valid coordinator delegation.

Build, AOtomation, mandatory-gate, active-coverage, and formula reads self-route through a read lease. A boolean environment flag is not authority. Forged, missing, stale, cross-checkout, or wrong-mode delegation fails closed.

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
Tools\generate_capture_backed_npc_combat_inventory.cmd --validate-current
Tools\generate_capture_backed_npc_combat_inventory.cmd --self-test
Tools\run_generated_combat_concurrency_tests.cmd
Tools\stress_generated_combat_pipeline.cmd
```

With no arguments, `Tools\generate_capture_backed_npc_combat_inventory.cmd` performs `--check`.

- `--check` builds a complete candidate from frozen inputs and fails if any of the six published files differs. It does not publish.
- `--write` builds, validates, and atomically publishes a complete candidate under the writer lease.
- `--validate-current` validates the current manifest, all five payload descriptors and acceptance counts, the recorded toolchain, and current authoritative input identities without regenerating.
- `--self-test` runs the primary extractor's focused self-test, including its controlled missing-generation-key invariant.
- `Tools\run_generated_combat_concurrency_tests.cmd` runs transaction/coordinator unit coverage plus the isolated fixture concurrency scenario.
- `Tools\stress_generated_combat_pipeline.cmd` requires a clean worktree and exercises real sequential checks, concurrent checks, reader/writer contention, identity stability, status stability, and residue cleanup.

The complete integration gate calls `--check`; it therefore rejects stale or mixed generated-combat inputs before later build and acceptance stages.

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
