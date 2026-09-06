namespace AORebirth.Interfaces.Persistence.Characters
{
    /// <summary>A captured character and its prior nonzero online value.</summary>
    public sealed class StaleOnlineCharacterData
    {
        public StaleOnlineCharacterData(int characterId, int previousOnline)
        {
            this.CharacterId = characterId;
            this.PreviousOnline = previousOnline;
        }

        public int CharacterId { get; private set; }
        public int PreviousOnline { get; private set; }
    }
}
