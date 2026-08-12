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

#endregion

namespace ChatEngine.PacketHandlers
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;

    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;
    using AORebirth.Enums;

    using ChatEngine.CoreClient;
    using ChatEngine.Lists;
    using ChatEngine.Packets;

    /// <summary>
    /// Looking for Team search (chat type 0x05DE / 1502).
    ///
    /// Filters:
    ///
    /// filter0 = side
    ///     0 = Neutral
    ///     1 = Clan
    ///     2 = Omni
    ///     0xFFFFFFFF = Any
    ///
    /// filter1 = level filter
    ///
    /// filter2 = location
    ///     0 = This Playfield
    ///     1 = Rubi-Ka
    ///     2 = Shadowlands
    ///     3 = Any
    ///     0xFFFFFFFF = Any
    ///
    /// filter3 = currently unused
    /// </summary>
    public static class LftSearch
    {
        private static readonly int ProfessionStatId =
        (int)StatIds.profession;

private static readonly int LevelStatId =
    (int)StatIds.level;

        private static readonly int ExpansionStatId =
            (int)StatIds.expansion;

        /// <summary>
        /// Must match ZoneEngine.Core.LftInviteClientPresence.LftSeedCommandPrefix.
        /// </summary>
        private const string LftSeedCommandPrefix = "#aorebirth-lft-seed";

        /// <summary>
        /// Give ZoneEngine time to seed remote candidates.
        /// </summary>
        private const int SeedSettleMilliseconds = 1500;

        /*
         * LFT location filter values.
         */
        private const uint LocationThisPlayfield = 0;
        private const uint LocationRubiKa = 1;
        private const uint LocationShadowlands = 2;
        private const uint LocationAny = 3;

        /*
         * Expansion flags from AORebirth.Enums.Expansions.
         *
         * NotumWars   = 1 << 0
         * ShadowLands = 1 << 1
         */
        private const int RubiKaExpansionBit =
            1 << 0;

        private const int ShadowlandsExpansionBit =
            1 << 1;

        public static void Read(Client client, byte[] packet)
        {
            PacketReader reader = new PacketReader(ref packet);

            reader.ReadUInt16(); // type
            reader.ReadUInt16(); // length

            uint filter0 = reader.ReadUInt32();
            uint filter1 = reader.ReadUInt32();
            uint filter2 = reader.ReadUInt32();
            uint filter3 = reader.ReadUInt32();

            reader.Finish();

            if (client == null
                || client.Character == null
                || client.Character.CharacterId == 0)
            {
                return;
            }

            client.Server.Debug(
                client,
                "{0} >> LftSearch FILTERS: f0={1} f1={2} f2={3} f3={4}",
                client.Character.characterName,
                filter0,
                filter1,
                filter2,
                filter3);

            /*
             * Searcher's own data.
             */
            int searcherLevel =
                ReadStat(
                    client.Character.CharacterId,
                    LevelStatId);

            if (searcherLevel < 1)
            {
                searcherLevel = 1;
            }

            if (searcherLevel > 220)
            {
                searcherLevel = 220;
            }

            int searcherSide =
                client.Character.CharacterSide;

            int searcherPlayfield =
                ResolvePlayfield(
                    client.Character.CharacterId);

            int searcherExpansion =
                ReadStat(
                    client.Character.CharacterId,
                    ExpansionStatId);

            /*
             * Statistics for debugging.
             */
            int skippedOffline = 0;
            int skippedLevel = 0;
            int skippedSide = 0;
            int skippedLocation = 0;
            int registryCount = 0;

            List<LftQueryReply.Entry> matches =
                new List<LftQueryReply.Entry>();

            foreach (
                KeyValuePair<uint, string> registration
                in LftRegistry.Snapshot())
            {
                registryCount++;

                uint candidateId =
                    registration.Key;

                /*
                 * Never show yourself.
                 */
                if (candidateId == client.Character.CharacterId)
                {
                    continue;
                }

                /*
                 * Candidate must currently be connected
                 * to ChatEngine.
                 */
                Client candidateClient;

                if (!client.ChatServer().ConnectedClients.TryGetValue(
                        candidateId,
                        out candidateClient)
                    || candidateClient == null
                    || candidateClient.Character == null)
                {
                    skippedOffline++;
                    continue;
                }

                /*
                 * Candidate statistics.
                 */
                int candidateProfession =
                    ReadStat(
                        candidateId,
                        ProfessionStatId);

                int candidateLevel =
                    ReadStat(
                        candidateId,
                        LevelStatId);

                if (candidateLevel < 1)
                {
                    candidateLevel = 1;
                }

                if (candidateLevel > 220)
                {
                    candidateLevel = 220;
                }

                int candidateSide =
                    candidateClient.Character.CharacterSide;

                /*
                 * LIVE PLAYFIELD.
                 *
                 * This is used for displaying the actual PF and
                 * for "This Playfield".
                 *
                 * It is NOT used to decide RK versus SL.
                 */
                int candidatePlayfield =
                    ResolvePlayfield(candidateId);

                /*
                 * EXPANSION.
                 *
                 * This is what determines RK / SL.
                 */
                int candidateExpansion =
                    ReadStat(
                        candidateId,
                        ExpansionStatId);

                /*
                 * =====================================================
                 * SIDE FILTER
                 * =====================================================
                 */
                if (filter0 != UInt32.MaxValue)
                {
                    if (candidateSide != (int)filter0)
                    {
                        skippedSide++;
                        continue;
                    }
                }

                /*
                 * =====================================================
                 * LEVEL FILTER
                 * =====================================================
                 */
                if (!TeamLevelRanges.IsCompatible(
                        searcherLevel,
                        candidateLevel))
                {
                    skippedLevel++;
                    continue;
                }

                /*
                 * =====================================================
                 * LOCATION FILTER
                 * =====================================================
                 *
                 * IMPORTANT:
                 *
                 * RK / SL is determined from the EXPANSION stat.
                 *
                 * We do NOT use PF numbers.
                 *
                 * This is important because:
                 *
                 *     PF 800 = Borealis = Rubi-Ka
                 *
                 * and PF number itself does not tell us reliably
                 * whether the character belongs to RK or SL.
                 */
                if (!MatchesLocation(
                        filter2,
                        searcherPlayfield,
                        candidatePlayfield,
                        candidateExpansion))
                {
                    skippedLocation++;
                    continue;
                }

                /*
                 * Candidate passed all filters.
                 */
                matches.Add(
                    new LftQueryReply.Entry
                    {
                        CharacterId =
                            candidateId,

                        Name =
                            candidateClient.Character.characterName
                            ?? string.Empty,

                        Level =
                            (uint)candidateLevel,

                        Playfield =
                            (uint)Math.Max(
                                0,
                                candidatePlayfield),

                        Side =
                            (byte)(candidateSide & 0xFF),

                        Profession =
                            (byte)(candidateProfession & 0xFF),

                        Comment =
                            registration.Value
                            ?? string.Empty
                    });
            }

            client.Server.Info(
                client,
                "{0} >> LftSearch: f0={1} f1={2} f2={3} f3={4} matches={5} searcherLvl={6} searcherSide={7} searcherPF={8} searcherExpansion={9} registry={10} skipOffline={11} skipLevel={12} skipSide={13} skipLocation={14}",
                client.Character.characterName,
                filter0,
                filter1,
                filter2,
                filter3,
                matches.Count,
                searcherLevel,
                searcherSide,
                searcherPlayfield,
                searcherExpansion,
                registryCount,
                skippedOffline,
                skippedLevel,
                skippedSide,
                skippedLocation);

            /*
             * Push name cache before sending LFT rows.
             */
            for (int i = 0; i < matches.Count; i++)
            {
                PushNameCache(
                    client,
                    matches[i]);
            }

            /*
             * Ask ZoneEngine to seed remote candidates.
             */
            NotifyZoneToSeedRemoteCandidates(
                client,
                matches);

            /*
             * Give ZoneEngine time to update playfields.
             */
            if (matches.Count > 0)
            {
                Thread.Sleep(
                    SeedSettleMilliseconds);

                for (int i = 0; i < matches.Count; i++)
                {
                    int pf =
                        ResolvePlayfield(
                            matches[i].CharacterId);

                    if (pf > 0)
                    {
                        matches[i].Playfield =
                            (uint)pf;
                    }
                }
            }

            /*
             * Send the final result to the client.
             */
            client.Send(
                LftQueryReply.CreateClear());

            for (int i = 0; i < matches.Count; i++)
            {
                client.Send(
                    LftQueryReply.CreateEntry(
                        matches[i]));

                /*
                 * Push name again after the row.
                 * Invite needs id -> name resolution.
                 */
                PushNameCache(
                    client,
                    matches[i]);
            }
        }

        /// <summary>
        /// Checks the location filter.
        ///
        /// RK / SL classification is based on the expansion stat,
        /// NOT on the playfield ID.
        /// </summary>
        private static bool MatchesLocation(
            uint filter,
            int searcherPlayfield,
            int candidatePlayfield,
            int candidateExpansion)
        {
            /*
             * Any.
             */
            if (filter == UInt32.MaxValue
                || filter == LocationAny)
            {
                return true;
            }

            /*
             * This Playfield.
             *
             * This one genuinely uses the PF ID because the
             * client is asking for the same actual playfield.
             */
            if (filter == LocationThisPlayfield)
            {
                return
                    searcherPlayfield > 0
                    && candidatePlayfield > 0
                    && candidatePlayfield == searcherPlayfield;
            }

            /*
             * Rubi-Ka.
             */
            if (filter == LocationRubiKa)
            {
                return IsRubiKa(
                    candidateExpansion);
            }

            /*
             * Shadowlands.
             */
            if (filter == LocationShadowlands)
            {
                return IsShadowlands(
                    candidateExpansion);
            }

            /*
             * Unknown filter.
             */
            return true;
        }

        /// <summary>
        /// Determines whether the character is in Shadowlands
        /// using the expansion stat.
        /// </summary>
        private static bool IsShadowlands(
            int expansion)
        {
            return
                (expansion & ShadowlandsExpansionBit) != 0;
        }

        /// <summary>
        /// Determines whether the character is in Rubi-Ka
        /// using the expansion stat.
        ///
        /// In AORebirth:
        ///
        /// ShadowLands = bit 1 = value 2
        /// Rubi-Ka     = bit 0 = value 1
        ///
        /// Therefore an expansion without the Shadowlands bit
        /// is treated as Rubi-Ka.
        /// </summary>
        private static bool IsRubiKa(
            int expansion)
        {
            /*
             * Explicit Rubi-Ka bit.
             */
            if ((expansion & RubiKaExpansionBit) != 0)
            {
                return true;
            }

            /*
             * If Shadowlands bit is not present, this is also
             * treated as Rubi-Ka.
             *
             * This handles values such as:
             *
             *     0x00000185
             *
             * where Shadowlands bit 2 is NOT set.
             */
            return
                (expansion & ShadowlandsExpansionBit) == 0;
        }

        /// <summary>
        /// Push id -> name information to the client.
        /// </summary>
        private static void PushNameCache(
            Client client,
            LftQueryReply.Entry entry)
        {
            if (client == null
                || entry == null
                || entry.CharacterId == 0)
            {
                return;
            }

            string name =
                entry.Name ?? string.Empty;

            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            /*
             * Type 20.
             */
            client.Send(
                PlayerName.Create(
                    entry.CharacterId,
                    name));

            /*
             * Type 21 as fallback for GUI paths.
             */
            client.Send(
                NameLookupResult.Create(
                    entry.CharacterId,
                    name));

            if (!client.KnownClients.Contains(
                    entry.CharacterId))
            {
                client.KnownClients.Add(
                    entry.CharacterId);
            }
        }

        /// <summary>
        /// Resolve the current playfield.
        /// </summary>
        private static int ResolvePlayfield(
            uint characterId)
        {
            int live;

            /*
             * First use the live registry.
             */
            if (LftPlayfieldRegistry.TryGet(
                    characterId,
                    out live)
                && live > 0)
            {
                return live;
            }

            /*
             * Fallback to database.
             */
            try
            {
                DBCharacter dbCharacter =
                    CharacterDao.Instance.Get(
                        (int)characterId);

                if (dbCharacter != null
                    && dbCharacter.Playfield > 0)
                {
                    return dbCharacter.Playfield;
                }
            }
            catch (Exception)
            {
            }

            return 0;
        }

        /// <summary>
        /// Tell ZoneEngine to seed the remote candidates.
        /// </summary>
        private static void NotifyZoneToSeedRemoteCandidates(
            Client client,
            List<LftQueryReply.Entry> matches)
        {
            if (client == null
                || matches == null
                || matches.Count == 0
                || Program.ISCom == null)
            {
                return;
            }

            StringBuilder command =
                new StringBuilder(
                    LftSeedCommandPrefix);

            for (int i = 0;
                 i < matches.Count;
                 i++)
            {
                LftQueryReply.Entry entry =
                    matches[i];

                command.Append(' ');
                command.Append(
                    entry.CharacterId);

                if (!string.IsNullOrWhiteSpace(
                        entry.Name))
                {
                    command.Append(':');

                    command.Append(
                        entry.Name
                            .Trim()
                            .Replace(
                                ' ',
                                '_'));
                }
            }

            Program.ISCom.BroadCast(
                new AORebirth.Communication.Messages.ChatCommand
                {
                    CharacterId =
                        unchecked(
                            (int)client.Character.CharacterId),

                    ChatCommandString =
                        command.ToString()
                });
        }

        /// <summary>
        /// Read a character stat.
        /// </summary>
        private static int ReadStat(
            uint characterId,
            int statId)
        {
            try
            {
                return
                    StatDao.Instance.GetById(
                        50000,
                        (int)characterId,
                        statId).StatValue;
            }
            catch (Exception)
            {
                return 0;
            }
        }
}
}
