namespace ZoneEngine.Core.Playfields.Content
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    #endregion

    /// <summary>
    /// ICC Shuttleport (4582 / 0x11E6) capture-backed NPC population.
    /// Capture ICC Shuttleport [PF 4582] - 20260818-214552.
    /// </summary>
    public sealed class IccShuttleportContentModule : IPlayfieldContentModule
    {
        private const int IccShuttleportPlayfieldInstance = 4582;

        public bool Supports(Identity playfieldIdentity)
        {
            return playfieldIdentity.Instance == IccShuttleportPlayfieldInstance;
        }

        public void Register(PlayfieldContentRegistration registration)
        {
            if (registration == null || !this.Supports(registration.PlayfieldIdentity))
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "IccShuttleportContentModule RegisterCapturedNpcSpawns pf="
                + registration.PlayfieldIdentity.Instance);
            registration.RegisterCapturedNpcSpawns();
        }

        public bool ShouldSuppressDbMobSpawn(int playfieldInstance, int mobSpawnId)
        {
            return false;
        }
    }
}
