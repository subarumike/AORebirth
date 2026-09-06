# ACGEntrance registry and existing mission destination reconstruction

## Outcome and authority boundary

The complete active **1000026 placement-container corpus** yields **2,242 distinct ACGEntrance identities in 202 explicitly serialized playfields**. All positions, raw rotations and locally sourced names are retained. The external catalog's 2,235 IDs are all present; 2,234 ID/name pairs match exactly. One character differs and seven additional local records exist.

The original 270 offers resolve **270/270** to unique catalog placements by exact playfield and identical local IEEE754 binary32 XYZ. The fixed 93,185-offer corpus resolves **92,830/93,185** under the same raw-packet-backed rule. The remaining **355** have matching normalized coordinates but no retained raw packets and remain unassigned. No nearest-neighbor tolerance, name selection, terminal-ID substitution, or bit-derived identity is used.

**The operational-key crosswalk is not complete.** `Door_t` initializes stat `0xBD` to zero, while registration can allocate a different key in a loaded dungeon. Zero is not a unique positive entrance number. All 2,242 final operational keys remain explicit nulls pending a complete per-placement registration-lifecycle bridge. This does not block the independently proven coordinate route to the placement catalog.

These results identify destination **placements**, not server mission eligibility, weights, room geometry, collision surfaces, interaction radii, or production-ready dynamic-door bindings.

## Repository isolation and provenance

Primary worktree: `C:\Users\Mike\Documents\AORebirth`; branch `master`; starting HEAD and `origin/master`: `cf1e12b894b1247b34f96f832b217c1cfb828213`. Its pre-existing untracked `quest example from PRK.txt` was left alone.

Dedicated worktree: `C:\Users\Mike\Documents\AORebirth-acgentrance-registry-reconstruction`.

Branch: `codex/acgentrance-registry-reconstruction`.

Prior reconciliation: `5a802fe69982b2e7afce998e6c77e54380cbd748`.

Chosen base: `a9da4fc0dee664e43cebdbf5c0a9f2afe51f1e0c`, the tip of `codex/mission-location-capture-reconciliation`, containing that audit plus its external Neko consumer provenance. No master, runtime, DAO, or deployment branch was merged or changed. The final published commit is recorded in the task handoff; it cannot be embedded as its own content hash.

Source roles remain separate:

- `AUTHORITATIVE_EXTERNAL_GAME_CODE_EXTRACT`: [supplied ACGEntrances.json](../reference/missions/external-location-catalog/ACGEntrances.json), SHA-256 `da64734fd544d93c3ccfb2ae56ad4248c18a101b86fed7e0deadc8f315d6c1c8`. Supplied by another project and reportedly extracted from AO game code/client content. It was not originally extracted by AORebirth or this task. The Neko consumer is historical provenance, not the local extractor.
- Official resource/database/binary inputs below: independent local reconstruction authority.
- Existing captures: observations of server output, not the source of the client location catalog.

The earlier [reconciliation report](MISSION_LOCATION_CATALOG_RECONCILIATION.md) and its artifacts were not rewritten. Its negative blind-ID-scan result remains valid: catalog-ID hits were request-terminal identities, not destination identities.

## Exact client inputs

Client root: `C:\Users\Mike\Documents\AO stripdown\Anarchy Online`. Database root: `cd_image\data\db` beneath it. These paths were read-only references under Mike's task-specific authorization.

Client label read from `version.id`: **18.8.62_EP1**. The three PE files are x86 PE32, image base **0x10000000**. They have no retrieved product/file version resource; the build label is not misrepresented as an embedded DLL version.

