namespace ZoneEngine.Core.Playfields.Content
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    public sealed class TempleOfThreeWindsContentModule : IPlayfieldContentModule
    {
        private const int TempleOfThreeWindsPlayfieldInstance = 1931;

        public bool Supports(Identity playfieldIdentity)
        {
            return playfieldIdentity.Instance == TempleOfThreeWindsPlayfieldInstance;
        }

        public void Register(PlayfieldContentRegistration registration)
        {
            if (registration == null || !this.Supports(registration.PlayfieldIdentity))
            {
                return;
            }

            registration.RegisterCapturedNpcSpawns();
        }

        public bool ShouldSuppressDbMobSpawn(int playfieldInstance, int mobSpawnId)
        {
            return false;
        }
    }
}
