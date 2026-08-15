#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
//
//     * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer.
//     * Neither the name of the CellAO Team nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
// IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES.
//

#endregion

namespace LoginEngine.Packets
{
    #region Usings ...

    using System;

    using AO.Core.Encryption;

    using LoginEngine.CoreClient;
    using LoginEngine.QueryBase;

    #endregion

    /// <summary>
    /// </summary>
    public class CheckLogin
    {
        #region Fields

        private readonly LoginFlags lf = new LoginFlags();

        private readonly LoginName ln = new LoginName();

        private readonly LoginPasswd lp = new LoginPasswd();

        private const int LoginAllowedFlag = 0;

        #endregion

        #region Public Methods and Operators

        public bool IsCharacterOnAccount(Client client, int characterId)
        {
            return this.IsCharacterOnAccount(client.AccountName, characterId);
        }

        internal bool IsCharacterOnAccount(string accountName, int characterId)
        {
            if (string.IsNullOrWhiteSpace(accountName) || characterId < 1)
            {
                return false;
            }

            var le = new LoginEncryption();

            return le.IsCharacterOnAccount(
                accountName,
                (UInt32)characterId);
        }

        public bool IsLoginAllowed(Client client, string accountName)
        {
            if (!client.HasAuthenticationChallenge(accountName)
                || !string.Equals(
                    accountName,
                    client.AccountName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return this.IsLoginAllowed(accountName);
        }

        internal bool IsLoginAllowed(string accountName)
        {
            if (string.IsNullOrWhiteSpace(accountName))
            {
                return false;
            }

            this.ln.GetLoginName(accountName);
            this.lf.GetLoginFlags(accountName);

            if (this.ln.LoginN != null
                && string.Equals(
                    accountName,
                    this.ln.LoginN,
                    StringComparison.OrdinalIgnoreCase)
                && this.lf.FlagsL == LoginAllowedFlag)
            {
                return true;
            }

            return false;
        }

        public bool IsLoginCorrect(Client client, string loginKey)
        {
            if (!client.HasAuthenticationChallenge(client.AccountName)
                || string.IsNullOrWhiteSpace(loginKey))
            {
                return false;
            }

            return this.IsLoginCorrect(
                client.AccountName,
                client.ServerSalt,
                loginKey);
        }

        internal bool IsLoginCorrect(
            string accountName,
            string serverSalt,
            string loginKey)
        {
            if (string.IsNullOrWhiteSpace(accountName)
                || string.IsNullOrWhiteSpace(serverSalt)
                || string.IsNullOrWhiteSpace(loginKey))
            {
                return false;
            }

            var le = new LoginEncryption();

            this.lp.GetLoginPassword(accountName);

            return IsLoginCorrect(
                loginKey,
                serverSalt,
                accountName,
                this.lp.PasswdL);
        }

        internal static bool IsLoginCorrect(
            string loginKey,
            string serverSalt,
            string accountName,
            string passwordHash)
        {
            if (string.IsNullOrWhiteSpace(accountName)
                || string.IsNullOrWhiteSpace(serverSalt)
                || string.IsNullOrWhiteSpace(loginKey))
            {
                return false;
            }

            var le = new LoginEncryption();
            return le.IsValidLogin(
                loginKey,
                serverSalt,
                accountName,
                passwordHash);
        }

        #endregion
    }
}
