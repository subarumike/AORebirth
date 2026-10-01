# AO Character Stats Handoff To Godot

Status: PROVEN current-source handoff with PARTIAL/UNRESOLVED boundaries separated.

## Scope

- Target consumer: `D:/anarchyonlinegodot`
- Source repository: `D:/AORebirth-fresh`
- Source SHA at audit start: `f82491dd210c925ecbf3e1313691c4d14bd57492`
- No Ghidra, no new reverse engineering, no generic AO knowledge, and no writes to `D:/anarchyonlinegodot`.
- Gameplay behavior was not modified.

## Machine-Readable Artifact

- `data/ao_character_stats_handoff.json`

## Recovery Summary

- Total stat IDs recovered: 712
- Current `CharacterStat` IDs recovered: 630
- Legacy `StatIds` IDs recovered: 705
- Default values recovered from `StatNamesDefaults`: 289
- Abilities recovered: 6
- Skills recovered: 69
- Fixed new-character stat rows recovered: 81
- Starter vital breed/profession pairs computed from proven formula: 56
- Formula sections exported: 12

## Full Stat Categories Recovered

- ability
- armor_class
- cash_trade
- combat_rating_modifier
- damage_modifier
- identity_creation
- melee_skills
- movement_detection_navigation
- nano_ncu
- nano_skills
- navigation_skills
- progression
- ranged_skills
- reflect_absorb
- scale_height
- skill
- speed_skills
- spying_skills
- trade_skills
- treatment_first_aid
- vitals
- weapon_skills

## Proven New-Character Values

- `NewCharacter.json` proves 81 fixed starting stat rows, including `Level=1`, `Side=0`, `TitleLevel=1`, `MaxNCU=8`, `AggDef=100`, `RunSpeed=5`, `BodyDevelopment=5`, `NanoPool=5`, and all 69 trainable skills at base 5.
- `CharacterName.cs` proves creation/account-dependent persisted rows for sex, head mesh, scale, visual sex/breed/profession, breed, profession, fatness, GM level, and expansion flags.
- `NewCharacter.json` proves breed starting abilities; `StarterVitalStats.cs` proves starting current/max health and nano from breed/profession/level/title-level/body-dev/nano-pool formula outputs.
- XP/IP/cash defaults are present in `StatNamesDefaults`, but explicit new-character persistence for XP/IP/cash was not proven; they are exported under PARTIAL default-only findings.

## Breed Differences

- Solitus: Strength=6, Agility=6, Stamina=6, Intelligence=6, Sense=6, Psychic=6
- Opifex: Strength=3, Agility=15, Stamina=6, Intelligence=6, Sense=10, Psychic=3
- Nanomage: Strength=3, Agility=3, Stamina=3, Intelligence=15, Sense=6, Psychic=10
- Atrox: Strength=15, Agility=6, Stamina=10, Intelligence=3, Sense=3, Psychic=3

Breed also contributes HP/nano formula base, multiplier, and modifier tables. Exact tables and computed starter values are in JSON.

## Profession Differences

Profession differences are proven for skill cost multipliers/cap tier selection and HP/nano profession rows. Sample skill-cost multipliers by profession:

