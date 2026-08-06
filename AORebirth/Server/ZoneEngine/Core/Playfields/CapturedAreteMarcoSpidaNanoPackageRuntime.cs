namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Arete.Quests;
    using ZoneEngine.Core.MessageHandlers;

    #endregion

    /// <summary>
    /// Capture 20260721-nanoprogramsvendor / 20260721-nano-enforcer-arete:
    /// Use Marco nano crystal package → Overflow content grants → Delete crystal → tip reward.
    /// </summary>
    internal static class CapturedAreteMarcoSpidaNanoPackageRuntime
    {
        private const int OverflowTemplateActionUnknown1 = 1;

        private const int OverflowTemplateActionUnknown2 = 87;

        private const int OverflowNextFreeSlot = 111;

        internal static bool TryHandleCrystalUse(ICharacter character, Identity itemPosition, Item item)
        {
            if (character == null || item == null)
            {
                return false;
            }

            CapturedAreteMarcoSpidaNanoPackageContent package;
            if (!CapturedAreteMarcoSpidaVendorContentProvider.TryGetNanoPackage(item.LowID, out package)
                && !CapturedAreteMarcoSpidaVendorContentProvider.TryGetNanoPackage(item.HighID, out package))
            {
                return false;
            }

            if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(character)
                || character.Controller == null
                || character.Controller.Client == null)
            {
                Log("package-use skipped: inventory/client missing crystal=" + package.CrystalItemId);
                return false;
            }

            for (int i = 0; i < package.Contents.Length; i++)
            {
                CapturedAreteMarcoSpidaNanoPackageContentEntry entry = package.Contents[i];
                if (!ItemLoader.ItemList.ContainsKey(entry.ItemId))
                {
                    Log(
                        "package-use skipped: ItemLoader missing content id="
                        + entry.ItemId
                        + " crystal="
                        + package.CrystalItemId);
                    return false;
                }
            }

            // Capture 20260721-nanoprogramsvendor: Overflow nanos first, then delete crystal.
            int granted = 0;
            for (int i = 0; i < package.Contents.Length; i++)
            {
                CapturedAreteMarcoSpidaNanoPackageContentEntry entry = package.Contents[i];
                Item grantItem;
                try
                {
                    grantItem = new Item(entry.Quality, entry.ItemId, entry.ItemId);
                }
                catch (Exception ex)
                {
                    Log(
                        "package content create failed id="
                        + entry.ItemId
                        + " reason="
                        + ex.Message);
                    return false;
                }

                QuestRewardInventoryGrantResult grant =
                    InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(character, grantItem);
                if (grant.Status == QuestRewardInventoryGrantStatus.Success)
                {
                    SendOverflowGrantPackets(character, entry.ItemId, entry.Quality);
                    granted++;
                    continue;
                }

                // Capture-backed packages contain unique nanos; already-owned uniques must not abort the open.
                if (grant.Status == QuestRewardInventoryGrantStatus.InventoryAddFailed
                    && grant.InventoryError == InventoryError.HaveUniqueAlready)
                {
                    Log(
                        "package content already owned id="
                        + entry.ItemId
                        + " crystal="
                        + package.CrystalItemId
                        + " — continue");
                    continue;
                }

                Log(
                    "package content grant failed id="
                    + entry.ItemId
                    + " status="
                    + grant.Status
                    + " invErr="
                    + grant.InventoryError);
                return false;
            }

            // Capture: TemplateAction crystal Unknown2=3 at Inventory placement, then DeleteItem.
            TemplateActionMessageHandler.Default.Send(
                character,
                item,
                (int)itemPosition.Type,
                itemPosition.Instance);
            character.BaseInventory.RemoveItem((int)itemPosition.Type, itemPosition.Instance);
            CharacterActionMessageHandler.Default.SendDeleteItem(
                character,
                (int)itemPosition.Type,
                itemPosition.Instance);

            // Capture order: tip XP/credits + 223373 + Action59/Delete after crystal delete.
            StanGoodmanQuestRuntime.TryCompleteBuyNanoTipOnCrystalUse(character, item);

            Log(
                "package opened name="
                + package.DisplayName
                + " crystal="
                + package.CrystalItemId
                + " contents="
                + package.Contents.Length
                + " granted="
                + granted
                + " character="
                + character.Identity
                + " evidence="
                + package.Evidence);
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
                    Unknown1 = OverflowTemplateActionUnknown1,
                    Unknown2 = OverflowTemplateActionUnknown2,
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
                    TargetPlacement = OverflowNextFreeSlot
                });
        }

        private static void Log(string message)
        {
            LogUtil.Debug(DebugInfoDetail.Engine, "MarcoSpidaNanoPackage " + message);
        }
    }
}
