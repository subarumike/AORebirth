namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    #endregion

    /// <summary>
    /// Grants a mission key item into a character's inventory when a mission is accepted, mirroring the
    /// captured official flow (SimpleItemFullUpdate for the key followed by ContainerAddItem routing it
    /// through the overflow window into the backpack). The item-grant/persist mechanics follow the same
    /// pattern proven by the private-city guest-key terminal.
    /// </summary>
    internal static class MissionKeyGrantService
    {
        // Item identity type observed for mission keys on the wire (capture 20260717-pull-mish-doit).
        private const int MissionKeyIdentityType = 0x0000C76D;

        // Mission-key item template (ACGItemTemplateID / StaticInstance) from the same capture.
        private const int MissionKeyTemplateId = 28577;

        // Wire constants replicated from the captured SimpleItemFullUpdate for the mission key.
        private const int MissionKeyStateMachineType = 0x000F424F;

        private const byte MissionKeyUnknown2 = 0x71;

        private const byte MissionKeyOverflowSlot = 0x6F;

        // Accept-grant repair kit for RepairMachine missions. Capture 20260717-211849 finishes with
        // UseItemOnItem on Terminal:57958339, but the accept-granted template id was not on the wire
        // (container loot 101277/101581 were implants/clusters, not the tool). Use a real items.dat
        // tool template and label it on the wire as the mission repair kit.
        private const int RepairItemLowId = 87810; // Hacker Tool (ItemDb_Rest)

        private const int RepairItemHighId = 87810;

        private const int RepairItemFallbackLowId = 95576; // Bomb Disarmament Tools

        private const int RepairItemFallbackHighId = 95576;

        private const int RepairItemQuality = 1;

        private const byte RepairItemOverflowSlot = 0x70;

        private const string RepairItemDisplayName = "Mission Repair Kit";

        private const uint MissionKeyFlags = 0x80000205;

        // CharacterAction the official server sends to drop a mission key on delete (capture 20260717-185345,
        // printed as decimal Action=47). Not a named CharacterActionType, so it is applied by raw value.
        private const int MissionKeyDeleteAction = 0x2F;

        private static int missionKeyInstanceSeed =
            Math.Max(0x00F6706A, unchecked((int)(DateTime.UtcNow.Ticks & 0x3fffffff)));

        /// <summary>
        /// Creates the mission key, adds it to the standard inventory page, persists, and notifies the client.
        /// On success <paramref name="keyInstance"/> carries the granted key's item-identity instance so the
        /// caller can remember it for a later mission-delete.
        /// </summary>
        public static bool TryGrantMissionKey(
            IZoneClient client,
            ICharacter character,
            string keyName,
            out int keyInstance,
            out InventoryError inventoryError)
        {
            return TryGrantItem(
                client,
                character,
                MissionKeyTemplateId,
                MissionKeyTemplateId,
                1,
                keyName,
                MissionKeyOverflowSlot,
                out keyInstance,
                out inventoryError);
        }

        /// <summary>
        /// Grants the repair kit alongside the mission key for RepairMachine accepts.
        /// </summary>
        public static bool TryGrantRepairItem(
            IZoneClient client,
            ICharacter character,
            int quality,
            out int itemInstance,
            out InventoryError inventoryError)
        {
            int resolvedQuality = quality > 0 ? quality : RepairItemQuality;
            int lowId;
            int highId;
            if (!TryResolveRepairTemplateIds(out lowId, out highId))
            {
                itemInstance = 0;
                inventoryError = InventoryError.Invalid;
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "MissionKeyGrant repair kit templates missing from items.dat");
                return false;
            }

            return TryGrantItem(
                client,
                character,
                lowId,
                highId,
                resolvedQuality,
                RepairItemDisplayName,
                RepairItemOverflowSlot,
                out itemInstance,
                out inventoryError);
        }

        public static bool IsRepairTool(IItem item)
        {
            if (item == null)
            {
                return false;
            }

            return (item.LowID == RepairItemLowId && item.HighID == RepairItemHighId)
                   || (item.LowID == RepairItemFallbackLowId && item.HighID == RepairItemFallbackHighId);
        }

        public static bool HasRepairTool(ICharacter character)
        {
            IItem unused;
            return TryFindRepairTool(character, out unused);
        }

        /// <summary>
        /// Removes one repair kit from inventory after a successful machine repair.
        /// </summary>
        public static bool TryConsumeRepairTool(IZoneClient client, ICharacter character, IItem repairItem)
        {
            if (client == null || character == null || character.BaseInventory == null || repairItem == null
                || !IsRepairTool(repairItem))
            {
                return false;
            }

            foreach (KeyValuePair<int, IInventoryPage> pageEntry in character.BaseInventory.Pages)
            {
                foreach (KeyValuePair<int, IItem> itemEntry in pageEntry.Value.List().ToList())
                {
                    IItem item = itemEntry.Value;
                    if (item == null || !IsRepairTool(item)
                        || item.Identity == null
                        || item.Identity.Instance != repairItem.Identity.Instance)
                    {
                        continue;
                    }

                    try
                    {
                        pageEntry.Value.Remove(itemEntry.Key);
                        character.BaseInventory.Write();
                    }
                    catch
                    {
                        return false;
                    }

                    client.SendCompressed(
                        new DespawnMessage
                        {
                            Identity = item.Identity,
                            Unknown = 1
                        });
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveRepairTemplateIds(out int lowId, out int highId)
        {
            if (ItemLoader.ItemList.ContainsKey(RepairItemLowId)
                && ItemLoader.ItemList.ContainsKey(RepairItemHighId))
            {
                lowId = RepairItemLowId;
                highId = RepairItemHighId;
                return true;
            }

            if (ItemLoader.ItemList.ContainsKey(RepairItemFallbackLowId)
                && ItemLoader.ItemList.ContainsKey(RepairItemFallbackHighId))
            {
                lowId = RepairItemFallbackLowId;
                highId = RepairItemFallbackHighId;
                return true;
            }

            lowId = 0;
            highId = 0;
            return false;
        }

        private static bool TryFindRepairTool(ICharacter character, out IItem found)
        {
            found = null;
            if (character == null || character.BaseInventory == null)
            {
                return false;
            }

            foreach (KeyValuePair<int, IInventoryPage> pageEntry in character.BaseInventory.Pages)
            {
                foreach (KeyValuePair<int, IItem> itemEntry in pageEntry.Value.List().ToList())
                {
                    IItem item = itemEntry.Value;
                    if (item != null && IsRepairTool(item))
                    {
                        found = item;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryGrantItem(
            IZoneClient client,
            ICharacter character,
            int lowId,
            int highId,
            int quality,
            string itemName,
            byte overflowSlot,
            out int itemInstance,
            out InventoryError inventoryError)
        {
            itemInstance = 0;
            inventoryError = InventoryError.Invalid;

            if (client == null || character == null || character.BaseInventory == null
                || character.Playfield == null)
            {
                return false;
            }

            IInventoryPage inventoryPage;
            if (!character.BaseInventory.Pages.TryGetValue(character.BaseInventory.StandardPage, out inventoryPage))
            {
                return false;
            }

            int inventorySlot = inventoryPage.FindFreeSlot();
            if (inventorySlot == -1)
            {
                inventoryError = InventoryError.InventoryIsFull;
                return false;
            }

            // A missing template makes Item's constructor throw. Detect it up-front so the failure is logged
            // with the offending id instead of being silently swallowed (that is why the repair tool never
            // appeared: its template is not present in items.dat).
            if (!ItemLoader.ItemList.ContainsKey(lowId) || !ItemLoader.ItemList.ContainsKey(highId))
            {
                inventoryError = InventoryError.Invalid;
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "MissionKeyGrant missing item template '" + itemName + "' low=" + lowId + " high=" + highId);
                return false;
            }

            Item grantedItem;
            try
            {
                grantedItem = CreateItem(lowId, highId, quality);
            }
            catch (Exception ex)
            {
                inventoryError = InventoryError.Invalid;
                LogUtil.ErrorException(ex);
                return false;
            }

            inventoryError = inventoryPage.Add(inventorySlot, grantedItem);
            if (inventoryError != InventoryError.OK)
            {
                return false;
            }

            try
            {
                if (!character.BaseInventory.Write())
                {
                    TryRemoveInventorySlot(inventoryPage, inventorySlot);
                    inventoryError = InventoryError.Invalid;
                    return false;
                }
            }
            catch
            {
                TryRemoveInventorySlot(inventoryPage, inventorySlot);
                inventoryError = InventoryError.Invalid;
                return false;
            }

            itemInstance = grantedItem.Identity.Instance;

            client.SendCompressed(
                CreateItemMessage(character, grantedItem.Identity, itemName, overflowSlot, lowId, highId, quality));
            client.SendCompressed(
                new ContainerAddItemMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    SourceContainer = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                    Target = new Identity { Type = IdentityType.OverflowWindow, Instance = character.Identity.Instance },
                    TargetPlacement = overflowSlot
                });

            return true;
        }

        /// <summary>
        /// Returns true if the character currently holds a mission key item in any inventory page. Used to
        /// decide whether the accepted-mission window should be re-sent on zone-in (the key persists in the
        /// inventory but the client's mission window is cleared on every zone).
        /// </summary>
        public static bool HasMissionKey(ICharacter character)
        {
            if (character == null || character.BaseInventory == null)
            {
                return false;
            }

            foreach (KeyValuePair<int, IInventoryPage> pageEntry in character.BaseInventory.Pages)
            {
                foreach (KeyValuePair<int, IItem> itemEntry in pageEntry.Value.List().ToList())
                {
                    IItem item = itemEntry.Value;
                    // Match by template ids only — after DB reload the wire identity type is not always
                    // preserved, and requiring MissionKeyIdentityType made zone resync skip the journal.
                    if (item != null && item.LowID == MissionKeyTemplateId && item.HighID == MissionKeyTemplateId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Destroys a previously granted mission key: removes it from the character's inventory and tells the
        /// client to drop it. Mirrors the captured official mission-delete teardown (capture 20260717-185345):
        /// a CharacterAction(0x2F) that references the key by its own item identity followed by a Despawn of
        /// that identity. The Despawn is the location-independent teardown; the CharacterAction matches the
        /// captured sequence.
        /// </summary>
        public static bool TryRemoveMissionKey(IZoneClient client, ICharacter character, int keyInstance)
        {
            if (client == null || character == null || character.BaseInventory == null)
            {
                return false;
            }

            var keyIdentity = new Identity { Type = (IdentityType)MissionKeyIdentityType, Instance = keyInstance };

            bool removedFromInventory = false;
            foreach (KeyValuePair<int, IInventoryPage> pageEntry in character.BaseInventory.Pages)
            {
                foreach (KeyValuePair<int, IItem> itemEntry in pageEntry.Value.List().ToList())
                {
                    IItem item = itemEntry.Value;
                    if (item == null || item.Identity == null || item.LowID != MissionKeyTemplateId
                        || item.HighID != MissionKeyTemplateId
                        || item.Identity.Type != (IdentityType)MissionKeyIdentityType
                        || item.Identity.Instance != keyInstance)
                    {
                        continue;
                    }

                    try
                    {
                        pageEntry.Value.Remove(itemEntry.Key);
                        character.BaseInventory.Write();
                        removedFromInventory = true;
                    }
                    catch
                    {
                    }

                    break;
                }

                if (removedFromInventory)
                {
                    break;
                }
            }

            client.SendCompressed(
                new CharacterActionMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    Action = (CharacterActionType)MissionKeyDeleteAction,
                    Unknown1 = 0,
                    Target = keyIdentity,
                    Parameter1 = 1,
                    Parameter2 = 0,
                    Unknown2 = 0
                });
            client.SendCompressed(
                new DespawnMessage { Identity = keyIdentity, Unknown = 1 });

            return removedFromInventory;
        }

        private static Item CreateItem(int lowId, int highId, int quality)
        {
            var item = new Item(quality, lowId, highId)
                       {
                           Identity =
                               new Identity
                               {
                                   Type = (IdentityType)MissionKeyIdentityType,
                                   Instance = CreateMissionKeyInstance()
                               },
                           Flags = 1
                       };

            foreach (GameTuple<CharacterStat, uint> stat in CreateItemStats(lowId, highId, quality))
            {
                item.SetAttribute((int)stat.Value1, unchecked((int)stat.Value2));
            }

            item.MultipleCount = 1;
            return item;
        }

        private static int CreateMissionKeyInstance()
        {
            int instance = Interlocked.Increment(ref missionKeyInstanceSeed) & 0x7fffffff;
            return instance == 0 ? CreateMissionKeyInstance() : instance;
        }

        private static SimpleItemFullUpdateMessage CreateItemMessage(
            ICharacter character,
            Identity itemIdentity,
            string itemName,
            byte overflowSlot,
            int lowId,
            int highId,
            int quality)
        {
            return new SimpleItemFullUpdateMessage
                   {
                       Identity = new Identity { Type = itemIdentity.Type, Instance = itemIdentity.Instance },
                       Unknown = 0,
                       MsgVersion = 0x0B,
                       Identitytype = (int)character.Identity.Type,
                       Instance = character.Identity.Instance,
                       Playfield = character.Playfield.Identity.Instance,
                       Unknown1 = new Identity { Type = (IdentityType)MissionKeyStateMachineType, Instance = 0 },
                       Unknown2 = MissionKeyUnknown2,
                       Unknown3 = overflowSlot,
                       Stats = CreateItemStats(lowId, highId, quality),
                       Name = TerminatedName(itemName)
                   };
        }

        /// <summary>
        /// Length-prefixed item names on the wire include a trailing null in their Int32 length count (the
        /// same convention as QuestInfo.Info). The serializer writes the raw string length, so we must append
        /// the terminator explicitly; otherwise the client keeps reading adjacent bytes past the name and the
        /// tooltip shows trailing garbage (e.g. "Mission key øâ6").
        /// </summary>
        private static string TerminatedName(string keyName)
        {
            if (string.IsNullOrEmpty(keyName))
            {
                return string.Empty;
            }

            return keyName + '\0';
        }

        private static GameTuple<CharacterStat, uint>[] CreateItemStats(int lowId, int highId, int quality)
        {
            return new[]
                   {
                       MissionKeyStat(CharacterStat.Flags, MissionKeyFlags),
                       MissionKeyStat(CharacterStat.StaticInstance, lowId),
                       MissionKeyStat(CharacterStat.ACGItemLevel, quality),
                       MissionKeyStat(CharacterStat.ACGItemTemplateID, lowId),
                       MissionKeyStat(CharacterStat.ACGItemTemplateID2, highId),
                       MissionKeyStat(CharacterStat.MultipleCount, 1)
                   };
        }

        private static GameTuple<CharacterStat, uint> MissionKeyStat(CharacterStat stat, int value)
        {
            return MissionKeyStat(stat, unchecked((uint)value));
        }

        private static GameTuple<CharacterStat, uint> MissionKeyStat(CharacterStat stat, uint value)
        {
            return new GameTuple<CharacterStat, uint> { Value1 = stat, Value2 = value };
        }

        private static void TryRemoveInventorySlot(IInventoryPage inventoryPage, int inventorySlot)
        {
            try
            {
                if (inventoryPage != null && inventoryPage[inventorySlot] != null)
                {
                    inventoryPage.Remove(inventorySlot);
                }
            }
            catch
            {
            }
        }
    }
}
