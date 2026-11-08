# Retail mission startup and version 1 compatibility

## Source and content

| Input | Revision |
| --- | --- |
| Feature branch | codex/retail-mission-doa-repair |
| Starting feature | 9abd2a00137fa36394594a9c971f001f47d563ae |
| Initial runtime and guard repair | 56efd30ec8301284390215393ef9a947fc059ac6 |
| Validated runtime and tests | 6c2b6e10b82c4e3cfe087d8f58bd7f50216a029a |
| Public master used for the architecture comparison | cda38284540f45573424fb399a60c9ab7627a6f7 |
| Approved private content | 2b5431fd382bd5fa00a7b232efb85dc556ad7cc2 |
| Complete test GameData manifest SHA-256 | 04e18c137f28a5b13a63548d65976b72236df664c0581a17c07d28c484c13000 |
| Required items.dat SHA-256 | f470fa9d8683f95e6642f0115d0a8641eee1ab92e8d6acac5412e2afe8604ebe |

The release content and original-client test installation were preserved. The candidate used a separate checkout, a test-only database and loopback ports. The original-client acceptance gate applies to the new runtime revision.

## Zone startup

**OBSERVED.** On the original isolated Windows startup, MsgPack.Cli threw `ArgumentException: An item with the same key has already been added. Key: 725` while unpacking `DatSpell.Stats` under `DatRdbTemplate.Events`. `ItemTemplateCatalog` logged the exception and published an empty catalog. Mission reward validation then stopped Zone with `Configured mission reward template is unavailable: 101335`. Shutdown also reported an initialization/disposal error. The same startup command succeeded on retry.

**PROVEN.** The catalog previously caught every load exception and exposed whatever had been loaded. An item-decoder failure could therefore surface as a misleading downstream mission error. Shutdown also resolved `QuestDungeonService` from the container even before the service's startup point. That resolution could construct the dungeon sweep and inventory services during failure cleanup.

Two independent scans of the retained approved file consumed 14 compressed slices and all 9,471,730 bytes. The header, decoded population and distinct IDs agreed at 131,807. The scans found zero repeated keys across 425,835 spell-stat maps. The standalone current reader also loaded all items. These checks establish the retained file's integrity. The original failing process did not record its stream cursor or a hash of the bytes it read during that attempt.

**UNVERIFIED.** The initiating duplicate-key exception on valid retained data did not recur in the controlled baseline. The evidence does not establish whether the failure began in MsgPack, the runtime, or the input as seen by that process. A deliberately duplicated stat reproduces the empty-catalog failure path in the old source, but it does not explain the intermittent trigger.

### Startup changes

- `ItemsDatReader` checks compressed slice boundaries, complete item arrays, declared counts and trailing data. It preserves the decoding exception and reports the compressed and decompressed positions when available.
- `ItemTemplateCatalog` publishes the completed map after successful load and rejects a missing, empty or invalid required catalog. It identifies the file and original error.
- `Program` resolves items, mission offers, reward references and key policy before World registration. It stops the dungeon service only if startup acquired it and skips team teardown when World startup was never reached.

The package preflight still checks the package and configuration. The cold-start runs below exercise real catalog admission in the native Zone process.

| Cold process checks | Outcome |
| --- | --- |
| Baseline 9abd2a001, isolated World | 8/8 ready and clean exit. Five launches of the incident apphost, two launches from a fresh native build and one managed control. Native readiness 5.688–7.594 s. |
| Baseline 9abd2a001, World plus Chat and Login | 4/4 exact incident apphost starts ready and clean exit. Native readiness 6.531–7.125 s. |
| Final 6c2b6e10, World plus Chat and Login | 8/8 ready and clean exit. Seven native apphost runs at 5.672–6.938 s and one managed control at 6.015 s. Every run loaded 131,807 items and the same 13 mission response profiles and 65 definitions. |
| Final 6c2b6e10, malformed nonempty items.dat | Native process exited 1 in 1.578 s with a catalog-specific package error. The Zone never became ready, registered with World or opened its port. Teardown completed without a shutdown error. |

Each trial launched a new process with a bounded timeout. The operating-system file cache was retained. Companion engines and all owned test listeners were cleaned up. The generated-item contract permits repeated top-level item IDs. The final implementation preserves the existing last-record precedence and distinct catalog count. Duplicate keys inside a spell-stat map remain an error.

