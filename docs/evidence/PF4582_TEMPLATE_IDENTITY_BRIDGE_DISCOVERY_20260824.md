# PF4582 Official Template Identity Bridge Discovery

Primary outcome: `NO_BRIDGE_LOCATED`.

This evidence-only audit activates no PF4582 placement. Printable hash bytes, names, coordinates, levels, MonsterData, and third-party material remain non-authoritative unless an official source-key join reaches a terminal identity.

## Required metrics

```text
PF4582_BRIDGE_OUTCOME=NO_BRIDGE_LOCATED
PF4582_SOURCE_PLACEMENTS=206
PF4582_TEMPLATE_KEYS_TOTAL=38
PF4582_TEMPLATE_KEYS_ROUNDTRIP=38
PF4582_SOURCE_NPC_IDS=206
PF4582_SOURCE_PROVENANCE_CLASS=OFFICIAL_RESOURCE_EXTRACT
PF4582_ORIGINAL_SOURCE_INPUT_FOUND=YES
PF4582_EXTRACTION_TOOL_FOUND=NO
PF4582_TEMPLATE_FIELD_OFFICIAL_NAME_PROVEN=NO
PF4582_TEMPLATE_FIELD_SEMANTICS=OFFICIAL_ACGHASH_TEMPLATE_KEY_UINT32_TERMINAL_LOOKUP_UNPROVEN
PF4582_TEMPLATE_FIELD_BYTE_ORDER=BIG_ENDIAN_UINT32_RESOURCE_BYTES_LITTLE_ENDIAN_ASCII_IS_DISPLAY_ONLY
PF4582_NPCID_SEMANTICS=ABSENT_FROM_OFFICIAL_PF4582_RESOURCE_WINDOWS_UNPROVEN_EXTRACTOR_OR_SOURCE_KEY
PF4582_OFFICIAL_SOURCE_FILES_INSPECTED=40
PF4582_OFFICIAL_SOURCE_BUILDS_INSPECTED=2
PF4582_STRUCTURAL_KEY_OCCURRENCES=412
PF4582_FALSE_POSITIVE_OCCURRENCES_REJECTED=1216
PF4582_STATIC_PARSER_FOUND=YES
PF4582_STATIC_LOOKUP_CONSUMER_FOUND=NO
PF4582_STATIC_TERMINAL_IDENTITY_FOUND=NO
PF4582_STATIC_BRIDGED_HASHES=0
PF4582_STATIC_BRIDGED_NPC_IDS=0
PF4582_STATIC_BASELINE_MATCH=0
PF4582_STATIC_BASELINE_PARTIAL=14
PF4582_STATIC_BASELINE_CONFLICT=0
PF4582_STATIC_BASELINE_NOT_REACHED=0
PF4582_RUNTIME_TEMPLATE_FIELD_FOUND=YES
PF4582_RUNTIME_NPCID_FIELD_FOUND=NO
PF4582_RUNTIME_DYNEL_JOIN_FOUND=NO
PF4582_RUNTIME_CAPTURE_IMPLEMENTED=NO
PF4582_RUNTIME_CAPTURE_READY=NO
PF4582_RUNTIME_CAPTURE_LIVE_VALIDATED=NO
PF4582_NEW_DIRECT_HASH_BRIDGES=0
PF4582_NEW_DIRECT_NPCID_BRIDGES=0
PF4582_NEWLY_PROVEN_PROFILE_IDENTITIES=0
PF4582_SAME_HASH_PROPAGATION_PROVEN=NO
PF4582_ISRE_BLOCKED_PROPAGATION_PROVEN=NO
PF4582_RUNTIME_ACTIVE_BEFORE=25
PF4582_RUNTIME_ACTIVE_AFTER=25
PF4582_RUNTIME_BLOCKED_BEFORE=181
PF4582_RUNTIME_BLOCKED_AFTER=181
PF4582_RUNTIME_ACTIVATION_CHANGED=NO
```

## Source provenance

Classification: `OFFICIAL_RESOURCE_EXTRACT`.

The supplied JSON is a derived official-resource extract with field-level limits: both official EP1 and EP2 PF4582 resource windows reproduce the exact 206-value TemplateHash multiset and all 38 multiplicities, and the official parser exposes the matching hash-spawn placement structure. Variable serialized sections prevent claiming a flat fixed-record table. The exact exporter, its version, the JSON field labels, source names, and NpcId lineage remain unavailable.

