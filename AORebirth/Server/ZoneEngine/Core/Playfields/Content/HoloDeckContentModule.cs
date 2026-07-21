namespace ZoneEngine.Core.Playfields.Content
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    #endregion

    /// <summary>
    /// ICC Holodeck Freelancers Inc. (7001 / 0x1B59) — capture-backed population + vendor.
    /// Capture 20260719-155043.
    /// </summary>
    public sealed class HoloDeckContentModule : IPlayfieldContentModule
    {
        private const int HoloDeckPlayfieldInstance = 7001;

        public bool Supports(Identity playfieldIdentity)
        {
            return playfieldIdentity.Instance == HoloDeckPlayfieldInstance;
        }

        public void Register(PlayfieldContentRegistration registration)
        {
            if (registration == null || !this.Supports(registration.PlayfieldIdentity))
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "HoloDeckContentModule RegisterCapturedNpcSpawns pf="
                + registration.PlayfieldIdentity.Instance);
            registration.RegisterCapturedNpcSpawns();
        }

        public bool ShouldSuppressDbMobSpawn(int playfieldInstance, int mobSpawnId)
        {
            // Capture owns the holodeck interior; suppress any DB mobs on 7001.
            return playfieldInstance == HoloDeckPlayfieldInstance;
        }
    }
}
