# PF4582 official EP1 structural evidence

This directory is the governed AORebirth-local snapshot of the three structured
PF4582 evidence artifacts imported byte-for-byte from the completed, read-only
AO Stripdown investigation. The evidence manifest pins their SHA-256 digests,
official build and resource fingerprints, native parser path, and authority
limits. No official binary or bulk scanner output is stored here.

The official EP1 resource contains 207 `HashSpawnPoint_t` records across two
districts (142 and 65). It proves the official packed four-byte `ACGHash_t`
scalar/tag, its serialized and parsed locations, and the parser/native accessor
path. It does not prove a terminal mob template, `MonsterData`, visual identity,
AORebirth profile, or runtime dynel join.

The accepted 206-record `PlayfieldDistrictInfo.json` remains unchanged. Its
legacy `TemplateHash` integer is converted explicitly to canonical ACGHash text;
it is never compared directly with the byte-swapped official native scalar.
`NpcId` remains AORebirth's stable source-placement key but is not claimed as a
proven native Funcom field. The additional official `NCNN` record has no
fabricated `SourceNpcId` and no runtime authority.

Regenerate and validate the reconciliation and evidence overlay through
`Tools\reconcile_pf4582_official_source.cmd` as documented in
`docs\ai\WORKFLOW.md`.