Delivered dataset SHA-256: `b747aea145cb36e3f9be5b2cacc7aaebca3d24017a14540ac1f29f4bd1296b32`.

## Strongest bounded field semantics

The official resource stores the exact JSON numeric value as a big-endian uint32 byte pattern, and GameData parses the corresponding spawn value into a one-uint32 ACGHash_t. The exposed official API calls it a hash, parser text calls it a template, and GetHashAsText can render it as text; neither official build exposes a field named TemplateHash. No inspected consumer joins the value to a stable mob template, MonsterData, or live identity. All 206 supplied NpcIds are absent from both bounded PF4582 resource windows and no NpcId field exists in the inspected HashSpawnPoint layout.

The numeric representation remains exactly reversible: the unsigned integer and eight-digit integer hex are serialized as four little-endian bytes; those bytes are also displayed as four printable characters. That display is not itself semantic proof.

## Official sources inspected

| Logical source | Build | Media | Size | SHA-256 | Structural | Rejected |
|---|---|---|---:|---|---:|---:|
| AO_CLIENT_EP1_ANARCHYONLINE_EXE | 18.8.62_EP1 | INSTALLED_OFFICIAL_CLIENT | 85936 | `370c0670cc9cb46626ef24692376aaf492bb1787bad8a1125365a6be4f663862` | 0 | 0 |
| AO_CLIENT_EP1_BINARYSTREAM_DLL | 18.8.62_EP1 | INSTALLED_OFFICIAL_CLIENT | 15360 | `ae2b6b93effecb892e515afb967c034ef319a5a9aea687a14a95eb9f8700eb2d` | 0 | 0 |
| AO_CLIENT_EP1_GAMECODE_DLL | 18.8.62_EP1 | INSTALLED_OFFICIAL_CLIENT | 3035136 | `654969a6b65946cb161f0e60aed8589260fc5eca1795488f66bb56f8fff73726` | 0 | 0 |
| AO_CLIENT_EP1_GAMEDATA_DLL | 18.8.62_EP1 | INSTALLED_OFFICIAL_CLIENT | 205312 | `7b7d4a44a9bcbbd771507332e3641bbfaf0f80f2a4ff2335c6757f6653f870e3` | 0 | 0 |
| AO_CLIENT_EP1_RDB_BASE | 18.8.62_EP1 | INSTALLED_OFFICIAL_RESOURCE_DATABASE | 1073741824 | `fe480aaf552b77100337a7d0fc7d5d2686cc598d688543a50699e560e4cd1ba0` | 206 | 40 |
| AO_CLIENT_EP1_RDB_INDEX | 18.8.62_EP1 | INSTALLED_OFFICIAL_RESOURCE_DATABASE | 9437184 | `498ce5e038567580ed683275508c3fad6b7d18d4c660ce457769aa2f92f217ee` | 0 | 0 |
| AO_CLIENT_EP1_VERSION_ID | 18.8.62_EP1 | INSTALLED_OFFICIAL_CLIENT | 13 | `e321d2adb1ff92b886d6dc57e66c7e928459221197c74596d0979566635eb8fa` | 0 | 0 |
| AO_CLIENT_EP2_ANARCHYONLINE_EXE | 18.8.62_EP2 | INSTALLED_OFFICIAL_CLIENT | 86448 | `20aa1daa31de191cc5498cef34f6a95df8667d2c0a01b9212888f71882e3d387` | 0 | 0 |
| AO_CLIENT_EP2_BINARYSTREAM_DLL | 18.8.62_EP2 | INSTALLED_OFFICIAL_CLIENT | 15872 | `fca5131ae23d538bef37a3a0656893620143731f4874336e41256c28f2a3b5f1` | 0 | 0 |
| AO_CLIENT_EP2_GAMECODE_DLL | 18.8.62_EP2 | INSTALLED_OFFICIAL_CLIENT | 3057664 | `60e5c2073fd488ec01579cd23ba7c87e3881228815ec037954d5ce3dbf64b5b4` | 0 | 0 |
| AO_CLIENT_EP2_GAMEDATA_DLL | 18.8.62_EP2 | INSTALLED_OFFICIAL_CLIENT | 205312 | `feb26481fe8555fddbecb2ee6eef49cd7a18940c08ea33a3190ea68ac1f05909` | 0 | 0 |
| AO_CLIENT_EP2_N3_DLL | 18.8.62_EP2 | INSTALLED_OFFICIAL_CLIENT | 406528 | `e242f4855de93094161b619047cd838b6a3261bb53a5eb17065f60eda5239168` | 0 | 0 |
| AO_CLIENT_EP2_RDB_INDEX | 18.8.62_EP2 | INSTALLED_OFFICIAL_RESOURCE_DATABASE | 16777216 | `6ab5f9747bd82840a6562f1f03d44faece899f7be1c3552aa7c1e646b07892d9` | 0 | 0 |
| AO_CLIENT_EP2_RDB_SEGMENT_009 | 18.8.62_EP2 | INSTALLED_OFFICIAL_RESOURCE_DATABASE | 1073741824 | `d93e794944ce046214c756c25a1378164b580b8eb0e8070451dbfc8b27bb0ac6` | 206 | 1176 |
| AO_CLIENT_EP2_VERSION_ID | 18.8.62_EP2 | INSTALLED_OFFICIAL_CLIENT | 13 | `8a250438c64aa6750f3ee2c20731400a58c713cd424fbd17b7fb6ff3e761ace2` | 0 | 0 |

