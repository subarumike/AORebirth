namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.Controllers;

    using Coordinate = AORebirth.Core.Vector.Coordinate;
    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    #endregion

    /// <summary>
    /// Capture 20260727-Alien- quest-ncu / 20260727-055715:
    /// Door:C0001F49 (Exit) inside PF 8009 → Arete outdoor;
    /// Door:108CD4D0 (Crashed Alien Ship) on Arete → PF 8009 interior.
    /// </summary>
    public class CrashedAlienShipDoorInteractionHandler
    {
        public static readonly CrashedAlienShipDoorInteractionHandler Default =
            new CrashedAlienShipDoorInteractionHandler();

        public const int CrashedAlienShipPlayfieldId = 8009;

        public const int AreteLandingPlayfieldId = 6553;

        public const int ExitDoorInstance = unchecked((int)0xC0001F49);

        public const int EntryDoorInstance = unchecked((int)0x108CD4D0);

        // Capture exit N3Teleport Destination (ACG-local Exit door).
        public const float ExitEnvelopeX = 39.85169f;

        public const float ExitEnvelopeY = 0.435f;

        public const float ExitEnvelopeZ = 25.41414f;

        public const float ExitEnvelopeHeadingY = 0.9998459f;

        public const float ExitEnvelopeHeadingW = -0.01755503f;

        // Outdoor SCFU after exit.
        public const float ExitLandingX = 3887.511f;

        public const float ExitLandingY = 6.108168f;

        public const float ExitLandingZ = 248.7638f;

        public const float ExitLandingHeadingY = -0.7038539f;

        public const float ExitLandingHeadingW = 0.7103448f;

        // Capture entry N3Teleport Destination (outdoor envelope).
        public const float EntryEnvelopeX = 3893.491f;

        public const float EntryEnvelopeY = 6.275671f;

        public const float EntryEnvelopeZ = 248.1938f;

        public const float EntryEnvelopeHeadingY = 0.7191251f;

        public const float EntryEnvelopeHeadingW = 0.6948806f;

        // Interior SCFU after entry.
        public const float EntryLandingX = 40.2771f;

        public const float EntryLandingY = 0.435f;

        public const float EntryLandingZ = 31.18121f;

        public const float EntryLandingHeadingY = -0.008304117f;

        public const float EntryLandingHeadingW = 0.9999655f;

        public bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            if (client == null || message == null)
            {
                return false;
            }

            if (target.Type != IdentityType.Door)
            {
                return false;
            }

            if (target.Instance == ExitDoorInstance)
            {
                return this.TryExit(client, message);
            }

            if (target.Instance == EntryDoorInstance)
            {
                return this.TryEnter(client, message);
            }

            return false;
        }

        private bool TryExit(IZoneClient client, GenericCmdMessage message)
        {
            ICharacter character = client.Controller != null ? client.Controller.Character : null;
            if (character == null
                || character.Playfield == null
                || character.Playfield.Identity.Instance != CrashedAlienShipPlayfieldId
                || !(character.Controller is PlayerController))
            {
                if (character != null)
                {
                    GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                }

                return true;
            }

            Dynel dynel = character as Dynel;
            Playfield sourcePlayfield = character.Playfield as Playfield;
            if (dynel == null || sourcePlayfield == null)
            {
                GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                return true;
            }

            character.Stats[StatIds.externalplayfieldinstance].BaseValue = CrashedAlienShipPlayfieldId;
            GenericCmdMessageHandler.Default.Acknowledge(character, message);

            var landing = new Coordinate(ExitLandingX, ExitLandingY, ExitLandingZ);
            var heading = new Quaternion(0f, ExitLandingHeadingY, 0f, ExitLandingHeadingW);
            var envelope = new Vector3(ExitEnvelopeX, ExitEnvelopeY, ExitEnvelopeZ);
            var envelopeHeading = new Quaternion(0f, ExitEnvelopeHeadingY, 0f, ExitEnvelopeHeadingW);

            sourcePlayfield.Teleport(
                dynel,
                landing,
                heading,
                new Identity { Type = IdentityType.Playfield, Instance = AreteLandingPlayfieldId },
                transferCharacter => TeleportMessageHandler.Default.SendCapturedCrashedAlienShipDoorExit(
                    transferCharacter,
                    envelope,
                    envelopeHeading,
                    AreteLandingPlayfieldId));

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "CrashedAlienShip door exit Use char=" + character.Identity.ToString(true)
                + " evidence=20260727-Alien- quest-ncu");
            return true;
        }

        private bool TryEnter(IZoneClient client, GenericCmdMessage message)
        {
            ICharacter character = client.Controller != null ? client.Controller.Character : null;
            if (character == null
                || character.Playfield == null
                || character.Playfield.Identity.Instance != AreteLandingPlayfieldId
                || !(character.Controller is PlayerController))
            {
                if (character != null)
                {
                    GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                }

                return true;
            }

            Dynel dynel = character as Dynel;
            Playfield sourcePlayfield = character.Playfield as Playfield;
            if (dynel == null || sourcePlayfield == null)
            {
                GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                return true;
            }

            character.Stats[StatIds.externalplayfieldinstance].BaseValue = AreteLandingPlayfieldId;
            GenericCmdMessageHandler.Default.Acknowledge(character, message);

            var landing = new Coordinate(EntryLandingX, EntryLandingY, EntryLandingZ);
            var heading = new Quaternion(0f, EntryLandingHeadingY, 0f, EntryLandingHeadingW);
            var envelope = new Vector3(EntryEnvelopeX, EntryEnvelopeY, EntryEnvelopeZ);
            var envelopeHeading = new Quaternion(0f, EntryEnvelopeHeadingY, 0f, EntryEnvelopeHeadingW);

            sourcePlayfield.Teleport(
                dynel,
                landing,
                heading,
                new Identity { Type = IdentityType.Playfield, Instance = CrashedAlienShipPlayfieldId },
                transferCharacter => TeleportMessageHandler.Default.SendCapturedCrashedAlienShipDoorEntry(
                    transferCharacter,
                    envelope,
                    envelopeHeading,
                    CrashedAlienShipPlayfieldId));

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "CrashedAlienShip door entry Use char=" + character.Identity.ToString(true)
                + " evidence=20260727-055715");
            return true;
        }
    }
}
