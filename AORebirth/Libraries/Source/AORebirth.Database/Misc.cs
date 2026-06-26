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
    using System.Text;

    using Dapper;

    using Utility;

    using Config = Utility.Config.ConfigReadWrite;

    #endregion

    /// <summary>
    /// </summary>
    public static class Misc
    {
        private const int MaxSqlBatchLength = 256 * 1024;

        private const string InsertIntoKeyword = "insert into";

        private const string ValuesKeyword = "values";

        #region Public Methods and Operators

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        public static bool CheckDatabase()
        {
            string applicationFolder = Path.Combine(Directory.GetCurrentDirectory(), "SqlTables");
            string[] files = Directory.GetFiles(applicationFolder, "*.sql", SearchOption.TopDirectoryOnly);

            string errorMessage = string.Empty;
            try
            {
                using (IDbConnection conn = Connector.GetConnection())
                {
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
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
                            if (!Exists(conn, fName))
                            {
                                tablesNotFound.Add(sqlFile);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
            }

            if (errorMessage != string.Empty)
            {
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine("Error checking for table " + fName);
                Console.WriteLine(errorMessage);
                Colouring.Pop();
                return false;
            }

            try
            {
                using (IDbConnection conn = Connector.GetConnection())
                {
                    if (tablesNotFound.Count > 0)
                    {
                        Colouring.Push(ConsoleColor.Red);
                        Console.Write("SQL Tables are not complete. Should they be created? (Y/N) ");
                        Colouring.Pop();

                        string answer = Console.ReadLine();
                        string sqlQuery;
                        if (answer.ToLower() == "y")
                        {
                            foreach (string sqlFile in tablesNotFound)
                            {
                                fName = Path.GetFileNameWithoutExtension(sqlFile);
                                long fileSize = new FileInfo(sqlFile).Length;
                                Colouring.Push(ConsoleColor.Green);
                                Console.Write("Table " + fName.PadRight(67) + "[  0%]");
                                Colouring.Pop();
                                if (fileSize > 10000)
                                {
                                    string[] queries = File.ReadAllLines(sqlFile);
                                    int counter = 0;
                                    string lastpercent = "0";
                                    ExecuteSqlStatementsUntilFirstInsert(conn, queries, ref counter);
                                    while (counter < queries.Length)
                                    {
                                        if (queries[counter].TrimStart().StartsWith(
                                            InsertIntoKeyword,
                                            StringComparison.OrdinalIgnoreCase))
                                        {
                                            break;
                                        }

                                        counter++;
                                    }

                                    if (counter < queries.Length)
                                    {
                                        ExecuteLargeSqlInserts(conn, queries, counter, fName, lastpercent);
                                    }
                                    else
                                    {
                                        Colouring.Push(ConsoleColor.Green);
                                        Console.Write("\rTable " + fName.PadRight(67) + "[100%]");
                                        Colouring.Pop();
                                    }
                                }
                                else
                                {
                                    sqlQuery = File.ReadAllText(sqlFile);
                                    conn.Execute(sqlQuery);
                                    Colouring.Push(ConsoleColor.Green);
                                    Console.Write("\rTable " + fName.PadRight(67) + "[100%]");
                                    Colouring.Pop();
                                }

                                Console.WriteLine();
                            }
                        }

                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                LogUtil.ErrorException(e);
                return false;
            }

            return true;
        }

        private static void ExecuteLargeSqlInserts(
            IDbConnection conn,
            string[] queries,
            int startIndex,
            string tableName,
            string lastPercent)
        {
            StringBuilder buffer = new StringBuilder(MaxSqlBatchLength);
            string batchPrefix = string.Empty;
            int counter = startIndex;
            while (counter < queries.Length)
            {
                string statement = ReadSqlStatement(queries, ref counter);
                if (statement.Trim().Length == 0)
                {
                    continue;
                }

                string insertPrefix;
                string valuesPart;
                if (!TrySplitInsertStatement(statement, out insertPrefix, out valuesPart))
                {
                    FlushSqlBatch(conn, buffer);
                    batchPrefix = string.Empty;
                    conn.Execute(statement);
                    WriteTableProgress(tableName, counter, queries.Length, ref lastPercent);
                    continue;
                }

                if (batchPrefix != insertPrefix)
                {
                    FlushSqlBatch(conn, buffer);
                    batchPrefix = insertPrefix;
                }

                foreach (string valuesRow in SplitSqlValuesRows(valuesPart))
                {
                    if (buffer.Length == 0)
                    {
                        buffer.Append(batchPrefix);
                    }

                    if (buffer.Length > batchPrefix.Length
                        && buffer.Length + valuesRow.Length + 2 > MaxSqlBatchLength)
                    {
                        FlushSqlBatch(conn, buffer);
                        buffer.Append(batchPrefix);
                    }

                    if (buffer.Length > batchPrefix.Length)
                    {
                        buffer.Append(", ");
                    }

                    buffer.Append(valuesRow);
                }
                WriteTableProgress(tableName, counter, queries.Length, ref lastPercent);
            }

            FlushSqlBatch(conn, buffer);
        }

        private static void ExecuteSqlStatementsUntilFirstInsert(IDbConnection conn, string[] queries, ref int counter)
        {
            while (counter < queries.Length)
            {
                int statementStart = counter;
                string statement = ReadSqlStatement(queries, ref counter);
                if (statement.Trim().Length == 0)
                {
                    continue;
                }

                if (statement.TrimStart().StartsWith(InsertIntoKeyword, StringComparison.OrdinalIgnoreCase))
                {
                    counter = statementStart;
                    return;
                }

                conn.Execute(statement);
            }
        }

        private static string ReadSqlStatement(string[] queries, ref int counter)
        {
            StringBuilder statement = new StringBuilder();
            while (counter < queries.Length)
            {
                string line = queries[counter];
                counter++;
                if (line.Trim().Length == 0)
                {
                    continue;
                }

                if (statement.Length > 0)
                {
                    statement.Append('\n');
                }

                statement.Append(line);
                if (line.TrimEnd().EndsWith(";", StringComparison.Ordinal))
                {
                    break;
                }
            }

            return statement.ToString();
        }

        private static bool TrySplitInsertStatement(string statement, out string insertPrefix, out string valuesPart)
        {
            insertPrefix = string.Empty;
            valuesPart = string.Empty;

            string trimmed = statement.Trim();
            if (!trimmed.StartsWith(InsertIntoKeyword, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int valuesIndex = trimmed.IndexOf(ValuesKeyword, StringComparison.OrdinalIgnoreCase);
            if (valuesIndex < 0)
            {
                return false;
            }

            valuesPart = trimmed.Substring(valuesIndex + ValuesKeyword.Length).Trim();
            if (valuesPart.EndsWith(";", StringComparison.Ordinal))
            {
                valuesPart = valuesPart.Substring(0, valuesPart.Length - 1).Trim();
            }

            if (!valuesPart.StartsWith("(", StringComparison.Ordinal))
            {
                return false;
            }

            insertPrefix = trimmed.Substring(0, valuesIndex).TrimEnd() + " VALUES ";
            return true;
        }

        private static IEnumerable<string> SplitSqlValuesRows(string valuesPart)
        {
            List<string> rows = new List<string>();
            int rowStart = -1;
            int depth = 0;
            bool inString = false;
            char stringDelimiter = '\0';
            for (int i = 0; i < valuesPart.Length; i++)
            {
                char current = valuesPart[i];
                if (inString)
                {
                    if (current == '\\')
                    {
                        i++;
                        continue;
                    }

                    if (current == stringDelimiter)
                    {
                        if (i + 1 < valuesPart.Length && valuesPart[i + 1] == stringDelimiter)
                        {
                            i++;
                            continue;
                        }

                        inString = false;
                    }

                    continue;
                }

                if (current == '\'' || current == '"')
                {
                    inString = true;
                    stringDelimiter = current;
                    continue;
                }

                if (current == '(')
                {
                    if (depth == 0)
                    {
                        rowStart = i;
                    }

                    depth++;
                    continue;
                }

                if (current == ')' && depth > 0)
                {
                    depth--;
                    if (depth == 0 && rowStart >= 0)
                    {
                        rows.Add(valuesPart.Substring(rowStart, i - rowStart + 1).Trim());
                        rowStart = -1;
                    }
                }
            }

            if (rows.Count == 0 && valuesPart.Trim().Length > 0)
            {
                rows.Add(valuesPart.Trim());
            }

            return rows;
        }

        private static void FlushSqlBatch(IDbConnection conn, StringBuilder buffer)
        {
            if (buffer.Length == 0)
            {
                return;
            }

            buffer.Append(";");
            try
            {
                conn.Execute(buffer.ToString());
            }
            catch (Exception)
            {
                Console.WriteLine(buffer.ToString().Substring(0, Math.Min(300, buffer.Length)));
                throw;
            }

            buffer.Clear();
        }

        private static void WriteTableProgress(string tableName, int counter, int total, ref string lastPercent)
        {
            string percent = Convert.ToInt32(Math.Floor((double)counter / total * 100)).ToString();
            if (percent == lastPercent)
            {
                return;
            }

            Console.Write("\rTable " + tableName.PadRight(67) + "[" + percent.PadLeft(3) + "%]");
            lastPercent = percent;
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
                    return conn.Query<string>("SELECT table_name FROM information_schema.tables").Contains(fName);
                default:
                    throw new Exception("Unknown database type encountered. Check your Config.xml or tell the coders");
            }
        }

        #endregion
    }
}
