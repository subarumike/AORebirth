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

namespace AORebirth.Database.Dao
{
    #region Usings ...

    using System;
    using System.Data;
    using AORebirth.Database.Entities;
    using AORebirth.Interfaces.Persistence.Accounts;

    using Dapper;

    using Utility;

    #endregion

    /// <summary>
    /// Data access object for LoginData
    /// </summary>
    public class LoginDataDao : Dao<DBLoginData, LoginDataDao>
    {
        #region Public Methods and Operators

        /// <summary>
        /// </summary>
        /// <param name="charId">
        /// </param>
        /// <returns>
        /// </returns>
        public DBLoginData GetByCharacterId(int charId)
        {
            return ToLegacy(DatabaseDaoFactory.CreateAccountDao().LoadByCharacterId(charId).Account);
        }

        /// <summary>
        /// Get login data by username
        /// </summary>
        /// <param name="username">
        /// Name of the user
        /// </param>
        /// <returns>
        /// DBLogindata object
        /// </returns>
        public DBLoginData GetByUsername(string username)
        {
            return ToLegacy(DatabaseDaoFactory.CreateAccountDao().LoadByUsername(username));
        }

        /// <summary>
        /// Returns the count of registered users.
        /// </summary>
        /// <param name="">
        /// </param>
        /// <returns>
        /// long count
        /// </returns>
        public long GetRegisteredCount()
        {
            return DatabaseDaoFactory.CreateAccountDao().CountRegisteredAccounts();
        }


        /// <summary>
        /// </summary>
        /// <param name="user">
        /// </param>
        public void LogoffChars(string user)
        {
            var characters = DatabaseDaoFactory.CreateCharacterDao();
            foreach (var character in characters.ListForAccount(user))
            {
                CharacterOnlineOwnershipGuard.TryClearLoginOwnership(
                    character.CharacterId, id => characters.MarkOffline(id));
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="user">
        /// </param>
        /// <param name="gmlevel">
        /// </param>
        public static void SetGM(string user, int gmlevel)
        {
            try
            {
                using (IDbConnection conn = Connector.GetConnection())
                {
                    conn.Execute("UPDATE login SET GM=@gm", new { gm = gmlevel });
                }
            }
            catch (Exception e)
            {
                LogUtil.ErrorException(e);
            }
        }

        public static void SetExpansions(string user, int expansions)
        {
            SetExpansions(user, expansions, DatabaseDaoFactory.CreateAccountDao());
        }

        internal static void SetExpansions(string user, int expansions, IAccountDao accounts)
        {
            try
            {
                accounts.SetExpansions(user, expansions);
            }
            catch (Exception e)
            {
                LogUtil.ErrorException(e);
            }
        }

        /// <summary>
        /// Write login data to table
        /// </summary>
        /// <param name="login">
        /// Login data to write
        /// </param>
        public static void WriteLoginData(DBLoginData login)
        {
            WriteLoginData(login, DatabaseDaoFactory.CreateAccountDao());
        }

        internal static void WriteLoginData(DBLoginData login, IAccountDao accounts)
        {
            try
            {
                accounts.CreateGameAccount(new NewGameAccountData
                {
                    Email = login.Email,
                    FirstName = login.FirstName,
                    LastName = login.LastName,
                    Username = login.Username,
                    PasswordHash = login.Password,
                    AllowedCharacters = login.AllowedCharacters,
                    Flags = login.Flags,
                    AccountFlags = login.AccountFlags,
                    Expansions = login.Expansions,
                    GmLevel = login.GM
                });
            }
            catch (Exception e)
            {
                LogUtil.ErrorException(e);
                throw;
            }
        }

        /// <summary>
        /// Write new password to table
        /// </summary>
        /// <param name="login">
        /// DBLoginData object
        /// </param>
        /// <returns>
        /// </returns>
        public static int WriteNewPassword(DBLoginData login)
        {
            return WriteNewPassword(login, DatabaseDaoFactory.CreateAccountDao());
        }

        internal static int WriteNewPassword(DBLoginData login, IAccountDao accounts)
        {
            try
            {
                return accounts.ChangePassword(login.Username, login.Password);
            }
            catch (Exception e)
            {
                LogUtil.ErrorException(e);
                return 0;
            }
        }

        public bool Exists(string username)
        {
            return DatabaseDaoFactory.CreateAccountDao().UsernameExists(username);
        }

        // Existing engine callers still receive the complete legacy account representation.
        // Authentication, hashing and authorization remain in those callers.
        private static DBLoginData ToLegacy(GameAccountData account)
        {
            if (account == null) return null;
            return new DBLoginData
            {
                Id = account.AccountId,
                CreationDate = account.CreationDate,
                Email = account.Email,
                FirstName = account.FirstName,
                LastName = account.LastName,
                Username = account.Username,
                Password = account.PasswordHash,
                AllowedCharacters = account.AllowedCharacters,
                Flags = account.Flags,
                AccountFlags = account.AccountFlags,
                Expansions = account.Expansions,
                GM = account.GmLevel
            };
        }

        #endregion
    }
}
