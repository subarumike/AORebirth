namespace AORebirth.Core.Entities
{
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    public class PlayerCharacter : Character
    {
        public PlayerCharacter(Identity parent, Identity identity, IController controller)
            : base(parent, identity, controller)
        {
        }
    }
}
