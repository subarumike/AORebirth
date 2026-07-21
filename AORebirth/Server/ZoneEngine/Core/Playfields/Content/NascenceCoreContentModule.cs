namespace ZoneEngine.Core.Playfields.Content
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    /// <summary>
    /// Nascence Core (4312) Heckler registration is owned by NascenceLifeContentModule.
    /// This module is retained as a no-op so existing references stay compile-safe;
    /// Heckler spawn still runs from NascenceCoreHecklerSpawnOrchestrator inside
    /// SpawnCapturedNpcContent.
    /// </summary>
    public sealed class NascenceCoreContentModule : IPlayfieldContentModule
    {
        public bool Supports(Identity playfieldIdentity)
        {
            // NascenceLifeContentModule owns RegisterCapturedNpcSpawns for 4312.
            return false;
        }

        public void Register(PlayfieldContentRegistration registration)
        {
        }

        public bool ShouldSuppressDbMobSpawn(int playfieldInstance, int mobSpawnId)
        {
            return false;
        }
    }
}
