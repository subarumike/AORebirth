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

        private static void LogCapturedAreteRobotContent(bool isError, string message)
        {
            LogUtil.Debug(isError ? DebugInfoDetail.Error : DebugInfoDetail.Engine, message);
        }
    }
}
