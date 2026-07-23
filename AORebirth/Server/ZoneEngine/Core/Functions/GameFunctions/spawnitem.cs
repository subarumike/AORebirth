namespace ZoneEngine.Core.Functions.GameFunctions
{
    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    /// <summary>
    /// FunctionType.SpawnItem (53064) — args=[hash, ql, count] (+ Use slot appended by PerformAction).
    /// Capture 20260723-123341 token-board upgrade: TemplateAction Overflow(Unknown2=87) →
    /// ContainerAddItem(slot 0x6F) → TemplateAction Inventory(Unknown2=3) into the same slot.
    /// Hashes: OIHO = Omni boards 296371..296378, CNAD = Clan boards 296363..296370.
    /// </summary>
    internal class spawnitem : FunctionPrototype
    {
        private const int CapturedTemplateActionUnknown1 = 1;

        private const int CapturedOverflowTemplateActionUnknown2 = 87;

        private const int CapturedInventoryTemplateActionUnknown2 = 3;

        private const int CapturedOverflowNextFreeSlot = 0x6F;

        private const int OmniTokenBoardSeedId = 296371;

        private const int ClanTokenBoardSeedId = 296363;

        public override FunctionType FunctionId
        {
            get
            {
                return FunctionType.SpawnItem;
            }
        }

        public override bool Execute(
            INamedEntity self,
            IEntity caller,
            IInstancedEntity target,
            MessagePackObject[] arguments)
        {
            ICharacter character = self as ICharacter;
            if (character == null || character.BaseInventory == null || arguments == null || arguments.Length < 3)
            {
                return false;
            }

            string hash;
            int quality;
            int slot;
            try
            {
                hash = arguments[0].AsString();
                quality = arguments[1].AsInt32();
                slot = arguments[arguments.Length - 1].AsInt32();
            }
            catch
            {
                return false;
            }

            int itemId;
            if (!TryResolveItemId(hash, quality, out itemId))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "SpawnItem unresolved hash=" + hash + " ql=" + quality);
                return false;
            }

            Item item;
            try
            {
                item = new Item(quality, itemId, itemId);
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "SpawnItem create failed id=" + itemId + " ql=" + quality + " err=" + ex.Message);
                return false;
            }

            int page = (int)IdentityType.Inventory;
            InventoryError addError;
            try
            {
                addError = character.BaseInventory.AddToPage(page, slot, item);
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "SpawnItem AddToPage failed slot=" + slot + " err=" + ex.Message);
                return false;
            }

            if (addError != InventoryError.OK)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "SpawnItem AddToPage rejected slot=" + slot + " err=" + addError);
                return false;
            }

            character.BaseInventory.Write();
            SendCaptureGrantPackets(character, itemId, quality, slot);
            return true;
        }

        private static bool TryResolveItemId(string hash, int quality, out int itemId)
        {
            itemId = 0;
            int seedId;
            if (string.Equals(hash, "OIHO", StringComparison.OrdinalIgnoreCase))
            {
                seedId = OmniTokenBoardSeedId;
            }
            else if (string.Equals(hash, "CNAD", StringComparison.OrdinalIgnoreCase))
            {
                seedId = ClanTokenBoardSeedId;
            }
            else
            {
                return false;
            }

            if (!ItemLoader.ItemList.ContainsKey(seedId))
            {
                return false;
            }

            ItemTemplate seed = ItemLoader.ItemList[seedId];
            int lowId = seed.GetLowId(quality);
            int highId = seed.GetHighId(quality);
            if (lowId <= 0 || highId == 1234567890)
            {
                return false;
            }

            // Exact QL templates use low==high; prefer the high endpoint when they differ.
            itemId = highId;
            return ItemLoader.ItemList.ContainsKey(itemId);
        }

        private static void SendCaptureGrantPackets(ICharacter character, int itemId, int quality, int inventorySlot)
        {
            character.Send(
                new TemplateActionMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    ItemLowId = itemId,
                    ItemHighId = itemId,
                    Quality = quality,
                    Unknown1 = CapturedTemplateActionUnknown1,
                    Unknown2 = CapturedOverflowTemplateActionUnknown2,
                    Placement = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                    Unknown3 = 0,
                    Unknown4 = 0
                });
            character.Send(
                new ContainerAddItemMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    SourceContainer = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                    Target =
                        new Identity
                        {
                            Type = IdentityType.OverflowWindow,
                            Instance = character.Identity.Instance
                        },
                    TargetPlacement = CapturedOverflowNextFreeSlot
                });
            character.Send(
                new TemplateActionMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    ItemLowId = itemId,
                    ItemHighId = itemId,
                    Quality = quality,
                    Unknown1 = CapturedTemplateActionUnknown1,
                    Unknown2 = CapturedInventoryTemplateActionUnknown2,
                    Placement = new Identity { Type = IdentityType.Inventory, Instance = inventorySlot },
                    Unknown3 = 0,
                    Unknown4 = 0
                });
        }
    }
}
