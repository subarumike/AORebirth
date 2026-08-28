# ACG Placement/Spawn-Policy Schema Audit

## Result

The official type-`1000014` ACG corpus is authoritative placement/spawn-policy data. All 32,805 decoded placements are structurally ready from official bytes; runtime identity, MonsterData, captures, and population analytics are separate optional enrichment axes.

The previous 508,021-byte opaque total is now split correctly: 507,976 bytes are the proven `PlayfieldDistrictInfo_t::GetZoneToDistrictIndex` vector, while 45 bytes are inactive allocation slack outside the active resource envelope.

## Binary hierarchy

```text
ResourceDatabase active B-tree
  -> type 1000014 / instance = validated playfield resource
  -> PlayfieldDistrictInfo_t (version, zone-index length, district count)
  -> DistrictData_t
  -> HashSpawnPoint_t
  -> ACGHash_t + placement/spawn-policy fields
  -> zone-to-district index vector
```

Each indexed resource uses a 34-byte `FA FA` allocation envelope with an explicit active length. The active type-1000014 payload begins with `uint16 FormatVersion`, `uint32 ZoneToDistrictIndexLength`, and `uint8 DistrictCount`; variable-length district records follow, then exactly that many serialized zone-to-district bytes. District collection counts are `uint8`. Hash-spawn records are packed without inferred padding: 32 bytes in versions 5/6 or 36 bytes in version 7 before conditional sections.

## Sixteen scalar field kinds

| Field | Offset | Type | Semantics | Evidence | Native parser/accessor |
| --- | --- | --- | --- | --- | --- |
| `PositionX` | `0` | `float32-le` | centre_position_x | proven | GameData.dll+0x439E SpawnPoint_t::GetCentrePos |
| `PositionY` | `4` | `float32-le` | centre_position_y | proven | GameData.dll+0x439E SpawnPoint_t::GetCentrePos |
| `PositionZ` | `8` | `float32-le` | centre_position_z | proven | GameData.dll+0x439E SpawnPoint_t::GetCentrePos |
| `Radius` | `12` | `float32-le` | spawn_point_radius | proven | GameData.dll+0x43A2 SpawnPoint_t::GetRadius |
| `RotationMidEncoded` | `16` | `uint16-le` | rotation_mid | proven | GameData.dll+0x2468 RotationSpawnPoint_t::GetRotationMid |
| `RotationWidthEncoded` | `18` | `uint16-le` | rotation_width | proven | GameData.dll+0x2748 RotationSpawnPoint_t::GetRotationWidth |
| `AcgHashNativeUInt32` | `20` | `packed-acghash-uint32-le` | authoritative_placement_identity | proven | GameData.dll+0x1B23 ACGHash_t reader; +0x4459 HashSpawnPoint_t::GetHash |
| `LevelMinimum` | `24` | `uint16-le` | minimum_level | proven | GameData.dll+0x2D49 HashSpawnPoint_t::GetMinLevel |
| `LevelMaximum` | `26` | `uint16-le` | maximum_level | proven | GameData.dll+0x445D HashSpawnPoint_t::GetMaxLevel |
| `RespawnChance` | `28` | `uint8` | respawn_chance | proven | GameData.dll+0x4461 HashSpawnPoint_t::GetRespawnChance |
| `SerializedOptionalFlags` | `29` | `uint8-bitmask` | serialized_section_presence | proven | GameData.dll+0x640F HashSpawnPoint_t::ReadBlob |
| `RespawnTime` | `30` | `uint16-le` | respawn_time | proven | GameData.dll+0x4465 HashSpawnPoint_t::GetRespawnTime |
| `MoreFlags` | `32 when format version >= 7` | `int32-le-bitmask` | more_flags | strongly-corroborated | GameData.dll+0x447C HashSpawnPoint_t::HasMoreFlag |
| `NativeFlags` | `32 for versions 5/6 or 36 for version 7, when presence bit 0 is set` | `uint16-le-bitmask` | flags | strongly-corroborated | GameData.dll+0x4469 HashSpawnPoint_t::HasFlag |
| `AssistanceRadius` | `34 for versions 5/6 or 38 for version 7, when presence bit 0 is set` | `uint8` | assistance_or_proximity_range | strongly-corroborated | GameData.dll+0x44B9 HashSpawnPoint_t::GetAssistanceRadius; +0x448F GetProximityRange |
| `UnknownOptionalU8` | `35 for versions 5/6 or 39 for version 7, when presence bit 0 is set` | `uint8` | unknown | unknown | GameData.dll+0x640F HashSpawnPoint_t::ReadBlob only |

