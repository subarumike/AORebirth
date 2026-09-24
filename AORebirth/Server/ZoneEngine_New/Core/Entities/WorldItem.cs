namespace ZoneEngine_New.Core.Entities
{
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Inventory;

    /// <summary>
    /// Item template spawned into the world from a hash. Sends the world-item stat set seen in retail
    /// SimpleItemFullUpdate captures instead of the full inventory template stats.
    /// </summary>
    public sealed class WorldItem : StaticDynel
    {
        const int WorldItemFlags = (int)(ItemFlags.Visible | ItemFlags.TellCollision | ItemFlags.DisableStatelCollision);

        public WorldItem(Identity identity, ItemTemplate template, int lowId, int highId, int quality)
            : base(identity, template)
        {
            LowId = lowId;
            HighId = highId;
            Quality = quality;
        }

        public int LowId { get; }

        public int HighId { get; }

        public int Quality { get; }

        /// <summary>
        /// The owner fields must be 0:0; the codec only writes Coordinate/Heading for an ownerless item,
        /// and the client has no Dynels.dat position for a runtime instance.
        /// </summary>
        public override MessageBody BuildSpawnMessage()
        {
            var message = (SimpleItemFullUpdateMessage)base.BuildSpawnMessage();
            message.Identitytype = 0;
            message.Instance = 0;
            return message;
        }

        protected override GameTuple<CharacterStat, uint>[] BuildStats()
        {
            int flags = Template.Stats.TryGetValue(CharacterStat.Flags, out int templateFlags) ? templateFlags : 0;
            return
            [
                Stat(CharacterStat.Flags, flags | WorldItemFlags),
                Stat(CharacterStat.StaticInstance, LowId),
                Stat(CharacterStat.ACGItemLevel, Quality),
                Stat(CharacterStat.ACGItemTemplateID, LowId),
                Stat(CharacterStat.ACGItemTemplateID2, HighId),
                Stat(CharacterStat.MultipleCount, 1)
            ];
        }

        static GameTuple<CharacterStat, uint> Stat(CharacterStat stat, int value)
            => new() { Value1 = stat, Value2 = (uint)value };
    }
}