| Input | Bytes | SHA-256 |
| --- | ---: | --- |
| Gamecode.dll | 3,035,136 | `654969a6b65946cb161f0e60aed8589260fc5eca1795488f66bb56f8fff73726` |
| N3.dll | 385,024 | `8c019efd72d547879a06585b69147ab1546b9617a2fce090e5863791aec8b0bb` |
| GameData.dll | 205,312 | `7b7d4a44a9bcbbd771507332e3641bbfaf0f80f2a4ff2335c6757f6653f870e3` |
| ResourceDatabase.dat | 1,073,741,824 | `3cabdede7b9b2468ed22f10f536fb2f7083ea05ed9483e2d96b22cf080d736a6` |
| ResourceDatabase.dat.001 | 1,073,741,824 | `f8884a2c382ce7c95f20b4423567f176ed40675ba9ce8362527288712871ba73` |
| ResourceDatabase.dat.002 | 142,116,570 | `2024021f966c3c8a8c083e01cbad2335ba33c19a1661a148060391755a608cc1` |
| ResourceDatabase.idx | 9,437,184 | `ba152f59096d5358f4d1b6511d3a3d264999e0a59f1ab7bf3a7cc18a4888c273` |

The [source manifest](../generated/missions/acgentrance-reconstruction/acgentrance-source-manifest.json) records exact paths, full hashes, architecture, image base, version metadata and private Ghidra programs. Main inputs were hashed before analysis and rechecked afterward. Supplemental prior evidence and inspected client DLLs have separate before/after hash ledgers; the manifest explicitly identifies that their baseline was taken during the investigation rather than before its first command.

Existing copied Ghidra projects were newer than the available analyzer and could not be opened. Fresh exact-binary imports were created instead with **Ghidra 12.1.3 PUBLIC**, in private worktree projects `AcgFreshGamecodedll`, `AcgFreshN3dll`, `AcgFreshGameDatadll`; programs retain the original DLL names. Java home, temporary files, headless logs and writable projects are confined to `tools-temp/acgentrance-analysis`. No original Ghidra project was analyzed or updated in place. Private databases, client binaries and the original large decompiler dump are not committed.

## Function map and native findings

The [generated function map](../generated/missions/acgentrance-reconstruction/acgentrance-ghidra-function-map.json) preserves each module SHA, base, RVA/address, export where present, proposed name, callers, direct/imported callees, structure offsets, interpretation and exact targeted evidence-file hash. Committed native evidence consists of compact address/assembly-byte windows with xrefs and all call sites. Full decompiler/assembly exports remain private and ignored; their hashes are preserved. Imperfect decompiler prototypes were not treated as authoritative argument lists.

| Module | RVA | Established role |
| --- | --- | --- |
| Gamecode | `0x12231E` | CreateRDBDynels: complete serialized identity, template creation, overrides, placement transform, Door_t linking |
| N3 | `0x5494`, `0x3E99` | Complete dynel-identity lookup and comparison |
| Gamecode | `0x152559`, `0xD213`, `0x7F5E0` | Register type DAC6 creator; construct Door_t |
| Gamecode | `0x7F113` | Door construction explicitly initializes stat BD to zero |
| Gamecode | `0x3D90`, `0x2E62B` | Embedded StatHolder getter; mode argument ignored; absent slot sentinel 1234567890 |
| Gamecode | `0x800CB` | Door_t::LinkDoorToRooms; reads BD in mode 2 |
| N3 | `0xD98C`, `0xCA9D` | RegisterEntranceDoor and GetEntranceDoor |
| N3 | `0xE09B`, `0xE1FF` | Registry-owning playfield construction/destruction |
| Gamecode | `0xCB232`, `0xC9F49`, `0xABEA7` | Mission-alternative reader, inherited request header, Quest_t reader |
| Gamecode | `0xACCDF`, `0xACBA0` | Quest action array and action WorldPos parsing |
| GameData | `0xCA52`, `0xC7D2`, `0xC7CE` | WorldPos wire decode, local and world accessors |
| Gamecode | `0x1ABC9`, `0x2065E` | Accepted-quest world-position accessor and mission selection-list accessor |
| N3 | `0xD415`, `0x52AD` | Placement becomes an unchanged playfield-relative position/rotation |

