namespace ZoneEngine.Core.Playfields.Content
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    #endregion

    /// <summary>
    /// Nascence Life — outdoor Shadowlands starter zones (capture-backed mob/NPC population).
    /// Playfields: 4310 Frontier, 4311 Wilds, 4312 Core/Swamp, 4313,
    /// 4001 Jobe Research (Drake), 4531 Goldman/Arete Harbor.
    /// Heckler population on 4312 remains in NascenceCoreHecklerSpawnOrchestrator.
    /// </summary>
    public sealed class NascenceLifeContentModule : IPlayfieldContentModule
    {
        internal const int FrontierPlayfieldId = 4310;

        internal const int WildsPlayfieldId = 4311;

        internal const int CorePlayfieldId = 4312;

        internal const int Nascence4313PlayfieldId = 4313;

        // Capture 20260723-221330 SCFU Scientist Drake Rodriguez on PF 4001 (Jobe Research).
        internal const int JobeResearchPlayfieldId = 4001;

        // Capture 20260723-221330 Goldman Harbor / Arete (capturePlayfieldObjectId 11B3).
        internal const int GoldmanAretePlayfieldId = 4531;

        public bool Supports(Identity playfieldIdentity)
        {
            int pf = playfieldIdentity.Instance;
            return pf == FrontierPlayfieldId
                   || pf == WildsPlayfieldId
                   || pf == CorePlayfieldId
                   || pf == Nascence4313PlayfieldId
                   || pf == JobeResearchPlayfieldId
                   || pf == GoldmanAretePlayfieldId;
        }

        public void Register(PlayfieldContentRegistration registration)
        {
            if (registration == null || !this.Supports(registration.PlayfieldIdentity))
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "NascenceLifeContentModule RegisterCapturedNpcSpawns pf="
                + registration.PlayfieldIdentity.Instance);
            registration.RegisterCapturedNpcSpawns();
        }

        public bool ShouldSuppressDbMobSpawn(int playfieldInstance, int mobSpawnId)
        {
            return false;
        }
    }
}
