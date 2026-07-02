namespace ZoneEngine.Core
{
    #region Usings ...

    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Events;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Core.Statels;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.MessageHandlers;

    #endregion

    public sealed class InventoryContainerRuntimeService
    {
        public static readonly InventoryContainerRuntimeService Default = new InventoryContainerRuntimeService();

        private InventoryContainerRuntimeService()
        {
        }

        public void OpenBank(ICharacter character)
        {
            BankMessageHandler.Default.Send(character);
        }

        public BankSlot[] ResolveBankSlots(ICharacter character)
        {
            return character.BaseInventory.Pages[(int)IdentityType.Bank].ToInventoryArray();
        }

        public bool TryHandleGenericCmdUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            switch (InventoryContainerInteractionRules.ResolveRouteMode(target))
            {
                case InventoryContainerInteractionRouteMode.InventoryItem:
                    client.Controller.UseItem(target);
                    GenericCmdMessageHandler.Default.Acknowledge(client.Controller.Character, message);
                    return true;

                case InventoryContainerInteractionRouteMode.WearOrSocialBackpack:
                    if (client.Controller.TryUseBackpackContainer(target))
                    {
                        GenericCmdMessageHandler.Default.Acknowledge(client.Controller.Character, message);
                    }

                    return true;

                case InventoryContainerInteractionRouteMode.BackpackContainer:
                    IInventoryPage backpackPage;
                    if (client.Controller.Character.BaseInventory.TryGetBackpackPage(target, out backpackPage))
                    {
                        BackpackContainerActionMessageHandler.Default.SendClose(client.Controller.Character, target);
                        client.Controller.Character.BaseInventory.MarkBackpackClosed(target);
                        GenericCmdMessageHandler.Default.Acknowledge(client.Controller.Character, message);
                    }

                    return true;
            }

            return false;
        }

        public bool TryHandleUseItemOnItem(IZoneClient client, GenericCmdMessage message)
        {
            if (UseItemOnItemInteractionRules.ResolveRouteMode(message.Action)
                != UseItemOnItemInteractionRouteMode.UseItemOnItem)
            {
                return false;
            }

            IItem item =
                Pool.Instance.GetObject<IInventoryPage>(
                    new Identity()
                    {
                        Type = (IdentityType)client.Controller.Character.Identity.Instance,
                        Instance = (int)message.Target[0].Type
                    })[message.Target[0].Instance];
            client.Controller.Character.Stats[StatIds.secondaryitemtemplate].Value = item.LowID;
            //client.Controller.Character.Stats[StatIds.secondaryitemtype]
            if (Pool.Instance.Contains(message.Target[1]))
            {
                StaticDynel temp =
                    Pool.Instance.GetObject<StaticDynel>(
                        client.Controller.Character.Playfield.Identity,
                        message.Target[1]);
                if (temp != null)
                {
                    Event ev = temp.Events.FirstOrDefault(x => x.EventType == EventType.OnUseItemOn);
                    if (ev != null)
                    {
                        ev.Perform(client.Controller.Character, temp);
                    }
                }
            }
            else
            {
                client.Controller.UseStatel(message.Target[1], EventType.OnUseItemOn);
            }

            return true;
        }
    }
}
