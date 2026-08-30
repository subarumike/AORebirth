# Development-Only ACG Placement Placeholders

## Status and safety boundary

The ACG placement placeholder system is a Debug-build visualization aid. It is
`Off` by default, accepts exactly one playfield `ResourceInstance`, loads only
that selected playfield shard, and cannot be enabled in a Release build. It
does not authorize production population or change the Legacy playfield route.

The source package is
`AO_ACG_Spawn_Capture_Atlas_18.8.62_EP1_20260829.zip`, pinned to SHA-256
`379e39cf3a2a697b5613316ff2a7da66a9d5f0ecc30d1b75efe0a4dffc7d093e`.
The deterministic AORebirth generator validates the ZIP, every package member,
the governed visual evidence source, and all corpus counts before writing
`docs/generated/acg_development_placeholders`.

The imported corpus retains:

- 630 enumerated type-`1000014` resources and 627 parsed resources;
- explicit malformed boundaries for PF103, PF615, and PF4805;
- 32,805 primary `HashSpawnPoint` records;
- 32,737 official `AdditionalPoints` and 65,542 total coordinates;
- 4,016 unique native ACG keys across 459 populated playfields;
- the deterministic 4,016-target / 238-playfield capture plan;
- all duplicate source rows and 142 duplicate primary-coordinate rows.

`RespawnChancePercent` from the portable CSV is imported as
`RespawnChanceRaw`. No percentage meaning is promoted. Additional points remain
child coordinates with unresolved selection and multiplicity semantics.

## Evidence registry

`docs/reference/acg-development-visual-evidence.json` is the human-reviewed
source for the native-key registry. The generated registry is keyed by
`AcgHashNativeUInt32`; `AcgHashWireBytes` is retained separately. Display text
is never a join key, and `0x20202020` remains distinct from `0x9F9F9F9F`.

Current grades are:

- `ExactOfficial`: FDQO only — server template `43296` / `A004`, MonsterData
  `1040023:17655`, mesh `1010002:15222`.
- `CaptureCorrelated`: UIGU, RPOF, and VAWT. These remain placeholders.
- `CaptureCorrelatedMultipleVariants`: 01V1. Its two captured body variants
  and unresolved Atrox-family variant remain explicit.
- `Unresolved`: the other 4,011 native ACG keys.

Only `ExactOfficial` may select its mapped visual automatically. FDQO retains
the exact `A004` / CatMesh `15222` Beach Leet appearance. Every
capture-correlated or unresolved row uses equipped Mesh `283882`, resolved
directly from local `items.dat` Item `283862` (`No No Placard`) stat `209`, as
the explicit development placeholder and must never be described as the real
unresolved mob. Item stat `mesh` / `12` (`9013`) is the inventory-item mesh and
does not populate the SimpleChar mesh list. The existing `A004` template remains
only the safe NPC construction pipeline before its monster appearance is
cleared and the equipped-mesh layer is applied.

## Modes

| Mode | Selected-playfield materialization |
| --- | --- |
| `Off` | No corpus load and no runtime entities. This is the default. |
| `CapturePlan` | The recommended primary target assigned to each ACG in the selected playfield. |
| `CurrentPlayfieldPrimary` | Every official primary record in the selected playfield. |
| `CurrentPlayfieldAllPoints` | Every primary plus every official child `AdditionalPoint`; emits an explicit warning. |
| `ResolvedComparison` | All selected-playfield primaries; `ExactOfficial` uses its exact mapping and every other grade remains a placeholder. |

Every created entity receives a normal transient runtime identity from the
existing pool allocator. ACG keys, resource IDs, ordinals, and stable source IDs
are never used as runtime entity IDs. Full source provenance is retained in the
server-side development registry even when the visible name is shortened.

Placeholders verify Item `283862` still resolves stat `209` to equipped Mesh
`283882` in the loaded `items.dat`. They clear the construction template's
MonsterData, CatMesh, DisplayCatMesh, head, texture, and mesh layers, then put
Mesh `283882` in the normal right-hand SimpleChar mesh slot. They use a passive
idle controller, neutral side, the existing immune flag plus explicit combat
guards, no combat contract, no loot registration, and no mission/XP completion
path. Exact FDQO continues to set `catmesh` and `displaycatmesh` to `15222`.
Placeholders have no waypoints and do not run NPC timers. No proven server-side
non-collision switch is available on this path, so collision suppression
remains explicitly unclaimed.

## Operator guide

Build Debug through the normal repository wrapper, then enable one playfield by
running the local managed restart wrapper. For PF4582 primary placements:

```cmd
restart-engines-acg-development-placeholders.cmd CurrentPlayfieldPrimary 4582
```

For its capture-plan subset, replace the mode with `CapturePlan`. Use
`CurrentPlayfieldAllPoints` only when the additional-point volume and unresolved
semantics are intentionally accepted for that development session. A normal
`restart-engines.cmd` starts the next session with `Off` because the specialized
wrapper uses process-local environment variables.

The wrapper launches no AO client and performs no capture. Mike retains client
control and live validation.

To record a newly resolved visual, provide the completed evidence corpus for
offline analysis. After an exact native-ACG identity bridge is proven, update
the one keyed entry in
`docs/reference/acg-development-visual-evidence.json`, preserve the former
classification in the evidence trail, update the expected grade counts, and
regenerate with:

```cmd
tools\generate_acg_development_placeholders.cmd "<portable-atlas-zip>"
tools\generate_acg_development_placeholders.cmd "<portable-atlas-zip>" --check
```

Capture correlation alone cannot be promoted to `ExactOfficial`.
