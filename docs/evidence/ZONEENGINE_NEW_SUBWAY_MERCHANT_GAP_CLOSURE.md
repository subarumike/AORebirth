# ZoneEngine_New Subway merchant and NPC inventory gap closure

## Source inventory boundary

Read-only projection of the complete checked-in
`docs/generated/capture_backed_npc_combat_active_coverage.json` at the
`05c8d4ef7429e1ed765a19bd63ace0e21a2b29e5` starting checkpoint:
1,551 binding records / 1,565 actors. Certified combat: 545 records / 559 actors.
Unresolved combat: 1,006 records / 1,006 actors. These are **combat** classifications,
not permission to suppress the accepted actors' appearance or social behavior.
The source JSON retains every exact binding key, original identity, level, source,
profile selector and resource; the table does not replace those per-record keys.

| Accepted surface | Bindings | Actors | Combat-ready bindings | Exact Legacy source owner |
| --- | ---: | ---: | ---: | --- |
| Arete additional | 17 | 17 | 4 | AreteFinishCaptureMobRuntime / AreteLandingSpawn / CapturedAreteRobotContentProvider |
| Arete family | 83 | 96 | 39 | AlexAreaMobRuntime / AreteLandingSpawn / JunkyardCleaningRobotRuntime / LoreleiOasisMobRuntime / MarcusPadAmbientCombat |
| Nascence core hecklers | 40 | 40 | 0 | NascenceCoreHecklerContentProvider |
| Nascence life | 868 | 868 | 0 | NascenceLifeContentModule / NascenceLifeSpawn |
| Rome Blue | 22 | 22 | 0 | RomeBlueCitySpawn |
| Subway initial encounters | 3 | 3 | 0 | CapturedSubwayEncounterRuntimeService |
| Subway merchants | 6 | 6 | 0 | CapturedSubwayVendorContentProvider / CapturedSubwayVendorRuntimeService |
| Subway ordinary | 322 | 322 | 322 | CapturedSubwayContentProvider / CapturedSubwayOrdinaryContentProvider |
| Temple named | 12 | 12 | 12 | CapturedTempleOfThreeWindsEncounterRuntimeService |
| Temple ordinary | 167 | 167 | 167 | CapturedTempleOfThreeWindsContentProvider |
| Temple corpse adds | 1 | 2 | 1 | CapturedTempleOfThreeWindsEncounterRuntimeService |
| Thrak garden | 10 | 10 | 0 | ThrakOmniGardenSpawn |

The bounded independent source check proves the distinction directly:
`RomeBlueCitySpawn` creates actors, applies exact level/HP/visual/texture/mesh data,
registers unresolved combat, and still activates/announces them. Thrak does likewise,
with additional source-defined side/profession/breed/runspeed/flags and captured buff
projection. Missing combat certification alone is not an actor-data gap. Root owns
the remaining per-surface New adapters and final NPC readiness decision.

## Exact six-merchant repair

`EXISTING_AREA_REOPENED=accepted Subway social/vendor activation`

`WHY_REQUIRED=All six compiled Legacy merchants have supported placements, appearance, owner-to-terminal links and stock despite unresolved combat; the checkpoint has no corresponding New actor/stock consumer.`

`BLOCKER_THAT_PROVED_IT=CapturedSubwayVendorRuntimeService.Spawn/CreateCharacter/TryCreateVendor and SimpleCharFullUpdate captured-vendor branch versus New accepted placement catalog.`

| Source NPC | Source endpoint | Name | Vendor template | Accepted dialogue |
| --- | --- | --- | ---: | --- |
| 79135F51 | 12ECC394 | Tailor | 99637 | Exact Tailor tree; dialogue domain owns interaction |
| 79135F52 | 12ECC395 | Basic Quality Weaponsdealer | 99572 | None promoted |
| 79135F53 | 12ECC396 | Basic Quality Armorer | 99570 | None promoted |
| 79135F54 | 12ECC397 | Basic Quality Pharmacist | 99574 | None promoted |
| 79135F55 | 12ECC398 | Basic Tools Merchant | 99601 | None promoted |
| 79135F56 | 12ECC399 | Container Supplier | 99634 | None promoted |

`AcceptedSubwayMerchantCatalog` consumes the same pure compiled provider, not a
copy of raw captures, names, or native-template guesses. Every merchant preserves
its exact XYZ/quaternion, appearance value, breed/sex/MonsterData/head, textures,
meshes, flags/unknown SCFU bytes and source-defined waypoint projection. The active
Legacy constructor freezes level180, HP17841, runspeed448, Family0/LOS0 and explicit
movement/visual fields. New uses those fields verbatim, forces SimpleNpcInfo even
for Family0, and neither rebases a guessed level nor supplies unarmed retaliation.

The existing active Legacy stock is the provider's complete baseline: **202 exact
slot/low/high/QL rows across six endpoints**. Alternate captured snapshots remain
evidence, not an invented random refresh policy. Catalog validation requires the
vendor and both item endpoints before exposing a shop. A missing endpoint refuses
the entire shop while preserving the accepted social actor, as the Legacy consumer
does; the generic ItemBuilder's Unknown/missing-high fallbacks are not authority.
Root owns the sealed accepted-stock and activation hooks; existing TradeService
continues to own purchases, cash/inventory commit and shop lifetime.

Legacy merchants set DoNotDoTimers=true and unresolved passive combat. New does not
invent patrol, regeneration or retaliation; the single pharmacist waypoint remains
a captured wire projection. The exact merchant source has no separate respawn or
merchant loot policy. This adapter adds neither; shared death/lifetime handling is
coordinated with root. Unresolved merchant combat is deliberately still quarantined.

## Files inspected and changed

Inspected: the complete combat inventory JSON; exact Rome/Thrak spawn constructors;
Subway vendor provider/DTOs, runtime service, interaction handler, registry references
and SCFU branch; New accepted activation/catalog, NpcCharacter/Character projection,
VendingMachine/ShopStock/TradeService, item template lookup and focused test doubles.

Owned changes: `Core/Mobs/AcceptedSubwayMerchantCatalog.cs`,
`ZoneEngine_New.Tests/AcceptedSubwayMerchantTests.cs`,
`ZoneEngine_New.Tests/AcceptedSubwayShopRuntimeTests.cs` and this report. Root owns
shared links/activation/stock hooks; dialogue domain owns Tailor interaction tests.

## Validation boundary

Four deterministic catalog tests cover all six exact projections, all 202 stock
rows/owner-endpoint links, missing endpoint refusal without actor suppression,
and passive/no-rebase/no-patrol behavior. Four additional service-path tests use the
actual accepted actor activation, shop open, purchase/sale and durable commit route,
including stale actor/world/session and absent real catalog refusal. In the fifth
combined checkpoint, catalog and mission cases passed; two positive service-path
cases exposed a fixture omission: the simulated Player had not been registered in
the playfield registry. The fixture now mirrors real ownership with Register(Player);
the runtime exact-owner guard remains unchanged. The final coordinated rerun is
**420/420 tests PASS**, including the repaired service-path cases and actual packaged
item-catalog/pricing validation. Current composition is 22 accepted NPC adapters,
19 commercial NPC shops, and 3 separate standalone shops; Stan/Sarah quest hand-ins
are not commercial vendor capabilities. This source-owned six-actor subset retains
its exact 202 baseline stock rows; broader vendor counts are not extra Subway NPCs.
No production database, migrations, live client or capture tool used.
This six-actor repair does not certify all 1,565 inventory actors or overall master
readiness; final cross-domain/current-SHA acceptance remains a coordinating gate.
