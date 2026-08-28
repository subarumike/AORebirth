# AO Enemy Archetype Census

## Result

The original client resource chain supports an exact census of reusable NPC visual records, complete visual signatures, and structural base-model families. It does not expose hostility/gameplay ownership for every MonsterData record, so the narrower count of enemy-only families remains unproven.

MonsterData is not treated as the archetype by itself. Names, levels, loot, ACG hashes, placements, and runtime identities are excluded from visual identity.

## Proven resource chain

```text
server-authored SimpleChar stat 359
  -> ResourceDatabase 1040023:<MonsterData>
  -> MonsterData stat 12 Mesh
  -> ResourceDatabase 1010002:<CATMesh>
  -> n3VisualDynel_t::SetCatMesh

MonsterData stat 64 HeadMesh -> head/skin setup
MonsterData group map 1 -> CAT-mesh animation/effect selection
```

ACG placement records are a separate official placement/spawn-policy corpus. The shipped client has no ACGHash-to-MonsterData resolver, so all current official placement-to-archetype rows remain unresolved rather than guessed.

## Counts

| Metric | Count |
| --- | ---: |
| Official placements | 32805 |
| Official NPC visual records (MonsterData) | 1470 |
| Unique official names | 1468 |
| Unique MonsterData IDs | 1470 |
| Unique referenced CATMesh IDs | 856 |
| Unique exact mesh-resource signatures | 856 |
| Unique CATMesh texture/material signatures | 795 |
| Unique complete visual signatures | 1360 |
| Structural base-model families | 750 |
| Captured runtime visual variants | 428 |
| Observed contextual name/level variants | 582 |

No single `ARCHETYPE_COUNT` is selected: exact complete visual signatures and broader structural families answer different questions, and neither can be narrowed to enemy-only records from MonsterData alone.

## Canonical signature methodology

- Exact CATMesh raw-resource hashes preserve full shipped mesh/material/texture differences.
- HeadMesh absence, observed zero, and concrete resource references remain distinct.
- MonsterData animation-map and Features states are included in complete visual signatures.
- Structural base families use decoded joint hierarchy and mesh-topology counts; they intentionally group   visible variants that share a body/skeleton structure.
- SCFU texture and mesh overrides preserve explicit slots and produce separate runtime visual variants.
- `1234567890` is rejected; missing fields never become zero.

## Leet case study

- Base-model families: 25
- Complete visual archetypes in those families: 28
- MonsterData IDs: 17655, 157180, 213108, 226880, 247801, 247829, 247831, 247832, 247834, 247835, 247836, 247837 (+17 more)
- CATMesh IDs: 15222, 157177, 213085, 226560, 247818, 247819, 247820, 247821, 247822, 247823, 247824, 247825 (+13 more)
- Official names: ai_cutecreature_draculeet, ai_cutecreature_draculeet_pet, ai_cutecreature_frankenleet, ai_cutecreature_frankenleet_pet, athleet, barleet, bob marleet, bruce leet, bulleet, calculeet, cheerleet, deleet (+17 more)
- Captured names: Beach Leet, Eleet, Flurryflutter the Phearsome Smasher, Flurryflutter the Troubled Smasher, Leet, Princess Leet, Redeye the Pheared, Redeyeflutter of the Phat Loot Wrecker, Redeyeflutter the Dood Wrecker, Shinyflutter the Phearsome, Shinyfoot the Supa Pheared Smasher, Swiftfoot the Frantic Exterminator (+1 more)
- Captured level range: 1..15
- ACG placement count: unresolved because the official client does not join ACGHash to MonsterData.

The literal `leet` token is used only to seed known records. The case study then expands through shared client visual families, allowing Beach Leet, Leet, Eleet, Soleet, and named variants to converge when their actual resources converge and remain separate when their resources differ.

## Heckler case study

- Base-model families: 1
- Complete visual archetypes in those families: 1
- MonsterData IDs: 290647
- CATMesh IDs: 290605
- Official names: heckler_of_celebration
- Captured names: none observed
- Captured level range: None..None
- ACG placement count: unresolved for the same official-source boundary.

## Top 20 reused complete visual signatures

