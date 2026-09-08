namespace ZoneEngine_New.Core.Missions;

using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;

/// <summary>The existing MissionKeyGrantService packet order, after the owning SQL transaction.</summary>
internal static class GeneratedMissionArtifactProjection
{
    internal static void Send(Player player, Item item, int slot, bool acceptance, string name)
    {
        if (acceptance)
        {
            GameTuple<CharacterStat, uint> Stat(CharacterStat id, uint value) => new() { Value1 = id, Value2 = value };
            player.Session?.Send(new SimpleItemFullUpdateMessage
            {
                Identity = item.Identity, Unknown = 0, MsgVersion = 0x0B,
                Identitytype = (int)player.Identity.Type, Instance = player.Identity.Instance, Playfield = player.Playfield!.Identity.Instance,
                Unknown1 = new() { Type = (IdentityType)0xF424F, Instance = 0 }, Unknown2 = 0x71, Unknown3 = 0x6F,
                Name = name + '\0', Stats = [Stat(CharacterStat.Flags, item.LowId == 28577 ? 0x80000205u : 0x80000003u),
                    Stat(CharacterStat.StaticInstance, (uint)item.LowId), Stat(CharacterStat.ACGItemLevel, (uint)item.Quality),
                    Stat(CharacterStat.ACGItemTemplateID, (uint)item.LowId), Stat(CharacterStat.ACGItemTemplateID2, (uint)item.HighId),
                    Stat(CharacterStat.MultipleCount, (uint)item.StackCount)]
            });
        }
        else Template(87, new() { Type = IdentityType.OverflowWindow, Instance = 0 }, 0, 0);
        player.Session?.Send(new ContainerAddItemMessage
        {
            Identity = player.Identity, Unknown = 0, SourceContainer = new() { Type = IdentityType.OverflowWindow, Instance = 0 },
            Target = new() { Type = IdentityType.OverflowWindow, Instance = player.Identity.Instance }, TargetPlacement = 0x6F
        });
        if (acceptance)
        {
            Template(87, new() { Type = IdentityType.OverflowWindow, Instance = 0 }, 0, 0);
            Template(3, new() { Type = IdentityType.Inventory, Instance = slot }, (int)player.Identity.Type, player.Identity.Instance);
        }
        void Template(int action, Identity placement, int arg3, int arg4) => player.Session?.Send(new TemplateActionMessage
        {
            Identity = player.Identity, Unknown = 0, ItemLowId = item.LowId, ItemHighId = item.HighId, Quality = item.Quality,
            Unknown1 = 1, Unknown2 = action, Placement = placement, Unknown3 = arg3, Unknown4 = arg4
        });
    }
}