`PROVEN_FROM_MODERN_CLIENT_CODE`: ordinary static placement identities are copied intact and compared as complete values. The code also contains an instanced/generated identity-map path; the result must not be generalized to say that every dynamically created runtime identity always equals a static content ID.

`DERIVED_NUMERICAL_PATTERN`: all 2,242 extracted identities have the top-bit prefix `0xC0000000`, and every low-16 value agrees with the separately serialized playfield. There are no exceptions in this corpus. The middle component is a content-number diagnostic, with gaps; no recovered consumer treats it as a runtime entrance or statel index. These numerical observations never replace serialized fields.

## Complete placement structure and local catalog

The existing `RdbReader` was reused for index/shard access, with added cycle, count, duplicate-key and coverage validation. **460,193 active index entries across 2,125 linked nodes** were audited. All **630 resources of type 1000026** were parsed through their final byte: **12,624 placements**, including **2,242 type-DAC6 placements**. This is a census of active indexed placement records, not deleted/unindexed data or coincidental type-byte occurrences in arbitrary resources.

`0xDAC6` is an embedded dynel/template type, **not** the top-level placement resource type. Parent resource instance is the placement container's playfield; explicit PF is retained independently. Referenced templates are resource type **1000020**, with **11** template instances; all template and override sections needed by these placements are parsed.

| Placement-relative offset | Width | Meaning |
| --- | ---: | --- |
| 0 | 4 | LE UInt32 identity type |
| 4 | 4 | LE UInt32 complete instance |
| 8 | 4 | Preserved version/unknown word |
| 12 | 8 | Preserved coordinate-ownership identity words |
| 20 | 4 | Explicit LE playfield ID |
| 24 | 12 | LE binary32 position XYZ |
| 36 | 16 | Four raw binary32 Quaternion_t components |
| 52 | 4 | Preserved unknown word; not asserted to be a template identity type |
| 56 | 4 | Referenced template resource instance |
| 60 | 4 | Override byte length |
| 64 | variable | Exact override blob |

Each container begins with a LE count. Each record has a preceding LE byte length, exactly `64 + override_length`. Templates use LE content; placement override content uses BE headers/sections. Stat-count encodings are `(n+1)*1009`; names are length-prefixed raw bytes in section21/subtype33. Empty or zero-DWORD tails are recognized, unknown sections remain explicit parser failures, and malformed boundaries are rejected. Raw values and offsets are never discarded.

All 2,242 transforms are retained. No scale is serialized in this structure. Position is AO client **local XYZ**, passed through the native playfield-relative placement path; no Godot conversion is applied. Raw quaternion values are kept without inventing a complete rotation convention or scale.

There are **371 distinct locally sourced names**. **2,236** come from placement overrides; the six PF100 records with no name section use the independently parsed referenced template name `ACG Entrance`. Raw bytes, exact source offsets and source paths are preserved. Latin-1 is a byte-preserving presentation mapping, not a claim that an unverified localization/code-page system has been reconstructed.

## Stat BD, registry scope and door relationships

`PROVEN_FROM_ENTRANCE_REGISTRY`: owner is one loaded **n3Playfield_t object instance**, not a global PF-number namespace. Owner `+0x48` points to a vector of 12-byte `{int32 key, UInt32 type, UInt32 instance}` entries; `+0x4C` is the allocation counter, initialized to1. Keys are appended without duplicate rejection. Lookup returns the first equal key. A miss in a nonempty registry returns its first identity; an empty/null registry returns zero identity. That fallback is **not** used for evidence resolution.

`PROVEN_FROM_MODERN_CLIENT_CODE`: DAC6 is registered to the factory creator that constructs **Door_t**. Its constructor initializes BD to **0**. The raw StatHolder getter's absent-value sentinel is **1234567890**; mode2 is ignored in that getter implementation. These are different facts: absence is not default zero, and default zero is not an allocated entrance key.

No placement override or referenced template in this catalog serializes stat189. Thus direct/inherited/override BD source fields remain null; the separately proven construction default is recorded as0. The complete final value after placement initialization, room linking and possible scripts has not been reproduced for every loaded instance. Accordingly:

