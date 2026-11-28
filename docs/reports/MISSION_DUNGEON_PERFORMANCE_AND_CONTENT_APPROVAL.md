# Mission Phase 3B dungeon performance and content approval

## Source and disposition

| Input | Verified revision |
| --- | --- |
| Starting feature and benchmark baseline | `c0e1f324ea228597b4f85672374641589846068e` |
| Starting public master | `98090d8e1b6411bf8d413a0cab683f922a515a84` |
| Measured runtime implementation | `5ac7f2746bc4a0a80411f8c9e0b45af745a65a` |
| Final implementation and validation source | `7bcc873e455f26fd78ce2f0bba51109cdabc7ae8` |
| Feature branch | `codex/mission-dungeon-modules` |

Physical placement and collision preparation now allocate substantially less. Private archive approval and publication are pending. The protected master integration decision remains open.

## Implementation

- Placement uses the existing ray and sphere-sweep math with value-type coordinates. A sweep can reject a blocked route at its first blocking witness. Clear routes still perform the original samples and bidirectional clearance checks. Existing collision callers and nearest-hit diagnostics keep their full query behavior.
- Mesh and floor indexes reuse exact bound geometry. Retained district lookups reuse the verified catalog index. Objectives and population share a room-distance result for the same retained layout. Weak layout keys let the indexes disappear with the layout.
- Original room surfaces and height offsets are normalized once per room in the verified style catalog. Each instance owns its placed positions, orientations, floor transforms, door records and mesh arrays. Placed meshes copy topology from the cached template. The geometry ownership test changes one placed mesh and checks that a second dungeon remains exact.
- The immutable resolver keys style entries by schema, style and archive digest, and NPC catalogs by their own digest. Its least-recently-used caches keep at most 16 style entries and two NPC catalog identities. Style cache entries hold the NPC identity instead of the parsed payload. A failed load is discarded. Worlds can retain resources they are using independently of cache eviction.
- Private staging rejects links, junctions, traversal, unexpected archive ownership and inconsistent prior indexes. Previously retained immutable files are carried forward.

The existing procedural generator, DAO contracts and transaction paths still own their work. V2 and V3 reconstruction remain supported, with the explicit V1 refusal. Initial door placement and lock rolls retain their deterministic behavior. Later lock, unlock, open and close transitions still have live-world scope. Durable door transitions remain a separate mission task.

## Benchmarks

The exact prior and optimized runtime trees ran separately with byte-identical test-only instrumentation, staged inputs, layouts and QLs. Each exercised normal acceptance and first dungeon entry, two warmer iterations, and reconstruction against a different active GameData index. First-time unsealed worlds used their normal cryptographic chest seed, so their total timings include stochastic chest work. The standalone placement measurements use an identical fixed seed. Every row below shows baseline → optimized wall time, process CPU time and calling-thread allocated memory. The process peak is a host-wide high-water mark.

| Style / floors / seed / QL | Cold accept | Cold first world | Warm first world, median of two | Reconstruct with a fresh resolver |
| --- | --- | --- | --- | --- |
| `321/1/1/QL37` | 1,130.08 → 1,389.76 ms / 1,125.00 → 1,421.88 ms / 377.25 → 331.47 MiB | 1,626.64 → 1,094.74 ms / 1,640.62 → 1,078.12 ms / 583.93 → 376.46 MiB | 1,250.92 → 1,093.76 ms / 1,210.94 → 1,078.12 ms / 597.61 → 376.22 MiB | 1,198.04 → 1,413.32 ms / 1,140.62 → 1,500.00 ms / 429.01 → 383.20 MiB |
| `324/1/9/QL125` | 3,014.12 → 2,744.24 ms / 3,531.25 → 3,218.75 ms / 461.69 → 400.87 MiB | 2,836.25 → 2,287.11 ms / 2,718.75 → 2,343.75 ms / 728.88 → 385.78 MiB | 1,609.58 → 1,382.83 ms / 1,601.56 → 1,445.31 ms / 693.20 → 379.38 MiB | 1,631.16 → 1,553.04 ms / 2,062.50 → 1,984.38 ms / 449.49 → 389.01 MiB |
| `341/2/42/QL22` | 1,584.91 → 1,495.56 ms / 1,906.25 → 1,671.88 ms / 378.30 → 332.49 MiB | 1,402.82 → 1,670.81 ms / 1,359.38 → 1,656.25 ms / 490.52 → 374.88 MiB | 1,416.28 → 1,319.31 ms / 1,359.38 → 1,257.81 ms / 499.72 → 374.94 MiB | 1,536.42 → 1,272.42 ms / 1,578.12 → 1,234.38 ms / 429.95 → 384.13 MiB |
| `351/1/2/QL160` | 1,769.03 → 1,695.77 ms / 1,734.38 → 1,593.75 ms / 435.40 → 338.82 MiB | 2,075.85 → 1,754.72 ms / 1,984.38 → 1,734.38 ms / 801.85 → 384.01 MiB | 2,167.62 → 1,773.55 ms / 2,140.62 → 1,773.44 ms / 812.89 → 383.96 MiB | 1,883.09 → 1,768.57 ms / 1,859.38 → 1,703.12 ms / 487.42 → 390.80 MiB |

