namespace ZoneEngine.Core.Playfields.Content
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    #endregion

    /// <summary>
    /// Jobe Platform (4530 / 0x11B2) — Perk-Reset Service Provider spawn.
    /// Capture 20260716-Reset-perks.
    /// </summary>
    public sealed class JobePlatformContentModule : IPlayfieldContentModule
    {
        private const int JobePlatformPlayfieldInstance = 4530;

        public bool Supports(Identity playfieldIdentity)
        {
            return playfieldIdentity.Instance == JobePlatformPlayfieldInstance;
        }

        public void Register(PlayfieldContentRegistration registration)
        {
            if (registration == null || !this.Supports(registration.PlayfieldIdentity))
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "JobePlatformContentModule RegisterCapturedNpcSpawns pf="
                + registration.PlayfieldIdentity.Instance);
            registration.RegisterCapturedNpcSpawns();
        }

        public bool ShouldSuppressDbMobSpawn(int playfieldInstance, int mobSpawnId)
        {
            return false;
        }
    }
}
