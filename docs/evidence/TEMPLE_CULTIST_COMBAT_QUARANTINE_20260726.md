# Temple Cultist combat quarantine audit — 2026-07-26

## Outcome

No remaining Cultist cohort can be restored from the generated profiles without
inventing or borrowing an uncaptured `SpecialAttackWeapon` initialization.
Production combat behavior is therefore unchanged.

The accepted checkpoint is still `376/489` certified and `113` quarantined.
The current resolver enumerates `76` quarantined Cultist spawn rows, not the
checkpoint's stated `70`. This is a pre-existing six-row accounting discrepancy:
the audit was run at accepted commit
`47b7604633b960b46fb553eca2c46e9cba391707` before any production change.
It does not represent six newly quarantined actors, and this evidence-only slice
does not revise the accepted certification metric.

## Shared exact packet structure

Every complete generated Cultist profile audited here uses:

- WIFU outer fields `N3=0`, `Unknown1=11`, state machine `1000015:0`,
  `Unknown2=262`, `Unknown3=0`;
- right-hand slot `6`;
- ordered WIFU stat identifiers
  `0,23,701,702,703,412,26,294,210`;
- empty `SpecialAttackWeapon`, `N3=0`;
- `Attack` N3/action `0/0`;
- one `AttackInfo` stream at slot `6`, weapon instance `0`, damage type `0`,
  hit type `3`, N3 `0`;
- exact order `WIFU -> SpecialAttackWeapon -> Attack -> AttackInfo`.

That common structure is not sufficient to fill a missing level. The generated
profiles contain level- and generation-specific WIFU quality/template/energy
variants and distinct `SpecialAttackWeapon Unknown1..5` initialization values.
The packet factory emits those values exactly from the selected contract. The
repository has no production owner that supplies missing Cultist
`SpecialAttackWeapon Unknown1..4` values to the binder. Selecting another
level's values would therefore be nearest-level or cross-level substitution.

## Generated combat families

| MonsterData | Generated levels | Complete profile IDs / weapon initialization | Capture sessions | Classification |
|---|---|---|---|---|
| `26074` | 21, 24, 30, 31, 34 | `cd33f0b6a298d696-03dc0b29328f8462`, `9c5706fc671cbac5-57c871f8212c0bb1`, `5339849977417b5a-9ca73d7846c38f6c`, `5007e6560bf8f010-0b1b1f1616ca04ad`, `1e9cacd0721ac81b-620ee46c9d239150`; WIFU `204747/204747`, QL1, Energy/ammo `-1`; SAW initialization differs by level (`320:320:320:12:0` through `535:535:535:20:0`) | `20260721-032547`, `20260721-033006`, `20260721-052115` | Exact structure agrees; uncaptured levels lack exact SAW initialization. |
| `26082` | 26, 27, 29-33, 35 | Eight complete profiles; WIFU families `130163/130164` and `130164/130164`, variable QL, Energy/ammo `-1` | `20260721-033006`, `20260721-043204`, `20260721-052115`, `20260721-230426` | Genuine weapon-template variant plus level-specific SAW initialization. |
| `26103` | 20 (two), 25, 28 (two), 30-32, 34-35 | Ten complete profiles; WIFU families `129028/129029` and `129028/129028`, variable QL, Energy/ammo `-1`; L28 profiles `5643a4bd8a3aaf44-084c355903d1b399` and `5643a4bd8a3aaf44-9ce155283cfcc36c` remain indistinguishable to runtime source `0x79834DCF` | `20260528-190456`, `20260721-031913`, `20260721-033006`, `20260721-052115`, `20260721-232051` | Genuine weapon variant; one exact-level ambiguity; uncaptured levels lack exact SAW initialization. |
| `26135` | 29, 30 (two), 32-34 | Six complete profiles, WIFU `158298/158299`, variable QL, Energy/ammo `-1`; L30 profiles `fe392350897b358a-24a802cb7db6c581` (QL23) and `fe392350897b358a-c4027eb3ce806767` (QL24) remain indistinguishable to runtime source `0x7983FB27` | `20260721-052115`, `20260722-042930` | One exact-level ambiguity; uncaptured levels lack exact SAW initialization. |
| `26137` | 21, 23, 26, 28-30 | `8dae5024f999475e-03dc0b29328f8462`, `58e682ec3ebb63c8-b677b63db5c16f15`, `a84eedad1a598b40-c4f21242a4a7ba96`, `e30508b3b9b8e352-a87351572a2f5f23`, `3ec94c5698a809ab-d38e253d2479c92e`, `2ee43d964a95a575-9ca73d7846c38f6c`; WIFU `204747/204747`, QL1, Energy/ammo `-1`; six distinct SAW initializations: `320:320:320:12:0`, `351:351:351:13:0`, `400:400:400:16:0`, `434:434:434:17:0`, `450:450:450:17:0`, `468:468:468:18:0` | `20260721-031913`, `20260721-032547`, `20260721-052115`, `20260721-230426`, `20260722-042930` | Largest internally structure-compatible blocked cohort (10 actors), but every captured level has a different exact SAW initialization and none covers the active missing levels. |
| `26147` | 20, 23, 27, 29, 31-34 | Eight complete profiles; WIFU families `144103/144104` and `144104/144104`, Energy15/ammo14, variable QL and SAW initialization | `20260721-031913`, `20260721-032547`, `20260721-033006`, `20260721-052115`, `20260721-230426` | Genuine weapon-template variant plus level-specific SAW initialization. |
| `26149` | 27 (two), 30 (two), 32, 34-35 | Seven complete profiles; WIFU families `124313/124314` and `124314/124314`; Energy/ammo variants `20/19` and `16/15`; L30 profiles `b73196d6390cb04e-36542c39514bffc3` and `b73196d6390cb04e-f75f789a125e1112` are genuine cross-weapon variants | `20260528-191120`, `20260721-031913`, `20260721-042139`, `20260721-052115`, `20260721-230426`, `20260721-232051` | Genuine weapon and ammo-state variants; two exact-level actors remain ambiguous; uncaptured levels lack exact SAW initialization. |

