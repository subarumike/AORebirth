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
    /// </summary>
    public static class LftSearch
    {
        private static readonly int ProfessionStatId = (int)StatIds.profession;

        private static readonly int LevelStatId = (int)StatIds.level;

        /// <summary>Must match ZoneEngine.Core.LftInviteClientPresence.LftSeedCommandPrefix.</summary>
        private const string LftSeedCommandPrefix = "#aorebirth-lft-seed";

        /// <summary>
        /// Zone pushes playfields then seeds; wait so Location is not "Not found"
        /// and Invite arm is ready before rows are clickable.
        /// </summary>
        private const int SeedSettleMilliseconds = 1500;

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

            if (client == null || client.Character == null || client.Character.CharacterId == 0)
            {
                return;
            }

            int searcherLevel = ReadStat(client.Character.CharacterId, LevelStatId);
            if (searcherLevel < 1)
            {
                searcherLevel = 1;
            }

            if (searcherLevel > 220)
            {
                searcherLevel = 220;
            }

            List<LftQueryReply.Entry> matches = new List<LftQueryReply.Entry>();

            foreach (KeyValuePair<uint, string> registration in LftRegistry.Snapshot())
            {
                uint candidateId = registration.Key;
                if (candidateId == client.Character.CharacterId)
                {
                    continue;
                }

                Client candidateClient;
                if (!client.ChatServer().ConnectedClients.TryGetValue(candidateId, out candidateClient)
                    || candidateClient == null
                    || candidateClient.Character == null)
                {
                    continue;
                }

                int candidateProfession = ReadStat(candidateId, ProfessionStatId);
                int candidatePlayfield = ResolvePlayfield(candidateId);
                int candidateSide = candidateClient.Character.CharacterSide;
                int candidateLevel = ReadStat(candidateId, LevelStatId);
                if (candidateLevel < 1)
                {
                    candidateLevel = 1;
                }

                if (candidateLevel > 220)
                {
                    candidateLevel = 220;
                }

                // Only list characters inside the searcher's XP/SK share window.
                // Out-of-range invites are what pop the client "too high" warn.
                if (!TeamLevelRanges.IsCompatible(searcherLevel, candidateLevel))
                {
                    continue;
                }

                matches.Add(
                    new LftQueryReply.Entry
                    {
                        CharacterId = candidateId,
                        Name = candidateClient.Character.characterName ?? string.Empty,
                        Level = (uint)candidateLevel,
                        Playfield = (uint)Math.Max(0, candidatePlayfield),
                        Side = (byte)(candidateSide & 0xFF),
                        Profession = (byte)(candidateProfession & 0xFF),
                        Comment = registration.Value ?? string.Empty
                    });
            }

            client.Server.Info(
                client,
                "{0} >> LftSearch: f0={1} f1={2} f2={3} f3={4} matches={5} searcherLvl={6}",
                client.Character.characterName,
                filter0,
                filter1,
                filter2,
                filter3,
                matches.Count,
                searcherLevel);

            // Chat type 20 PlayerName MUST be on the searcher before Invite, or GUI shows NoName
            // and Yes re-checks forever (Yes-loop). Type 21 NameLookup is name→id reply only.
            for (int i = 0; i < matches.Count; i++)
            {
                PushNameCache(client, matches[i]);
            }

            // Seed Zone AFTER name cache so Invite sees name + dynel level together.
            NotifyZoneToSeedRemoteCandidates(client, matches);
            if (matches.Count > 0)
            {
                Thread.Sleep(SeedSettleMilliseconds);
                for (int i = 0; i < matches.Count; i++)
                {
                    int pf = ResolvePlayfield(matches[i].CharacterId);
                    if (pf > 0)
                    {
                        matches[i].Playfield = (uint)pf;
                    }
                }
            }

            client.Send(LftQueryReply.CreateClear());
            for (int i = 0; i < matches.Count; i++)
            {
                client.Send(LftQueryReply.CreateEntry(matches[i]));
                // Push name again after the row — Invite must resolve id→name.
                PushNameCache(client, matches[i]);
            }
        }

        private static void PushNameCache(Client client, LftQueryReply.Entry entry)
        {
            if (client == null || entry == null || entry.CharacterId == 0)
            {
                return;
            }

            string name = entry.Name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            // Same packet tell/channel use for id→name (MessageType.CharacterName = 20).
            client.Send(PlayerName.Create(entry.CharacterId, name));
            // Also type 21 in case some GUI paths share that table.
            client.Send(NameLookupResult.Create(entry.CharacterId, name));
            if (!client.KnownClients.Contains(entry.CharacterId))
            {
                client.KnownClients.Add(entry.CharacterId);
            }
        }

        private static int ResolvePlayfield(uint characterId)
        {
            int live;
            if (LftPlayfieldRegistry.TryGet(characterId, out live) && live > 0)
            {
                return live;
            }

            try
            {
                DBCharacter dbCharacter = CharacterDao.Instance.Get((int)characterId);
                if (dbCharacter != null && dbCharacter.Playfield > 0)
                {
                    return dbCharacter.Playfield;
                }
            }
            catch (Exception)
            {
            }

            return 0;
        }

        private static void NotifyZoneToSeedRemoteCandidates(Client client, List<LftQueryReply.Entry> matches)
        {
            if (client == null || matches == null || matches.Count == 0 || Program.ISCom == null)
            {
                return;
            }

            var command = new StringBuilder(LftSeedCommandPrefix);
            for (int i = 0; i < matches.Count; i++)
            {
                LftQueryReply.Entry entry = matches[i];
                command.Append(' ');
                command.Append(entry.CharacterId);
                if (!string.IsNullOrWhiteSpace(entry.Name))
                {
                    command.Append(':');
                    command.Append(entry.Name.Trim().Replace(' ', '_'));
                }
            }

            Program.ISCom.BroadCast(
                new AORebirth.Communication.Messages.ChatCommand
                {
                    CharacterId = unchecked((int)client.Character.CharacterId),
                    ChatCommandString = command.ToString()
                });
        }

        private static int ReadStat(uint characterId, int statId)
        {
            try
            {
                return StatDao.Instance.GetById(50000, (int)characterId, statId).StatValue;
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}
