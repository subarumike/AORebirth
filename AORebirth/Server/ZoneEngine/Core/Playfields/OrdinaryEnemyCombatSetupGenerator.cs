namespace AORebirth.Core.Playfields
{
    using System;

    using ZoneEngine.Core.Playfields;

    /// <summary>
    /// Produces only numeric combat state whose exact runtime formula has been
    /// independently proven. Weapon identity and packet semantics remain selected
    /// by the capture-backed catalog.
    /// </summary>
    internal static partial class OrdinaryEnemyCombatSetupGenerator
    {
        internal const string DisobedientBotFormulaId =
            "disobedient-bot-siw1-floor-19L-plus-28-over-4-v1";

        internal const int DisobedientBotMinimumLevel = 5;

        internal const int DisobedientBotMaximumLevel = 10;

        internal const string StimFiendFormulaId =
            "stim-fiend-siw1-floor-11L-minus-2-over-2-v1";

        internal const int StimFiendMinimumLevel = 9;

        internal const int StimFiendMaximumLevel = 17;

        internal const string FilthFleaFormulaId =
            "filth-flea-saw-bounded-level-piecewise-v1";

        internal const int FilthFleaMinimumLevel = 4;

        internal const int FilthFleaMaximumLevel = 21;

        internal const string ViolentVagabondFormulaId =
            "violent-vagabond-saw-bounded-affine-floor-v1";

        internal const int ViolentVagabondMinimumLevel = 6;

        internal const int ViolentVagabondMaximumLevel = 10;

        internal const string MeldedPatternsFormulaId =
            "melded-patterns-saw-floor-11L-minus-2-over-2-plus-28-v1";

        internal const int MeldedPatternsMinimumLevel = 18;

        internal const int MeldedPatternsMaximumLevel = 25;

        internal const string FragmentedSoulFormulaId =
            "fragmented-soul-saw-6L-minus-1-plus-2-floor-L-over-2-v1";

        internal const int FragmentedSoulMinimumLevel = 17;

        internal const int FragmentedSoulMaximumLevel = 21;

        internal const string IncompleteRebuildFormulaId =
            "incomplete-rebuild-saw-6L-plus-1-minus-2-v1";

        internal const int IncompleteRebuildMinimumLevel = 17;

        internal const int IncompleteRebuildMaximumLevel = 22;

        internal const string MolestedMoleculesFormulaId =
            "molested-molecules-saw-floor-11L-minus-2-over-2-v1";

        internal const int MolestedMoleculesMinimumLevel = 17;

        internal const int MolestedMoleculesMaximumLevel = 25;

        internal const string EternalSentinelFormulaId =
            "eternal-sentinel-saw-floor-11L-minus-2-over-2-plus-floor-L-plus-4-over-2-v1";

        internal const int EternalSentinelMinimumLevel = 18;

        internal const int EternalSentinelMaximumLevel = 20;

        internal const string TempleCultistFormulaId =
            "temple-cultist-saw-bounded-level-piecewise-v1";

        internal const string TempleCultistRaisedPrimaryFormulaId =
            "temple-cultist-26135-saw-bounded-level-piecewise-plus-20-v1";

        internal const int TempleCultistMinimumLevel = 20;

        internal const int TempleCultistMaximumLevel = 35;

        private const int RightHandWeaponSlot = 6;

        private static readonly OrdinaryEnemyEquippedFormulaDomain[]
            EquippedFormulaDomains =
            {
                new OrdinaryEnemyEquippedFormulaDomain(
                    OrdinaryEnemyEquippedFormulaKind.ViolentVagabond,
                    ViolentVagabondFormulaId,
                    127,
                    203733,
                    ViolentVagabondMinimumLevel,
                    ViolentVagabondMaximumLevel,
                    RightHandWeaponSlot,
                    "130590"),
                new OrdinaryEnemyEquippedFormulaDomain(
                    OrdinaryEnemyEquippedFormulaKind.MeldedPatterns,
                    MeldedPatternsFormulaId,
                    127,
                    NpcCombatAttackRules.CapturedSubwayMeldedPatternsMonsterData,
                    MeldedPatternsMinimumLevel,
                    MeldedPatternsMaximumLevel,
                    NpcCombatAttackRules.CapturedSubwayMeldedPatternsWeaponSlot,
                    "121817..121820"),
                new OrdinaryEnemyEquippedFormulaDomain(
                    OrdinaryEnemyEquippedFormulaKind.FragmentedSoul,
                    FragmentedSoulFormulaId,
                    127,
                    NpcCombatAttackRules.CapturedSubwayFragmentedSoulMonsterData,
                    FragmentedSoulMinimumLevel,
                    FragmentedSoulMaximumLevel,
                    NpcCombatAttackRules.CapturedSubwayFragmentedSoulWeaponSlot,
                    "123685..123688"),
                new OrdinaryEnemyEquippedFormulaDomain(
                    OrdinaryEnemyEquippedFormulaKind.IncompleteRebuild,
                    IncompleteRebuildFormulaId,
                    127,
                    NpcCombatAttackRules.CapturedSubwayIncompleteRebuildMonsterData,
                    IncompleteRebuildMinimumLevel,
                    IncompleteRebuildMaximumLevel,
                    RightHandWeaponSlot,
                    "122653..122656"),
                new OrdinaryEnemyEquippedFormulaDomain(
                    OrdinaryEnemyEquippedFormulaKind.MolestedMolecules,
                    MolestedMoleculesFormulaId,
                    127,
                    NpcCombatAttackRules.CapturedSubwayMolestedMoleculesMonsterData,
                    MolestedMoleculesMinimumLevel,
                    MolestedMoleculesMaximumLevel,
                    RightHandWeaponSlot,
                    "122216..122219"),
                new OrdinaryEnemyEquippedFormulaDomain(
                    OrdinaryEnemyEquippedFormulaKind.EternalSentinel,
                    EternalSentinelFormulaId,
                    CapturedTempleOfThreeWindsContentProvider.PlayfieldInstance,
                    41690,
                    EternalSentinelMinimumLevel,
                    EternalSentinelMaximumLevel,
                    RightHandWeaponSlot,
                    "123381..123384"),
                TempleCultistDomain(26074, "204747"),
                TempleCultistDomain(26082, "130163..130164"),
                TempleCultistDomain(26103, "129028..129029"),
                new OrdinaryEnemyEquippedFormulaDomain(
                    OrdinaryEnemyEquippedFormulaKind.TempleCultistRaisedPrimary,
                    TempleCultistRaisedPrimaryFormulaId,
                    CapturedTempleOfThreeWindsContentProvider.PlayfieldInstance,
                    26135,
                    TempleCultistMinimumLevel,
                    TempleCultistMaximumLevel,
                    RightHandWeaponSlot,
                    "158298..158299"),
                TempleCultistDomain(26137, "204747"),
                TempleCultistDomain(26147, "144103..144104"),
                TempleCultistDomain(26149, "124313..124314")
            };

        internal static bool TryGenerate(
            OrdinaryEnemyCombatSetupInput input,
            out OrdinaryEnemyCombatNumericSetup setup)
        {
            setup = null;
            if (input == null)
            {
                return false;
            }

            if (MatchesCategoricalInput(
                    input,
                    NpcCombatAttackRules.CapturedSubwayDisobedientBotMonsterData,
                    DisobedientBotMinimumLevel,
                    DisobedientBotMaximumLevel,
                    NpcCombatAttackRules.CapturedSubwayDisobedientBotLowTemplate,
                    NpcCombatAttackRules.CapturedSubwayDisobedientBotHighTemplate,
                    NpcCombatAttackRules.CapturedSubwayDisobedientBotWeaponTag,
                    NpcCombatAttackRules.CapturedSubwayDisobedientBotWeaponName))
            {
                // Exact positive-integer floor division. All five captured levels
                // reproduce exactly; the bounded L7 result is 40.
                int value = checked((19 * input.ActorLevel) + 28) / 4;
                setup = RepeatedSetup(DisobedientBotFormulaId, value);
                return true;
            }

            if (MatchesCategoricalInput(
                    input,
                    NpcCombatAttackRules.CapturedSubwayStimFiendMonsterData,
                    StimFiendMinimumLevel,
                    StimFiendMaximumLevel,
                    NpcCombatAttackRules.CapturedSubwayStimFiendLowTemplate,
                    NpcCombatAttackRules.CapturedSubwayStimFiendHighTemplate,
                    NpcCombatAttackRules.CapturedSubwayStimFiendWeaponTag,
                    NpcCombatAttackRules.CapturedSubwayStimFiendWeaponName))
            {
                // Exact positive-integer floor division. L10..L14 reproduce the
                // complete Stim Fiend captures; the L17 result is held inside the
                // cross-family-proven standard SIW1 L10..L22 interval.
                int value = checked((11 * input.ActorLevel) - 2) / 2;
                setup = RepeatedSetup(StimFiendFormulaId, value);
                return true;
            }

            return false;
        }

        internal static bool TryGenerateFilthFlea(
            int actorLevel,
            out OrdinaryEnemyCombatNumericSetup setup)
        {
            setup = null;
            if (actorLevel < FilthFleaMinimumLevel
                || actorLevel > FilthFleaMaximumLevel)
            {
                return false;
            }

            int value = actorLevel <= 10
                ? checked((21 * actorLevel) + 28) / 4
                : checked((6 * actorLevel) - 1);
            setup = RepeatedSetup(FilthFleaFormulaId, value);
            return true;
        }

        internal static bool TryGenerateEquipped(
            OrdinaryEnemyEquippedCombatSetupInput input,
            out OrdinaryEnemyCombatNumericSetup setup)
        {
            setup = null;
            if (input == null)
            {
                return false;
            }

            foreach (OrdinaryEnemyEquippedFormulaDomain domain in
                EquippedFormulaDomains)
            {
                if (!domain.Matches(input))
                {
                    continue;
                }

                setup = domain.Generate(input.ActorLevel);
                return true;
            }

            return false;
        }

        internal static bool TryGetEquippedFormulaDomain(
            int monsterData,
            out OrdinaryEnemyEquippedFormulaDomain domain)
        {
            domain = Array.Find(
                EquippedFormulaDomains,
                value => value.MonsterData == monsterData);
            return domain != null;
        }

        internal static bool MatchesGeneratedEquippedSetup(
            int monsterData,
            int actorLevel,
            CapturedEnemyCombatContract contract,
            out OrdinaryEnemyCombatNumericSetup setup)
        {
            setup = null;
            if (contract == null
                || contract.AttackModel != CapturedEnemyAttackModel.EquippedWeapon
                || !contract.UsesProductionEquippedWeaponValues
                || !TryGenerateEquipped(
                    new OrdinaryEnemyEquippedCombatSetupInput(
                        monsterData,
                        actorLevel,
                        contract.WeaponLowId,
                        contract.WeaponHighId,
                        contract.WeaponQuality,
                        contract.WeaponInventorySlot),
                    out setup))
            {
                return false;
            }

            return contract.SpecialAttackWeaponUnknown1
                   == setup.SpecialAttackWeaponUnknown1
                   && contract.SpecialAttackWeaponUnknown2
                   == setup.SpecialAttackWeaponUnknown2
                   && contract.SpecialAttackWeaponUnknown3
                   == setup.SpecialAttackWeaponUnknown3
                   && contract.SpecialAttackWeaponUnknown4
                   == setup.SpecialAttackWeaponUnknown4;
        }

        private static OrdinaryEnemyEquippedFormulaDomain TempleCultistDomain(
            int monsterData,
            string weaponFamilyId)
        {
            return new OrdinaryEnemyEquippedFormulaDomain(
                OrdinaryEnemyEquippedFormulaKind.TempleCultist,
                TempleCultistFormulaId,
                CapturedTempleOfThreeWindsContentProvider.PlayfieldInstance,
                monsterData,
                TempleCultistMinimumLevel,
                TempleCultistMaximumLevel,
                RightHandWeaponSlot,
                weaponFamilyId);
        }

        internal static bool MatchesGeneratedSetup(
            int monsterData,
            int actorLevel,
            CapturedEnemyCombatContract contract,
            out OrdinaryEnemyCombatNumericSetup setup)
        {
            setup = null;
            if (contract == null
                || contract.AttackModel != CapturedEnemyAttackModel.Specialized
                || !contract.UsesProductionSpecializedValues
                || contract.CapturedSpecialAttacks == null)
            {
                return false;
            }

            if (monsterData
                    == NpcCombatAttackRules.CapturedSubwayFilthFleaMonsterData
                && contract.CapturedSpecialAttacks.Length == 2
                && FilthFleaSpecialsMatch(contract.CapturedSpecialAttacks)
                && TryGenerateFilthFlea(actorLevel, out setup))
            {
                return contract.SpecialAttackWeaponUnknown1
                           == setup.SpecialAttackWeaponUnknown1
                       && contract.SpecialAttackWeaponUnknown2
                           == setup.SpecialAttackWeaponUnknown2
                       && contract.SpecialAttackWeaponUnknown3
                           == setup.SpecialAttackWeaponUnknown3
                       && contract.SpecialAttackWeaponUnknown4
                           == setup.SpecialAttackWeaponUnknown4;
            }

            if (contract.CapturedSpecialAttacks.Length != 1)
            {
                return false;
            }

            CapturedEnemySpecialAttackDefinition special =
                contract.CapturedSpecialAttacks[0];
            if (!TryGenerate(
                    new OrdinaryEnemyCombatSetupInput(
                        monsterData,
                        actorLevel,
                        special.LowTemplate,
                        special.HighTemplate,
                        special.Tag,
                        special.Name),
                    out setup))
            {
                return false;
            }

            return contract.SpecialAttackWeaponUnknown1
                   == setup.SpecialAttackWeaponUnknown1
                   && contract.SpecialAttackWeaponUnknown2
                   == setup.SpecialAttackWeaponUnknown2
                   && contract.SpecialAttackWeaponUnknown3
                   == setup.SpecialAttackWeaponUnknown3
                   && contract.SpecialAttackWeaponUnknown4
                   == setup.SpecialAttackWeaponUnknown4;
        }

        private static bool FilthFleaSpecialsMatch(
            CapturedEnemySpecialAttackDefinition[] specials)
        {
            return specials != null
                   && specials.Length == 2
                   && specials[0].LowTemplate
                      == NpcCombatAttackRules
                          .CapturedSubwayFilthFleaStickToHeadLowTemplate
                   && specials[0].HighTemplate
                      == NpcCombatAttackRules
                          .CapturedSubwayFilthFleaStickToHeadHighTemplate
                   && specials[0].Tag
                      == NpcCombatAttackRules
                          .CapturedSubwayFilthFleaStickToHeadTag
                   && specials[0].Name
                      == NpcCombatAttackRules
                          .CapturedSubwayFilthFleaStickToHeadName
                   && specials[1].LowTemplate
                      == NpcCombatAttackRules
                          .CapturedSubwayFilthFleaArmsLowTemplate
                   && specials[1].HighTemplate
                      == NpcCombatAttackRules
                          .CapturedSubwayFilthFleaArmsHighTemplate
                   && specials[1].Tag
                      == NpcCombatAttackRules
                          .CapturedSubwayFilthFleaArmsTag
                   && specials[1].Name
                      == NpcCombatAttackRules
                          .CapturedSubwayFilthFleaArmsName;
        }

        private static bool MatchesCategoricalInput(
            OrdinaryEnemyCombatSetupInput input,
            int monsterData,
            int minimumLevel,
            int maximumLevel,
            int lowTemplate,
            int highTemplate,
            int tag,
            string name)
        {
            return input.MonsterData == monsterData
                   && input.ActorLevel >= minimumLevel
                   && input.ActorLevel <= maximumLevel
                   && input.SpecialAttackLowTemplate == lowTemplate
                   && input.SpecialAttackHighTemplate == highTemplate
                   && input.SpecialAttackTag == tag
                   && string.Equals(
                       input.SpecialAttackName,
                       name,
                       System.StringComparison.Ordinal);
        }

        private static OrdinaryEnemyCombatNumericSetup RepeatedSetup(
            string formulaId,
            int value)
        {
            return new OrdinaryEnemyCombatNumericSetup(
                formulaId,
                value,
                value,
                value,
                value);
        }
    }

    internal sealed class OrdinaryEnemyCombatResultDomain
    {
        internal OrdinaryEnemyCombatResultDomain(
            string domainId,
            int resourceId,
            string name,
            int monsterData,
            int weaponLowTemplate,
            int weaponHighTemplate,
            int weaponQuality,
            int weaponSlot,
            int attackInfoAmmoCount,
            int damageTypeWire,
            int hitTypeWire,
            int weaponInstance,
            byte specialAttackWeaponN3Unknown,
            byte attackN3Unknown,
            byte attackAction,
            string evidence)
        {
            this.DomainId = domainId;
            this.ResourceId = resourceId;
            this.Name = name;
            this.MonsterData = monsterData;
            this.WeaponLowTemplate = weaponLowTemplate;
            this.WeaponHighTemplate = weaponHighTemplate;
            this.WeaponQuality = weaponQuality;
            this.WeaponSlot = weaponSlot;
            this.AttackInfoAmmoCount = attackInfoAmmoCount;
            this.DamageTypeWire = damageTypeWire;
            this.HitTypeWire = hitTypeWire;
            this.WeaponInstance = weaponInstance;
            this.SpecialAttackWeaponN3Unknown = specialAttackWeaponN3Unknown;
            this.AttackN3Unknown = attackN3Unknown;
            this.AttackAction = attackAction;
            this.Evidence = evidence;
        }

        internal string DomainId { get; private set; }

        internal int ResourceId { get; private set; }

        internal string Name { get; private set; }

        internal int MonsterData { get; private set; }

        internal int WeaponLowTemplate { get; private set; }

        internal int WeaponHighTemplate { get; private set; }

        internal int WeaponQuality { get; private set; }

        internal int WeaponSlot { get; private set; }

        internal int AttackInfoAmmoCount { get; private set; }

        internal int DamageTypeWire { get; private set; }

        internal int HitTypeWire { get; private set; }

        internal int WeaponInstance { get; private set; }

        internal byte SpecialAttackWeaponN3Unknown { get; private set; }

        internal byte AttackN3Unknown { get; private set; }

        internal byte AttackAction { get; private set; }

        internal string Evidence { get; private set; }

        internal bool Matches(
            int resourceId,
            string name,
            int monsterData,
            CapturedEnemyCombatContract contract)
        {
            return contract != null
                   && resourceId == this.ResourceId
                   && string.Equals(name, this.Name, StringComparison.Ordinal)
                   && monsterData == this.MonsterData
                   && contract.AttackModel == CapturedEnemyAttackModel.EquippedWeapon
                   && contract.WeaponLowId == this.WeaponLowTemplate
                   && contract.WeaponHighId == this.WeaponHighTemplate
                   && contract.WeaponQuality == this.WeaponQuality
                   && contract.WeaponInventorySlot == this.WeaponSlot
                   && contract.HasEmptySpecialAttackWeaponContext
                   && contract.HasCapturedAttackStartContext
                   && contract.HasCapturedEquippedAttackInfo
                   && contract.AttackInfoAmmoCount == this.AttackInfoAmmoCount
                   && contract.AttackInfoWeaponSlot == this.WeaponSlot
                   && contract.AttackInfoUnknown == this.DamageTypeWire
                   && contract.AttackInfoHitType == this.HitTypeWire
                   && contract.AttackInfoWeaponInstance == this.WeaponInstance
                   && contract.SpecialAttackWeaponN3Unknown
                      == this.SpecialAttackWeaponN3Unknown
                   && contract.AttackN3Unknown == this.AttackN3Unknown
                   && contract.AttackAction == this.AttackAction;
        }
    }

    internal static class OrdinaryEnemyCombatResultDomainRegistry
    {
        internal const string ViolentVagabondResultDomainId =
            "equipped-melee-empty-saw-slot6-normal-result-v1";

        private static readonly OrdinaryEnemyCombatResultDomain[] Domains =
        {
            new OrdinaryEnemyCombatResultDomain(
                ViolentVagabondResultDomainId,
                127,
                "Violent Vagabond",
                203733,
                130590,
                130590,
                1,
                6,
                0,
                0,
                NpcCombatAttackRules.NormalAttackInfoHitType,
                0,
                0,
                0,
                0,
                "40 distinct Vagabond miss chains prove empty SAW, Attack action 0, "
                + "slot 6, instance 0, and packet order; all 166 compatible captured "
                + "ordinary equipped-melee normal-result streams use AttackInfo "
                + "damage wire 0 and hit wire 3, while finite ranged damage-wire-4 "
                + "streams are categorically excluded")
        };

        internal static bool TryResolve(
            int resourceId,
            string name,
            int monsterData,
            CapturedEnemyCombatContract contract,
            out OrdinaryEnemyCombatResultDomain domain)
        {
            domain = Array.Find(
                Domains,
                value => value.Matches(resourceId, name, monsterData, contract));
            return domain != null;
        }
    }
}