## Official parser and consumer trace

The official source record and parser are proven. The chain stops at ACGHash_t because no inspected official consumer uses that field to select a stable mob template or identity.

```text
official EP1 PF4582 variable serialized hash-spawn resource
-> GameData::PlayfieldDistrictInfo_t / DistrictData_t
-> GameData::HashSpawnPoint_t::ReadBlob
-> HashSpawnPoint_t ACGHash_t
-> NO IDENTIFIED LOOKUP CONSUMER
-> NO TERMINAL STABLE AO IDENTITY
```

## Runtime bridge trace

The source hash is retained in district data and a separate RDB-dynel constructor reaches an identity, but the official client exposes no proven call or object that contains both. Instrumenting both streams would require coordinate/order inference and is therefore not capture-ready.

```text
PlayfieldAnarchy_t district data at +0xB8
-> HashSpawnPoint_t ACGHash_t at +0x24
-> NO CALL TO CreateFromTemplate
separate RDBDynelLoader_t collection at +0xC8
-> CreateRDBDynels / CreateFromTemplate
-> live dynel identity
NO SAME-CONTEXT JOIN BETWEEN THE TWO CHAINS
```

## Baseline controls

All 14 governed baseline keys are `STATIC_BASELINE_PARTIAL`: their official PF4582 source records are reproduced, but no terminal official identity was available to match or conflict with the AORebirth profile.

## Same-hash propagation

No hash receives global or PF4582-wide propagation. The official record proves repeated tags and per-spawn overrides, but no terminal lookup proves that a tag always selects one identity or that scripts cannot substitute a variant. Existing baseline mappings remain NpcId-specific; dynamic names remain DYNAMIC_OR_VARIANT; the ten blocked ISRE placements remain unproved.

## Per-hash result

