namespace AORebirth.Core.Playfields
{
    using System;
    using System.Linq;
    using SmokeLounge.AOtomation.Messaging.GameData;

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

}