- Final effective BD values resolved: **0**; explicitly unresolved: **2,242**.
- Positive operational keys resolved: **0**. No global or PF-only key map is invented.
- Global/PF/loaded-owner operational-key collisions: **not evaluable**, not “proven no collisions.” The actual client permits duplicate entries; offline duplicate detection is tested with explicitly scoped fixtures.
- On a non-dungeon path, zero does not register an entrance. A dungeon boundary path can allocate a key and write it back; this depends on room linkage and loaded registry order. Not every static ACGEntrance must possess a unique operational registration key.

`SYMBOLIC_NAME_SUPPORTED`: repository enums call189 `ExitInstance` (`StatIds.cs:792`, `StatNamesDefaults.cs:261`). This task did not establish an original-client enum-name symbol, so neutral BD/key fields remain in use.

Factory class relationship is proven; **no distinct per-placement physical door, room, building or statel association is promoted**. Each row retains `PLACEMENT_ONLY`, null link fields and null radius. Exact counts of those promoted links are all0. The dynel's opaque ID is not an index into a statel file.

## External reconciliation and PF505 anchors

| Comparison | Count |
| --- | ---: |
| External memberships / unique IDs | 2,235 / 2,235 |
| Local rows / unique identities | 2,242 / 2,242 |
| Exact ID intersection | 2,235 |
| Exact ID and exact name | 2,234 |
| External-only | 0 |
| Local-only | 7 |
| Case-only / whitespace-only differences | 0 / 0 |
| Byte-encoding/display-character difference | 1 |
| Substantive differences | 0 |
| External/local duplicate IDs | 0 / 0 |
| Unresolved local names | 0 |

The differing identity is `0xC0000280`, PF640: local raw name begins byte`C6`, preserved as `Ænima HQ`; external name is `?nima HQ`. Replacing high bytes with ASCII `?` reproduces that display difference, but the exact external conversion mechanism is unknown. Neither source is silently normalized.

Local-only: six PF100 identities `0xC0000064` through `0xC0050064`, and PF105 `0xC0000069` (`Alien Mothership`). Their external absence cannot be attributed to version differences without an external client fingerprint.

All **29 PF505 records**, including exact names, coordinates, offsets and unresolved keys, are listed in the [generated PF505 table](../generated/missions/acgentrance-reconstruction/acgentrance-pf505.md) and fixture. Anchor tests prove Central Desert Den `0xC00001F9` at`0x111F0E0A`, South Desert Den `0xC00101F9` at`0x111F0EC3`, and Mantis Hive `0xC01201F9` at`0x111F13D4`, each with separately serialized PF505. Components span0..33, missing23,27,28,30,32.

Repeated names remain separate rows: **120** name groups contain multiple identities, **152** same-name/same-PF groups and **45** names spanning multiple PFs. No exact same-PF coordinate duplicate exists in this extraction. Names never determine identity.

## Actual mission destination representation and exact matching rule

`PROVEN_FROM_PACKET_PARSER` and `PROVEN_FROM_MODERN_CLIENT_CODE`: message **QuestAlternative 0x5C436609** is decoded by Gamecode RVA`0xCB232`. Its inherited header includes the request terminal, followed by a byte offer count. It reads each complete mission identity and Quest_t via`0xABEA7`; the action-array path`0xACCDF -> 0xACBA0` reads a **WorldPos_c** at action object`+0x6C`.

WorldPos wire record, all BE:

| Relative to WorldPos start D | Type | Meaning |
| --- | --- | --- |
| D+0 | 2 UInt32 | Explicit playfield Identity_t |
| D+8 | 2 signed int32 | X/Z integer components used in global-world conversion; previously `UnkChunk5` |
| D+16 | 3 binary32 | Raw local XYZ |

