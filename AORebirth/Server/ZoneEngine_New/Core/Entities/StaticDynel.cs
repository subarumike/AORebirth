namespace ZoneEngine_New.Core.Entities
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Inventory;

    using MsgQuaternion = SmokeLounge.AOtomation.Messaging.GameData.Quaternion;
    using MsgVector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;

    /// <summary>
    /// Playfield-placed world object backed by a reference-only interpolated item template.
    /// </summary>
    public abstract class StaticDynel : Dynel, IUsableDynel
    {
        const int SimpleItemFullUpdateUnknown1Type = 1000015;
        const byte SimpleItemFullUpdateUnknown3 = 0x6F;
        const int SimpleItemFullUpdateMsgVersion = 0x0b;

        protected StaticDynel(Identity identity, ItemTemplate template)
            : base(identity)
        {
            ArgumentNullException.ThrowIfNull(template);
            Template = template;
            ApplyTemplateStats();
        }

        public ItemTemplate Template { get; }

        public bool TryUse(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);

            if (player.Session == null || Playfield == null)
                return false;

            if (player.Playfield == null
                || player.Playfield.Identity.Instance != Playfield.Identity.Instance)
                return false;

            if (Distance3D(player) > LootableDynel.OpenRange)
                return false;

            if (!Template.MeetsActionRequirements(stat => player.Stats.Get(stat), ActionType.ToUse))
                return false;

            return OnUse(player);
        }

        protected virtual bool OnUse(Player player)
        {
            if (player.Session == null)
                return false;

            player.Session.Send(
                new ChatTextMessage
                {
                    Identity = player.Identity,
                    Text = "Not Implemented.",
                    Unknown1 = 0,
                    Unknown2 = 0,
                    Unknown3 = 0
                });
            return true;
        }

        public override MessageBody BuildSpawnMessage()
        {
            MsgVector3 coordinate = Position;
            MsgQuaternion heading = Rotation;

            var message = new SimpleItemFullUpdateMessage
            {
                Identity = Identity,
                Unknown = 0,
                MsgVersion = SimpleItemFullUpdateMsgVersion,
                Identitytype = (int)Identity.Type,
                Instance = Identity.Instance,
                Coordinate = coordinate,
                Heading = heading,
                Playfield = Playfield != null ? Playfield.Identity.Instance : 0,
                Unknown1 = new Identity
                {
                    Type = (IdentityType)SimpleItemFullUpdateUnknown1Type,
                    Instance = 0
                },
                Unknown2 = 0,
                Unknown3 = SimpleItemFullUpdateUnknown3,
                Stats = BuildStats(),
                Name = Template.Name ?? string.Empty
            };
            message.Owner = Identity.None;
            return message;
        }

        void ApplyTemplateStats()
        {
            foreach (KeyValuePair<CharacterStat, int> pair in Template.Stats)
                Stats.Set(pair.Key, pair.Value);

            Stats.Set(CharacterStat.ACGItemTemplateID, Template.Id);
            Stats.Set(CharacterStat.ACGItemTemplateID2, Template.Id);
            Stats.Set(CharacterStat.StaticInstance, Template.Id);
        }

        GameTuple<CharacterStat, uint>[] BuildStats()
        {
            var stats = new List<GameTuple<CharacterStat, uint>>();
            foreach ((CharacterStat stat, int _, int _, int full) in Stats.GetEntries())
            {
                stats.Add(
                    new GameTuple<CharacterStat, uint>
                    {
                        Value1 = stat,
                        Value2 = (uint)full
                    });
            }

            return stats.ToArray();
        }
    }

    /// <summary>Default Dynels.dat static: runs template OnUse spells.</summary>
    public sealed class PlayfieldStaticDynel : StaticDynel
    {
        public PlayfieldStaticDynel(Identity identity, ItemTemplate template)
            : base(identity, template)
        {
        }

        protected override bool OnUse(Player player)
        {
            if (Playfield != null
                && Item.ExecuteOnUseSpells(
                    Template,
                    player,
                    Playfield.GetRequiredService<IInventoryRepository>(),
                    Playfield.GetRequiredService<IItemBuilder>()))
                return true;

            return base.OnUse(player);
        }
    }
}
