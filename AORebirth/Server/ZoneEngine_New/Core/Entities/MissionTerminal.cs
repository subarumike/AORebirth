namespace ZoneEngine_New.Core.Entities
{
    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Inventory;

    /// <summary>Playfield mission terminal. Offer/roll UI is not implemented yet.</summary>
    public sealed class MissionTerminal : StaticDynel
    {
        public const int LiveIdentityType = 0x0000DAC1;

        public MissionTerminal(Identity identity, ItemTemplate template)
            : base(identity, template)
        {
        }

        public static bool IsMissionTerminalType(IdentityType type)
            => type == IdentityType.MissionTerminal || (int)type == LiveIdentityType;
    }
}
