namespace AORebirth.Core.Combat
{
    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    public sealed class CombatStrikeContext
    {
        public int MinDamage { get; set; }

        public int MaxDamage { get; set; }

        public int DamageBonus { get; set; }

        public double Range { get; set; }

        public bool UsesEquippedWeapon { get; set; }

        public int AttackInfoAmmoCount { get; set; }

        public int AttackInfoWeaponSlot { get; set; }

        public int AttackInfoHitType { get; set; }

        public int AttackInfoWeaponInstance { get; set; }

        public CombatDamageSource DamageSource { get; set; }

        public WeaponSlot WeaponSlot { get; set; }

        public int WeaponLowId { get; set; }

        public int WeaponHighId { get; set; }

        public int WeaponQualityLevel { get; set; }

        public int RawDamageType { get; set; }

        public string AttackSkillDefinitions { get; set; }

        public string AttackSkillValues { get; set; }

        public int? EffectiveAttackRating { get; set; }

        public int? AddAllOff { get; set; }

        public bool SendAttackInfo { get; set; } = true;

        public StatIds? SpecialAttackStat { get; set; }

        public int OutgoingDamageScale { get; set; } = 1;
    }
}
