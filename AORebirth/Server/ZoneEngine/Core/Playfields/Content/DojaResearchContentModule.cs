namespace ZoneEngine.Core.Playfields.Content
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using AORebirth.Core.Playfields;

    #endregion

    /// <summary>
    /// DOJA Research / Lab R1 — PF 7010 (Scarlett Dalquist Nascense DOJA chip turn-in).
    /// Capture 20260821-222107.
    /// </summary>
    public sealed class DojaResearchContentModule : IPlayfieldContentModule
    {
        public bool Supports(Identity playfieldIdentity)
        {
            return playfieldIdentity.Instance == ScarlettDalquistSpawn.DojaResearchPlayfieldId;
        }

        public void Register(PlayfieldContentRegistration registration)
        {
            if (registration == null || !this.Supports(registration.PlayfieldIdentity))
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "DojaResearchContentModule RegisterCapturedNpcSpawns pf="
                + registration.PlayfieldIdentity.Instance);
            registration.RegisterCapturedNpcSpawns();
        }

        public bool ShouldSuppressDbMobSpawn(int playfieldInstance, int mobSpawnId)
        {
            return false;
        }
    }
}
