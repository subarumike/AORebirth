namespace ZoneEngine.Core.Playfields.Content
{
    #region Usings ...

    using AORebirth.Core.Playfields;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    #endregion

    /// <summary>
    /// Nascence Dungeon 3 ACG interior (dyn PF 0x00209103) — capture-backed mobs.
    /// Capture 20260830-140240 Collapsed Temple outdoor PF 4311.
    /// </summary>
    public sealed class NascenceDungeon3ContentModule : IPlayfieldContentModule
    {
        public bool Supports(Identity playfieldIdentity)
        {
            return NascenceDungeon3Rules.IsDungeonPlayfield(playfieldIdentity.Instance);
        }

        public void Register(PlayfieldContentRegistration registration)
        {
            if (registration == null || !this.Supports(registration.PlayfieldIdentity))
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "NascenceDungeon3ContentModule RegisterCapturedNpcSpawns pf="
                + registration.PlayfieldIdentity.Instance);
            registration.RegisterCapturedNpcSpawns();
        }

        public bool ShouldSuppressDbMobSpawn(int playfieldInstance, int mobSpawnId)
        {
            return NascenceDungeon3Rules.IsDungeonPlayfield(playfieldInstance);
        }
    }
}
