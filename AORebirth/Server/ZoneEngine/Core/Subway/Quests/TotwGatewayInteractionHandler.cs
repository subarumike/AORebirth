namespace ZoneEngine.Core.Subway.Quests
{
    #region Usings ...

    using System.Linq;

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Playfields;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    #endregion

    internal sealed class TotwGatewayInteractionHandler
    {
        internal const int GatewayInstance = WindcallerKarrecInteractionRules.GatewayInstance;

        internal static readonly TotwGatewayInteractionHandler Default =
            new TotwGatewayInteractionHandler();

        private TotwGatewayInteractionHandler()
        {
        }

        internal bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            if (!WindcallerKarrecInteractionRules.IsGateway(target))
            {
                return false;
            }

            ICharacter character = client == null || client.Controller == null
                                       ? null
                                       : client.Controller.Character;
            if (character == null
                || character.Playfield == null
                || character.Playfield.Identity.Instance != WindcallerKarrecQuestRuntime.SubwayPlayfieldId
                || !IsKnownGatewayInCurrentPlayfield(character, target))
            {
                if (character != null)
                {
                    GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                }

                return true;
            }

            bool hasAccountAccess = WindcallerKarrecQuestRuntime.HasAccountAccess(character);
            if (!hasAccountAccess && WindcallerKarrecQuestRuntime.IsCompleted(character))
            {
                KarrecCompletionResult retry =
                    WindcallerKarrecQuestRuntime.CompleteAfterOfferingsConsumed(character);
                if (!retry.Completed)
                {
                    GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                    return true;
                }

                hasAccountAccess = WindcallerKarrecQuestRuntime.HasAccountAccess(character);
            }

            if (!hasAccountAccess)
            {
                GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                return true;
            }

            Dynel dynel = character as Dynel;
            Playfield sourcePlayfield = character.Playfield as Playfield;
            if (dynel == null || sourcePlayfield == null)
            {
                GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                return true;
            }

            var landing = new Coordinate(1814.0f, 29.0f, 2699.0f);
            var heading = new Quaternion(
                0.0f,
                -0.9576424956321716f,
                0.0f,
                0.2879597544670105f);
            GenericCmdMessageHandler.Default.Acknowledge(character, message);
            sourcePlayfield.Teleport(
                dynel,
                landing,
                heading,
                new Identity { Type = IdentityType.Playfield, Instance = 647 },
                transferCharacter => TeleportMessageHandler.Default.SendCapturedGatewayTransfer(
                    transferCharacter,
                    new Vector3(3214.815185546875f, 35.51499938964844f, 791.053466796875f),
                    new Vector3(1814.0f, 29.0f, 2699.0f),
                    heading,
                    647));
            return true;
        }

        private static bool IsKnownGatewayInCurrentPlayfield(ICharacter character, Identity target)
        {
            AORebirth.Core.Playfields.PlayfieldData playfieldData;
            return character != null
                   && character.Playfield != null
                   && PlayfieldLoader.PFData.TryGetValue(
                       character.Playfield.Identity.Instance,
                       out playfieldData)
                   && playfieldData.Statels.Any(
                       value => value.Identity.Type == target.Type
                                && value.Identity.Instance == target.Instance);
        }
    }
}
