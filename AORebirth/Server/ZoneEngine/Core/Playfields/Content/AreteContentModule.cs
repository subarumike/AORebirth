namespace ZoneEngine.Core.Playfields.Content
{
    #region Usings ...

    using AORebirth.Core.Playfields;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Playfields;

    #endregion

    public sealed class AreteContentModule : IPlayfieldContentModule
    {
        private const int PrivateAretePlayfieldInstance = 6553;

        private static readonly CapturedAreteRobotContentProvider CapturedAreteRobotContent =
            new CapturedAreteRobotContentProvider(LogCapturedAreteRobotContent);

        private static readonly NpcPatrolReplayCoordinator NpcPatrolReplay =
            new NpcPatrolReplayCoordinator(CapturedAreteRobotContent);

        private static readonly CapturedAreteRobotSpawnOrchestrator CapturedAreteRobotSpawns =
            new CapturedAreteRobotSpawnOrchestrator(CapturedAreteRobotContent, NpcPatrolReplay);

        public bool Supports(Identity playfieldIdentity)
        {
            return playfieldIdentity.Instance == PrivateAretePlayfieldInstance;
        }

        public void Register(PlayfieldContentRegistration registration)
        {
            if (registration == null || !this.Supports(registration.PlayfieldIdentity))
            {
                return;
            }

            registration.RegisterCapturedNpcSpawns(CapturedAreteRobotSpawns.SpawnForPlayfield);
        }

        public bool ShouldSuppressDbMobSpawn(int playfieldInstance, int mobSpawnId)
        {
            if (playfieldInstance != PrivateAretePlayfieldInstance)
            {
                return false;
            }

            switch (mobSpawnId)
            {
                case 2027138231:
                case 2027138245:
                case 2027138246:
                case 2027138249:
                case 2027138259:
                    return true;
                default:
                    return false;
            }
        }

        private static void LogCapturedAreteRobotContent(bool isError, string message)
        {
            LogUtil.Debug(isError ? DebugInfoDetail.Error : DebugInfoDetail.Engine, message);
        }
    }
}
