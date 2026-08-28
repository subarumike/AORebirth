# ACG-to-MonsterData Resource Chain Audit

## Result

`ACG_MONSTERDATA_RELATION=SERVER_RUNTIME_ASSOCIATION`

The official client keeps ACG placement/spawn-policy data and runtime NPC model identity on two independent axes. The server-authored SimpleChar full update supplies stat 359; the client uses that integer as resource instance `1040023:<MonsterData>`. No official static ACGHash-to-MonsterData edge or client runtime join was found.

## Decisive data flow

```text
STATIC:  ResourceDatabase 1000014 -> DistrictData -> HashSpawnPoint -> ACGHash/coordinates -> no client consumer
RUNTIME: server SimpleChar full update -> stat 359 -> 1040023:MonsterData -> CATMesh -> visual
```

## Effective ResourceDatabase view

- Clients audited: 2
- EP1 segments: 3
- EP2 segments: 24
- Physical database files: 29
- Active EP1 records: 460193
- Active resource types: 50
- Duplicate active keys: 0
- Physically present shadowed same-key records: not enumerable from the active logical index
- Other unindexed physical records: not enumerable from the active logical index

The `.dat`, `.001` ... files are contiguous physical segments selected by one active B-tree index, not expansion-priority overlays. No separate base, Shadowlands, Alien Invasion, later-patch, or localized database layer is consumed beside each client's unified logical database. EP1 and EP2 contain identical raw records for ACG, PlayfieldDynels, CATMesh, and MonsterData; the larger EP2 segment set carries graphics-client assets rather than additional gameplay MonsterData.

