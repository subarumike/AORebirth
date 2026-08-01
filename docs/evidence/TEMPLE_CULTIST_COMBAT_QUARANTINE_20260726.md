# Temple Cultist combat quarantine audit — 2026-07-26

> **PF1931 status authority (2026-08-01):** Historical evidence/provenance only. Current PF1931 status is the [Temple acceptance matrix](PF1931_TEMPLE_ACCEPTANCE_MATRIX_20260801.md); any PF1931 completion, blocker, or test-count statement below is superseded by that matrix.

> Superseded on 2026-07-28 by
> `docs/evidence/TEMPLE_ORDINARY_COMBAT_COMPLETION_20260728.md`. The earlier
> conclusion below predated the exact active WIFU catalog and bounded
> cross-family SpecialAttackWeapon formula proof. All `76` Cultists listed in
> this historical audit are now restored; the table remains as provenance for
> the starting quarantine.

## Outcome

No remaining Cultist cohort can be restored from the generated profiles without
inventing or borrowing an uncaptured `SpecialAttackWeapon` initialization.
Production combat behavior is therefore unchanged.

The authoritative actor-based PF127/PF1931 denominator is `489` unique active
spawns: `313` certified and `176` quarantined. PF1931 contains `149` unique
Cultist actors: `73` certified and `76` quarantined. There are `76` Cultist
rejection rows because the resolver is invoked exactly once for each unique
Cultist spawn and every failed invocation contributes one row. The generated
catalog contains `50` Cultist profile rows; those evidence profiles are not
runtime actors and are not part of the `149`-actor denominator.

The previous `376` certified / `113` quarantined checkpoint and its `70`
remaining Cultists were stale incrementally maintained documentation. They do
not reconcile to the current resolver over the current `489` unique actors.
There are no duplicate Cultist spawn keys, duplicate playfield/source-identity
pairs, or multiple rejection records for one actor. The six-row `76` versus
`70` difference was therefore stale documentation, not rejection-row inflation.

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

## MD26137 serialized SAW field map

Byte ranges below are zero-based offsets in the serialized 37-byte
`SpecialAttackWeapon` message body.

| Bytes | Serializer field | L21 | L23 | L26 | L28 | L29 | L30 | Classification and current owner |
|---|---|---:|---:|---:|---:|---:|---:|---|
| `0..3` | message type | `1D3C0F1C` | same | same | same | same | same | Constant serializer-owned N3 message type. |
| `4..7` | source identity type | `0000C350` | same | same | same | same | same | Constant `SimpleChar` identity type supplied by runtime actor identity. |
| `8..11` | source identity instance | `0x7984B52E` | `0x7983FB9B` | `0x79834ECD` | `0x7984B374`, `0x7983FC37`, or `0x79834EC1` | `0x7987F038` | `0x7984B543` or `0x7987F630` | Generation-local runtime identity; not a reusable combat-profile value. |
| `12` | N3 `Unknown` | `0` | `0` | `0` | `0` | `0` | `0` | Constant capture-bound field copied by the packet factory. |
| `13..16` | empty X3F1 `Specials` array | `000003F1` | same | same | same | same | same | Constant capture-bound empty array. |
| `17..20` | `Unknown1` | `320` | `351` | `400` | `434` | `450` | `468` | Level-dependent, capture-bound, no production owner. |
| `21..24` | `Unknown2` | `320` | `351` | `400` | `434` | `450` | `468` | Level-dependent, capture-bound, no production owner. |
| `25..28` | `Unknown3` | `320` | `351` | `400` | `434` | `450` | `468` | Level-dependent, capture-bound, no production owner. |
| `29..32` | `Unknown4` | `12` | `13` | `16` | `17` | `17` | `18` | Level-dependent, capture-bound, no production owner. |
| `33..36` | `Unknown5` | initial `0`; observed `0,13` | `0` | `0` | `0` | `0` | initial `0`; observed `0,20,0` | Initial value is capture-bound. Subsequent values are replayed only from the selected contract's ordered per-actor observation cursor. No generic missing-level source exists. |

The exact initial SAW field tuples are therefore:

| Level | Generated profile | Initial tuple `Unknown1:Unknown2:Unknown3:Unknown4:Unknown5` | Capture sessions |
|---:|---|---|---|
| 21 | `8dae5024f999475e-03dc0b29328f8462` | `320:320:320:12:0` | `20260721-052115` |
| 23 | `58e682ec3ebb63c8-b677b63db5c16f15` | `351:351:351:13:0` | `20260721-032547` |
| 26 | `a84eedad1a598b40-c4f21242a4a7ba96` | `400:400:400:16:0` | `20260721-052115` |
| 28 | `e30508b3b9b8e352-a87351572a2f5f23` | `434:434:434:17:0` | `20260721-031913`, `20260721-052115` |
| 29 | `3ec94c5698a809ab-d38e253d2479c92e` | `450:450:450:17:0` | `20260721-230426` |
| 30 | `2ee43d964a95a575-9ca73d7846c38f6c` | `468:468:468:18:0` | `20260721-052115`, `20260722-042930` |

