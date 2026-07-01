namespace ZoneEngine.Core.Playfields.Content
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    public sealed class PrivateCityContentModule : IPlayfieldContentModule
    {
        private const int PrivateCityPlayfieldMinInstance = 0x100000;

        private const int PrivateCityPlayfieldMaxInstance = 0x12FFFF;

        public bool Supports(Identity playfieldIdentity)
        {
            if (playfieldIdentity.Type != IdentityType.Playfield
                && playfieldIdentity.Type != IdentityType.Playfield2)
            {
                return false;
            }

            return playfieldIdentity.Instance >= PrivateCityPlayfieldMinInstance
                   && playfieldIdentity.Instance <= PrivateCityPlayfieldMaxInstance;
        }

        public void Register(PlayfieldContentRegistration registration)
        {
        }
    }
}
