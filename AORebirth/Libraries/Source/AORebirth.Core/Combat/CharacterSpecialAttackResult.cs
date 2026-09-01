namespace AORebirth.Core.Combat
{
    public sealed class CharacterSpecialAttackResult
    {
        public StrikeOutcome Outcome { get; set; }

        public int Damage { get; set; }

        public int AmmoCount { get; set; }

        public int EquipSlot { get; set; }

        public int SpecialStatId { get; set; }
    }
}