The native exported names prove position, radius, rotation midpoint/width, level range, respawn chance/time, and ACGHash property roles. The bounded client call graph does not expose the server-side executor for these policies, so units or enforcement are left unresolved where the binary does not prove them.

## Corpus field distributions

| Field | Present | Unique | Minimum | Maximum | Zero | Playfields varying |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `PositionX` | 32805 | 27744 | 0.0 | 4957.8623046875 | 126 | 255 |
| `PositionY` | 32805 | 20325 | 0.0 | 480.5 | 168 | 252 |
| `PositionZ` | 32805 | 27859 | 0.0 | 5056.50439453125 | 126 | 255 |
| `Radius` | 32805 | 129 | 0.0 | 182484.75 | 1951 | 169 |
| `RotationMidEncoded` | 32805 | 355 | 0 | 359 | 10240 | 228 |
| `RotationWidthEncoded` | 32805 | 39 | 0 | 14389 | 791 | 111 |
| `AcgHashNativeUInt32` | 32805 | 4016 | 538976288 | 2678038431 | 0 | 254 |
| `LevelMinimum` | 32805 | 218 | 0 | 14640 | 126 | 222 |
| `LevelMaximum` | 32805 | 227 | 0 | 18482 | 126 | 215 |
| `RespawnChance` | 32805 | 22 | 0 | 255 | 3 | 148 |
| `SerializedOptionalFlags` | 32805 | 7 | 0 | 7 | 10580 | 187 |
| `RespawnTime` | 32805 | 213 | 0 | 65535 | 149 | 201 |
| `MoreFlags` | 22631 | 2 | 0 | 1 | 21522 | 33 |
| `NativeFlags` | 19212 | 55 | 0 | 231 | 1643 | 127 |
| `AssistanceRadius` | 19212 | 39 | 0 | 200 | 12434 | 55 |
| `UnknownOptionalU8` | 19212 | 38 | 0 | 255 | 15525 | 54 |

Variable sections: 284 records contain 32737 additional points; 3077 records contain 3077 decoded extensions and 6201 tag entries.

## Orientation

ACG encodes orientation as a rotation midpoint and rotation width, not a quaternion, Euler triple, transform matrix, or facing vector. Midpoint values are 0..359, but width reaches 14,389; no universal degree conversion is justified. The native getters return floats, while no reached transform consumer proves angular units, axis, handedness, or normalization. Orientation readiness is therefore `PARTIAL`.

## Spawn-policy findings

- Position: proven native centre vector.
- Radius: proven native radius field; random-displacement behavior and units remain unproven.
- Count: no simultaneous spawn/generator capacity field is present. Collection counts serialize structure only.
- Respawn: native `RespawnTime` and `RespawnChance` properties are proven; time units and 255 chance sentinel behavior remain unknown.
- Grouping: district parentage and per-record `AdditionalPoints` child points are official. No cross-row group, encounter, generator, or cluster ID is present. The 25-metre components remain heuristic analytics.
- Probability: respawn chance exists; no content-choice weight or spawn-table selector is present.
- Level/context: native minimum and maximum level fields exist; they do not assign captured runtime levels to rows.
- Path/patrol: no waypoint, spline, navigation, or patrol resource reference is present. Additional points are native spawn-point children, not a proven route.
- Flags: two native bitmasks exist, but their individual bits have no proven names in the available client evidence.
- Classification: all rows are generic `HashSpawnPoint_t` placements. No proven field classifies a row as NPC, static object, effect, interactive, or hostile enemy.

