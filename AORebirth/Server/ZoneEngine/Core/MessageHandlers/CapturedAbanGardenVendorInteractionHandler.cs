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

    using ZoneEngine.Core.Nascence.Quests;
    using ZoneEngine.Core.Playfields;

    /// <summary>
    /// Aban Redeemed garden vendors (PF 4676). Capture 20260823-205320:
    /// Click opens KnuBot dialogue first; shop opens via dialog shop icon (GenericCmd Use)
    /// or Or-Mada business answer. El-Mada shop/talk require Aban garden key — without key:
    /// deny shop; no invented no-key chat lines.
    /// </summary>
    internal sealed class CapturedAbanGardenVendorInteractionHandler
    {
        internal static readonly CapturedAbanGardenVendorInteractionHandler Default =
            new CapturedAbanGardenVendorInteractionHandler();

        private CapturedAbanGardenVendorInteractionHandler()
        {
        }

        internal bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            ICharacter character = client.Controller.Character;
            if (!this.TryOpenShop(character, target, true))
            {
                CapturedAbanGardenVendorRuntimeDefinition runtime;
                if (!CapturedAbanGardenVendorRuntimeRegistry.TryGet(target.Instance, out runtime))
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
        /// Open the capture-backed VendingMachine shop for an Aban garden vendor NPC.
        /// Used by dialog shop-icon Use and Or-Mada business answer.
        /// </summary>
        internal bool TryOpenShop(ICharacter character, Identity npcIdentity, bool acknowledgeDeniedOnGate = false)
        {
            if (character == null || character.Playfield == null)
            {
                return false;
            }

            CapturedAbanGardenVendorRuntimeDefinition runtime;
            if (!CapturedAbanGardenVendorRuntimeRegistry.TryGet(npcIdentity.Instance, out runtime))
            {
                return false;
            }

            if (!CapturedAbanGardenVendorRuntimeRegistry.Same(runtime.NpcIdentity, npcIdentity))
            {
                return false;
            }

            if (!CapturedAbanGardenVendorRuntimeRegistry.Same(
                    runtime.PlayfieldIdentity,
                    character.Playfield.Identity))
            {
                return false;
            }

            if (runtime.Content != null
                && runtime.Content.RequiresGardenKey
                && !NascenceAbanFalaQuestRuntime.HasAbanGardenKey(character))
            {
                // Capture has no Aban no-key chat lines — deny silently (no invented text).
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
