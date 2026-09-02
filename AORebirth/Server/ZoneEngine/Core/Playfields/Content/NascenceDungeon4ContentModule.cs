namespace ZoneEngine.Core.Playfields.Content
{
    #region Usings ...

    using AORebirth.Core.Playfields;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    #endregion

    /// <summary>
    /// Nascence Dungeon 4 ACG interior (dyn PF 0x002090C1) — capture-backed mobs.
    /// Capture 20260830-143801 A Door outdoor PF 4311.
    /// </summary>
    public sealed class NascenceDungeon4ContentModule : IPlayfieldContentModule
    {
        public bool Supports(Identity playfieldIdentity)
        {
            return NascenceDungeon4Rules.IsDungeonPlayfield(playfieldIdentity.Instance);
        }

        public void Register(PlayfieldContentRegistration registration)
        {
            if (registration == null || !this.Supports(registration.PlayfieldIdentity))
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "NascenceDungeon4ContentModule RegisterCapturedNpcSpawns pf="
                + registration.PlayfieldIdentity.Instance);
            registration.RegisterCapturedNpcSpawns();
        }

        public bool ShouldSuppressDbMobSpawn(int playfieldInstance, int mobSpawnId)
        {
            return NascenceDungeon4Rules.IsDungeonPlayfield(playfieldInstance);
        }
    }
}
