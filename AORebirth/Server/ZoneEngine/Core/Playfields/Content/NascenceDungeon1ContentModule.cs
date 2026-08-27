namespace ZoneEngine.Core.Playfields.Content
{
    #region Usings ...

    using AORebirth.Core.Playfields;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    #endregion

    /// <summary>
    /// Nascence Lord/Lady cave ACG dungeon (dyn PF 0x1F900B) — capture-backed mobs.
    /// Captures 20260823-171238 / 20260824-125307.
    /// </summary>
    public sealed class NascenceDungeon1ContentModule : IPlayfieldContentModule
    {
        public bool Supports(Identity playfieldIdentity)
        {
            return NascenceDungeon1Rules.IsDungeonPlayfield(playfieldIdentity.Instance);
        }

        public void Register(PlayfieldContentRegistration registration)
        {
            if (registration == null || !this.Supports(registration.PlayfieldIdentity))
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "NascenceDungeon1ContentModule RegisterCapturedNpcSpawns pf="
                + registration.PlayfieldIdentity.Instance);
            registration.RegisterCapturedNpcSpawns();
        }

        public bool ShouldSuppressDbMobSpawn(int playfieldInstance, int mobSpawnId)
        {
            return NascenceDungeon1Rules.IsDungeonPlayfield(playfieldInstance);
        }
    }
}
