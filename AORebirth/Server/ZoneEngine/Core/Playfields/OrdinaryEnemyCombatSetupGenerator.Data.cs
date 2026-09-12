namespace AORebirth.Core.Playfields
{
    using System;

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
        ViolentVagabond,
        MeldedPatterns,
        FragmentedSoul,
        IncompleteRebuild,
        MolestedMolecules,
        EternalSentinel,
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
                case OrdinaryEnemyEquippedFormulaKind.ViolentVagabond:
                    return lowTemplate == 130590
                           && highTemplate == 130590
                           && quality == 1;
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
                case OrdinaryEnemyEquippedFormulaKind.EternalSentinel:
                    return OrdinaryEnemyCombatSetupGenerator
                        .IsEternalSentinelWeaponLoadout(
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
                case OrdinaryEnemyEquippedFormulaKind.ViolentVagabond:
                    return new OrdinaryEnemyCombatNumericSetup(
                        this.FormulaId,
                        checked((17 * actorLevel) + 26) / 4,
                        checked((19 * actorLevel) + 26) / 4,
                        checked((15 * actorLevel) + 26) / 4,
                        checked((17 * actorLevel) + 25) / 4);
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
                case OrdinaryEnemyEquippedFormulaKind.EternalSentinel:
                {
                    int primary = checked((11 * actorLevel) - 2) / 2;
                    return new OrdinaryEnemyCombatNumericSetup(
                        this.FormulaId,
                        primary,
                        primary,
                        primary,
                        checked(actorLevel + 4) / 2);
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

    internal static partial class OrdinaryEnemyCombatSetupGenerator
    {
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

        internal static bool IsEternalSentinelWeaponLoadout(
            int lowTemplate,
            int highTemplate,
            int quality)
        {
            return (lowTemplate == 123381
                    && highTemplate == 123382
                    && quality >= 15
                    && quality <= 18)
                   || (lowTemplate == 123383
                       && highTemplate == 123383
                       && quality == 21)
                   || (lowTemplate == 123383
                       && highTemplate == 123384
                       && quality == 22);
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

    }
}
