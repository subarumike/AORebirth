namespace ZoneEngine.Core.Playfields.Content
{
    #region Usings ...

    using System;

    using AORebirth.Core.Playfields;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    public sealed class PlayfieldContentRegistration
    {
        private readonly Playfield playfield;

        private readonly Identity playfieldIdentity;

        public PlayfieldContentRegistration(Playfield playfield, Identity playfieldIdentity)
        {
            this.playfield = playfield;
            this.playfieldIdentity = playfieldIdentity;
        }

        public Playfield Playfield
        {
            get
            {
                return this.playfield;
            }
        }

        public Identity PlayfieldIdentity
        {
            get
            {
                return this.playfieldIdentity;
            }
        }

        public void RegisterCapturedNpcSpawns(Action<Playfield, Identity> spawnContent)
        {
            if (spawnContent == null)
            {
                return;
            }

            spawnContent(this.playfield, this.playfieldIdentity);
        }
    }
}
