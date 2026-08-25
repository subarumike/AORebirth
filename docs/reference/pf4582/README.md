# PF4582 Authoritative Placement Source

`PlayfieldDistrictInfo.json` is the exact developer-supplied PF4582 placement
dataset received on 2026-08-24. Its SHA-256 is
`b747aea145cb36e3f9be5b2cacc7aaebca3d24017a14540ac1f29f4bd1296b32`.

Authority is limited to the 206 accepted placement records and their retained
source metadata. `NpcId` is a stable AORebirth source-placement key; it is not
proven to be an original native Funcom field. The file does not prove mob
appearance, combat, movement, aggression, loot, scripts, dialogue, boss
mechanics, or exact respawn behavior.

`runtime-evidence-map.json` binds only the 25 previously implemented runtime
placements to their authoritative `NpcId` values. All other placements remain
known but runtime blocked. Dynamic source names and all undecoded flags remain
explicitly unresolved.

`template-hash-evidence.json` is the pinned, non-runtime identity-evidence
ledger for all 38 legacy numeric `TemplateHash` groups. It records candidates
and blockers but grants no activation authority. `TemplateHash` remains a
compatibility name: official EP1 evidence proves a packed four-byte `ACGHash_t`
scalar/tag, not a cryptographic hash or terminal mob-template identity. Its
generated audit is reproducible through `Tools\audit_pf4582_template_hashes.cmd`.

`official/` contains the byte-identical structured EP1 evidence snapshot and
its governed manifest. The official source has 207 `HashSpawnPoint_t` records;
206 reconcile to the accepted cohort and `NCNN` remains an additional official
blocked placement with no fabricated `SourceNpcId`, profile, or runtime
activation. `Tools\reconcile_pf4582_official_source.cmd` generates the
207-record evidence overlay and non-runtime C# catalog.

The current bridge outcome is `STRUCTURAL_SOURCE_AND_CONSUMER_FOUND`. The prior
`NO_BRIDGE_LOCATED` outcome is preserved as superseded history. The official
source structure, `ACGHash_t` type, parser, native field, vector, and accessors
are proven; terminal mob identity, static mappings, and the runtime hash-to-dynel
join remain unresolved.

Regenerate and validate through the repository wrappers documented in
`docs/ai/WORKFLOW.md`.
