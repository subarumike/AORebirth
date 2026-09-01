namespace AORebirth.Core.Playfields
{
    using System.Globalization;

    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;
    using AORebirth.Enums;

    using Utility;

    /// <summary>
    /// Dyn ACG dungeon leases live only in memory. After ZoneEngine restart (or cold login),
    /// the saved playfield id is still in the 0x00208xxx band but no longer in D1/D2/D3
    /// lease dictionaries — D1's old band-wide IsDungeonPlayfield then stole the PF and
    /// stamped the wrong cave (Mike: logoff in D3 → logon in D2).
    /// Re-adopt using externaldoor / exterior PF / interior X before PlayfieldById.
    /// </summary>
    internal static class NascenceDungeonLeaseRehydrate
    {
        private const int AcgBandFloor = unchecked((int)0x00208000);

        private const int AcgBandMask = 0x00007FFF;

        private const int CharacterStatType = 50000;

        internal static void RehydrateBeforePlayfieldCreate(DBCharacter character)
        {
            if (character == null)
            {
                return;
            }

            int playfieldId = character.Playfield;
            if (!IsAcgBand(playfieldId)
                && playfieldId != NascenceDungeon1Rules.DungeonPlayfieldId
                && playfieldId != NascenceDungeon1Rules.ReservedDungeonPlayfieldId
                && playfieldId != NascenceDungeon2Rules.DungeonPlayfieldId
                && playfieldId != NascenceDungeon2Rules.LegacyDungeonPlayfieldId
                && playfieldId != NascenceDungeon3Rules.DungeonPlayfieldId
                && playfieldId != NascenceDungeon4Rules.DungeonPlayfieldId)
            {
                return;
            }

            // Still owned in this process (same-session reconnect) — nothing to do.
            if (NascenceDungeon4Rules.IsDungeonPlayfield(playfieldId)
                || NascenceDungeon3Rules.IsDungeonPlayfield(playfieldId)
                || NascenceDungeon2Rules.IsDungeonPlayfield(playfieldId)
                || NascenceDungeon1Rules.IsDungeonPlayfield(playfieldId))
            {
                return;
            }

            int door = ReadStat(character.Id, (int)StatIds.externaldoorinstance);
            int exterior = ReadStat(character.Id, (int)StatIds.externalplayfieldinstance);

            string owner = null;
            if (door == unchecked((int)NascenceDungeon4Rules.AcgEntranceInstanceStat))
            {
                NascenceDungeon4Rules.AdoptLease(playfieldId);
                owner = "D4";
            }
            else if (door == unchecked((int)NascenceDungeon3Rules.AcgEntranceInstanceStat)
                || exterior == NascenceDungeon3Rules.SourcePlayfieldId
                || character.X >= 1100f)
            {
                NascenceDungeon3Rules.AdoptLease(playfieldId);
                owner = "D3";
            }
            else if (door == unchecked((int)NascenceDungeon2Rules.AcgEntranceInstanceStat))
            {
                NascenceDungeon2Rules.AdoptLease(playfieldId);
                owner = "D2";
            }
            else if (door == unchecked((int)NascenceDungeon1Rules.AcgEntranceInstanceStat))
            {
                NascenceDungeon1Rules.AdoptLease(playfieldId);
                owner = "D1";
            }

            if (owner == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "NascenceDungeonLeaseRehydrate skip ambiguous pf={0} door={1:X8} exterior={2} xyz=({3:0.#},{4:0.#},{5:0.#}) char={6}",
                        playfieldId,
                        door,
                        exterior,
                        character.X,
                        character.Y,
                        character.Z,
                        character.Id));
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "NascenceDungeonLeaseRehydrate adopt={0} pf={1} door={2:X8} exterior={3} xyz=({4:0.#},{5:0.#},{6:0.#}) char={7}",
                    owner,
                    playfieldId,
                    door,
                    exterior,
                    character.X,
                    character.Y,
                    character.Z,
                    character.Id));
        }

        private static bool IsAcgBand(int playfieldInstance)
        {
            return (playfieldInstance & ~AcgBandMask) == AcgBandFloor;
        }

        private static int ReadStat(int characterId, int statId)
        {
            DBStats row = StatDao.Instance.GetById(CharacterStatType, characterId, statId);
            return row == null ? 0 : row.StatValue;
        }
    }
}