## Historical raw-byte classes

- `serialized_hash_spawn_point_records`: 32805 instances / 2087486 bytes. losslessly retained per placement in the complete catalog.
- `serialized_zone_to_district_index`: 622 instances / 507976 bytes. fully decoded and losslessly retained.
- `inactive_record_allocation_slack`: 2 instances / 45 bytes. semantics unknown; losslessly retained.

## Former opaque regions

- `zone_to_district_index`: 622 instances / 507976 bytes; decoded 507976; remaining 0. UnknownHeaderU32 equals serialized byte count for all 622 non-empty instances; all 507,976 bytes are valid district indices; GameData exports PlayfieldDistrictInfo_t::GetZoneToDistrictIndex.
- `record_allocation_slack`: 2 instances / 45 bytes; decoded 0; remaining 45. Bytes lie outside the active FAFA envelope. PF111 retains 36 bytes and PF9080 retains nine; no active parser consumer owns them.

## Case studies

- PF4582: 207 placements across 2 of 2 districts. The 25/181, 199/7, and 199/8 figures are runtime implementation gates, never placement-existence gates.
- Borealis PF3081: 1 official placement. Guide/Guard capture identity is irrelevant to its validity.
- PF127 Subway: 326 placements across 4 of 4 districts.
- Central Elysium PF4542: 643 placements across 55 of 57 districts.
- Andromeda PF655: 397 placements across 25 of 61 districts.

## Readiness boundary

Placement readiness is official decode quality only. The three parser-limited resources (103, 615, 4805) remain resource-level parser boundaries and create no synthetic placement rows. They do not turn any of the 32,805 successfully decoded rows into invalid placements.

## Acceptance

```text
ACG_SCHEMA_AUDIT_COMPLETE=YES
ACG_PLACEMENTS=32805
ACG_RAW_FIELDS=16
ACG_PROVEN_FIELDS=12
ACG_STRONGLY_CORROBORATED_FIELDS=3
ACG_CANDIDATE_FIELDS=0
ACG_UNKNOWN_FIELDS=1
OPAQUE_TOTAL_BYTES=508021
OPAQUE_REGION_INSTANCES=624
OPAQUE_STRUCTURAL_CLASSES=2
OPAQUE_BYTES_DECODED=507976
OPAQUE_BYTES_REMAINING=45
POSITION_PROVEN=YES
ORIENTATION_PROVEN=PARTIAL
GROUPING_PROVEN=PARTIAL
SPAWN_RADIUS_PROVEN=PARTIAL
SPAWN_COUNT_PROVEN=NOT_PRESENT
RESPAWN_TIMING_PROVEN=YES
PROBABILITY_PROVEN=PARTIAL
LEVEL_CONTEXT_PROVEN=YES
PATH_RELATION_PROVEN=NOT_PRESENT
FLAGS_PROVEN=PARTIAL
PLACEMENTS_READY=32805
PLACEMENTS_PARTIAL=0
PLACEMENTS_PARSER_LIMITED=0
PLACEMENTS_INVALID=0
PARSER_LIMITED_RESOURCES=3
PF4582_PLACEMENTS=207
BOREALIS_PLACEMENTS=1
PF127_PLACEMENTS=326
POPULATION_IDENTITY_REQUIRED_FOR_PLACEMENT=NO
RUNTIME_CAPTURE_REQUIRED_FOR_PLACEMENT=NO
MONSTERDATA_REQUIRED_FOR_PLACEMENT=NO
ACGHASH_ROLE=AUTHORITATIVE_PLACEMENT_IDENTITY
MONSTERDATA_ROLE=SERVER_RUNTIME_CREATURE_IDENTITY
STATIC_ACG_MONSTERDATA_SEARCH_REOPENED=NO
TESTS=PASS_29_OF_29
DETERMINISTIC_REPEAT_RUN=YES
DETERMINISTIC_DIGEST=2bb0ac0e2c98e083fc2eb4a197650411e3f027d5cdccdd352266ab3dffb6dfd4
COMMIT=PENDING
```
