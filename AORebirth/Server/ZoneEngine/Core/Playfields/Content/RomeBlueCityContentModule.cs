namespace ZoneEngine.Core.Playfields.Content
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    #endregion

    /// <summary>
    /// Rome Blue / Omni city district (735 / 0x02DF) — capture-backed city NPC population.
    /// Capture 20260717-210219.
    /// </summary>
    public sealed class RomeBlueCityContentModule : IPlayfieldContentModule
    {
        private const int RomeBluePlayfieldInstance = 735;

        public bool Supports(Identity playfieldIdentity)
        {
            return playfieldIdentity.Instance == RomeBluePlayfieldInstance;
        }

        public void Register(PlayfieldContentRegistration registration)
        {
            if (registration == null || !this.Supports(registration.PlayfieldIdentity))
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "RomeBlueCityContentModule RegisterCapturedNpcSpawns pf="
                + registration.PlayfieldIdentity.Instance);
            registration.RegisterCapturedNpcSpawns();
        }

        public bool ShouldSuppressDbMobSpawn(int playfieldInstance, int mobSpawnId)
        {
            return false;
        }
    }
}