| Client | Database file | Bytes | SHA-256 |
| --- | --- | ---: | --- |
| 18.8.62_EP1 | ResourceDatabase.idx | 9437184 | `ba152f59096d5358f4d1b6511d3a3d264999e0a59f1ab7bf3a7cc18a4888c273` |
| 18.8.62_EP1 | ResourceDatabase.dat | 1073741824 | `3cabdede7b9b2468ed22f10f536fb2f7083ea05ed9483e2d96b22cf080d736a6` |
| 18.8.62_EP1 | ResourceDatabase.dat.001 | 1073741824 | `f8884a2c382ce7c95f20b4423567f176ed40675ba9ce8362527288712871ba73` |
| 18.8.62_EP1 | ResourceDatabase.dat.002 | 142116570 | `2024021f966c3c8a8c083e01cbad2335ba33c19a1661a148060391755a608cc1` |
| 18.8.62_EP2 | ResourceDatabase.idx | 16777216 | `6ab5f9747bd82840a6562f1f03d44faece899f7be1c3552aa7c1e646b07892d9` |
| 18.8.62_EP2 | ResourceDatabase.dat | 1073741824 | `3e903d446f63d44a669bf6d9627dc9a13f59c60dd611fab34f592902b5982b88` |
| 18.8.62_EP2 | ResourceDatabase.dat.001 | 1073741824 | `e3f2b9d7175b124bc4757784030b9907971f1a7927d2a4765033d724dbd6563b` |
| 18.8.62_EP2 | ResourceDatabase.dat.002 | 1073741824 | `61748f16dd3bc467ddbb9390e0db7bc9e1c71d0be0d0fd2ed859e035d815d62c` |
| 18.8.62_EP2 | ResourceDatabase.dat.003 | 1073741824 | `37335143df44f133e6b01e91f368ab0229dc4eb00ea336678f25e066a50c3c4c` |
| 18.8.62_EP2 | ResourceDatabase.dat.004 | 1073741824 | `c82654886ecf3e34ad5fa8122b6837ab028078b61d8891e6d378e34a3fce802a` |
| 18.8.62_EP2 | ResourceDatabase.dat.005 | 1073741824 | `9770609c2eefe733d7da15cc3ca078d6cdb25c8e7c426536e2ada909b4e0075f` |
| 18.8.62_EP2 | ResourceDatabase.dat.006 | 1073741824 | `d9b52637b1e411e2b7a68a1d0e123927539fb5b21c6759b7a89ee4d5b5c29ba6` |
| 18.8.62_EP2 | ResourceDatabase.dat.007 | 1073741824 | `ea2162db3cf1f7374502d7fdde4272739a976e46369251a6e80fdc9b14864437` |
| 18.8.62_EP2 | ResourceDatabase.dat.008 | 1073741824 | `084c7c7ac7b51f7e4af33578d11e798a989848f81d71c0083472445f431dc9be` |
| 18.8.62_EP2 | ResourceDatabase.dat.009 | 1073741824 | `d93e794944ce046214c756c25a1378164b580b8eb0e8070451dbfc8b27bb0ac6` |
| 18.8.62_EP2 | ResourceDatabase.dat.010 | 1073741824 | `edf46d441223518482bb2403223fe86b1860fd045427093293c4bd2b9e697960` |
| 18.8.62_EP2 | ResourceDatabase.dat.011 | 1073741824 | `35c033d2d9f27fef464ba1d0ba6a07f8b45e1e9a0357ce70864273902ecda330` |
| 18.8.62_EP2 | ResourceDatabase.dat.012 | 1073741824 | `fdfafd26b9f23ca044c861093ad501e3addf4e6a47dc384bb18c3a523240a0b3` |
| 18.8.62_EP2 | ResourceDatabase.dat.013 | 1073741824 | `5d0e88b4a9d0a0f0d0411fc9eab382207782d79f9e54d7e5323b5a7de59a0787` |
| 18.8.62_EP2 | ResourceDatabase.dat.014 | 1073741824 | `9bfac31902f60ee4691005dd387a710d6dfd81632ccc29d439fa26efd1ca0e5a` |
| 18.8.62_EP2 | ResourceDatabase.dat.015 | 1073741824 | `c62537dfe17d4b004281480764e420544d8a8a78beeda6e64da601e68ba82830` |
| 18.8.62_EP2 | ResourceDatabase.dat.016 | 1073741824 | `834404b0bdc1401a172e2389543996dc9bd30385d748feb00c666925fea64747` |
| 18.8.62_EP2 | ResourceDatabase.dat.017 | 1073741824 | `07c2203e61d3c5b577a0703dd5e026ed206b87b71cd60cf80fe6fde88108fb25` |
| 18.8.62_EP2 | ResourceDatabase.dat.018 | 1073741824 | `07814c05ea6e6bb66d08be29d8597f3303ea4a5ced530155204be4c73fca8919` |
| 18.8.62_EP2 | ResourceDatabase.dat.019 | 1073741824 | `254eeb78af8aebc00c025bef84090b56bf8a0f252c22126d7b19ca95212265d3` |
| 18.8.62_EP2 | ResourceDatabase.dat.020 | 1073741824 | `fb8477381969a8dc9cccd814259e6607f0bdb8534ce575767eec393e8424532e` |
| 18.8.62_EP2 | ResourceDatabase.dat.021 | 1073741824 | `96de33bf34b80734519d0ebfb709899516af52b3e07f9c026620ecf8bbe122d3` |
| 18.8.62_EP2 | ResourceDatabase.dat.022 | 1073741824 | `71aeb413c17385519b78b647acf667168b3b8bdc067cb0fcaa587138c74394b2` |
| 18.8.62_EP2 | ResourceDatabase.dat.023 | 3228887 | `b179d51365604c4c079b8ed6f1a6d503a98df08ccdb8b265cb128f3d4f3b2d0d` |

## ACG binary schema

- Resources: 630
- Placements: 32805
- Versions: 5, 6, 7
- Decoded raw field kinds: 16
- Trailing opaque bytes: 507976
- Allocation slack bytes: 45
- Superseding schema result: the 507,976-byte historical `TrailingOpaqueRegion` is the fully decoded `ZoneToDistrictIndex` vector; only 45 allocation-slack bytes remain semantically opaque. See `docs/reference/ACG_PLACEMENT_SCHEMA.md`.
- The three historical dropped raw-byte classes meant exact raw payloads omitted from this older normalized projection, not three undecoded ACG fields. The complete schema catalog now retains all three losslessly.
- ACG hash generation: unknown; the client reader only proves a packed four-byte scalar/tag.
- Representative PF4582, PF3081, PF127, Central Elysium, and Andromeda records retain exact raw bytes, offsets, widths, signedness, floats, indices, and full opaque regions in the forensic catalog.

