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

    using ZoneEngine.Core.MessageHandlers;

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

        // Repair kit identity type from capture 20260724-repaair-machine-mish (SimpleItemFullUpdate /
        // FullCharacter inventory row): IdentityType.Terminal = 0xC73D, NOT MissionKey.
        private const int RepairKitIdentityType = 0x0000C73D;

        // Mission-key item template (ACGItemTemplateID / StaticInstance) from the same capture.
        private const int MissionKeyTemplateId = 28577;

        // Wire constants replicated from the captured SimpleItemFullUpdate for the mission key.
        private const int MissionKeyStateMachineType = 0x000F424F;

        private const byte MissionKeyUnknown2 = 0x71;

        private const byte MissionKeyOverflowSlot = 0x6F;

        // Accept-grant repair kits (capture 20260724-134055): StaticInstance varies per accept among
        // 100292 / 100299 / 100344 / 100349 / 100361, ACGItemLevel=1, Flags=0x80000003.
        // Fall back to Hacker Tool / Bomb tools when none of those templates exist in items.dat.
        private static readonly int[] RepairItemCaptureIds =
            {
                100292, 100299, 100344, 100349, 100361
            };

        private const int RepairItemHackerLowId = 87810;

        private const int RepairItemHackerHighId = 87814;

        private const int RepairItemFallbackLowId = 95576; // Bomb Disarmament Tools

        private const int RepairItemFallbackHighId = 95576;

        private const int RepairItemQuality = 1;

        private const string RepairItemDisplayName = "Mission Repair Kit";

        private const uint MissionKeyFlags = 0x80000205;

        // Capture Flags for the accept-granted repair kit (signed -2147483645 on the wire).
        private const uint RepairItemFlags = 0x80000003;

        // Finish capture 20260725-185432: Action=47 (0x2F), Parameter1=1, Despawn Unknown=0.
        // (Older journal-delete capture used Parameter1=0x71; finish is authoritative for mish complete.)
        private const int MissionKeyDeleteAction = 0x2F;

        private const int MissionKeyDeleteParameter1 = 1;

        private static int missionKeyInstanceSeed =
            Math.Max(0x00F6706A, unchecked((int)(DateTime.UtcNow.Ticks & 0x3fffffff)));

        private static readonly object RepairPickSync = new object();

        private static readonly Random RepairPickRng = new Random();

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
                MissionKeyFlags,
                MissionKeyIdentityType,
                false,
                0,
                out keyInstance,
                out inventoryError);
        }

        public static bool TryGrantReservedMissionKey(
            IZoneClient client,
            ICharacter character,
            int reservedKeyInstance,
            string keyName,
            out InventoryError inventoryError)
        {
            int actualInstance;
            bool granted = TryGrantItem(
                client,
                character,
                MissionKeyTemplateId,
                MissionKeyTemplateId,
                1,
                keyName,
                MissionKeyOverflowSlot,
                MissionKeyFlags,
                MissionKeyIdentityType,
                false,
                reservedKeyInstance,
                out actualInstance,
                out inventoryError);
            return granted && actualInstance == reservedKeyInstance;
        }

        /// <summary>
        /// Grants the repair kit alongside the mission key for RepairMachine accepts.
        /// Always QL 1 (capture); mission QL must not be applied to the kit template.
        /// Wire matches capture 20260724-134055: Terminal identity, Unknown2/3=0x71/0x6F,
        /// ContainerAdd Overflow→Overflow Slot=111 — same path as the mission key (kit first).
        /// </summary>
        public static bool TryGrantRepairItem(
            IZoneClient client,
            ICharacter character,
            int quality,
            out int itemInstance,
            out InventoryError inventoryError)
        {
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

            // Capture kit is always QL 1; ignore mission QL so high-QL accepts do not reject the template.
            return TryGrantItem(
                client,
                character,
                lowId,
                highId,
                RepairItemQuality,
                RepairItemDisplayName,
                MissionKeyOverflowSlot,
                RepairItemFlags,
                RepairKitIdentityType,
                false,
                0,
                out itemInstance,
                out inventoryError);
        }

        /// <summary>
        /// Grants an arbitrary named item (FindItem cube pickup / finish reward) into inventory.
        /// Finish reward uses gold 20260725-185432 wire: TemplateAction → ContainerAddItem,
        /// TargetPlacement=0x6F — CreateItem + second TemplateAction defer bag UI until zone.
        /// </summary>
        public static bool TryGrantNamedItem(
            IZoneClient client,
            ICharacter character,
            int lowId,
            int highId,
            int quality,
            string itemName,
            out int itemInstance,
            out InventoryError inventoryError)
        {
            return TryGrantItem(
                client,
                character,
                lowId,
                highId,
                quality > 0 ? quality : 1,
                string.IsNullOrEmpty(itemName) ? "Mission Item" : itemName,
                MissionKeyOverflowSlot,
                MissionKeyFlags,
                MissionKeyIdentityType,
                true,
                0,
                out itemInstance,
                out inventoryError);
        }

        public static bool IsRepairTool(IItem item)
        {
            if (item == null)
            {
                return false;
            }

            for (int i = 0; i < RepairItemCaptureIds.Length; i++)
            {
                int captureId = RepairItemCaptureIds[i];
                if (item.LowID == captureId && item.HighID == captureId)
                {
                    return true;
                }
            }

            // Accept Hacker Tool pair / same-id endpoints (older grants).
            if (item.LowID == RepairItemHackerLowId
                && (item.HighID == RepairItemHackerHighId || item.HighID == RepairItemHackerLowId))
            {
                return true;
            }

            return item.LowID == RepairItemFallbackLowId && item.HighID == RepairItemFallbackHighId;
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
            // Prefer a random capture kit AOID present in items.dat (official varies per accept).
            var available = new List<int>();
            for (int i = 0; i < RepairItemCaptureIds.Length; i++)
            {
                int captureId = RepairItemCaptureIds[i];
                if (ItemLoader.ItemList.ContainsKey(captureId))
                {
                    available.Add(captureId);
                }
            }

            if (available.Count > 0)
            {
                int pick;
                lock (RepairPickSync)
                {
                    pick = available[RepairPickRng.Next(available.Count)];
                }

                lowId = pick;
                highId = pick;
                return true;
            }

            if (ItemLoader.ItemList.ContainsKey(RepairItemHackerLowId)
                && ItemLoader.ItemList.ContainsKey(RepairItemHackerHighId))
            {
                lowId = RepairItemHackerLowId;
                highId = RepairItemHackerHighId;
                return true;
            }

            if (ItemLoader.ItemList.ContainsKey(RepairItemHackerLowId))
            {
                lowId = RepairItemHackerLowId;
                highId = RepairItemHackerLowId;
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
            uint itemFlags,
            int itemIdentityType,
            bool finishRewardWire,
            int reservedItemInstance,
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
                grantedItem = CreateItem(
                    lowId,
                    highId,
                    quality,
                    itemFlags,
                    itemIdentityType,
                    reservedItemInstance);
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

            // Capture accept (20260724-134055): CreateItem → ContainerAdd → TemplateActions.
            // Finish gold 20260725-185432: TemplateAction → ContainerAddItem only (no CreateItem).
            MissionDiagnostics.Log(
                "GRANT-ITEM name={0} low={1} high={2} ql={3} idType=0x{4:X} instance={5} invSlot={6} overflow={7} finishWire={8}",
                itemName,
                lowId,
                highId,
                quality,
                itemIdentityType,
                itemInstance,
                inventorySlot,
                overflowSlot,
                finishRewardWire);

            if (!finishRewardWire)
            {
                client.SendCompressed(
                    CreateItemMessage(
                        character,
                        grantedItem.Identity,
                        itemName,
                        MissionKeyUnknown2,
                        overflowSlot,
                        lowId,
                        highId,
                        quality,
                        itemFlags));
            }

            if (finishRewardWire)
            {
                client.SendCompressed(
                    new TemplateActionMessage
                    {
                        Identity = character.Identity,
                        Unknown = 0,
                        ItemLowId = lowId,
                        ItemHighId = highId,
                        Quality = quality,
                        Unknown1 = 1,
                        Unknown2 = 87,
                        Placement = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                        Unknown3 = 0,
                        Unknown4 = 0
                    });
            }

            client.SendCompressed(
                new ContainerAddItemMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    SourceContainer = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                    Target = new Identity
                             {
                                 Type = IdentityType.OverflowWindow,
                                 Instance = character.Identity.Instance
                             },
                    TargetPlacement = overflowSlot
                });

            if (!finishRewardWire)
            {
                client.SendCompressed(
                    new TemplateActionMessage
                    {
                        Identity = character.Identity,
                        Unknown = 0,
                        ItemLowId = lowId,
                        ItemHighId = highId,
                        Quality = quality,
                        Unknown1 = 1,
                        Unknown2 = 87,
                        Placement = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                        Unknown3 = 0,
                        Unknown4 = 0
                    });
                client.SendCompressed(
                    new TemplateActionMessage
                    {
                        Identity = character.Identity,
                        Unknown = 0,
                        ItemLowId = lowId,
                        ItemHighId = highId,
                        Quality = quality,
                        Unknown1 = 1,
                        Unknown2 = 3,
                        Placement = new Identity { Type = IdentityType.Inventory, Instance = inventorySlot },
                        Unknown3 = 50000,
                        Unknown4 = character.Identity.Instance
                    });
            }

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

        public static bool HasMissionKeyInstance(ICharacter character, int keyInstance)
        {
            if (character == null || character.BaseInventory == null || keyInstance == 0)
            {
                return false;
            }

            foreach (KeyValuePair<int, IInventoryPage> pageEntry in character.BaseInventory.Pages)
            {
                foreach (KeyValuePair<int, IItem> itemEntry in pageEntry.Value.List())
                {
                    IItem item = itemEntry.Value;
                    if (item != null
                        && item.Identity != null
                        && item.LowID == MissionKeyTemplateId
                        && item.HighID == MissionKeyTemplateId
                        && item.Identity.Instance == keyInstance)
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

            Identity keyIdentity = new Identity
                                   {
                                       Type = (IdentityType)MissionKeyIdentityType,
                                       Instance = keyInstance
                                   };

            bool removedFromInventory = false;
            int inventoryPageType = 0;
            int inventorySlot = -1;
            foreach (KeyValuePair<int, IInventoryPage> pageEntry in character.BaseInventory.Pages)
            {
                foreach (KeyValuePair<int, IItem> itemEntry in pageEntry.Value.List().ToList())
                {
                    IItem item = itemEntry.Value;
                    // Match template + instance only. After DB reload the wire IdentityType is often
                    // not MissionKey (0xC76D), which left orphan keys in the bag on journal delete.
                    if (item == null || item.Identity == null || item.LowID != MissionKeyTemplateId
                        || item.HighID != MissionKeyTemplateId
                        || item.Identity.Instance != keyInstance)
                    {
                        continue;
                    }

                    // Keep constructed MissionKey type (0xC76D). Bag IdentityType can drift
                    // after reload and then CA 0x2F/Despawn is ignored until next zone FullCharacter.
                    if (item.Identity.Instance != 0)
                    {
                        keyIdentity = new Identity
                                      {
                                          Type = (IdentityType)MissionKeyIdentityType,
                                          Instance = item.Identity.Instance
                                      };
                    }

                    inventoryPageType = pageEntry.Key;
                    inventorySlot = itemEntry.Key;

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

            if (!removedFromInventory)
            {
                // Still notify client with the stored instance so a ghost icon can clear.
                NotifyMissionItemDeleted(client, character, keyIdentity);
                return false;
            }

            // Capture finish: CA 0x2F + Despawn. Also SendDeleteItem for the bag slot —
            // without it the key icon stays until the next zone FullCharacter rebuild.
            if (inventorySlot >= 0)
            {
                CharacterActionMessageHandler.Default.SendDeleteItem(
                    character,
                    inventoryPageType,
                    inventorySlot);
            }

            MissionDiagnostics.Log(
                "KEY-DELETE char={0} type=0x{1:X} instance={2} page={3} slot={4}",
                character.Identity.Instance,
                (int)keyIdentity.Type,
                keyIdentity.Instance,
                inventoryPageType,
                inventorySlot);
            NotifyMissionItemDeleted(client, character, keyIdentity);
            return true;
        }

        /// <summary>
        /// Removes every mission-key template found in inventory (journal delete / orphan cleanup).
        /// </summary>
        public static bool TryRemoveAnyMissionKey(IZoneClient client, ICharacter character)
        {
            if (client == null || character == null || character.BaseInventory == null)
            {
                return false;
            }

            bool any = false;
            var instances = new List<int>();
            foreach (KeyValuePair<int, IInventoryPage> pageEntry in character.BaseInventory.Pages)
            {
                foreach (KeyValuePair<int, IItem> itemEntry in pageEntry.Value.List().ToList())
                {
                    IItem item = itemEntry.Value;
                    if (item == null || item.Identity == null || item.LowID != MissionKeyTemplateId
                        || item.HighID != MissionKeyTemplateId)
                    {
                        continue;
                    }

                    instances.Add(item.Identity.Instance);
                }
            }

            for (int i = 0; i < instances.Count; i++)
            {
                if (TryRemoveMissionKey(client, character, instances[i]))
                {
                    any = true;
                }
            }

            return any;
        }

        /// <summary>
        /// Destroys a previously granted repair kit on journal delete (capture 20260724-134055):
        /// CharacterAction(0x2F) targeting the Terminal kit identity with Parameter1=0x71, then Despawn.
        /// </summary>
        public static bool TryRemoveRepairItem(IZoneClient client, ICharacter character, int kitInstance)
        {
            if (client == null || character == null || character.BaseInventory == null || kitInstance == 0)
            {
                return false;
            }

            var kitIdentity = new Identity { Type = (IdentityType)RepairKitIdentityType, Instance = kitInstance };

            bool removedFromInventory = false;
            foreach (KeyValuePair<int, IInventoryPage> pageEntry in character.BaseInventory.Pages)
            {
                foreach (KeyValuePair<int, IItem> itemEntry in pageEntry.Value.List().ToList())
                {
                    IItem item = itemEntry.Value;
                    if (item == null || item.Identity == null || !IsRepairTool(item)
                        || item.Identity.Instance != kitInstance)
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

                    // Prefer the live identity type from the bag when present.
                    kitIdentity = item.Identity;
                    break;
                }

                if (removedFromInventory)
                {
                    break;
                }
            }

            NotifyMissionItemDeleted(client, character, kitIdentity);
            return removedFromInventory;
        }

        /// <summary>
        /// Removes one repair kit from inventory when the per-mission kit instance is unknown.
        /// </summary>
        public static bool TryRemoveAnyRepairItem(IZoneClient client, ICharacter character)
        {
            IItem kit;
            if (!TryFindRepairTool(character, out kit) || kit == null || kit.Identity == null)
            {
                return false;
            }

            return TryRemoveRepairItem(client, character, kit.Identity.Instance);
        }

        private static void NotifyMissionItemDeleted(
            IZoneClient client,
            ICharacter character,
            Identity itemIdentity)
        {
            if (client == null || character == null || itemIdentity == null)
            {
                return;
            }

            client.SendCompressed(
                new CharacterActionMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    Action = (CharacterActionType)MissionKeyDeleteAction,
                    Unknown1 = 0,
                    Target = itemIdentity,
                    Parameter1 = MissionKeyDeleteParameter1,
                    Parameter2 = 0,
                    Unknown2 = 0
                });
            client.SendCompressed(
                new DespawnMessage { Identity = itemIdentity, Unknown = 0 });
        }

        private static Item CreateItem(
            int lowId,
            int highId,
            int quality,
            uint itemFlags,
            int itemIdentityType,
            int reservedItemInstance)
        {
            var item = new Item(quality, lowId, highId)
                       {
                           Identity =
                               new Identity
                               {
                                   Type = (IdentityType)itemIdentityType,
                                   Instance = reservedItemInstance != 0
                                                  ? reservedItemInstance
                                                  : CreateMissionKeyInstance()
                               },
                           Flags = 1
                       };

            foreach (GameTuple<CharacterStat, uint> stat in CreateItemStats(lowId, highId, quality, itemFlags))
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
            byte unknown2,
            byte unknown3,
            int lowId,
            int highId,
            int quality,
            uint itemFlags)
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
                       Unknown2 = unknown2,
                       Unknown3 = unknown3,
                       Stats = CreateItemStats(lowId, highId, quality, itemFlags),
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

        private static GameTuple<CharacterStat, uint>[] CreateItemStats(
            int lowId,
            int highId,
            int quality,
            uint itemFlags)
        {
            return new[]
                   {
                       MissionKeyStat(CharacterStat.Flags, itemFlags),
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
