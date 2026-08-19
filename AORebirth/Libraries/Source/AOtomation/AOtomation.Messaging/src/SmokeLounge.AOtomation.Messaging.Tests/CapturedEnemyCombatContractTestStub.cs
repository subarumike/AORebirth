namespace AORebirth.Core.Playfields
{
    using System;
    using System.Linq;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.Playfields;

    internal enum CapturedEnemyAttackModel
    {
        Unresolved = 0,
        FixedAttackInfo = 1,
        EquippedWeapon = 2,
        Specialized = 3,
        BasicCaptureBackedOrdinary = 4
    }

    internal sealed class CapturedEnemyWeaponStatDefinition
    {
        internal CapturedEnemyWeaponStatDefinition(CharacterStat stat, uint value)
        {
            this.Stat = stat;
            this.Value = value;
        }

        internal CharacterStat Stat { get; private set; }

        internal uint Value { get; private set; }
    }

    internal sealed class CapturedEnemyWeaponDefinition
    {
        private static readonly CharacterStat[] RequiredStatOrder =
        {
            CharacterStat.Flags,
            CharacterStat.StaticInstance,
            CharacterStat.ACGItemLevel,
            CharacterStat.ACGItemTemplateID,
            CharacterStat.ACGItemTemplateID2,
            CharacterStat.MultipleCount,
            CharacterStat.Energy,
            CharacterStat.AttackDelay,
            CharacterStat.RechargeDelay
        };

        internal CapturedEnemyWeaponDefinition(
            string evidence,
            int evidenceSourceIdentity,
            byte n3Unknown,
            int unknown1,
            int inventorySlot,
            int stateMachineType,
            int stateMachineInstance,
            short unknown2,
            CapturedEnemyWeaponStatDefinition[] stats,
            int unknown3)
        {
            this.Evidence = evidence ?? string.Empty;
            this.EvidenceSourceIdentity = evidenceSourceIdentity;
            this.N3Unknown = n3Unknown;
            this.Unknown1 = unknown1;
            this.InventorySlot = inventorySlot;
            this.StateMachineType = stateMachineType;
            this.StateMachineInstance = stateMachineInstance;
            this.Unknown2 = unknown2;
            this.Stats = stats ?? new CapturedEnemyWeaponStatDefinition[0];
            this.Unknown3 = unknown3;
        }

        internal string Evidence { get; private set; }

        internal int EvidenceSourceIdentity { get; private set; }

        internal byte N3Unknown { get; private set; }

        internal int Unknown1 { get; private set; }

        internal int InventorySlot { get; private set; }

        internal int StateMachineType { get; private set; }

        internal int StateMachineInstance { get; private set; }

        internal short Unknown2 { get; private set; }

        internal CapturedEnemyWeaponStatDefinition[] Stats { get; private set; }

        internal int Unknown3 { get; private set; }

        internal int LowId
        {
            get { return this.SignedStatValue(CharacterStat.ACGItemTemplateID); }
        }

        internal int HighId
        {
            get { return this.SignedStatValue(CharacterStat.ACGItemTemplateID2); }
        }

        internal int Quality
        {
            get { return this.SignedStatValue(CharacterStat.ACGItemLevel); }
        }

        internal int InitialEnergy
        {
            get { return this.SignedStatValue(CharacterStat.Energy); }
        }

        internal bool IsValid
        {
            get
            {
                if (string.IsNullOrWhiteSpace(this.Evidence)
                    || this.EvidenceSourceIdentity == 0
                    || this.Unknown1 != 0x0b
                    || this.InventorySlot <= 0
                    || this.StateMachineType == 0
                    || this.Unknown2 != (short)(0x0100 | (this.InventorySlot & 0xff))
                    || this.Stats.Length != RequiredStatOrder.Length)
                {
                    return false;
                }

                for (int index = 0; index < RequiredStatOrder.Length; index++)
                {
                    if (this.Stats[index] == null || this.Stats[index].Stat != RequiredStatOrder[index])
                    {
                        return false;
                    }
                }

                return this.LowId > 0
                       && this.HighId > 0
                       && this.Quality > 0
                       && this.SignedStatValue(CharacterStat.StaticInstance) == this.LowId
                       && this.SignedStatValue(CharacterStat.MultipleCount) > 0;
            }
        }

        internal int SignedStatValue(CharacterStat stat)
        {
            CapturedEnemyWeaponStatDefinition value = this.Stats.SingleOrDefault(
                candidate => candidate != null && candidate.Stat == stat);
            return value == null ? 0 : unchecked((int)value.Value);
        }

        internal CapturedEnemyWeaponDefinition WithEvidenceSourceIdentity(int sourceIdentity)
        {
            return new CapturedEnemyWeaponDefinition(
                this.Evidence,
                sourceIdentity,
                this.N3Unknown,
                this.Unknown1,
                this.InventorySlot,
                this.StateMachineType,
                this.StateMachineInstance,
                this.Unknown2,
                this.Stats,
                this.Unknown3);
        }

        internal CapturedEnemyWeaponDefinition WithProductionWeaponQuality(int quality)
        {
            return new CapturedEnemyWeaponDefinition(
                this.Evidence,
                this.EvidenceSourceIdentity,
                this.N3Unknown,
                this.Unknown1,
                this.InventorySlot,
                this.StateMachineType,
                this.StateMachineInstance,
                this.Unknown2,
                this.Stats.Select(
                    value => value.Stat == CharacterStat.ACGItemLevel
                                 ? new CapturedEnemyWeaponStatDefinition(
                                     value.Stat,
                                     unchecked((uint)quality))
                                 : value).ToArray(),
                this.Unknown3);
        }

        internal CapturedEnemyWeaponDefinition WithProductionWeaponLoadout(
            int lowId,
            int highId,
            int quality)
        {
            return new CapturedEnemyWeaponDefinition(
                this.Evidence,
                this.EvidenceSourceIdentity,
                this.N3Unknown,
                this.Unknown1,
                this.InventorySlot,
                this.StateMachineType,
                this.StateMachineInstance,
                this.Unknown2,
                this.Stats.Select(
                    value =>
                    {
                        uint replacement;
                        switch (value.Stat)
                        {
                            case CharacterStat.StaticInstance:
                            case CharacterStat.ACGItemTemplateID:
                                replacement = unchecked((uint)lowId);
                                break;
                            case CharacterStat.ACGItemLevel:
                                replacement = unchecked((uint)quality);
                                break;
                            case CharacterStat.ACGItemTemplateID2:
                                replacement = unchecked((uint)highId);
                                break;
                            default:
                                return value;
                        }

                        return new CapturedEnemyWeaponStatDefinition(
                            value.Stat,
                            replacement);
                    }).ToArray(),
                this.Unknown3);
        }
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
            byte attackInfoN3Unknown,
            bool sendAttackInfo,
            int[] capturedDamageObservations = null,
            int? lethalAttackInfoUnknown = null)
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
            this.AttackInfoN3Unknown = attackInfoN3Unknown;
            this.SendAttackInfo = sendAttackInfo;
            this.CapturedDamageObservations = capturedDamageObservations == null
                                                  ? new int[0]
                                                  : capturedDamageObservations.ToArray();
            this.LethalAttackInfoUnknown = lethalAttackInfoUnknown;
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

        internal byte AttackInfoN3Unknown { get; set; }

        internal bool SendAttackInfo { get; set; }

        internal int[] CapturedDamageObservations { get; private set; }

        internal int? LethalAttackInfoUnknown { get; private set; }

        internal CapturedEnemyCombatAttackDefinition WithCapturedDamageObservations(
            int[] capturedDamageObservations,
            int? lethalAttackInfoUnknown = null)
        {
            return new CapturedEnemyCombatAttackDefinition(
                this.MinDamage,
                this.MaxDamage,
                this.DamageBonus,
                this.Range,
                this.RechargeSeconds,
                this.UsesEquippedWeapon,
                this.AttackInfoAmmoCount,
                this.AttackInfoWeaponSlot,
                this.AttackInfoUnknown,
                this.AttackInfoHitType,
                this.AttackInfoWeaponInstance,
                this.AttackInfoN3Unknown,
                this.SendAttackInfo,
                capturedDamageObservations,
                lethalAttackInfoUnknown ?? this.LethalAttackInfoUnknown);
        }

        internal bool IsValid
        {
            get
            {
                return this.MinDamage > 0
                       && this.MaxDamage >= this.MinDamage
                       && this.RechargeSeconds > 0;
            }
        }

        internal bool IsValidOneShot
        {
            get
            {
                return this.MinDamage > 0
                       && this.MaxDamage >= this.MinDamage
                       && this.RechargeSeconds == 0.0d;
            }
        }
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
            int specialAttackWeaponUnknown5,
            byte specialAttackWeaponN3Unknown,
            byte attackN3Unknown,
            byte attackAction)
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
            this.SpecialAttackWeaponN3Unknown = specialAttackWeaponN3Unknown;
            this.AttackN3Unknown = attackN3Unknown;
            this.AttackAction = attackAction;
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

        internal byte SpecialAttackWeaponN3Unknown { get; set; }

        internal byte AttackN3Unknown { get; set; }

        internal byte AttackAction { get; set; }

        internal bool IsValid
        {
            get
            {
                return this.InitialAttackDelaySeconds >= 0
                       && (this.OpeningAttack == null || this.OpeningAttack.IsValid)
                       && this.RepeatingAttack != null
                       && this.RepeatingAttack.IsValid;
            }
        }
    }

    internal sealed class CapturedEnemyParallelAttackStreamDefinition
    {
        internal CapturedEnemyParallelAttackStreamDefinition(
            double initialDelaySeconds,
            CapturedEnemyCombatAttackDefinition attack,
            bool repeats = true)
        {
            this.InitialDelaySeconds = initialDelaySeconds;
            this.Attack = attack;
            this.Repeats = repeats;
        }

        internal double InitialDelaySeconds { get; private set; }

        internal CapturedEnemyCombatAttackDefinition Attack { get; private set; }

        internal bool Repeats { get; private set; }

        internal bool IsValid
        {
            get
            {
                return this.InitialDelaySeconds >= 0
                       && this.Attack != null
                       && (this.Repeats
                               ? this.Attack.IsValid
                               : this.Attack.IsValidOneShot);
            }
        }
    }

    internal sealed class CapturedEnemyParallelAttackSequenceDefinition
    {
        internal CapturedEnemyParallelAttackSequenceDefinition(
            CapturedEnemyParallelAttackStreamDefinition[] streams,
            CapturedEnemySpecialAttackDefinition[] specialAttacks,
            int specialAttackWeaponUnknown1,
            int specialAttackWeaponUnknown2,
            int specialAttackWeaponUnknown3,
            int specialAttackWeaponUnknown4,
            int specialAttackWeaponUnknown5,
            byte specialAttackWeaponN3Unknown,
            byte attackN3Unknown,
            byte attackAction,
            double attackStartDelaySeconds = 0.0d)
        {
            this.Streams = streams;
            this.SpecialAttacks = specialAttacks;
            this.SpecialAttackWeaponUnknown1 = specialAttackWeaponUnknown1;
            this.SpecialAttackWeaponUnknown2 = specialAttackWeaponUnknown2;
            this.SpecialAttackWeaponUnknown3 = specialAttackWeaponUnknown3;
            this.SpecialAttackWeaponUnknown4 = specialAttackWeaponUnknown4;
            this.SpecialAttackWeaponUnknown5 = specialAttackWeaponUnknown5;
            this.SpecialAttackWeaponN3Unknown = specialAttackWeaponN3Unknown;
            this.AttackN3Unknown = attackN3Unknown;
            this.AttackAction = attackAction;
            this.AttackStartDelaySeconds = attackStartDelaySeconds;
        }

        internal CapturedEnemyParallelAttackStreamDefinition[] Streams { get; private set; }

        internal CapturedEnemySpecialAttackDefinition[] SpecialAttacks { get; private set; }

        internal int SpecialAttackWeaponUnknown1 { get; private set; }

        internal int SpecialAttackWeaponUnknown2 { get; private set; }

        internal int SpecialAttackWeaponUnknown3 { get; private set; }

        internal int SpecialAttackWeaponUnknown4 { get; private set; }

        internal int SpecialAttackWeaponUnknown5 { get; private set; }

        internal byte SpecialAttackWeaponN3Unknown { get; private set; }

        internal byte AttackN3Unknown { get; private set; }

        internal byte AttackAction { get; private set; }

        internal double AttackStartDelaySeconds { get; private set; }

        internal bool IsValid
        {
            get
            {
                return this.Streams != null
                       && this.Streams.Length > 0
                       && !double.IsNaN(this.AttackStartDelaySeconds)
                       && !double.IsInfinity(this.AttackStartDelaySeconds)
                       && this.AttackStartDelaySeconds >= 0.0d
                       && this.Streams.All(
                           stream => stream != null && stream.IsValid);
            }
        }
    }

    internal sealed class CapturedEnemyCombatContract
    {
        private bool declaredCombatReady;

        internal static CapturedEnemyCombatContract CapturedFixedPacketSequence(
            string evidence,
            int evidenceSourceIdentity,
            ZoneEngine.Core.NpcAiProfile aiProfile,
            int minDamage,
            int maxDamage,
            double rechargeSeconds,
            CapturedEnemySpecialAttackDefinition[] specialAttacks,
            byte specialAttackWeaponN3Unknown,
            int specialAttackWeaponUnknown1,
            int specialAttackWeaponUnknown2,
            int specialAttackWeaponUnknown3,
            int specialAttackWeaponUnknown4,
            int specialAttackWeaponUnknown5,
            byte attackN3Unknown,
            byte attackAction,
            int attackInfoAmmoCount,
            int attackInfoWeaponSlot,
            int attackInfoDamageTypeWire,
            int attackInfoHitTypeWire,
            int attackInfoWeaponInstance,
            byte attackInfoN3Unknown,
            bool requiresDamageLineOfSight,
            int[] capturedDamageObservations = null,
            double[] capturedAttackStartDelayObservationsSeconds = null,
            double[] capturedFirstHitDelayObservationsSeconds = null,
            double[] capturedLandedIntervalObservationsSeconds = null,
            int? capturedDamageBonus = null,
            bool? capturedUsesEquippedWeapon = null,
            double? capturedAttackRange = null,
            bool? capturedSendAttackInfo = null)
        {
            int[] damageObservations = capturedDamageObservations == null
                                           ? new int[0]
                                           : capturedDamageObservations.ToArray();
            double[] attackStartDelayObservations =
                capturedAttackStartDelayObservationsSeconds == null
                    ? new double[0]
                    : capturedAttackStartDelayObservationsSeconds.ToArray();
            double[] firstHitDelayObservations =
                capturedFirstHitDelayObservationsSeconds == null
                    ? new double[0]
                    : capturedFirstHitDelayObservationsSeconds.ToArray();
            double[] landedIntervalObservations =
                capturedLandedIntervalObservationsSeconds == null
                    ? new double[0]
                    : capturedLandedIntervalObservationsSeconds.ToArray();
            return new CapturedEnemyCombatContract
            {
                Evidence = evidence,
                EvidenceSourceIdentity = evidenceSourceIdentity,
                Retaliates = true,
                AiProfile = aiProfile,
                AttackModel = CapturedEnemyAttackModel.FixedAttackInfo,
                MinDamage = minDamage,
                MaxDamage = maxDamage,
                RechargeSeconds = rechargeSeconds,
                CapturedDamageObservations = damageObservations,
                CapturedAttackStartDelayObservationsSeconds = attackStartDelayObservations,
                CapturedFirstHitDelayObservationsSeconds = firstHitDelayObservations,
                CapturedLandedIntervalObservationsSeconds = landedIntervalObservations,
                AttackStartDelaySeconds = attackStartDelayObservations.Length == 0
                                              ? 0.0d
                                              : attackStartDelayObservations[0],
                FirstHitDelaySeconds = firstHitDelayObservations.Length == 0
                                           ? 0.0d
                                           : firstHitDelayObservations[0],
                CapturedDamageBonus = capturedDamageBonus ?? 0,
                CapturedUsesEquippedWeapon = capturedUsesEquippedWeapon ?? false,
                CapturedAttackRange = capturedAttackRange,
                SendCapturedAttackInfo = capturedSendAttackInfo ?? false,
                HasCapturedFixedAttackBehavior = capturedDamageBonus.HasValue
                                                 && capturedUsesEquippedWeapon.HasValue
                                                 && capturedSendAttackInfo.HasValue,
                CapturedSpecialAttacks = specialAttacks
                    ?? new CapturedEnemySpecialAttackDefinition[0],
                HasCapturedRequiredPacketFields = true,
                HasCapturedSpecialAttackWeaponContext = true,
                HasEmptySpecialAttackWeaponContext = specialAttacks == null
                                                     || specialAttacks.Length == 0,
                HasCapturedAttackStartContext = true,
                SpecialAttackWeaponN3Unknown = specialAttackWeaponN3Unknown,
                SpecialAttackWeaponUnknown1 = specialAttackWeaponUnknown1,
                SpecialAttackWeaponUnknown2 = specialAttackWeaponUnknown2,
                SpecialAttackWeaponUnknown3 = specialAttackWeaponUnknown3,
                SpecialAttackWeaponUnknown4 = specialAttackWeaponUnknown4,
                SpecialAttackWeaponUnknown5 = specialAttackWeaponUnknown5,
                AttackN3Unknown = attackN3Unknown,
                AttackAction = attackAction,
                AttackInfoAmmoCount = attackInfoAmmoCount,
                AttackInfoWeaponSlot = attackInfoWeaponSlot,
                AttackInfoUnknown = attackInfoDamageTypeWire,
                AttackInfoHitType = attackInfoHitTypeWire,
                AttackInfoWeaponInstance = attackInfoWeaponInstance,
                AttackInfoN3Unknown = attackInfoN3Unknown,
                RequiresDamageLineOfSight = requiresDamageLineOfSight,
                IsCombatReady = true
            };
        }

        internal static CapturedEnemyCombatContract CapturedSpecialSequence(
            string evidence,
            CapturedEnemySpecialAttackSequenceDefinition specialAttackSequence)
        {
            CapturedEnemyCombatContract contract = new CapturedEnemyCombatContract
            {
                AttackModel = CapturedEnemyAttackModel.Specialized,
                Evidence = evidence,
                Retaliates = true,
                AiProfile = ZoneEngine.Core.NpcAiProfile.Passive,
                SpecialAttackSequence = specialAttackSequence,
                HasCapturedSpecialAttackWeaponContext = true,
                CapturedSpecialAttacks = specialAttackSequence.SpecialAttacks,
                HasCapturedAttackStartContext = true,
                SpecialAttackWeaponN3Unknown =
                    specialAttackSequence.SpecialAttackWeaponN3Unknown,
                SpecialAttackWeaponUnknown1 =
                    specialAttackSequence.SpecialAttackWeaponUnknown1,
                SpecialAttackWeaponUnknown2 =
                    specialAttackSequence.SpecialAttackWeaponUnknown2,
                SpecialAttackWeaponUnknown3 =
                    specialAttackSequence.SpecialAttackWeaponUnknown3,
                SpecialAttackWeaponUnknown4 =
                    specialAttackSequence.SpecialAttackWeaponUnknown4,
                SpecialAttackWeaponUnknown5 =
                    specialAttackSequence.SpecialAttackWeaponUnknown5,
                AttackN3Unknown = specialAttackSequence.AttackN3Unknown,
                AttackAction = specialAttackSequence.AttackAction
            };
            contract.RefreshSpecializedReadiness();
            return contract;
        }

        internal static CapturedEnemyCombatContract CapturedParallelAttackSequence(
            string evidence,
            CapturedEnemyParallelAttackSequenceDefinition parallelAttackSequence,
            bool requiresDamageLineOfSight = false,
            ZoneEngine.Core.NpcAiProfile aiProfile = ZoneEngine.Core.NpcAiProfile.Passive)
        {
            CapturedEnemyCombatContract contract = new CapturedEnemyCombatContract
            {
                AttackModel = CapturedEnemyAttackModel.Specialized,
                Evidence = evidence,
                Retaliates = true,
                AiProfile = aiProfile,
                ParallelAttackSequence = parallelAttackSequence,
                RequiresDamageLineOfSight = requiresDamageLineOfSight,
                HasCapturedSpecialAttackWeaponContext = true,
                CapturedSpecialAttacks = parallelAttackSequence.SpecialAttacks,
                HasCapturedAttackStartContext = true,
                SpecialAttackWeaponN3Unknown =
                    parallelAttackSequence.SpecialAttackWeaponN3Unknown,
                SpecialAttackWeaponUnknown1 =
                    parallelAttackSequence.SpecialAttackWeaponUnknown1,
                SpecialAttackWeaponUnknown2 =
                    parallelAttackSequence.SpecialAttackWeaponUnknown2,
                SpecialAttackWeaponUnknown3 =
                    parallelAttackSequence.SpecialAttackWeaponUnknown3,
                SpecialAttackWeaponUnknown4 =
                    parallelAttackSequence.SpecialAttackWeaponUnknown4,
                SpecialAttackWeaponUnknown5 =
                    parallelAttackSequence.SpecialAttackWeaponUnknown5,
                AttackN3Unknown = parallelAttackSequence.AttackN3Unknown,
                AttackAction = parallelAttackSequence.AttackAction
            };
            contract.RefreshSpecializedReadiness();
            return contract;
        }

        internal static CapturedEnemyCombatContract CapturedProfileSelector(
            string evidence,
            int evidenceSourceIdentityHint,
            string profileSelectorHint,
            ZoneEngine.Core.NpcAiProfile aiProfile,
            double? capturedAttackRange,
            int? specialAttackWeaponUnknown5,
            double? attackStartDelaySeconds,
            bool requiresDamageLineOfSight = false)
        {
            return new CapturedEnemyCombatContract
            {
                AttackModel = CapturedEnemyAttackModel.Unresolved,
                IsCombatReady = false,
                Evidence = evidence,
                Retaliates = true,
                AiProfile = aiProfile,
                EvidenceSourceIdentityHint = evidenceSourceIdentityHint,
                EvidenceProfileSelectorHint = profileSelectorHint ?? string.Empty,
                EvidenceSpecialAttackWeaponUnknown5Hint = specialAttackWeaponUnknown5,
                EvidenceAttackStartDelaySecondsHint = attackStartDelaySeconds,
                CapturedAttackRange = capturedAttackRange,
                RequiresDamageLineOfSight = requiresDamageLineOfSight
            };
        }

        internal static CapturedEnemyCombatContract Unresolved(
            string evidence,
            bool retaliationObserved)
        {
            return new CapturedEnemyCombatContract
            {
                AttackModel = CapturedEnemyAttackModel.Unresolved,
                IsCombatReady = false,
                Evidence = evidence,
                Retaliates = retaliationObserved,
                AiProfile = ZoneEngine.Core.NpcAiProfile.Passive
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
            int attackInfoAmmoCount,
            int attackInfoHitType,
            byte attackInfoN3Unknown)
        {
            return new CapturedEnemyCombatContract
            {
                AttackModel = CapturedEnemyAttackModel.FixedAttackInfo,
                IsCombatReady = false,
                Evidence = evidence,
                Retaliates = true,
                AiProfile = ZoneEngine.Core.NpcAiProfile.Passive,
                MinDamage = minDamage,
                MaxDamage = maxDamage,
                RechargeSeconds = rechargeSeconds,
                AttackInfoWeaponSlot = weaponSlot,
                AttackInfoUnknown = attackInfoUnknown,
                AttackInfoWeaponInstance = weaponInstance,
                AttackInfoAmmoCount = attackInfoAmmoCount,
                AttackInfoHitType = attackInfoHitType,
                AttackInfoN3Unknown = attackInfoN3Unknown
            };
        }

        internal static CapturedEnemyCombatContract EquippedWeapon(
            string evidence,
            int lowId,
            int highId,
            int quality,
            int inventorySlot,
            bool requiresDamageLineOfSight = false)
        {
            return new CapturedEnemyCombatContract
            {
                AttackModel = CapturedEnemyAttackModel.EquippedWeapon,
                IsCombatReady = false,
                Evidence = evidence,
                Retaliates = true,
                AiProfile = ZoneEngine.Core.NpcAiProfile.Passive,
                WeaponLowId = lowId,
                WeaponHighId = highId,
                WeaponQuality = quality,
                WeaponInventorySlot = inventorySlot,
                RequiresDamageLineOfSight = requiresDamageLineOfSight
            };
        }

        internal static CapturedEnemyCombatContract EquippedWeaponWithCapturedPacketSequence(
            string evidence,
            int evidenceSourceIdentity,
            int lowId,
            int highId,
            int quality,
            int inventorySlot,
            bool usesEquippedWeaponDamage,
            int minDamage,
            int maxDamage,
            int damageBonus,
            double? attackRange,
            double attackStartDelaySeconds,
            double movementTransitionDelaySeconds,
            double firstHitDelaySeconds,
            double rechargeSeconds,
            bool hasCapturedCombatStopSequence,
            bool sendStopFightOnDeath,
            int attackInfoAmmoCount,
            int attackInfoUnknown,
            int specialAttackWeaponUnknown1,
            int specialAttackWeaponUnknown2,
            int specialAttackWeaponUnknown3,
            int specialAttackWeaponUnknown4,
            int specialAttackWeaponUnknown5,
            int attackInfoHitType,
            byte attackInfoN3Unknown,
            byte specialAttackWeaponN3Unknown,
            byte attackN3Unknown,
            byte attackAction,
            bool requiresDamageLineOfSight = false,
            bool usesEquippedWeaponTiming = false,
            ZoneEngine.Core.NpcAiProfile aiProfile = ZoneEngine.Core.NpcAiProfile.Passive)
        {
            return new CapturedEnemyCombatContract
            {
                AttackModel = CapturedEnemyAttackModel.EquippedWeapon,
                IsCombatReady = false,
                Evidence = evidence,
                Retaliates = true,
                EvidenceSourceIdentity = evidenceSourceIdentity,
                HasCapturedRequiredPacketFields = true,
                UsesEquippedWeaponDamage = usesEquippedWeaponDamage,
                UsesEquippedWeaponTiming = usesEquippedWeaponTiming,
                AiProfile = aiProfile,
                MinDamage = minDamage,
                MaxDamage = maxDamage,
                CapturedDamageBonus = damageBonus,
                CapturedAttackRange = attackRange,
                WeaponLowId = lowId,
                WeaponHighId = highId,
                WeaponQuality = quality,
                WeaponInventorySlot = inventorySlot,
                HasEmptySpecialAttackWeaponContext = true,
                HasCapturedAttackStartContext = true,
                HasCapturedEquippedAttackInfo = true,
                HasCapturedCombatStopSequence = hasCapturedCombatStopSequence,
                AttackInfoAmmoCount = attackInfoAmmoCount,
                AttackInfoWeaponSlot = inventorySlot,
                AttackInfoUnknown = attackInfoUnknown,
                AttackInfoWeaponInstance = 0,
                AttackInfoHitType = attackInfoHitType,
                AttackInfoN3Unknown = attackInfoN3Unknown,
                SpecialAttackWeaponN3Unknown = specialAttackWeaponN3Unknown,
                AttackN3Unknown = attackN3Unknown,
                AttackAction = attackAction,
                AttackStartDelaySeconds = attackStartDelaySeconds,
                MovementTransitionDelaySeconds = movementTransitionDelaySeconds,
                FirstHitDelaySeconds = firstHitDelaySeconds,
                RechargeSeconds = rechargeSeconds,
                SendStopFightOnDeath = sendStopFightOnDeath,
                SpecialAttackWeaponUnknown1 = specialAttackWeaponUnknown1,
                SpecialAttackWeaponUnknown2 = specialAttackWeaponUnknown2,
                SpecialAttackWeaponUnknown3 = specialAttackWeaponUnknown3,
                SpecialAttackWeaponUnknown4 = specialAttackWeaponUnknown4,
                SpecialAttackWeaponUnknown5 = specialAttackWeaponUnknown5,
                RequiresDamageLineOfSight = requiresDamageLineOfSight
            };
        }

        internal CapturedEnemyAttackModel AttackModel { get; set; }

        internal bool Retaliates { get; set; }

        internal ZoneEngine.Core.NpcAiProfile AiProfile { get; set; }

        internal int EvidenceSourceIdentity { get; set; }

        internal int EvidenceSourceIdentityHint { get; set; }

        internal string EvidenceProfileSelectorHint { get; set; }

        internal int? EvidenceSpecialAttackWeaponUnknown5Hint { get; set; }

        internal double? EvidenceAttackStartDelaySecondsHint { get; set; }

        internal bool HasCapturedRequiredPacketFields { get; set; }

        internal bool UsesEquippedWeaponDamage { get; set; }

        internal bool UsesEquippedWeaponTiming { get; set; }

        internal bool UsesProductionWeaponQuality { get; set; }

        internal bool UsesProductionSpecializedValues { get; set; }

        internal bool UsesProductionEquippedWeaponValues { get; set; }

        internal bool UsesProductionActorValuesForPresentationWeapon { get; set; }

        internal bool UsesCaptureProvenArchetype { get; set; }

        internal string CaptureProvenArchetypeId { get; set; }

        internal int CapturedDamageBonus { get; set; }

        internal double? CapturedAttackRange { get; set; }

        internal int[] CapturedDamageObservations { get; set; }

        internal double[] CapturedAttackStartDelayObservationsSeconds { get; set; }

        internal double[] CapturedFirstHitDelayObservationsSeconds { get; set; }

        internal double[] CapturedLandedIntervalObservationsSeconds { get; set; }

        internal bool CapturedUsesEquippedWeapon { get; set; }

        internal bool SendCapturedAttackInfo { get; set; }

        internal bool HasCapturedFixedAttackBehavior { get; set; }

        internal bool IsCombatReady
        {
            get
            {
                if (!this.declaredCombatReady)
                {
                    return false;
                }

                if (this.AttackModel == CapturedEnemyAttackModel.FixedAttackInfo)
                {
                    bool completeAttackSource = this.AttackInfoWeaponInstance == 0
                        ? this.AttackInfoWeaponSlot == 0
                          || (this.AttackInfoWeaponSlot == 6
                              && this.WeaponDefinition != null
                              && this.WeaponDefinition.IsValid
                              && this.WeaponDefinition.InventorySlot == 6)
                        : this.CapturedSpecialAttacks != null
                          && this.CapturedSpecialAttacks.Any(
                              value => value != null
                                       && value.Tag == this.AttackInfoWeaponInstance);
                    bool ammoMatches = this.WeaponDefinition == null
                        || (this.WeaponDefinition.InitialEnergy == -1
                            ? this.AttackInfoAmmoCount == -1
                            : this.WeaponDefinition.InitialEnergy == 0
                                ? this.AttackInfoAmmoCount == 0
                                : this.WeaponDefinition.InitialEnergy > 0
                                  && this.AttackInfoAmmoCount
                                     == this.WeaponDefinition.InitialEnergy - 1);
                    return this.Retaliates
                           && this.EvidenceSourceIdentity != 0
                           && this.HasCapturedRequiredPacketFields
                           && this.HasCapturedSpecialAttackWeaponContext
                           && this.HasCapturedAttackStartContext
                           && this.MinDamage > 0
                           && this.MaxDamage >= this.MinDamage
                           && this.RechargeSeconds > 0
                           && this.HasCapturedFixedAttackBehavior
                           && this.SendCapturedAttackInfo
                           && this.CapturedDamageObservations != null
                           && this.CapturedDamageObservations.Length > 0
                           && this.CapturedDamageObservations.All(value => value > 0)
                           && this.CapturedDamageObservations.Min() == this.MinDamage
                           && this.CapturedDamageObservations.Max() == this.MaxDamage
                           && this.CapturedAttackStartDelayObservationsSeconds != null
                           && this.CapturedAttackStartDelayObservationsSeconds.Length > 0
                           && this.CapturedAttackStartDelayObservationsSeconds.All(
                               value => value >= 0.0d)
                           && this.CapturedFirstHitDelayObservationsSeconds != null
                           && this.CapturedFirstHitDelayObservationsSeconds.Length > 0
                           && this.CapturedFirstHitDelayObservationsSeconds.All(
                               value => value >= 0.0d)
                           && this.CapturedAttackStartDelayObservationsSeconds.Length
                              == this.CapturedFirstHitDelayObservationsSeconds.Length
                           && this.CapturedLandedIntervalObservationsSeconds != null
                           && this.CapturedLandedIntervalObservationsSeconds.Length > 0
                           && this.CapturedLandedIntervalObservationsSeconds.All(
                               value => value > 0.0d)
                           && Math.Abs(
                               this.AttackStartDelaySeconds
                               - this.CapturedAttackStartDelayObservationsSeconds[0]) < 0.000001d
                           && Math.Abs(
                               this.FirstHitDelaySeconds
                               - this.CapturedFirstHitDelayObservationsSeconds[0]) < 0.000001d
                           && Math.Abs(
                               this.RechargeSeconds
                               - this.CapturedLandedIntervalObservationsSeconds[0]) < 0.000001d
                           && completeAttackSource
                           && ammoMatches;
                }

                if (this.AttackModel == CapturedEnemyAttackModel.EquippedWeapon)
                {
                    return this.EvidenceSourceIdentity > 0
                           && this.HasCapturedRequiredPacketFields
                           && this.HasCapturedEquippedAttackInfo
                           && this.HasCapturedAttackStartContext
                           && this.WeaponDefinition != null
                           && this.WeaponDefinition.IsValid
                           && this.WeaponDefinition.EvidenceSourceIdentity
                           == this.EvidenceSourceIdentity
                           && this.WeaponDefinition.LowId == this.WeaponLowId
                           && this.WeaponDefinition.HighId == this.WeaponHighId
                           && this.WeaponDefinition.Quality == this.WeaponQuality
                           && this.WeaponDefinition.InventorySlot == this.WeaponInventorySlot
                           && this.AttackInfoWeaponSlot == this.WeaponInventorySlot
                           && this.AttackInfoWeaponInstance == 0
                           && (this.UsesEquippedWeaponTiming
                               || this.FirstHitDelaySeconds > 0)
                           && (this.UsesEquippedWeaponTiming
                               || this.RechargeSeconds > 0)
                           && (this.UsesEquippedWeaponDamage
                               || (this.MinDamage > 0
                                   && this.MaxDamage >= this.MinDamage))
                           && (this.WeaponDefinition.InitialEnergy == -1
                                   ? this.AttackInfoAmmoCount == -1
                                   : this.WeaponDefinition.InitialEnergy == 0
                                       ? this.AttackInfoAmmoCount == 0
                                       : this.WeaponDefinition.InitialEnergy > 0
                                         && this.AttackInfoAmmoCount
                                            == this.WeaponDefinition.InitialEnergy - 1);
                }

                if (this.AttackModel == CapturedEnemyAttackModel.Specialized)
                {
                    if (this.SpecialAttackSequence != null)
                    {
                        return this.EvidenceSourceIdentity > 0
                               && this.SpecialAttackSequence.IsValid
                               && this.AttackHasCompleteSource(
                                   this.SpecialAttackSequence.OpeningAttack,
                                   this.SpecialAttackSequence.SpecialAttacks)
                               && this.AttackHasCompleteSource(
                                   this.SpecialAttackSequence.RepeatingAttack,
                                   this.SpecialAttackSequence.SpecialAttacks);
                    }

                    return this.EvidenceSourceIdentity > 0
                           && this.ParallelAttackSequence != null
                           && this.ParallelAttackSequence.Streams != null
                           && this.ParallelAttackSequence.Streams.Length > 0
                           && this.ParallelAttackSequence.Streams.All(
                               stream => stream != null
                                         && stream.IsValid
                                         && this.AttackHasCompleteSource(
                                             stream.Attack,
                                             this.ParallelAttackSequence.SpecialAttacks));
                }

                return false;
            }

            set { this.declaredCombatReady = value; }
        }

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

        internal CapturedEnemyWeaponDefinition WeaponDefinition { get; set; }

        internal bool HasEmptySpecialAttackWeaponContext { get; set; }

        internal bool HasCapturedSpecialAttackWeaponContext
        {
            get { return this.hasCapturedSpecialAttackWeaponContext || this.HasEmptySpecialAttackWeaponContext; }
            set { this.hasCapturedSpecialAttackWeaponContext = value; }
        }

        internal CapturedEnemySpecialAttackDefinition[] CapturedSpecialAttacks { get; set; }

        internal bool HasCapturedAttackStartContext { get; set; }

        internal bool HasCapturedEquippedAttackInfo { get; set; }

        internal bool HasCapturedCombatStopSequence { get; set; }

        internal int AttackInfoAmmoCount { get; set; }

        internal double FirstHitDelaySeconds { get; set; }

        internal double AttackStartDelaySeconds { get; set; }

        internal double MovementTransitionDelaySeconds { get; set; }

        internal bool SendStopFightOnDeath { get; set; }

        internal int AttackInfoHitType { get; set; }

        internal byte AttackInfoN3Unknown { get; set; }

        internal byte SpecialAttackWeaponN3Unknown { get; set; }

        internal byte AttackN3Unknown { get; set; }

        internal byte AttackAction { get; set; }

        internal int SpecialAttackWeaponUnknown1 { get; set; }

        internal int SpecialAttackWeaponUnknown2 { get; set; }

        internal int SpecialAttackWeaponUnknown3 { get; set; }

        internal int SpecialAttackWeaponUnknown4 { get; set; }

        internal int SpecialAttackWeaponUnknown5 { get; set; }

        internal int[] CapturedSpecialAttackWeaponUnknown5Observations { get; set; }

        internal CapturedEnemySpecialAttackSequenceDefinition SpecialAttackSequence { get; set; }

        internal CapturedEnemyParallelAttackSequenceDefinition ParallelAttackSequence { get; set; }

        internal bool RequiresDamageLineOfSight { get; set; }

        internal bool IsQuarantined
        {
            get { return !this.IsCombatReady; }
        }

        internal string QuarantineReason
        {
            get { return this.IsCombatReady ? string.Empty : "captured attack packet context is incomplete"; }
        }

        internal CapturedEnemyCombatContract WithCapturedWeapon(
            CapturedEnemyWeaponDefinition weaponDefinition)
        {
            this.WeaponDefinition = weaponDefinition;
            this.ApplyCapturedWeaponIdentity(weaponDefinition);
            this.RefreshSpecializedReadiness();
            return this;
        }

        internal CapturedEnemyCombatContract WithEvidenceSourceHint(int sourceIdentity)
        {
            var clone = (CapturedEnemyCombatContract)this.MemberwiseClone();
            clone.EvidenceSourceIdentityHint = sourceIdentity;
            return clone;
        }

        internal CapturedEnemyCombatContract WithEvidenceProfileSelectorHint(
            string profileSelector)
        {
            var clone = (CapturedEnemyCombatContract)this.MemberwiseClone();
            clone.EvidenceProfileSelectorHint = profileSelector ?? string.Empty;
            return clone;
        }

        internal CapturedEnemyCombatContract WithCaptureProvenRetaliationEligibility(
            string eligibilityEvidence)
        {
            var clone = (CapturedEnemyCombatContract)this.MemberwiseClone();
            clone.Retaliates = true;
            clone.AiProfile = ZoneEngine.Core.NpcAiProfile.Passive;
            if (!string.IsNullOrWhiteSpace(eligibilityEvidence)
                && (string.IsNullOrWhiteSpace(clone.Evidence)
                    || clone.Evidence.IndexOf(
                        eligibilityEvidence,
                        StringComparison.Ordinal) < 0))
            {
                clone.Evidence = string.IsNullOrWhiteSpace(clone.Evidence)
                                     ? eligibilityEvidence
                                     : clone.Evidence + "; " + eligibilityEvidence;
            }
            return clone;
        }

        internal CapturedEnemyCombatContract WithCaptureProvenArchetype(
            string archetypeId)
        {
            var clone = (CapturedEnemyCombatContract)this.MemberwiseClone();
            clone.UsesCaptureProvenArchetype = true;
            clone.CaptureProvenArchetypeId = archetypeId ?? string.Empty;
            return clone;
        }

        internal CapturedEnemyCombatContract WithCapturedAttackRange(
            double capturedAttackRange)
        {
            var clone = (CapturedEnemyCombatContract)this.MemberwiseClone();
            clone.CapturedAttackRange = capturedAttackRange;
            return clone;
        }

        internal CapturedEnemyCombatContract WithProductionWeaponQuality()
        {
            var clone = (CapturedEnemyCombatContract)this.MemberwiseClone();
            clone.UsesProductionWeaponQuality = true;
            return clone;
        }

        internal CapturedEnemyCombatContract WithProductionSpecializedValues()
        {
            var clone = (CapturedEnemyCombatContract)this.MemberwiseClone();
            clone.UsesProductionSpecializedValues = true;
            return clone;
        }

        internal CapturedEnemyCombatContract WithProductionEquippedWeaponValues()
        {
            var clone = (CapturedEnemyCombatContract)this.MemberwiseClone();
            clone.UsesProductionEquippedWeaponValues = true;
            clone.UsesEquippedWeaponDamage = true;
            clone.UsesEquippedWeaponTiming = true;
            return clone;
        }

        internal CapturedEnemyCombatContract WithProductionActorValuesForPresentationWeapon()
        {
            var clone = (CapturedEnemyCombatContract)this.MemberwiseClone();
            clone.UsesProductionActorValuesForPresentationWeapon = true;
            return clone;
        }

        internal CapturedEnemyCombatContract WithCapturedSpecialAttackWeaponUnknown5Observations(
            int[] observations)
        {
            if (observations == null || observations.Length == 0)
            {
                return this;
            }

            var clone = (CapturedEnemyCombatContract)this.MemberwiseClone();
            clone.CapturedSpecialAttackWeaponUnknown5Observations = observations.ToArray();
            clone.SpecialAttackWeaponUnknown5 = observations[0];
            return clone;
        }

        internal CapturedEnemyCombatContract WithCapturedSpecializedDamageObservations(
            int[][] capturedDamageObservationsByAttack,
            int?[] lethalAttackInfoUnknownByAttack)
        {
            if (this.AttackModel != CapturedEnemyAttackModel.Specialized
                || capturedDamageObservationsByAttack == null
                || lethalAttackInfoUnknownByAttack == null
                || lethalAttackInfoUnknownByAttack.Length
                   != capturedDamageObservationsByAttack.Length)
            {
                return null;
            }

            var clone = (CapturedEnemyCombatContract)this.MemberwiseClone();
            if (this.SpecialAttackSequence != null)
            {
                CapturedEnemySpecialAttackSequenceDefinition sequence = this.SpecialAttackSequence;
                int expectedAttackCount = sequence.OpeningAttack == null ? 1 : 2;
                if (capturedDamageObservationsByAttack.Length != expectedAttackCount)
                {
                    return null;
                }

                int observationIndex = 0;
                CapturedEnemyCombatAttackDefinition openingAttack = sequence.OpeningAttack == null
                                                                          ? null
                                                                          : sequence.OpeningAttack.WithCapturedDamageObservations(
                                                                              capturedDamageObservationsByAttack[observationIndex],
                                                                              lethalAttackInfoUnknownByAttack[observationIndex++]);
                CapturedEnemyCombatAttackDefinition repeatingAttack =
                    sequence.RepeatingAttack.WithCapturedDamageObservations(
                        capturedDamageObservationsByAttack[observationIndex],
                        lethalAttackInfoUnknownByAttack[observationIndex]);
                clone.SpecialAttackSequence = new CapturedEnemySpecialAttackSequenceDefinition(
                    sequence.InitialAttackDelaySeconds,
                    openingAttack,
                    repeatingAttack,
                    sequence.SpecialAttacks,
                    sequence.SpecialAttackWeaponUnknown1,
                    sequence.SpecialAttackWeaponUnknown2,
                    sequence.SpecialAttackWeaponUnknown3,
                    sequence.SpecialAttackWeaponUnknown4,
                    sequence.SpecialAttackWeaponUnknown5,
                    sequence.SpecialAttackWeaponN3Unknown,
                    sequence.AttackN3Unknown,
                    sequence.AttackAction);
                return clone;
            }

            if (this.ParallelAttackSequence == null
                || capturedDamageObservationsByAttack.Length
                   != this.ParallelAttackSequence.Streams.Length)
            {
                return null;
            }

            CapturedEnemyParallelAttackSequenceDefinition parallelSequence =
                this.ParallelAttackSequence;
            var enrichedStreams = new CapturedEnemyParallelAttackStreamDefinition[
                parallelSequence.Streams.Length];
            for (int index = 0; index < parallelSequence.Streams.Length; index++)
            {
                CapturedEnemyParallelAttackStreamDefinition stream = parallelSequence.Streams[index];
                enrichedStreams[index] = new CapturedEnemyParallelAttackStreamDefinition(
                    stream.InitialDelaySeconds,
                    stream.Attack.WithCapturedDamageObservations(
                        capturedDamageObservationsByAttack[index],
                        lethalAttackInfoUnknownByAttack[index]));
            }

            clone.ParallelAttackSequence = new CapturedEnemyParallelAttackSequenceDefinition(
                enrichedStreams,
                parallelSequence.SpecialAttacks,
                parallelSequence.SpecialAttackWeaponUnknown1,
                parallelSequence.SpecialAttackWeaponUnknown2,
                parallelSequence.SpecialAttackWeaponUnknown3,
                parallelSequence.SpecialAttackWeaponUnknown4,
                parallelSequence.SpecialAttackWeaponUnknown5,
                parallelSequence.SpecialAttackWeaponN3Unknown,
                parallelSequence.AttackN3Unknown,
                parallelSequence.AttackAction);
            return clone;
        }

        internal CapturedEnemyCombatContract WithCaptureCertification(
            string generatedEvidence,
            int evidenceSourceIdentity,
            CapturedEnemyWeaponDefinition weaponDefinition)
        {
            var clone = (CapturedEnemyCombatContract)this.MemberwiseClone();
            clone.Evidence = string.IsNullOrWhiteSpace(generatedEvidence)
                ? this.Evidence
                : generatedEvidence;
            clone.EvidenceSourceIdentity = evidenceSourceIdentity;
            clone.WeaponDefinition = weaponDefinition;
            clone.ApplyCapturedWeaponIdentity(weaponDefinition);
            clone.IsCombatReady = true;
            return clone;
        }

        private bool HasExplicitCapturedAttackRange()
        {
            return this.CapturedAttackRange.HasValue
                   && this.CapturedAttackRange.Value > 0.0d
                   && !double.IsNaN(this.CapturedAttackRange.Value)
                   && !double.IsInfinity(this.CapturedAttackRange.Value);
        }

        private bool HasCapturedWeaponAttackRangeSource()
        {
            return !this.CapturedAttackRange.HasValue
                   && this.CapturedUsesEquippedWeapon
                   && this.AttackInfoWeaponInstance == 0
                   && this.WeaponDefinition != null
                   && this.WeaponDefinition.IsValid
                   && this.AttackInfoWeaponSlot == this.WeaponDefinition.InventorySlot
                   && this.WeaponInventorySlot == this.WeaponDefinition.InventorySlot
                   && this.WeaponLowId == this.WeaponDefinition.LowId
                   && this.WeaponHighId == this.WeaponDefinition.HighId
                   && this.WeaponQuality == this.WeaponDefinition.Quality;
        }

        private void ApplyCapturedWeaponIdentity(CapturedEnemyWeaponDefinition weaponDefinition)
        {
            if (weaponDefinition == null)
            {
                return;
            }

            this.WeaponLowId = weaponDefinition.LowId;
            this.WeaponHighId = weaponDefinition.HighId;
            this.WeaponQuality = weaponDefinition.Quality;
            this.WeaponInventorySlot = weaponDefinition.InventorySlot;
        }

        private bool hasCapturedSpecialAttackWeaponContext;

        private void RefreshSpecializedReadiness()
        {
            if (this.AttackModel == CapturedEnemyAttackModel.EquippedWeapon)
            {
                this.IsCombatReady = this.EvidenceSourceIdentity > 0
                                     && this.HasCapturedRequiredPacketFields
                                     && this.HasCapturedEquippedAttackInfo
                                     && this.HasCapturedAttackStartContext
                                     && this.WeaponDefinition != null
                                     && this.WeaponDefinition.IsValid
                                     && this.WeaponDefinition.EvidenceSourceIdentity
                                     == this.EvidenceSourceIdentity
                                     && this.WeaponDefinition.LowId == this.WeaponLowId
                                     && this.WeaponDefinition.HighId == this.WeaponHighId
                                     && this.WeaponDefinition.Quality == this.WeaponQuality
                                     && this.WeaponDefinition.InventorySlot == this.WeaponInventorySlot
                                     && this.AttackInfoWeaponSlot == this.WeaponInventorySlot
                                     && this.AttackInfoWeaponInstance == 0
                                     && (this.UsesEquippedWeaponTiming
                                         || this.FirstHitDelaySeconds > 0)
                                     && (this.UsesEquippedWeaponTiming
                                         || this.RechargeSeconds > 0)
                                     && (this.UsesEquippedWeaponDamage
                                         || (this.MinDamage > 0
                                             && this.MaxDamage >= this.MinDamage
                                             && this.HasExplicitCapturedAttackRange()))
                                     && (this.WeaponDefinition.InitialEnergy == -1
                                             ? this.AttackInfoAmmoCount == -1
                                             : this.WeaponDefinition.InitialEnergy == 0
                                                 ? this.AttackInfoAmmoCount == 0
                                                 : this.WeaponDefinition.InitialEnergy > 0
                                                   && this.AttackInfoAmmoCount
                                                      == this.WeaponDefinition.InitialEnergy - 1);
                return;
            }

            if (this.AttackModel != CapturedEnemyAttackModel.Specialized)
            {
                return;
            }

            if (this.SpecialAttackSequence != null)
            {
                this.IsCombatReady = this.EvidenceSourceIdentity > 0
                                     && this.SpecialAttackSequence.IsValid
                                     && this.AttackHasCompleteSource(
                                         this.SpecialAttackSequence.OpeningAttack,
                                         this.SpecialAttackSequence.SpecialAttacks)
                                     && this.AttackHasCompleteSource(
                                         this.SpecialAttackSequence.RepeatingAttack,
                                         this.SpecialAttackSequence.SpecialAttacks);
                return;
            }

            this.IsCombatReady = this.EvidenceSourceIdentity > 0
                                 && this.ParallelAttackSequence != null
                                 && this.ParallelAttackSequence.Streams != null
                                 && this.ParallelAttackSequence.Streams.Length > 0
                                 && this.ParallelAttackSequence.Streams.All(
                                     stream => stream != null
                                               && stream.IsValid
                                               && this.AttackHasCompleteSource(
                                                   stream.Attack,
                                                   this.ParallelAttackSequence.SpecialAttacks));
        }

        private bool AttackHasCompleteSource(
            CapturedEnemyCombatAttackDefinition attack,
            CapturedEnemySpecialAttackDefinition[] specials)
        {
            if (attack == null)
            {
                return true;
            }

            if (attack.AttackInfoWeaponSlot == 6 && attack.AttackInfoWeaponInstance == 0)
            {
                return this.WeaponDefinition != null
                       && this.WeaponDefinition.IsValid
                       && this.WeaponDefinition.InventorySlot == attack.AttackInfoWeaponSlot;
            }

            if (attack.AttackInfoWeaponInstance == 0)
            {
                return attack.AttackInfoWeaponSlot == 0;
            }

            return specials != null
                   && specials.Any(value => value != null && value.Tag == attack.AttackInfoWeaponInstance);
        }
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

        internal static CapturedEnemyCombatContract For(string name, int monsterData)
        {
            if (monsterData == NpcCombatAttackRules.CapturedSubwayFilthFleaMonsterData)
            {
                return CapturedEnemyCombatContract.CapturedSpecialSequence(
                        "20260708-004038 and 20260709-193914: Filth Flea normal slot rolls with criticals excluded",
                        new CapturedEnemySpecialAttackSequenceDefinition(
                            NpcCombatAttackRules.CapturedSubwayFilthFleaInitialAttackSeconds,
                            new CapturedEnemyCombatAttackDefinition(
                                NpcCombatAttackRules.CapturedSubwayFilthFleaPoisonMinimumDamage,
                                NpcCombatAttackRules.CapturedSubwayFilthFleaPoisonMaximumDamage,
                                0,
                                NpcCombatAttackRules.MaxMeleeCombatDistance,
                                NpcCombatAttackRules.CapturedSubwayFilthFleaPoisonRechargeSeconds,
                                false,
                                NpcCombatAttackRules.UnarmedAttackInfoAmmoCount,
                                NpcCombatAttackRules.CapturedSubwayFilthFleaPoisonWeaponSlot,
                                0,
                                NpcCombatAttackRules.NormalAttackInfoHitType,
                                NpcCombatAttackRules.CapturedSubwayFilthFleaStickToHeadTag,
                                0,
                                true),
                            new CapturedEnemyCombatAttackDefinition(
                                NpcCombatAttackRules.CapturedSubwayFilthFleaMeleeMinimumDamage,
                                NpcCombatAttackRules.CapturedSubwayFilthFleaMeleeMaximumDamage,
                                0,
                                NpcCombatAttackRules.MaxMeleeCombatDistance,
                                NpcCombatAttackRules.CapturedSubwayFilthFleaMeleeRechargeSeconds,
                                false,
                                NpcCombatAttackRules.UnarmedAttackInfoAmmoCount,
                                NpcCombatAttackRules.CapturedSubwayFilthFleaMeleeWeaponSlot,
                                0,
                                NpcCombatAttackRules.NormalAttackInfoHitType,
                                NpcCombatAttackRules.CapturedSubwayFilthFleaArmsTag,
                                0,
                                true),
                            new[]
                            {
                                new CapturedEnemySpecialAttackDefinition(
                                    NpcCombatAttackRules.CapturedSubwayFilthFleaStickToHeadLowTemplate,
                                    NpcCombatAttackRules.CapturedSubwayFilthFleaStickToHeadHighTemplate,
                                    NpcCombatAttackRules.CapturedSubwayFilthFleaStickToHeadTag,
                                    NpcCombatAttackRules.CapturedSubwayFilthFleaStickToHeadName),
                                new CapturedEnemySpecialAttackDefinition(
                                    NpcCombatAttackRules.CapturedSubwayFilthFleaArmsLowTemplate,
                                    NpcCombatAttackRules.CapturedSubwayFilthFleaArmsHighTemplate,
                                    NpcCombatAttackRules.CapturedSubwayFilthFleaArmsTag,
                                    NpcCombatAttackRules.CapturedSubwayFilthFleaArmsName)
                            },
                            NpcCombatAttackRules.CapturedSubwayFilthFleaSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayFilthFleaSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayFilthFleaSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayFilthFleaSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayFilthFleaSpecialAttackWeaponLastValue,
                            0,
                            0,
                            0))
                    .WithProductionSpecializedValues();
            }

            if (monsterData == BloodcreeperMonsterData)
            {
                return CapturedEnemyCombatContract.CapturedParallelAttackSequence(
                    "Bloodcreeper captured dual natural attack sequence.",
                    new CapturedEnemyParallelAttackSequenceDefinition(
                        new[]
                        {
                            new CapturedEnemyParallelAttackStreamDefinition(
                                NpcCombatAttackRules
                                    .CapturedSubwayBloodcreeperSpitInitialSeconds,
                                new CapturedEnemyCombatAttackDefinition(
                                    NpcCombatAttackRules
                                        .CapturedSubwayBloodcreeperSpitMinimumDamage,
                                    NpcCombatAttackRules
                                        .CapturedSubwayBloodcreeperSpitMaximumDamage,
                                    0,
                                    NpcCombatAttackRules.MaxMeleeCombatDistance,
                                    NpcCombatAttackRules
                                        .CapturedSubwayBloodcreeperSpitRechargeSeconds,
                                    false,
                                    NpcCombatAttackRules
                                        .UnarmedAttackInfoAmmoCount,
                                    NpcCombatAttackRules
                                        .CapturedSubwayBloodcreeperSpitWeaponSlot,
                                    0,
                                    NpcCombatAttackRules.NormalAttackInfoHitType,
                                    NpcCombatAttackRules
                                        .CapturedSubwayBloodcreeperSpitTag,
                                    0,
                                    true)),
                            new CapturedEnemyParallelAttackStreamDefinition(
                                NpcCombatAttackRules
                                    .CapturedSubwayBloodcreeperBiteInitialSeconds,
                                new CapturedEnemyCombatAttackDefinition(
                                    NpcCombatAttackRules
                                        .CapturedSubwayBloodcreeperBiteMinimumDamage,
                                    NpcCombatAttackRules
                                        .CapturedSubwayBloodcreeperBiteMaximumDamage,
                                    0,
                                    NpcCombatAttackRules.MaxMeleeCombatDistance,
                                    NpcCombatAttackRules
                                        .CapturedSubwayBloodcreeperBiteRechargeSeconds,
                                    false,
                                    NpcCombatAttackRules
                                        .UnarmedAttackInfoAmmoCount,
                                    NpcCombatAttackRules
                                        .CapturedSubwayBloodcreeperBiteWeaponSlot,
                                    0,
                                    NpcCombatAttackRules.NormalAttackInfoHitType,
                                    NpcCombatAttackRules
                                        .CapturedSubwayBloodcreeperBiteTag,
                                    0,
                                    true))
                        },
                        new[]
                        {
                            new CapturedEnemySpecialAttackDefinition(
                                NpcCombatAttackRules
                                    .CapturedSubwayBloodcreeperSpitLowTemplate,
                                NpcCombatAttackRules
                                    .CapturedSubwayBloodcreeperSpitHighTemplate,
                                NpcCombatAttackRules
                                    .CapturedSubwayBloodcreeperSpitTag,
                                NpcCombatAttackRules
                                    .CapturedSubwayBloodcreeperSpitName),
                            new CapturedEnemySpecialAttackDefinition(
                                NpcCombatAttackRules
                                    .CapturedSubwayBloodcreeperBiteLowTemplate,
                                NpcCombatAttackRules
                                    .CapturedSubwayBloodcreeperBiteHighTemplate,
                                NpcCombatAttackRules
                                    .CapturedSubwayBloodcreeperBiteTag,
                                NpcCombatAttackRules
                                    .CapturedSubwayBloodcreeperBiteName)
                        },
                        NpcCombatAttackRules
                            .CapturedSubwayBloodcreeperSpecialAttackWeaponValue,
                        NpcCombatAttackRules
                            .CapturedSubwayBloodcreeperSpecialAttackWeaponValue,
                        NpcCombatAttackRules
                            .CapturedSubwayBloodcreeperSpecialAttackWeaponValue,
                        NpcCombatAttackRules
                            .CapturedSubwayBloodcreeperSpecialAttackWeaponValue,
                        NpcCombatAttackRules
                            .CapturedSubwayBloodcreeperSpecialAttackWeaponLastValue,
                        0,
                        0,
                        0))
                    .WithProductionSpecializedValues();
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
                }.WithProductionSpecializedValues();
            }

            if (monsterData == 203733)
            {
                return CapturedEnemyCombatContract.Unresolved(
                    "Violent Vagabond has no landed own-source damage; adjacent Mugger substitution is forbidden",
                    true);
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
            if (monsterData
                == NpcCombatAttackRules.CapturedSubwayFilthFleaMonsterData)
            {
                OrdinaryEnemyCombatNumericSetup generated;
                if (!level.HasValue
                    || !OrdinaryEnemyCombatSetupGenerator.TryGenerateFilthFlea(
                        level.Value,
                        out generated))
                {
                    return CapturedEnemyCombatContract.Unresolved(
                        "Filth Flea mathematical combat setup is unsupported.",
                        true);
                }

                CapturedEnemyCombatContract baseline = For(name, monsterData);
                CapturedEnemySpecialAttackSequenceDefinition sequence =
                    baseline.SpecialAttackSequence;
                return CapturedEnemyCombatContract.CapturedSpecialSequence(
                        baseline.Evidence,
                        new CapturedEnemySpecialAttackSequenceDefinition(
                            sequence.InitialAttackDelaySeconds,
                            sequence.OpeningAttack,
                            sequence.RepeatingAttack,
                            sequence.SpecialAttacks,
                            generated.SpecialAttackWeaponUnknown1,
                            generated.SpecialAttackWeaponUnknown2,
                            generated.SpecialAttackWeaponUnknown3,
                            generated.SpecialAttackWeaponUnknown4,
                            sequence.SpecialAttackWeaponUnknown5,
                            sequence.SpecialAttackWeaponN3Unknown,
                            sequence.AttackN3Unknown,
                            sequence.AttackAction))
                    .WithProductionSpecializedValues();
            }

            if (monsterData
                == NpcCombatAttackRules.CapturedSubwayDisobedientBotMonsterData)
            {
                OrdinaryEnemyCombatNumericSetup generated;
                if (!level.HasValue
                    || !OrdinaryEnemyCombatSetupGenerator.TryGenerate(
                        new OrdinaryEnemyCombatSetupInput(
                            monsterData,
                            level.Value,
                            NpcCombatAttackRules.CapturedSubwayDisobedientBotLowTemplate,
                            NpcCombatAttackRules.CapturedSubwayDisobedientBotHighTemplate,
                            NpcCombatAttackRules.CapturedSubwayDisobedientBotWeaponTag,
                            NpcCombatAttackRules.CapturedSubwayDisobedientBotWeaponName),
                        out generated))
                {
                    return CapturedEnemyCombatContract.Unresolved(
                        "Disobedient Bot mathematical combat setup is unsupported.",
                        true);
                }

                return CapturedEnemyCombatContract.CapturedSpecialSequence(
                        "Disobedient Bot capture-validated mathematical SIW1 setup.",
                        new CapturedEnemySpecialAttackSequenceDefinition(
                            NpcCombatAttackRules
                                .CapturedSubwayDisobedientBotInitialAttackSeconds,
                            null,
                            new CapturedEnemyCombatAttackDefinition(
                                NpcCombatAttackRules
                                    .CapturedSubwayDisobedientBotMinimumDamage,
                                NpcCombatAttackRules
                                    .CapturedSubwayDisobedientBotMaximumDamage,
                                0,
                                NpcCombatAttackRules.MaxMeleeCombatDistance,
                                NpcCombatAttackRules
                                    .CapturedSubwayDisobedientBotRechargeSeconds,
                                false,
                                NpcCombatAttackRules.UnarmedAttackInfoAmmoCount,
                                NpcCombatAttackRules
                                    .CapturedSubwayDisobedientBotWeaponSlot,
                                0,
                                NpcCombatAttackRules.NormalAttackInfoHitType,
                                NpcCombatAttackRules
                                    .CapturedSubwayDisobedientBotWeaponTag,
                                0,
                                true),
                            new[]
                            {
                                new CapturedEnemySpecialAttackDefinition(
                                    NpcCombatAttackRules
                                        .CapturedSubwayDisobedientBotLowTemplate,
                                    NpcCombatAttackRules
                                        .CapturedSubwayDisobedientBotHighTemplate,
                                    NpcCombatAttackRules
                                        .CapturedSubwayDisobedientBotWeaponTag,
                                    NpcCombatAttackRules
                                        .CapturedSubwayDisobedientBotWeaponName)
                            },
                            generated.SpecialAttackWeaponUnknown1,
                            generated.SpecialAttackWeaponUnknown2,
                            generated.SpecialAttackWeaponUnknown3,
                            generated.SpecialAttackWeaponUnknown4,
                            NpcCombatAttackRules
                                .CapturedSubwayDisobedientBotInitialSpecialAttackWeaponUnknown5,
                            0,
                            0,
                            0))
                    .WithProductionSpecializedValues();
            }

            if (monsterData == 26092)
            {
                if (level != 5)
                {
                    return CapturedEnemyCombatContract.Unresolved(
                        "The complete captured Thief contract is level 5 only.",
                        true);
                }

                const string evidence = "20260711-170337 raw 155/156,301/302,480/564/654";
                return CapturedEnemyCombatContract.EquippedWeaponWithCapturedPacketSequence(
                        evidence,
                        unchecked((int)0x795B5DB2u),
                        121567,
                        121567,
                        1,
                        6,
                        true,
                        0,
                        0,
                        0,
                        0,
                        NpcCombatAttackRules.CapturedSubwayThiefAttackStartDelaySeconds,
                        NpcCombatAttackRules.CapturedSubwayThiefMovementTransitionDelaySeconds,
                        NpcCombatAttackRules.CapturedSubwayThiefFirstHitDelaySeconds,
                        NpcCombatAttackRules.CapturedSubwayThiefRechargeSeconds,
                        true,
                        true,
                        NpcCombatAttackRules.CapturedSubwayThiefAttackInfoAmmoCount,
                        NpcCombatAttackRules.CapturedSubwayThiefAttackInfoUnknown,
                        NpcCombatAttackRules.CapturedSubwayThiefSpecialAttackWeaponUnknown1,
                        NpcCombatAttackRules.CapturedSubwayThiefSpecialAttackWeaponUnknown2,
                        NpcCombatAttackRules.CapturedSubwayThiefSpecialAttackWeaponUnknown3,
                        NpcCombatAttackRules.CapturedSubwayThiefSpecialAttackWeaponUnknown4,
                        NpcCombatAttackRules.CapturedSubwayThiefSpecialAttackWeaponUnknown5,
                        3,
                        0,
                        0,
                        0,
                        0)
                    .WithCapturedWeapon(
                        new CapturedEnemyWeaponDefinition(
                            evidence,
                            unchecked((int)0x795B5DB2u),
                            0,
                            11,
                            6,
                            1000015,
                            0,
                            262,
                            new[]
                            {
                                TestWeaponStat(CharacterStat.Flags, 67109889),
                                TestWeaponStat(CharacterStat.StaticInstance, 121567),
                                TestWeaponStat(CharacterStat.ACGItemLevel, 1),
                                TestWeaponStat(CharacterStat.ACGItemTemplateID, 121567),
                                TestWeaponStat(CharacterStat.ACGItemTemplateID2, 121567),
                                TestWeaponStat(CharacterStat.MultipleCount, 1),
                                TestWeaponStat(CharacterStat.Energy, -1),
                                TestWeaponStat(CharacterStat.AttackDelay, 235),
                                TestWeaponStat(CharacterStat.RechargeDelay, 235)
                            },
                            0));
            }

            if (monsterData == 203733 && level.HasValue)
            {
                OrdinaryEnemyCombatNumericSetup generated;
                if (!OrdinaryEnemyCombatSetupGenerator.TryGenerateEquipped(
                        new OrdinaryEnemyEquippedCombatSetupInput(
                            monsterData,
                            level.Value,
                            130590,
                            130590,
                            1,
                            6),
                        out generated))
                {
                    return CapturedEnemyCombatContract.Unresolved(
                        "Violent Vagabond mathematical combat setup is unsupported.",
                        true);
                }

                const int evidenceSourceIdentity = unchecked((int)0x794CD4CCu);
                return CapturedEnemyCombatContract
                    .EquippedWeaponWithCapturedPacketSequence(
                        "Violent Vagabond captured setup and categorical result domain",
                        evidenceSourceIdentity,
                        130590,
                        130590,
                        1,
                        6,
                        true,
                        0,
                        0,
                        0,
                        null,
                        0.0d,
                        0.0d,
                        0.0d,
                        0.0d,
                        false,
                        false,
                        0,
                        0,
                        generated.SpecialAttackWeaponUnknown1,
                        generated.SpecialAttackWeaponUnknown2,
                        generated.SpecialAttackWeaponUnknown3,
                        generated.SpecialAttackWeaponUnknown4,
                        0,
                        NpcCombatAttackRules.NormalAttackInfoHitType,
                        0,
                        0,
                        0,
                        0,
                        false,
                        true)
                    .WithCapturedWeapon(
                        new CapturedEnemyWeaponDefinition(
                            "Violent Vagabond WIFU",
                            evidenceSourceIdentity,
                            0,
                            11,
                            6,
                            1000015,
                            0,
                            262,
                            new[]
                            {
                                TestWeaponStat(CharacterStat.Flags, 4199425),
                                TestWeaponStat(CharacterStat.StaticInstance, 130590),
                                TestWeaponStat(CharacterStat.ACGItemLevel, 1),
                                TestWeaponStat(CharacterStat.ACGItemTemplateID, 130590),
                                TestWeaponStat(CharacterStat.ACGItemTemplateID2, 130590),
                                TestWeaponStat(CharacterStat.MultipleCount, 1),
                                TestWeaponStat(CharacterStat.Energy, 1),
                                TestWeaponStat(CharacterStat.AttackDelay, 175),
                                TestWeaponStat(CharacterStat.RechargeDelay, 175)
                            },
                            0))
                    .WithProductionEquippedWeaponValues()
                    .WithProductionActorValuesForPresentationWeapon();
            }

            return For(name, monsterData);
        }

        private static CapturedEnemyWeaponStatDefinition TestWeaponStat(
            CharacterStat stat,
            int value)
        {
            return new CapturedEnemyWeaponStatDefinition(stat, unchecked((uint)value));
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
            CapturedEnemyCombatContract contract = new CapturedEnemyCombatContract
            {
                AttackModel = runtimeReady
                    ? CapturedEnemyAttackModel.FixedAttackInfo
                    : CapturedEnemyAttackModel.Unresolved,
                IsCombatReady = runtimeReady,
                Retaliates = observed,
                AiProfile = ZoneEngine.Core.NpcAiProfile.Passive,
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
            if (archetype != null
                && (archetype.MonsterData == 203727
                    || archetype.MonsterData == 96056
                    || archetype.MonsterData == 203743
                    || archetype.MonsterData == 55648
                    || archetype.MonsterData == 30464
                    || archetype.MonsterData == 96195
                    || archetype.MonsterData == 203731
                    || archetype.MonsterData == 31909
                    || archetype.MonsterData == 203730
                    || archetype.MonsterData == 96193))
            {
                return contract.WithProductionSpecializedValues();
            }

            return archetype != null && archetype.MonsterData == 203746
                       ? contract.WithProductionEquippedWeaponValues()
                       : contract;
        }

        internal static CapturedEnemyCombatContract ForOrdinaryGeneratedSetup(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int level)
        {
            CapturedSubwayCombatEvidenceDefinition combat =
                archetype == null ? null : archetype.Combat;
            OrdinaryEnemyCombatNumericSetup generated;
            if (archetype == null
                || archetype.MonsterData
                   != NpcCombatAttackRules.CapturedSubwayStimFiendMonsterData
                || combat == null
                || !combat.Observed
                || !combat.RuntimeReady
                || !OrdinaryEnemyCombatSetupGenerator.TryGenerate(
                    new OrdinaryEnemyCombatSetupInput(
                        NpcCombatAttackRules.CapturedSubwayStimFiendMonsterData,
                        level,
                        NpcCombatAttackRules.CapturedSubwayStimFiendLowTemplate,
                        NpcCombatAttackRules.CapturedSubwayStimFiendHighTemplate,
                        NpcCombatAttackRules.CapturedSubwayStimFiendWeaponTag,
                        NpcCombatAttackRules.CapturedSubwayStimFiendWeaponName),
                    out generated))
            {
                return new CapturedEnemyCombatContract
                {
                    AttackModel = CapturedEnemyAttackModel.Unresolved,
                    IsCombatReady = false,
                    Retaliates = combat != null && combat.Observed,
                    Evidence = "Stim Fiend generated setup is unsupported for level "
                               + level
                };
            }

            return CapturedEnemyCombatContract
                .CapturedSpecialSequence(
                    string.Join(",", archetype.EvidenceCaptures)
                    + ": Stim Fiend mathematical SIW1 setup "
                    + generated.FormulaId,
                    new CapturedEnemySpecialAttackSequenceDefinition(
                        0.0d,
                        null,
                        new CapturedEnemyCombatAttackDefinition(
                            combat.MinDamage,
                            combat.MaxDamage,
                            0,
                            NpcCombatAttackRules.MaxMeleeCombatDistance,
                            combat.RechargeSeconds,
                            false,
                            NpcCombatAttackRules.UnarmedAttackInfoAmmoCount,
                            NpcCombatAttackRules.CapturedSubwayStimFiendWeaponSlot,
                            0,
                            NpcCombatAttackRules.NormalAttackInfoHitType,
                            NpcCombatAttackRules.CapturedSubwayStimFiendWeaponTag,
                            0,
                            true),
                        new[]
                        {
                            new CapturedEnemySpecialAttackDefinition(
                                NpcCombatAttackRules.CapturedSubwayStimFiendLowTemplate,
                                NpcCombatAttackRules.CapturedSubwayStimFiendHighTemplate,
                                NpcCombatAttackRules.CapturedSubwayStimFiendWeaponTag,
                                NpcCombatAttackRules.CapturedSubwayStimFiendWeaponName)
                        },
                        generated.SpecialAttackWeaponUnknown1,
                        generated.SpecialAttackWeaponUnknown2,
                        generated.SpecialAttackWeaponUnknown3,
                        generated.SpecialAttackWeaponUnknown4,
                        NpcCombatAttackRules
                            .CapturedSubwayStimFiendInitialSpecialAttackWeaponUnknown5,
                        0,
                        0,
                        0))
                .WithProductionSpecializedValues();
        }

        private static CapturedEnemyCombatContract ForGeneratedEquippedWeaponSetup(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int sourceInstance,
            int level,
            OrdinaryEnemySpawnWeaponLoadout weapon)
        {
            OrdinaryEnemyCombatNumericSetup generated;
            if (archetype == null
                || weapon == null
                || !OrdinaryEnemyCombatSetupGenerator.TryGenerateEquipped(
                    new OrdinaryEnemyEquippedCombatSetupInput(
                        archetype.MonsterData,
                        level,
                        weapon.LowId,
                        weapon.HighId,
                        weapon.Quality,
                        6),
                    out generated))
            {
                return CapturedEnemyCombatContract.Unresolved(
                    "Selected equipped generation is outside its proven formula domain",
                    archetype != null
                    && archetype.Combat != null
                    && archetype.Combat.Observed);
            }

            return CapturedEnemyCombatContract
                .EquippedWeaponWithCapturedPacketSequence(
                    weapon.Evidence,
                    sourceInstance,
                    weapon.LowId,
                    weapon.HighId,
                    weapon.Quality,
                    6,
                    true,
                    0,
                    0,
                    0,
                    null,
                    0.0d,
                    0.0d,
                    0.0d,
                    0.0d,
                    false,
                    false,
                    0,
                    0,
                    generated.SpecialAttackWeaponUnknown1,
                    generated.SpecialAttackWeaponUnknown2,
                    generated.SpecialAttackWeaponUnknown3,
                    generated.SpecialAttackWeaponUnknown4,
                    0,
                    NpcCombatAttackRules.NormalAttackInfoHitType,
                    0,
                    0,
                    0,
                    0,
                    false,
                    true)
                .WithProductionEquippedWeaponValues()
                .WithProductionWeaponQuality();
        }

        internal static CapturedEnemyCombatContract ForOrdinaryGeneratedSetup(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int sourceInstance,
            int level)
        {
            if (archetype == null)
            {
                return ForOrdinary(archetype);
            }

            if (archetype.MonsterData == IncompleteRebuildMonsterData
                || archetype.MonsterData
                   == NpcCombatAttackRules.CapturedSubwayMolestedMoleculesMonsterData)
            {
                CapturedSubwaySourceWeaponEvidenceDefinition matched =
                    archetype.SourceWeaponEvidence.SingleOrDefault(
                        value => value.SourceInstance == sourceInstance);
                return matched == null
                    ? (archetype.MonsterData
                       == NpcCombatAttackRules
                           .CapturedSubwayMolestedMoleculesMonsterData
                           ? ForOrdinary(archetype)
                               .WithEvidenceSourceHint(sourceInstance)
                           : CapturedEnemyCombatContract.Unresolved(
                               "Exact source weapon loadout is unavailable",
                               archetype.Combat != null
                               && archetype.Combat.Observed))
                    : ForGeneratedEquippedWeaponSetup(
                        archetype,
                        sourceInstance,
                        level,
                        new OrdinaryEnemySpawnWeaponLoadout(
                            matched.LowId,
                            matched.HighId,
                            matched.Quality,
                            matched.EvidenceCaptures));
            }

            return ForOrdinary(archetype, sourceInstance);
        }

        internal static CapturedEnemyCombatContract
            ForOrdinarySelectedAtomicGeneration(
                CapturedSubwayOrdinaryArchetypeDefinition archetype,
                int sourceInstance,
                int level,
                CapturedSubwayGenerationVariantDefinition[] generationEvidence)
        {
            CapturedSubwayGenerationVariantDefinition[] matches =
                (generationEvidence
                 ?? new CapturedSubwayGenerationVariantDefinition[0])
                    .Where(value => value != null && value.Level == level)
                    .ToArray();
            if (matches.Length != 1)
            {
                return CapturedEnemyCombatContract.Unresolved(
                    "Runtime level does not select one atomic generation",
                    archetype != null
                    && archetype.Combat != null
                    && archetype.Combat.Observed);
            }

            CapturedSubwayGenerationVariantDefinition selected = matches[0];
            return ForOrdinary(
                archetype,
                sourceInstance,
                new OrdinaryEnemySpawnVariant(
                    selected.Level,
                    selected.Health,
                    selected.HealthDamage,
                    selected.MonsterScale,
                    selected.RunSpeed,
                    selected.Evidence,
                    new OrdinaryEnemySpawnWeaponLoadout(
                        selected.WeaponLowId,
                        selected.WeaponHighId,
                        selected.WeaponQuality,
                        selected.Evidence)),
                generationEvidence);
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
            OrdinaryEnemyCombatNumericSetup generated;
            string atomicFailure = string.Empty;
            if (!hasExactCombatEvidence
                || !OrdinaryEnemyAtomicGenerationEvidenceValidator.TryValidateSelectedVariant(
                    FragmentedSoulMonsterData,
                    sourceInstance,
                    variant,
                    generationEvidence,
                    out atomicFailure)
                || !OrdinaryEnemyCombatSetupGenerator.TryGenerateEquipped(
                    new OrdinaryEnemyEquippedCombatSetupInput(
                        archetype.MonsterData,
                        variant.Level,
                        weapon.LowId,
                        weapon.HighId,
                        weapon.Quality,
                        NpcCombatAttackRules.CapturedSubwayFragmentedSoulWeaponSlot),
                    out generated))
            {
                return CapturedEnemyCombatContract.Unresolved(
                    "Fragmented Soul mathematical setup is unsupported",
                    hasExactCombatEvidence);
            }

            CapturedEnemyCombatContract contract = CapturedEnemyCombatContract
                .EquippedWeaponWithCapturedPacketSequence(
                    weapon.Evidence,
                    sourceInstance,
                    weapon.LowId,
                    weapon.HighId,
                    weapon.Quality,
                    NpcCombatAttackRules.CapturedSubwayFragmentedSoulWeaponSlot,
                    true,
                    0,
                    0,
                    0,
                    null,
                    0.0d,
                    0.0d,
                    0.0d,
                    0.0d,
                    false,
                    false,
                    0,
                    0,
                    generated.SpecialAttackWeaponUnknown1,
                    generated.SpecialAttackWeaponUnknown2,
                    generated.SpecialAttackWeaponUnknown3,
                    generated.SpecialAttackWeaponUnknown4,
                    0,
                    NpcCombatAttackRules.NormalAttackInfoHitType,
                    0,
                    0,
                    0,
                    0,
                    false,
                    true)
                .WithProductionEquippedWeaponValues()
                .WithProductionWeaponQuality();
            contract.Retaliates = true;
            contract.AiProfile = ZoneEngine.Core.NpcAiProfile.Passive;
            return contract;
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
                Retaliates = true,
                AiProfile = ZoneEngine.Core.NpcAiProfile.Passive,
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
                AttackInfoWeaponInstance = 0,
                UsesProductionWeaponQuality = true
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
                    Retaliates = true,
                    AiProfile = ZoneEngine.Core.NpcAiProfile.Passive,
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
                    UsesProductionWeaponQuality = true,
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
                    UsesProductionWeaponQuality = true,
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
                Retaliates = true,
                AiProfile = ZoneEngine.Core.NpcAiProfile.Passive,
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
                WeaponInventorySlot = 6,
                UsesProductionWeaponQuality = true
            };
        }

        internal static CapturedEnemyCombatContract ForOrdinary(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int sourceInstance,
            OrdinaryEnemySpawnVariant variant,
            CapturedSubwayGenerationVariantDefinition[] generationEvidence)
        {
            if (archetype != null
                && archetype.MonsterData
                   == NpcCombatAttackRules.CapturedSubwayMeldedPatternsMonsterData)
            {
                OrdinaryEnemySpawnWeaponLoadout meldedWeapon = variant == null
                    ? null
                    : variant.WeaponLoadout;
                OrdinaryEnemyCombatNumericSetup generated;
                string meldedAtomicFailure;
                if (!OrdinaryEnemyAtomicGenerationEvidenceValidator.TryValidateSelectedVariant(
                        NpcCombatAttackRules.CapturedSubwayMeldedPatternsMonsterData,
                        sourceInstance,
                        variant,
                        generationEvidence,
                        out meldedAtomicFailure)
                    || !OrdinaryEnemyCombatSetupGenerator.TryGenerateEquipped(
                        new OrdinaryEnemyEquippedCombatSetupInput(
                            archetype.MonsterData,
                            variant.Level,
                            meldedWeapon.LowId,
                            meldedWeapon.HighId,
                            meldedWeapon.Quality,
                            NpcCombatAttackRules.CapturedSubwayMeldedPatternsWeaponSlot),
                        out generated))
                {
                    return CapturedEnemyCombatContract.Unresolved(
                        "Melded Patterns mathematical setup is unsupported",
                        true);
                }

                return CapturedEnemyCombatContract
                    .EquippedWeaponWithCapturedPacketSequence(
                        meldedWeapon.Evidence,
                        sourceInstance,
                        meldedWeapon.LowId,
                        meldedWeapon.HighId,
                        meldedWeapon.Quality,
                        NpcCombatAttackRules.CapturedSubwayMeldedPatternsWeaponSlot,
                        true,
                        0,
                        0,
                        0,
                        null,
                        0.0d,
                        0.0d,
                        0.0d,
                        0.0d,
                        false,
                        false,
                        0,
                        0,
                        generated.SpecialAttackWeaponUnknown1,
                        generated.SpecialAttackWeaponUnknown2,
                        generated.SpecialAttackWeaponUnknown3,
                        generated.SpecialAttackWeaponUnknown4,
                        0,
                        NpcCombatAttackRules.NormalAttackInfoHitType,
                        0,
                        0,
                        0,
                        0,
                        false,
                        true)
                    .WithProductionEquippedWeaponValues();
            }

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

            if (archetype.MonsterData == IncompleteRebuildMonsterData)
            {
                string incompleteFailure;
                if (!OrdinaryEnemyAtomicGenerationEvidenceValidator
                        .TryValidateSelectedVariant(
                            IncompleteRebuildMonsterData,
                            sourceInstance,
                            variant,
                            generationEvidence,
                            out incompleteFailure))
                {
                    return CapturedEnemyCombatContract.Unresolved(
                        "Incomplete Rebuild atomic generation evidence is incomplete: "
                        + incompleteFailure,
                        archetype.Combat != null
                        && archetype.Combat.Observed);
                }

                return ForGeneratedEquippedWeaponSetup(
                    archetype,
                    sourceInstance,
                    variant.Level,
                    variant.WeaponLoadout);
            }

            CapturedEnemyCombatContract baseline = ForOrdinary(archetype, sourceInstance);
            int monsterData = archetype.MonsterData;
            const string displayName = "Redundant Scan";
            OrdinaryEnemySpawnWeaponLoadout weapon = variant == null
                ? null
                : variant.WeaponLoadout;
            string atomicFailure = string.Empty;
            if (baseline.AttackModel != CapturedEnemyAttackModel.EquippedWeapon
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
                Retaliates = true,
                AiProfile = ZoneEngine.Core.NpcAiProfile.Passive,
                Evidence = weapon.Evidence
                           + ": " + displayName
                           + " selected one captured atomic level/stat/weapon generation; "
                           + "one normal local-player hit is 19; "
                           + "item owns runtime damage and recharge; captured AttackInfo ammo "
                           + "17"
                           + ", slot 6, unknown 0.",
                WeaponLowId = weapon.LowId,
                WeaponHighId = weapon.HighId,
                WeaponQuality = weapon.Quality,
                WeaponInventorySlot = 6,
                UsesProductionWeaponQuality = true,
                UsesProductionEquippedWeaponValues = true,
                HasCapturedEquippedAttackInfo = true,
                AttackInfoAmmoCount = 17,
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
