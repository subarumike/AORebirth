namespace ZoneEngine_New.Core.Data
{
    using AORebirth.Interfaces.Persistence.Characters;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using static SharedCharacterPersistence;

    /// <summary>Reads <c>organizations</c> through the shared character persistence DAO.</summary>
    public sealed class MySqlOrganizationRepository : IOrganizationRepository
    {
        readonly ICharacterPersistenceDao _persistence;

        public MySqlOrganizationRepository(ICharacterPersistenceDao? persistence = null)
            => _persistence = persistence ?? Create();

        public OrganizationRecord? GetById(int organizationId)
        {
            if (organizationId <= 0)
                return null;

            PersistedOrganizationData? row = Run(() => _persistence.LoadOrganization(organizationId));
            return row == null
                ? null
                : new OrganizationRecord(row.Id, row.Creation, row.Name, row.LeaderId, row.LeaderName,
                    row.GovernmentForm, row.Description, row.Objective, row.History, row.Tax, row.Bank);
        }

        public bool IsNameTaken(string name, int exceptOrganizationId)
            => Run(() => _persistence.IsOrganizationNameTaken(name, exceptOrganizationId));

        public void SaveText(int organizationId, OrganizationTextField field, string value)
            => Run(() => _persistence.SaveOrganizationText(organizationId, field, value));

        public void SaveGoverningForm(int organizationId, int governingForm, int leaderId, int lowestRank)
            => Run(() => _persistence.SaveOrganizationGoverningForm(organizationId, governingForm, leaderId, lowestRank));

        public void SaveRank(int characterId, int rank)
            => Run(() => _persistence.SaveOrganizationRank(characterId, rank));

        public void SaveMembership(int characterId, int organizationId)
            => Run(() => _persistence.SaveOrganizationMembership(characterId, organizationId));

        public void SaveLeader(int organizationId, int leaderId)
            => Run(() => _persistence.SaveOrganizationLeader(organizationId, leaderId));

        public AORebirth.Interfaces.Persistence.Characters.PersistedOrgLockoutData GetLockout(int characterId)
            => Run(() => _persistence.LoadOrgLockout(characterId));

        public void SaveLockout(int characterId, string characterName, long secondsRemaining)
                 => Run(() => _persistence.SaveOrgLockout(characterId, characterName, secondsRemaining));

            public AORebirth.Interfaces.Persistence.Characters.PersistedAnarchistKickResult RecordAnarchistKick(int organizationId,
                int kickedId, string kickedName, int kickerId, string kickerName, int memberQuorum, int leaderQuorum)
                => Run(() => _persistence.RecordAnarchistKick(organizationId, kickedId, kickedName, kickerId, kickerName,
                    memberQuorum, leaderQuorum));

        public AORebirth.Interfaces.Persistence.Characters.PersistedOrganizationBankResult TransferBank(int organizationId, int characterId,
            bool deposit, long amount, long bankCap, long cashCap)
            => Run(() => _persistence.TransferOrganizationBank(organizationId, characterId, deposit, amount, bankCap, cashCap));

        public void ClearMembership(int characterId)
            => Run(() => _persistence.ClearOrganizationMembership(characterId));

        public System.Collections.Generic.IList<int> GetMemberIds(int organizationId)
            => Run(() => _persistence.LoadOrganizationMemberIds(organizationId));

        public void Disband(int organizationId)
            => Run(() => _persistence.DisbandOrganization(organizationId));

        public int GetStoredRank(int characterId)
            => Run(() => ReadStoredRank(_persistence.LoadStats(characterId)));

        public int GetOrganizationIdOf(int characterId)
            => Run(() => _persistence.LoadOrganizationId(characterId));

        static int ReadStoredRank(System.Collections.Generic.IList<CharacterStatData> stats)
        {
            foreach (CharacterStatData stat in stats)
            {
                if (stat.StatId == (int)CharacterStat.ClanLevel)
                    return stat.StatValue;
            }

            return 0;
        }
    }
}