GameData RVA`0xCA52` stores localXYZ at WorldPos`+0x14` and separately computes worldXYZ at`+8` by adding `(intX,0,intZ)` and storing binary32. The two integers are **not demonstrated entrance keys**. The previous “low/high entrance ID” style guess is not promoted.

The request-terminal identity is at complete-packet offset42; the first offer starts at51. Variable offer boundaries, WorldPos D, action start, description offsets and exact raw packet hashes are retained per offer. The coordinate field verifier checks the complete cohort end, all exposed numeric fields, byte-exact opaque AOSharp spans, action-array count, description bytes, and all local float bits. It is honestly classified **capture-schema-assisted**: opaque section lengths come from the retained AOSharp normalization. It is not presented as a new fully general Quest_t parser.

No destination BD field or complete ACGEntrance ID field was established in the mission representation. The client does supply enough **explicit PF and local coordinates** for this catalog reconciliation. The prior catalog-ID matches remain the **terminal** value, e.g. header instance`0xC000028F` in the level2 fixture, not the entrance selected for that offer.

Exact rule: compare explicit PF and **all three local binary32 components**, with identity XYZ axes and **zero tolerance**. Both native paths use local AO Vector3 coordinates; the separate global WorldPos vector is not substituted. This yields exactly one catalog placement for every normalized offer, corroborated by 92,830 raw byte verifications. It resolves the advertised placement identity, not an operational door key or physical collision plane. No arbitrary radius or nearest entrance is used.

Description-name support is diagnostic only: 15,199 coordinate-selected names also occur among the prior description candidates. Another 1,782 candidate-bearing descriptions do not contain the selected exact name; examples include generic `building` text for a location named `a house` or `a ruined factory`. These are why text-substring candidates are not exact destination-name fields.

GetEntranceDoor has no local code caller in the recovered N3 xrefs and no import in the inspected client DLL inventory. The traced mission/action/WorldPos paths and accepted-quest accessor do not call it. This is bounded static negative coverage, **not** a claim that arbitrary dynamic lookup or every uninspected executable has been ruled out. Mission destination placement resolution here does not depend on that registry.

## Exact existing-corpus reprocessing

The prior source manifest fixes **77 sessions**. No newly discovered session was added. Every retained event source and inbound hash was checked against the prior evidence, and every offer retained session/request/cohort/index/source-line linkage. Primary validation reused the established level2 validator: **27 slider states, 54 requests, 270 offers, five offers per cohort**. No new capture was requested or performed.

Some prior text-artifact digest entries correspond exactly to CRLF bytes while the committed files are LF. The new [prior-hash ledger](../generated/missions/acgentrance-reconstruction/prior-artifact-hash-verification.json) records actual hashes and the exact reversible newline equivalence; it does not pretend the byte hashes are equal or rewrite history. Gzip corpus hashes and raw source/packet hashes are verified exactly.

| Coverage/result | Original level2 | Fixed full corpus |
| --- | ---: | ---: |
| Offers | 270 | 93,185 |
| Raw inbound present and destination decoded | 270 | 92,830 |
| Explicit PF / normalized coordinates | 270 | 93,185 |
| Slider metadata / character-level metadata | 270 | 92,830 |
| Description contains prior catalog-name candidate | 106 | 16,981 |
| Proven exact destination-name wire field | 0 | 0 |
| Proven operational key | 0 | 0 |
| EXACT_PLAYFIELD_COORDINATE_MATCH | **270** | **92,830** |
| Strong-only assignments | 0 | 0 |
| Ambiguous assignments | 0 | 0 |
| Unresolved: RAW_PACKET_MISSING | 0 | **355** |
| Other decoder failures | 0 | 0 |

The 355 missing-raw offers retain their unique normalized-coordinate candidate as a diagnostic, with `resolved_acgentrance_identity: null`. They are not mixed into exact assignments. Recovering their original packet bytes would be necessary to promote them under this gate; new rolls would be different offers and would not fill that historical gap.

## Artifacts, tests and reproducibility