## MonsterData reverse index

- MonsterData IDs: 1470
- Proven static referenced IDs: 231
- Proven static unreferenced IDs: 1239
- Total proven static references: 498
- Raw candidate referenced IDs: 1297
- Raw candidate occurrences: 60346
- Candidates preceded by typed resource `1040023`: 0
- Candidates preceded by stat `359`: 518

Nano resource stat-359 pairs prove a separate static spell/morph-to-MonsterData path. All other raw four-byte equality remains a correlation candidate only, and none creates an ACG, spawn-template, or contextual NPC-definition edge.

Proven static reference types:

- `1040005` Nano: 498 references to 231 unique MonsterData IDs; Nano stat 359 -> MonsterData used by the separate spell/morph client path.

Significant raw-candidate resource types:

- `1000020` Item: 21205 candidates; Item raw candidates include stat-359 adjacency, but the audited client spawn path does not consume Item as an ACG resolver.
- `1010001` RDBMesh: 11279 candidates; visual/geometry resource numeric candidates; no MonsterData-owning field or ACG join.
- `1010002` CATMesh: 9687 candidates; visual/geometry resource numeric candidates; no MonsterData-owning field or ACG join.
- `1000014` PlayfieldDistrictInfo/ACG: 5206 candidates; decoded placement-field numeric collisions; no typed MonsterData field.
- `1040005` Nano: 2744 candidates; proven separate Nano stat-359 spell/morph reference path; not ACG or spawning.
- `1010004` Texture: 1755 candidates; visual/geometry resource numeric candidates; no MonsterData-owning field or ACG join.
- `1040023` MonsterData: 1699 candidates; raw numeric candidates only; no typed MonsterData field or ACG consumer proven.
- `1000009` unknown type 1000009: 1590 candidates; raw numeric candidates only; no typed MonsterData field or ACG consumer proven.
- `1000021` Wall: 1321 candidates; raw numeric candidates only; no typed MonsterData field or ACG consumer proven.
- `1000013` unknown type 1000013: 668 candidates; raw numeric candidates only; no typed MonsterData field or ACG consumer proven.
- `1010003` Animation: 628 candidates; visual/geometry resource numeric candidates; no MonsterData-owning field or ACG join.
- `1000029` unknown type 1000029: 382 candidates; raw numeric candidates only; no typed MonsterData field or ACG consumer proven.
- `1010026` unknown type 1010026: 342 candidates; raw numeric candidates only; no typed MonsterData field or ACG consumer proven.
- `1000026` Statel/PlayfieldDynels: 336 candidates; decoded TemplateId and identity fields have zero MonsterData matches; remaining raw hits are descriptor/numeric candidates.
- `1010016` unknown type 1010016: 328 candidates; raw numeric candidates only; no typed MonsterData field or ACG consumer proven.
- `1010008` Icon: 319 candidates; visual/geometry resource numeric candidates; no MonsterData-owning field or ACG join.
- `1000001` Playfield: 216 candidates; raw numeric candidates only; no typed MonsterData field or ACG consumer proven.
- `1010017` unknown type 1010017: 149 candidates; raw numeric candidates only; no typed MonsterData field or ACG consumer proven.

## Spawn/template candidates

- PlayfieldDynels: 12624 dynels, 0 TemplateId-to-MonsterData matches, 0 identity-instance matches.
- District SpawnInfo entries: 7203; rejected because the field is untyped, has no official reader/caller, and does not establish MonsterData semantics.

## Client and SCFU MonsterData trace

Static ACG reader chain:

- `ResourceDatabase 1000014:<playfield>`
- `GameData.dll+0x9def PlayfieldDistrictInfo_t::ReadBlob`
- `GameData.dll+0x49be DistrictData_t reader`
- `GameData.dll+0x640f HashSpawnPoint_t::ReadBlob`
- `GameData.dll+0x1b23 ACGHash_t reader`
- `DistrictData_t hash-spawn vector +0x5c`
- `terminates without an official client consumer`

