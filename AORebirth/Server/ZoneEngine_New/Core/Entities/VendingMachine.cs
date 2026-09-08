namespace ZoneEngine_New.Core.Entities
{
    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Trade;

    using MsgQuaternion = SmokeLounge.AOtomation.Messaging.GameData.Quaternion;
    using MsgVector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;

    /// <summary>
    /// A shop. Either placed directly by Dynels.dat, or attached to an <see cref="NpcCharacter"/>
    /// that carries a shop item in its equipment (<see cref="OwnerNpc"/>).
    /// </summary>
    public sealed class VendingMachine : StaticDynel
    {
        const int FullUpdateTypeIdentifier = 0x0b;
        const int FullUpdateUnknown4 = 0xf424f;
        const int FullUpdateUnknown5 = 0;
        const short FullUpdateUnknown6 = 111;
        const int FullUpdateUnknown8 = 2;
        const int FullUpdateUnknown9 = 50;
        const int FullUpdateUnknown11 = 3;

        public VendingMachine(Identity identity, ItemTemplate template)
            : base(identity, template)
        {
        }

        public static bool IsVendingMachineType(IdentityType type)
            => type == IdentityType.VendingMachine;

        /// <summary>Shared stock for every concurrent shopper. Rolled lazily on first open.</summary>
        public ShopStock Stock { get; } = new();

        /// <summary>
        /// Set when this machine is the shop behind an NPC vendor. The NPC owns the transform and is
        /// the identity the player interacts with; the machine itself is never directly usable.
        /// </summary>
        public NpcCharacter? OwnerNpc { get; set; }

        /// <summary>Credits the machine pays per unit of item value when buying from a player.</summary>
        public int BuyModifier => Stats.GetOrZero(CharacterStat.BuyModifier);

        /// <summary>Credits the machine charges per unit of item value when selling to a player.</summary>
        public int SellModifier => Stats.GetOrZero(CharacterStat.SellModifier);

        /// <summary>The identity the client addresses this shop by: the NPC when one owns it.</summary>
        public Identity ShopIdentity => OwnerNpc != null ? OwnerNpc.Identity : Identity;

        /// <summary>
        /// An NPC-owned machine is opened through the NPC, never by targeting the machine directly.
        /// Returning false here keeps a crafted GenericCmd Use on the hidden machine from opening a
        /// shop the player cannot see.
        /// </summary>
        protected override bool OnUse(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);

            if (OwnerNpc != null || Playfield == null)
                return false;

            return Playfield.GetRequiredService<TradeService>().TryOpenShop(player, this);
        }

        public override MessageBody BuildSpawnMessage()
        {
            MsgVector3 coordinate = Position;
            MsgQuaternion heading = Rotation;
            NpcCharacter? owner = OwnerNpc;

            // World-placed machine: with no owning NPC the client reads the transform off this packet.
            // NPC-owned machine: NpcIdentity binds the pane to the character instead.
            return new VendingMachineFullUpdateMessage
            {
                Identity = Identity,
                Unknown = 0,
                TypeIdentifier = FullUpdateTypeIdentifier,
                NpcIdentity = owner != null ? owner.Identity : Identity.None,
                Coordinates = owner != null ? owner.Position : coordinate,
                Heading = owner != null ? owner.Rotation : heading,
                PlayfieldId = Playfield != null ? Playfield.Identity.Instance : 0,
                Unknown4 = FullUpdateUnknown4,
                Unknown5 = FullUpdateUnknown5,
                Unknown6 = FullUpdateUnknown6,
                Stats = BuildStats(),
                Unknown7 = (Template.Name ?? string.Empty) + "\0",
                Unknown8 = FullUpdateUnknown8,
                Unknown9 = FullUpdateUnknown9,
                Unknown10 = Array.Empty<Identity>(),
                Unknown11 = FullUpdateUnknown11
            };
        }
    }
}
