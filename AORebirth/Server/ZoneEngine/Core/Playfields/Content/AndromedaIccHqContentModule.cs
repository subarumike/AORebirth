namespace ZoneEngine.Core.Playfields.Content
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    #endregion

    /// <summary>
    /// ICC HQ Andromeda (655 / 0x028F) — capture-backed city NPC population.
    /// Capture 20260719-ICC-Capture.
    /// </summary>
    public sealed class AndromedaIccHqContentModule : IPlayfieldContentModule
    {
        private const int AndromedaPlayfieldInstance = 655;

        public bool Supports(Identity playfieldIdentity)
        {
            return playfieldIdentity.Instance == AndromedaPlayfieldInstance;
        }

        public void Register(PlayfieldContentRegistration registration)
        {
            if (registration == null || !this.Supports(registration.PlayfieldIdentity))
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "AndromedaIccHqContentModule RegisterCapturedNpcSpawns pf="
                + registration.PlayfieldIdentity.Instance);
            registration.RegisterCapturedNpcSpawns();
        }

        public bool ShouldSuppressDbMobSpawn(int playfieldInstance, int mobSpawnId)
        {
            return false;
        }
    }
}
