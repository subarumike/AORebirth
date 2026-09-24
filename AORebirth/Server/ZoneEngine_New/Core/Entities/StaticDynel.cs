namespace ZoneEngine_New.Core.Entities
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.WorldSimulation;

    using MsgQuaternion = SmokeLounge.AOtomation.Messaging.GameData.Quaternion;
    using MsgVector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;
    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

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

            if (GetEdgeDistanceTo(player) > LootableDynel.OpenRange)
                return false;

            // TeleportProxy2 terminals (Grid enter): combat must not block.
            // Clear fight for client isfightingme; skip ToUse criteria that include it.
            bool gridEnter = GridEnterTerminal.IsGridEnter(Template);
            if (gridEnter)
            {
                GridEnterTerminal.ClearCombatForEntry(player);
                return OnUse(player);
            }

            if (!Template.MeetsActionRequirements(
                    stat => player.Stats.GetOrZero(stat),
                    ActionType.ToUse))
                return false;

            return OnUse(player);
        }

        protected virtual bool OnUse(Player player)
        {
            if (Playfield == null || player.Session == null)
                return false;

            IGameData gameData = Playfield.GetRequiredService<IGameData>();
            if (gameData.TryGetCapturedGridEnter(
                    Playfield.Identity.Instance,
                    Identity.Instance,
                    out CapturedGridEnterLanding landing))
            {
                Vector3 position = landing.PositionFallback;
                Quaternion heading = landing.HeadingFallback;
                if (landing.ExitTerminalInstance != 0
                    && PortalDoorLandingResolver.TryResolveProxyLanding(
                        gameData.GetPlayfieldGeometry(landing.PlayfieldId),
                        landing.ExitTerminalInstance,
                        PortalDoorLandingResolver.Proxy2EntryDoorClearance,
                        out Vector3 resolved,
                        out Quaternion resolvedHeading))
                {
                    position = resolved;
                    heading = resolvedHeading;
                }

                player.Stats.Set(CharacterStat.ExternalPlayfieldInstance, 0, StatDetail.Base, dirty: true);
                player.Stats.Set(CharacterStat.ExternalDoorInstance, 0, StatDetail.Base, dirty: true);
                player.Rotation = heading;
                Playfield dest = Playfield.GetRequiredService<PlayfieldManager>()
                    .GetOrCreate(landing.PlayfieldId);
                player.Session.TransferToPlayfield(dest, position, heading);
                return true;
            }

            return Template.ExecuteOnUseSpells(
                player,
                Playfield.GetRequiredService<IInventoryRepository>(),
                Playfield.GetRequiredService<IItemBuilder>());
        }

        public override MessageBody BuildSpawnMessage()
        {
            MsgVector3 coordinate = Position;
            MsgQuaternion heading = Rotation;

            // World-placed statics: Identitytype/Instance are the *owner* slot, not the dynel id.
            // Legacy leaves them at 0. Non-zero Identitytype (e.g. Terminal=51005) makes the
            // AoFlags gate omit Coordinate/Heading, so the client never places the mesh.
            var message = new SimpleItemFullUpdateMessage
            {
                Identity = Identity,
                Unknown = 0,
                MsgVersion = SimpleItemFullUpdateMsgVersion,
                Identitytype = 0,
                Instance = 0,
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
                Name = string.Empty
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