- Adventurer: MartialArts=2.8, BodyDevelopment=1.2, NanoPool=1.6, ComputerLiteracy=1.6, RunSpeed=1.0, Treatment=1.0, FirstAid=1.2
- Agent: MartialArts=1.6, BodyDevelopment=2.4, NanoPool=1.2, ComputerLiteracy=1.6, RunSpeed=1.6, Treatment=2.0, FirstAid=2.0
- Bureaucrat: MartialArts=2.8, BodyDevelopment=2.4, NanoPool=1.4, ComputerLiteracy=1.0, RunSpeed=2.4, Treatment=2.0, FirstAid=2.0
- Doctor: MartialArts=2.0, BodyDevelopment=2.0, NanoPool=1.0, ComputerLiteracy=1.0, RunSpeed=2.4, Treatment=1.0, FirstAid=1.0
- Enforcer: MartialArts=1.6, BodyDevelopment=1.0, NanoPool=2.0, ComputerLiteracy=1.6, RunSpeed=2.4, Treatment=2.0, FirstAid=1.6
- Engineer: MartialArts=2.8, BodyDevelopment=2.4, NanoPool=1.8, ComputerLiteracy=1.3, RunSpeed=2.0, Treatment=1.6, FirstAid=2.0
- Fixer: MartialArts=2.8, BodyDevelopment=1.8, NanoPool=1.6, ComputerLiteracy=1.0, RunSpeed=1.0, Treatment=1.2, FirstAid=1.2
- Keeper: MartialArts=3.0, BodyDevelopment=1.2, NanoPool=2.2, ComputerLiteracy=2.4, RunSpeed=2.0, Treatment=1.8, FirstAid=1.2
- MartialArtist: MartialArts=1.0, BodyDevelopment=1.5, NanoPool=1.6, ComputerLiteracy=2.0, RunSpeed=1.0, Treatment=2.0, FirstAid=1.6
- Metaphysicist: MartialArts=2.8, BodyDevelopment=2.4, NanoPool=1.0, ComputerLiteracy=1.0, RunSpeed=2.4, Treatment=2.0, FirstAid=2.0
- Nanotechnician: MartialArts=2.8, BodyDevelopment=2.4, NanoPool=1.0, ComputerLiteracy=1.0, RunSpeed=2.4, Treatment=2.0, FirstAid=2.0
- Shade: MartialArts=1.6, BodyDevelopment=2.6, NanoPool=2.5, ComputerLiteracy=2.4, RunSpeed=1.0, Treatment=1.5, FirstAid=2.5
- Soldier: MartialArts=2.0, BodyDevelopment=1.1, NanoPool=2.0, ComputerLiteracy=2.0, RunSpeed=2.0, Treatment=2.0, FirstAid=2.0
- Trader: MartialArts=2.0, BodyDevelopment=2.0, NanoPool=1.2, ComputerLiteracy=1.5, RunSpeed=1.9, Treatment=1.6, FirstAid=1.6

The full 69-skill multiplier matrix and starter HP/nano by breed/profession are in JSON.

## Derived-Stat Formulas Exported

- `title_level`: PROVEN
- `total_ip_earned_at_level`: PROVEN
- `available_ip`: PROVEN
- `max_health`: PROVEN
- `max_nano`: PROVEN
- `skill_trickle`: PROVEN
- `ability_level_cap`: PROVEN
- `skill_level_cap_and_cost`: PROVEN
- `ncu_usage`: PROVEN
- `combat_weapon_damage_current`: PROVEN
- `computer_literacy_shop_pricing`: PROVEN
- `xp_thresholds`: PROVEN

## Persistence Classifications

- PERSISTED_AND_REBASED_DERIVED_RUNTIME
- PERSISTED_IF_PRESENT_OR_TEMPLATE_CONTEXT
- PERSISTED_NEW_CHARACTER
- RECOVERED_LEGACY_REGISTRY
- SERVER_ONLY_DERIVED_USED_IP

## PARTIAL / UNRESOLVED

