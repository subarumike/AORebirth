#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met.
// Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES ARE DISCLAIMED.
// IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES.

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
    /// filter0 = side
    ///     0 = Neutral
    ///     1 = Clan
    ///     2 = Omni
    ///     0xFFFFFFFF = Any
    ///
    /// filter1 = profession filter
    ///
    /// filter2 = location
    ///     0 = This Playfield
    ///     1 = Anywhere
    ///     2 = Rubi-Ka
    ///     3 = Shadowlands
    ///
    /// filter3 = unknown / unused
    ///
    /// IMPORTANT:
    ///
    /// Profession and location filtering are performed against
    /// the LFT CANDIDATE.
    ///
    /// The searcher's profession/location does NOT affect the
    /// candidate filtering.
    /// </summary>
    public static class LftSearch
    {
        private static readonly int ProfessionStatId =
            (int)StatIds.profession;

        private static readonly int LevelStatId =
            (int)StatIds.level;

        private const string LftSeedCommandPrefix =
            "#aorebirth-lft-seed";

        private const int SeedSettleMilliseconds =
            1500;

        private const uint LocationThisPlayfield =
            0;

        private const uint LocationAny =
            1;

        private const uint LocationRubiKa =
            2;

        private const uint LocationShadowlands =
            3;

        /*
         * ============================================================
         * SHADOWLANDS PLAYFIELD IDs
         * ============================================================
         *
         * PF ID in this list = Shadowlands.
         * PF ID not in this list = Rubi-Ka.
         */
        private static readonly HashSet<uint> ShadowlandsPlayfieldIds =
            new HashSet<uint>
            {
                4211,
                4212,
                4213,
                4214,
                4215,
                4001,

                4220,
                4221,
                4222,
                4223,
                4224,

                4310,
                4311,
                4312,
                4313,
                4314,
                4315,
                4316,
                4318,

                4320,
                4321,
                4322,
                4324,
                4327,
                4328,
                4329,
                4330,
                4331,
                4336,
                4337,

                4364,
                4365,
                4366,
                4367,
                4368,
                4374,

                4524,
                4525,
                4526,
                4530,
                4531,
                4532,
                4533,
                4534,
                4540,
                4541,
                4542,
                4543,
                4544,

                4605,

                4621,
                4622,
                4623,
                4624,
                4625,
                4626,
                4627,
                4628,
                4629,
                4630,

                4676,
                4677,
                4678,
                4679,
                4680,
                4681,
                4682,
                4683,
                4684,
                4685,
                4686,
                4687,
                4688,
                4689,
                4690,
                4691,
                4692,
                4693,
                4694,
                4695,
                4696,
                4697,
                4698,
                4699,

                4872,
                4873,
                4877,
                4880,
                4881,

                6011,
                6012,
                6013,
                6015,
                6020,
                6021,
                6022,
                6024,
                6041,

                4335,
                4389,
                4390,
                4391,
                4468,
                6007,
                6035,
                6036
            };

        public static void Read(
            Client client,
            byte[] packet)
        {
            PacketReader reader =
                new PacketReader(ref packet);

            reader.ReadUInt16();
            reader.ReadUInt16();

            uint filter0 =
                reader.ReadUInt32();

            uint filter1 =
                reader.ReadUInt32();

            uint filter2 =
                reader.ReadUInt32();

            uint filter3 =
                reader.ReadUInt32();

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
             * ========================================================
             * SEARCHER DATA
             * ========================================================
             */

            int searcherLevel =
                ReadStat(
                    client.Character.CharacterId,
                    LevelStatId);

            if (searcherLevel < 1)
            {
                searcherLevel = 1;
            }
            else if (searcherLevel > 220)
            {
                searcherLevel = 220;
            }

            int searcherSide =
                client.Character.CharacterSide;

            int searcherPlayfield =
                ResolvePlayfield(
                    client.Character.CharacterId);

            /*
             * ========================================================
             * DEBUG COUNTERS
             * ========================================================
             */

            int skippedOffline = 0;
            int skippedLevel = 0;
            int skippedSide = 0;
            int skippedProfession = 0;
            int skippedLocation = 0;
            int registryCount = 0;

            List<LftQueryReply.Entry> matches =
                new List<LftQueryReply.Entry>();

            /*
             * ========================================================
             * LFT REGISTRY
             * ========================================================
             */

            foreach (
                KeyValuePair<uint, string> registration
                in LftRegistry.Snapshot())
            {
                registryCount++;

                uint candidateId =
                    registration.Key;

                /*
                 * Never return the searcher himself.
                 */
                if (candidateId ==
                    client.Character.CharacterId)
                {
                    continue;
                }

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
                 * ====================================================
                 * CANDIDATE DATA
                 * ====================================================
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
                else if (candidateLevel > 220)
                {
                    candidateLevel = 220;
                }

                int candidateSide =
                    candidateClient.Character.CharacterSide;

                int candidatePlayfield =
                    ResolvePlayfield(
                        candidateId);

                /*
                 * IMPORTANT:
                 *
                 * RK/SL is determined from the CANDIDATE'S
                 * PLAYFIELD ID.
                 *
                 * If candidatePlayfield is in the SL list:
                 *     SL
                 *
                 * Otherwise:
                 *     RK
                 */
                bool candidateIsShadowlands =
                    ShadowlandsPlayfieldIds.Contains(
                        (uint)Math.Max(
                            0,
                            candidatePlayfield));

                string candidateLocation =
                    candidateIsShadowlands
                        ? "SL"
                        : "RK";

                client.Server.Debug(
                    client,
                    "LFT CANDIDATE: {0} id={1} side={2} level={3} profession={4} pf={5} location={6} filter1={7} filter2={8} filter3={9} searcherPF={10}",
                    candidateClient.Character.characterName,
                    candidateId,
                    candidateSide,
                    candidateLevel,
                    candidateProfession,
                    candidatePlayfield,
                    candidateLocation,
                    filter1,
                    filter2,
                    filter3,
                    searcherPlayfield);

                /*
                 * ====================================================
                 * SIDE FILTER
                 * ====================================================
                 */

                if (filter0 != UInt32.MaxValue)
                {
                    if (candidateSide !=
                        (int)filter0)
                    {
                        skippedSide++;

                        client.Server.Debug(
                            client,
                            "LFT SIDE REJECT: candidate={0} candidateSide={1} filter0={2}",
                            candidateClient.Character.characterName,
                            candidateSide,
                            filter0);

                        continue;
                    }
                }

                /*
                 * ====================================================
                 * LEVEL FILTER
                 * ====================================================
                 */

                if (!TeamLevelRanges.IsCompatible(
                        searcherLevel,
                        candidateLevel))
                {
                    skippedLevel++;

                    client.Server.Debug(
                        client,
                        "LFT LEVEL REJECT: candidate={0} candidateLevel={1} searcherLevel={2}",
                        candidateClient.Character.characterName,
                        candidateLevel,
                        searcherLevel);

                    continue;
                }

                /*
                 * ====================================================
                 * PROFESSION FILTER
                 * ====================================================
                 *
                 * IMPORTANT:
                 *
                 * AO LFT profession filter uses:
                 *
                 * Soldier         = 2
                 * Martial Artist  = 4
                 * Engineer        = 8
                 * Fixer           = 16
                 * Agent           = 32
                 * Adventurer      = 64
                 * Trader          = 128
                 * Bureaucrat      = 256
                 * Enforcer        = 512
                 * Doctor          = 1024
                 * Nanotechnician  = 2048
                 * Metaphysicist   = 4096
                 * Keeper          = 16384
                 * Shade           = 32768
                 *
                 * Therefore:
                 *
                 * professionBit = 1 << candidateProfession
                 *
                 * NOT:
                 *
                 * 1 << (candidateProfession - 1)
                 *
                 * Monster (13) is not an LFT profession filter.
                 */

                if (filter1 != UInt32.MaxValue)
                {
                    uint professionBit =
                        1u << candidateProfession;

                    bool professionMatches =
                        professionBit == filter1;

                    client.Server.Debug(
                        client,
                        "LFT PROFESSION CHECK: candidate={0} profession={1} professionBit={2} filter1={3} matches={4}",
                        candidateClient.Character.characterName,
                        candidateProfession,
                        professionBit,
                        filter1,
                        professionMatches);

                    if (!professionMatches)
                    {
                        skippedProfession++;

                        client.Server.Debug(
                            client,
                            "LFT PROFESSION REJECT: candidate={0} candidateProfession={1} filter1={2}",
                            candidateClient.Character.characterName,
                            candidateProfession,
                            filter1);

                        continue;
                    }
                }

                /*
                 * ====================================================
                 * LOCATION FILTER
                 * ====================================================
                 *
                 * filter2:
                 *
                 * 0 = This Playfield
                 * 1 = Anywhere
                 * 2 = Rubi-Ka
                 * 3 = Shadowlands
                 */

                if (!MatchesLocation(
                        filter2,
                        searcherPlayfield,
                        candidatePlayfield,
                        candidateId))
                {
                    skippedLocation++;

                    client.Server.Debug(
                        client,
                        "LFT LOCATION REJECT: candidate={0} candidateId={1} candidatePF={2} candidateLocation={3} filter2={4} searcherPF={5}",
                        candidateClient.Character.characterName,
                        candidateId,
                        candidatePlayfield,
                        candidateLocation,
                        filter2,
                        searcherPlayfield);

                    continue;
                }

                client.Server.Debug(
                    client,
                    "LFT LOCATION ACCEPT: candidate={0} candidateId={1} candidatePF={2} candidateLocation={3} filter2={4} searcherPF={5}",
                    candidateClient.Character.characterName,
                    candidateId,
                    candidatePlayfield,
                    candidateLocation,
                    filter2,
                    searcherPlayfield);

                /*
                 * ====================================================
                 * MATCH
                 * ====================================================
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

            /*
             * ========================================================
             * SEARCH SUMMARY
             * ========================================================
             */

            client.Server.Info(
                client,
                "{0} >> LftSearch: f0={1} f1={2} f2={3} f3={4} matches={5} searcherLvl={6} searcherSide={7} searcherPF={8} registry={9} skipOffline={10} skipLevel={11} skipSide={12} skipProfession={13} skipLocation={14}",
                client.Character.characterName,
                filter0,
                filter1,
                filter2,
                filter3,
                matches.Count,
                searcherLevel,
                searcherSide,
                searcherPlayfield,
                registryCount,
                skippedOffline,
                skippedLevel,
                skippedSide,
                skippedProfession,
                skippedLocation);

            /*
             * ========================================================
             * NAME CACHE
             * ========================================================
             */

            for (int i = 0;
                 i < matches.Count;
                 i++)
            {
                PushNameCache(
                    client,
                    matches[i]);
            }

            /*
             * ========================================================
             * SEED REMOTE CANDIDATES
             * ========================================================
             */

            NotifyZoneToSeedRemoteCandidates(
                client,
                matches);

            if (matches.Count > 0)
            {
                Thread.Sleep(
                    SeedSettleMilliseconds);

                for (int i = 0;
                     i < matches.Count;
                     i++)
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
             * ========================================================
             * SEND RESULTS
             * ========================================================
             */

            client.Send(
                LftQueryReply.CreateClear());

            for (int i = 0;
                 i < matches.Count;
                 i++)
            {
                client.Send(
                    LftQueryReply.CreateEntry(
                        matches[i]));

                PushNameCache(
                    client,
                    matches[i]);
            }
        }

        /// <summary>
        /// Checks the location of the LFT candidate.
        ///
        /// 0 = This Playfield
        /// 1 = Anywhere
        /// 2 = Rubi-Ka
        /// 3 = Shadowlands
        ///
        /// Searcher's location is used ONLY for This Playfield.
        /// RK/SL is determined ONLY from the candidate PF ID.
        /// </summary>
        private static bool MatchesLocation(
            uint filter,
            int searcherPlayfield,
            int candidatePlayfield,
            uint candidateId)
        {
            /*
             * ========================================================
             * ANYWHERE
             * ========================================================
             *
             * No location filtering at all.
             */
            if (filter == LocationAny)
            {
                return true;
            }

            /*
             * ========================================================
             * THIS PLAYFIELD
             * ========================================================
             *
             * Compare candidate PF against searcher PF.
             */
            if (filter == LocationThisPlayfield)
            {
                if (searcherPlayfield <= 0
                    || candidatePlayfield <= 0)
                {
                    return false;
                }

                return candidatePlayfield ==
                       searcherPlayfield;
            }

            /*
             * ========================================================
             * CANDIDATE LOCATION
             * ========================================================
             *
             * IMPORTANT:
             *
             * RK/SL is determined from the candidate PF ID.
             *
             * The candidate CharacterId is NOT used here.
             */
            bool candidateIsShadowlands =
                ShadowlandsPlayfieldIds.Contains(
                    (uint)Math.Max(
                        0,
                        candidatePlayfield));

            /*
             * ========================================================
             * RUBI-KA
             * ========================================================
             *
             * If PF is NOT in the SL PF list,
             * candidate is considered RK.
             */
            if (filter == LocationRubiKa)
            {
                return !candidateIsShadowlands;
            }

            /*
             * ========================================================
             * SHADOWLANDS
             * ========================================================
             *
             * If PF IS in the SL PF list,
             * candidate is considered SL.
             */
            if (filter == LocationShadowlands)
            {
                return candidateIsShadowlands;
            }

            /*
             * Unknown location value.
             */
            return false;
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

            client.Send(
                PlayerName.Create(
                    entry.CharacterId,
                    name));

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
        /// Resolve current playfield.
        ///
        /// Used for:
        ///
        ///     - This Playfield
        ///     - Displaying candidate PF
        ///
        /// It is also used to determine RK/SL by
        /// checking the resulting PF ID against
        /// ShadowlandsPlayfieldIds.
        /// </summary>
        private static int ResolvePlayfield(
            uint characterId)
        {
            int live;

            if (LftPlayfieldRegistry.TryGet(
                    characterId,
                    out live)
                && live > 0)
            {
                return live;
            }

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
        /// Tell ZoneEngine to seed remote candidates.
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
        /// Read character stat.
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