The startup hardening is verified. The incident-level reliability status remains FAIL because the original intermittent trigger is unresolved.

| Retained evidence | SHA-256 |
| --- | --- |
| Baseline native and managed summary | 48bf364fd81c5c682bbbffc2f81792d6adb66b26855ec6d79c30f90d23eda93a |
| Baseline full-context summary | 8f7fa447743e7dc9664e9d2772b3d1e53664dcb857f4358ab62b795a5c333129 |
| Final governed build receipt | 78f5757fea11bf8f01f115a13e3a6fe76dfd70797513a489c565ede3db68e901 |
| Final positive summary | 54899fd041a73f58242b5378a40a9e431791e47ed66a239ed135e228ed0eaa99 |
| Final malformed-input summary | 7b2306e01c2ded029cb9ee9e50583d5dcef88ccf377009e71a9398459e049bdd |

## Historical version 1

The pre-cutover captured mission runtime at `c8f4b98987453e65bd66308732775513e9ddb67b` selected a compatible bundle and saved its ID, generator hash, captured building identity, allocated PF2 and object state through the generated mission DAO. The persisted offer held the exterior destination. The old selection seed chose a bundle, and the generator payload held the actual geometry.

Commit `8f517d711f6208c3308c06c68b414fd59a1316df` removed the captured world runtime. Its new accepted-dungeon writer already used generation version 2. The current versioned JSON in `generatedquests` and the old `generatedmissionbindings` and `generatedmissionobjects` rows use distinct contracts and playfield identity ranges. No historical writer or conversion was found that made a version 1 seed-only JSON row sufficient to choose the old bundle. The old binding's row revision and the generator's wire versions are separate fields.

The existing capture evidence in `AORebirth/GameData/Missions/Layouts.json` (Git blob `8321e8f95b6e4ea1102677db42e917121b1a0f48`) retains exact C79F generator bodies, the entry point, structural and objective placements, and the interior exit. The original runtime split the last eight bytes into the PlayfieldAnarchyF world coordinates. The five layout hashes are independently documented. [Original capture provenance](../evidence/RK_MISSION_ACG_INTERIOR_EVIDENCE_20260728.md)

| Original layout | Style | Floors | Room records | Captured doors | SHA-256 |
| --- | ---: | ---: | ---: | ---: | --- |
| Kill Person | 341 | 1 | 10 | 11 | ffe4327ac8af0f0a41a04cff7fe53ecd40c55a027f10a2cda2cd2a8fc18f1269 |
| Find Person | 351 | 1 | 13 | 14 | d5413273f69b018b66fcd6fe31bfa7be15b338cb6cb8fd17d83f7e14c4e4be82 |
| Find Item | 321 | 1 | 24 | 27 | 3cfe53d3a32b50679530bdfd5ff7572405eb8865f4ab0c13308c7bcd935bf431 |
| Repair Machine | 341 | 1 | 17 | 17 | e75f1326a72db6d42ddb5ebd72320338148193e6469e70b1c30b2d8a0f6d1926 |
| Return Item | 341 | 1 | 20 | 21 | f7f00e3344bd12f2d7d302761403c9c5b083fc8a181417c7f2c9748da501ff59 |

`V1_RESULT`

At runtime, a version 1 seed-only record stays readable, and the layout generator reports the missing captured-layout binding instead of building version 2 geometry. The existing safe exterior return for a character saved in an old captured PF2 remains in place. There were no changes to player records, keys, generated mission schemas, or the version 2 and version 3 generators. Reactivating a specific historical accepted mission requires its matching persisted bundle and offer, runtime object identities and state, and a verified path to materialize them without rerolling. No matching historical player-instance snapshot was identified in the reviewed evidence. Static capture equality alone does not establish live reconstruction.

## Regression validation

The final governed feature run passed **183 tests, zero failures, zero skips** on `6c2b6e10b82c4e3cfe087d8f58bd7f50216a029a`, with a clean measured source closure and the complete approved GameData manifest. It covered the required item-catalog admission cases, 13 V1 checks, frozen V2 packet and spawn bits, deterministic V3 solo and team reconstruction, five objectives, entry and reconnect, key duplication and cleanup, chest inventory transfers, mission terminal wire output, sliders, rewards and the recovered cave. Historical entry and exit coverage was offline. Walking and original-client presentation remain pending.

