namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;
    using AORebirth.Core.Playfields;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;
    using ZoneEngine.Core;
    using ZoneEngine.Core.Playfields;

    #endregion

    /// <summary>
    /// Capture 20260830-140240: interior door Use (ACK Temp1=2) and
    /// floor Button (down/up/boss) local teleports.
    /// </summary>
    public sealed class NascenceDungeon3InteractionHandler
    {
        public static readonly NascenceDungeon3InteractionHandler Default =
            new NascenceDungeon3InteractionHandler();

        // Capture 20260830-140240 terminal instances — paired down/up platforms.
        // Button (down) 57FC32E6 @ (1362.3,64.19,183.7) ↔ Button (up) 57FC32E7 @ (1082.5,52.01,264.9)
        // Button (down) 57FC32EA @ (1008.6,52.01,249.01) ↔ Button (up) 57FC32EB @ (440.01,52.01,118.3)
        // Button (boss) 57FC32EE @ (661.1,76.11,130.6) ↔ Button (up) 57FC32EF @ (130.86,52.02,129.26)
        private const int ButtonDownEntry = unchecked((int)0x57FC32E6);

        private const int ButtonUpEntry = unchecked((int)0x57FC32E7);

        private const int ButtonDownMid = unchecked((int)0x57FC32EA);

        private const int ButtonUpMid = unchecked((int)0x57FC32EB);

        private const int ButtonBoss = unchecked((int)0x57FC32EE);

        private const int ButtonUpBoss = unchecked((int)0x57FC32EF);

        private NascenceDungeon3InteractionHandler()
        {
        }

        public bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            if (client == null || message == null || client.Controller == null)
            {
                return false;
            }

            ICharacter character = client.Controller.Character;
            if (character == null
                || character.Playfield == null
                || !NascenceDungeon3Rules.IsDungeonPlayfield(character.Playfield.Identity.Instance))
            {
                return false;
            }

            if (target.Type == IdentityType.Door
                && NascenceDungeon3DoorCapture.IsInteriorDoorInstance(target.Instance))
            {
                // Capture 20260830-140240 door Use: GenericCmd ACK Temp1=2 only.
                // No DoorStatusUpdate on live — open/close anim is client ACG mesh after ACK.
                GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                ClientActionBusyRuntime.Clear(character);
                LogUtil.Debug(
                    DebugInfoDetail.Zoning,
                    "NascenceDungeon3 door Use ACK Temp1=2 door=" + target.Instance.ToString("X8"));
                return true;
            }

            if (target.Type != IdentityType.Terminal)
            {
                return false;
            }

            float destX;
            float destY;
            float destZ;
            if (!TryResolveButtonDestination(target.Instance, out destX, out destY, out destZ))
            {
                return false;
            }

            GenericCmdMessageHandler.Default.Acknowledge(character, message);

            var landing = new Vector3 { x = destX, y = destY, z = destZ };
            var heading = new Quaternion(0f, 0f, 0f, 1f);
            TeleportMessageHandler.Default.SendLocal(character, landing, heading);

            var dynel = character as Dynel;
            if (dynel != null)
            {
                dynel.RawCoordinates = landing;
                dynel.RawHeading = heading;
            }

            var zoneClient = client as ZoneClient;
            if (zoneClient != null)
            {
                NascenceDungeon3DoorReplay.RefreshFloorButtonsAfterTeleport(
                    zoneClient,
                    character,
                    target.Instance);
                NascenceDungeon3DoorReplay.RevealZoneAtPosition(zoneClient, character, destX, destZ);
                if (IsBossDownButton(target.Instance))
                {
                    NascenceDungeon3DoorReplay.RevealBossWingForCharacter(zoneClient, character);
                    var playfieldForBoss = character.Playfield as Playfield;
                    if (playfieldForBoss != null)
                    {
                        NascenceDungeon3BossRoomRuntime.ForceHavarisVisible(playfieldForBoss, character);
                    }
                }

                var playfield = character.Playfield as Playfield;
                if (playfield != null)
                {
                    playfield.RefreshCharacterVisibility(character, forceRefresh: true);
                }
            }

            // After teleport/refresh — local teleport re-locks the client action (Mike: Wait for finish).
            ClientActionBusyRuntime.Clear(character);

            LogUtil.Debug(
                DebugInfoDetail.Zoning,
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "NascenceDungeon3 button Use terminal={0:X8} dest=({1:F3},{2:F3},{3:F3})",
                    target.Instance,
                    destX,
                    destY,
                    destZ));
            return true;
        }

        private static bool TryResolveButtonDestination(
            int terminalInstance,
            out float x,
            out float y,
            out float z)
        {
            x = 0f;
            y = 0f;
            z = 0f;
            switch (terminalInstance)
            {
                case ButtonDownEntry:
                    // Lands on paired Button (up) platform after first down Use.
                    x = 1082.5f;
                    y = 52.01305f;
                    z = 264.9f;
                    return true;
                case ButtonUpEntry:
                    x = 1362.3f;
                    y = 64.18521f;
                    z = 183.7f;
                    return true;
                case ButtonDownMid:
                    x = 440.01f;
                    y = 52.01125f;
                    z = 118.3f;
                    return true;
                case ButtonUpMid:
                    x = 1008.6f;
                    y = 52.00832f;
                    z = 249.01f;
                    return true;
                case ButtonBoss:
                    x = 130.8566f;
                    y = 52.01596f;
                    z = 129.2647f;
                    return true;
                case ButtonUpBoss:
                    x = 661.1f;
                    y = 76.10554f;
                    z = 130.6f;
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsBossDownButton(int terminalInstance)
        {
            return terminalInstance == ButtonBoss;
        }
    }
}
