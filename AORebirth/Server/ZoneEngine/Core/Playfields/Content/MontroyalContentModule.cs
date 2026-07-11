namespace ZoneEngine.Core.Playfields.Content
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    public sealed class MontroyalContentModule : IPlayfieldContentModule
    {
        private const int MontroyalPlayfieldInstance = 655;

        public bool Supports(Identity playfieldIdentity)
        {
            if (playfieldIdentity.Type != IdentityType.Playfield
                && playfieldIdentity.Type != IdentityType.Playfield2)
            {
                return false;
            }

            return playfieldIdentity.Instance == MontroyalPlayfieldInstance;
        }

        public void Register(PlayfieldContentRegistration registration)
        {
        }

        public bool ShouldSuppressDbMobSpawn(int playfieldInstance, int mobSpawnId)
        {
            return false;
        }
    }
}