New tooling: `Tools/acgentrance_reconstruction.cmd`, `acgentrance_reconstruction.py`, `acgentrance_artifacts.py`, `acgentrance_evidence.py`, `acgentrance_mission_decoder.py`, `test_acgentrance_reconstruction.py`, and `Tools/acgentrance/ExportAcgEvidence.java`. Existing runtime code and old reconciliation tooling were not modified.

All generated outputs are under `docs/generated/missions/acgentrance-reconstruction`. The [evidence manifest](../generated/missions/acgentrance-reconstruction/mission-location-evidence-manifest.json) inventories exact tool, input, native-evidence and generated-artifact hashes. Major outputs include source/function/layout/scope maps; full records JSONL and CSV; template data; per-row BD, key, coordinate and link projections; exact external comparison; PF505 fixtures; per-session coverage; packet field map; level2 and full-corpus reconciliation; unresolved rows; and summary. Full-corpus/unresolved JSONL uses deterministic gzip to keep evidence compact. Exploratory initial output and private Ghidra projects are ignored, not committed.

Commands, run from the dedicated worktree:

```bat
Tools\acgentrance_reconstruction.cmd sources
Tools\acgentrance_reconstruction.cmd references
Tools\acgentrance_reconstruction.cmd generate
Tools\acgentrance_reconstruction.cmd test
Tools\acgentrance_reconstruction.cmd generate --check
Tools\acgentrance_reconstruction.cmd sources --verify
Tools\acgentrance_reconstruction.cmd references --verify
```

The wrapper uses the repository Python runtime selector. No internet, SQL, client session or external-project executable is required for extraction/reconciliation/tests. Native regeneration optionally uses the locally installed Ghidra/Java toolchain in private projects; exact program SHA is checked before accepting evidence.

Regression coverage: full UInt32/signed diagnostic preservation; explicit PF independent of low bits; all PF505 anchors and gaps; malformed/truncated containers and unknown optional fields; duplicate identities and index cycles; direct/inherited/override/conflicting/missing stat inputs without invented final values; scoped key collisions; case/space/encoding preservation; transform roundtrip; signed WorldPos offsets and float32 conversion; five-offer/raw-boundary/terminal separation; tampered coordinates; unique versus ambiguous coordinate candidates; missing raw and nearest-neighbor rejection; exact270/93,185 counts; and stale-artifact rejection without writes. Full generation followed by byte-comparison check covers deterministic artifact output and complete input provenance.

Validation status and final commit/push SHA are recorded in the task handoff. Intermediate parser/test failures were corrected before the final gates; the copied-project Ghidra incompatibility is retained above rather than concealed as original-project analysis. Early investigation command/encoding errors changed no source input and are not project blockers.

## Remaining risks and direct disposition

Implementation-ready **as evidence tooling**: indexed ACGEntrance identity/name/PF/local-transform catalog, the WorldPos local/global distinction, exact fixed-corpus placement reconciliation, and deterministic validation gates.

Evidence-only / unresolved: final effective BD and operational registry entry for each loaded placement; instance-scoped registry allocation order; room/statel/building/physical-door links and radii; full quaternion/scale conventions; complete general Quest_t parsing independent of captured opaque spans; dynamic GetEntranceDoor consumers; exact external encoding/version explanation; raw bytes for 355 historical offers; server destination eligibility/weighting. No request to change runtime tables or gameplay follows from these results.

Strongest remaining static leads are the template-factory initialization path, Door_t initialization/room-link call chain and loaded scene registry lifecycle. Additional live rolling is **not required** to reproduce this catalog or resolve the retained raw-backed corpus, and would not recover missing historical packets.

`LIVE_MISSION_CAPTURE_PERFORMED: NO`

`RUNTIME_MISSION_LOGIC_CHANGED: NO`

`PRODUCTION_DESTINATION_DATA_CHANGED: NO`

`EXTERNAL_SOURCE_MISREPRESENTED: NO`

`SOURCE_INDEPENDENTLY_REPRODUCED: PARTIAL`
