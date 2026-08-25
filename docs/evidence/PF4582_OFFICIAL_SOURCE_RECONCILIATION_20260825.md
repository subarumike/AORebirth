# PF4582 official source reconciliation

The accepted 206-record AORebirth source reconciles one-to-one to 206 of the 207 official EP1 `HashSpawnPoint_t` records. The unmatched official record is `NCNN`. This is structural evidence only: the official terminal mob identity remains unresolved and runtime activation is unchanged.

## Required metrics

```text
PF4582_PRIOR_BRIDGE_OUTCOME=NO_BRIDGE_LOCATED
PF4582_BRIDGE_OUTCOME=STRUCTURAL_SOURCE_AND_CONSUMER_FOUND
PF4582_PRIOR_OUTCOME_SUPERSEDED=YES
PF4582_OFFICIAL_BUILD=18.8.62_EP1
PF4582_OFFICIAL_RESOURCE_TYPE=1000014
PF4582_OFFICIAL_RESOURCE_INSTANCE=4582
PF4582_OFFICIAL_RESOURCE_RECORDS=207
PF4582_ACCEPTED_SOURCE_RECORDS=206
PF4582_OFFICIAL_ADDITIONAL_RECORDS=1
PF4582_OFFICIAL_ICC_RECORDS=142
PF4582_OFFICIAL_CENTRAL_ICC_RECORDS=65
PF4582_OFFICIAL_STRUCTURAL_SOURCE_PROVEN=YES
PF4582_ACGHASH_OFFICIAL_TYPE_PROVEN=YES
PF4582_ACGHASH_PARSER_CONSUMER_PROVEN=YES
PF4582_TERMINAL_IDENTITY_BRIDGE=UNRESOLVED
PF4582_STATIC_MOB_MAPPINGS_EXTRACTED=0
PF4582_ACCEPTED_RECORDS_RECONCILED=206
PF4582_ACCEPTED_RECORDS_UNMATCHED=0
PF4582_OFFICIAL_RECORDS_UNMATCHED=1
PF4582_OFFICIAL_EXTRA_KEY=NCNN
PF4582_NCNN_DISPOSITION=INCLUDE_AS_OFFICIAL_BLOCKED_PLACEMENT
PF4582_NCNN_SOURCE_NPCID_PRESENT=NO
PF4582_NCNN_PROFILE_SELECTED=NO
PF4582_NCNN_RUNTIME_ACTIVE=NO
PF4582_OFFICIAL_OVERLAY_RECORDS=207
PF4582_OFFICIAL_OVERLAY_RECONCILED_TO_SOURCE_NPCID=206
PF4582_OFFICIAL_OVERLAY_WITHOUT_SOURCE_NPCID=1
PF4582_OFFICIAL_OVERLAY_RUNTIME_CONSUMED=NO
PF4582_CURRENT_RUNTIME_CATALOG_RECORDS=206
PF4582_OFFICIAL_RECORDS_PENDING_RUNTIME_INTEGRATION=1
PF4582_ACCEPTED_JSON_SHA256_MATCH=YES
PF4582_ACCEPTED_JSON_REWRITTEN=NO
PF4582_DUAL_ENCODING_KEYS_ROUNDTRIPPED=38
PF4582_SOURCE_NPCID_STABLE_FOR_AOREBIRTH=YES
PF4582_SOURCE_NPCID_PROVEN_NATIVE_FUNCOM_FIELD=NO
PF4582_RUNTIME_ACTIVE_BEFORE=25
PF4582_RUNTIME_ACTIVE_AFTER=25
PF4582_CURRENT_RUNTIME_BLOCKED_BEFORE=181
PF4582_CURRENT_RUNTIME_BLOCKED_AFTER=181
PF4582_RUNTIME_ACTIVATION_CHANGED=NO
```

## Official resource

Build `18.8.62_EP1`; type `1000014`; instance `4582`; offset `0x0C6B4436`; length `10999`; record SHA-256 `24ee42fd0c43ab69f6c832826085555fd622e147af6d72a97903f769a08ea8d3`. Format version `7` contains two districts with 142 and 65 records.

The official native path is `PlayfieldDistrictInfo_t::ReadBlob -> operator>>(DistrictData_t) -> HashSpawnPoint_t::ReadBlob -> operator>>(ACGHash_t)`. `ACGHash_t` is a packed four-byte scalar/tag. The parser and native accessors are proven; no terminal mob-template or dynel identity resolver is proven.

## Encoding model

The legacy accepted `TemplateHash` uint32 is decoded as little-endian ASCII to `CanonicalAcgHashText`. Official wire bytes are the reversed canonical bytes, and the official native scalar is those wire bytes interpreted little-endian. Accepted and official native integers are never compared directly. All 38 accepted keys round-trip without collision; the accepted JSON remains byte-identical.

## Duplicate reconciliation

`5` exact duplicate-equivalence groups are retained. SourceNpcId/official-order pairing is used only after monotonic order preservation was demonstrated across `185` unique exact-field matches. No record is collapsed or assigned twice.

## NCNN audit

Official identity: `18.8.62_EP1:1000014:4582:district-1:record-50`; district `1` `Central ICC Shuttleport`; ordinal `50`; relative offset `0x1EDB`.

Position `[940.228759765625, 47.210018157958984, 875.340087890625]`; levels `1-1`; radius `0.0010000000474974513`; encoded rotation `269` width `1`; chance `80`; time `30.0`; native flags `0`; more flags `0`; serialized optional flags `1`; unknown optional byte `0`; assistance radius `0`; serialized size `40`.

Canonical text `NCNN`; wire bytes `4E 4E 43 4E`; native scalar `0x4E434E4E`. `BossMods`, `Name`, `SpawnPointFlags`, and `SpawnUnknowns` do not exist in the imported official record and are not fabricated.

Disposition: `INCLUDE_AS_OFFICIAL_BLOCKED_PLACEMENT`. NCNN is a normal 40-byte HashSpawnPoint_t in the official district vector, has the same parsed field schema as all neighboring records, and no imported official field or consumer rule proves it disabled, sentinel, editor-only, or non-runtime. Inclusion preserves official placement evidence only and grants no identity or activation authority.

The disposition records an official blocked placement only. `SourceNpcId` is null, no profile is selected, and runtime activation is unauthorized.

## Runtime boundary

The current runtime catalog remains 206 records with 25 active and 181 blocked. The 207-record official overlay is not referenced by `IccShuttleportSpawn`; no candidate mapping or ISRE propagation is performed.
