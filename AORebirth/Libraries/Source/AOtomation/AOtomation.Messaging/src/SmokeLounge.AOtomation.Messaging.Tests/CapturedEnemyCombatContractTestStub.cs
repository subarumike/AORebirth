namespace AORebirth.Core.Playfields
{
    internal enum CapturedEnemyAttackModel
    {
        Unresolved = 0,
        FixedAttackInfo = 1,
        EquippedWeapon = 2,
        Specialized = 3
    }

    internal sealed class CapturedEnemyCombatContract
    {
        internal CapturedEnemyAttackModel AttackModel { get; set; }

        internal bool IsCombatReady { get; set; }

        internal string Evidence { get; set; }

        internal int MinDamage { get; set; }

        internal int MaxDamage { get; set; }
    }

    internal static class CapturedSubwayCombatCatalog
    {
        internal static CapturedEnemyCombatContract For(string name, int monsterData)
        {
            return new CapturedEnemyCombatContract();
        }

        internal static CapturedEnemyCombatContract ForOrdinary(
            CapturedSubwayOrdinaryArchetypeDefinition archetype)
        {
            bool observed = archetype != null
                            && archetype.Combat != null
                            && archetype.Combat.Observed;
            return new CapturedEnemyCombatContract
            {
                AttackModel = observed
                    ? CapturedEnemyAttackModel.FixedAttackInfo
                    : CapturedEnemyAttackModel.Unresolved,
                IsCombatReady = observed,
                Evidence = archetype == null
                    ? string.Empty
                    : string.Join(",", archetype.EvidenceCaptures),
                MinDamage = observed ? archetype.Combat.MinDamage : 0,
                MaxDamage = observed ? archetype.Combat.MaxDamage : 0
            };
        }
    }
}

namespace ZoneEngine.Core
{
    internal sealed class CombatLootTableEntry
    {
        internal string ExactName { get; set; }

        internal int MonsterData { get; set; }

        internal int NpcFamily { get; set; }

        internal int Slot { get; set; }

        internal int DropChanceBasisPoints { get; set; }

        internal CombatLootItemTemplate[] ItemTemplates { get; set; }
    }

    internal sealed class CombatLootItemTemplate
    {
        internal int LowId { get; set; }

        internal int HighId { get; set; }

        internal int MinQuality { get; set; }

        internal int MaxQuality { get; set; }

        internal int RangeCheck { get; set; }

        internal string DropGroupHash { get; set; }
    }
}
