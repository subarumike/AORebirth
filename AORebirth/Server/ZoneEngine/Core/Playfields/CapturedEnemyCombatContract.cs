namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Playfields;

    internal enum CapturedEnemyAttackModel
    {
        Unresolved,
        FixedAttackInfo,
        EquippedWeapon,
        Specialized
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

        internal int MinDamage { get; private set; }

        internal int MaxDamage { get; private set; }

        internal int DamageBonus { get; private set; }

        internal double Range { get; private set; }

        internal double RechargeSeconds { get; private set; }

        internal bool UsesEquippedWeapon { get; private set; }

        internal int AttackInfoAmmoCount { get; private set; }

        internal int AttackInfoWeaponSlot { get; private set; }

        internal int AttackInfoUnknown { get; private set; }

        internal int AttackInfoHitType { get; private set; }

        internal int AttackInfoWeaponInstance { get; private set; }

        internal byte AttackInfoN3Unknown { get; private set; }

        internal bool SendAttackInfo { get; private set; }

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
            this.SpecialAttacks = specialAttacks ?? new CapturedEnemySpecialAttackDefinition[0];
            this.SpecialAttackWeaponUnknown1 = specialAttackWeaponUnknown1;
            this.SpecialAttackWeaponUnknown2 = specialAttackWeaponUnknown2;
            this.SpecialAttackWeaponUnknown3 = specialAttackWeaponUnknown3;
            this.SpecialAttackWeaponUnknown4 = specialAttackWeaponUnknown4;
            this.SpecialAttackWeaponUnknown5 = specialAttackWeaponUnknown5;
            this.SpecialAttackWeaponN3Unknown = specialAttackWeaponN3Unknown;
            this.AttackN3Unknown = attackN3Unknown;
            this.AttackAction = attackAction;
        }

        internal double InitialAttackDelaySeconds { get; private set; }

        internal CapturedEnemyCombatAttackDefinition OpeningAttack { get; private set; }

        internal CapturedEnemyCombatAttackDefinition RepeatingAttack { get; private set; }

        internal CapturedEnemySpecialAttackDefinition[] SpecialAttacks { get; private set; }

        internal int SpecialAttackWeaponUnknown1 { get; private set; }

        internal int SpecialAttackWeaponUnknown2 { get; private set; }

        internal int SpecialAttackWeaponUnknown3 { get; private set; }

        internal int SpecialAttackWeaponUnknown4 { get; private set; }

        internal int SpecialAttackWeaponUnknown5 { get; private set; }

        internal byte SpecialAttackWeaponN3Unknown { get; private set; }

        internal byte AttackN3Unknown { get; private set; }

        internal byte AttackAction { get; private set; }

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

        internal DateTime ResolveNextTickAfterHit(DateTime now)
        {
            return this.Repeats
                       ? now + TimeSpan.FromSeconds(this.Attack.RechargeSeconds)
                       : DateTime.MaxValue;
        }

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
            this.Streams = streams ?? new CapturedEnemyParallelAttackStreamDefinition[0];
            this.SpecialAttacks = specialAttacks ?? new CapturedEnemySpecialAttackDefinition[0];
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
                if (this.Streams.Length == 0)
                {
                    return false;
                }

                if (double.IsNaN(this.AttackStartDelaySeconds)
                    || double.IsInfinity(this.AttackStartDelaySeconds)
                    || this.AttackStartDelaySeconds < 0.0d)
                {
                    return false;
                }

                foreach (CapturedEnemyParallelAttackStreamDefinition stream in this.Streams)
                {
                    if (stream == null || !stream.IsValid)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }

    internal sealed class CapturedEnemyCombatContract
    {
        private CapturedEnemyCombatContract()
        {
        }

        internal string Evidence { get; private set; }

        internal bool Retaliates { get; private set; }

        internal NpcAiProfile AiProfile { get; private set; }

        internal CapturedEnemyAttackModel AttackModel { get; private set; }

        internal int EvidenceSourceIdentity { get; private set; }

        internal int EvidenceSourceIdentityHint { get; private set; }

        internal string EvidenceProfileSelectorHint { get; private set; }

        internal int? EvidenceSpecialAttackWeaponUnknown5Hint { get; private set; }

        internal double? EvidenceAttackStartDelaySecondsHint { get; private set; }

        internal bool HasCapturedRequiredPacketFields { get; private set; }

        internal bool UsesEquippedWeaponDamage { get; private set; }

        internal bool UsesEquippedWeaponTiming { get; private set; }

        internal bool UsesProductionWeaponQuality { get; private set; }

        internal bool UsesProductionSpecializedValues { get; private set; }

        internal bool UsesProductionEquippedWeaponValues { get; private set; }

        internal bool UsesProductionActorValuesForPresentationWeapon { get; private set; }

        internal bool UsesCaptureProvenArchetype { get; private set; }

        internal string CaptureProvenArchetypeId { get; private set; }

        internal int CapturedDamageBonus { get; private set; }

        internal double? CapturedAttackRange { get; private set; }

        internal int[] CapturedDamageObservations { get; private set; }

        internal double[] CapturedAttackStartDelayObservationsSeconds { get; private set; }

        internal double[] CapturedFirstHitDelayObservationsSeconds { get; private set; }

        internal double[] CapturedLandedIntervalObservationsSeconds { get; private set; }

        internal bool CapturedUsesEquippedWeapon { get; private set; }

        internal bool SendCapturedAttackInfo { get; private set; }

        internal bool HasCapturedFixedAttackBehavior { get; private set; }

        internal int MinDamage { get; private set; }

        internal int MaxDamage { get; private set; }

        internal double RechargeSeconds { get; private set; }

        internal int AttackInfoWeaponSlot { get; private set; }

        internal int AttackInfoUnknown { get; private set; }

        internal int AttackInfoWeaponInstance { get; private set; }

        internal int WeaponLowId { get; private set; }

        internal int WeaponHighId { get; private set; }

        internal int WeaponQuality { get; private set; }

        internal int WeaponInventorySlot { get; private set; }

        internal CapturedEnemyWeaponDefinition WeaponDefinition { get; private set; }

        internal bool HasEmptySpecialAttackWeaponContext { get; private set; }

        internal bool HasCapturedSpecialAttackWeaponContext { get; private set; }

        internal CapturedEnemySpecialAttackDefinition[] CapturedSpecialAttacks { get; private set; }

        internal bool HasCapturedAttackStartContext { get; private set; }

        internal bool HasCapturedEquippedAttackInfo { get; private set; }

        internal bool HasCapturedCombatStopSequence { get; private set; }

        internal int AttackInfoAmmoCount { get; private set; }

        internal int AttackInfoHitType { get; private set; }

        internal byte AttackInfoN3Unknown { get; private set; }

        internal byte SpecialAttackWeaponN3Unknown { get; private set; }

        internal byte AttackN3Unknown { get; private set; }

        internal byte AttackAction { get; private set; }

        internal int SpecialAttackWeaponUnknown1 { get; private set; }

        internal int SpecialAttackWeaponUnknown2 { get; private set; }

        internal int SpecialAttackWeaponUnknown3 { get; private set; }

        internal int SpecialAttackWeaponUnknown4 { get; private set; }

        internal int SpecialAttackWeaponUnknown5 { get; private set; }

        internal int[] CapturedSpecialAttackWeaponUnknown5Observations { get; private set; }

        internal double AttackStartDelaySeconds { get; private set; }

        internal double MovementTransitionDelaySeconds { get; private set; }

        internal double FirstHitDelaySeconds { get; private set; }

        internal bool SendStopFightOnDeath { get; private set; }

        internal bool RequiresDamageLineOfSight { get; private set; }

        internal CapturedEnemySpecialAttackSequenceDefinition SpecialAttackSequence { get; private set; }

        internal CapturedEnemyParallelAttackSequenceDefinition ParallelAttackSequence { get; private set; }

        internal bool IsCombatReady
        {
            get
            {
                if (!this.Retaliates)
                {
                    return false;
                }

                switch (this.AttackModel)
                {
                    case CapturedEnemyAttackModel.FixedAttackInfo:
                        // Authored FixedAttackOnSight (mission/Arete/Lorelei): combat-ready without
                        // full corpus observations so mobs can retaliate with real AttackInfo.
                        if (this.IsAuthoredFixedAttackFallback())
                        {
                            return true;
                        }

                        return this.EvidenceSourceIdentity > 0
                               && this.HasCapturedRequiredPacketFields
                               && this.HasCapturedSpecialAttackWeaponContext
                               && this.HasCapturedAttackStartContext
                               && this.MinDamage > 0
                               && this.MaxDamage >= this.MinDamage
                               && this.RechargeSeconds > 0
                               && this.HasCompleteCapturedFixedRuntimeObservations()
                               && this.FixedAttackHasCompleteSource()
                               && (this.WeaponDefinition == null
                                   || (this.WeaponDefinition.IsValid
                                       && this.WeaponDefinition.EvidenceSourceIdentity
                                          == this.EvidenceSourceIdentity
                                       && this.AttackInfoAmmoMatchesCapturedEnergy()));
                    case CapturedEnemyAttackModel.EquippedWeapon:
                        return this.EvidenceSourceIdentity > 0
                               && this.HasCapturedRequiredPacketFields
                               && this.HasCapturedEquippedAttackInfo
                               && this.HasCapturedAttackStartContext
                               && this.WeaponDefinition != null
                               && this.WeaponDefinition.IsValid
                               && this.WeaponDefinition.EvidenceSourceIdentity
                               == this.EvidenceSourceIdentity
                               && this.WeaponLowId > 0
                               && this.WeaponHighId > 0
                               && this.WeaponQuality > 0
                               && this.WeaponInventorySlot > 0
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
                               && this.AttackInfoAmmoMatchesCapturedEnergy();
                    case CapturedEnemyAttackModel.Specialized:
                        return this.EvidenceSourceIdentity > 0
                               && (this.HasCompleteSpecialAttackSequence()
                                   || this.HasCompleteParallelAttackSequence());
                    default:
                        return false;
                }
            }
        }

        internal bool IsQuarantined
        {
            get { return !this.IsCombatReady; }
        }

        internal string QuarantineReason
        {
            get
            {
                if (!this.Retaliates)
                {
                    return "retaliation is not capture-proven";
                }

                if (this.AttackModel == CapturedEnemyAttackModel.Unresolved)
                {
                    return "captured attack contract is unresolved";
                }

                if (this.AttackModel == CapturedEnemyAttackModel.FixedAttackInfo)
                {
                    if (this.EvidenceSourceIdentity <= 0)
                    {
                        return "fixed packet source identity is missing";
                    }

                    if (!this.HasCapturedRequiredPacketFields)
                    {
                        return "fixed packet required fields are incomplete";
                    }

                    if (!this.HasCapturedSpecialAttackWeaponContext)
                    {
                        return "fixed packet SpecialAttackWeapon context is incomplete";
                    }

                    if (!this.HasCapturedAttackStartContext)
                    {
                        return "fixed packet Attack context is incomplete";
                    }

                    if (this.MinDamage <= 0 || this.MaxDamage < this.MinDamage)
                    {
                        return "fixed packet captured damage observations are invalid";
                    }

                    if (this.RechargeSeconds <= 0)
                    {
                        return "fixed packet captured landed interval is invalid";
                    }

                    if (!this.HasCompleteCapturedFixedRuntimeObservations())
                    {
                        return "fixed packet captured timing, damage, or attack-mode observations are incomplete";
                    }

                    if (!this.FixedAttackHasCompleteSource())
                    {
                        return "fixed packet attack source is incomplete";
                    }

                    if (this.WeaponDefinition != null
                        && (!this.WeaponDefinition.IsValid
                            || this.WeaponDefinition.EvidenceSourceIdentity
                               != this.EvidenceSourceIdentity
                            || !this.AttackInfoAmmoMatchesCapturedEnergy()))
                    {
                        return "fixed packet owner-linked weapon state is incomplete";
                    }

                    return "fixed packet contract is incomplete";
                }

                if (this.EvidenceSourceIdentity == 0)
                {
                    return "capture source identity is missing";
                }

                if (this.RequiresPhysicalWeaponDefinition() && this.WeaponDefinition == null)
                {
                    return "owner-linked WeaponItemFullUpdate evidence is missing";
                }

                if (this.WeaponDefinition != null && !this.WeaponDefinition.IsValid)
                {
                    return "owner-linked WeaponItemFullUpdate evidence is invalid";
                }

                return "captured attack packet context is incomplete";
            }
        }

        internal CapturedEnemyCombatContract WithCapturedWeapon(
            CapturedEnemyWeaponDefinition weaponDefinition)
        {
            this.WeaponDefinition = weaponDefinition;
            this.ApplyCapturedWeaponIdentity(weaponDefinition);
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
            clone.AiProfile = NpcAiProfile.Passive;
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
            if (!clone.CapturedAttackRange.HasValue)
            {
                clone.CapturedAttackRange = SharedParallelAttackRange(
                    clone.ParallelAttackSequence);
            }

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
                        lethalAttackInfoUnknownByAttack[index]),
                    stream.Repeats);
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
                parallelSequence.AttackAction,
                parallelSequence.AttackStartDelaySeconds);
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
            return clone;
        }

        internal bool MatchesCapturedWeapon(IItem item)
        {
            if (item == null
                || this.WeaponDefinition == null
                || item.LowID != this.WeaponLowId
                || item.HighID != this.WeaponHighId
                || item.Quality != this.WeaponQuality)
            {
                return false;
            }

            foreach (CapturedEnemyWeaponStatDefinition stat in this.WeaponDefinition.Stats)
            {
                int expected = unchecked((int)stat.Value);
                switch (stat.Stat)
                {
                    case CharacterStat.Flags:
                        if (item.Flags != expected) return false;
                        break;
                    case CharacterStat.StaticInstance:
                    case CharacterStat.ACGItemTemplateID:
                        if (item.LowID != expected) return false;
                        break;
                    case CharacterStat.ACGItemLevel:
                        if (item.Quality != expected) return false;
                        break;
                    case CharacterStat.ACGItemTemplateID2:
                        if (item.HighID != expected) return false;
                        break;
                    case CharacterStat.MultipleCount:
                        if (item.MultipleCount != expected) return false;
                        break;
                    case CharacterStat.Energy:
                        if (item.GetAttribute((int)StatIds.energy) != expected) return false;
                        break;
                    case CharacterStat.AttackDelay:
                        if (!this.UsesProductionActorValuesForPresentationWeapon
                            && item.GetAttribute((int)StatIds.itemdelay) != expected)
                        {
                            return false;
                        }
                        break;
                    case CharacterStat.RechargeDelay:
                        if (!this.UsesProductionActorValuesForPresentationWeapon
                            && item.GetAttribute((int)StatIds.rechargedelay) != expected)
                        {
                            return false;
                        }
                        break;
                    default:
                        return false;
                }
            }

            return true;
        }

        internal bool RequiresPhysicalWeaponPresentation
        {
            get { return this.RequiresPhysicalWeaponDefinition(); }
        }

        private bool AttackInfoAmmoMatchesCapturedEnergy()
        {
            if (this.WeaponDefinition == null)
            {
                return false;
            }

            int energy = this.WeaponDefinition.InitialEnergy;
            return energy == -1
                       ? this.AttackInfoAmmoCount == -1
                       : energy == 0
                             ? this.AttackInfoAmmoCount == 0
                             : energy > 0 && this.AttackInfoAmmoCount == energy - 1;
        }

        private bool HasCompleteCapturedFixedRuntimeObservations()
        {
            return this.HasCapturedFixedAttackBehavior
                   && this.SendCapturedAttackInfo
                   && this.CapturedDamageObservations != null
                   && this.CapturedDamageObservations.Length > 0
                   && this.CapturedDamageObservations.All(value => value > 0)
                   && this.CapturedDamageObservations.Min() == this.MinDamage
                   && this.CapturedDamageObservations.Max() == this.MaxDamage
                   && this.CapturedAttackStartDelayObservationsSeconds != null
                   && this.CapturedAttackStartDelayObservationsSeconds.Length > 0
                   && this.CapturedAttackStartDelayObservationsSeconds.All(
                       value => !double.IsNaN(value)
                                && !double.IsInfinity(value)
                                && value >= 0.0d)
                   && this.CapturedFirstHitDelayObservationsSeconds != null
                   && this.CapturedFirstHitDelayObservationsSeconds.Length > 0
                   && this.CapturedFirstHitDelayObservationsSeconds.All(
                       value => !double.IsNaN(value)
                                && !double.IsInfinity(value)
                                && value >= 0.0d)
                   && this.CapturedAttackStartDelayObservationsSeconds.Length
                      == this.CapturedFirstHitDelayObservationsSeconds.Length
                   && this.CapturedLandedIntervalObservationsSeconds != null
                   && this.CapturedLandedIntervalObservationsSeconds.Length > 0
                   && this.CapturedLandedIntervalObservationsSeconds.All(
                       value => !double.IsNaN(value)
                                && !double.IsInfinity(value)
                                && value > 0.0d)
                   && (this.CapturedUsesEquippedWeapon
                       || this.HasExplicitCapturedAttackRange())
                   && Math.Abs(
                       this.AttackStartDelaySeconds
                       - this.CapturedAttackStartDelayObservationsSeconds[0]) < 0.000001d
                   && Math.Abs(
                       this.FirstHitDelaySeconds
                       - this.CapturedFirstHitDelayObservationsSeconds[0]) < 0.000001d
                   && Math.Abs(
                       this.RechargeSeconds
                       - this.CapturedLandedIntervalObservationsSeconds[0]) < 0.000001d;
        }

        /// <summary>
        /// Production FixedAttackOnSight: damage + attack-start without corpus WIFU observations.
        /// </summary>
        internal bool IsAuthoredFixedAttackFallback()
        {
            return this.AttackModel == CapturedEnemyAttackModel.FixedAttackInfo
                   && this.Retaliates
                   && this.MinDamage > 0
                   && this.MaxDamage >= this.MinDamage
                   && this.RechargeSeconds > 0
                   && this.HasCapturedAttackStartContext
                   && this.HasCapturedSpecialAttackWeaponContext
                   && this.EvidenceSourceIdentity <= 0
                   && !this.HasCapturedRequiredPacketFields;
        }

        private bool HasExplicitCapturedAttackRange()
        {
            return this.CapturedAttackRange.HasValue
                   && this.CapturedAttackRange.Value > 0.0d
                   && !double.IsNaN(this.CapturedAttackRange.Value)
                   && !double.IsInfinity(this.CapturedAttackRange.Value);
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

        private bool HasCompleteSpecialAttackSequence()
        {
            if (this.SpecialAttackSequence == null || !this.SpecialAttackSequence.IsValid)
            {
                return false;
            }

            return this.AttackHasCompleteSource(
                       this.SpecialAttackSequence.OpeningAttack,
                       this.SpecialAttackSequence.SpecialAttacks)
                   && this.AttackHasCompleteSource(
                       this.SpecialAttackSequence.RepeatingAttack,
                       this.SpecialAttackSequence.SpecialAttacks);
        }

        private bool FixedAttackHasCompleteSource()
        {
            if (this.AttackInfoWeaponSlot == (int)WeaponSlots.Righthand
                && this.AttackInfoWeaponInstance == 0)
            {
                return this.WeaponDefinition != null
                       && this.WeaponDefinition.IsValid
                       && this.WeaponDefinition.InventorySlot == this.AttackInfoWeaponSlot;
            }

            if (this.AttackInfoWeaponInstance == 0)
            {
                return this.AttackInfoWeaponSlot == 0;
            }

            return this.CapturedSpecialAttacks != null
                   && this.CapturedSpecialAttacks.Any(
                       value => value != null && value.Tag == this.AttackInfoWeaponInstance);
        }

        private bool HasCompleteParallelAttackSequence()
        {
            if (this.ParallelAttackSequence == null || !this.ParallelAttackSequence.IsValid)
            {
                return false;
            }

            return this.ParallelAttackSequence.Streams.All(
                stream => this.AttackHasCompleteSource(
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

            if (attack.AttackInfoWeaponSlot == (int)WeaponSlots.Righthand
                && attack.AttackInfoWeaponInstance == 0)
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

        private bool RequiresPhysicalWeaponDefinition()
        {
            if (this.AttackModel == CapturedEnemyAttackModel.EquippedWeapon)
            {
                return true;
            }

            if (this.AttackModel == CapturedEnemyAttackModel.FixedAttackInfo)
            {
                return this.AttackInfoWeaponSlot == (int)WeaponSlots.Righthand
                       && this.AttackInfoWeaponInstance == 0;
            }

            if (this.SpecialAttackSequence != null)
            {
                return (this.SpecialAttackSequence.OpeningAttack != null
                        && this.SpecialAttackSequence.OpeningAttack.AttackInfoWeaponSlot
                        == (int)WeaponSlots.Righthand
                        && this.SpecialAttackSequence.OpeningAttack.AttackInfoWeaponInstance == 0)
                       || (this.SpecialAttackSequence.RepeatingAttack != null
                           && this.SpecialAttackSequence.RepeatingAttack.AttackInfoWeaponSlot
                           == (int)WeaponSlots.Righthand
                           && this.SpecialAttackSequence.RepeatingAttack.AttackInfoWeaponInstance == 0);
            }

            return this.ParallelAttackSequence != null
                   && this.ParallelAttackSequence.Streams.Any(
                       stream => stream.Attack.AttackInfoWeaponSlot == (int)WeaponSlots.Righthand
                                 && stream.Attack.AttackInfoWeaponInstance == 0);
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
                Evidence = evidence,
                Retaliates = true,
                AiProfile = NpcAiProfile.Passive,
                AttackModel = CapturedEnemyAttackModel.FixedAttackInfo,
                MinDamage = minDamage,
                MaxDamage = maxDamage,
                RechargeSeconds = rechargeSeconds,
                AttackInfoAmmoCount = attackInfoAmmoCount,
                AttackInfoWeaponSlot = weaponSlot,
                AttackInfoUnknown = attackInfoUnknown,
                AttackInfoWeaponInstance = weaponInstance,
                AttackInfoHitType = attackInfoHitType,
                AttackInfoN3Unknown = attackInfoN3Unknown
            };
        }

        /// <summary>
        /// Fixed damage + attack-on-sight (mission interiors).
        /// Enables AttackMessage start context so the client plays a real melee swing,
        /// and uses unarmed AttackInfo tags (not zeros) so hits are not "UNKNOWN damage".
        /// </summary>
        internal static CapturedEnemyCombatContract FixedAttackOnSight(
            string evidence,
            int minDamage,
            int maxDamage,
            double rechargeSeconds,
            int weaponSlot,
            int attackInfoUnknown,
            int weaponInstance,
            int attackInfoAmmoCount,
            int attackInfoHitType,
            byte attackInfoN3Unknown,
            byte specialAttackWeaponN3Unknown,
            byte attackN3Unknown,
            byte attackAction)
        {
            return new CapturedEnemyCombatContract
            {
                Evidence = evidence,
                Retaliates = true,
                AiProfile = NpcAiProfile.Aggressive,
                AttackModel = CapturedEnemyAttackModel.FixedAttackInfo,
                MinDamage = minDamage,
                MaxDamage = maxDamage,
                RechargeSeconds = rechargeSeconds,
                AttackInfoWeaponSlot = weaponSlot,
                AttackInfoUnknown = attackInfoUnknown,
                AttackInfoWeaponInstance = weaponInstance,
                AttackInfoAmmoCount = attackInfoAmmoCount,
                AttackInfoHitType = attackInfoHitType,
                AttackInfoN3Unknown = attackInfoN3Unknown,
                SpecialAttackWeaponN3Unknown = specialAttackWeaponN3Unknown,
                AttackN3Unknown = attackN3Unknown,
                AttackAction = attackAction,
                HasCapturedAttackStartContext = true,
                HasEmptySpecialAttackWeaponContext = true,
                HasCapturedSpecialAttackWeaponContext = true,
                SendCapturedAttackInfo = true,
                CapturedSpecialAttacks = new CapturedEnemySpecialAttackDefinition[0]
            };
        }

        internal static CapturedEnemyCombatContract CapturedFixedPacketSequence(
            string evidence,
            int evidenceSourceIdentity,
            NpcAiProfile aiProfile,
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
                RequiresDamageLineOfSight = requiresDamageLineOfSight
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
                Evidence = evidence,
                Retaliates = true,
                AiProfile = NpcAiProfile.Passive,
                AttackModel = CapturedEnemyAttackModel.EquippedWeapon,
                WeaponLowId = lowId,
                WeaponHighId = highId,
                WeaponQuality = quality,
                WeaponInventorySlot = inventorySlot,
                RequiresDamageLineOfSight = requiresDamageLineOfSight
            };
        }

        internal static CapturedEnemyCombatContract EquippedWeaponWithCapturedAttackInfo(
            string evidence,
            int lowId,
            int highId,
            int quality,
            int inventorySlot,
            int attackInfoAmmoCount,
            int attackInfoWeaponSlot,
            int attackInfoUnknown,
            int attackInfoWeaponInstance,
            int attackInfoHitType,
            byte attackInfoN3Unknown,
            bool requiresDamageLineOfSight = false)
        {
            CapturedEnemyCombatContract contract = EquippedWeapon(
                evidence,
                lowId,
                highId,
                quality,
                inventorySlot);
            contract.HasCapturedEquippedAttackInfo = true;
            contract.AttackInfoAmmoCount = attackInfoAmmoCount;
            contract.AttackInfoWeaponSlot = attackInfoWeaponSlot;
            contract.AttackInfoUnknown = attackInfoUnknown;
            contract.AttackInfoWeaponInstance = attackInfoWeaponInstance;
            contract.AttackInfoHitType = attackInfoHitType;
            contract.AttackInfoN3Unknown = attackInfoN3Unknown;
            contract.RequiresDamageLineOfSight = requiresDamageLineOfSight;
            return contract;
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
            NpcAiProfile aiProfile = NpcAiProfile.Passive)
        {
            CapturedEnemyCombatContract contract = EquippedWeapon(
                evidence,
                lowId,
                highId,
                quality,
                inventorySlot);
            contract.EvidenceSourceIdentity = evidenceSourceIdentity;
            contract.HasCapturedRequiredPacketFields = true;
            contract.UsesEquippedWeaponDamage = usesEquippedWeaponDamage;
            contract.UsesEquippedWeaponTiming = usesEquippedWeaponTiming;
            contract.AiProfile = aiProfile;
            contract.MinDamage = minDamage;
            contract.MaxDamage = maxDamage;
            contract.CapturedDamageBonus = damageBonus;
            contract.CapturedAttackRange = attackRange;
            contract.HasEmptySpecialAttackWeaponContext = true;
            contract.HasCapturedSpecialAttackWeaponContext = true;
            contract.CapturedSpecialAttacks = new CapturedEnemySpecialAttackDefinition[0];
            contract.HasCapturedAttackStartContext = true;
            contract.HasCapturedEquippedAttackInfo = true;
            contract.HasCapturedCombatStopSequence = hasCapturedCombatStopSequence;
            contract.AttackInfoAmmoCount = attackInfoAmmoCount;
            contract.AttackInfoWeaponSlot = inventorySlot;
            contract.AttackInfoUnknown = attackInfoUnknown;
            contract.AttackInfoWeaponInstance = 0;
            contract.AttackInfoHitType = attackInfoHitType;
            contract.AttackInfoN3Unknown = attackInfoN3Unknown;
            contract.SpecialAttackWeaponN3Unknown = specialAttackWeaponN3Unknown;
            contract.AttackN3Unknown = attackN3Unknown;
            contract.AttackAction = attackAction;
            contract.AttackStartDelaySeconds = attackStartDelaySeconds;
            contract.MovementTransitionDelaySeconds = movementTransitionDelaySeconds;
            contract.FirstHitDelaySeconds = firstHitDelaySeconds;
            contract.RechargeSeconds = rechargeSeconds;
            contract.SendStopFightOnDeath = sendStopFightOnDeath;
            contract.SpecialAttackWeaponUnknown1 = specialAttackWeaponUnknown1;
            contract.SpecialAttackWeaponUnknown2 = specialAttackWeaponUnknown2;
            contract.SpecialAttackWeaponUnknown3 = specialAttackWeaponUnknown3;
            contract.SpecialAttackWeaponUnknown4 = specialAttackWeaponUnknown4;
            contract.SpecialAttackWeaponUnknown5 = specialAttackWeaponUnknown5;
            contract.RequiresDamageLineOfSight = requiresDamageLineOfSight;
            return contract;
        }

        internal static CapturedEnemyCombatContract EquippedWeaponWithEmptySpecialAttackContext(
            string evidence,
            int lowId,
            int highId,
            int quality,
            int inventorySlot,
            int minDamage,
            int maxDamage,
            double attackStartDelaySeconds,
            double movementTransitionDelaySeconds,
            double firstHitDelaySeconds,
            double rechargeSeconds,
            bool sendStopFightOnDeath,
            int attackInfoAmmoCount,
            int attackInfoUnknown,
            int unknown1,
            int unknown2,
            int unknown3,
            int unknown4,
            int unknown5,
            int attackInfoHitType,
            byte attackInfoN3Unknown,
            byte specialAttackWeaponN3Unknown,
            byte attackN3Unknown,
            byte attackAction,
            bool requiresDamageLineOfSight = false)
        {
            CapturedEnemyCombatContract contract = EquippedWeapon(
                evidence,
                lowId,
                highId,
                quality,
                inventorySlot);
            contract.HasEmptySpecialAttackWeaponContext = true;
            contract.HasCapturedSpecialAttackWeaponContext = true;
            contract.CapturedSpecialAttacks = new CapturedEnemySpecialAttackDefinition[0];
            contract.HasCapturedAttackStartContext = true;
            contract.HasCapturedEquippedAttackInfo = true;
            contract.HasCapturedCombatStopSequence = true;
            contract.AttackInfoAmmoCount = attackInfoAmmoCount;
            contract.AttackInfoWeaponSlot = inventorySlot;
            contract.AttackInfoUnknown = attackInfoUnknown;
            contract.AttackInfoWeaponInstance = 0;
            contract.AttackInfoHitType = attackInfoHitType;
            contract.AttackInfoN3Unknown = attackInfoN3Unknown;
            contract.SpecialAttackWeaponN3Unknown = specialAttackWeaponN3Unknown;
            contract.AttackN3Unknown = attackN3Unknown;
            contract.AttackAction = attackAction;
            contract.MinDamage = minDamage;
            contract.MaxDamage = maxDamage;
            contract.AttackStartDelaySeconds = attackStartDelaySeconds;
            contract.MovementTransitionDelaySeconds = movementTransitionDelaySeconds;
            contract.FirstHitDelaySeconds = firstHitDelaySeconds;
            contract.RechargeSeconds = rechargeSeconds;
            contract.SendStopFightOnDeath = sendStopFightOnDeath;
            contract.SpecialAttackWeaponUnknown1 = unknown1;
            contract.SpecialAttackWeaponUnknown2 = unknown2;
            contract.SpecialAttackWeaponUnknown3 = unknown3;
            contract.SpecialAttackWeaponUnknown4 = unknown4;
            contract.SpecialAttackWeaponUnknown5 = unknown5;
            contract.RequiresDamageLineOfSight = requiresDamageLineOfSight;
            return contract;
        }

        internal static CapturedEnemyCombatContract CapturedSpecialSequence(
            string evidence,
            CapturedEnemySpecialAttackSequenceDefinition specialAttackSequence)
        {
            return new CapturedEnemyCombatContract
            {
                Evidence = evidence,
                Retaliates = true,
                AiProfile = NpcAiProfile.Passive,
                AttackModel = CapturedEnemyAttackModel.Specialized,
                SpecialAttackSequence = specialAttackSequence,
                HasEmptySpecialAttackWeaponContext =
                    specialAttackSequence.SpecialAttacks.Length == 0,
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
        }

        internal static CapturedEnemyCombatContract CapturedParallelAttackSequence(
            string evidence,
            CapturedEnemyParallelAttackSequenceDefinition parallelAttackSequence,
            bool requiresDamageLineOfSight = false,
            NpcAiProfile aiProfile = NpcAiProfile.Passive)
        {
            double? capturedAttackRange = SharedParallelAttackRange(parallelAttackSequence);
            return new CapturedEnemyCombatContract
            {
                Evidence = evidence,
                Retaliates = true,
                AiProfile = aiProfile,
                AttackModel = CapturedEnemyAttackModel.Specialized,
                ParallelAttackSequence = parallelAttackSequence,
                RequiresDamageLineOfSight = requiresDamageLineOfSight,
                HasEmptySpecialAttackWeaponContext =
                    parallelAttackSequence.SpecialAttacks.Length == 0,
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
                AttackAction = parallelAttackSequence.AttackAction,
                CapturedAttackRange = capturedAttackRange
            };
        }

        private static double? SharedParallelAttackRange(
            CapturedEnemyParallelAttackSequenceDefinition parallelAttackSequence)
        {
            if (parallelAttackSequence == null || parallelAttackSequence.Streams == null)
            {
                return null;
            }

            double[] ranges = parallelAttackSequence.Streams
                .Where(stream => stream != null && stream.Attack != null)
                .Select(stream => stream.Attack.Range)
                .ToArray();
            if (ranges.Length == 0
                || ranges.Any(value => value <= 0.0d || double.IsNaN(value) || double.IsInfinity(value))
                || ranges.Any(value => Math.Abs(value - ranges[0]) > 0.000001d))
            {
                return null;
            }

            return ranges[0];
        }

        internal static CapturedEnemyCombatContract CapturedProfileSelector(
            string evidence,
            int evidenceSourceIdentityHint,
            string profileSelectorHint,
            NpcAiProfile aiProfile,
            double? capturedAttackRange,
            int? specialAttackWeaponUnknown5,
            double? attackStartDelaySeconds,
            bool requiresDamageLineOfSight = false)
        {
            return new CapturedEnemyCombatContract
            {
                Evidence = evidence ?? string.Empty,
                Retaliates = true,
                AiProfile = aiProfile,
                AttackModel = CapturedEnemyAttackModel.Unresolved,
                EvidenceSourceIdentityHint = evidenceSourceIdentityHint,
                EvidenceProfileSelectorHint = profileSelectorHint ?? string.Empty,
                EvidenceSpecialAttackWeaponUnknown5Hint = specialAttackWeaponUnknown5,
                EvidenceAttackStartDelaySecondsHint = attackStartDelaySeconds,
                CapturedAttackRange = capturedAttackRange,
                RequiresDamageLineOfSight = requiresDamageLineOfSight
            };
        }

        internal static CapturedEnemyCombatContract Unresolved(string evidence, bool retaliationObserved)
        {
            return new CapturedEnemyCombatContract
            {
                Evidence = evidence,
                Retaliates = retaliationObserved,
                AiProfile = NpcAiProfile.Passive,
                AttackModel = CapturedEnemyAttackModel.Unresolved
            };
        }
    }

    internal static class CapturedEnemyCombatRuntimeRegistry
    {
        private static readonly object Sync = new object();

        private static readonly Dictionary<int, CapturedEnemyCombatContract> Contracts =
            new Dictionary<int, CapturedEnemyCombatContract>();

        private static readonly Dictionary<int, int> CapturedWeaponEnergy =
            new Dictionary<int, int>();

        private static readonly Dictionary<int, IItem> CapturedWeaponItems =
            new Dictionary<int, IItem>();

        internal static void Register(
            int serverInstance,
            CapturedEnemyCombatContract contract,
            IItem capturedWeapon = null)
        {
            lock (Sync)
            {
                Contracts[serverInstance] = contract;
                if (contract != null
                    && contract.WeaponDefinition != null
                    && contract.WeaponDefinition.IsValid)
                {
                    CapturedWeaponEnergy[serverInstance] = contract.WeaponDefinition.InitialEnergy;
                    if (capturedWeapon != null)
                    {
                        CapturedWeaponItems[serverInstance] = capturedWeapon;
                    }
                    else
                    {
                        CapturedWeaponItems.Remove(serverInstance);
                    }
                }
                else
                {
                    CapturedWeaponEnergy.Remove(serverInstance);
                    CapturedWeaponItems.Remove(serverInstance);
                }
            }
        }

        internal static bool TryGet(int serverInstance, out CapturedEnemyCombatContract contract)
        {
            lock (Sync)
            {
                return Contracts.TryGetValue(serverInstance, out contract);
            }
        }

        internal static void Remove(int serverInstance)
        {
            lock (Sync)
            {
                Contracts.Remove(serverInstance);
                CapturedWeaponEnergy.Remove(serverInstance);
                CapturedWeaponItems.Remove(serverInstance);
            }
        }

        internal static void QuarantineRuntime(ICharacter character, string reason)
        {
            if (character == null)
            {
                return;
            }

            CapturedEnemyCombatContract current;
            string evidence = TryGet(character.Identity.Instance, out current) && current != null
                                  ? current.Evidence
                                  : string.Empty;
            var controller = character.Controller as NPCController;
            if (controller != null)
            {
                controller.AiProfile = NpcAiProfile.Passive;
            }

            Register(
                character.Identity.Instance,
                CapturedEnemyCombatContract.Unresolved(
                    evidence + "; runtime quarantine=" + reason,
                    true));
            LogUtil.Debug(
                DebugInfoDetail.Error,
                "CapturedEnemyCombatRuntimeQuarantined actor=" + character.Identity
                + " reason=" + reason);
        }

        internal static bool TryConsumeCapturedWeaponAmmo(int serverInstance, out int ammoCount)
        {
            lock (Sync)
            {
                int energy;
                if (!CapturedWeaponEnergy.TryGetValue(serverInstance, out energy))
                {
                    ammoCount = 0;
                    return false;
                }

                if (energy == -1)
                {
                    ammoCount = -1;
                    return true;
                }

                if (energy == 0)
                {
                    CapturedEnemyCombatContract contract;
                    if (Contracts.TryGetValue(serverInstance, out contract)
                        && contract != null
                        && contract.WeaponDefinition != null
                        && contract.WeaponDefinition.InitialEnergy == 0)
                    {
                        ammoCount = 0;
                        return true;
                    }

                    ammoCount = 0;
                    return false;
                }

                if (energy < 0)
                {
                    ammoCount = 0;
                    return false;
                }

                energy--;
                CapturedWeaponEnergy[serverInstance] = energy;
                ammoCount = energy;
                return true;
            }
        }

        internal static bool TryGetCapturedWeaponEnergy(int serverInstance, out int energy)
        {
            lock (Sync)
            {
                return CapturedWeaponEnergy.TryGetValue(serverInstance, out energy);
            }
        }

        internal static bool TryGetCapturedWeaponItem(int serverInstance, out IItem item)
        {
            lock (Sync)
            {
                return CapturedWeaponItems.TryGetValue(serverInstance, out item);
            }
        }
    }

    internal static class EnemyItemQualityPolicy
    {
        internal static bool TryResolve(
            int actorLevel,
            int lowTemplateId,
            int highTemplateId,
            out int quality)
        {
            quality = 0;
            if (actorLevel < 1
                || lowTemplateId < 1
                || highTemplateId < 1
                || !ItemLoader.ItemList.ContainsKey(lowTemplateId)
                || !ItemLoader.ItemList.ContainsKey(highTemplateId))
            {
                return false;
            }

            int lowQuality = ItemLoader.ItemList[lowTemplateId].Quality;
            int highQuality = ItemLoader.ItemList[highTemplateId].Quality;
            if (lowQuality < 1 || highQuality < 1)
            {
                return false;
            }

            int minimumQuality = Math.Min(lowQuality, highQuality);
            int maximumQuality = Math.Max(lowQuality, highQuality);
            quality = Math.Max(minimumQuality, Math.Min(maximumQuality, actorLevel));
            return true;
        }
    }

    internal static class CapturedEnemyCombatRuntime
    {
        private const int MissingItemStatValue = 1234567890;

        internal static bool Prepare(
            Character character,
            NPCController controller,
            CapturedEnemyCombatContract contract,
            out string failure)
        {
            failure = string.Empty;
            if (character == null || controller == null || contract == null)
            {
                failure = "character, controller, or combat contract is null";
                return false;
            }

            if (contract.Retaliates)
            {
                bool hasDirectCaptureCertification = contract.IsCombatReady;
                CapturedEnemyCombatContract resolved;
                string resolutionFailure;
                if (CapturedEnemyCombatProfileCatalog.TryResolve(
                    character,
                    contract,
                    out resolved,
                    out resolutionFailure))
                {
                    contract = resolved;
                }
                else if (!hasDirectCaptureCertification
                         || (!contract.IsAuthoredFixedAttackFallback()
                             && (string.IsNullOrWhiteSpace(resolutionFailure)
                                 || !resolutionFailure.StartsWith(
                                     "no canonical raw combat profile for ",
                                     StringComparison.Ordinal))))
                {
                    // Authored FixedAttackOnSight remains a deliberate non-corpus policy. A
                    // capture-backed contract may only survive a missing catalog entry; selector,
                    // source, compatibility, and safety failures must stay quarantined.
                    contract = CapturedEnemyCombatContract.Unresolved(
                        contract.Evidence + "; corpus resolution="
                        + (string.IsNullOrWhiteSpace(resolutionFailure)
                               ? "selected retaliatory contract was not capture-certified"
                               : resolutionFailure),
                        true);
                }
            }

            if (!contract.IsCombatReady)
            {
                controller.AiProfile = NpcAiProfile.Passive;
                CapturedEnemyCombatRuntimeRegistry.Register(character.Identity.Instance, contract);
                failure = "captured combat quarantined: " + contract.QuarantineReason
                          + "; evidence=" + contract.Evidence;
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "CapturedEnemyCombatQuarantined actor=" + character.Identity
                    + " reason=" + failure);
                return true;
            }

            controller.AiProfile = contract.AiProfile;
            IItem capturedWeapon = null;
            if (contract.WeaponDefinition != null
                && !TryEquipCapturedWeapon(character, contract, out capturedWeapon, out failure))
            {
                controller.AiProfile = NpcAiProfile.Passive;
                CapturedEnemyCombatRuntimeRegistry.Register(
                    character.Identity.Instance,
                    CapturedEnemyCombatContract.Unresolved(
                        contract.Evidence + "; runtime failure=" + failure,
                        contract.Retaliates));
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "CapturedEnemyCombatQuarantined actor=" + character.Identity
                    + " reason=" + failure);
                return true;
            }

            CapturedEnemyCombatRuntimeRegistry.Register(
                character.Identity.Instance,
                contract,
                capturedWeapon);
            return true;
        }

        internal static bool PrepareAndRequireCombatReady(
            Character character,
            NPCController controller,
            CapturedEnemyCombatContract contract,
            out string failure)
        {
            if (!Prepare(character, controller, contract, out failure))
            {
                if (controller != null)
                {
                    controller.AiProfile = NpcAiProfile.Passive;
                }

                return false;
            }

            CapturedEnemyCombatContract prepared;
            if (character == null
                || !CapturedEnemyCombatRuntimeRegistry.TryGet(
                    character.Identity.Instance,
                    out prepared)
                || prepared == null
                || !prepared.IsCombatReady)
            {
                if (controller != null)
                {
                    controller.AiProfile = NpcAiProfile.Passive;
                }

                if (string.IsNullOrWhiteSpace(failure))
                {
                    failure = "captured combat preparation did not produce a runtime-ready contract";
                }

                return false;
            }

            return true;
        }

        internal static bool TryValidateLiveCapturedWeapon(
            ICharacter character,
            CapturedEnemyCombatContract contract,
            out IItem item,
            out string failure)
        {
            item = null;
            failure = string.Empty;
            if (character == null || contract == null || !contract.RequiresPhysicalWeaponPresentation)
            {
                failure = "character or physical captured contract is unavailable";
                return false;
            }

            IInventoryPage weaponPage;
            if (character.BaseInventory == null
                || !character.BaseInventory.Pages.TryGetValue(
                    (int)IdentityType.WeaponPage,
                    out weaponPage)
                || !weaponPage.ValidSlot(contract.WeaponInventorySlot))
            {
                failure = "required captured weapon page or slot is unavailable";
                return false;
            }

            item = weaponPage[contract.WeaponInventorySlot];
            IItem registeredItem;
            if (item == null
                || !CapturedEnemyCombatRuntimeRegistry.TryGetCapturedWeaponItem(
                    character.Identity.Instance,
                    out registeredItem)
                || !ReferenceEquals(item, registeredItem))
            {
                failure = "required captured weapon object is missing or was replaced";
                return false;
            }

            if (!contract.MatchesCapturedWeapon(item))
            {
                failure = "required captured weapon fields no longer match WIFU evidence";
                return false;
            }

            int currentEnergy;
            if (!CapturedEnemyCombatRuntimeRegistry.TryGetCapturedWeaponEnergy(
                    character.Identity.Instance,
                    out currentEnergy))
            {
                failure = "captured weapon Energy state is unavailable";
                return false;
            }

            if (currentEnergy < -1
                || (currentEnergy == 0 && contract.WeaponDefinition.InitialEnergy > 0))
            {
                failure = "captured weapon Energy is exhausted";
                return false;
            }

            return true;
        }

        private static bool TryEquipCapturedWeapon(
            Character character,
            CapturedEnemyCombatContract contract,
            out IItem capturedWeapon,
            out string failure)
        {
            capturedWeapon = null;
            failure = string.Empty;
            if (!ItemLoader.ItemList.ContainsKey(contract.WeaponLowId)
                || !ItemLoader.ItemList.ContainsKey(contract.WeaponHighId))
            {
                failure = string.Format(
                    "captured weapon template missing low={0} high={1}",
                    contract.WeaponLowId,
                    contract.WeaponHighId);
                return false;
            }

            IInventoryPage weaponPage;
            if (character.BaseInventory == null
                || !character.BaseInventory.Pages.TryGetValue((int)IdentityType.WeaponPage, out weaponPage))
            {
                failure = "weapon inventory page is unavailable";
                return false;
            }

            if (!weaponPage.ValidSlot(contract.WeaponInventorySlot)
                || weaponPage[contract.WeaponInventorySlot] != null)
            {
                failure = "captured weapon slot is invalid or occupied: " + contract.WeaponInventorySlot;
                return false;
            }

            var weapon = new Item(
                contract.WeaponQuality,
                contract.WeaponLowId,
                contract.WeaponHighId)
            {
                MultipleCount = 1
            };
            ApplyCapturedWeaponStats(
                weapon,
                contract.WeaponDefinition,
                contract.UsesProductionActorValuesForPresentationWeapon);
            if (!contract.MatchesCapturedWeapon(weapon))
            {
                failure = "constructed weapon does not exactly match captured WIFU templates/QL/stats";
                return false;
            }

            if (contract.RequiresPhysicalWeaponPresentation
                && !contract.UsesProductionActorValuesForPresentationWeapon)
            {
                int rawRange = weapon.GetAttribute((int)StatIds.attackrange);
                if (rawRange == MissingItemStatValue || rawRange <= 0)
                {
                    failure = "captured weapon template has no valid attackrange";
                    return false;
                }
            }

            InventoryError result = weaponPage.Add(contract.WeaponInventorySlot, weapon);
            if (result != InventoryError.OK)
            {
                failure = "captured weapon add failed: " + result;
                return false;
            }

            if (contract.HasCapturedEquippedAttackInfo || contract.WeaponDefinition != null)
            {
                ApplyCapturedEquippedAttackDisplayStats(character, weapon);
            }

            capturedWeapon = weapon;
            return true;
        }

        private static void ApplyCapturedWeaponStats(
            Item weapon,
            CapturedEnemyWeaponDefinition definition,
            bool retainProductionTiming)
        {
            if (weapon == null || definition == null)
            {
                return;
            }

            foreach (CapturedEnemyWeaponStatDefinition stat in definition.Stats)
            {
                int value = unchecked((int)stat.Value);
                if (stat.Stat == CharacterStat.Flags)
                {
                    weapon.Flags = value;
                }
                else if (stat.Stat == CharacterStat.MultipleCount)
                {
                    weapon.MultipleCount = value;
                }
                else if (stat.Stat == CharacterStat.Energy)
                {
                    weapon.SetAttribute((int)StatIds.energy, value);
                }
                else if (stat.Stat == CharacterStat.AttackDelay)
                {
                    if (!retainProductionTiming)
                    {
                        weapon.SetAttribute((int)StatIds.itemdelay, value);
                    }
                }
                else if (stat.Stat == CharacterStat.RechargeDelay)
                {
                    if (!retainProductionTiming)
                    {
                        weapon.SetAttribute((int)StatIds.rechargedelay, value);
                    }
                }
            }
        }

        private static void ApplyCapturedEquippedAttackDisplayStats(ICharacter character, IItem weapon)
        {
            ApplyWeaponStatIfPresent(character, weapon, StatIds.defaultattacktype);
            ApplyWeaponStatIfPresent(character, weapon, StatIds.damagetype);
            ApplyWeaponStatIfPresent(character, weapon, StatIds.weapontype);
        }

        private static void ApplyWeaponStatIfPresent(ICharacter character, IItem weapon, StatIds stat)
        {
            int value = weapon.GetAttribute((int)stat);
            if (value == MissingItemStatValue)
            {
                return;
            }

            SetMobStat(character, stat, value);
        }

        private static void SetMobStat(ICharacter character, StatIds stat, int value)
        {
            character.Stats.SetBaseValueWithoutTriggering((int)stat, (uint)Math.Max(0, value));
        }
    }

    internal static class CapturedSubwayCombatCatalog
    {
        private const int MissingItemStatValue = 1234567890;

        private const int DerangedShopperMonsterData = 203736;

        private const int DerangedShopperSourceInstance = unchecked((int)0x79574527);

        private const int IncompleteRebuildMonsterData = 203728;

        private const int FragmentedSoulMonsterData = 203729;

        private const int LooterMonsterData = 203745;

        private const int MuggerMonsterData = 203734;

        private const int RedundantScanMonsterData = 204178;

        private const int WorkmanStrikerMonsterData = 203854;

        private static readonly int[] MuggerSourceInstances =
        {
            unchecked((int)0x7953AA11),
            unchecked((int)0x7953AD6B),
            unchecked((int)0x795450D4),
            unchecked((int)0x795451FE),
            unchecked((int)0x79557F14),
            unchecked((int)0x7957E5C6),
            unchecked((int)0x7957E5C7),
            unchecked((int)0x7957E5C8),
            unchecked((int)0x7957E5CA)
        };

        private static readonly int[] IncompleteRebuildSourceInstances =
        {
            unchecked((int)0x79545170),
            unchecked((int)0x79545172),
            unchecked((int)0x79545177),
            unchecked((int)0x79545181),
            unchecked((int)0x79545188),
            unchecked((int)0x795451BC),
            unchecked((int)0x795451C1),
            unchecked((int)0x795451CB),
            unchecked((int)0x795451FD),
            unchecked((int)0x79545241)
        };

        private static readonly int[] RedundantScanSourceInstances =
        {
            unchecked((int)0x7953AF85),
            unchecked((int)0x795451BF),
            unchecked((int)0x795451C4),
            unchecked((int)0x795451D3)
        };

        internal static CapturedEnemyCombatContract For(string name, int monsterData)
        {
            return For(name, monsterData, null);
        }

        internal static CapturedEnemyCombatContract For(string name, int monsterData, int? level)
        {
            switch (monsterData)
            {
                case 203726:
                    return CapturedEnemyCombatContract.EquippedWeapon(
                        "Eumenides QL20 captured weapon profile selector; exact packet sequence is resolved from the generated capture catalog",
                        NpcCombatAttackRules.CapturedSubwayEumenidesWeaponLowTemplate,
                        NpcCombatAttackRules.CapturedSubwayEumenidesWeaponHighTemplate,
                        NpcCombatAttackRules.CapturedSubwayEumenidesWeaponQuality,
                        (int)WeaponSlots.Righthand,
                        requiresDamageLineOfSight: true);
                case 203744:
                    return level.HasValue
                        ? StrikeForeman(level.Value)
                        : CapturedEnemyCombatContract.Unresolved(
                            "Strike Foreman requires its active runtime level",
                            true);
                case 203748:
                    return CapturedEnemyCombatContract.EquippedWeaponWithEmptySpecialAttackContext(
                        "20260712-232711/234401 and 20260720-053542: Vergil Aeneid QL23 Cast-Off E-Beamer 122123; 22-25 normal player damage with one captured 54 critical, captured attack-start/first-hit timing, and weapon-owned roll/cadence",
                        NpcCombatAttackRules.CapturedSubwayVergilWeaponTemplate,
                        NpcCombatAttackRules.CapturedSubwayVergilWeaponTemplate,
                        NpcCombatAttackRules.CapturedSubwayVergilWeaponQuality,
                        (int)WeaponSlots.Righthand,
                        NpcCombatAttackRules.CapturedSubwayVergilWeaponDamageMinimumOverride,
                        NpcCombatAttackRules.CapturedSubwayVergilWeaponDamageMaximumOverride,
                        NpcCombatAttackRules.CapturedSubwayVergilAttackStartDelaySeconds,
                        NpcCombatAttackRules.CapturedSubwayVergilMovementTransitionDelaySeconds,
                        NpcCombatAttackRules.CapturedSubwayVergilFirstHitDelaySeconds,
                        NpcCombatAttackRules.CapturedSubwayVergilRechargeOverrideSeconds,
                        true,
                        NpcCombatAttackRules.CapturedSubwayVergilInitialAttackInfoAmmoCount,
                        NpcCombatAttackRules.CapturedSubwayVergilAttackInfoUnknown,
                        NpcCombatAttackRules.CapturedSubwayVergilSpecialAttackWeaponValue,
                        NpcCombatAttackRules.CapturedSubwayVergilSpecialAttackWeaponValue,
                        NpcCombatAttackRules.CapturedSubwayVergilSpecialAttackWeaponValue,
                        NpcCombatAttackRules.CapturedSubwayVergilSpecialAttackWeaponValue,
                        NpcCombatAttackRules.CapturedSubwayVergilSpecialAttackWeaponLastValue,
                        3,
                        0,
                        0,
                        0,
                        0,
                        requiresDamageLineOfSight: true);
                case 155962:
                    CapturedEnemyCombatAttackDefinition abmouthXopzAttack =
                        new CapturedEnemyCombatAttackDefinition(
                            NpcCombatAttackRules.CapturedSubwayAbmouthXopzMinimumDamage,
                            NpcCombatAttackRules.CapturedSubwayAbmouthXopzMaximumDamage,
                            0,
                            NpcCombatAttackRules.MaxMeleeCombatDistance,
                            NpcCombatAttackRules.CapturedSubwayAbmouthAttackCycleSeconds,
                            false,
                            NpcCombatAttackRules.UnarmedAttackInfoAmmoCount,
                            NpcCombatAttackRules.CapturedSubwayAbmouthXopzWeaponSlot,
                            0,
                            NpcCombatAttackRules.NormalAttackInfoHitType,
                            NpcCombatAttackRules.CapturedSubwayAbmouthXopzTag,
                            0,
                            true);
                    CapturedEnemyCombatAttackDefinition abmouthDenwAttack =
                        new CapturedEnemyCombatAttackDefinition(
                            NpcCombatAttackRules.CapturedSubwayAbmouthDenwMinimumDamage,
                            NpcCombatAttackRules.CapturedSubwayAbmouthDenwMaximumDamage,
                            0,
                            NpcCombatAttackRules.MaxMeleeCombatDistance,
                            NpcCombatAttackRules.CapturedSubwayAbmouthAttackCycleSeconds,
                            false,
                            NpcCombatAttackRules.UnarmedAttackInfoAmmoCount,
                            NpcCombatAttackRules.CapturedSubwayAbmouthDenwWeaponSlot,
                            0,
                            NpcCombatAttackRules.NormalAttackInfoHitType,
                            NpcCombatAttackRules.CapturedSubwayAbmouthDenwTag,
                            0,
                            true);
                    return CapturedEnemyCombatContract.CapturedParallelAttackSequence(
                        "20260712-224840/232137 and 20260720-053802: Abmouth XOPZ paired stream, DENW stream, captured SIW context, and one 21.8-second combat warp cast (nano 286237) that teleports the engaged player and owned pets to Abmouth",
                        new CapturedEnemyParallelAttackSequenceDefinition(
                            new[]
                            {
                                new CapturedEnemyParallelAttackStreamDefinition(
                                    NpcCombatAttackRules.CapturedSubwayAbmouthXopzFirstInitialSeconds,
                                    abmouthXopzAttack),
                                new CapturedEnemyParallelAttackStreamDefinition(
                                    NpcCombatAttackRules.CapturedSubwayAbmouthDenwInitialSeconds,
                                    abmouthDenwAttack),
                                new CapturedEnemyParallelAttackStreamDefinition(
                                    NpcCombatAttackRules.CapturedSubwayAbmouthXopzSecondInitialSeconds,
                                    abmouthXopzAttack)
                            },
                            new[]
                            {
                                new CapturedEnemySpecialAttackDefinition(
                                    NpcCombatAttackRules.CapturedSubwayAbmouthXopzLowTemplate,
                                    NpcCombatAttackRules.CapturedSubwayAbmouthXopzHighTemplate,
                                    NpcCombatAttackRules.CapturedSubwayAbmouthXopzTag,
                                    NpcCombatAttackRules.CapturedSubwayAbmouthXopzName),
                                new CapturedEnemySpecialAttackDefinition(
                                    NpcCombatAttackRules.CapturedSubwayAbmouthDenwLowTemplate,
                                    NpcCombatAttackRules.CapturedSubwayAbmouthDenwHighTemplate,
                                    NpcCombatAttackRules.CapturedSubwayAbmouthDenwTag,
                                    NpcCombatAttackRules.CapturedSubwayAbmouthDenwName)
                            },
                            NpcCombatAttackRules.CapturedSubwayAbmouthSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayAbmouthSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayAbmouthSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayAbmouthSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayAbmouthSpecialAttackWeaponLastValue,
                            0,
                            0,
                            0));
                case 31909:
                    return CapturedEnemyCombatContract.CapturedSpecialSequence(
                        "20260712-224840/232137: Abmouth-owned Infector DMXF attacks, 21-26 player damage, and 3.7-second cadence",
                        new CapturedEnemySpecialAttackSequenceDefinition(
                            NpcCombatAttackRules.CapturedSubwayAbmouthInfectorInitialAttackSeconds,
                            null,
                            new CapturedEnemyCombatAttackDefinition(
                                NpcCombatAttackRules.CapturedSubwayAbmouthInfectorMinimumDamage,
                                NpcCombatAttackRules.CapturedSubwayAbmouthInfectorMaximumDamage,
                                0,
                                NpcCombatAttackRules.MaxMeleeCombatDistance,
                                NpcCombatAttackRules.CapturedSubwayAbmouthInfectorRechargeSeconds,
                                false,
                                NpcCombatAttackRules.UnarmedAttackInfoAmmoCount,
                                NpcCombatAttackRules.CapturedSubwayAbmouthInfectorWeaponSlot,
                                0,
                                NpcCombatAttackRules.NormalAttackInfoHitType,
                                NpcCombatAttackRules.CapturedSubwayAbmouthInfectorTag,
                                0,
                                true),
                            new[]
                            {
                                new CapturedEnemySpecialAttackDefinition(
                                    NpcCombatAttackRules.CapturedSubwayAbmouthInfectorLowTemplate,
                                    NpcCombatAttackRules.CapturedSubwayAbmouthInfectorHighTemplate,
                                    NpcCombatAttackRules.CapturedSubwayAbmouthInfectorTag,
                                    NpcCombatAttackRules.CapturedSubwayAbmouthInfectorName)
                            },
                            NpcCombatAttackRules.CapturedSubwayAbmouthInfectorSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayAbmouthInfectorSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayAbmouthInfectorSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayAbmouthInfectorSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayAbmouthInfectorSpecialAttackWeaponLastValue,
                            0,
                            0,
                            0));
                case 17657:
                {
                    OrdinaryEnemyCombatNumericSetup setup;
                    if (!level.HasValue
                        || !OrdinaryEnemyCombatSetupGenerator
                            .TryGenerateFilthFlea(level.Value, out setup))
                    {
                        return CapturedEnemyCombatContract.Unresolved(
                            "Filth Flea combat requires the bounded capture-proven level domain "
                            + OrdinaryEnemyCombatSetupGenerator
                                .FilthFleaMinimumLevel
                            + ".."
                            + OrdinaryEnemyCombatSetupGenerator
                                .FilthFleaMaximumLevel,
                            true);
                    }

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
                            setup.SpecialAttackWeaponUnknown1,
                            setup.SpecialAttackWeaponUnknown2,
                            setup.SpecialAttackWeaponUnknown3,
                            setup.SpecialAttackWeaponUnknown4,
                            NpcCombatAttackRules.CapturedSubwayFilthFleaSpecialAttackWeaponLastValue,
                            0,
                            0,
                            0))
                        .WithProductionSpecializedValues();
                }
                case 17720:
                    return CapturedEnemyCombatContract.FixedAttack(
                        "20260708-143600 and 20260709-210452: 37 normal local-player Discarded Pet SIW1 hits span 9..18; four 30..33 criticals remain report-only; 30 same-source landed-hit intervals span 4.609299..5.950416 seconds with conventional median 5.089763; AttackInfo uses ammo -1, slot 0, unknown 0, and instance SIW1; raw SpecialAttackWeapon first four fields are exact by level while the varying fifth field remains unresolved and is not synthesized",
                        NpcCombatAttackRules.CapturedSubwayDiscardedPetMinimumDamage,
                        NpcCombatAttackRules.CapturedSubwayDiscardedPetMaximumDamage,
                        NpcCombatAttackRules.CapturedSubwayDiscardedPetRechargeSeconds,
                        NpcCombatAttackRules.CapturedSubwayDiscardedPetWeaponSlot,
                        0,
                        NpcCombatAttackRules.CapturedSubwayDiscardedPetWeaponTag,
                        -1,
                        0,
                        0)
                        .WithProductionSpecializedValues();
                case 17649:
                    return ForDisobedientBot(level);
                case 30379:
                    return CapturedEnemyCombatContract.CapturedParallelAttackSequence(
                        "20260709-222339 and 20260716-033326/034104: Bloodcreeper proactive dual Skinspider Bite/Spit natural attacks, 21-41 rolled damage, and independent captured hand cadence",
                        new CapturedEnemyParallelAttackSequenceDefinition(
                            new[]
                            {
                                new CapturedEnemyParallelAttackStreamDefinition(
                                    NpcCombatAttackRules.CapturedSubwayBloodcreeperSpitInitialSeconds,
                                    new CapturedEnemyCombatAttackDefinition(
                                        NpcCombatAttackRules.CapturedSubwayBloodcreeperSpitMinimumDamage,
                                        NpcCombatAttackRules.CapturedSubwayBloodcreeperSpitMaximumDamage,
                                        0,
                                        NpcCombatAttackRules.MaxMeleeCombatDistance,
                                        NpcCombatAttackRules.CapturedSubwayBloodcreeperSpitRechargeSeconds,
                                        false,
                                        NpcCombatAttackRules.UnarmedAttackInfoAmmoCount,
                                        NpcCombatAttackRules.CapturedSubwayBloodcreeperSpitWeaponSlot,
                                        0,
                                        NpcCombatAttackRules.NormalAttackInfoHitType,
                                        NpcCombatAttackRules.CapturedSubwayBloodcreeperSpitTag,
                                        0,
                                        true)),
                                new CapturedEnemyParallelAttackStreamDefinition(
                                    NpcCombatAttackRules.CapturedSubwayBloodcreeperBiteInitialSeconds,
                                    new CapturedEnemyCombatAttackDefinition(
                                        NpcCombatAttackRules.CapturedSubwayBloodcreeperBiteMinimumDamage,
                                        NpcCombatAttackRules.CapturedSubwayBloodcreeperBiteMaximumDamage,
                                        0,
                                        NpcCombatAttackRules.MaxMeleeCombatDistance,
                                        NpcCombatAttackRules.CapturedSubwayBloodcreeperBiteRechargeSeconds,
                                        false,
                                        NpcCombatAttackRules.UnarmedAttackInfoAmmoCount,
                                        NpcCombatAttackRules.CapturedSubwayBloodcreeperBiteWeaponSlot,
                                        0,
                                        NpcCombatAttackRules.NormalAttackInfoHitType,
                                        NpcCombatAttackRules.CapturedSubwayBloodcreeperBiteTag,
                                        0,
                                        true))
                            },
                            new[]
                            {
                                new CapturedEnemySpecialAttackDefinition(
                                    NpcCombatAttackRules.CapturedSubwayBloodcreeperSpitLowTemplate,
                                    NpcCombatAttackRules.CapturedSubwayBloodcreeperSpitHighTemplate,
                                    NpcCombatAttackRules.CapturedSubwayBloodcreeperSpitTag,
                                    NpcCombatAttackRules.CapturedSubwayBloodcreeperSpitName),
                                new CapturedEnemySpecialAttackDefinition(
                                    NpcCombatAttackRules.CapturedSubwayBloodcreeperBiteLowTemplate,
                                    NpcCombatAttackRules.CapturedSubwayBloodcreeperBiteHighTemplate,
                                    NpcCombatAttackRules.CapturedSubwayBloodcreeperBiteTag,
                                    NpcCombatAttackRules.CapturedSubwayBloodcreeperBiteName)
                            },
                            NpcCombatAttackRules.CapturedSubwayBloodcreeperSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayBloodcreeperSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayBloodcreeperSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayBloodcreeperSpecialAttackWeaponValue,
                            NpcCombatAttackRules.CapturedSubwayBloodcreeperSpecialAttackWeaponLastValue,
                            0,
                            0,
                            0))
                        .WithProductionSpecializedValues();
                case 203734:
                    return CapturedEnemyCombatContract.Unresolved(
                        "Mugger combat requires an exact captured source identity; aggregate weapon fallback is forbidden",
                        true);
                case 26092:
                {
                    if (level != 5)
                    {
                        return CapturedEnemyCombatContract.Unresolved(
                            "20260711-170337 proves the complete Thief packet contract only for level 5; requested level="
                            + (level.HasValue ? level.Value.ToString() : "missing"),
                            true);
                    }

                    const string thiefEvidence =
                        "20260711-170337 raw 155/156,301/302,480/564/654: level-5 Thief owner-linked WIFU, exact attack start, three projectile hits, and six-second repeat cadence; 2026-07-12 private validation proved the weapon context renders projectile damage";
                    return CapturedEnemyCombatContract.EquippedWeaponWithCapturedPacketSequence(
                            thiefEvidence,
                            unchecked((int)0x795B5DB2u),
                            121567,
                            121567,
                            1,
                            (int)WeaponSlots.Righthand,
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
                        .WithCapturedWeapon(ThiefCapturedWeaponDefinition(thiefEvidence));
                }
                case 203733:
                    return level.HasValue
                        ? ViolentVagabond(level.Value)
                        : CapturedEnemyCombatContract.Unresolved(
                            "Violent Vagabond requires its active runtime level",
                            true);
                default:
                    return CapturedEnemyCombatContract.Unresolved(
                        "No captured combat contract for " + name + " monsterData=" + monsterData,
                        false);
            }
        }

        private static CapturedEnemyCombatContract StrikeForeman(int level)
        {
            const int evidenceSourceIdentity = unchecked((int)0x7954512Eu);
            const string archetypeId =
                "subway-strike-foreman-122767-equipped-level-bounded-v1";
            const string evidence =
                "20260709-212336/220439 exact 122767/122768 equipped WIFU family; "
                + "20260720-032106/033513 exact empty SAW 154/154/154/117/0, "
                + "Attack action 0, normal hit wire 3, damage wire 0, slot 6, "
                + "instance 0, mutable ammunition, and WIFU -> SAW -> Attack -> "
                + "AttackInfo ordering; actor level owns bounded item QL and the "
                + "equipped item owns runtime damage, range, and cadence";

            int quality;
            if (level != 19
                || !EnemyItemQualityPolicy.TryResolve(
                    level,
                    NpcCombatAttackRules.CapturedSubwayStrikeForemanWeaponLowTemplate,
                    NpcCombatAttackRules.CapturedSubwayStrikeForemanWeaponHighTemplate,
                    out quality))
            {
                return CapturedEnemyCombatContract.Unresolved(
                    "Strike Foreman combat is bounded to the active L19 "
                    + "122767/122768 item-quality domain",
                    true);
            }

            var productionWeapon = new Item(
                quality,
                NpcCombatAttackRules.CapturedSubwayStrikeForemanWeaponLowTemplate,
                NpcCombatAttackRules.CapturedSubwayStrikeForemanWeaponHighTemplate)
            {
                MultipleCount = 1
            };
            int initialEnergy =
                productionWeapon.GetAttribute((int)CharacterStat.MaxEnergy);
            if (initialEnergy == MissingItemStatValue || initialEnergy <= 0)
            {
                return CapturedEnemyCombatContract.Unresolved(
                    "Strike Foreman production weapon ammunition capacity is unavailable",
                    true);
            }

            CapturedEnemyWeaponDefinition weaponDefinition =
                new CapturedEnemyWeaponDefinition(
                    evidence,
                    evidenceSourceIdentity,
                    0,
                    11,
                    NpcCombatAttackRules.CapturedSubwayStrikeForemanWeaponSlot,
                    1000015,
                    0,
                    262,
                    new[]
                    {
                        CapturedWeaponStat(CharacterStat.Flags, 1027),
                        CapturedWeaponStat(
                            CharacterStat.StaticInstance,
                            NpcCombatAttackRules
                                .CapturedSubwayStrikeForemanWeaponLowTemplate),
                        CapturedWeaponStat(CharacterStat.ACGItemLevel, quality),
                        CapturedWeaponStat(
                            CharacterStat.ACGItemTemplateID,
                            NpcCombatAttackRules
                                .CapturedSubwayStrikeForemanWeaponLowTemplate),
                        CapturedWeaponStat(
                            CharacterStat.ACGItemTemplateID2,
                            NpcCombatAttackRules
                                .CapturedSubwayStrikeForemanWeaponHighTemplate),
                        CapturedWeaponStat(CharacterStat.MultipleCount, 1),
                        CapturedWeaponStat(CharacterStat.Energy, initialEnergy),
                        CapturedWeaponStat(CharacterStat.AttackDelay, 235),
                        CapturedWeaponStat(CharacterStat.RechargeDelay, 235)
                    },
                    0);

            return CapturedEnemyCombatContract
                .EquippedWeaponWithCapturedPacketSequence(
                    evidence,
                    evidenceSourceIdentity,
                    NpcCombatAttackRules.CapturedSubwayStrikeForemanWeaponLowTemplate,
                    NpcCombatAttackRules.CapturedSubwayStrikeForemanWeaponHighTemplate,
                    quality,
                    NpcCombatAttackRules.CapturedSubwayStrikeForemanWeaponSlot,
                    true,
                    0,
                    0,
                    0,
                    null,
                    0.0d,
                    0.0d,
                    0.0d,
                    0.0d,
                    true,
                    true,
                    initialEnergy - 1,
                    0,
                    NpcCombatAttackRules
                        .CapturedSubwayStrikeForemanSpecialAttackWeaponUnknown1,
                    NpcCombatAttackRules
                        .CapturedSubwayStrikeForemanSpecialAttackWeaponUnknown2,
                    NpcCombatAttackRules
                        .CapturedSubwayStrikeForemanSpecialAttackWeaponUnknown3,
                    NpcCombatAttackRules
                        .CapturedSubwayStrikeForemanSpecialAttackWeaponUnknown4,
                    NpcCombatAttackRules
                        .CapturedSubwayStrikeForemanSpecialAttackWeaponUnknown5,
                    NpcCombatAttackRules.NormalAttackInfoHitType,
                    0,
                    0,
                    0,
                    0,
                    true,
                    true)
                .WithCapturedWeapon(weaponDefinition)
                .WithProductionEquippedWeaponValues()
                .WithProductionWeaponQuality()
                .WithProductionActorValuesForPresentationWeapon()
                .WithCaptureProvenArchetype(archetypeId);
        }

        private static CapturedEnemyCombatContract ViolentVagabond(int level)
        {
            OrdinaryEnemyCombatNumericSetup generated;
            if (!OrdinaryEnemyCombatSetupGenerator.TryGenerateEquipped(
                    new OrdinaryEnemyEquippedCombatSetupInput(
                        203733,
                        level,
                        130590,
                        130590,
                        1,
                        (int)WeaponSlots.Righthand),
                    out generated))
            {
                return CapturedEnemyCombatContract.Unresolved(
                    "Violent Vagabond mathematical combat setup is unsupported for level "
                    + level,
                    true);
            }

            const int evidenceSourceIdentity = unchecked((int)0x794CD4CCu);
            const string evidence =
                "20260708-143600,20260709-205921/210452/212115/212336: "
                + "owner-linked 130590 QL1 WIFU, 40 distinct embedded-attacker misses, "
                + "empty SAW, Attack action 0, and exact miss-chain ordering; "
                + "normal-result semantics supplied by "
                + OrdinaryEnemyCombatResultDomainRegistry.ViolentVagabondResultDomainId;
            return CapturedEnemyCombatContract
                .EquippedWeaponWithCapturedPacketSequence(
                    evidence,
                    evidenceSourceIdentity,
                    130590,
                    130590,
                    1,
                    (int)WeaponSlots.Righthand,
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
                        evidence,
                        evidenceSourceIdentity,
                        0,
                        11,
                        (int)WeaponSlots.Righthand,
                        1000015,
                        0,
                        262,
                        new[]
                        {
                            CapturedWeaponStat(CharacterStat.Flags, 4199425),
                            CapturedWeaponStat(CharacterStat.StaticInstance, 130590),
                            CapturedWeaponStat(CharacterStat.ACGItemLevel, 1),
                            CapturedWeaponStat(CharacterStat.ACGItemTemplateID, 130590),
                            CapturedWeaponStat(CharacterStat.ACGItemTemplateID2, 130590),
                            CapturedWeaponStat(CharacterStat.MultipleCount, 1),
                            CapturedWeaponStat(CharacterStat.Energy, 1),
                            CapturedWeaponStat(CharacterStat.AttackDelay, 175),
                            CapturedWeaponStat(CharacterStat.RechargeDelay, 175)
                        },
                        0))
                .WithProductionEquippedWeaponValues()
                .WithProductionActorValuesForPresentationWeapon();
        }

        private static CapturedEnemyWeaponDefinition ThiefCapturedWeaponDefinition(
            string evidence)
        {
            return new CapturedEnemyWeaponDefinition(
                evidence,
                unchecked((int)0x795B5DB2u),
                0,
                11,
                (int)WeaponSlots.Righthand,
                1000015,
                0,
                262,
                new[]
                {
                    CapturedWeaponStat(CharacterStat.Flags, 67109889),
                    CapturedWeaponStat(CharacterStat.StaticInstance, 121567),
                    CapturedWeaponStat(CharacterStat.ACGItemLevel, 1),
                    CapturedWeaponStat(CharacterStat.ACGItemTemplateID, 121567),
                    CapturedWeaponStat(CharacterStat.ACGItemTemplateID2, 121567),
                    CapturedWeaponStat(CharacterStat.MultipleCount, 1),
                    CapturedWeaponStat(CharacterStat.Energy, -1),
                    CapturedWeaponStat(CharacterStat.AttackDelay, 235),
                    CapturedWeaponStat(CharacterStat.RechargeDelay, 235)
                },
                0);
        }

        private static CapturedEnemyWeaponStatDefinition CapturedWeaponStat(
            CharacterStat stat,
            int value)
        {
            return new CapturedEnemyWeaponStatDefinition(stat, unchecked((uint)value));
        }

        private static CapturedEnemyCombatContract ForDisobedientBot(int? level)
        {
            OrdinaryEnemyCombatNumericSetup generated;
            if (!level.HasValue
                || !OrdinaryEnemyCombatSetupGenerator.TryGenerate(
                    new OrdinaryEnemyCombatSetupInput(
                        NpcCombatAttackRules.CapturedSubwayDisobedientBotMonsterData,
                        level.Value,
                        NpcCombatAttackRules.CapturedSubwayDisobedientBotLowTemplate,
                        NpcCombatAttackRules.CapturedSubwayDisobedientBotHighTemplate,
                        NpcCombatAttackRules.CapturedSubwayDisobedientBotWeaponTag,
                        NpcCombatAttackRules.CapturedSubwayDisobedientBotWeaponName),
                    out generated))
            {
                return CapturedEnemyCombatContract.Unresolved(
                    "Disobedient Bot SIW1 mathematical combat setup is unsupported for level "
                    + (level.HasValue ? level.Value.ToString() : "unknown"),
                    true);
            }

            CapturedEnemyCombatContract contract =
                CapturedEnemyCombatContract.CapturedSpecialSequence(
                    "20260708-143600, 20260709-205921/210452/220439, 20260712-153918, 20260713-014714/033511, and 20260719-020104: 15 Disobedient Bot SIW1 normal local-player hits span 6-15 damage; focused raw packets prove the categorical SIW1 packet stream and numeric SpecialAttackWeapon values at levels 5, 6, 8, 9, and 10; numeric fields 1-4 are generated by "
                    + generated.FormulaId,
                    new CapturedEnemySpecialAttackSequenceDefinition(
                        NpcCombatAttackRules.CapturedSubwayDisobedientBotInitialAttackSeconds,
                        null,
                        new CapturedEnemyCombatAttackDefinition(
                            NpcCombatAttackRules.CapturedSubwayDisobedientBotMinimumDamage,
                            NpcCombatAttackRules.CapturedSubwayDisobedientBotMaximumDamage,
                            0,
                            NpcCombatAttackRules.MaxMeleeCombatDistance,
                            NpcCombatAttackRules.CapturedSubwayDisobedientBotRechargeSeconds,
                            false,
                            NpcCombatAttackRules.UnarmedAttackInfoAmmoCount,
                            NpcCombatAttackRules.CapturedSubwayDisobedientBotWeaponSlot,
                            0,
                            NpcCombatAttackRules.NormalAttackInfoHitType,
                            NpcCombatAttackRules.CapturedSubwayDisobedientBotWeaponTag,
                            0,
                            true),
                        new[]
                        {
                            new CapturedEnemySpecialAttackDefinition(
                                NpcCombatAttackRules.CapturedSubwayDisobedientBotLowTemplate,
                                NpcCombatAttackRules.CapturedSubwayDisobedientBotHighTemplate,
                                NpcCombatAttackRules.CapturedSubwayDisobedientBotWeaponTag,
                                NpcCombatAttackRules.CapturedSubwayDisobedientBotWeaponName)
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
            return contract;
        }

        private static CapturedEnemyCombatContract ForWorkmanStriker(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int sourceInstance)
        {
            return CapturedEnemyCombatContract.Unresolved(
                string.Format(
                    "Workman Striker source 0x{0:X8} requires a selected capture-reviewed atomic generation variant",
                    sourceInstance),
                archetype != null && archetype.Combat != null && archetype.Combat.Observed);
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
            bool hasExactCombatEvidence = combat != null && combat.Observed;
            bool hasExactGeneration = variant != null
                                      && weapon != null
                                      && generationEvidence != null
                                      && Array.Exists(
                                          generationEvidence,
                                          value => value != null
                                                   && value.MonsterData == WorkmanStrikerMonsterData
                                                   && value.SourceInstance == sourceInstance
                                                   && value.Level == variant.Level
                                                   && value.Health == variant.Health
                                                   && value.HealthDamage == variant.HealthDamage
                                                   && value.MonsterScale == variant.MonsterScale
                                                   && value.RunSpeed == variant.RunSpeed
                                                   && value.WeaponLowId == weapon.LowId
                                                   && value.WeaponHighId == weapon.HighId
                                                   && value.WeaponQuality == weapon.Quality);
            if (!hasExactCombatEvidence
                || !hasExactGeneration)
            {
                return CapturedEnemyCombatContract.Unresolved(
                    "Workman Striker combat requires one exact reviewed atomic level/stat/weapon generation for the selected source.",
                    combat != null && combat.Observed);
            }

            return CapturedEnemyCombatContract.EquippedWeaponWithCapturedAttackInfo(
                string.Format(
                    "{0}: Workman Striker source 0x{1:X8} selected captured L{2} QL{3} weapon {4}/{5} as one atomic generation; 59 distinct normal local-player hits span 9..23, seven criticals remain report-only, and captured AttackInfo uses ammo -1, slot 6, unknown 0, and weapon instance 0; item owns runtime damage and recharge; captured SIW shapes remain report-only",
                    weapon.Evidence,
                    sourceInstance,
                    variant.Level,
                    weapon.Quality,
                    weapon.LowId,
                    weapon.HighId),
                weapon.LowId,
                weapon.HighId,
                weapon.Quality,
                (int)WeaponSlots.Righthand,
                -1,
                (int)WeaponSlots.Righthand,
                0,
                0,
                0,
                0)
                .WithProductionWeaponQuality();
        }

        private static CapturedEnemyCombatContract ForLooter(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int sourceInstance)
        {
            return ForSourceSpecificWeaponArchetype(archetype, sourceInstance, "Looter");
        }

        private static CapturedEnemyCombatContract ForIncompleteRebuild(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int sourceInstance)
        {
            CapturedSubwaySourceWeaponEvidenceDefinition[] evidence =
                archetype == null
                    ? new CapturedSubwaySourceWeaponEvidenceDefinition[0]
                    : archetype.SourceWeaponEvidence;
            CapturedSubwayCombatEvidenceDefinition combat = archetype == null
                ? null
                : archetype.Combat;
            bool hasExactCombatEvidence = combat != null
                                          && combat.Observed
                                          && combat.ObservedRows == 2
                                          && combat.MinDamage == 17
                                          && combat.MaxDamage == 35
                                          && combat.WeaponSlot == (int)WeaponSlots.Righthand
                                          && combat.AttackInfoUnknown == 0
                                          && combat.WeaponInstance == 0;
            if (!hasExactCombatEvidence
                || !HasCompleteIncompleteRebuildSourceWeaponEvidence(evidence))
            {
                return CapturedEnemyCombatContract.Unresolved(
                    "Incomplete Rebuild combat requires the exact two normal 17..35 local-player hits and one owner-linked weapon tuple for each of the ten current sources",
                    combat != null && combat.Observed);
            }

            CapturedSubwaySourceWeaponEvidenceDefinition matched = null;
            int matches = 0;
            foreach (CapturedSubwaySourceWeaponEvidenceDefinition candidate in evidence)
            {
                if (candidate.SourceInstance != sourceInstance)
                {
                    continue;
                }

                matched = candidate;
                matches++;
            }

            if (matches != 1 || matched == null)
            {
                return CapturedEnemyCombatContract.Unresolved(
                    string.Format(
                        "Incomplete Rebuild source 0x{0:X8} requires exactly one owner-linked captured weapon tuple; found {1}",
                        sourceInstance,
                        matches),
                    true);
            }

            return CapturedEnemyCombatContract.EquippedWeaponWithCapturedAttackInfo(
                string.Format(
                    "{0}: Incomplete Rebuild source 0x{1:X8} owner-linked QL{2} weapon {3}/{4}; two normal local-player hits span 17..35 and one captured miss shares ammo 9, slot 6, unknown 0, and weapon instance 0; item owns runtime damage and recharge; no empty SIW or captured attack-start/stop context",
                    matched.EvidenceCaptures,
                    sourceInstance,
                    matched.Quality,
                    matched.LowId,
                    matched.HighId),
                matched.LowId,
                matched.HighId,
                matched.Quality,
                (int)WeaponSlots.Righthand,
                9,
                (int)WeaponSlots.Righthand,
                0,
                0,
                0,
                0)
                .WithProductionWeaponQuality();
        }

        private static CapturedEnemyCombatContract ForIncompleteRebuild(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int sourceInstance,
            int level)
        {
            CapturedSubwaySourceWeaponEvidenceDefinition matched =
                archetype == null
                    ? null
                    : archetype.SourceWeaponEvidence.SingleOrDefault(
                        value => value.SourceInstance == sourceInstance);
            return matched == null
                ? CapturedEnemyCombatContract.Unresolved(
                    string.Format(
                        "Incomplete Rebuild source 0x{0:X8} has no unique owner-linked weapon loadout",
                        sourceInstance),
                    archetype != null
                    && archetype.Combat != null
                    && archetype.Combat.Observed)
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

        private static CapturedEnemyCombatContract ForIncompleteRebuild(
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
                                          && combat.MinDamage == 17
                                          && combat.MaxDamage == 35
                                          && combat.WeaponSlot == (int)WeaponSlots.Righthand
                                          && combat.AttackInfoUnknown == 0
                                          && combat.WeaponInstance == 0;
            OrdinaryEnemySpawnWeaponLoadout weapon = variant == null
                ? null
                : variant.WeaponLoadout;
            string atomicFailure = string.Empty;
            if (!hasExactCombatEvidence
                || archetype == null
                || !HasCompleteIncompleteRebuildSourceWeaponEvidence(
                    archetype.SourceWeaponEvidence)
                || Array.IndexOf(IncompleteRebuildSourceInstances, sourceInstance) < 0
                || !OrdinaryEnemyAtomicGenerationEvidenceValidator.TryValidateSelectedVariant(
                    IncompleteRebuildMonsterData,
                    sourceInstance,
                    variant,
                    generationEvidence,
                    out atomicFailure))
            {
                return CapturedEnemyCombatContract.Unresolved(
                    "Incomplete Rebuild combat requires one exact reviewed atomic level/stat/weapon generation for the selected source",
                    hasExactCombatEvidence);
            }

            return ForGeneratedEquippedWeaponSetup(
                archetype,
                sourceInstance,
                variant.Level,
                weapon);
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
                        (int)WeaponSlots.Righthand),
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
                    string.Format(
                        "{0}: source 0x{1:X8} selected captured L{2} QL{3} weapon {4}/{5}; numeric SAW setup={6}; item and actor state own damage, range, cadence, Energy, and ammunition",
                        weapon.Evidence,
                        sourceInstance,
                        level,
                        weapon.Quality,
                        weapon.LowId,
                        weapon.HighId,
                        generated.FormulaId),
                    sourceInstance,
                    weapon.LowId,
                    weapon.HighId,
                    weapon.Quality,
                    (int)WeaponSlots.Righthand,
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

        private static CapturedEnemyCombatContract ForDerangedShopper(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int sourceInstance)
        {
            CapturedSubwaySourceWeaponEvidenceDefinition[] evidence =
                archetype == null
                    ? new CapturedSubwaySourceWeaponEvidenceDefinition[0]
                    : archetype.SourceWeaponEvidence;
            if (sourceInstance != DerangedShopperSourceInstance
                || evidence == null
                || evidence.Length != 1
                || evidence[0].SourceInstance != DerangedShopperSourceInstance
                || evidence[0].LowId != 125454
                || evidence[0].HighId != 125455
                || evidence[0].Quality != 8)
            {
                return CapturedEnemyCombatContract.Unresolved(
                    string.Format(
                        "Deranged Shopper source 0x{0:X8} requires the one exact owner-linked QL8 125454/125455 tuple",
                        sourceInstance),
                    archetype != null && archetype.Combat != null && archetype.Combat.Observed);
            }

            return CapturedEnemyCombatContract.EquippedWeaponWithCapturedAttackInfo(
                evidence[0].EvidenceCaptures + ",20260720-031025"
                + ": Deranged Shopper source 0x79574527 owner-linked QL8 weapon 125454/125455; ten normal local-player hits span 7..15, one 27-point critical is report-only, and six captured misses preserve ammo -1, slot 6, unknown 0, and weapon instance 0; capture 20260720-031025 also proves empty SpecialAttackWeapon 56/45/45/45/0 plus attack-start, StopFight, and death context; item owns runtime damage, damage bonus, and recharge; captured AttackInfo carries only ammo -1, slot 6, unknown 0, and weapon instance 0; the newly observed SIW/start/stop/death context remains evidence-only so runtime behavior is unchanged",
                evidence[0].LowId,
                evidence[0].HighId,
                evidence[0].Quality,
                (int)WeaponSlots.Righthand,
                -1,
                (int)WeaponSlots.Righthand,
                0,
                0,
                0,
                0);
        }

        private static CapturedEnemyCombatContract ForRedundantScan(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int sourceInstance)
        {
            CapturedSubwaySourceWeaponEvidenceDefinition[] evidence =
                archetype == null
                    ? new CapturedSubwaySourceWeaponEvidenceDefinition[0]
                    : archetype.SourceWeaponEvidence;
            bool retaliationObserved = archetype != null
                                       && archetype.Combat != null
                                       && archetype.Combat.Observed;
            if (!HasCompleteRedundantScanSourceWeaponEvidence(evidence))
            {
                return CapturedEnemyCombatContract.Unresolved(
                    "Redundant Scan combat requires one exact owner-linked weapon tuple for each of the four current sources",
                    retaliationObserved);
            }

            CapturedSubwaySourceWeaponEvidenceDefinition matched = null;
            int matches = 0;
            foreach (CapturedSubwaySourceWeaponEvidenceDefinition candidate in evidence)
            {
                if (candidate.SourceInstance != sourceInstance)
                {
                    continue;
                }

                matched = candidate;
                matches++;
            }

            if (matches != 1 || matched == null)
            {
                return CapturedEnemyCombatContract.Unresolved(
                    string.Format(
                        "Redundant Scan source 0x{0:X8} requires exactly one owner-linked captured weapon tuple; found {1}",
                        sourceInstance,
                        matches),
                    retaliationObserved);
            }

            return CapturedEnemyCombatContract.EquippedWeaponWithCapturedAttackInfo(
                string.Format(
                    "{0}: Redundant Scan source 0x{1:X8} owner-linked QL{2} weapon {3}/{4}; one normal local-player hit is 19; item owns runtime damage and recharge; captured AttackInfo carries only ammo 17, slot 6, unknown 0, and weapon instance 0; no fixed damage, empty SIW, or captured attack-start/stop context",
                    matched.EvidenceCaptures,
                    sourceInstance,
                    matched.Quality,
                    matched.LowId,
                    matched.HighId),
                matched.LowId,
                matched.HighId,
                matched.Quality,
                (int)WeaponSlots.Righthand,
                17,
                (int)WeaponSlots.Righthand,
                0,
                0,
                0,
                0)
                .WithProductionWeaponQuality();
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
                                          && combat.WeaponSlot == (int)WeaponSlots.Righthand
                                          && combat.AttackInfoUnknown == 0
                                          && combat.WeaponInstance == 0;
            OrdinaryEnemySpawnWeaponLoadout weapon = variant == null
                ? null
                : variant.WeaponLoadout;
            OrdinaryEnemyCombatNumericSetup generated;
            string atomicFailure = string.Empty;
            if (!hasExactCombatEvidence
                || archetype == null
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
                    "Fragmented Soul combat requires one exact owner-linked weapon generation inside the proven mathematical setup domain",
                    hasExactCombatEvidence);
            }

            CapturedEnemyCombatContract contract = CapturedEnemyCombatContract
                .EquippedWeaponWithCapturedPacketSequence(
                    string.Format(
                        "{0}: Fragmented Soul source 0x{1:X8} selected captured L{2} QL{3} weapon {4}/{5}; numeric SAW setup={6}; item owns damage, range, and cadence",
                        weapon.Evidence,
                        sourceInstance,
                        variant.Level,
                        weapon.Quality,
                        weapon.LowId,
                        weapon.HighId,
                        generated.FormulaId),
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
            return contract;
        }

        private static CapturedEnemyCombatContract ForRedundantScan(
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
                                          && combat.ObservedRows == 1
                                          && combat.MinDamage == 19
                                          && combat.MaxDamage == 19
                                          && combat.WeaponSlot == (int)WeaponSlots.Righthand
                                          && combat.AttackInfoUnknown == 0
                                          && combat.WeaponInstance == 0;
            OrdinaryEnemySpawnWeaponLoadout weapon = variant == null
                ? null
                : variant.WeaponLoadout;
            string atomicFailure = string.Empty;
            if (!hasExactCombatEvidence
                || archetype == null
                || !HasCompleteRedundantScanSourceWeaponEvidence(
                    archetype.SourceWeaponEvidence)
                || Array.IndexOf(RedundantScanSourceInstances, sourceInstance) < 0
                || !OrdinaryEnemyAtomicGenerationEvidenceValidator.TryValidateSelectedVariant(
                    RedundantScanMonsterData,
                    sourceInstance,
                    variant,
                    generationEvidence,
                    out atomicFailure))
            {
                return CapturedEnemyCombatContract.Unresolved(
                    "Redundant Scan combat requires one exact reviewed atomic level/stat/weapon generation for the selected source",
                    hasExactCombatEvidence);
            }

            return CapturedEnemyCombatContract.EquippedWeaponWithCapturedAttackInfo(
                string.Format(
                    "{0}: Redundant Scan source 0x{1:X8} selected captured L{2} QL{3} weapon {4}/{5} as one atomic generation; one normal local-player hit is 19; item owns runtime damage and recharge; captured AttackInfo carries only ammo 17, slot 6, unknown 0, and weapon instance 0; uniform selection over distinct captured generations is private policy",
                    weapon.Evidence,
                    sourceInstance,
                    variant.Level,
                    weapon.Quality,
                    weapon.LowId,
                    weapon.HighId),
                weapon.LowId,
                weapon.HighId,
                weapon.Quality,
                (int)WeaponSlots.Righthand,
                17,
                (int)WeaponSlots.Righthand,
                0,
                0,
                0,
                0)
                .WithProductionEquippedWeaponValues()
                .WithProductionWeaponQuality();
        }

        private static CapturedEnemyCombatContract ForSourceSpecificWeaponArchetype(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int sourceInstance,
            string displayName)
        {
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
                return CapturedEnemyCombatContract.Unresolved(
                    string.Format(
                        "{0} source 0x{1:X8} requires exactly one owner-linked captured weapon tuple; found {2}",
                        displayName,
                        sourceInstance,
                        matches),
                    archetype.Combat != null && archetype.Combat.Observed);
            }

            return CapturedEnemyCombatContract.EquippedWeapon(
                string.Format(
                    "{0}: {1} source 0x{2:X8} owner-linked QL{3} weapon {4}/{5}; item owns normal damage and recharge; no fixed damage, special-attack, or captured AttackInfo context",
                    matched.EvidenceCaptures,
                    displayName,
                    sourceInstance,
                    matched.Quality,
                    matched.LowId,
                    matched.HighId),
                matched.LowId,
                matched.HighId,
                matched.Quality,
                (int)WeaponSlots.Righthand)
                .WithProductionWeaponQuality();
        }

        internal static CapturedEnemyCombatContract ForSupportedSourceWeapon(
            string name,
            int monsterData,
            CapturedSubwaySourceWeaponEvidenceDefinition[] sourceWeaponEvidence,
            int sourceInstance)
        {
            if (!string.Equals(name, "Mugger", StringComparison.Ordinal)
                || monsterData != MuggerMonsterData)
            {
                return CapturedEnemyCombatContract.Unresolved(
                    string.Format(
                        "Unsupported source-specific weapon profile {0} monsterData={1}",
                        name,
                        monsterData),
                    false);
            }

            if (!HasCompleteMuggerSourceWeaponEvidence(sourceWeaponEvidence))
            {
                return CapturedEnemyCombatContract.Unresolved(
                    "Mugger combat requires one exact QL1 121567/121567 owner-linked weapon tuple for each of the nine current sources",
                    true);
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
                return CapturedEnemyCombatContract.Unresolved(
                    string.Format(
                        "Mugger source 0x{0:X8} requires exactly one owner-linked captured weapon tuple; found {1}",
                        sourceInstance,
                        matches),
                    true);
            }

            return CapturedEnemyCombatContract.EquippedWeaponWithCapturedAttackInfo(
                string.Format(
                    "{0}: Mugger source 0x{1:X8} owner-linked QL1 weapon 121567/121567; 38 normal local-player hits span 9..12, three 21-point criticals are report-only, and the median interval is 5.816469 seconds; item owns runtime damage, damage bonus, and recharge; captured AttackInfo carries only ammo -1, slot 6, unknown 0, and weapon instance 0; no empty SIW or captured attack-start/stop context",
                    matched.EvidenceCaptures,
                    sourceInstance),
                matched.LowId,
                matched.HighId,
                matched.Quality,
                (int)WeaponSlots.Righthand,
                -1,
                (int)WeaponSlots.Righthand,
                0,
                0,
                0,
                0,
                true);
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
                int matches = 0;
                foreach (CapturedSubwaySourceWeaponEvidenceDefinition evidence in sourceWeaponEvidence)
                {
                    if (evidence.SourceInstance == expectedSource
                        && evidence.LowId == 121567
                        && evidence.HighId == 121567
                        && evidence.Quality == 1)
                    {
                        matches++;
                    }
                }

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
                int matches = 0;
                foreach (CapturedSubwaySourceWeaponEvidenceDefinition evidence in sourceWeaponEvidence)
                {
                    if (IsExactRedundantScanSourceWeapon(evidence, expectedSource))
                    {
                        matches++;
                    }
                }

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
                int matches = 0;
                foreach (CapturedSubwaySourceWeaponEvidenceDefinition evidence in sourceWeaponEvidence)
                {
                    if (IsExactIncompleteRebuildSourceWeapon(evidence, expectedSource))
                    {
                        matches++;
                    }
                }

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
                    return evidence.LowId == 122653
                           && evidence.HighId == 122654
                           && evidence.Quality == 18;
                case 0x79545172:
                    return evidence.LowId == 122653
                           && evidence.HighId == 122654
                           && evidence.Quality == 14;
                case 0x79545188:
                    return evidence.LowId == 122653
                           && evidence.HighId == 122654
                           && evidence.Quality == 17;
                case 0x79545181:
                case 0x795451FD:
                case 0x79545241:
                    return evidence.LowId == 122654
                           && evidence.HighId == 122654
                           && evidence.Quality == 20;
                case 0x795451C1:
                    return evidence.LowId == 122655
                           && evidence.HighId == 122655
                           && evidence.Quality == 21;
                case 0x795451CB:
                    return evidence.LowId == 122655
                           && evidence.HighId == 122656
                           && evidence.Quality == 24;
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

        private static CapturedEnemyCombatContract ForMeldedPatterns(
            CapturedSubwayOrdinaryArchetypeDefinition archetype)
        {
            CapturedSubwayCombatEvidenceDefinition combat = archetype.Combat;
            bool hasFocusedWeaponCapture = archetype.EvidenceCaptures != null
                                           && Array.IndexOf(
                                               archetype.EvidenceCaptures,
                                               "20260716-034559") >= 0;
            bool hasExactNormalHitBoundary = combat != null
                                             && combat.Observed
                                             && combat.ObservedRows == 7
                                             && combat.MinDamage == 21
                                             && combat.MaxDamage == 34
                                             && combat.WeaponSlot
                                                == (int)WeaponSlots.Righthand;
            if (!hasFocusedWeaponCapture || !hasExactNormalHitBoundary)
            {
                return CapturedEnemyCombatContract.Unresolved(
                    "Melded Patterns equipped-weapon context requires focused capture 20260716-034559 and its seven normal 21..34 local-player hits",
                    combat != null && combat.Observed);
            }

            return CapturedEnemyCombatContract.EquippedWeapon(
                "20260716-034559: Melded Patterns QL20 Irreparable Sleekblaster Minor 121817/121818; seven normal local-player hits span 21..34 and no critical was observed; weapon owns runtime damage and recharge",
                NpcCombatAttackRules.CapturedSubwayMeldedPatternsWeaponLowTemplate,
                NpcCombatAttackRules.CapturedSubwayMeldedPatternsWeaponHighTemplate,
                NpcCombatAttackRules.CapturedSubwayMeldedPatternsWeaponQuality,
                (int)WeaponSlots.Righthand);
        }

        private static CapturedEnemyCombatContract ForMeldedPatterns(
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
            OrdinaryEnemyCombatNumericSetup generated;
            string atomicFailure;
            if (combat == null
                || !combat.Observed
                || !combat.RuntimeReady
                || archetype.MonsterData
                   != NpcCombatAttackRules.CapturedSubwayMeldedPatternsMonsterData
                || !OrdinaryEnemyAtomicGenerationEvidenceValidator.TryValidateSelectedVariant(
                    NpcCombatAttackRules.CapturedSubwayMeldedPatternsMonsterData,
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
                        NpcCombatAttackRules.CapturedSubwayMeldedPatternsWeaponSlot),
                    out generated))
            {
                return CapturedEnemyCombatContract.Unresolved(
                    "Melded Patterns combat requires one exact owner-linked weapon generation inside the proven mathematical setup domain",
                    combat != null && combat.Observed);
            }

            return CapturedEnemyCombatContract
                .EquippedWeaponWithCapturedPacketSequence(
                    string.Format(
                        "{0}: Melded Patterns source 0x{1:X8} selected captured L{2} QL{3} weapon {4}/{5}; numeric SAW setup={6}; weapon item owns damage, range, and cadence",
                        weapon.Evidence,
                        sourceInstance,
                        variant.Level,
                        weapon.Quality,
                        weapon.LowId,
                        weapon.HighId,
                        generated.FormulaId),
                    sourceInstance,
                    weapon.LowId,
                    weapon.HighId,
                    weapon.Quality,
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
                return CapturedEnemyCombatContract.Unresolved(
                    archetype.Name
                    + " combat requires an exact captured source identity; aggregate weapon fallback is forbidden",
                    archetype.Combat != null && archetype.Combat.Observed);
            }

            if (archetype != null
                && archetype.MonsterData
                   == NpcCombatAttackRules.CapturedSubwayMeldedPatternsMonsterData)
            {
                return ForMeldedPatterns(archetype);
            }

            if (archetype != null
                && archetype.MonsterData == NpcCombatAttackRules.CapturedSubwayBloodcreeperMonsterData)
            {
                return For(archetype.Name, archetype.MonsterData);
            }

            CapturedSubwayCombatEvidenceDefinition combat = archetype.Combat;
            if (combat == null || !combat.Observed)
            {
                return CapturedEnemyCombatContract.Unresolved(
                    "Generated ordinary archetype has no observed AttackInfo: " + archetype.Name,
                    false);
            }

            if (!combat.RuntimeReady)
            {
                return CapturedEnemyCombatContract.Unresolved(
                    "Generated ordinary archetype has report-only AttackInfo evidence without a runtime-ready damage range and cadence: "
                    + archetype.Name,
                    true);
            }

            CapturedEnemyCombatContract contract = CapturedEnemyCombatContract.FixedAttack(
                string.Join(",", archetype.EvidenceCaptures),
                combat.MinDamage,
                combat.MaxDamage,
                combat.RechargeSeconds,
                combat.WeaponSlot,
                combat.AttackInfoUnknown,
                combat.WeaponInstance,
                0,
                0,
                0);
            if (archetype.MonsterData == 203727
                || archetype.MonsterData == 96056
                || archetype.MonsterData == 203743
                || archetype.MonsterData == 55648
                || archetype.MonsterData == 30464
                || archetype.MonsterData == 96195
                || archetype.MonsterData == 203731
                || archetype.MonsterData == 31909
                || archetype.MonsterData == 203730
                || archetype.MonsterData == 96193)
            {
                return contract.WithProductionSpecializedValues();
            }

            return archetype.MonsterData == 203746
                       ? contract.WithProductionEquippedWeaponValues()
                       : contract;
        }

        internal static CapturedEnemyCombatContract ForOrdinaryGeneratedSetup(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int level)
        {
            return archetype != null
                   && archetype.MonsterData
                   == NpcCombatAttackRules.CapturedSubwayStimFiendMonsterData
                ? ForStimFiend(archetype, level)
                : ForOrdinary(archetype);
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

            if (archetype.MonsterData == IncompleteRebuildMonsterData)
            {
                return ForIncompleteRebuild(archetype, sourceInstance, level);
            }

            if (archetype.MonsterData
                == NpcCombatAttackRules.CapturedSubwayMolestedMoleculesMonsterData)
            {
                CapturedSubwaySourceWeaponEvidenceDefinition matched =
                    archetype.SourceWeaponEvidence.SingleOrDefault(
                        value => value.SourceInstance == sourceInstance);
                return matched == null
                    ? ForOrdinary(archetype)
                        .WithEvidenceSourceHint(sourceInstance)
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
            CapturedSubwayGenerationVariantDefinition[] levelMatches =
                (generationEvidence
                 ?? new CapturedSubwayGenerationVariantDefinition[0])
                    .Where(value => value != null && value.Level == level)
                    .ToArray();
            if (levelMatches.Length != 1)
            {
                return CapturedEnemyCombatContract.Unresolved(
                    string.Format(
                        "Source 0x{0:X8} level {1} has {2} atomic generations; runtime loadout selection is required",
                        sourceInstance,
                        level,
                        levelMatches.Length),
                    archetype != null
                    && archetype.Combat != null
                    && archetype.Combat.Observed);
            }

            CapturedSubwayGenerationVariantDefinition selected = levelMatches[0];
            var variant = new OrdinaryEnemySpawnVariant(
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
                    selected.Evidence));
            return ForOrdinary(
                archetype,
                sourceInstance,
                variant,
                generationEvidence);
        }

        private static CapturedEnemyCombatContract ForStimFiend(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int level)
        {
            CapturedSubwayCombatEvidenceDefinition combat =
                archetype == null ? null : archetype.Combat;
            OrdinaryEnemyCombatNumericSetup generated;
            if (combat == null
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
                return CapturedEnemyCombatContract.Unresolved(
                    "Stim Fiend SIW1 mathematical combat setup is unsupported for level "
                    + level,
                    combat != null && combat.Observed);
            }

            return CapturedEnemyCombatContract
                .CapturedSpecialSequence(
                    string.Join(",", archetype.EvidenceCaptures)
                    + ": Stim Fiend categorical SIW1 semantics with numeric fields 1-4 generated by "
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

        internal static CapturedEnemyCombatContract ForOrdinary(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int sourceInstance)
        {
            if (archetype != null && archetype.MonsterData == DerangedShopperMonsterData)
            {
                return ForDerangedShopper(archetype, sourceInstance);
            }

            if (archetype != null && archetype.MonsterData == WorkmanStrikerMonsterData)
            {
                return ForWorkmanStriker(archetype, sourceInstance);
            }

            if (archetype != null && archetype.MonsterData == IncompleteRebuildMonsterData)
            {
                return ForIncompleteRebuild(archetype, sourceInstance);
            }

            if (archetype != null && archetype.MonsterData == LooterMonsterData)
            {
                return ForLooter(archetype, sourceInstance);
            }

            if (archetype != null && archetype.MonsterData == RedundantScanMonsterData)
            {
                return ForRedundantScan(archetype, sourceInstance);
            }

            return ForOrdinary(archetype);
        }

        internal static CapturedEnemyCombatContract ForOrdinary(
            CapturedSubwayOrdinaryArchetypeDefinition archetype,
            int sourceInstance,
            OrdinaryEnemySpawnVariant variant,
            CapturedSubwayGenerationVariantDefinition[] generationEvidence)
        {
            if (archetype != null
                && archetype.MonsterData == WorkmanStrikerMonsterData)
            {
                return ForWorkmanStriker(
                    archetype,
                    sourceInstance,
                    variant,
                    generationEvidence);
            }

            if (archetype != null
                && archetype.MonsterData == IncompleteRebuildMonsterData)
            {
                return ForIncompleteRebuild(
                    archetype,
                    sourceInstance,
                    variant,
                    generationEvidence);
            }

            if (archetype != null
                && archetype.MonsterData == FragmentedSoulMonsterData)
            {
                return ForFragmentedSoul(
                    archetype,
                    sourceInstance,
                    variant,
                    generationEvidence);
            }

            if (archetype != null
                && archetype.MonsterData
                   == NpcCombatAttackRules.CapturedSubwayMeldedPatternsMonsterData)
            {
                return ForMeldedPatterns(
                    archetype,
                    sourceInstance,
                    variant,
                    generationEvidence);
            }

            return archetype != null
                   && archetype.MonsterData == RedundantScanMonsterData
                ? ForRedundantScan(
                    archetype,
                    sourceInstance,
                    variant,
                    generationEvidence)
                : ForOrdinary(archetype, sourceInstance);
        }
    }
}
