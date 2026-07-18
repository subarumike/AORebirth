namespace AORebirth.Core.Playfields
{
    using System;

    using ZoneEngine.Core.Playfields;

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

        internal double RechargeSeconds { get; set; }

        internal int AttackInfoWeaponSlot { get; set; }

        internal int AttackInfoUnknown { get; set; }

        internal int AttackInfoWeaponInstance { get; set; }

        internal int WeaponLowId { get; set; }

        internal int WeaponHighId { get; set; }

        internal int WeaponQuality { get; set; }

        internal int WeaponInventorySlot { get; set; }

        internal bool HasEmptySpecialAttackWeaponContext { get; set; }

        internal bool HasCapturedAttackStartContext { get; set; }

        internal bool HasCapturedEquippedAttackInfo { get; set; }
    }

    internal static class CapturedSubwayCombatCatalog
    {
        private const int BloodcreeperMonsterData = 30379;

        private const int LooterMonsterData = 203745;

        private const int WorkmanStrikerMonsterData = 203854;

        internal static CapturedEnemyCombatContract For(string name, int monsterData)
        {
            return monsterData == BloodcreeperMonsterData
                ? new CapturedEnemyCombatContract
                    {
                        AttackModel = CapturedEnemyAttackModel.Specialized,
                        IsCombatReady = true,
                        Evidence = "Bloodcreeper captured dual natural attack sequence."
                    }
                : new CapturedEnemyCombatContract();
        }

        internal static CapturedEnemyCombatContract For(string name, int monsterData, int? level)
        {
            return For(name, monsterData);
        }

        internal static CapturedEnemyCombatContract ForOrdinary(
            CapturedSubwayOrdinaryArchetypeDefinition archetype)
        {
            if (archetype != null
                && (archetype.MonsterData == WorkmanStrikerMonsterData
                    || archetype.MonsterData == LooterMonsterData))
            {
                return new CapturedEnemyCombatContract
                {
                    AttackModel = CapturedEnemyAttackModel.Unresolved,
                    IsCombatReady = false,
                    Evidence = archetype.Name + " requires exact source weapon evidence."
                };
            }

            if (archetype != null && archetype.MonsterData == BloodcreeperMonsterData)
            {
                return For(archetype.Name, archetype.MonsterData);
            }

            if (archetype != null
                && archetype.MonsterData
                   == NpcCombatAttackRules.CapturedSubwayMeldedPatternsMonsterData)
            {
                CapturedSubwayCombatEvidenceDefinition meldedCombat = archetype.Combat;
                bool hasFocusedWeaponCapture = archetype.EvidenceCaptures != null
                                               && Array.IndexOf(
                                                   archetype.EvidenceCaptures,
                                                   "20260716-034559") >= 0;
                bool hasExactNormalHitBoundary = meldedCombat != null
                                                 && meldedCombat.Observed
                                                 && meldedCombat.ObservedRows == 7
                                                 && meldedCombat.MinDamage == 21
                                                 && meldedCombat.MaxDamage == 34
                                                 && meldedCombat.WeaponSlot == 6;
                if (!hasFocusedWeaponCapture || !hasExactNormalHitBoundary)
                {
                    return new CapturedEnemyCombatContract
                    {
                        AttackModel = CapturedEnemyAttackModel.Unresolved,
                        IsCombatReady = false,
                        Evidence = "Melded Patterns captured weapon evidence is incomplete."
                    };
                }

                return new CapturedEnemyCombatContract
                {
                    AttackModel = CapturedEnemyAttackModel.EquippedWeapon,
                    IsCombatReady = true,
                    Evidence = "20260716-034559: seven normal local-player hits 21..34; no observed critical; weapon-owned damage and recharge",
                    WeaponLowId = NpcCombatAttackRules.CapturedSubwayMeldedPatternsWeaponLowTemplate,
                    WeaponHighId = NpcCombatAttackRules.CapturedSubwayMeldedPatternsWeaponHighTemplate,
                    WeaponQuality = NpcCombatAttackRules.CapturedSubwayMeldedPatternsWeaponQuality,
                    WeaponInventorySlot = 6
                };
            }

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
                MaxDamage = observed ? archetype.Combat.MaxDamage : 0,
                RechargeSeconds = observed ? archetype.Combat.RechargeSeconds : 0,
                AttackInfoWeaponSlot = observed ? archetype.Combat.WeaponSlot : 0,
                AttackInfoUnknown = observed ? archetype.Combat.AttackInfoUnknown : 0,
                AttackInfoWeaponInstance = observed ? archetype.Combat.WeaponInstance : 0
            };
        }

        internal static CapturedEnemyCombatContract ForOrdinary(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int sourceInstance)
        {
            if (archetype == null
                || (archetype.MonsterData != WorkmanStrikerMonsterData
                    && archetype.MonsterData != LooterMonsterData))
            {
                return ForOrdinary(archetype);
            }

            CapturedSubwaySourceWeaponEvidenceDefinition matched = null;
            int matches = 0;
            foreach (CapturedSubwaySourceWeaponEvidenceDefinition evidence in
                archetype.SourceWeaponEvidence)
            {
                if (evidence.SourceInstance != sourceInstance)
                {
                    continue;
                }

                matched = evidence;
                matches++;
            }

            if (matches != 1 || matched == null)
            {
                return new CapturedEnemyCombatContract
                {
                    AttackModel = CapturedEnemyAttackModel.Unresolved,
                    IsCombatReady = false,
                    Evidence = archetype.Name + " source weapon evidence is missing or conflicting."
                };
            }

            return new CapturedEnemyCombatContract
            {
                AttackModel = CapturedEnemyAttackModel.EquippedWeapon,
                IsCombatReady = true,
                Evidence = string.Format(
                    "{0}: {1} source 0x{2:X8} QL{3} weapon {4}/{5}; item owns normal damage and recharge",
                    matched.EvidenceCaptures,
                    archetype.Name,
                    sourceInstance,
                    matched.Quality,
                    matched.LowId,
                    matched.HighId),
                WeaponLowId = matched.LowId,
                WeaponHighId = matched.HighId,
                WeaponQuality = matched.Quality,
                WeaponInventorySlot = 6
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
