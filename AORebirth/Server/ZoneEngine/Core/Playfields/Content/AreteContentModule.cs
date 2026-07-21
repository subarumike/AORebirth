namespace ZoneEngine.Core.Playfields.Content
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    public sealed class AreteContentModule : IPlayfieldContentModule
    {
        private const int PrivateAretePlayfieldInstance = 6553;

        public bool Supports(Identity playfieldIdentity)
        {
            // PF 655 (Andromeda / ICC HQ) is owned by AndromedaIccHqContentModule.
            // Do not also Support 655 here — dual RegisterCapturedNpcSpawns doubles city NPCs.
            return playfieldIdentity.Instance == PrivateAretePlayfieldInstance;
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
            if (playfieldInstance != PrivateAretePlayfieldInstance)
            {
                return false;
            }

            switch (mobSpawnId)
            {
                case 2027138231:
                case 2027138245:
                case 2027138246:
                case 2027138249:
                case 2027138259:
                    return true;
                default:
                    return false;
            }
        }
    }
}