## Every resolver-rejected active row

`No exact profile` means the generated catalog contains no complete profile at
that exact MonsterData and level. `Ambiguous exact` names every compatible
candidate and preserves fail-closed selection.

| MonsterData | Level | Actors | Runtime source identities | Exact quarantine reason |
|---|---:|---:|---|---|
| `26074` | 20 | 3 | `0x79834DCE`, `0x7983FB3C`, `0x7984B36E` | No exact profile. |
| `26074` | 22 | 1 | `0x79834EC9` | No exact profile. |
| `26074` | 23 | 1 | `0x7983FB02` | No exact profile. |
| `26074` | 27 | 1 | `0x7983FC33` | No exact profile. |
| `26074` | 28 | 2 | `0x79834DDF`, `0x7983FB2A` | No exact profile. |
| `26074` | 33 | 1 | `0x79872FE9` | No exact profile. |
| `26074` | 35 | 1 | `0x79872BFA` | No exact profile. |
| `26082` | 20 | 1 | `0x7983FC46` | No exact profile. |
| `26082` | 21 | 2 | `0x79834AFB`, `0x79834D07` | No exact profile. |
| `26082` | 22 | 3 | `0x79834D0D`, `0x7983FBA5`, `0x7983FBA6` | No exact profile. |
| `26082` | 28 | 1 | `0x7984B3A8` | No exact profile. |
| `26082` | 34 | 1 | `0x7983FBA4` | No exact profile. |
| `26103` | 21 | 1 | `0x7983FBE7` | No exact profile. |
| `26103` | 23 | 1 | `0x79834E50` | No exact profile. |
| `26103` | 24 | 1 | `0x7983FBD0` | No exact profile. |
| `26103` | 26 | 1 | `0x7983FB03` | No exact profile. |
| `26103` | 28 | 1 | `0x79834DCF` | Ambiguous exact: `5643a4bd8a3aaf44-084c355903d1b399` (`129028/129029`, QL32, `20260721-052115`) versus `5643a4bd8a3aaf44-9ce155283cfcc36c` (same templates, QL30, `20260721-232051`); source matches neither. |
| `26103` | 29 | 1 | `0x7983FAE3` | No exact profile. |
| `26103` | 33 | 1 | `0x7987F149` | No exact profile. |
| `26135` | 20 | 1 | `0x7983F8FD` | No exact profile. |
| `26135` | 26 | 1 | `0x7984B3DF` | No exact profile. |
| `26135` | 28 | 2 | `0x79834DE9`, `0x7984B3AB` | No exact profile. |
| `26135` | 30 | 1 | `0x7983FB27` | Ambiguous exact: `fe392350897b358a-24a802cb7db6c581` (QL23) versus `fe392350897b358a-c4027eb3ce806767` (QL24), both `158298/158299` from `20260721-052115`; source matches neither. |
| `26135` | 35 | 4 | `0x7983FBB6`, `0x7983FBB8`, `0x7983FC3A`, `0x7985EE29` | No exact profile. |
| `26137` | 22 | 1 | `0x79834CC9` | No exact profile; adjacent captured levels have different SAW initialization. |
| `26137` | 24 | 1 | `0x7983FB3D` | No exact profile; adjacent captured levels have different SAW initialization. |
| `26137` | 25 | 1 | `0x7983FBA2` | No exact profile; adjacent captured levels have different SAW initialization. |
| `26137` | 31 | 2 | `0x79834DA8`, `0x7985316C` | No exact profile; L30 SAW initialization cannot be copied. |
| `26137` | 33 | 2 | `0x79834DF3`, `0x79853114` | No exact profile. |
| `26137` | 34 | 2 | `0x7983FB97`, `0x798537AE` | No exact profile. |
| `26137` | 35 | 1 | `0x79834CC7` | No exact profile. |
| `26147` | 21 | 1 | `0x7983FB9A` | No exact profile; generated family has two weapon-template variants. |
| `26147` | 22 | 2 | `0x7983FAC2`, `0x7983FAEF` | No exact profile; generated family has two weapon-template variants. |
| `26147` | 24 | 1 | `0x7983F8FE` | No exact profile; generated family has two weapon-template variants. |
| `26147` | 26 | 1 | `0x7983FB3E` | No exact profile; generated family has two weapon-template variants. |
| `26147` | 28 | 1 | `0x7983FB2E` | No exact profile; generated family has two weapon-template variants. |
| `26147` | 30 | 1 | `0x7985EC38` | No exact profile; generated family has two weapon-template variants. |
| `26147` | 35 | 4 | `0x79834CB9`, `0x7983FA5D`, `0x7983FBAD`, `0x7985EE30` | No exact profile; generated family has two weapon-template variants. |
| `26149` | 20 | 2 | `0x79822FDF`, `0x7983FBDA` | No exact profile. |
| `26149` | 21 | 1 | `0x7983FB8F` | No exact profile. |
| `26149` | 22 | 4 | `0x79834DCB`, `0x7983FB9F`, `0x7983FBA1`, `0x7983FC39` | No exact profile. |
| `26149` | 23 | 2 | `0x7983FAE0`, `0x7983FB43` | No exact profile. |
| `26149` | 24 | 1 | `0x79834B66` | No exact profile. |
| `26149` | 25 | 2 | `0x79834B77`, `0x7987F146` | No exact profile. |
| `26149` | 26 | 2 | `0x7983F9F4`, `0x7983FB85` | No exact profile. |
| `26149` | 28 | 1 | `0x7983FB40` | No exact profile. |
| `26149` | 29 | 2 | `0x7983FB88`, `0x7983FB93` | No exact profile. |
| `26149` | 30 | 2 | `0x7983FBE1`, `0x7983FC43` | Ambiguous exact and genuine cross-weapon distinction: `b73196d6390cb04e-36542c39514bffc3` uses `124313/124314` QL27 from `20260721-232051`; `b73196d6390cb04e-f75f789a125e1112` uses `124314/124314` QL32 from `20260721-031913`; neither runtime source distinguishes them. |
| `26149` | 31 | 1 | `0x79834DD0` | No exact profile. |
| `26149` | 33 | 1 | `0x79834DA6` | No exact profile. |

## Blocker totals

- `72` actors: no complete generated profile for their exact MonsterData and
  level; the available same-family levels have distinct exact SAW
  initialization and cannot be substituted.
- `2` actors: multiple same-template exact-level candidates with different WIFU
  QL and no source or production loadout selector that identifies one.
- `2` actors: genuine exact-level cross-weapon ambiguity.

No actor was enabled by a nearest-level, nearest-QL, identity, or cross-weapon
fallback.
