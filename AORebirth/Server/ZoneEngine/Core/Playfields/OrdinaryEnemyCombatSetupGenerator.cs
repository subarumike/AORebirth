namespace AORebirth.Core.Playfields
{
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

            if (input.MonsterData
                == NpcCombatAttackRules.CapturedSubwayMeldedPatternsMonsterData
                && input.ActorLevel >= MeldedPatternsMinimumLevel
                && input.ActorLevel <= MeldedPatternsMaximumLevel
                && input.WeaponSlot
                   == NpcCombatAttackRules.CapturedSubwayMeldedPatternsWeaponSlot
                && IsMeldedPatternsWeaponLoadout(
                    input.WeaponLowTemplate,
                    input.WeaponHighTemplate,
                    input.WeaponQuality))
            {
                // Exact positive-integer floor division. Captured L18, L19, L20,
                // L21, L24, and L25 rows reproduce exactly. Unknown2 is the
                // independently observed family offset from the same base value.
                int value = checked((11 * input.ActorLevel) - 2) / 2;
                setup = new OrdinaryEnemyCombatNumericSetup(
                    MeldedPatternsFormulaId,
                    value,
                    checked(value + 28),
                    value,
                    value);
                return true;
            }

            if (input.MonsterData
                == NpcCombatAttackRules.CapturedSubwayFragmentedSoulMonsterData
                && input.ActorLevel >= FragmentedSoulMinimumLevel
                && input.ActorLevel <= FragmentedSoulMaximumLevel
                && input.WeaponSlot
                   == NpcCombatAttackRules.CapturedSubwayFragmentedSoulWeaponSlot
                && IsFragmentedSoulWeaponLoadout(
                    input.WeaponLowTemplate,
                    input.WeaponHighTemplate,
                    input.WeaponQuality))
            {
                // All twenty-one unique raw Fragmented Soul SAW packets across L17..L21
                // reproduce this bounded integer setup. Unknown4 adds the even
                // level step using positive integer floor division.
                int baseValue = checked((6 * input.ActorLevel) - 1);
                int fourthValue = checked(
                    baseValue + (2 * (input.ActorLevel / 2)));
                setup = new OrdinaryEnemyCombatNumericSetup(
                    FragmentedSoulFormulaId,
                    baseValue,
                    baseValue,
                    baseValue,
                    fourthValue);
                return true;
            }

            return false;
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
