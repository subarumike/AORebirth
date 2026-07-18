namespace AORebirth.Core.Playfields
{
    using System;
    using System.Linq;

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

        internal bool HasCapturedCombatStopSequence { get; set; }

        internal int AttackInfoAmmoCount { get; set; }
    }

    internal static class CapturedSubwayCombatCatalog
    {
        private const int BloodcreeperMonsterData = 30379;

        private const int DerangedShopperMonsterData = 203736;

        private const int DerangedShopperSourceInstance = 0x79574527;

        private const int LooterMonsterData = 203745;

        private const int MuggerMonsterData = 203734;

        private const int RedundantScanMonsterData = 204178;

        private const int WorkmanStrikerMonsterData = 203854;

        private static readonly int[] MuggerSourceInstances =
        {
            0x7953AA11,
            0x7953AD6B,
            0x795450D4,
            0x795451FE,
            0x79557F14,
            0x7957E5C6,
            0x7957E5C7,
            0x7957E5C8,
            0x7957E5CA
        };

        private static readonly int[] RedundantScanSourceInstances =
        {
            0x7953AF85,
            0x795451BF,
            0x795451C4,
            0x795451D3
        };

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

        internal static CapturedEnemyCombatContract ForSupportedSourceWeapon(
            string name,
            int monsterData,
            CapturedSubwaySourceWeaponEvidenceDefinition[] sourceWeaponEvidence,
            int sourceInstance)
        {
            if (!string.Equals(name, "Mugger", StringComparison.Ordinal)
                || monsterData != MuggerMonsterData
                || !HasCompleteMuggerSourceWeaponEvidence(sourceWeaponEvidence))
            {
                return new CapturedEnemyCombatContract
                {
                    AttackModel = CapturedEnemyAttackModel.Unresolved,
                    IsCombatReady = false,
                    Evidence = "Mugger source weapon evidence is incomplete or unsupported."
                };
            }

            CapturedSubwaySourceWeaponEvidenceDefinition matched = null;
            int matches = 0;
            foreach (CapturedSubwaySourceWeaponEvidenceDefinition evidence in sourceWeaponEvidence)
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
                    Evidence = "Mugger source weapon evidence is missing or conflicting."
                };
            }

            return new CapturedEnemyCombatContract
            {
                AttackModel = CapturedEnemyAttackModel.EquippedWeapon,
                IsCombatReady = true,
                Evidence = string.Format(
                    "{0}: Mugger source 0x{1:X8} QL1 weapon 121567/121567; 38 normal local-player hits span 9..12; three 21-point criticals are report-only; median interval 5.816469 seconds; item owns runtime damage, damage bonus, and recharge; captured AttackInfo only; no empty SIW or attack-start/stop context",
                    matched.EvidenceCaptures,
                    sourceInstance),
                WeaponLowId = matched.LowId,
                WeaponHighId = matched.HighId,
                WeaponQuality = matched.Quality,
                WeaponInventorySlot = 6,
                HasCapturedEquippedAttackInfo = true,
                AttackInfoAmmoCount = -1,
                AttackInfoWeaponSlot = 6,
                AttackInfoUnknown = 0,
                AttackInfoWeaponInstance = 0
            };
        }

        private static bool HasCompleteMuggerSourceWeaponEvidence(
            CapturedSubwaySourceWeaponEvidenceDefinition[] sourceWeaponEvidence)
        {
            if (sourceWeaponEvidence == null
                || sourceWeaponEvidence.Length != MuggerSourceInstances.Length)
            {
                return false;
            }

            foreach (int expectedSource in MuggerSourceInstances)
            {
                int matches = sourceWeaponEvidence.Count(
                    evidence => evidence.SourceInstance == expectedSource
                                && evidence.LowId == 121567
                                && evidence.HighId == 121567
                                && evidence.Quality == 1);
                if (matches != 1)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasCompleteRedundantScanSourceWeaponEvidence(
            CapturedSubwaySourceWeaponEvidenceDefinition[] sourceWeaponEvidence)
        {
            if (sourceWeaponEvidence == null
                || sourceWeaponEvidence.Length != RedundantScanSourceInstances.Length)
            {
                return false;
            }

            foreach (int expectedSource in RedundantScanSourceInstances)
            {
                int matches = sourceWeaponEvidence.Count(
                    evidence => IsExactRedundantScanSourceWeapon(evidence, expectedSource));
                if (matches != 1)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsExactRedundantScanSourceWeapon(
            CapturedSubwaySourceWeaponEvidenceDefinition evidence,
            int expectedSource)
        {
            if (evidence == null || evidence.SourceInstance != expectedSource)
            {
                return false;
            }

            switch (expectedSource)
            {
                case 0x7953AF85:
                    return evidence.LowId == 122027
                           && evidence.HighId == 122027
                           && evidence.Quality == 20;
                case 0x795451BF:
                    return evidence.LowId == 122026
                           && evidence.HighId == 122027
                           && evidence.Quality == 14;
                case 0x795451C4:
                    return evidence.LowId == 122028
                           && evidence.HighId == 122029
                           && evidence.Quality == 25;
                case 0x795451D3:
                    return evidence.LowId == 122026
                           && evidence.HighId == 122027
                           && evidence.Quality == 16;
                default:
                    return false;
            }
        }

        internal static CapturedEnemyCombatContract For(string name, int monsterData, int? level)
        {
            return For(name, monsterData);
        }

        internal static CapturedEnemyCombatContract ForOrdinary(
            CapturedSubwayOrdinaryArchetypeDefinition archetype)
        {
            if (archetype != null
                && (archetype.MonsterData == DerangedShopperMonsterData
                    || archetype.MonsterData == WorkmanStrikerMonsterData
                    || archetype.MonsterData == LooterMonsterData
                    || archetype.MonsterData == RedundantScanMonsterData))
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
            if (archetype != null && archetype.MonsterData == DerangedShopperMonsterData)
            {
                CapturedSubwaySourceWeaponEvidenceDefinition[] evidence =
                    archetype.SourceWeaponEvidence;
                if (sourceInstance != DerangedShopperSourceInstance
                    || evidence == null
                    || evidence.Length != 1
                    || evidence[0].SourceInstance != DerangedShopperSourceInstance
                    || evidence[0].LowId != 125454
                    || evidence[0].HighId != 125455
                    || evidence[0].Quality != 8)
                {
                    return new CapturedEnemyCombatContract
                    {
                        AttackModel = CapturedEnemyAttackModel.Unresolved,
                        IsCombatReady = false,
                        Evidence = "Deranged Shopper source weapon evidence is missing or conflicting."
                    };
                }

                return new CapturedEnemyCombatContract
                {
                    AttackModel = CapturedEnemyAttackModel.EquippedWeapon,
                    IsCombatReady = true,
                    Evidence = "Deranged Shopper source 0x79574527 QL8 125454/125455; item owns runtime damage, damage bonus, and recharge; one critical is report-only and one captured miss preserves ammo -1, slot 6, and unknown 0.",
                    WeaponLowId = evidence[0].LowId,
                    WeaponHighId = evidence[0].HighId,
                    WeaponQuality = evidence[0].Quality,
                    WeaponInventorySlot = 6,
                    HasCapturedEquippedAttackInfo = true,
                    AttackInfoAmmoCount = -1,
                    AttackInfoWeaponSlot = 6,
                    AttackInfoUnknown = 0,
                    AttackInfoWeaponInstance = 0
                };
            }

            if (archetype != null && archetype.MonsterData == RedundantScanMonsterData)
            {
                CapturedSubwaySourceWeaponEvidenceDefinition[] evidence =
                    archetype.SourceWeaponEvidence;
                if (!HasCompleteRedundantScanSourceWeaponEvidence(evidence))
                {
                    return new CapturedEnemyCombatContract
                    {
                        AttackModel = CapturedEnemyAttackModel.Unresolved,
                        IsCombatReady = false,
                        Evidence = "Redundant Scan source weapon evidence is missing or conflicting."
                    };
                }

                CapturedSubwaySourceWeaponEvidenceDefinition redundantMatched = null;
                int redundantMatches = 0;
                foreach (CapturedSubwaySourceWeaponEvidenceDefinition candidate in evidence)
                {
                    if (candidate.SourceInstance != sourceInstance)
                    {
                        continue;
                    }

                    redundantMatched = candidate;
                    redundantMatches++;
                }

                if (redundantMatches != 1 || redundantMatched == null)
                {
                    return new CapturedEnemyCombatContract
                    {
                        AttackModel = CapturedEnemyAttackModel.Unresolved,
                        IsCombatReady = false,
                        Evidence = "Redundant Scan source weapon evidence is missing or conflicting."
                    };
                }

                return new CapturedEnemyCombatContract
                {
                    AttackModel = CapturedEnemyAttackModel.EquippedWeapon,
                    IsCombatReady = true,
                    Evidence = string.Format(
                        "{0}: Redundant Scan source 0x{1:X8} owner-linked QL{2} weapon {3}/{4}; one normal local-player hit is 19; item owns runtime damage and recharge; captured AttackInfo carries only ammo 17, slot 6, unknown 0, and weapon instance 0; no fixed damage, empty SIW, or captured attack-start/stop context",
                        redundantMatched.EvidenceCaptures,
                        sourceInstance,
                        redundantMatched.Quality,
                        redundantMatched.LowId,
                        redundantMatched.HighId),
                    WeaponLowId = redundantMatched.LowId,
                    WeaponHighId = redundantMatched.HighId,
                    WeaponQuality = redundantMatched.Quality,
                    WeaponInventorySlot = 6,
                    HasCapturedEquippedAttackInfo = true,
                    AttackInfoAmmoCount = 17,
                    AttackInfoWeaponSlot = 6,
                    AttackInfoUnknown = 0,
                    AttackInfoWeaponInstance = 0
                };
            }

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