| Archetype | Official records | Captures | MonsterData | Names |
| --- | ---: | ---: | --- | --- |
| `archetype-36410b296d8f7bd4` | 7 | 20 | 208640, 208641, 208642, 208643, 208644 (+2 more) | sl_barad-or, sl_celeth-or, sl_craig-or, sl_cron-or (+3 more) |
| `archetype-9d1b7110c7a11b80` | 7 | 3 | 208647, 208648, 208649, 208650, 208651 (+2 more) | sl_aile-len, sl_calath-len, sl_celeth-len, sl_culuth-len (+3 more) |
| `archetype-465562c30f4842e6` | 7 | 0 | 208842, 208849, 208856, 208863, 208870 (+2 more) | sl_celeth-suir, sl_erath-suir, sl_gorath-suir, sl_goroth-suir (+3 more) |
| `archetype-2bf3e47867d6669d` | 6 | 1 | 208523, 208531, 208544, 208551, 208635 (+1 more) | sl_calan-cur, sl_calun-cur, sl_celeth-cur, sl_eron-cur (+2 more) |
| `archetype-428d46d9615d1af5` | 6 | 0 | 208732, 208739, 208746, 208753, 208760 (+1 more) | sl_buran-kuir, sl_celeth-kuir, sl_evok-kuir, sl_ivok-kuir (+2 more) |
| `archetype-340a9c7e3b511f49` | 4 | 0 | 209229, 209238, 209245, 209252 | sl_malah-ana, sl_malah-behn, sl_malah-curran, sl_malah-dren |
| `archetype-927f9460b2b52d56` | 4 | 0 | 208558, 208604, 208612, 208630 | sl_cama-el, sl_celeth-el, sl_cor-el, sl_yor-el |
| `archetype-d04ef84dda1c1148` | 4 | 0 | 209143, 209150, 209158, 209165 | sl_essence eremite, sl_rippled eremite, sl_rotting eremite, sl_sparkling eremite |
| `archetype-3c10e29bfe9ed548` | 3 | 0 | 209347, 209354, 209361 | sl_weaver of decay, sl_weaver of shadow, sl_weaver of spirits |
| `archetype-adedbfad15f1cbaf` | 3 | 0 | 209117, 209125, 209136 | sl_burning shadow, sl_faded shadow, sl_icy shadow |
| `archetype-e072636bc8bb5239` | 3 | 0 | 247044, 247045, 247048 | sl_celeth-el_shoot, sl_cor-el_shoot, sl_yor-el_shoot |
| `archetype-4d9a0ec31c8218d6` | 2 | 58 | 203740, 215049 | jobe_traknavar, workman stiker |
| `archetype-1296981b7504c3fe` | 2 | 31 | 209173, 209180 | sl_crawlos, sl_creepos |
| `archetype-dbcdac694d276e41` | 2 | 19 | 17720, 218695 | mech dog - skin, sl_pet_predators |
| `archetype-fd408060ca9e93c8` | 2 | 15 | 209196, 209203 | sl_hiathlin, sl_hoathlan |
| `archetype-9be075a84e6629f8` | 2 | 13 | 209333, 209340 | sl_crippler of growth, sl_crippler of life |
| `archetype-9e74c23392ae4a2a` | 2 | 11 | 26135, 295564 | opifex thin male, opifex thin male - vernon torvalds |
| `archetype-2edd27cb8c8d919d` | 2 | 4 | 204985, 242322 | sl_david_marlin, solitusmale_ah_youngman |
| `archetype-0736d54b26316917` | 2 | 3 | 22821, 218783 | omni slayer droid, sl_pet_slayers |
| `archetype-8fd9935d3896d8d5` | 2 | 3 | 201533, 278499 | lctower_guard_small, unicorn commtower |

## ACG placement association

- Direct official: 0
- Indirect official: 0
- Ambiguous: 0
- Unresolved: 32805

The unresolved result is evidence, not a census failure: the client retains placement records while the server supplies the live MonsterData selector independently.

## Capture overlay

- Captured NPC observations: 3472
- Unique archetype: 3470
- Ambiguous archetype: 0
- Unknown archetype: 2

Runtime observations resolve through stable MonsterData/CATMesh evidence without requiring an exact placement. Runtime identities are retained only as observation provenance.

## Deduplication

The 1470 MonsterData records reduce to 1360 exact reusable visual variants and 750 broader structural families. Median MonsterData records per visual variant is 1.0; maximum is 7.

Placement-per-archetype statistics remain `not observed` because the official ACG-to-model association is absent. They are not reported as zero.

## Remaining unknown relationships

- Exact enemy-versus-friendly/structure ownership for every MonsterData record.
- The Funcom server/tooling ACGHash-to-MonsterData association.
- Four CATMesh records that the available AODB decoder cannot parse.
- MonsterData group map 2 and some grouped animation/effect semantics.
- Server-authored body, breed, gender, equipment, and texture overrides absent from MonsterData.

## Acceptance

```text
ENEMY_ARCHETYPE_CENSUS_IMPLEMENTED=YES
OFFICIAL_PLACEMENTS=32805
OFFICIAL_NPC_VISUAL_RECORDS=1470
UNIQUE_NAMES=1468
UNIQUE_MONSTER_DATA=1470
UNIQUE_CAT_MESH=856
UNIQUE_MESH_SIGNATURES=856
UNIQUE_TEXTURE_SIGNATURES=795
UNIQUE_COMPLETE_VISUAL_SIGNATURES=1360
BASE_MODEL_FAMILIES=750
VISUAL_VARIANTS=1360
GAMEPLAY_VARIANTS_IDENTIFIED=582
LEET_VISUAL_ARCHETYPES=28
LEET_NAMES=42
LEET_PLACEMENTS=NOT_OBSERVED_OFFICIAL_JOIN
LEET_PLAYFIELDS=7
HECKLER_VISUAL_ARCHETYPES=1
HECKLER_NAMES=1
HECKLER_PLACEMENTS=NOT_OBSERVED_OFFICIAL_JOIN
HECKLER_PLAYFIELDS=0
ACG_PLACEMENTS_DIRECT_TO_ARCHETYPE=0
ACG_PLACEMENTS_INDIRECT_TO_ARCHETYPE=0
ACG_PLACEMENTS_AMBIGUOUS=0
ACG_PLACEMENTS_UNRESOLVED=32805
CAPTURED_NPC_OBSERVATIONS=3472
CAPTURE_OBSERVATIONS_UNIQUE_ARCHETYPE=3470
CAPTURE_OBSERVATIONS_AMBIGUOUS_ARCHETYPE=0
CAPTURE_OBSERVATIONS_UNKNOWN_ARCHETYPE=2
RUNTIME_TO_EXACT_ACG_REQUIRED_FOR_ARCHETYPE=NO
ACGHASH_USED_AS_RUNTIME_IDENTITY=NO
NAMES_USED_AS_ARCHETYPE_IDENTITY=NO
LEVEL_USED_AS_ARCHETYPE_IDENTITY=NO
LOOT_USED_AS_VISUAL_ARCHETYPE_IDENTITY=NO
DETERMINISTIC_DIGEST=6b8c9b999ac7ca23f688a7fb64dae1a7fbc5651693bce8fad2187658004149c3
```