| Stage | `321/1/1/QL37` | `324/1/9/QL125` | `341/2/42/QL22` | `351/1/2/QL160` |
| --- | --- | --- | --- | --- |
| Resolve retained original resources | 980.57 → 706.77 ms / 890.62 → 671.88 ms / 321.29 → 321.29 MiB | 649.20 → 680.80 ms / 796.88 → 796.88 ms / 321.02 → 321.02 MiB | 1,075.26 → 1,000.97 ms / 1,015.62 → 921.88 ms / 322.36 → 322.37 MiB | 1,030.41 → 1,015.06 ms / 1,078.12 → 906.25 ms / 321.65 → 321.66 MiB |
| Resolve warm retained resources, first repeat | 0.01 → 0.01 ms / 0.00 → 0.00 ms / 0.00 → 0.00 MiB | 0.02 → 0.02 ms / 0.00 → 0.00 ms / 0.00 → 0.00 MiB | 0.01 → 0.02 ms / 0.00 → 0.00 ms / 0.00 → 0.00 MiB | 0.01 → 0.01 ms / 0.00 → 0.00 ms / 0.00 → 0.00 MiB |
| Generate layout | 0.53 → 0.58 ms / 0.00 → 0.00 ms / 0.80 → 0.80 MiB | 2.48 → 2.51 ms / 0.00 → 0.00 ms / 2.17 → 2.17 MiB | 0.72 → 0.94 ms / 0.00 → 0.00 ms / 1.00 → 1.00 MiB | 22.32 → 1.53 ms / 15.62 → 0.00 ms / 2.28 → 2.28 MiB |
| Bind original geometry | 4.98 → 4.30 ms / 0.00 → 0.00 ms / 3.87 → 4.03 MiB | 15.22 → 6.39 ms / 15.62 → 15.62 ms / 4.11 → 3.83 MiB | 4.54 → 3.35 ms / 15.62 → 0.00 ms / 3.72 → 3.78 MiB | 20.28 → 19.14 ms / 15.62 → 31.25 ms / 6.43 → 5.78 MiB |
| Build collision surface | 34.50 → 34.28 ms / 31.25 → 46.88 ms / 4.46 → 4.46 MiB | 149.91 → 156.73 ms / 140.62 → 125.00 ms / 39.63 → 39.63 MiB | 34.98 → 32.44 ms / 31.25 → 31.25 ms / 4.08 → 4.08 MiB | 50.25 → 53.91 ms / 62.50 → 62.50 ms / 7.43 → 7.43 MiB |
| Prepare NPC population | 0.63 → 0.49 ms / 15.62 → 0.00 ms / 0.26 → 0.26 MiB | 7.58 → 8.23 ms / 15.62 → 15.62 ms / 0.32 → 0.32 MiB | 0.29 → 0.28 ms / 0.00 → 0.00 ms / 0.17 → 0.17 MiB | 0.38 → 0.37 ms / 0.00 → 0.00 ms / 0.47 → 0.46 MiB |
| Validate frozen objective, standalone | 377.66 → 327.46 ms / 375.00 → 328.12 ms / 46.80 → 0.85 MiB | 489.70 → 455.63 ms / 546.88 → 531.25 ms / 61.77 → 1.42 MiB | 492.95 → 466.74 ms / 484.38 → 468.75 ms / 47.10 → 1.21 MiB | 744.97 → 622.38 ms / 750.00 → 578.12 ms / 97.58 → 1.63 MiB |
| Construct doors and transitions | 0.13 → 0.10 ms / 0.00 → 0.00 ms / 0.03 → 0.03 MiB | 14.80 → 11.20 ms / 0.00 → 15.62 ms / 0.04 → 0.04 MiB | 0.76 → 0.84 ms / 15.62 → 0.00 ms / 0.03 → 0.03 MiB | 0.14 → 0.12 ms / 0.00 → 0.00 ms / 0.05 → 0.05 MiB |
| Plan chest positions with fixed seed | 454.50 → 374.86 ms / 453.12 → 343.75 ms / 150.55 → 2.78 MiB | 785.42 → 668.77 ms / 828.12 → 656.25 ms / 256.46 → 4.88 MiB | 519.56 → 455.33 ms / 515.62 → 484.38 ms / 67.84 → 1.73 MiB | 1,018.99 → 725.16 ms / 968.75 → 718.75 ms / 334.24 → 5.67 MiB |
| Register objective and initialize chests | 1,169.67 → 706.03 ms / 1,187.50 → 687.50 ms / 527.56 → 369.18 MiB | 1,775.90 → 1,552.86 ms / 1,859.38 → 1,656.25 ms / 651.26 → 372.12 MiB | 831.14 → 1,145.41 ms / 859.38 → 1,156.25 ms / 434.27 → 367.52 MiB | 1,274.65 → 1,042.16 ms / 1,203.12 → 1,031.25 ms / 687.41 → 370.70 MiB |
| Seal and materialize chest population after plan | 5.50 → 6.36 ms / 0.00 → 0.00 ms / 0.05 → 0.04 MiB | 22.38 → 19.63 ms / 15.62 → 15.62 ms / 0.06 → 0.07 MiB | 6.74 → 6.40 ms / 15.62 → 15.62 ms / 0.03 → 0.03 MiB | 6.83 → 7.01 ms / 31.25 → 0.00 ms / 0.08 → 0.06 MiB |

