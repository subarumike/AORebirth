namespace ZoneEngine.Core.Playfields.Content
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using AORebirth.Core.Playfields;

    #endregion

    /// <summary>
    /// Thrak Omni (Unredeemed) Nascence garden — PF 4677.
    /// Capture 20260718-165625.
    /// </summary>
    public sealed class ThrakOmniGardenContentModule : IPlayfieldContentModule
    {
        public bool Supports(Identity playfieldIdentity)
        {
            return playfieldIdentity.Instance == ThrakOmniGardenSpawn.ThrakOmniGardenPlayfieldId;
        }

        public void Register(PlayfieldContentRegistration registration)
        {
            if (registration == null || !this.Supports(registration.PlayfieldIdentity))
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "ThrakOmniGardenContentModule RegisterCapturedNpcSpawns pf="
                + registration.PlayfieldIdentity.Instance);
            registration.RegisterCapturedNpcSpawns();
        }

        public bool ShouldSuppressDbMobSpawn(int playfieldInstance, int mobSpawnId)
        {
            return false;
        }
    }
}
