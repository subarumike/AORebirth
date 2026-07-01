namespace ZoneEngine.Core.Playfields.Content
{
    #region Usings ...

    using AORebirth.Core.Playfields;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    public sealed class PlayfieldContentCoordinator
    {
        private readonly IPlayfieldContentModule[] modules;

        public PlayfieldContentCoordinator(params IPlayfieldContentModule[] modules)
        {
            this.modules = modules ?? new IPlayfieldContentModule[0];
        }

        public void RegisterContent(Playfield playfield, Identity playfieldIdentity)
        {
            var registration = new PlayfieldContentRegistration(playfield, playfieldIdentity);
            foreach (IPlayfieldContentModule module in this.modules)
            {
                if (!module.Supports(playfieldIdentity))
                {
                    continue;
                }

                module.Register(registration);
            }
        }
    }
}
