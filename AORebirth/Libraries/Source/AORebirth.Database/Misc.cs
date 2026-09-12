#region License

// Copyright (c) 2005-2014, CellAO Team
// 
// 
// All rights reserved.
// 
// 
// Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
// 
// 
//     * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//     * Neither the name of the CellAO Team nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.
// 
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
// EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
// PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
// PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
// LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
// NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

#endregion

namespace AORebirth.Database
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;

    using Dapper;

    using Utility;

    using Config = Utility.Config.ConfigReadWrite;

    #endregion

    /// <summary>
    /// </summary>
    public static class Misc
    {
        #region Public Methods and Operators

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        public static bool CheckDatabase()
        {
            string applicationFolder = Path.Combine(Directory.GetCurrentDirectory(), "SqlTables");
            if (!Directory.Exists(applicationFolder))
            {
                Console.WriteLine("SCHEMA_INCOMPATIBLE: governed SqlTables inventory is missing. Restore the approved package before startup.");
                return false;
            }
            string[] files = Directory.GetFiles(applicationFolder, "*.sql", SearchOption.TopDirectoryOnly);

            string errorMessage = string.Empty;
            try
            {
                using (IDbConnection conn = Connector.GetConnection())
                {
                }
            }
            catch (Exception)
            {
                errorMessage = "DATABASE_UNREACHABLE: verify database configuration and access; no schema writes were attempted.";
            }

            if (errorMessage != string.Empty)
            {
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine("Error connecting to database");
                Console.WriteLine(errorMessage);
                Colouring.Pop();
                return false;
            }

            errorMessage = string.Empty;
            string fName = string.Empty;
            List<string> tablesNotFound = new List<string>();

            try
            {
                using (IDbConnection conn = Connector.GetConnection())
                {
                    foreach (string sqlFile in files)
                    {
                        if (sqlFile != null)
                        {
                            fName = Path.GetFileNameWithoutExtension(sqlFile).ToLower();
                    if (IsAlterScript(fName))
                    {
                        if (!IsAlterMigrationApplied(conn, fName, sqlFile))
                        {
                            tablesNotFound.Add(sqlFile);
                                }
                    }
                    else if (!Exists(conn, fName))
                    {
                        tablesNotFound.Add(sqlFile);
                    }
                        }
                    }
                }
            }
            catch (Exception)
            {
                errorMessage = "SCHEMA_INCOMPATIBLE: required schema could not be read; review the governed baseline and account read permissions.";
            }

            if (errorMessage != string.Empty)
            {
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine("Error checking for table " + fName);
                Console.WriteLine(errorMessage);
                Colouring.Pop();
                return false;
            }

            if (tablesNotFound.Count > 0)
            {
                Console.WriteLine("SCHEMA_MIGRATION_REQUIRED: runtime startup cannot create tables or apply SQL. Review the governed baseline and use the explicit operator migration plan before restarting.");
                foreach (string file in tablesNotFound)
                    Console.WriteLine("Missing schema asset: " + Path.GetFileName(file));
                return false;
            }

            return true;
        }

        /// <summary>
        /// </summary>
        /// <param name="orgId">
        /// </param>
        /// <returns>
        /// </returns>
        public static List<int> GetOrgMembers(uint orgId)
        {
            return GetOrgMembers(orgId, false);
        }

        /// <summary>
        /// </summary>
        /// <param name="orgId">
        /// </param>
        /// <param name="excludePresident">
        /// </param>
        /// <returns>
        /// </returns>
        public static List<int> GetOrgMembers(uint orgId, bool excludePresident)
        {
            List<int> orgMembers = new List<int>();
            try
            {
                using (IDbConnection conn = Connector.GetConnection())
                {
                    string pres = string.Empty;

                    if (excludePresident)
                    {
                        pres =
                            " AND `ID` NOT IN (SELECT `ID` FROM `characters_stats` WHERE `Stat` = '48' AND `Value` = '0')";
                    }

                    orgMembers.AddRange(
                        conn.Query<int>(
                            "SELECT `ID` FROM `characters_stats` WHERE `Stat` = '5' AND `Value` = @orgId " + pres,
                            orgId));
                }
            }
            catch (Exception e)
            {
                LogUtil.ErrorException(e);
            }

            return orgMembers;
        }

        /// <summary>
        /// </summary>
        public static void LogOffAll()
        {
            try
            {
                using (IDbConnection conn = Connector.GetConnection())
                {
                    conn.Execute("UPDATE characters set Online=0");
                }
            }
            catch (Exception e)
            {
                LogUtil.ErrorException(e);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="characterId">
        /// </param>
        public static void LogOffCharacter(int characterId)
        {
            try
            {
                using (IDbConnection conn = Connector.GetConnection())
                {
                    conn.Execute("UPDATE characters set Online=0 where id=@charid", new { charid = characterId });
                }
            }
            catch (Exception e)
            {
                LogUtil.ErrorException(e);
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// </summary>
        /// <param name="conn">
        /// </param>
        /// <param name="fName">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="Exception">
        /// </exception>
        private static bool Exists(IDbConnection conn, string fName)
        {
            switch (Config.Instance.CurrentConfig.SQLType)
            {
                case "MySql":
                    return conn.Query<string>(
                        "SELECT table_name FROM information_schema.tables WHERE table_schema = DATABASE()")
                        .Contains(fName);
                case "MsSql":
                    return conn.Query<string>("SELECT table_name FROM INFORMATION_SCHEMA.TABLES").Contains(fName);
                case "PostgreSQL":
                    return conn.Query<string>("SELECT table_name FROM INFORMATION_SCHEMA.tables").Contains(fName);
                default:
                    throw new Exception("Unknown database type encountered. Check your Config.xml or tell the coders");
            }
        }

        private static bool IsAlterScript(string scriptName)
        {
            return scriptName.EndsWith("_alter", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// *_alter.sql scripts add columns to an existing table. Skip when the base table is
        /// missing (CREATE will include the columns) or every ADD COLUMN is already present.
        /// </summary>
        private static bool IsAlterMigrationApplied(IDbConnection conn, string alterScriptName, string sqlFilePath)
        {
            const string alterSuffix = "_alter";
            string baseTableName = alterScriptName.Substring(
                0,
                alterScriptName.Length - alterSuffix.Length);

            if (!Exists(conn, baseTableName))
            {
                return true;
            }

            string[] columnsToAdd = ParseAlterAddColumnNames(File.ReadAllText(sqlFilePath));
            if (columnsToAdd.Length == 0)
            {
                return true;
            }

            HashSet<string> existingColumns = new HashSet<string>(
                GetTableColumnNames(conn, baseTableName),
                StringComparer.OrdinalIgnoreCase);

            foreach (string columnName in columnsToAdd)
            {
                if (!existingColumns.Contains(columnName))
                {
                    return false;
                }
            }

            return true;
        }

        private static string[] ParseAlterAddColumnNames(string alterSql)
        {
            MatchCollection matches = Regex.Matches(
                alterSql,
                @"ADD\s+COLUMN\s+`?([A-Za-z0-9_]+)`?",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            string[] columnNames = new string[matches.Count];
            for (int i = 0; i < matches.Count; i++)
            {
                columnNames[i] = matches[i].Groups[1].Value;
            }

            return columnNames;
        }

        private static IEnumerable<string> GetTableColumnNames(IDbConnection conn, string tableName)
        {
            switch (Config.Instance.CurrentConfig.SQLType)
            {
                case "MySql":
                    return conn.Query<string>(
                        "SELECT column_name FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = @tableName",
                        new { tableName });
                case "MsSql":
                    return conn.Query<string>(
                        "SELECT column_name FROM INFORMATION_SCHEMA.COLUMNS WHERE table_name = @tableName",
                        new { tableName });
                case "PostgreSQL":
                    return conn.Query<string>(
                        "SELECT column_name FROM information_schema.columns WHERE table_name = @tableName",
                        new { tableName });
                default:
                    throw new Exception("Unknown database type encountered. Check your Config.xml or tell the coders");
            }
        }

        #endregion
    }
}
