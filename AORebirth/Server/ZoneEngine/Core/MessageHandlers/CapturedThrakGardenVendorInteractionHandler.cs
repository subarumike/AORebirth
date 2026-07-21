namespace ZoneEngine.Core.MessageHandlers
{
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Events;
    using AORebirth.Core.Network;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Playfields;
    using ZoneEngine.Core.Thrak.Quests;

    /// <summary>
    /// Thrak Omni garden vendors (PF 4677). Capture 20260718-210135:
    /// Click opens KnuBot dialogue first; shop opens via dialog shop icon (GenericCmd Use)
    /// or Craig-Or "Business. Let's see what you've got." answer.
    /// Son-Len shop requires completed Thrak garden key quest.
    /// </summary>
    internal sealed class CapturedThrakGardenVendorInteractionHandler
    {
        internal static readonly CapturedThrakGardenVendorInteractionHandler Default =
            new CapturedThrakGardenVendorInteractionHandler();

        private CapturedThrakGardenVendorInteractionHandler()
        {
        }

        internal bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            ICharacter character = client.Controller.Character;
            if (!this.TryOpenShop(character, target, true))
            {
                // Not a registered Thrak vendor (or no vendor endpoint yet).
                CapturedThrakGardenVendorRuntimeDefinition runtime;
                if (!CapturedThrakGardenVendorRuntimeRegistry.TryGet(target.Instance, out runtime))
                {
                    return false;
                }

                GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                return true;
            }

            GenericCmdMessageHandler.Default.Acknowledge(character, message);
            return true;
        }

        /// <summary>
        /// Open the capture-backed VendingMachine shop for a Thrak garden vendor NPC.
        /// Used by dialog shop-icon Use and Craig-Or "Business" answer.
        /// </summary>
        internal bool TryOpenShop(ICharacter character, Identity npcIdentity, bool acknowledgeDeniedOnGate = false)
        {
            if (character == null || character.Playfield == null)
            {
                return false;
            }

            CapturedThrakGardenVendorRuntimeDefinition runtime;
            if (!CapturedThrakGardenVendorRuntimeRegistry.TryGet(npcIdentity.Instance, out runtime))
            {
                return false;
            }

            if (!CapturedThrakGardenVendorRuntimeRegistry.Same(runtime.NpcIdentity, npcIdentity))
            {
                return false;
            }

            if (!CapturedThrakGardenVendorRuntimeRegistry.Same(
                    runtime.PlayfieldIdentity,
                    character.Playfield.Identity))
            {
                return false;
            }

            if (runtime.Content != null
                && runtime.Content.RequiresCompletedGardenKeyQuest
                && !ThrakGardenKeyQuestRuntime.HasCompletedGardenKeyQuest(character))
            {
                return false;
            }

            if (runtime.VendorIdentity.Instance == 0)
            {
                return false;
            }

            Vendor vendor = Pool.Instance.GetObject<Vendor>(
                character.Playfield.Identity,
                runtime.VendorIdentity);
            if (vendor == null)
            {
                return false;
            }

            Event trade = vendor.Events.FirstOrDefault(candidate => candidate.EventType == EventType.OnTrade);
            if (trade == null)
            {
                return false;
            }

            trade.Perform(character, vendor);
            var temporaryBag =
                new TemporaryBag(
                    character.Identity,
                    new Identity
                    {
                        Type = IdentityType.TempBag,
                        Instance = Pool.Instance.GetFreeInstance<TemporaryBag>(0, IdentityType.TempBag)
                    },
                    character.Identity,
                    vendor.Identity);
            character.ShoppingBag = temporaryBag;
            TradeMessageHandler.Default.Send(character, temporaryBag);
            return true;
        }
    }
}
