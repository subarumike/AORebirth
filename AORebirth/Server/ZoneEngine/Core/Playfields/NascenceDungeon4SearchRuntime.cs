namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core;

    /// <summary>
    /// Capture 20260824-173734: Normal Action Search reveals hidden C741 mines in SL ACG interior.
    /// </summary>
    internal static class NascenceDungeon4SearchRuntime
    {
        private const float SearchRadiusMeters = 30.0f;

        private const float SearchRadiusSq = SearchRadiusMeters * SearchRadiusMeters;

        // Server→client search feedback before mine Visibility (capture 20260824-173734).
        private const CharacterActionType SearchObjectFoundAction = CharacterActionType.SpecialUsed;

        // Server→client follow-up targeting revealed mine dynel (0xBE).
        private const CharacterActionType SearchTargetMineAction = (CharacterActionType)0xBE;

        private static readonly object Gate = new object();

        private static readonly Dictionary<int, HashSet<int>> RevealedMineInstancesByCharacter =
            new Dictionary<int, HashSet<int>>();

        internal static void ClearForCharacter(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            lock (Gate)
            {
                RevealedMineInstancesByCharacter.Remove(character.Identity.Instance);
            }
        }

        internal static bool TryHandleSearch(ZoneClient client, ICharacter character)
        {
            if (client == null
                || character == null
                || character.Playfield == null
                || !NascenceDungeon4Rules.IsDungeonPlayfield(character.Playfield.Identity.Instance))
            {
                return false;
            }

            float px = (float)character.Position.x;
            float pz = (float)character.Position.z;
            int playfieldInstance = character.Playfield.Identity.Instance;
            int characterInstance = character.Identity.Instance;

            MineCandidate nearest = default(MineCandidate);
            nearest.DistSq = float.MaxValue;
            string[] minePackets = NascenceDungeon4DoorCapture.ZoneInMinePacketHex;
            for (int i = 0; i < minePackets.Length; i++)
            {
                string hex = minePackets[i];
                if (string.IsNullOrEmpty(hex))
                {
                    continue;
                }

                int mineInstance;
                float mx;
                float mz;
                if (!TryParseMine(hex, out mineInstance, out mx, out mz))
                {
                    continue;
                }

                lock (Gate)
                {
                    HashSet<int> revealed;
                    if (RevealedMineInstancesByCharacter.TryGetValue(characterInstance, out revealed)
                        && revealed.Contains(mineInstance))
                    {
                        continue;
                    }
                }

                float dx = mx - px;
                float dz = mz - pz;
                float distSq = (dx * dx) + (dz * dz);
                if (distSq > SearchRadiusSq || distSq >= nearest.DistSq)
                {
                    continue;
                }

                nearest = new MineCandidate
                {
                    Hex = hex,
                    Instance = mineInstance,
                    X = mx,
                    Z = mz,
                    DistSq = distSq
                };
            }

            if (nearest.Hex == null)
            {
                return false;
            }

            lock (Gate)
            {
                HashSet<int> revealed;
                if (!RevealedMineInstancesByCharacter.TryGetValue(characterInstance, out revealed))
                {
                    revealed = new HashSet<int>();
                    RevealedMineInstancesByCharacter[characterInstance] = revealed;
                }

                if (!revealed.Add(nearest.Instance))
                {
                    return false;
                }
            }

            byte[] minePacket = HexToBytes(nearest.Hex);
            ReplaceInstance(minePacket, NascenceDungeon4DoorCapture.CapturedCharacterInstance, characterInstance);
            ReplaceInstance(minePacket, NascenceDungeon4DoorCapture.CapturedPlayfieldId, playfieldInstance);
            ReplaceInstance(minePacket, NascenceDungeon4Rules.DungeonPlayfieldId, playfieldInstance);
            client.SendCompressed(minePacket);

            character.Send(
                new CharacterActionMessage
                {
                    Identity = character.Identity,
                    Action = SearchObjectFoundAction,
                    Target = character.Identity
                });

            character.Send(
                new CharacterActionMessage
                {
                    Identity = character.Identity,
                    Action = SearchTargetMineAction,
                    Target = new Identity
                    {
                        Type = (IdentityType)0xC741,
                        Instance = nearest.Instance
                    }
                });

            return true;
        }

        private static bool TryParseMine(string hex, out int instance, out float x, out float z)
        {
            instance = 0;
            x = z = 0;
            byte[] packet = HexToBytes(hex);
            for (int i = 0; i + 28 <= packet.Length; i++)
            {
                if (packet[i] != 0x00 || packet[i + 1] != 0x00 || packet[i + 2] != 0xC7 || packet[i + 3] != 0x41)
                {
                    continue;
                }

                instance = BitConverter.ToInt32(packet, i + 4);
                int o = i + 8;
                if (packet[o] != 0 || packet[o + 1] != 0 || packet[o + 2] != 0 || packet[o + 3] != 0)
                {
                    continue;
                }

                o += 5;
                if (o + 12 > packet.Length)
                {
                    continue;
                }

                x = BitConverter.ToSingle(packet, o);
                o += 4;
                o += 4;
                z = BitConverter.ToSingle(packet, o);
                return true;
            }

            return false;
        }

        private static void ReplaceInstance(byte[] packet, int capturedInstance, int runtimeInstance)
        {
            if (packet == null || capturedInstance == 0 || runtimeInstance == 0)
            {
                return;
            }

            byte[] capturedBytes = BitConverter.GetBytes(capturedInstance);
            byte[] runtimeBytes = BitConverter.GetBytes(runtimeInstance);
            for (int i = 0; i <= packet.Length - 4; i++)
            {
                if (packet[i] == capturedBytes[0]
                    && packet[i + 1] == capturedBytes[1]
                    && packet[i + 2] == capturedBytes[2]
                    && packet[i + 3] == capturedBytes[3])
                {
                    Buffer.BlockCopy(runtimeBytes, 0, packet, i, 4);
                }
            }
        }

        private static byte[] HexToBytes(string hex)
        {
            int length = hex.Length / 2;
            var bytes = new byte[length];
            for (int i = 0; i < length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }

        private struct MineCandidate
        {
            internal string Hex;

            internal int Instance;

            internal float X;

            internal float Z;

            internal float DistSq;
        }
    }
}