| UInt32 | Hex | LE bytes | Tag | Placements | Prior | Direct | Propagation |
|---:|---|---|---|---:|---|---|---|
| 1095584067 | `0x414D4943` | `43 49 4D 41` | `CIMA` | 13 | CANDIDATE | NO_BRIDGE | PROPAGATION_UNPROVEN |
| 1095979092 | `0x41535054` | `54 50 53 41` | `TPSA` | 16 | CANDIDATE | NO_BRIDGE | PROPAGATION_UNPROVEN |
| 1096042831 | `0x4154494F` | `4F 49 54 41` | `OITA` | 1 | BASELINE_PROVEN | NO_BRIDGE | NPCID_SPECIFIC |
| 1146375747 | `0x44544E43` | `43 4E 54 44` | `CNTD` | 1 | BASELINE_PROVEN | NO_BRIDGE | NPCID_SPECIFIC |
| 1163019598 | `0x4552454E` | `4E 45 52 45` | `NERE` | 1 | BASELINE_PROVEN | NO_BRIDGE | NPCID_SPECIFIC |
| 1163021903 | `0x45524E4F` | `4F 4E 52 45` | `ONRE` | 1 | BASELINE_PROVEN | NO_BRIDGE | NPCID_SPECIFIC |
| 1163023177 | `0x45525349` | `49 53 52 45` | `ISRE` | 11 | BASELINE_PROVEN | NO_BRIDGE | NPCID_SPECIFIC |
| 1163284805 | `0x45565145` | `45 51 56 45` | `EQVE` | 1 | BASELINE_PROVEN | NO_BRIDGE | NPCID_SPECIFIC |
| 1178682433 | `0x46414441` | `41 44 41 46` | `ADAF` | 1 | BASELINE_PROVEN | NO_BRIDGE | NPCID_SPECIFIC |
| 1196247123 | `0x474D4853` | `53 48 4D 47` | `SHMG` | 1 | BASELINE_PROVEN | NO_BRIDGE | NPCID_SPECIFIC |
| 1196249922 | `0x474D5342` | `42 53 4D 47` | `BSMG` | 1 | NO_EVIDENCE | NO_BRIDGE | DYNAMIC_OR_VARIANT |
| 1229079369 | `0x49424349` | `49 43 42 49` | `ICBI` | 1 | BASELINE_PROVEN | NO_BRIDGE | NPCID_SPECIFIC |
| 1229343811 | `0x49464C43` | `43 4C 46 49` | `CLFI` | 1 | BASELINE_PROVEN | NO_BRIDGE | NPCID_SPECIFIC |
| 1230127939 | `0x49524343` | `43 43 52 49` | `CCRI` | 1 | BASELINE_PROVEN | NO_BRIDGE | NPCID_SPECIFIC |
| 1230327119 | `0x49554D4F` | `4F 4D 55 49` | `OMUI` | 1 | BASELINE_PROVEN | NO_BRIDGE | NPCID_SPECIFIC |
| 1230522714 | `0x4958495A` | `5A 49 58 49` | `ZIXI` | 26 | CANDIDATE | NO_BRIDGE | PROPAGATION_UNPROVEN |
| 1246118721 | `0x4A464341` | `41 43 46 4A` | `ACFJ` | 23 | CANDIDATE | NO_BRIDGE | PROPAGATION_UNPROVEN |
| 1262571596 | `0x4B41504C` | `4C 50 41 4B` | `LPAK` | 2 | CANDIDATE | NO_BRIDGE | PROPAGATION_UNPROVEN |
| 1263749447 | `0x4B534947` | `47 49 53 4B` | `GISK` | 10 | CANDIDATE | NO_BRIDGE | PROPAGATION_UNPROVEN |
| 1263751763 | `0x4B535253` | `53 52 53 4B` | `SRSK` | 3 | CANDIDATE | NO_BRIDGE | PROPAGATION_UNPROVEN |
| 1280132162 | `0x4C4D4442` | `42 44 4D 4C` | `BDML` | 1 | NO_EVIDENCE | NO_BRIDGE | DYNAMIC_OR_VARIANT |
| 1280132418 | `0x4C4D4542` | `42 45 4D 4C` | `BEML` | 1 | NO_EVIDENCE | NO_BRIDGE | DYNAMIC_OR_VARIANT |
| 1280462675 | `0x4C524F53` | `53 4F 52 4C` | `SORL` | 5 | CANDIDATE | NO_BRIDGE | PROPAGATION_UNPROVEN |
| 1280525906 | `0x4C534652` | `52 46 53 4C` | `RFSL` | 7 | CANDIDATE | NO_BRIDGE | PROPAGATION_UNPROVEN |
| 1296911426 | `0x4D4D4C42` | `42 4C 4D 4D` | `BLMM` | 1 | AMBIGUOUS | NO_BRIDGE | DYNAMIC_OR_VARIANT |
| 1314079299 | `0x4E534243` | `43 42 53 4E` | `CBSN` | 7 | CANDIDATE | NO_BRIDGE | PROPAGATION_UNPROVEN |
| 1329812567 | `0x4F435457` | `57 54 43 4F` | `WTCO` | 10 | CANDIDATE | NO_BRIDGE | PROPAGATION_UNPROVEN |
| 1330463810 | `0x4F4D4442` | `42 44 4D 4F` | `BDMO` | 1 | NO_EVIDENCE | NO_BRIDGE | DYNAMIC_OR_VARIANT |
| 1330467906 | `0x4F4D5442` | `42 54 4D 4F` | `BTMO` | 1 | NO_EVIDENCE | NO_BRIDGE | DYNAMIC_OR_VARIANT |
| 1330725958 | `0x4F514446` | `46 44 51 4F` | `FDQO` | 9 | CANDIDATE | NO_BRIDGE | PROPAGATION_UNPROVEN |
| 1330925122 | `0x4F544E42` | `42 4E 54 4F` | `BNTO` | 1 | BASELINE_PROVEN | NO_BRIDGE | NPCID_SPECIFIC |
| 1380204867 | `0x52444143` | `43 41 44 52` | `CADR` | 10 | CANDIDATE | NO_BRIDGE | PROPAGATION_UNPROVEN |
| 1380273228 | `0x52454C4C` | `4C 4C 45 52` | `LLER` | 5 | CANDIDATE | NO_BRIDGE | PROPAGATION_UNPROVEN |
| 1380796994 | `0x524D4A42` | `42 4A 4D 52` | `BJMR` | 1 | NO_EVIDENCE | NO_BRIDGE | DYNAMIC_OR_VARIANT |
| 1414742857 | `0x54534349` | `49 43 53 54` | `ICST` | 12 | BASELINE_PROVEN | NO_BRIDGE | NPCID_SPECIFIC |
| 1430934083 | `0x554A5243` | `43 52 4A 55` | `CRJU` | 8 | CANDIDATE | NO_BRIDGE | PROPAGATION_UNPROVEN |
| 1497649731 | `0x59445243` | `43 52 44 59` | `CRDY` | 1 | CANDIDATE | NO_BRIDGE | PROPAGATION_UNPROVEN |
| 1514951251 | `0x5A4C5253` | `53 52 4C 5A` | `SRLZ` | 9 | CANDIDATE | NO_BRIDGE | PROPAGATION_UNPROVEN |

