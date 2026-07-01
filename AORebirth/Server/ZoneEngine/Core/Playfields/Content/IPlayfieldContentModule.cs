namespace ZoneEngine.Core.Playfields.Content
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    public interface IPlayfieldContentModule
    {
        bool Supports(Identity playfieldIdentity);

        void Register(PlayfieldContentRegistration registration);
    }
}
