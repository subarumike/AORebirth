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

    internal sealed class CapturedEnemyCombatAttackDefinition
    {
        internal CapturedEnemyCombatAttackDefinition()
        {
        }

        internal CapturedEnemyCombatAttackDefinition(
            int minDamage,
            int maxDamage,
            int damageBonus,
            double range,
            double rechargeSeconds,
            bool usesEquippedWeapon,
            int attackInfoAmmoCount,
            int attackInfoWeaponSlot,
            int attackInfoUnknown,
            int attackInfoHitType,
            int attackInfoWeaponInstance,
            bool sendAttackInfo)
        {
            this.MinDamage = minDamage;
            this.MaxDamage = maxDamage;
            this.DamageBonus = damageBonus;
            this.Range = range;
            this.RechargeSeconds = rechargeSeconds;
            this.UsesEquippedWeapon = usesEquippedWeapon;
            this.AttackInfoAmmoCount = attackInfoAmmoCount;
            this.AttackInfoWeaponSlot = attackInfoWeaponSlot;
            this.AttackInfoUnknown = attackInfoUnknown;
            this.AttackInfoHitType = attackInfoHitType;
            this.AttackInfoWeaponInstance = attackInfoWeaponInstance;
            this.SendAttackInfo = sendAttackInfo;
        }

        internal int MinDamage { get; set; }

        internal int MaxDamage { get; set; }

        internal int DamageBonus { get; set; }

        internal double Range { get; set; }

        internal double RechargeSeconds { get; set; }

        internal bool UsesEquippedWeapon { get; set; }

        internal int AttackInfoAmmoCount { get; set; }

        internal int AttackInfoWeaponSlot { get; set; }

        internal int AttackInfoUnknown { get; set; }

        internal int AttackInfoWeaponInstance { get; set; }

        internal int AttackInfoHitType { get; set; }

        internal bool SendAttackInfo { get; set; }
    }

    internal sealed class CapturedEnemySpecialAttackDefinition
    {
        internal CapturedEnemySpecialAttackDefinition(
            int lowTemplate,
            int highTemplate,
            int tag,
            string name)
        {
            this.LowTemplate = lowTemplate;
            this.HighTemplate = highTemplate;
            this.Tag = tag;
            this.Name = name;
        }

        internal int LowTemplate { get; private set; }

        internal int HighTemplate { get; private set; }

        internal int Tag { get; private set; }

        internal string Name { get; private set; }
    }

    internal sealed class CapturedEnemySpecialAttackSequenceDefinition
    {
        internal CapturedEnemySpecialAttackSequenceDefinition()
        {
        }

        internal CapturedEnemySpecialAttackSequenceDefinition(
            double initialAttackDelaySeconds,
            CapturedEnemyCombatAttackDefinition openingAttack,
            CapturedEnemyCombatAttackDefinition repeatingAttack,
            CapturedEnemySpecialAttackDefinition[] specialAttacks,
            int specialAttackWeaponUnknown1,
            int specialAttackWeaponUnknown2,
            int specialAttackWeaponUnknown3,
            int specialAttackWeaponUnknown4,
            int specialAttackWeaponUnknown5)
        {
            this.InitialAttackDelaySeconds = initialAttackDelaySeconds;
            this.OpeningAttack = openingAttack;
            this.RepeatingAttack = repeatingAttack;
            this.SpecialAttacks = specialAttacks;
            this.SpecialAttackWeaponUnknown1 = specialAttackWeaponUnknown1;
            this.SpecialAttackWeaponUnknown2 = specialAttackWeaponUnknown2;
            this.SpecialAttackWeaponUnknown3 = specialAttackWeaponUnknown3;
            this.SpecialAttackWeaponUnknown4 = specialAttackWeaponUnknown4;
            this.SpecialAttackWeaponUnknown5 = specialAttackWeaponUnknown5;
        }

        internal double InitialAttackDelaySeconds { get; set; }

        internal CapturedEnemyCombatAttackDefinition OpeningAttack { get; set; }

        internal CapturedEnemyCombatAttackDefinition RepeatingAttack { get; set; }

        internal CapturedEnemySpecialAttackDefinition[] SpecialAttacks { get; set; }

        internal int SpecialAttackWeaponUnknown1 { get; set; }

        internal int SpecialAttackWeaponUnknown2 { get; set; }

        internal int SpecialAttackWeaponUnknown3 { get; set; }

        internal int SpecialAttackWeaponUnknown4 { get; set; }

        internal int SpecialAttackWeaponUnknown5 { get; set; }
    }

    internal sealed class CapturedEnemyCombatContract
    {
        internal static CapturedEnemyCombatContract CapturedSpecialSequence(
            string evidence,
            CapturedEnemySpecialAttackSequenceDefinition specialAttackSequence)
        {
            return new CapturedEnemyCombatContract
            {
                AttackModel = CapturedEnemyAttackModel.Specialized,
                IsCombatReady = true,
                Evidence = evidence,
                SpecialAttackSequence = specialAttackSequence
            };
        }

        internal static CapturedEnemyCombatContract FixedAttack(
            string evidence,
            int minDamage,
            int maxDamage,
            double rechargeSeconds,
            int weaponSlot,
            int attackInfoUnknown,
            int weaponInstance,
            int attackInfoAmmoCount = 0)
        {
            return new CapturedEnemyCombatContract
            {
                AttackModel = CapturedEnemyAttackModel.FixedAttackInfo,
                IsCombatReady = true,
                Evidence = evidence,
                MinDamage = minDamage,
                MaxDamage = maxDamage,
                RechargeSeconds = rechargeSeconds,
                AttackInfoWeaponSlot = weaponSlot,
                AttackInfoUnknown = attackInfoUnknown,
                AttackInfoWeaponInstance = weaponInstance,
                AttackInfoAmmoCount = attackInfoAmmoCount
            };
        }

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

        internal CapturedEnemySpecialAttackSequenceDefinition SpecialAttackSequence { get; set; }

        internal bool RequiresDamageLineOfSight { get; set; }
    }

    internal static class CapturedSubwayCombatCatalog
    {
        private const int BloodcreeperMonsterData = 30379;

        private const int DerangedShopperMonsterData = 203736;

        private const int DerangedShopperSourceInstance = 0x79574527;

        private const int DiscardedPetMonsterData = 17720;

        private const int IncompleteRebuildMonsterData = 203728;

        private const int FragmentedSoulMonsterData = 203729;

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

        private static readonly int[] IncompleteRebuildSourceInstances =
        {
            0x79545170,
            0x79545172,
            0x79545177,
            0x79545181,
            0x79545188,
            0x795451BC,
            0x795451C1,
            0x795451CB,
            0x795451FD,
            0x79545241
        };

        private static readonly int[] RedundantScanSourceInstances =
        {
            0x7953AF85,
            0x795451BF,
            0x795451C4,
            0x795451D3
        };

        private static readonly int[] FragmentedSoulSourceInstances =
        {
            0x7954516A,
            0x7954516F,
            0x7954517A,
            0x7954518A,
            0x7954518B,
            0x7954518E,
            0x795451AA,
            0x795451AE,
            0x79545248,
            0x79545367
        };

        internal static CapturedEnemyCombatContract For(string name, int monsterData)
        {
            if (monsterData == BloodcreeperMonsterData)
            {
                return new CapturedEnemyCombatContract
                    {
                        AttackModel = CapturedEnemyAttackModel.Specialized,
                        IsCombatReady = true,
                        Evidence = "Bloodcreeper captured dual natural attack sequence."
                    };
            }

            if (monsterData == DiscardedPetMonsterData)
            {
                return new CapturedEnemyCombatContract
                {
                    AttackModel = CapturedEnemyAttackModel.FixedAttackInfo,
                    IsCombatReady = true,
                    Evidence = "37 normal local-player Discarded Pet SIW1 hits span 9..18; four 30..33 criticals remain report-only; conventional median 5.089763 seconds.",
                    MinDamage = 9,
                    MaxDamage = 18,
                    RechargeSeconds = 5.089763,
                    AttackInfoAmmoCount = -1,
                    AttackInfoWeaponSlot = 0,
                    AttackInfoUnknown = 0,
                    AttackInfoWeaponInstance = 0x53495731
                };
            }

            if (monsterData == 203733)
            {
                return new CapturedEnemyCombatContract
                {
                    AttackModel = CapturedEnemyAttackModel.Specialized,
                    IsCombatReady = true,
                    Evidence = "private-project playability policy uses adjacent same-level Subway Mugger 9..12 damage; Red Wine remains excluded from combat.",
                    SpecialAttackSequence = new CapturedEnemySpecialAttackSequenceDefinition
                    {
                        OpeningAttack = null,
                        RepeatingAttack = new CapturedEnemyCombatAttackDefinition
                        {
                            MinDamage = 9,
                            MaxDamage = 12,
                            RechargeSeconds = 4.5802404,
                            AttackInfoAmmoCount = 0,
                            AttackInfoWeaponSlot = 6,
                            AttackInfoUnknown = 0,
                            AttackInfoWeaponInstance = 0
                        },
                        SpecialAttacks = new CapturedEnemySpecialAttackDefinition[0],
                        SpecialAttackWeaponUnknown1 = 32,
                        SpecialAttackWeaponUnknown2 = 35,
                        SpecialAttackWeaponUnknown3 = 29,
                        SpecialAttackWeaponUnknown4 = 31,
                        SpecialAttackWeaponUnknown5 = 0
                    }
                };
            }

            return new CapturedEnemyCombatContract();
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
                AttackInfoWeaponInstance = 0,
                RequiresDamageLineOfSight = true
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

        private static bool HasCompleteIncompleteRebuildSourceWeaponEvidence(
            CapturedSubwaySourceWeaponEvidenceDefinition[] sourceWeaponEvidence)
        {
            if (sourceWeaponEvidence == null
                || sourceWeaponEvidence.Length != IncompleteRebuildSourceInstances.Length)
            {
                return false;
            }

            foreach (int expectedSource in IncompleteRebuildSourceInstances)
            {
                int matches = sourceWeaponEvidence.Count(
                    evidence => IsExactIncompleteRebuildSourceWeapon(evidence, expectedSource));
                if (matches != 1)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsExactIncompleteRebuildSourceWeapon(
            CapturedSubwaySourceWeaponEvidenceDefinition evidence,
            int expectedSource)
        {
            if (evidence == null || evidence.SourceInstance != expectedSource)
            {
                return false;
            }

            switch (expectedSource)
            {
                case 0x79545170:
                case 0x79545177:
                case 0x795451BC:
                    return evidence.LowId == 122653 && evidence.HighId == 122654 && evidence.Quality == 18;
                case 0x79545172:
                    return evidence.LowId == 122653 && evidence.HighId == 122654 && evidence.Quality == 14;
                case 0x79545188:
                    return evidence.LowId == 122653 && evidence.HighId == 122654 && evidence.Quality == 17;
                case 0x79545181:
                case 0x795451FD:
                case 0x79545241:
                    return evidence.LowId == 122654 && evidence.HighId == 122654 && evidence.Quality == 20;
                case 0x795451C1:
                    return evidence.LowId == 122655 && evidence.HighId == 122655 && evidence.Quality == 21;
                case 0x795451CB:
                    return evidence.LowId == 122655 && evidence.HighId == 122656 && evidence.Quality == 24;
                default:
                    return false;
            }
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
                    || archetype.MonsterData == IncompleteRebuildMonsterData
                    || archetype.MonsterData == FragmentedSoulMonsterData
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
            bool runtimeReady = observed && archetype.Combat.RuntimeReady;
            return new CapturedEnemyCombatContract
            {
                AttackModel = runtimeReady
                    ? CapturedEnemyAttackModel.FixedAttackInfo
                    : CapturedEnemyAttackModel.Unresolved,
                IsCombatReady = runtimeReady,
                Evidence = archetype == null
                    ? string.Empty
                    : runtimeReady
                        ? string.Join(",", archetype.EvidenceCaptures)
                        : archetype.Name + " combat evidence is report-only.",
                MinDamage = runtimeReady ? archetype.Combat.MinDamage : 0,
                MaxDamage = runtimeReady ? archetype.Combat.MaxDamage : 0,
                RechargeSeconds = runtimeReady ? archetype.Combat.RechargeSeconds : 0,
                AttackInfoWeaponSlot = runtimeReady ? archetype.Combat.WeaponSlot : 0,
                AttackInfoUnknown = runtimeReady ? archetype.Combat.AttackInfoUnknown : 0,
                AttackInfoWeaponInstance = runtimeReady ? archetype.Combat.WeaponInstance : 0
            };
        }

        private static CapturedEnemyCombatContract ForFragmentedSoul(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int sourceInstance,
            OrdinaryEnemySpawnVariant variant,
            CapturedSubwayGenerationVariantDefinition[] generationEvidence)
        {
            CapturedSubwayCombatEvidenceDefinition combat = archetype == null
                ? null
                : archetype.Combat;
            bool hasExactCombatEvidence = combat != null
                                          && combat.Observed
                                          && combat.ObservedRows == 2
                                          && combat.MinDamage == 18
                                          && combat.MaxDamage == 23
                                          && combat.WeaponSlot == 6
                                          && combat.AttackInfoUnknown == 0
                                          && combat.WeaponInstance == 0;
            OrdinaryEnemySpawnWeaponLoadout weapon = variant == null
                ? null
                : variant.WeaponLoadout;
            string atomicFailure = string.Empty;
            if (!hasExactCombatEvidence
                || !FragmentedSoulSourceInstances.Contains(sourceInstance)
                || !OrdinaryEnemyAtomicGenerationEvidenceValidator.TryValidateSelectedVariant(
                    FragmentedSoulMonsterData,
                    sourceInstance,
                    variant,
                    generationEvidence,
                    out atomicFailure))
            {
                return new CapturedEnemyCombatContract
                {
                    AttackModel = CapturedEnemyAttackModel.Unresolved,
                    IsCombatReady = false,
                    Evidence = "Fragmented Soul atomic generation evidence is incomplete: "
                               + atomicFailure
                };
            }

            return new CapturedEnemyCombatContract
            {
                AttackModel = CapturedEnemyAttackModel.EquippedWeapon,
                IsCombatReady = true,
                Evidence = weapon.Evidence
                           + ": Fragmented Soul selected one captured atomic level/stat/weapon generation; "
                           + "two normal local-player hits span 18..23; item owns runtime damage and recharge; "
                           + "captured AttackInfo ammo 24, slot 6, unknown 0.",
                WeaponLowId = weapon.LowId,
                WeaponHighId = weapon.HighId,
                WeaponQuality = weapon.Quality,
                WeaponInventorySlot = 6,
                HasCapturedEquippedAttackInfo = true,
                AttackInfoAmmoCount = 24,
                AttackInfoWeaponSlot = 6,
                AttackInfoUnknown = 0,
                AttackInfoWeaponInstance = 0
            };
        }

        private static CapturedEnemyCombatContract ForWorkmanStriker(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int sourceInstance,
            OrdinaryEnemySpawnVariant variant,
            CapturedSubwayGenerationVariantDefinition[] generationEvidence)
        {
            CapturedSubwayCombatEvidenceDefinition combat = archetype == null
                ? null
                : archetype.Combat;
            OrdinaryEnemySpawnWeaponLoadout weapon = variant == null
                ? null
                : variant.WeaponLoadout;
            string atomicFailure = string.Empty;
            bool hasExactCombatEvidence = combat != null
                                          && combat.Observed
                                          && combat.RuntimeReady
                                          && combat.ObservedRows == 59
                                          && combat.MinDamage == 9
                                          && combat.MaxDamage == 23
                                          && combat.WeaponSlot == 6
                                          && combat.AttackInfoUnknown == 0
                                          && combat.WeaponInstance == 0;
            if (!hasExactCombatEvidence
                || !OrdinaryEnemyAtomicGenerationEvidenceValidator.TryValidateSelectedVariant(
                    WorkmanStrikerMonsterData,
                    sourceInstance,
                    variant,
                    generationEvidence,
                    out atomicFailure))
            {
                return new CapturedEnemyCombatContract
                {
                    AttackModel = CapturedEnemyAttackModel.Unresolved,
                    IsCombatReady = false,
                    Evidence = "Workman Striker combat requires one exact reviewed atomic level/stat/weapon generation for the selected source: "
                               + atomicFailure
                };
            }

            return new CapturedEnemyCombatContract
            {
                AttackModel = CapturedEnemyAttackModel.EquippedWeapon,
                IsCombatReady = true,
                Evidence = weapon.Evidence
                           + ": Workman Striker selected one captured atomic level/stat/weapon generation; "
                           + "59 normal local-player hits span 9..23; item owns runtime damage and recharge; "
                           + "captured AttackInfo ammo -1, slot 6, unknown 0, and weapon instance 0.",
                WeaponLowId = weapon.LowId,
                WeaponHighId = weapon.HighId,
                WeaponQuality = weapon.Quality,
                WeaponInventorySlot = 6,
                HasCapturedEquippedAttackInfo = true,
                AttackInfoAmmoCount = -1,
                AttackInfoWeaponSlot = 6,
                AttackInfoUnknown = 0,
                AttackInfoWeaponInstance = 0
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
                    Evidence = "20260710-202132,20260720-031025: Deranged Shopper source 0x79574527 QL8 125454/125455; ten normal local-player hits span 7..15, one 27-point critical is report-only, and six captured misses preserve ammo -1, slot 6, unknown 0, and weapon instance 0; capture 20260720-031025 also proves empty SpecialAttackWeapon 56/45/45/45/0 plus attack-start, StopFight, and death context; item owns runtime damage, damage bonus, and recharge; the newly observed SIW/start/stop/death context remains evidence-only so runtime behavior is unchanged.",
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

            if (archetype != null && archetype.MonsterData == WorkmanStrikerMonsterData)
            {
                return new CapturedEnemyCombatContract
                {
                    AttackModel = CapturedEnemyAttackModel.Unresolved,
                    IsCombatReady = false,
                    Evidence = "Workman Striker requires a selected capture-reviewed atomic generation variant."
                };
            }

            if (archetype != null && archetype.MonsterData == IncompleteRebuildMonsterData)
            {
                CapturedSubwaySourceWeaponEvidenceDefinition[] evidence = archetype.SourceWeaponEvidence;
                CapturedSubwayCombatEvidenceDefinition combat = archetype.Combat;
                bool hasExactCombatEvidence = combat != null
                                              && combat.Observed
                                              && combat.ObservedRows == 2
                                              && combat.MinDamage == 17
                                              && combat.MaxDamage == 35
                                              && combat.WeaponSlot == 6
                                              && combat.AttackInfoUnknown == 0
                                              && combat.WeaponInstance == 0;
                if (!hasExactCombatEvidence
                    || !HasCompleteIncompleteRebuildSourceWeaponEvidence(evidence))
                {
                    return new CapturedEnemyCombatContract
                    {
                        AttackModel = CapturedEnemyAttackModel.Unresolved,
                        IsCombatReady = false,
                        Evidence = "Incomplete Rebuild combat or source weapon evidence is missing or conflicting."
                    };
                }

                CapturedSubwaySourceWeaponEvidenceDefinition incompleteMatched = null;
                int incompleteMatches = 0;
                foreach (CapturedSubwaySourceWeaponEvidenceDefinition candidate in evidence)
                {
                    if (candidate.SourceInstance != sourceInstance)
                    {
                        continue;
                    }

                    incompleteMatched = candidate;
                    incompleteMatches++;
                }

                if (incompleteMatches != 1 || incompleteMatched == null)
                {
                    return new CapturedEnemyCombatContract
                    {
                        AttackModel = CapturedEnemyAttackModel.Unresolved,
                        IsCombatReady = false,
                        Evidence = "Incomplete Rebuild source weapon evidence is missing or conflicting."
                    };
                }

                return new CapturedEnemyCombatContract
                {
                    AttackModel = CapturedEnemyAttackModel.EquippedWeapon,
                    IsCombatReady = true,
                    Evidence = string.Format(
                        "{0}: Incomplete Rebuild source 0x{1:X8} owner-linked QL{2} weapon {3}/{4}; two normal local-player hits span 17..35 and one captured miss shares ammo 9, slot 6, unknown 0, and weapon instance 0; item owns runtime damage and recharge; no empty SIW or captured attack-start/stop context",
                        incompleteMatched.EvidenceCaptures,
                        sourceInstance,
                        incompleteMatched.Quality,
                        incompleteMatched.LowId,
                        incompleteMatched.HighId),
                    WeaponLowId = incompleteMatched.LowId,
                    WeaponHighId = incompleteMatched.HighId,
                    WeaponQuality = incompleteMatched.Quality,
                    WeaponInventorySlot = 6,
                    HasCapturedEquippedAttackInfo = true,
                    AttackInfoAmmoCount = 9,
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
                || archetype.MonsterData != LooterMonsterData)
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

        internal static CapturedEnemyCombatContract ForOrdinary(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int sourceInstance,
            OrdinaryEnemySpawnVariant variant,
            CapturedSubwayGenerationVariantDefinition[] generationEvidence)
        {
            if (archetype == null
                || (archetype.MonsterData != WorkmanStrikerMonsterData
                    && archetype.MonsterData != IncompleteRebuildMonsterData
                    && archetype.MonsterData != RedundantScanMonsterData
                    && archetype.MonsterData != FragmentedSoulMonsterData))
            {
                return ForOrdinary(archetype, sourceInstance);
            }

            if (archetype.MonsterData == WorkmanStrikerMonsterData)
            {
                return ForWorkmanStriker(
                    archetype,
                    sourceInstance,
                    variant,
                    generationEvidence);
            }

            if (archetype.MonsterData == FragmentedSoulMonsterData)
            {
                return ForFragmentedSoul(
                    archetype,
                    sourceInstance,
                    variant,
                    generationEvidence);
            }

            CapturedEnemyCombatContract baseline = ForOrdinary(archetype, sourceInstance);
            int monsterData = archetype.MonsterData;
            string displayName = monsterData == IncompleteRebuildMonsterData
                ? "Incomplete Rebuild"
                : "Redundant Scan";
            OrdinaryEnemySpawnWeaponLoadout weapon = variant == null
                ? null
                : variant.WeaponLoadout;
            string atomicFailure = string.Empty;
            if (!baseline.IsCombatReady
                || !OrdinaryEnemyAtomicGenerationEvidenceValidator.TryValidateSelectedVariant(
                    monsterData,
                    sourceInstance,
                    variant,
                    generationEvidence,
                    out atomicFailure))
            {
                return new CapturedEnemyCombatContract
                {
                    AttackModel = CapturedEnemyAttackModel.Unresolved,
                    IsCombatReady = false,
                    Evidence = displayName + " atomic generation evidence is incomplete: "
                               + atomicFailure
                };
            }

            return new CapturedEnemyCombatContract
            {
                AttackModel = CapturedEnemyAttackModel.EquippedWeapon,
                IsCombatReady = true,
                Evidence = weapon.Evidence
                           + ": " + displayName
                           + " selected one captured atomic level/stat/weapon generation; "
                           + (monsterData == IncompleteRebuildMonsterData
                               ? "two normal local-player hits span 17..35; "
                               : "one normal local-player hit is 19; ")
                           + "item owns runtime damage and recharge; captured AttackInfo ammo "
                           + (monsterData == IncompleteRebuildMonsterData ? "9" : "17")
                           + ", slot 6, unknown 0.",
                WeaponLowId = weapon.LowId,
                WeaponHighId = weapon.HighId,
                WeaponQuality = weapon.Quality,
                WeaponInventorySlot = 6,
                HasCapturedEquippedAttackInfo = true,
                AttackInfoAmmoCount = monsterData == IncompleteRebuildMonsterData ? 9 : 17,
                AttackInfoWeaponSlot = 6,
                AttackInfoUnknown = 0,
                AttackInfoWeaponInstance = 0
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