The final TRX SHA-256 is `cfee719e852c237aa3c648a354c8e05713564a388ad3a39eb6c461513f4957a7`. The authoritative architecture report SHA-256 is `a1a4811aa5553a90b93edbe6ed124eff67737696d53709aab53e8ac8c8079f74`.

The earlier chest-use packet offset mismatch and the intermittent style-351 visibility case remain separate baseline findings. The recovered QL 125 fixture, seed 1775745370, SHA-256 `dba290938ae177b3018a02ec9fd3db58b15afb8fb5378eabb1ae2465a7157ad5`, was exercised on its saved version 3 layout. The approved RK allowlist, exclusions and loot selector are unchanged. The prior 10,000-population result remains historical.

The DOA guard narrows the frozen procedural constants exception to version 2. Its mutation test rejects both a new version 1 alias and widening the exception to version 3. All 17 mutation cases passed. The authoritative GameData comparison reported zero introduced findings and eight inherited open findings.

## Files changed

| File | Purpose |
| --- | --- |
| `AORebirth/Server/ZoneEngine_New/Core/Inventory/Dat/ItemsDatReader.cs` | Validate the existing item stream and retain decode context. |
| `AORebirth/Server/ZoneEngine_New/Core/Inventory/ItemTemplateCatalog.cs` | Reject an invalid required catalog before publication. |
| `AORebirth/Server/ZoneEngine_New/Program.cs` | Validate required catalogs before service registration and avoid creating the dungeon service during failure cleanup. |
| `AORebirth/Server/ZoneEngine_New/Core/Quests/Dungeons/DungeonLayoutGenerator.cs` | Reserve the frozen procedural route for version 2. |
| `Tools/NewEngineRuntimeContentGuard/DoaRules.cs` and `DoaMutationTests.cs` | Enforce the narrower version boundary. |
| `AORebirth/Server/ZoneEngine_New.Tests/ItemsDatCompressionCompatibilityTests.cs` | Exercise valid, missing, empty and malformed data and a genuine repeated stat key. |
| `AORebirth/Server/ZoneEngine_New.Tests/ProceduralDungeonFoundationTests.cs` and `HistoricalMissionV1Replay.cs` | Check exact captured layouts, saved binding mismatches and the seed-only route. |
| `AORebirth/Server/ZoneEngine_New.Tests/ZoneEngine_New.Tests.csproj` | Include the focused item, binding and key checks. |

## Original-client handoff

The exact Windows candidate is staged separately with the verified unchanged Login, Chat and World artifacts. It has a test-only loopback database and two level-60 Omni test characters by the real PF 710 mission terminal. The original handoff's five measured binary hashes were rechecked against its earlier receipt. Candidate and original databases retain distinct owners and ports. The candidate's verified handoff receipt SHA-256 is `0e428f728b006a55eb08d00b38a8d4abe7f73772e30053af174e3a311ca1945e`.

In the candidate worktree, load `.local\startup-candidate\environment.cmd` in Command Prompt. Run `cmd /d /c stop-engines.cmd -CoreOnly -IdentifyOnly` and proceed when no other AORebirth core engine is identified. Use `cmd /d /c start-engines.cmd -StartupTimeoutSeconds 120`, then `cmd /d /c status-engines.cmd --core`. The test database is already available. If stopped, start the dedicated container with `docker start aorebirth-retail-mission-startup-control`.

Mike can connect manually to `127.0.0.1:27500` with the existing client proxy. In a separate Command Prompt in the original client folder, use `set "AO_REBIRTH_LOGINKEY_PATCH=Always"`, then `AnarchyOnline.exe IA127.0.0.1 IP27500 DUlocal`. The ignored `client-accounts.json` in the candidate's local handoff folder contains the test credentials. Recheck the terminal, keys, entry, each objective, chests and reconnect before accepting these inputs. Stop the test engines with `cmd /d /c stop-engines.cmd -CoreOnly`. Engine listeners were closed at handoff.

- `ZONE_STARTUP_RELIABILITY=FAIL`
- `STARTUP_ROOT_CAUSE=UNRESOLVED`
- `VERSION1_GEOMETRY=PARTIAL`
- `VERSION2_RECONSTRUCTION=PASS`
- `VERSION3_RECONSTRUCTION=PASS`
- `QL125_CAVE_FIXTURE=PASS`
- `DOA_ENFORCEMENT=PASS`
- `ORIGINAL_CLIENT_ACCEPTANCE=PENDING_MIKE`
- `MASTER_INTEGRATION_READY=NO`