## Dead ends

- Official exports contain ACGHash_t, GetHash, GetHashAsInt, and GetHashAsText, but no symbol named TemplateHash or NpcId.
- The official PF4582 resource windows and inspected HashSpawnPoint layout contain the hash/template key and placement overrides but no proven NpcId, name, MonsterData, or AO identity.
- No Gamecode consumer imports or calls HashSpawnPoint_t::GetHash, DistrictData_t::GetHashSpawnPoints, or DistrictData_t::GetSpawnInfo.
- The only inspected CreateFromTemplate call consumes RDBDynelLoader_t, which is stored separately from district hash-spawn data.
- Names, coordinates, levels, MonsterData, appearance, and respawn observations do not supply the missing source-key join.
- Third-party resource type hints aided discovery but were not used as authority.

## Exact evidence still required

- Obtain the exact extractor or upstream source that produced PlayfieldDistrictInfo.json, including its mapping of native ACGHash_t and source NpcId.
- Locate official server-side district-spawn realization code or data that consumes ACGHash_t and terminates in a stable template/identity.
- Alternatively identify one fingerprint-gated official runtime callsite that directly correlates HashSpawnPoint_t+0x24 with the resulting Identity_t or MonsterData in the same context.

## Safety and no-promotion invariants

```text
OFFICIAL_FUNCOM_EVIDENCE_REQUIRED=YES
CELL_AO_USED_AS_AUTHORITY=NO
THIRD_PARTY_DATA_USED_AS_AUTHORITY=NO
NAME_ONLY_JOIN_ACCEPTED=NO
COORDINATE_JOIN_ACCEPTED=NO
LEVEL_ONLY_JOIN_ACCEPTED=NO
MONSTERDATA_ONLY_JOIN_ACCEPTED=NO
DYNAMIC_NAME_FORCED_RESOLVED=NO
OFFICIAL_BINARY_MODIFIED=NO
LIVE_CLIENT_STARTED=NO
LIVE_CAPTURE_PERFORMED=NO
PRODUCTION_OPERATION_PERFORMED=NO
DATABASE_OPERATION_PERFORMED=NO
RUNTIME_ACTIVATION_CHANGED=NO
COMMIT_CREATED=NO
PUSH_PERFORMED=NO
```
