namespace ZoneEngine.Core.Andromeda
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;

    using Coordinate = AORebirth.Core.Vector.Coordinate;
    using Quaternion = AORebirth.Core.Vector.Quaternion;

    #endregion

    /// <summary>
    /// Capture 20260806-naleb-transport: Zyvania Bagh (ICC HQ PF655) transports to
    /// Omni Forest / Steps of Madness entrance PF716 at (808, 11.66283, 2823).
    /// </summary>
    public static class ZyvaniaBaghTransportRuntime
    {
        public const string TransportOfferNodeId = "zyvania_transport_offer";

        public const string TeleportNodeId = "zyvania_teleport";

        public const int TransportAcceptAnswerIndex = 0;

        public const int SourcePlayfieldId = 655;

        public const int DestinationPlayfieldId = 716;

        public const float DestinationX = 808f;

        public const float DestinationY = 11.66283f;

        public const float DestinationZ = 2823f;

        // Capture CharDCMove after teleport-ended.
        public const float DestinationHeadingX = 0f;

        public const float DestinationHeadingY = -0.9645079f;

        public const float DestinationHeadingZ = 0f;

        public const float DestinationHeadingW = 0.2640538f;

        public static bool TryHandleDialogueAnswer(
            ICharacter source,
            string previousNodeId,
            int answerIndex)
        {
            if (source == null)
            {
                return false;
            }

            if (!string.Equals(previousNodeId, TransportOfferNodeId, StringComparison.OrdinalIgnoreCase)
                || answerIndex != TransportAcceptAnswerIndex)
            {
                return false;
            }

            return TryTeleportToNelebEntrance(source);
        }

        public static bool TryTeleportToNelebEntrance(ICharacter source)
        {
            Character character = source as Character;
            if (character == null || character.Playfield == null)
            {
                return false;
            }

            character.DoNotDoTimers = false;
            character.StopMovement();
            character.Stats[StatIds.externaldoorinstance].BaseValue = 0;
            character.Stats[StatIds.externalplayfieldinstance].BaseValue = 0;

            Dynel dynel = character;
            var destination = new Coordinate(DestinationX, DestinationY, DestinationZ);
            var heading = new Quaternion(
                DestinationHeadingX,
                DestinationHeadingY,
                DestinationHeadingZ,
                DestinationHeadingW);

            character.Playfield.Teleport(
                dynel,
                destination,
                heading,
                new Identity { Type = IdentityType.Playfield, Instance = DestinationPlayfieldId });

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    "ZyvaniaBaghTransport char={0} destPf={1} dest=({2:F3},{3:F3},{4:F3}) evidence=20260806-naleb-transport",
                    character.Identity,
                    DestinationPlayfieldId,
                    DestinationX,
                    DestinationY,
                    DestinationZ));

            return true;
        }
    }
}
