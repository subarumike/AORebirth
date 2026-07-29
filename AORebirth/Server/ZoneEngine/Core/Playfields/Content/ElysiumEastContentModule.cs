namespace ZoneEngine.Core.Playfields.Content
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    #endregion

    /// <summary>
    /// Elysium East (PF 4543) + South (PF 4540) — captures 182451/190145/193914.
    /// </summary>
    public sealed class ElysiumEastContentModule : IPlayfieldContentModule
    {
        internal const int EastOfElysiumPlayfieldId = 4543;

        internal const int SouthOfElysiumPlayfieldId = 4540;

        public bool Supports(Identity playfieldIdentity)
        {
            return playfieldIdentity.Instance == EastOfElysiumPlayfieldId
                   || playfieldIdentity.Instance == SouthOfElysiumPlayfieldId;
        }

        public void Register(PlayfieldContentRegistration registration)
        {
            if (registration == null || !this.Supports(registration.PlayfieldIdentity))
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "ElysiumEastContentModule RegisterCapturedNpcSpawns pf="
                + registration.PlayfieldIdentity.Instance);
            registration.RegisterCapturedNpcSpawns();
        }

        public bool ShouldSuppressDbMobSpawn(int playfieldInstance, int mobSpawnId)
        {
            return false;
        }
    }
}
