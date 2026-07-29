namespace ZoneEngine.Core.Playfields.Content
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    #endregion

    /// <summary>
    /// Crashed Alien Ship (8009 / 0x1F49) — Karli Cappelleri + interior population.
    /// Captures 20260727-055715 / 20260727-Alien- quest-ncu.
    /// </summary>
    public sealed class CrashedAlienShipContentModule : IPlayfieldContentModule
    {
        private const int CrashedAlienShipPlayfieldInstance = 8009;

        public bool Supports(Identity playfieldIdentity)
        {
            return playfieldIdentity.Instance == CrashedAlienShipPlayfieldInstance;
        }

        public void Register(PlayfieldContentRegistration registration)
        {
            if (registration == null || !this.Supports(registration.PlayfieldIdentity))
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "CrashedAlienShipContentModule RegisterCapturedNpcSpawns pf="
                + registration.PlayfieldIdentity.Instance);
            registration.RegisterCapturedNpcSpawns();
        }

        public bool ShouldSuppressDbMobSpawn(int playfieldInstance, int mobSpawnId)
        {
            return playfieldInstance == CrashedAlienShipPlayfieldInstance;
        }
    }
}
