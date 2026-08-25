# PF4582 official ACGHash structural bridge discovery

Current outcome: `STRUCTURAL_SOURCE_AND_CONSUMER_FOUND`.

Historical outcome `NO_BRIDGE_LOCATED` is preserved and explicitly superseded because the later official EP1 investigation located the source resource, 207-record `HashSpawnPoint_t` structure, packed `ACGHash_t` field, parser, native storage, vector, and accessors.

The correction is structural only. The terminal mob identity, static mob mappings, and same-context runtime dynel join remain unresolved. This report activates no placement.

## Required metrics

```text
PF4582_PRIOR_BRIDGE_OUTCOME=NO_BRIDGE_LOCATED
PF4582_BRIDGE_OUTCOME=STRUCTURAL_SOURCE_AND_CONSUMER_FOUND
PF4582_PRIOR_OUTCOME_SUPERSEDED=YES
PF4582_SUPERSESSION_REASON=OFFICIAL_EP1_SOURCE_AND_NATIVE_PARSER_CONSUMER_LOCATED
PF4582_OFFICIAL_BUILD=18.8.62_EP1
PF4582_OFFICIAL_RESOURCE_TYPE=1000014
PF4582_OFFICIAL_RESOURCE_INSTANCE=4582
PF4582_OFFICIAL_RESOURCE_RECORDS=207
PF4582_ACCEPTED_SOURCE_RECORDS=206
PF4582_OFFICIAL_ADDITIONAL_RECORDS=1
PF4582_OFFICIAL_STRUCTURAL_SOURCE_PROVEN=YES
PF4582_ACGHASH_OFFICIAL_TYPE_PROVEN=YES
PF4582_ACGHASH_PARSER_CONSUMER_PROVEN=YES
PF4582_TERMINAL_IDENTITY_BRIDGE=UNRESOLVED
PF4582_STATIC_MOB_MAPPINGS_EXTRACTED=0
PF4582_SOURCE_PLACEMENTS=206
PF4582_TEMPLATE_KEYS_TOTAL=38
PF4582_TEMPLATE_KEYS_ROUNDTRIP=38
PF4582_SOURCE_NPC_IDS=206
PF4582_SOURCE_NPCID_STABLE_FOR_AOREBIRTH=YES
PF4582_SOURCE_NPCID_PROVEN_NATIVE_FUNCOM_FIELD=NO
PF4582_TEMPLATE_FIELD_OFFICIAL_NAME_PROVEN=NO
PF4582_STRUCTURAL_KEY_OCCURRENCES=206
PF4582_FALSE_POSITIVE_OCCURRENCES_REJECTED=5039
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

## Official semantics

ACGHash_t is an official packed four-byte scalar/tag, not a cryptographic hash or a proven mob-template, resource, visual, or terminal runtime identity. TemplateHash remains only as a legacy AORebirth field name.

The accepted legacy integer is decoded from little-endian bytes to canonical four-character text. Official wire bytes reverse those canonical bytes, and the official native scalar interprets the wire bytes as little-endian. The accepted integer and official native integer are not compared directly.

## Official parser and native accessor path

The official EP1 parser consumes PF4582 HashSpawnPoint_t and its ACGHash_t field. This is a structural parser/native consumer, not a terminal mob identity resolver.

```text
ResourceDatabase.dat type 1000014 / instance 4582
GameData.dll!PlayfieldDistrictInfo_t::ReadBlob RVA 0x9DEF
GameData.dll!operator>>(DistrictData_t) RVA 0x49BE
GameData.dll!HashSpawnPoint_t::ReadBlob RVA 0x640F
GameData.dll!operator>>(ACGHash_t) RVA 0x1B23
HashSpawnPoint_t parsed ACGHash_t field +0x24
DistrictData_t hash-spawn vector +0x5C
GameData.dll!HashSpawnPoint_t::GetHash RVA 0x4459
GameData.dll!DistrictData_t::GetHashSpawnPoints RVA 0x44F0
terminal mob identity unresolved
```

## Status boundary

```text
OfficialStructuralSourceStatus=PROVEN
OfficialAcgHashTypeStatus=PROVEN
OfficialParserConsumerStatus=PROVEN
TerminalMobIdentityStatus=UNRESOLVED
StaticMobMappingsExtracted=0
RuntimeHashToDynelJoinStatus=UNRESOLVED
```

## Per-key structural result

| Canonical ACGHash | Accepted uint32 | Accepted LE bytes | Official wire | Official native | Placements | Terminal identity |
|---|---:|---|---|---|---:|---|
| `ACFJ` | 1246118721 | `41 43 46 4A` | `4A 46 43 41` | `0x4143464A` | 23 | unresolved |
| `ADAF` | 1178682433 | `41 44 41 46` | `46 41 44 41` | `0x41444146` | 1 | unresolved |
| `BDML` | 1280132162 | `42 44 4D 4C` | `4C 4D 44 42` | `0x42444D4C` | 1 | unresolved |
| `BDMO` | 1330463810 | `42 44 4D 4F` | `4F 4D 44 42` | `0x42444D4F` | 1 | unresolved |
| `BEML` | 1280132418 | `42 45 4D 4C` | `4C 4D 45 42` | `0x42454D4C` | 1 | unresolved |
| `BJMR` | 1380796994 | `42 4A 4D 52` | `52 4D 4A 42` | `0x424A4D52` | 1 | unresolved |
| `BLMM` | 1296911426 | `42 4C 4D 4D` | `4D 4D 4C 42` | `0x424C4D4D` | 1 | unresolved |
| `BNTO` | 1330925122 | `42 4E 54 4F` | `4F 54 4E 42` | `0x424E544F` | 1 | unresolved |
| `BSMG` | 1196249922 | `42 53 4D 47` | `47 4D 53 42` | `0x42534D47` | 1 | unresolved |
| `BTMO` | 1330467906 | `42 54 4D 4F` | `4F 4D 54 42` | `0x42544D4F` | 1 | unresolved |
| `CADR` | 1380204867 | `43 41 44 52` | `52 44 41 43` | `0x43414452` | 10 | unresolved |
| `CBSN` | 1314079299 | `43 42 53 4E` | `4E 53 42 43` | `0x4342534E` | 7 | unresolved |
| `CCRI` | 1230127939 | `43 43 52 49` | `49 52 43 43` | `0x43435249` | 1 | unresolved |
| `CIMA` | 1095584067 | `43 49 4D 41` | `41 4D 49 43` | `0x43494D41` | 13 | unresolved |
| `CLFI` | 1229343811 | `43 4C 46 49` | `49 46 4C 43` | `0x434C4649` | 1 | unresolved |
| `CNTD` | 1146375747 | `43 4E 54 44` | `44 54 4E 43` | `0x434E5444` | 1 | unresolved |
| `CRDY` | 1497649731 | `43 52 44 59` | `59 44 52 43` | `0x43524459` | 1 | unresolved |
| `CRJU` | 1430934083 | `43 52 4A 55` | `55 4A 52 43` | `0x43524A55` | 8 | unresolved |
| `EQVE` | 1163284805 | `45 51 56 45` | `45 56 51 45` | `0x45515645` | 1 | unresolved |
| `FDQO` | 1330725958 | `46 44 51 4F` | `4F 51 44 46` | `0x4644514F` | 9 | unresolved |
| `GISK` | 1263749447 | `47 49 53 4B` | `4B 53 49 47` | `0x4749534B` | 10 | unresolved |
| `ICBI` | 1229079369 | `49 43 42 49` | `49 42 43 49` | `0x49434249` | 1 | unresolved |
| `ICST` | 1414742857 | `49 43 53 54` | `54 53 43 49` | `0x49435354` | 12 | unresolved |
| `ISRE` | 1163023177 | `49 53 52 45` | `45 52 53 49` | `0x49535245` | 11 | unresolved |
| `LLER` | 1380273228 | `4C 4C 45 52` | `52 45 4C 4C` | `0x4C4C4552` | 5 | unresolved |
| `LPAK` | 1262571596 | `4C 50 41 4B` | `4B 41 50 4C` | `0x4C50414B` | 2 | unresolved |
| `NERE` | 1163019598 | `4E 45 52 45` | `45 52 45 4E` | `0x4E455245` | 1 | unresolved |
| `OITA` | 1096042831 | `4F 49 54 41` | `41 54 49 4F` | `0x4F495441` | 1 | unresolved |
| `OMUI` | 1230327119 | `4F 4D 55 49` | `49 55 4D 4F` | `0x4F4D5549` | 1 | unresolved |
| `ONRE` | 1163021903 | `4F 4E 52 45` | `45 52 4E 4F` | `0x4F4E5245` | 1 | unresolved |
| `RFSL` | 1280525906 | `52 46 53 4C` | `4C 53 46 52` | `0x5246534C` | 7 | unresolved |
| `SHMG` | 1196247123 | `53 48 4D 47` | `47 4D 48 53` | `0x53484D47` | 1 | unresolved |
| `SORL` | 1280462675 | `53 4F 52 4C` | `4C 52 4F 53` | `0x534F524C` | 5 | unresolved |
| `SRLZ` | 1514951251 | `53 52 4C 5A` | `5A 4C 52 53` | `0x53524C5A` | 9 | unresolved |
| `SRSK` | 1263751763 | `53 52 53 4B` | `4B 53 52 53` | `0x5352534B` | 3 | unresolved |
| `TPSA` | 1095979092 | `54 50 53 41` | `41 53 50 54` | `0x54505341` | 16 | unresolved |
| `WTCO` | 1329812567 | `57 54 43 4F` | `4F 43 54 57` | `0x5754434F` | 10 | unresolved |
| `ZIXI` | 1230522714 | `5A 49 58 49` | `49 58 49 5A` | `0x5A495849` | 26 | unresolved |

## Exact evidence still required

- Locate an official server-side district-spawn realization path terminating in a stable mob identity.
- Alternatively obtain a fingerprint-gated same-context runtime correlation between HashSpawnPoint_t+0x24 and the resulting identity.
- Obtain the exporter or upstream source that assigned accepted SourceNpcId values.

## Safety invariants

```text
OFFICIAL_BINARY_COPIED_TO_AOREBIRTH=NO
OFFICIAL_BINARY_MODIFIED=NO
NAME_ONLY_JOIN_ACCEPTED=NO
COORDINATE_JOIN_ACCEPTED=NO
LEVEL_ONLY_JOIN_ACCEPTED=NO
TERMINAL_IDENTITY_INFERRED=NO
RUNTIME_ACTIVATION_CHANGED=NO
LIVE_CLIENT_STARTED=NO
LIVE_CAPTURE_PERFORMED=NO
PRODUCTION_OPERATION_PERFORMED=NO
DATABASE_OPERATION_PERFORMED=NO
COMMIT_CREATED=NO
PUSH_PERFORMED=NO
```
