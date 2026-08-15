namespace LoginAuthenticationValidation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Text;

    using AO.Core.Encryption;

    using LoginEngine.Packets;

    internal static class Program
    {
        private const string ServerPrivateKey =
            "7ad852c6494f664e8df21446285ecd6f400cf20e1d872ee96136d7744887424b";

        private const string Prime =
            "eca2e8c85d863dcdc26a429a71a9815ad052f6139669dd659f98ae159d313d13c6bf2838e10a69b6478b64a24bd054ba8248e8fa778703b418408249440b2c1edd28853e240d8a7e49540b76d120d3b1ad2878b1b99490eb4a2a5e84caa8a91cecbdb1aa7c816e8be343246f80c637abc653b893fd91686cf8d32d6cfe5f2a6f";

        private const string ClientPublicKey =
            "8f2d7c34a0b9e8d6c5f4a3928170615049382716f5e4d3c2b1a09876543210fedcba98765432100123456789abcdef";

        private const string ServerSalt =
            "00112233445566778899aabbccddeeff102132435465768798a9babbdcedfe0f";

        private static int Main()
        {
            var failures = new List<string>();

            string account = "PlayerOne";
            string password = "Correct-Password-123!";
            string longPassword = new string('P', 96) + "!9";
            var encryption = new LoginEncryption();
            string storedHash = encryption.GeneratePasswordHash(password);
            string blankHash = encryption.GeneratePasswordHash(string.Empty);
            string longHash = encryption.GeneratePasswordHash(longPassword);

            Expect(
                "correct password",
                true,
                Validate(account, password, storedHash, account),
                failures);
            Expect(
                "incorrect password",
                false,
                Validate(account, "Wrong-Password-123!", storedHash, account),
                failures);
            Expect(
                "blank password against nonblank hash",
                false,
                Validate(account, string.Empty, storedHash, account),
                failures);
            Expect(
                "case-different password",
                false,
                Validate(account, "correct-password-123!", storedHash, account),
                failures);
            Expect(
                "special-character password",
                true,
                Validate(account, password, storedHash, account),
                failures);
            Expect(
                "long valid password",
                true,
                Validate(account, longPassword, longHash, account),
                failures);
            Expect(
                "blank generated password compatibility",
                true,
                Validate(account, string.Empty, blankHash, account),
                failures);
            Expect(
                "malformed stored hash",
                false,
                Validate(account, password, "not-a-valid-hash", account),
                failures);
            Expect(
                "empty stored hash",
                false,
                Validate(account, password, string.Empty, account),
                failures);
            Expect(
                "nonexistent account equivalent empty hash",
                false,
                Validate(account, password, string.Empty, account),
                failures);
            Expect(
                "username case variation matching credential case",
                true,
                Validate("PLAYERONE", password, storedHash, "PLAYERONE"),
                failures);
            Expect(
                "credential username mismatch",
                false,
                Validate("PLAYERONE", password, storedHash, "playerone"),
                failures);
            Expect(
                "server salt mismatch",
                false,
                ValidateWithCredentialSalt(
                    account,
                    password,
                    storedHash,
                    account,
                    ServerSalt,
                    "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"),
                failures);
            Expect(
                "malformed credential payload",
                false,
                encryption.IsValidLogin("not-a-login-key", ServerSalt, account, storedHash),
                failures);

            if (failures.Count > 0)
            {
                foreach (string failure in failures)
                {
                    Console.WriteLine("FAIL " + failure);
                }

                return 1;
            }

            Console.WriteLine("PASS LoginAuthenticationValidation 14/14");
            return 0;
        }

        private static bool Validate(
            string accountName,
            string suppliedPassword,
            string storedHash,
            string credentialAccountName,
            string serverSalt = ServerSalt)
        {
            string loginKey = CreateLoginKey(credentialAccountName, suppliedPassword, serverSalt);
            return CheckLogin.IsLoginCorrect(loginKey, serverSalt, accountName, storedHash);
        }

        private static bool ValidateWithCredentialSalt(
            string accountName,
            string suppliedPassword,
            string storedHash,
            string credentialAccountName,
            string credentialServerSalt,
            string validationServerSalt)
        {
            string loginKey = CreateLoginKey(credentialAccountName, suppliedPassword, credentialServerSalt);
            return CheckLogin.IsLoginCorrect(
                loginKey,
                validationServerSalt,
                accountName,
                storedHash);
        }

        private static void Expect(string name, bool expected, bool actual, IList<string> failures)
        {
            if (actual != expected)
            {
                failures.Add(name + " expected " + expected + " actual " + actual);
            }
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
            var clientPublicKey = new BigInteger(ClientPublicKey, 16);
            var serverPrivateKey = new BigInteger(ServerPrivateKey, 16);
            var prime = new BigInteger(Prime, 16);
            string teaKey = clientPublicKey.modPow(serverPrivateKey, prime).ToString(16).ToLowerInvariant();
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
                uint value = uint.Parse(
                    serverSalt.Substring(index, 8),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture);
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
            return (uint)IPAddress.NetworkToHostOrder(Convert.ToInt32(hexInput, 16));
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
    }
}
