namespace AORebirth.LinuxBuild.Stage7MySqlSecurityIntegrationTests
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;

    using AORebirth.Database;

    internal sealed class Stage7SecurityContractException : Exception
    {
        internal Stage7SecurityContractException(string code)
            : base("Stage 7.1 security contract failed.")
        {
            this.Code = code;
        }

        internal string Code { get; private set; }
    }

    internal sealed class Stage7SecurityDatabaseBaseline
    {
        internal static readonly string[] CharacterIdTables =
        {
            "missionflags",
            "missionstates",
            "missionobjectiveprogress",
            "missionobjectiveobservations",
            "missionrewardledger",
            "characterstimers",
            "charactersactivenanos",
            "charactersmeshs",
            "charactersuploadednanos",
            "charactersperks"
        };

        internal static readonly string[] ContainerTypeTables =
        {
            "items",
            "instanceditems"
        };

        internal static readonly string[] TrackedTables =
        {
            "login",
            "characters",
            "organizations",
            "receivedmessages",
            "stats",
            "items",
            "instanceditems",
            "missionflags",
            "missionstates",
            "missionobjectiveprogress",
            "missionobjectiveobservations",
            "missionrewardledger",
            "characterstimers",
            "charactersactivenanos",
            "charactersmeshs",
            "charactersuploadednanos",
            "charactersperks"
        };

        private readonly IDictionary<string, TableFingerprint> fingerprints;

        private Stage7SecurityDatabaseBaseline(IDictionary<string, TableFingerprint> fingerprints)
        {
            this.fingerprints = fingerprints;
        }

        internal bool IsEmpty
        {
            get
            {
                return this.fingerprints.Values.All(value => value.RowCount == 0);
            }
        }

        internal static Stage7SecurityDatabaseBaseline Capture()
        {
            var fingerprints = new Dictionary<string, TableFingerprint>(StringComparer.Ordinal);
            using (IDbConnection connection = Connector.GetConnection())
            {
                foreach (string tableName in TrackedTables)
                {
                    fingerprints.Add(tableName, CaptureTable(connection, tableName));
                }
            }

            return new Stage7SecurityDatabaseBaseline(fingerprints);
        }

        internal bool Matches(Stage7SecurityDatabaseBaseline other)
        {
            if (other == null || this.fingerprints.Count != other.fingerprints.Count)
            {
                return false;
            }

            foreach (KeyValuePair<string, TableFingerprint> pair in this.fingerprints)
            {
                TableFingerprint otherFingerprint;
                if (!other.fingerprints.TryGetValue(pair.Key, out otherFingerprint)
                    || pair.Value.RowCount != otherFingerprint.RowCount
                    || !string.Equals(pair.Value.Hash, otherFingerprint.Hash, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static TableFingerprint CaptureTable(IDbConnection connection, string tableName)
        {
            var rows = new List<string>();
            using (IDbCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT * FROM `" + tableName + "`";
                using (IDataReader reader = command.ExecuteReader())
                {
                    string columns = string.Join(
                        "|",
                        Enumerable.Range(0, reader.FieldCount)
                            .Select(index => EncodeText(reader.GetName(index))));
                    while (reader.Read())
                    {
                        var values = new string[reader.FieldCount];
                        for (int index = 0; index < reader.FieldCount; index++)
                        {
                            values[index] = EncodeValue(reader.GetValue(index));
                        }

                        rows.Add(columns + "#" + string.Join("|", values));
                    }
                }
            }

            rows.Sort(StringComparer.Ordinal);
            string payload = string.Join("\n", rows);
            using (SHA256 sha256 = SHA256.Create())
            {
                return new TableFingerprint(
                    rows.Count,
                    Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(payload))));
            }
        }

        private static string EncodeValue(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return "null";
            }

            var bytes = value as byte[];
            if (bytes != null)
            {
                return "bytes:" + Convert.ToBase64String(bytes);
            }

            string text;
            var dateTime = value as DateTime?;
            if (dateTime.HasValue)
            {
                text = dateTime.Value.ToString("O", CultureInfo.InvariantCulture);
            }
            else
            {
                var formattable = value as IFormattable;
                text = formattable == null
                           ? Convert.ToString(value, CultureInfo.InvariantCulture)
                           : formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            return value.GetType().FullName + ":" + EncodeText(text ?? string.Empty);
        }

        private static string EncodeText(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private sealed class TableFingerprint
        {
            internal TableFingerprint(int rowCount, string hash)
            {
                this.RowCount = rowCount;
                this.Hash = hash;
            }

            internal string Hash { get; private set; }

            internal int RowCount { get; private set; }
        }
    }

    internal static class Stage7SecuritySourceContract
    {
        internal static void Verify()
        {
            string root = FindRepositoryRoot();
            string loginRoot = Path.Combine(root, "AORebirth", "Server", "LoginEngine");
            string databaseRoot = Path.Combine(
                root,
                "AORebirth",
                "Libraries",
                "Source",
                "AORebirth.Database");

            string create = ReadSource(
                Path.Combine(loginRoot, "MessageHandlers", "CreateCharacterHandler.cs"),
                "source-create-handler");
            RequireNoDirectDao(create, "source-create-handler-direct-dao");
            RequireToken(create, "TryGetAuthenticatedAccountName", "source-create-authentication");
            RequireToken(create, "AccountName = authenticatedAccount", "source-create-canonical-account");
            RequireToken(create, "new CharacterName", "source-create-domain-route");

            string delete = ReadSource(
                Path.Combine(loginRoot, "MessageHandlers", "DeleteCharacterHandler.cs"),
                "source-delete-handler");
            RequireNoDirectDao(delete, "source-delete-handler-direct-dao");
            RequireToken(delete, "TryGetAuthenticatedAccountName", "source-delete-authentication");
            RequireToken(
                delete,
                "TryDeleteChar(authenticatedAccount",
                "source-delete-account-scoped-route");
            Require(
                delete.IndexOf("IsCharacterOnAccount", StringComparison.Ordinal) < 0,
                "source-delete-split-ownership-check");

            string select = ReadSource(
                Path.Combine(loginRoot, "MessageHandlers", "SelectCharacterHandler.cs"),
                "source-select-handler");
            int selectAuthentication = select.IndexOf("TryGetAuthenticatedAccountName", StringComparison.Ordinal);
            int selectOwnership = select.IndexOf("IsCharacterOnAccount(authenticatedAccount", StringComparison.Ordinal);
            int selectMutation = select.IndexOf("CharacterDao.Instance", StringComparison.Ordinal);
            Require(
                selectAuthentication >= 0
                && selectOwnership > selectAuthentication
                && selectMutation > selectOwnership,
                "source-select-guard-order");

            string characterName = ReadSource(
                Path.Combine(loginRoot, "Packets", "CharacterName.cs"),
                "source-character-name");
            RequireToken(
                characterName,
                "DeleteForUser(accountName, charid)",
                "source-character-name-account-delete");

            string characterDao = ReadSource(
                Path.Combine(databaseRoot, "Dao", "CharacterDao.cs"),
                "source-character-dao");
            int deleteForUserStart = characterDao.IndexOf("internal bool DeleteForUser", StringComparison.Ordinal);
            int deleteOwnedDataStart = characterDao.IndexOf("private void DeleteOwnedData", StringComparison.Ordinal);
            Require(
                deleteForUserStart >= 0 && deleteOwnedDataStart > deleteForUserStart,
                "source-delete-for-user-shape");
            string deleteForUser = characterDao.Substring(
                deleteForUserStart,
                deleteOwnedDataStart - deleteForUserStart);
            RequireToken(deleteForUser, "BeginTransaction()", "source-delete-for-user-transaction");
            RequireToken(deleteForUser, "Username = accountName", "source-delete-for-user-owner-query");
            RequireToken(deleteForUser, "DeleteOwnedData(id, connection, transaction)", "source-delete-for-user-owned-data");
            RequireToken(
                deleteForUser,
                "DELETE FROM characters WHERE Id=@Id AND Username=@Username",
                "source-delete-for-user-guarded-delete");

            string deleteOwnedData = characterDao.Substring(deleteOwnedDataStart);
            RequireToken(deleteOwnedData, "OrganizationDao.Instance.GetWhere", "source-delete-owned-organizations");
            RequireToken(deleteOwnedData, "ItemDao.Instance.Delete", "source-delete-owned-items");
            RequireToken(deleteOwnedData, "InstancedItemDao.Instance.Delete", "source-delete-owned-instanced-items");
            RequireToken(
                deleteOwnedData,
                "ReceivedMessagesDao.Instance.Delete(new { PlayerId = id }, connection, transaction)",
                "source-delete-owned-receivedmessages");
            RequireToken(deleteOwnedData, "StatDao.Instance.Delete", "source-delete-owned-stats");
            foreach (string tableName in Stage7SecurityDatabaseBaseline.CharacterIdTables)
            {
                RequireToken(
                    deleteOwnedData,
                    "DELETE FROM " + tableName + " WHERE CharacterId=@CharacterId",
                    "source-delete-owned-" + tableName);
            }
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "AI_START_HERE.md"))
                    && Directory.Exists(Path.Combine(directory.FullName, "LinuxBuild")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new Stage7SecurityContractException("source-repository-root");
        }

        private static string ReadSource(string path, string code)
        {
            Require(File.Exists(path), code);
            string source = File.ReadAllText(path);
            source = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            return Regex.Replace(source, @"//.*?$", string.Empty, RegexOptions.Multiline);
        }

        private static void RequireNoDirectDao(string source, string code)
        {
            Require(
                source.IndexOf("AORebirth.Database", StringComparison.Ordinal) < 0
                && source.IndexOf("CharacterDao", StringComparison.Ordinal) < 0
                && source.IndexOf("LoginDataDao", StringComparison.Ordinal) < 0,
                code);
        }

        private static void RequireToken(string source, string token, string code)
        {
            Require(source.IndexOf(token, StringComparison.Ordinal) >= 0, code);
        }

        private static void Require(bool condition, string code)
        {
            if (!condition)
            {
                throw new Stage7SecurityContractException(code);
            }
        }
    }
}