Server packet to visual-resource chain:

- `N3.dll+0x9b08`: construct inbound Family-10 SimpleChar full update.
- `Gamecode.dll+0x7916d`: decode SimpleCharFullUpdateIIR body.
- `N3.dll+0x65d1`: activate inbound info item.
- `N3.dll+0x3f80`: create dynel from ribosome.
- `Gamecode.dll+0x7803b`: write server-authored field as SimpleChar stat 359.
- `Gamecode.dll+0x5c3ed/0x590b8/0x5a3b1`: read stat 359 during setup, refresh, or stat update.
- `Gamecode.dll+0x52686/0x52271`: propagate and bind requested MonsterData instance.
- `Gamecode.dll+0x4e275/0x4e174`: resolve resource identity 1040023:<stat359>.
- `DatabaseController.dll+0x2c24`: load MonsterData binary stream.
- `Gamecode.dll+0x4de5d`: parse MonsterData.

Decoder caveat: The exact wide-value meaning of the Family-10 reader's low-16-bit/padding sequence remains unresolved. The direct server-authored field-to-stat-359 assignment and the ordinary stat-update selector path are independently established.

## Proof cases

- PF4582: independent static placement and server-authored runtime MonsterData axes; no official static join.
- Leet: runtime Leets resolve MonsterData to CATMesh/archetype; ACG candidates remain independent.
- Heckler: ordinary Heckler names are not separate MonsterData names in the client; EP1 and EP2 relevant records are byte-identical, so the gap is server/context naming or shared generic visual data rather than a missing expansion layer.

## CATMesh decoder limits

The four limited records remain 201342, 201345, 214953, 260512. They affect none of the PF4582, Leet, or named Heckler proof records, so no speculative decoder repair was made.

## Acceptance

```text
ACG_MONSTERDATA_RESOURCE_AUDIT=COMPLETE
RESOURCE_DATABASES_DISCOVERED=2
RESOURCE_DATABASES_EFFECTIVE=2
RESOURCE_DATABASES_EXCLUDED=0
ACG_PLACEMENTS=32805
MONSTERDATA_RECORDS=1470
CATMESH_RECORDS=861
ACG_RAW_FIELDS=16
ACG_DROPPED_FIELDS=0
ACG_DROPPED_RAW_BYTE_CLASSES=3
ACG_OPAQUE_BYTES=508021
MONSTERDATA_REFERENCED_IDS=231
MONSTERDATA_UNREFERENCED_IDS=1239
MONSTERDATA_REFERENCE_TYPES=1_PROVEN_STATIC_TYPES_38_RAW_CANDIDATE_TYPES
STATIC_ACG_TO_MONSTERDATA_DIRECT=0
STATIC_ACG_TO_MONSTERDATA_INDIRECT=0
SERVER_SUPPLIES_RUNTIME_MONSTERDATA=YES
CLIENT_RUNTIME_JOIN_FOUND=NO
ACG_MONSTERDATA_RELATION=SERVER_RUNTIME_ASSOCIATION
PF4582_RESULT=INDEPENDENT_STATIC_AND_RUNTIME_AXES_NO_OFFICIAL_JOIN
LEET_RESULT=RUNTIME_MONSTERDATA_VISUAL_CHAIN_PROVEN_STATIC_ACG_JOIN_ABSENT
HECKLER_RESULT=NO_EXPANSION_OMISSION_SERVER_OR_CONTEXT_NAMING_REMAINS
ACG_COORDINATES_USED_TO_INFER_STATIC_BRIDGE=NO
APPEARANCE_USED_TO_INFER_STATIC_BRIDGE=NO
RUNTIME_ID_USED_TO_INFER_STATIC_BRIDGE=NO
TESTS=PASS
DETERMINISTIC_REPEAT_RUN=YES
DETERMINISTIC_DIGEST=8a60130b95887635fd4462cff7d4a1fa91238963a57242c2d23de1f3393c9464
```