- UNRESOLVED: ComputerLiteracy to NCU capacity - ComputerLiteracy is proven for shop pricing. NCU capacity is read from MaxNCU; no current evidence proves a ComputerLiteracy-derived NCU capacity formula.
- PARTIAL: New-character XP/IP/cash persistence - Defaults XP=0, IP=1500, Cash=0 exist in StatNamesDefaults, but NewCharacter.json/CharacterName.cs do not explicitly persist XP/IP/Cash rows. Player rebase derives level IP bonus and sends available IP.
- PARTIAL: Profession starter loadout stat effects - Profession starter loadout classes exist, but audited direct stat-row writes are NewCharacter stats, creation/account rows, breed abilities, and StarterVitalStats. Item/nano loadout effects are not exported as direct character stat values.
- UNRESOLVED: Reflect/absorb combat application - Reflect/absorb stat IDs/defaults are recoverable, but current weapon DamageCalculator does not apply reflect/absorb reduction.
- PARTIAL: Nano damage type 168 current production mapping - A fixture maps raw nano damage type 168 to NanoAC/NanoDamageModifier, but current DamageCalculator maps only 90-97 for weapon damage.
- PARTIAL: Legacy SkillTrickleTable versus current SkillCatalog - Legacy SkillTrickleTable remains, but current ZoneEngine_New rebase uses SkillCatalog with GameData/SkillTrickle.json.
- PARTIAL: Context-sensitive item/nano stat IDs - Some IDs are reused in template context, for example nano stat 54 as NCU cost while character stat 54 is Level.

## Exact AORebirth Source/Evidence Files Used

- `AI_START_HERE.md`
- `AGENTS.md`
- `docs/project/DEVELOPMENT_AUTHORITY.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/KNOWN_DECISIONS.md`
- `AORebirth/Libraries/Source/AORebirth.Enums/StatIds.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/GameData/CharacterStat.cs`
- `AORebirth/Libraries/Source/AORebirth.Stats/StatNamesDefaults.cs`
- `AORebirth/Libraries/Source/AORebirth.Enums/Breed.cs`
- `AORebirth/Libraries/Source/AORebirth.Enums/Profession.cs`
- `AORebirth/GameData/NewCharacter.json`
- `AORebirth/GameData/AbilityCosts.json`
- `AORebirth/GameData/SkillCosts.json`
- `AORebirth/GameData/SkillTrickle.json`
- `AORebirth/GameData/Xp.json`
- `AORebirth/Server/LoginEngine/CharacterCreation/NewCharacterStats.cs`
- `AORebirth/Server/LoginEngine/CharacterCreation/StarterVitalStats.cs`
- `AORebirth/Server/LoginEngine/Packets/CharacterName.cs`
- `AORebirth/Server/LoginEngine/Packets/CreateCharacterHandler.cs`
- `AORebirth/Server/LoginEngine/CharacterCreation/*.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Characters/CharacterHydrationService.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Characters/CharacterHydrationValidator.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Persistence/MySqlStatRepository.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Persistence/SharedCharacterPersistence.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Persistence/StatRecord.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Entities/StatCollection.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Entities/Character.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Entities/Player.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Entities/PlayerHydrator.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Stats/SkillCatalog.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Stats/SkillTraining.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Stats/MaxHealthCalculator.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Stats/MaxNanoCalculator.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Nanos/BuffApplyRules.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Nanos/NanoSpell.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Helpers/DamageCalculator.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Trade/TradeRules.cs`
- `AORebirth/Tests/ZoneEngine.New/SkillTrainingTests.cs`
- `AORebirth/Tests/Fixtures/Gameplay/DamageCalculation.cs`
- `AORebirth/Documentation/Stats.md`
- `docs/reports/NEWENGINE_RETAIL_SPAWN_FIELD_MATRIX.md`
- `docs/evidence/ZONEENGINE_NEW_NANOS_RECONCILIATION.md`
- `docs/evidence/ZONEENGINE_NEW_NANO_GAP_CLOSURE.md`
- `docs/evidence/DELMUS_D2D98446_RUNTIME_RECONCILIATION.md`

## Validation

- JSON generated from parsed AORebirth source/data files, not hand-filled generic AO knowledge.
- Authoritative JSON fields contain only PROVEN current-source values/formulas; PARTIAL/UNRESOLVED findings are isolated in `partial_unresolved_findings` and default-only sections.
- No gameplay, config, database schema, or `D:/anarchyonlinegodot` files were modified.
