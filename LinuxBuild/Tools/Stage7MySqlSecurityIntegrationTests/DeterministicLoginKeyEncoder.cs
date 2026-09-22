namespace AORebirth.LinuxBuild.Stage7MySqlSecurityIntegrationTests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Text;

    using AO.Core.Encryption;

    internal static class DeterministicLoginKeyEncoder
    {
        private const string ClientPublicKeyHex = "2";
        private const string PrimeHex =
            "eca2e8c85d863dcdc26a429a71a9815ad052f6139669dd659f98ae159d313d13c6bf2838e10a69b6478b64a24bd054ba8248e8fa778703b418408249440b2c1edd28853e240d8a7e49540b76d120d3b1ad2878b1b99490eb4a2a5e84caa8a91cecbdb1aa7c816e8be343246f80c637abc653b893fd91686cf8d32d6cfe5f2a6f";
        private const string ServerPrivateKeyHex =
            "7ad852c6494f664e8df21446285ecd6f400cf20e1d872ee96136d7744887424b";

        internal static string Create(string username, string password, byte[] salt)
        {
            byte[] usernameBytes = GetAscii(username, "username");
            byte[] passwordBytes = GetAscii(password, "password");
            Require(usernameBytes.Length > 0, "username-empty");
            Require(passwordBytes.Length > 0, "password-empty");
            Require(Array.IndexOf(usernameBytes, (byte)'|') < 0, "username-delimiter");
            Require(Array.IndexOf(passwordBytes, (byte)'|') < 0, "password-delimiter");
            Require(salt != null && salt.Length == 32, "salt-size");

            for (int index = 0; index < salt.Length; index++)
            {
                Require(salt[index] != 0, "salt-zero");
            }

            int dataLength = checked(usernameBytes.Length + 34 + passwordBytes.Length);
            var plaintext = new List<byte>();
            plaintext.AddRange(new byte[8]);
            plaintext.Add((byte)((dataLength >> 24) & 0xff));
            plaintext.Add((byte)((dataLength >> 16) & 0xff));
            plaintext.Add((byte)((dataLength >> 8) & 0xff));
            plaintext.Add((byte)(dataLength & 0xff));
            plaintext.AddRange(usernameBytes);
            plaintext.Add((byte)'|');
            plaintext.AddRange(salt);
            plaintext.Add((byte)'|');
            plaintext.AddRange(passwordBytes);
            while ((plaintext.Count & 7) != 0)
            {
                plaintext.Add(0);
            }

            uint[] teaKey = CreateTeaKey();
            uint previousLeft = 0;
            uint previousRight = 0;
            var encrypted = new StringBuilder(plaintext.Count * 2);
            byte[] bytes = plaintext.ToArray();
            for (int offset = 0; offset < bytes.Length; offset += 8)
            {
                uint left = ReadUInt32LittleEndian(bytes, offset) ^ previousLeft;
                uint right = ReadUInt32LittleEndian(bytes, offset + 4) ^ previousRight;
                EncryptTeaRound(ref left, ref right, teaKey);
                encrypted.Append(ToNetworkHex(left));
                encrypted.Append(ToNetworkHex(right));
                previousLeft = left;
                previousRight = right;
            }

            return ClientPublicKeyHex + "-" + encrypted;
        }

        private static byte[] GetAscii(string value, string field)
        {
            Require(value != null, field + "-null");
            byte[] bytes = Encoding.ASCII.GetBytes(value);
            Require(string.Equals(Encoding.ASCII.GetString(bytes), value, StringComparison.Ordinal), field + "-ascii");
            return bytes;
        }

        private static uint[] CreateTeaKey()
        {
            var clientPublicKey = new BigInteger(ClientPublicKeyHex, 16);
            var serverPrivateKey = new BigInteger(ServerPrivateKeyHex, 16);
            var prime = new BigInteger(PrimeHex, 16);
            string keyText = clientPublicKey.modPow(serverPrivateKey, prime).ToString(16).ToLowerInvariant();
            Require(keyText.Length >= 32, "shared-secret-too-short");
            if (keyText.Length > 32)
            {
                keyText = keyText.Substring(0, 32);
            }

            var key = new uint[4];
            for (int index = 0; index < key.Length; index++)
            {
                int networkWord = Convert.ToInt32(keyText.Substring(index * 8, 8), 16);
                key[index] = unchecked((uint)IPAddress.NetworkToHostOrder(networkWord));
            }

            return key;
        }

        private static uint ReadUInt32LittleEndian(byte[] bytes, int offset)
        {
            return (uint)(bytes[offset]
                          | (bytes[offset + 1] << 8)
                          | (bytes[offset + 2] << 16)
                          | (bytes[offset + 3] << 24));
        }

        private static string ToNetworkHex(uint value)
        {
            int networkValue = IPAddress.HostToNetworkOrder(unchecked((int)value));
            return unchecked((uint)networkValue).ToString("x8", CultureInfo.InvariantCulture);
        }

        private static void EncryptTeaRound(ref uint left, ref uint right, uint[] key)
        {
            const uint Delta = 0x9e3779b9;
            uint sum = 0;
            for (int round = 0; round < 32; round++)
            {
                unchecked
                {
                    sum += Delta;
                    left += ((right << 4) + key[0]) ^ (right + sum) ^ ((right >> 5) + key[1]);
                    right += ((left << 4) + key[2]) ^ (left + sum) ^ ((left >> 5) + key[3]);
                }
            }
        }

        private static void Require(bool condition, string code)
        {
            if (!condition)
            {
                throw new InvalidOperationException("Stage 7.1 deterministic login-key contract failed: " + code + ".");
            }
        }
    }
}
