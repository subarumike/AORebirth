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
    /// Capture 20260830-143801: interior door Use (ACK Temp1=2) and
    /// floor Button (down/up/boss) local teleports.
    /// </summary>
    public sealed class NascenceDungeon4InteractionHandler
    {
        public static readonly NascenceDungeon4InteractionHandler Default =
            new NascenceDungeon4InteractionHandler();

        // Capture 20260830-143801 terminal instances — paired down/up/boss platforms.
        // Button (boss) 57FCC5AF @ (578.6,52.0083,189.01) → (130.8566,52.0160,129.2647)
        // Button (down) 57FCC5AB @ (958.5,60.005,213.8) → (685.01,52.01,216.8)
        // Button (down) 57FCC5A7 @ (1289.01,52.0083,121.4) → (845.01,52.01,226.8)
        // Button (up)   57FCC5B0 @ (130.8566,52.016,129.2647) → (578.6,52.0083,189.01)
        // Button (up)   57FCC5AC @ (685.01,52.01,216.8) → (958.5,60.005,213.8)
        // Button (up)   57FCC5A8 @ (845.01,52.01,226.8) → (1289.01,52.0083,121.4)
        private const int ButtonBoss = unchecked((int)0x57FCC5AF);

        private const int ButtonDownMid = unchecked((int)0x57FCC5AB);

        private const int ButtonDownEntry = unchecked((int)0x57FCC5A7);

        private const int ButtonUpBoss = unchecked((int)0x57FCC5B0);

        private const int ButtonUpMid = unchecked((int)0x57FCC5AC);

        private const int ButtonUpEntry = unchecked((int)0x57FCC5A8);

        private NascenceDungeon4InteractionHandler()
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
                || !NascenceDungeon4Rules.IsDungeonPlayfield(character.Playfield.Identity.Instance))
            {
                return false;
            }

            if (target.Type == IdentityType.Door
                && NascenceDungeon4DoorCapture.IsInteriorDoorInstance(target.Instance))
            {
                // Capture 20260830-143801 door Use: GenericCmd ACK Temp1=2 only.
                GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                ClientActionBusyRuntime.Clear(character);
                LogUtil.Debug(
                    DebugInfoDetail.Zoning,
                    "NascenceDungeon4 door Use ACK Temp1=2 door=" + target.Instance.ToString("X8"));
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
                NascenceDungeon4DoorReplay.RefreshFloorButtonsAfterTeleport(
                    zoneClient,
                    character,
                    target.Instance);
                NascenceDungeon4DoorReplay.RevealZoneAtPosition(zoneClient, character, destX, destZ);
                if (IsBossDownButton(target.Instance))
                {
                    NascenceDungeon4DoorReplay.RevealBossWingForCharacter(zoneClient, character);
                    var playfieldForBoss = character.Playfield as Playfield;
                    if (playfieldForBoss != null)
                    {
                        NascenceDungeon4BossRoomRuntime.ForceHavarisVisible(playfieldForBoss, character);
                    }
                }

                var playfield = character.Playfield as Playfield;
                if (playfield != null)
                {
                    playfield.RefreshCharacterVisibility(character);
                }
            }

            ClientActionBusyRuntime.Clear(character);

            LogUtil.Debug(
                DebugInfoDetail.Zoning,
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "NascenceDungeon4 button Use terminal={0:X8} dest=({1:F3},{2:F3},{3:F3})",
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
                case ButtonBoss:
                    x = 130.8566f;
                    y = 52.0160f;
                    z = 129.2647f;
                    return true;
                case ButtonDownMid:
                    x = 685.0100f;
                    y = 52.0100f;
                    z = 216.8000f;
                    return true;
                case ButtonDownEntry:
                    x = 845.0100f;
                    y = 52.0100f;
                    z = 226.8000f;
                    return true;
                case ButtonUpBoss:
                    x = 578.6000f;
                    y = 52.0083f;
                    z = 189.0100f;
                    return true;
                case ButtonUpMid:
                    x = 958.5000f;
                    y = 60.0050f;
                    z = 213.8000f;
                    return true;
                case ButtonUpEntry:
                    x = 1289.0100f;
                    y = 52.0083f;
                    z = 121.4000f;
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
