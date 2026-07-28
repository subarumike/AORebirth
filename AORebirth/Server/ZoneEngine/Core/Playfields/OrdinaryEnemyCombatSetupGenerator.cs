namespace AORebirth.Core.Playfields
{
    using System;

    using ZoneEngine.Core.Playfields;

    internal sealed class OrdinaryEnemyCombatSetupInput
    {
        internal OrdinaryEnemyCombatSetupInput(
            int monsterData,
            int actorLevel,
            int specialAttackLowTemplate,
            int specialAttackHighTemplate,
            int specialAttackTag,
            string specialAttackName)
        {
            this.MonsterData = monsterData;
            this.ActorLevel = actorLevel;
            this.SpecialAttackLowTemplate = specialAttackLowTemplate;
            this.SpecialAttackHighTemplate = specialAttackHighTemplate;
            this.SpecialAttackTag = specialAttackTag;
            this.SpecialAttackName = specialAttackName;
        }

        internal int MonsterData { get; private set; }

        internal int ActorLevel { get; private set; }

        internal int SpecialAttackLowTemplate { get; private set; }

        internal int SpecialAttackHighTemplate { get; private set; }

        internal int SpecialAttackTag { get; private set; }

        internal string SpecialAttackName { get; private set; }
    }

    internal sealed class OrdinaryEnemyCombatNumericSetup
    {
        internal OrdinaryEnemyCombatNumericSetup(
            string formulaId,
            int specialAttackWeaponUnknown1,
            int specialAttackWeaponUnknown2,
            int specialAttackWeaponUnknown3,
            int specialAttackWeaponUnknown4)
        {
            this.FormulaId = formulaId;
            this.SpecialAttackWeaponUnknown1 = specialAttackWeaponUnknown1;
            this.SpecialAttackWeaponUnknown2 = specialAttackWeaponUnknown2;
            this.SpecialAttackWeaponUnknown3 = specialAttackWeaponUnknown3;
            this.SpecialAttackWeaponUnknown4 = specialAttackWeaponUnknown4;
        }

        internal string FormulaId { get; private set; }

        internal int SpecialAttackWeaponUnknown1 { get; private set; }

        internal int SpecialAttackWeaponUnknown2 { get; private set; }

        internal int SpecialAttackWeaponUnknown3 { get; private set; }

        internal int SpecialAttackWeaponUnknown4 { get; private set; }
    }

    internal sealed class OrdinaryEnemyEquippedCombatSetupInput
    {
        internal OrdinaryEnemyEquippedCombatSetupInput(
            int monsterData,
            int actorLevel,
            int weaponLowTemplate,
            int weaponHighTemplate,
            int weaponQuality,
            int weaponSlot)
        {
            this.MonsterData = monsterData;
            this.ActorLevel = actorLevel;
            this.WeaponLowTemplate = weaponLowTemplate;
            this.WeaponHighTemplate = weaponHighTemplate;
            this.WeaponQuality = weaponQuality;
            this.WeaponSlot = weaponSlot;
        }

        internal int MonsterData { get; private set; }

        internal int ActorLevel { get; private set; }

        internal int WeaponLowTemplate { get; private set; }

        internal int WeaponHighTemplate { get; private set; }

        internal int WeaponQuality { get; private set; }

        internal int WeaponSlot { get; private set; }
    }

    internal enum OrdinaryEnemyEquippedFormulaKind
    {
        MeldedPatterns,
        FragmentedSoul,
        IncompleteRebuild,
        MolestedMolecules,
        TempleCultist,
        TempleCultistRaisedPrimary
    }

    internal sealed class OrdinaryEnemyEquippedFormulaDomain
    {
        internal OrdinaryEnemyEquippedFormulaDomain(
            OrdinaryEnemyEquippedFormulaKind kind,
            string formulaId,
            int resourceId,
            int monsterData,
            int minimumLevel,
            int maximumLevel,
            int weaponSlot,
            string weaponFamilyId)
        {
            this.Kind = kind;
            this.FormulaId = formulaId;
            this.ResourceId = resourceId;
            this.MonsterData = monsterData;
            this.MinimumLevel = minimumLevel;
            this.MaximumLevel = maximumLevel;
            this.WeaponSlot = weaponSlot;
            this.WeaponFamilyId = weaponFamilyId;
        }

        internal OrdinaryEnemyEquippedFormulaKind Kind { get; private set; }

        internal string FormulaId { get; private set; }

        internal int ResourceId { get; private set; }

        internal int MonsterData { get; private set; }

        internal int MinimumLevel { get; private set; }

        internal int MaximumLevel { get; private set; }

        internal int WeaponSlot { get; private set; }

        internal string WeaponFamilyId { get; private set; }

        internal bool Matches(OrdinaryEnemyEquippedCombatSetupInput input)
        {
            return input != null
                   && input.MonsterData == this.MonsterData
                   && input.ActorLevel >= this.MinimumLevel
                   && input.ActorLevel <= this.MaximumLevel
                   && input.WeaponSlot == this.WeaponSlot
                   && this.MatchesWeaponLoadout(
                       input.WeaponLowTemplate,
                       input.WeaponHighTemplate,
                       input.WeaponQuality);
        }

        internal bool MatchesWeaponLoadout(
            int lowTemplate,
            int highTemplate,
            int quality)
        {
            switch (this.Kind)
            {
                case OrdinaryEnemyEquippedFormulaKind.MeldedPatterns:
                    return OrdinaryEnemyCombatSetupGenerator
                        .IsMeldedPatternsWeaponLoadout(
                            lowTemplate,
                            highTemplate,
                            quality);
                case OrdinaryEnemyEquippedFormulaKind.FragmentedSoul:
                    return OrdinaryEnemyCombatSetupGenerator
                        .IsFragmentedSoulWeaponLoadout(
                            lowTemplate,
                            highTemplate,
                            quality);
                case OrdinaryEnemyEquippedFormulaKind.IncompleteRebuild:
                    return OrdinaryEnemyCombatSetupGenerator
                        .IsIncompleteRebuildWeaponLoadout(
                            lowTemplate,
                            highTemplate,
                            quality);
                case OrdinaryEnemyEquippedFormulaKind.MolestedMolecules:
                    return OrdinaryEnemyCombatSetupGenerator
                        .IsMolestedMoleculesWeaponLoadout(
                            lowTemplate,
                            highTemplate,
                            quality);
                case OrdinaryEnemyEquippedFormulaKind.TempleCultist:
                case OrdinaryEnemyEquippedFormulaKind.TempleCultistRaisedPrimary:
                    return OrdinaryEnemyCombatSetupGenerator
                        .IsTempleCultistWeaponLoadout(
                            this.MonsterData,
                            lowTemplate,
                            highTemplate,
                            quality);
                default:
                    return false;
            }
        }

        internal OrdinaryEnemyCombatNumericSetup Generate(int actorLevel)
        {
            switch (this.Kind)
            {
                case OrdinaryEnemyEquippedFormulaKind.MeldedPatterns:
                {
                    int value = checked((11 * actorLevel) - 2) / 2;
                    return new OrdinaryEnemyCombatNumericSetup(
                        this.FormulaId,
                        value,
                        checked(value + 28),
                        value,
                        value);
                }
                case OrdinaryEnemyEquippedFormulaKind.FragmentedSoul:
                {
                    int baseValue = checked((6 * actorLevel) - 1);
                    int fourthValue = checked(
                        baseValue + (2 * (actorLevel / 2)));
                    return new OrdinaryEnemyCombatNumericSetup(
                        this.FormulaId,
                        baseValue,
                        baseValue,
                        baseValue,
                        fourthValue);
                }
                case OrdinaryEnemyEquippedFormulaKind.IncompleteRebuild:
                {
                    int baseValue = checked((6 * actorLevel) + 1);
                    return new OrdinaryEnemyCombatNumericSetup(
                        this.FormulaId,
                        baseValue,
                        baseValue,
                        baseValue,
                        checked(baseValue - 2));
                }
                case OrdinaryEnemyEquippedFormulaKind.MolestedMolecules:
                {
                    int value = checked((11 * actorLevel) - 2) / 2;
                    return new OrdinaryEnemyCombatNumericSetup(
                        this.FormulaId,
                        value,
                        value,
                        value,
                        value);
                }
                case OrdinaryEnemyEquippedFormulaKind.TempleCultist:
                case OrdinaryEnemyEquippedFormulaKind.TempleCultistRaisedPrimary:
                {
                    int baseValue;
                    if (actorLevel <= 25)
                    {
                        baseValue = checked((31 * actorLevel) - 10) / 2;
                    }
                    else if (actorLevel <= 33)
                    {
                        baseValue = checked((17 * actorLevel) - 42)
                                    - (actorLevel & 1);
                    }
                    else
                    {
                        baseValue = checked((17 * actorLevel) - 43);
                    }

                    int fourthValue = actorLevel <= 25
                        ? checked(actorLevel + 4) / 2
                        : checked(actorLevel + 6) / 2;
                    int primaryValue =
                        this.Kind == OrdinaryEnemyEquippedFormulaKind
                            .TempleCultistRaisedPrimary
                            ? checked(baseValue + 20)
                            : baseValue;
                    return new OrdinaryEnemyCombatNumericSetup(
                        this.FormulaId,
                        primaryValue,
                        baseValue,
                        baseValue,
                        fourthValue);
                }
                default:
                    throw new InvalidOperationException(
                        "Unsupported equipped combat formula domain.");
            }
        }
    }

    /// <summary>
    /// Produces only numeric combat state whose exact runtime formula has been
    /// independently proven. Weapon identity and packet semantics remain selected
    /// by the capture-backed catalog.
    /// </summary>
    internal static class OrdinaryEnemyCombatSetupGenerator
    {
        internal const string DisobedientBotFormulaId =
            "disobedient-bot-siw1-floor-19L-plus-28-over-4-v1";

        internal const int DisobedientBotMinimumLevel = 5;

        internal const int DisobedientBotMaximumLevel = 10;

        internal const string StimFiendFormulaId =
            "stim-fiend-siw1-floor-11L-minus-2-over-2-v1";

        internal const int StimFiendMinimumLevel = 10;

        internal const int StimFiendMaximumLevel = 17;

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

        internal static bool IsMeldedPatternsWeaponLoadout(
            int lowTemplate,
            int highTemplate,
            int quality)
        {
            return (lowTemplate == 121817
                    && highTemplate == 121818
                    && quality >= 1
                    && quality <= 19)
                   || (lowTemplate == 121818
                       && highTemplate == 121818
                       && quality == 20)
                   || (lowTemplate == 121819
                       && highTemplate == 121820
                       && quality >= 21
                       && quality <= 40);
        }

        internal static bool IsFragmentedSoulWeaponLoadout(
            int lowTemplate,
            int highTemplate,
            int quality)
        {
            return (lowTemplate == 123685
                    && highTemplate == 123686
                    && quality >= 1
                    && quality <= 19)
                   || (lowTemplate == 123686
                       && highTemplate == 123686
                       && quality == 20)
                   || (lowTemplate == 123687
                       && highTemplate == 123687
                       && quality == 21)
                   || (lowTemplate == 123687
                       && highTemplate == 123688
                       && quality >= 22
                       && quality <= 40);
        }

        internal static bool IsIncompleteRebuildWeaponLoadout(
            int lowTemplate,
            int highTemplate,
            int quality)
        {
            return (lowTemplate == 122653
                    && highTemplate == 122654
                    && quality >= 1
                    && quality <= 19)
                   || (lowTemplate == 122654
                       && highTemplate == 122654
                       && quality == 20)
                   || (lowTemplate == 122655
                       && highTemplate == 122655
                       && quality == 21)
                   || (lowTemplate == 122655
                       && highTemplate == 122656
                       && quality >= 22
                       && quality <= 40);
        }

        internal static bool IsMolestedMoleculesWeaponLoadout(
            int lowTemplate,
            int highTemplate,
            int quality)
        {
            return (lowTemplate == 122216
                    && highTemplate == 122217
                    && quality >= 1
                    && quality <= 19)
                   || (lowTemplate == 122217
                       && highTemplate == 122217
                       && quality == 20)
                   || (lowTemplate == 122218
                       && highTemplate == 122219
                       && quality >= 21
                       && quality <= 40);
        }

        internal static bool IsTempleCultistWeaponLoadout(
            int monsterData,
            int lowTemplate,
            int highTemplate,
            int quality)
        {
            if (quality <= 0)
            {
                return false;
            }

            switch (monsterData)
            {
                case 26074:
                case 26137:
                    return lowTemplate == 204747 && highTemplate == 204747;
                case 26082:
                    return (lowTemplate == 130163 && highTemplate == 130164)
                           || (lowTemplate == 130164 && highTemplate == 130164);
                case 26103:
                    return lowTemplate == 129028 && highTemplate == 129029;
                case 26135:
                    return lowTemplate == 158298 && highTemplate == 158299;
                case 26147:
                    return (lowTemplate == 144103 && highTemplate == 144103)
                           || (lowTemplate == 144103 && highTemplate == 144104)
                           || (lowTemplate == 144104 && highTemplate == 144104);
                case 26149:
                    return (lowTemplate == 124313 && highTemplate == 124314)
                           || (lowTemplate == 124314 && highTemplate == 124314);
                default:
                    return false;
            }
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
                || contract.CapturedSpecialAttacks == null
                || contract.CapturedSpecialAttacks.Length != 1)
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
}
