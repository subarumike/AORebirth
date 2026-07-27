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

        internal static bool TryGenerate(
            OrdinaryEnemyCombatSetupInput input,
            out OrdinaryEnemyCombatNumericSetup setup)
        {
            setup = null;
            if (input == null
                || input.MonsterData
                   != NpcCombatAttackRules.CapturedSubwayDisobedientBotMonsterData
                || input.ActorLevel < DisobedientBotMinimumLevel
                || input.ActorLevel > DisobedientBotMaximumLevel
                || input.SpecialAttackLowTemplate
                   != NpcCombatAttackRules.CapturedSubwayDisobedientBotLowTemplate
                || input.SpecialAttackHighTemplate
                   != NpcCombatAttackRules.CapturedSubwayDisobedientBotHighTemplate
                || input.SpecialAttackTag
                   != NpcCombatAttackRules.CapturedSubwayDisobedientBotWeaponTag
                || !string.Equals(
                    input.SpecialAttackName,
                    NpcCombatAttackRules.CapturedSubwayDisobedientBotWeaponName,
                    System.StringComparison.Ordinal))
            {
                return false;
            }

            // Exact positive-integer floor division. All five captured levels
            // reproduce exactly; the bounded L7 result is 40.
            int value = checked((19 * input.ActorLevel) + 28) / 4;
            setup = new OrdinaryEnemyCombatNumericSetup(
                DisobedientBotFormulaId,
                value,
                value,
                value,
                value);
            return true;
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
    }
}
