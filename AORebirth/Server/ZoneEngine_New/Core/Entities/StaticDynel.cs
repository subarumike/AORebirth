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

        /// <summary>
        /// Template OnUse runs after the template's AttackDelay, like an inventory use, through the
        /// playfield's <see cref="ItemUseService"/>. <see cref="DelaysUse"/> false runs at once.
        /// </summary>
        public bool TryUse(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);

            ItemUseService? uses = DelaysUse ? Playfield?.GetService<ItemUseService>() : null;
            if (uses != null)
                return uses.TryBegin(player, this) != ItemUseStart.Rejected;

            return CanBeginUse(player) && ExecuteUse(player);
        }

        /// <summary>False for uses that open a UI rather than run template spells.</summary>
        protected virtual bool DelaysUse => true;

        /// <summary>Gates checked when a use starts and again when a delayed use completes.</summary>
        public bool CanBeginUse(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);

            if (player.Session == null || Playfield == null)
                return false;

            if (player.Playfield == null
                || player.Playfield.Identity.Instance != Playfield.Identity.Instance)
                return false;

            if (GetEdgeDistanceTo(player) > LootableDynel.OpenRange)
                return false;

            return Template.MeetsActionRequirements(stat => player.Stats.Get(stat), ActionType.ToUse);
        }

        /// <summary>Runs the use. Callers gate with <see cref="CanBeginUse"/>.</summary>
        public bool ExecuteUse(Player player) => OnUse(player);

        protected virtual bool OnUse(Player player)
        {
            if (Playfield == null || player.Session == null)
                return false;

            return Template.ExecuteOnUseSpells(
                player,
                Playfield.GetRequiredService<IInventoryRepository>(),
                Playfield.GetRequiredService<IItemBuilder>());
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
                // Client displays this as a C string. The length must include the trailing NUL,
                // matching mission-key SimpleItemFullUpdate. Empty stays length 0.
                Name = TerminateClientName(Template.Name)
            };
            message.Owner = Identity.None;
            return message;
        }

        static string TerminateClientName(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            return name[^1] == '\0' ? name : name + '\0';
        }

        void ApplyTemplateStats()
        {
            foreach (KeyValuePair<CharacterStat, int> pair in Template.Stats)
                Stats.Set(pair.Key, pair.Value);

            Stats.Set(CharacterStat.ACGItemTemplateID, Template.Id);
            Stats.Set(CharacterStat.ACGItemTemplateID2, Template.Id);
            Stats.Set(CharacterStat.StaticInstance, Template.Id);
        }

        protected virtual GameTuple<CharacterStat, uint>[] BuildStats()
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
    }
}
