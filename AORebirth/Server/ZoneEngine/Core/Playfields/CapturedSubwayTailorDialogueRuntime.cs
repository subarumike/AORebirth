namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.MessageHandlers;

    #endregion

    // Capture-backed Tailor measurement rewards from AOSharpLiveCapture/20260719-021611.
    internal static class CapturedSubwayTailorDialogueRuntime
    {
        internal static bool TryGrantMeasurementItem(ICharacter source, int answerIndex)
        {
            int itemId;
            if (!CapturedSubwayTailorDialogueContent.TryGetMeasurementItemId(answerIndex, out itemId)
                || !IsPlayerInSubway(source)
                || !InventoryContainerRuntimeService.Default.HasCharacterInventory(source)
                || source.Controller == null
                || source.Controller.Client == null
                || !ItemLoader.ItemList.ContainsKey(itemId))
            {
                return false;
            }

            Item item;
            try
            {
                item = new Item(1, itemId, itemId);
            }
            catch (Exception)
            {
                return false;
            }

            QuestRewardInventoryGrantResult grant =
                InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, item);
            if (grant.Status != QuestRewardInventoryGrantStatus.Success)
            {
                return false;
            }

            SendCapturedItemNotifications(source, item);
            return true;
        }

        private static void SendCapturedItemNotifications(ICharacter source, Item item)
        {
            source.Send(
                new TemplateActionMessage
                {
                    Identity = source.Identity,
                    Unknown = 0,
                    ItemLowId = item.LowID,
                    ItemHighId = item.HighID,
                    Quality = item.Quality,
                    Unknown1 = 1,
                    Unknown2 = 87,
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
                    Target = new Identity { Type = IdentityType.OverflowWindow, Instance = source.Identity.Instance },
                    TargetPlacement = 0x6F
                });
        }

        private static bool IsPlayerInSubway(ICharacter source)
        {
            return source != null
                   && source.Identity.Type == IdentityType.CanbeAffected
                   && source.Identity.Instance != 0
                   && source.Playfield != null
                   && source.Playfield.Identity.Instance == CapturedSubwayVendorContentProvider.SubwayPlayfieldResource;
        }
    }
}
