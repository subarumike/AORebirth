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

    #endregion

    /// <summary>
    /// Capture 20260823-182854: interior door Use (ACK Temp1=2) and
    /// floor Button (down/up/boss) local teleports.
    /// </summary>
    public sealed class NascenceDungeon2InteractionHandler
    {
        public static readonly NascenceDungeon2InteractionHandler Default =
            new NascenceDungeon2InteractionHandler();

        // Capture 20260823-182854 terminal instances — do not remap.
        private const int ButtonDown = unchecked((int)0x57EC6ADF);

        private const int ButtonUpLower = unchecked((int)0x57EC6AE0);

        private const int ButtonBoss = unchecked((int)0x57EC6AE3);

        private const int ButtonUpBoss = unchecked((int)0x57EC6AE4);

        private NascenceDungeon2InteractionHandler()
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
                || !NascenceDungeon2Rules.IsDungeonPlayfield(character.Playfield.Identity.Instance))
            {
                return false;
            }

            if (target.Type == IdentityType.Door
                && NascenceDungeon2DoorCapture.IsInteriorDoorInstance(target.Instance))
            {
                // Capture 20260823-171238 door Use: GenericCmd ACK Temp1=2 only.
                // No DoorStatusUpdate on live — open/close anim is client ACG mesh after ACK.
                GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                LogUtil.Debug(
                    DebugInfoDetail.Zoning,
                    "NascenceDungeon2 door Use ACK Temp1=2 door=" + target.Instance.ToString("X8"));
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
                NascenceDungeon2DoorReplay.RefreshFloorButtonsAfterTeleport(
                    zoneClient,
                    character,
                    target.Instance);
                NascenceDungeon2DoorReplay.RevealZoneAtPosition(zoneClient, character, destX, destZ);
                if (IsBossDownButton(target.Instance))
                {
                    NascenceDungeon2DoorReplay.RevealBossWingForCharacter(zoneClient, character);
                    var playfieldForBoss = character.Playfield as Playfield;
                    if (playfieldForBoss != null)
                    {
                        NascenceDungeon2BossRoomRuntime.ForceHavarisVisible(playfieldForBoss, character);
                    }
                }

                var playfield = character.Playfield as Playfield;
                if (playfield != null)
                {
                    playfield.RefreshCharacterVisibility(character);
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.Zoning,
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "NascenceDungeon2 button Use terminal={0:X8} dest=({1:F3},{2:F3},{3:F3})",
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
                case ButtonDown:
                    x = 685.01f;
                    y = 52.01f;
                    z = 176.8f;
                    return true;
                case ButtonUpLower:
                    x = 957.01f;
                    y = 52.01f;
                    z = 288.6f;
                    return true;
                case ButtonBoss:
                    x = 130.8566f;
                    y = 52.016f;
                    z = 129.2647f;
                    return true;
                case ButtonUpBoss:
                    x = 427.3f;
                    y = 52.01f;
                    z = 288.4f;
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
