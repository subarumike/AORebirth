namespace ZoneEngine.Core.Playfields.Content
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Missions;

    #endregion

    /// <summary>
    /// Dynamic RK mission instances (high id band, see <see cref="MissionInstanceService"/>).
    /// Populates any per-character mission-instance playfield with the captured "repair machine" mob set
    /// (captures 20260717-211215 + 211849). Empty-instance test slice: mobs only, no geometry/collision.
    /// </summary>
    public sealed class MissionInstanceContentModule : IPlayfieldContentModule
    {
        public bool Supports(Identity playfieldIdentity)
        {
            return MissionInstanceService.IsMissionInstancePlayfield(playfieldIdentity.Instance)
                   && !MissionAcgBindingRuntime.IsBoundLivePlayfield(
                       playfieldIdentity.Instance);
        }

        public void Register(PlayfieldContentRegistration registration)
        {
            if (registration == null || !this.Supports(registration.PlayfieldIdentity))
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "MissionInstanceContentModule RegisterCapturedNpcSpawns pf="
                + registration.PlayfieldIdentity.Instance);
            registration.RegisterCapturedNpcSpawns();
        }

        public bool ShouldSuppressDbMobSpawn(int playfieldInstance, int mobSpawnId)
        {
            return false;
        }
    }
}