| Workload | Baseline peak working set | Optimized peak working set |
| --- | ---: | ---: |
| Complete four-case benchmark host | 2,271.27 MiB | 2,281.26 MiB |

Cold physical validation took less wall time and allocated roughly 97–98% less in every fixed layout. First-world allocations fell in all four cases. Cold elapsed time was mixed. The two-floor style 341 first world, and the single-floor style 321 acceptance and fresh-resolver comparison, took longer in these samples. Cold retained-resource loading still allocates roughly 321–322 MiB while reading and parsing the large shared NPC archive. Process peak RSS did not fall. These results do not establish a whole-path cold latency improvement under concurrent load.

### Identical persisted snapshots in fresh processes

The separate capture host wrote the four accepted mission and durable census/inventory snapshots once on exact Phase 3. Each replay host loaded the same four files and used the normal `QuestDungeonService.ResolveLogin` path with a fresh service, resolver and manager against the second active GameData revision. Baseline and optimized replay each passed 4/4 in different .NET test-host processes from each other and the capture host. The processes used the serialized test DAO, with no running Zone executable in this benchmark. Each case creates a new resolver. Later cases in the same host can reuse CLR startup work.

| Style / floors / seed / QL | Reconstruct wall | Process CPU | Calling-thread allocation |
| --- | ---: | ---: | ---: |
| `321/1/1/37` | 1,609.00 → 1,231.75 ms | 2,156.25 → 1,671.88 ms | 432.95 → 387.00 MiB |
| `324/1/9/125` | 2,996.04 → 2,703.61 ms | 3,468.75 → 3,171.88 ms | 490.17 → 429.26 MiB |
| `341/2/42/22` | 1,448.02 → 1,580.00 ms | 1,734.38 → 2,015.62 ms | 433.80 → 387.82 MiB |
| `351/1/2/160` | 2,046.34 → 1,678.61 ms | 2,312.50 → 2,062.50 ms | 487.08 → 390.71 MiB |

Peak working set observed through the replay workload was 2,282.40 → 2,276.64 MiB.

The four capture hashes, raw transformed collision fingerprints, door and transition placements, objective binding, NPC population and deliberately persisted killed branch all agreed. The stored chest packets and contents replayed exactly. Every case used the same DAO counts across versions, including one mission transaction and two mission snapshot reads, with zero acceptance writes. The exact-input two-floor style 341 replay took about 132 ms longer in this sample. The interval covering objective registration and sealed chest materialization accounted for most of the observed delay. This does not establish a persistent regression in that interval or a change to chest behavior. Dedicated Zone-executable startup and network latency remain unmeasured.

All four layout/objective/door/NPC fingerprints, 12 fixed-seed chest plans and 16 fixture DAO-boundary count records matched. Each individual mission replay also retained its actual chest packets and stored item rows. Fixture call counts identify boundaries, while the separate MySQL checks exercise existing SQL implementations on a disposable database.

## Private archive verification

The exact approval draft adds 47 files totaling 91,854,107 bytes. The private upstream projection grows from 17 to 64 files and the full canonical candidate grows from 6,856 to 6,903 files. Both projections have zero modifications and removals. Forty-two archived copies of 30 distinct original files match the approved canonical bytes. The shared NPC catalog and spawn-feature inputs match their recorded canonical reconciliation lineage. The second test revision retains all 46 immutable first-revision files.

