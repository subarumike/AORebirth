namespace ZoneEngine.Core.Playfields.Content
{
    #region Usings ...

    using AORebirth.Core.Playfields;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    #endregion

    /// <summary>
    /// Nascence Dungeon 2 ACG interior (dyn PF 0x00208047) — capture-backed mobs.
    /// Capture 20260823-182854 Nascense Frontier PF 4310.
    /// </summary>
    public sealed class NascenceDungeon2ContentModule : IPlayfieldContentModule
    {
        public bool Supports(Identity playfieldIdentity)
        {
            return NascenceDungeon2Rules.IsDungeonPlayfield(playfieldIdentity.Instance);
        }

        public void Register(PlayfieldContentRegistration registration)
        {
            if (registration == null || !this.Supports(registration.PlayfieldIdentity))
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "NascenceDungeon2ContentModule RegisterCapturedNpcSpawns pf="
                + registration.PlayfieldIdentity.Instance);
            registration.RegisterCapturedNpcSpawns();
        }

        public bool ShouldSuppressDbMobSpawn(int playfieldInstance, int mobSpawnId)
        {
            return NascenceDungeon2Rules.IsDungeonPlayfield(playfieldInstance);
        }
    }
}