## Production-source gate

| Candidate authoritative source | What it actually owns | Six-level result |
|---|---|---|
| `AORebirth/Datafiles/items.dat` through `ItemLoader` and `Item` | Direct load of canonical template `204747` proves QL `1`, attack skill `105:100`, template RechargeDelay/stat `210=320`, AttackDelay/stat `294=280`, and no per-level record. Template low/high are both `204747`; with identical low/high templates, `Item.GetAttribute` returns the one low-template value rather than a level-dependent interpolation. The SAW serializer does not read item-template stats. | Rejected. Template stat `210=320` numerically matches `Unknown1..3` only at L21, but misses the other five levels and supplies no `Unknown4` mapping. It reproduces `0/6` complete tuples and no code maps that item stat to SAW. |
| Active ordinary spawn construction | `CapturedTempleOfThreeWindsContentProvider` supplies level, health, scale, RunSpeed, appearance, and source identity. `OrdinaryEnemyRuntimeService.ApplyStats` installs those actor stats. | Rejected. No SAW field or independently validated SAW calculation exists in this path. |
| Equipped-item instance and WIFU builder | `CapturedEnemyCombatContract.TryEquipCapturedWeapon` constructs QL1 item `204747/204747`. `ApplyCapturedWeaponStats` owns Flags, MultipleCount, Energy, AttackDelay, and RechargeDelay; for these captures it overwrites item delay/recharge to `235/235` at every level. | Rejected. These values reproduce captured WIFU, not SAW `Unknown1..4`; all six item instances have the same template, QL, and `235/235` runtime delay/recharge values. |
| Player combat-start SAW generation | `AttackMessageHandler` selects one captured melee constant tuple or one captured ranged-special tuple based on equipped-item capability. | Rejected. It is player-only, not level-dependent, and neither constant tuple matches any MD26137 captured tuple. |
| Shared captured-NPC packet factory | `CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon` copies `Unknown1..5` directly from its selected contract arguments. | Exact `6/6` only when the six captured contracts themselves are supplied. It derives `0/6` missing-level tuples and is not an authoritative production calculator. |
| Per-actor captured runtime state | `NpcCombatTickCoordinator` advances only `Unknown5` through the selected contract's ordered observation array. Captured weapon Energy/ammunition are separately actor-owned. | Rejected for missing levels. It owns mutable replay state after exact contract selection, but supplies no `Unknown1..4` values and no missing-level `Unknown5` observation sequence. |

The six-level proof therefore failed the implementation gate. Exact-byte tests
confirm that all six existing contracts still reproduce their captured
WIFU -> SAW -> Attack -> AttackInfo bodies, including the distinct SAW values,
but no source other than those level-specific capture contracts supplies the
varying fields. No formula was inferred from the six points, no adjacent level
was copied, and no production combat code was changed.

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

## Authoritative count reconciliation

| Scope | Actor denominator | Certified actors | Quarantined actors | Rejection rows | Generated profile rows |
|---|---:|---:|---:|---:|---:|
| PF127 | `322` | `226` | `96` | `96` | not an actor denominator |
| PF1931 | `167` | `87` | `80` | `80` | not an actor denominator |
| PF127 + PF1931 | `489` | `313` | `176` | `176` | not an actor denominator |
| PF1931 Cultists only | `149` | `73` | `76` | `76` | `50` |

Permanent regression coverage asserts:

- uniqueness of `playfield + SpawnKey` and `playfield + source identity`;
- one resolver classification per active actor;
- `certified + quarantined = actor denominator`;
- one rejection row per quarantined actor;
- `72 + 2 + 2 = 76` Cultist blockers;
- `73 + 76 = 149` Cultist actors;
- `313 + 176 = 489` active dungeon actors.

## Validation

- Focused MD26137 capture-bound source gate: `1/1` pass.
- Focused Cultist actor-count reconciliation: `1/1` pass.
- Focused MD26137 exact four-packet replay: `1/1` pass.
- Full combat profile catalog: `42/42` pass.
- Captured combat packet factory: `34/34` pass.
- Generated exact-byte packet fixtures: `3/3` pass.
- Temple ordinary content: `6/6` pass.
- Focused Subway combat regressions: `5/5` pass.
- World population foundation: `35/39` pass. The four failures are unrelated
  existing expectations in source-aware fallback preservation, Mugger damage
  shape, Incomplete Rebuild damage shape, and Deranged Shopper damage shape.
- Complete messaging suite: `509/551` pass, `42` unrelated failures. The
  failures remain in pre-existing damage-policy expectations, stale generated
  coverage input hashes, inventory-route ownership assertions, missing deployed
  content fixtures, world-population expectations, and visibility row/hook
  expectations; none is in the changed MD26137 or count-reconciliation tests.
- Debug build: pass.
- `git diff --check`: pass.
- Engine restart: pass; ports `6996`, `7012`, `7500`, and `7501` listening.
