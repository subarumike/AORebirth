namespace ZoneEngine.Core.Playfields.Content
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Missions;

    #endregion

    /// <summary>
    /// Dynamic RK mission instances (high id band, see <see cref="MissionInstanceService"/>).
    /// Bound ACG playfields must enter the same captured-NPC registration hook: the NPC runtime
    /// selects Stage 5 operational materialization for a bound PF2 and retains the legacy spawn
    /// path only for an unbound mission instance.
    /// </summary>
    public sealed class MissionInstanceContentModule : IPlayfieldContentModule
    {
        public bool Supports(Identity playfieldIdentity)
        {
            return MissionInstanceService.IsMissionInstancePlayfield(playfieldIdentity.Instance);
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
