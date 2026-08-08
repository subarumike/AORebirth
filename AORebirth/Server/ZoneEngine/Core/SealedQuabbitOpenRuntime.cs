namespace ZoneEngine.Core
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.MessageHandlers;

    #endregion

    /// <summary>
    /// Capture 20260806-rabbit / 20260806-131535:
    /// Use Inventory sealed rabbit (301782) →
    /// TemplateAction 301749 Overflow Unknown2=87 →
    /// ContainerAddItem Overflow slot 0x6F →
    /// TemplateAction 301782 Inventory Unknown2=3 Unknown3=50000 Unknown4=charInstance →
    /// DeleteItem sealed → Use ACK.
    /// Item 301782 OnUse is SpawnItem hash PUWT → opened social 301749 (AttractorMesh 301724).
    /// </summary>
    public static class SealedQuabbitOpenRuntime
    {
        public const int SealedQuabbitItemId = 301782;

        public const int OpenedQuabbitItemId = 301749;

        public const int CapturedOpenedQuality = 1;

        private const int CapturedTemplateActionUnknown1 = 1;

        private const int CapturedOverflowTemplateActionUnknown2 = 87;

        private const int CapturedConsumeTemplateActionUnknown2 = 3;

        private const int CapturedConsumeTemplateActionUnknown3 = 50000;

        private const int CapturedOverflowNextFreeSlot = 0x6F;

        public static bool IsSealedQuabbit(Item item)
        {
            return item != null
                   && (item.LowID == SealedQuabbitItemId || item.HighID == SealedQuabbitItemId);
        }

        public static bool TryHandleUse(ICharacter character, Identity itemPosition, Item item)
        {
            if (character == null || item == null || !IsSealedQuabbit(item))
            {
                return false;
            }

            if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(character)
                || character.Controller == null
                || character.Controller.Client == null)
            {
                Log("use claimed but skipped: inventory/client missing");
                return true;
            }

            if (!ItemLoader.ItemList.ContainsKey(OpenedQuabbitItemId))
            {
                Log("use claimed but skipped: ItemLoader missing opened id=" + OpenedQuabbitItemId);
                return true;
            }

            if (!InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                    character,
                    OpenedQuabbitItemId))
            {
                Item opened;
                try
                {
                    opened = new Item(CapturedOpenedQuality, OpenedQuabbitItemId, OpenedQuabbitItemId);
                }
                catch (Exception ex)
                {
                    Log("opened create failed: " + ex.Message);
                    return true;
                }

                QuestRewardInventoryGrantResult grant =
                    InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(character, opened);
                if (grant.Status != QuestRewardInventoryGrantStatus.Success
                    && !(grant.Status == QuestRewardInventoryGrantStatus.InventoryAddFailed
                         && grant.InventoryError == InventoryError.HaveUniqueAlready))
                {
                    // Still push Overflow packets so the client sees the open (vacuum-pack pattern).
                    SendOverflowGrantPackets(character, OpenedQuabbitItemId, CapturedOpenedQuality);
                    Log(
                        "opened grant status="
                        + grant.Status
                        + " invErr="
                        + grant.InventoryError
                        + " (overflow sent)");
                }
                else if (grant.Status == QuestRewardInventoryGrantStatus.Success)
                {
                    SendOverflowGrantPackets(character, OpenedQuabbitItemId, CapturedOpenedQuality);
                }
                else
                {
                    // Unique already owned — skip duplicate grant packets.
                    Log("opened already carried; consuming sealed only");
                }
            }
            else
            {
                Log("opened already carried; consuming sealed only");
            }

            // Capture: sealed TemplateAction Unknown2=3 Unknown3=50000 Unknown4=char, then DeleteItem.
            character.Send(
                new TemplateActionMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    ItemLowId = SealedQuabbitItemId,
                    ItemHighId = SealedQuabbitItemId,
                    Quality = item.Quality > 0 ? item.Quality : CapturedOpenedQuality,
                    Unknown1 = CapturedTemplateActionUnknown1,
                    Unknown2 = CapturedConsumeTemplateActionUnknown2,
                    Placement = new Identity
                                {
                                    Type = itemPosition.Type,
                                    Instance = itemPosition.Instance
                                },
                    Unknown3 = CapturedConsumeTemplateActionUnknown3,
                    Unknown4 = character.Identity.Instance
                });
            character.BaseInventory.RemoveItem((int)itemPosition.Type, itemPosition.Instance);
            CharacterActionMessageHandler.Default.SendDeleteItem(
                character,
                (int)itemPosition.Type,
                itemPosition.Instance);

            Log(
                "opened sealed→"
                + OpenedQuabbitItemId
                + " char="
                + character.Identity.ToString(true)
                + " slot="
                + itemPosition
                + " evidence=20260806-rabbit");
            return true;
        }

        private static void SendOverflowGrantPackets(ICharacter source, int itemId, int quality)
        {
            source.Send(
                new TemplateActionMessage
                {
                    Identity = source.Identity,
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
            source.Send(
                new ContainerAddItemMessage
                {
                    Identity = source.Identity,
                    Unknown = 0,
                    SourceContainer = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                    Target = new Identity
                             {
                                 Type = IdentityType.OverflowWindow,
                                 Instance = source.Identity.Instance
                             },
                    TargetPlacement = CapturedOverflowNextFreeSlot
                });
        }

        private static void Log(string message)
        {
            LogUtil.Debug(DebugInfoDetail.Engine, "SealedQuabbitOpenRuntime " + message);
        }
    }
}
