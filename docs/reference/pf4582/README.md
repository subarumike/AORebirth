# PF4582 Authoritative Placement Source

`PlayfieldDistrictInfo.json` is the exact developer-supplied PF4582 placement
dataset received on 2026-08-24. Its SHA-256 is
`b747aea145cb36e3f9be5b2cacc7aaebca3d24017a14540ac1f29f4bd1296b32`.

Authority is limited to the 206 placement records and their retained source
metadata. The file does not prove mob appearance, combat, movement, aggression,
loot, scripts, dialogue, boss mechanics, or exact respawn behavior.

`runtime-evidence-map.json` binds only the 25 previously implemented runtime
placements to their authoritative `NpcId` values. All other placements remain
known but runtime blocked. Dynamic source names and all undecoded flags remain
explicitly unresolved.

`template-hash-evidence.json` is the pinned, non-runtime identity-evidence
ledger for all 38 numeric `TemplateHash` groups. It records candidates and
blockers but grants no activation authority. Its generated audit is reproducible
through `Tools\audit_pf4582_template_hashes.cmd`.

Regenerate and validate through the repository wrappers documented in
`docs/ai/WORKFLOW.md`.
