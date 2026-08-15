namespace ProductionLoginAcceptance
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Numerics;
    using System.Security.Cryptography;
    using System.Text;

    internal static class Program
    {
        private const string ClientPublicKey =
            "8f2d7c34a0b9e8d6c5f4a3928170615049382716f5e4d3c2b1a09876543210fedcba98765432100123456789abcdef";

        private const string ServerPrivateKey =
            "7ad852c6494f664e8df21446285ecd6f400cf20e1d872ee96136d7744887424b";

        private const string Prime =
            "eca2e8c85d863dcdc26a429a71a9815ad052f6139669dd659f98ae159d313d13c6bf2838e10a69b6478b64a24bd054ba8248e8fa778703b418408249440b2c1edd28853e240d8a7e49540b76d120d3b1ad2878b1b99490eb4a2a5e84caa8a91cecbdb1aa7c816e8be343246f80c637abc653b893fd91686cf8d32d6cfe5f2a6f";

        private const int PacketTypeSystemMessage = 0x0001;
        private const int SystemUserLogin = 0x00000022;
        private const int SystemServerSalt = 0x00000024;
        private const int SystemUserCredentials = 0x00000025;
        private const int SystemLoginError = 0x0000000D;
        private const int SystemCharacterList = 0x0000000E;

        private static int Main(string[] args)
        {
            try
            {
                string host = GetArg(args, "--host") ?? Environment.GetEnvironmentVariable("AOR_PRODUCTION_LOGIN_HOST") ?? "2.24.96.30";
                int port = int.Parse(GetArg(args, "--port") ?? Environment.GetEnvironmentVariable("AOR_PRODUCTION_LOGIN_PORT") ?? "7500", CultureInfo.InvariantCulture);
                string credentialsFile = GetArg(args, "--credentials-file") ?? Environment.GetEnvironmentVariable("AOR_ACCEPTANCE_CREDENTIALS_FILE");
                if (string.IsNullOrWhiteSpace(credentialsFile))
                {
                    Console.WriteLine("FAIL missing credentials file");
                    return 2;
                }

                string[] credentialLines = File.ReadAllLines(credentialsFile);
                if (credentialLines.Length < 2)
                {
                    Console.WriteLine("FAIL invalid credentials file");
                    return 2;
                }

                string username = credentialLines[0].Trim();
                string password = credentialLines[1];
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
                {
                    Console.WriteLine("FAIL invalid credentials content");
                    return 2;
                }

                AcceptanceResult correct = Attempt(host, port, username, password);
                AcceptanceResult incorrect = Attempt(host, port, username, password + "-wrong");

                Console.WriteLine("REAL_PROTOCOL_EXERCISED=YES");
                Console.WriteLine("CORRECT_PASSWORD=" + (correct.Accepted ? "PASS" : "FAIL"));
                Console.WriteLine("CORRECT_PASSWORD_STAGE=" + correct.Stage);
                Console.WriteLine("INCORRECT_PASSWORD=" + (!incorrect.Accepted && incorrect.LoginErrorReceived ? "PASS" : "FAIL"));
                Console.WriteLine("INCORRECT_PASSWORD_STAGE=" + incorrect.Stage);

                if (correct.Accepted && !incorrect.Accepted && incorrect.LoginErrorReceived)
                {
                    Console.WriteLine("PASS ProductionLoginAcceptance 2/2");
                    return 0;
                }

                Console.WriteLine("FAIL ProductionLoginAcceptance");
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL " + ex.GetType().Name);
                return 1;
            }
        }

        private static AcceptanceResult Attempt(string host, int port, string username, string password)
        {
            using (var client = new TcpClient())
            {
                client.ReceiveTimeout = 8000;
                client.SendTimeout = 8000;
                client.Connect(host, port);

                using (NetworkStream stream = client.GetStream())
                {
                    WritePacket(stream, CreateUserLoginPacket(username, "18.8.55.1_EP1"));
                    Packet saltPacket = ReadPacket(stream);
                    if (saltPacket.SystemMessageType != SystemServerSalt || saltPacket.Body.Length < 36)
                    {
                        return new AcceptanceResult(false, false, "NO_SERVER_SALT");
                    }

                    byte[] salt = new byte[32];
                    Buffer.BlockCopy(saltPacket.Body, 4, salt, 0, salt.Length);
                    string serverSaltHex = BytesToHex(salt);
                    string loginKey = CreateLoginKey(username, password, serverSaltHex);

                    WritePacket(stream, CreateUserCredentialsPacket(username, loginKey));
                    Packet response = ReadPacket(stream);
                    if (response.SystemMessageType == SystemCharacterList)
                    {
                        return new AcceptanceResult(true, false, "CHARACTER_LIST");
                    }

                    if (response.SystemMessageType == SystemLoginError)
                    {
                        return new AcceptanceResult(false, true, "LOGIN_ERROR");
                    }

                    return new AcceptanceResult(false, false, "UNEXPECTED_SYSTEM_" + response.SystemMessageType.ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        private static byte[] CreateUserLoginPacket(string username, string clientVersion)
        {
            using (var body = new MemoryStream())
            {
                WriteInt32(body, SystemUserLogin);
                WriteInt32(body, 2);
                WriteFixedAscii(body, username, 40);
                WriteFixedAscii(body, clientVersion, 20);
                return CreateSystemPacket(1, 0, body.ToArray());
            }
        }

        private static byte[] CreateUserCredentialsPacket(string username, string credentials)
        {
            using (var body = new MemoryStream())
            {
                WriteInt32(body, SystemUserCredentials);
                WriteFixedAscii(body, username, 40);
                WriteInt32(body, credentials.Length);
                WriteFixedAscii(body, credentials, credentials.Length);
                return CreateSystemPacket(2, 0, body.ToArray());
            }
        }

        private static byte[] CreateSystemPacket(ushort messageId, int receiver, byte[] body)
        {
            int size = 16 + body.Length;
            int paddedSize = size % 4 == 0 ? size : size + (4 - (size % 4));
            using (var packet = new MemoryStream())
            {
                WriteUInt16(packet, messageId);
                WriteInt16(packet, PacketTypeSystemMessage);
                WriteInt16(packet, 1);
                WriteInt16(packet, size);
                WriteInt32(packet, 0);
                WriteInt32(packet, receiver);
                packet.Write(body, 0, body.Length);
                while (packet.Length < paddedSize)
                {
                    packet.WriteByte(0);
                }

                return packet.ToArray();
            }
        }

        private static void WritePacket(NetworkStream stream, byte[] packet)
        {
            stream.Write(packet, 0, packet.Length);
            stream.Flush();
        }

        private static Packet ReadPacket(NetworkStream stream)
        {
            byte[] header = ReadExact(stream, 16);
            short size = ReadInt16(header, 6);
            if (size < 20)
            {
                throw new InvalidDataException("Invalid packet size.");
            }

            int paddedSize = size % 4 == 0 ? size : size + (4 - (size % 4));
            byte[] rest = ReadExact(stream, paddedSize - 16);
            byte[] body = new byte[size - 16];
            Buffer.BlockCopy(rest, 0, body, 0, body.Length);
            return new Packet(ReadInt32(body, 0), body);
        }

        private static byte[] ReadExact(NetworkStream stream, int length)
        {
            byte[] buffer = new byte[length];
            int offset = 0;
            while (offset < length)
            {
                int read = stream.Read(buffer, offset, length - offset);
                if (read <= 0)
                {
                    throw new EndOfStreamException();
                }

                offset += read;
            }

            return buffer;
        }

        private static string CreateLoginKey(string username, string password, string serverSalt)
        {
            string teaKey = ComputeTeaKey();
            string plaintext = CreatePlaintext(username, password, serverSalt);
            string encryptedBlock = EncryptTea(plaintext, teaKey);
            return ClientPublicKey + "-" + encryptedBlock;
        }

        private static string ComputeTeaKey()
        {
            BigInteger clientPublicKey = ParsePositiveHex(ClientPublicKey);
            BigInteger serverPrivateKey = ParsePositiveHex(ServerPrivateKey);
            BigInteger prime = ParsePositiveHex(Prime);
            string teaKey = BigInteger.ModPow(clientPublicKey, serverPrivateKey, prime).ToString("x").ToLowerInvariant();
            if (teaKey.Length < 32)
            {
                teaKey = teaKey.PadLeft(32, '0');
            }
            else if (teaKey.Length > 32)
            {
                teaKey = teaKey.Substring(0, 32);
            }

            return teaKey;
        }

        private static BigInteger ParsePositiveHex(string hex)
        {
            if (hex.Length % 2 != 0)
            {
                hex = "0" + hex;
            }

            byte[] bytes = new byte[(hex.Length / 2) + 1];
            for (int source = 0, target = bytes.Length - 2; source < hex.Length; source += 2, target--)
            {
                bytes[target] = byte.Parse(hex.Substring(source, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return new BigInteger(bytes);
        }

        private static string CreatePlaintext(string username, string password, string serverSalt)
        {
            string saltBytes = SaltHexToBytes(serverSalt);
            int dataLength = username.Length + password.Length + 34;
            var sb = new StringBuilder();
            sb.Append("AOLOGIN!");
            sb.Append(IntToBigEndianString(dataLength));
            sb.Append(username);
            sb.Append('|');
            sb.Append(saltBytes);
            sb.Append('|');
            sb.Append(password);
            while (sb.Length % 8 != 0)
            {
                sb.Append('\0');
            }

            return sb.ToString();
        }

        private static string SaltHexToBytes(string serverSalt)
        {
            if (serverSalt == null || serverSalt.Length != 64)
            {
                throw new ArgumentException("Server salt must be 64 hex characters.", "serverSalt");
            }

            var sb = new StringBuilder(32);
            for (int index = 0; index < serverSalt.Length; index += 8)
            {
                uint value = uint.Parse(serverSalt.Substring(index, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                sb.Append((char)((value >> 24) & 0xFF));
                sb.Append((char)((value >> 16) & 0xFF));
                sb.Append((char)((value >> 8) & 0xFF));
                sb.Append((char)(value & 0xFF));
            }

            return sb.ToString();
        }

        private static string IntToBigEndianString(int value)
        {
            var sb = new StringBuilder(4);
            sb.Append((char)((value >> 24) & 0xFF));
            sb.Append((char)((value >> 16) & 0xFF));
            sb.Append((char)((value >> 8) & 0xFF));
            sb.Append((char)(value & 0xFF));
            return sb.ToString();
        }

        private static string EncryptTea(string plaintext, string key)
        {
            uint[] keyInt = ConvertHexKeyToUInts(key);
            uint[] previous = { 0U, 0U };
            var encrypted = new StringBuilder();

            for (int index = 0; index < plaintext.Length; index += 8)
            {
                uint[] block =
                {
                    PlaintextToUInt(plaintext, index) ^ previous[0],
                    PlaintextToUInt(plaintext, index + 4) ^ previous[1]
                };

                EncryptTeaRound(block, keyInt);
                encrypted.Append(UIntToNetworkHex(block[0]));
                encrypted.Append(UIntToNetworkHex(block[1]));
                previous[0] = block[0];
                previous[1] = block[1];
            }

            return encrypted.ToString();
        }

        private static uint[] ConvertHexKeyToUInts(string key)
        {
            return new[]
            {
                ConvertHexToUInt(key.Substring(0, 8)),
                ConvertHexToUInt(key.Substring(8, 8)),
                ConvertHexToUInt(key.Substring(16, 8)),
                ConvertHexToUInt(key.Substring(24, 8))
            };
        }

        private static uint PlaintextToUInt(string input, int index)
        {
            return (uint)input[index]
                   | ((uint)input[index + 1] << 8)
                   | ((uint)input[index + 2] << 16)
                   | ((uint)input[index + 3] << 24);
        }

        private static uint ConvertHexToUInt(string hexInput)
        {
            return unchecked((uint)IPAddress.NetworkToHostOrder(unchecked((int)uint.Parse(hexInput, NumberStyles.HexNumber, CultureInfo.InvariantCulture))));
        }

        private static string UIntToNetworkHex(uint value)
        {
            int networkValue = IPAddress.HostToNetworkOrder(unchecked((int)value));
            return unchecked((uint)networkValue).ToString("x8", CultureInfo.InvariantCulture);
        }

        private static void EncryptTeaRound(uint[] data, uint[] key)
        {
            uint n = 32;
            uint sum = 0;
            const uint Delta = 0x9e3779b9;

            while (n-- > 0)
            {
                sum += Delta;
                data[0] += ((data[1] << 4) + key[0]) ^ (data[1] + sum) ^ ((data[1] >> 5) + key[1]);
                data[1] += ((data[0] << 4) + key[2]) ^ (data[0] + sum) ^ ((data[0] >> 5) + key[3]);
            }
        }

        private static string BytesToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static void WriteFixedAscii(Stream stream, string value, int length)
        {
            byte[] bytes = new byte[length];
            int count = Math.Min(value.Length, length);
            Encoding.ASCII.GetBytes(value, 0, count, bytes, 0);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void WriteUInt16(Stream stream, int value)
        {
            stream.WriteByte((byte)((value >> 8) & 0xFF));
            stream.WriteByte((byte)(value & 0xFF));
        }

        private static void WriteInt16(Stream stream, int value)
        {
            WriteUInt16(stream, unchecked((ushort)value));
        }

        private static void WriteInt32(Stream stream, int value)
        {
            stream.WriteByte((byte)((value >> 24) & 0xFF));
            stream.WriteByte((byte)((value >> 16) & 0xFF));
            stream.WriteByte((byte)((value >> 8) & 0xFF));
            stream.WriteByte((byte)(value & 0xFF));
        }

        private static short ReadInt16(byte[] buffer, int offset)
        {
            return unchecked((short)((buffer[offset] << 8) | buffer[offset + 1]));
        }

        private static int ReadInt32(byte[] buffer, int offset)
        {
            return (buffer[offset] << 24)
                   | (buffer[offset + 1] << 16)
                   | (buffer[offset + 2] << 8)
                   | buffer[offset + 3];
        }

        private static string GetArg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private sealed class Packet
        {
            public Packet(int systemMessageType, byte[] body)
            {
                this.SystemMessageType = systemMessageType;
                this.Body = body;
            }

            public int SystemMessageType { get; private set; }

            public byte[] Body { get; private set; }
        }

        private sealed class AcceptanceResult
        {
            public AcceptanceResult(bool accepted, bool loginErrorReceived, string stage)
            {
                this.Accepted = accepted;
                this.LoginErrorReceived = loginErrorReceived;
                this.Stage = stage;
            }

            public bool Accepted { get; private set; }

            public bool LoginErrorReceived { get; private set; }

            public string Stage { get; private set; }
        }
    }
}
