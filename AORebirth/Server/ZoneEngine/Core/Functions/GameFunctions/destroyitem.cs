namespace ZoneEngine.Core.Functions.GameFunctions
{
    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.MessageHandlers;

    /// <summary>
    /// FunctionType.DestroyItem (53130) — remove inventory item at Use slot.
    /// PerformAction appends the inventory slot as the last argument.
    /// Capture 20260723-123341: CharacterAction DeleteItem (Inventory:slot) before SpawnItem.
    /// </summary>
    internal class destroyitem : FunctionPrototype
    {
        public override FunctionType FunctionId
        {
            get
            {
                return FunctionType.DestroyItem;
            }
        }

        public override bool Execute(
            INamedEntity self,
            IEntity caller,
            IInstancedEntity target,
            MessagePackObject[] arguments)
        {
            ICharacter character = self as ICharacter;
            if (character == null || character.BaseInventory == null || arguments == null || arguments.Length < 1)
            {
                return false;
            }

            int slot;
            try
            {
                slot = arguments[arguments.Length - 1].AsInt32();
            }
            catch
            {
                return false;
            }

            int page = (int)IdentityType.Inventory;
            try
            {
                character.BaseInventory.RemoveItem(page, slot);
            }
            catch
            {
                return false;
            }

            CharacterActionMessageHandler.Default.SendDeleteItem(character, page, slot);
            character.BaseInventory.Write();
            return true;
        }
    }
}
