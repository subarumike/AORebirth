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
// IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES.
//

#endregion

namespace ChatEngine.PacketHandlers
{
    #region Usings ...

    using System;
    using System.IO;
    using System.Net;
    using System.Text;

    using AORebirth.Communication;
    using AORebirth.Database.Dao;

    using ChatEngine.CoreClient;
    using ChatEngine.Packets;

    using Utility;

    #endregion

    /// <summary>
    /// The authenticate.
    /// </summary>
    internal static class Authenticate
    {
        #region Public Methods and Operators

        /// <summary>
        /// The read.
        /// </summary>
        /// <param name="client">
        /// </param>
        /// <param name="packet">
        /// </param>
        public static void Read(Client client, ref byte[] packet)
        {
            MemoryStream m_stream = new MemoryStream(packet);
            BinaryReader m_reader = new BinaryReader(m_stream);

            /*
             * Authentication packet:
             *
             * bytes 8-11 = character id
             * after that = username
             * after username = login key
             *
             * The login key is read because the client sends it,
             * but it is NOT validated.
             */

            m_stream.Position = 12;

            short userNameLength =
                IPAddress.NetworkToHostOrder(
                    m_reader.ReadInt16());

            string userName =
                Encoding.ASCII.GetString(
                    m_reader.ReadBytes(userNameLength));

            short loginKeyLength =
                IPAddress.NetworkToHostOrder(
                    m_reader.ReadInt16());

            /*
             * The client still sends the login key.
             * Read it so the packet is consumed correctly,
             * but do not validate it.
             */
            m_reader.ReadBytes(loginKeyLength);

            /*
             * Character ID.
             */
            uint characterId =
                BitConverter.ToUInt32(
                    new[]
                    {
                    packet[11],
                    packet[10],
                    packet[9],
                    packet[8]
                    },
                    0);

            /*
             * =========================================================
             * USERNAME-ONLY LOGIN
             * =========================================================
             *
             * We only verify:
             *
             * 1. Username exists.
             * 2. Account is allowed.
             * 3. Character belongs to that account.
             *
             * Password/login key is NOT checked.
             */

            if (string.IsNullOrWhiteSpace(userName))
            {
                client.Send(LoginError.Create("Invalid login"));
                client.Server.DisconnectClient(client);
                return;
            }

            /*
             * Check that the account exists.
             */
            DBLoginData loginData =
                LoginDataDao.Instance.GetByUsername(userName);

            if (loginData == null
                || string.IsNullOrWhiteSpace(loginData.Username))
            {
                client.Send(LoginError.Create("Invalid login"));
                client.Server.DisconnectClient(client);
                return;
            }

            /*
             * Check that the character belongs to this account.
             */
            bool characterBelongsToAccount =
                CharacterDao.Instance.IsCharacterOnAccount(
                    loginData.Username,
                    characterId);

            if (!characterBelongsToAccount)
            {
                client.Send(LoginError.Create("Invalid login"));
                client.Server.DisconnectClient(client);
                return;
            }

            /*
             * =========================================================
             * LOGIN SUCCESS
             * =========================================================
             */

            client.Send(LoginOk.Create());

            /*
             * Save character ID in client.
             */
            client.Character =
                new Character(
                    characterId,
                    client);

            /*
             * Add client to connected clients list.
             */
            if (!client.ChatServer().ConnectedClients.ContainsKey(
                    client.Character.CharacterId))
            {
                client.ChatServer().ConnectedClients.Add(
                    client.Character.CharacterId,
                    client);
            }

            /*
             * Add yourself to known clients.
             */
            client.KnownClients.Add(
                client.Character.CharacterId);

            /*
             * Give client its own name lookup.
             */
            byte[] pname =
                PlayerName.Create(
                    client,
                    client.Character.CharacterId);

            client.Send(pname);

            /*
             * Send server welcome message.
             */
            byte[] anonv =
                MsgAnonymousVicinity.Create(
                    string.Empty,
                    string.Format(
                        client.ChatServer().MessageOfTheDay,
                        AssemblyInfoclass.RevisionName
                        + " "
                        + AssemblyInfoclass.AssemblyVersion),
                    string.Empty);

            client.Send(anonv);

            /*
             * Add client to channels.
             */
            client.ChatServer().AddClientToChannels(client);
        }

        #endregion
    }
}