The ignored owner package has the authenticated starting revision, proposed Git tree, original asset and NPC catalog hashes, before-and-after full manifests, and readback-verified review payload. Three unrelated existing canonical differences remain a separate governed intake gate. Public Git contains no archive files, private catalog identities, or approval records.

Explicit approval of the exact private package is required. After approval, publish the verified private tree and read back the resulting SHA. Prepare a protected PR only after content publication and an acceptable review of performance. Official Windows intake uses the protected source that admits the archive paths and resolves the pre-existing canonical differences through the existing workflow.

## Validation

| Check | Result |
| --- | --- |
| Identical four-case benchmark harness | 4/4 on exact Phase 3 and 4/4 on the optimized runtime. |
| Focused mission regression and retail packet fixtures | 300 passed of 301. One proven inherited exception, zero new failures and zero skips. |
| All five objectives | Acceptance, cross-revision reconstruction and completion passed. |
| Historical and damaged archives | Historical V2 golden layouts, historical V3 restoration, explicit V1 refusal, missing/tampered archive rejection and multi-floor routes passed. |
| Geometry and population | Full versus first-hit sweep outcomes, floor-result parity, retained mesh ownership, durable NPC kill observations, stable NPC transforms and no-respawn passed. |
| Mission responses | All 78 reproduced SHA-256 `3d1aa7e35baa65d7669390c439d75311e12d249385c9e9e2eb304ea2fe834604`. |
| Disposable MySQL | 98 acceptance checks and 50 chest checks passed. |
| Staging and admission | 6 tests passed. |
| Runtime data architecture | 31/31 mutation groups passed, zero introduced findings and 216 inherited findings recorded. |
| Hosted check | Optimized runtime passed on the exact measured SHA. [Runtime data architecture run](https://github.com/subarumike/AORebirth/actions/runs/46129322629). |

The inherited `OrdinaryRetailChestUsePublishesContentsBeforeSuccessAndCloseBeforeSuccess` assertion again reports expected 21 and actual 20 at byte index 16. The relevant packet source and fixture remained unchanged. Phase 3 reproduced the same signature on the byte-identical public-master tree.

## Status

| Required status | Result |
| --- | --- |
| `MISSION_PHASE3B` | PARTIAL |
| `DUNGEON_PERFORMANCE` | PARTIAL, with lower allocations and mixed cold wall time |
| `COLD_PLACEMENT_OPTIMIZATION` | PASS |
| `FULL_DUNGEON_BENCHMARKS` | PASS for complete entry and reconstruction in the measured test hosts |
| `ORIGINAL_AO_GEOMETRY` | PASS |
| `DETERMINISTIC_RECONSTRUCTION` | PASS |
| `CROSS_REVISION_RECONSTRUCTION` | PASS |
| `NPC_POPULATION_PERSISTENCE` | PASS |
| `CHEST_PERSISTENCE` | PASS |
| `PRIVATE_ARCHIVES_VALIDATED` | PASS |
| `PRIVATE_CONTENT_APPROVED` | NO |
| `PRIVATE_CONTENT_PUBLISHED` | NO |
| `EXISTING_DAO_PRESERVED` | YES |
| `DOA_ENFORCEMENT` | PASS |
| `NEW_REGRESSIONS` | 0 functional regressions. Mixed measured latency is recorded separately. |
| `MASTER_INTEGRATION_READY` | NO |
| `PRODUCTION_DEPLOYMENT` | NOT_PERFORMED |

## Evidence hashes

| Validation artifact | SHA-256 |
| --- | --- |
| Baseline construction benchmark | `6f975513e26896ee5b2fb3aa224e97fa1ff8fae61e0e0bac97cd266d21f84288` |
| Optimized construction benchmark | `86cd2691d1c9f8c9e1a17d0f59f5b7e11501d6670a6cdc066dbd66b4eaadc4c5` |
| Baseline separate-host replay | `0abbbc393a9d465b1ffcfa616011ac25f8039e460cb9b4d3f147a6d60dd0cb7e` |
| Optimized separate-host replay | `4918e24201a13f25f0d08570944a950401ae2265a99a270e46f5e9f5fddef8bc` |
| Focus suite with proven baseline exception | `f84e13b04e0fbf2c7d3d1cf633fcb127d6282cea15bdf23b854a48120b990dce` |
| Disposable acceptance DAO suite | `ddd6794e8e17200462c76328641af7b7d1cc65fb88e660067b3be99f520a4ee8` |
| Disposable chest DAO suite | `b8d604f3ba118cab97a437768849bda74ed1b3d3c977874aa4bd6bf797e39ce6` |
