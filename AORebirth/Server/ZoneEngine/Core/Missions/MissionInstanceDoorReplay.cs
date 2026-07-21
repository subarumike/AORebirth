namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;
    using AORebirth.Core.Playfields;

    using Utility;

    #endregion

    /// <summary>
    /// Replays capture DoorFullUpdate + ChestFullUpdate packets into a mission instance.
    /// Prefers shape-specific packets from <see cref="MissionInstanceDynelCapture"/>
    /// (capture 20260719-5-different-shape-fo-mish); falls back to legacy door-only capture.
    /// </summary>
    internal static class MissionInstanceDoorReplay
    {
        public static void SendForCharacter(IZoneClient client, ICharacter character)
        {
            if (client == null || character == null || character.Playfield == null)
            {
                return;
            }

            if (!MissionInstanceService.IsMissionInstancePlayfield(character.Playfield.Identity.Instance))
            {
                return;
            }

            var zoneClient = client as ZoneClient;
            if (zoneClient == null)
            {
                return;
            }

            int pf = character.Playfield.Identity.Instance;
            MissionShape shape = MissionInstanceShapeCatalog.PickShape(pf, null);
            int shapePf = shape != null ? shape.CapturedPlayfieldId : pf;
            int sent = 0;
            try
            {
                string[] doors = MissionInstanceDynelCapture.GetDoors(shapePf);
                string[] chests = MissionInstanceDynelCapture.GetChests(shapePf);
                if (doors == null || doors.Length == 0)
                {
                    doors = MissionInstanceDoorCapture.CapturedDoorPacketHex;
                    chests = null;
                }

                sent += SendPackets(
                    zoneClient,
                    character,
                    doors,
                    MissionInstanceDynelCapture.CapturedCharacterInstance);
                sent += SendPackets(
                    zoneClient,
                    character,
                    chests,
                    MissionInstanceDynelCapture.CapturedCharacterInstance);
            }
            catch (Exception ex)
            {
                LogUtil.ErrorException(ex);
            }

            MissionDiagnostics.Log(
                "DOOR-CHEST-REPLAY char={0} pf={1} sent={2}",
                character.Identity.Instance,
                pf,
                sent);
        }

        private static int SendPackets(
            ZoneClient zoneClient,
            ICharacter character,
            string[] hexPackets,
            int capturedCharacterInstance)
        {
            if (hexPackets == null || hexPackets.Length == 0)
            {
                return 0;
            }

            int sent = 0;
            foreach (string hex in hexPackets)
            {
                if (string.IsNullOrEmpty(hex))
                {
                    continue;
                }

                byte[] packet = HexToBytes(hex);
                ReplaceInstance(packet, capturedCharacterInstance, character.Identity.Instance);
                // Also retarget legacy door capture character if present.
                ReplaceInstance(
                    packet,
                    MissionInstanceDoorCapture.CapturedCharacterInstance,
                    character.Identity.Instance);
                // Retarget captured playfield id embedded in packet to live instance pf.
                RetargetPlayfieldIds(packet, character.Playfield.Identity.Instance);
                zoneClient.SendCompressed(packet);
                sent++;
            }

            return sent;
        }

        private static void RetargetPlayfieldIds(byte[] packet, int livePlayfieldId)
        {
            int[] captured = MissionInstanceDynelCapture.ShapePlayfieldIds;
            for (int c = 0; c < captured.Length; c++)
            {
                ReplaceInstance(packet, captured[c], livePlayfieldId);
            }

            // Legacy door capture pf 1413198 (0x15904E).
            ReplaceInstance(packet, 1413198, livePlayfieldId);
        }

        private static void ReplaceInstance(byte[] packet, int from, int to)
        {
            if (packet == null || from == to)
            {
                return;
            }

            byte f0 = (byte)(from >> 24);
            byte f1 = (byte)(from >> 16);
            byte f2 = (byte)(from >> 8);
            byte f3 = (byte)from;

            for (int i = 0; i + 4 <= packet.Length; i++)
            {
                if (packet[i] == f0 && packet[i + 1] == f1 && packet[i + 2] == f2 && packet[i + 3] == f3)
                {
                    packet[i] = (byte)(to >> 24);
                    packet[i + 1] = (byte)(to >> 16);
                    packet[i + 2] = (byte)(to >> 8);
                    packet[i + 3] = (byte)to;
                }
            }
        }

        private static byte[] HexToBytes(string hex)
        {
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = byte.Parse(
                    hex.Substring(i * 2, 2),
                    System.Globalization.NumberStyles.HexNumber);
            }

            return bytes;
        }
    }
}
