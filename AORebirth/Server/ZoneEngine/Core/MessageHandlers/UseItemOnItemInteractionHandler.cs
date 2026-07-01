namespace ZoneEngine.Core.MessageHandlers
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

    #endregion

    public sealed class UseItemOnItemInteractionHandler
    {
        public static readonly UseItemOnItemInteractionHandler Default =
            new UseItemOnItemInteractionHandler();

        private UseItemOnItemInteractionHandler()
        {
        }

        public bool TryHandle(IZoneClient client, GenericCmdMessage message)
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
