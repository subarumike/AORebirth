namespace ZoneEngine_New.Core.Data
{
    /// <summary>One organizations row (the Clan stat is its Id) with the leader's character name.</summary>
    public sealed record OrganizationRecord(
        int Id, System.DateTime Creation, string Name, int LeaderId, string LeaderName,
        int GovernmentForm, string Description, string Objective, string History, long Tax = 0, long Bank = 0);

    /// <summary>Read access to the organizations table.</summary>
    public interface IOrganizationRepository
    {
        OrganizationRecord? GetById(int organizationId);

        bool IsNameTaken(string name, int exceptOrganizationId);

        void SaveText(int organizationId, AORebirth.Interfaces.Persistence.Characters.OrganizationTextField field, string value);

        /// <summary>Stores the form and resets every member except the leader to <paramref name="lowestRank"/>.</summary>
        void SaveGoverningForm(int organizationId, int governingForm, int leaderId, int lowestRank);

        /// <summary>Stores the rank of one member (promote and demote).</summary>
        void SaveRank(int characterId, int rank);

        /// <summary>Stores the organization membership of one character (join).</summary>
        void SaveMembership(int characterId, int organizationId);

        /// <summary>Stores the new leader (presidency transfer).</summary>
        void SaveLeader(int organizationId, int leaderId);

        /// <summary>Stores the organization tax (whole credits, zero or positive).</summary>
        void SaveTax(int organizationId, int tax);

        /// <summary>Organization invite lockout for this character, or null when none exists.</summary>
        AORebirth.Interfaces.Persistence.Characters.PersistedOrgLockoutData GetLockout(int characterId);

        /// <summary>Stores the remaining organization lockout seconds of one character.</summary>
        void SaveLockout(int characterId, string characterName, long secondsRemaining);

        /// <summary>Stores one Anarchism kick request under the organization lock; reached quorums consume their requests.</summary>
        AORebirth.Interfaces.Persistence.Characters.PersistedAnarchistKickResult RecordAnarchistKick(int organizationId,
            int kickedId, string kickedName, int kickerId, string kickerName, int memberQuorum, int leaderQuorum);

        /// <summary>Moves credits between one character's Cash and the Bank under the organization lock; caps are enforced there.</summary>
        AORebirth.Interfaces.Persistence.Characters.PersistedOrganizationBankResult TransferBank(int organizationId, int characterId,
            bool deposit, long amount, long bankCap, long cashCap);

        /// <summary>Tax schedule of one member (org_member_tax), or null when none is stored.</summary>
        AORebirth.Interfaces.Persistence.Characters.PersistedOrgMemberTaxData GetMemberTax(int characterId);

        /// <summary>Inserts or updates the tax schedule of one member.</summary>
        void SaveMemberTax(int characterId, AORebirth.Interfaces.Persistence.Characters.PersistedOrgMemberTaxData data);

        /// <summary>Removes the membership of one character (leave, kick, side change); also deletes its tax row.</summary>
        void ClearMembership(int characterId);

        /// <summary>Character ids that are members of the organization (stored Clan stat).</summary>
        System.Collections.Generic.IList<int> GetMemberIds(int organizationId);

        /// <summary>Clears every member and deletes the organization.</summary>
        void Disband(int organizationId);

        /// <summary>Stored rank (ClanLevel) of one character, 0 when none is stored.</summary>
        int GetStoredRank(int characterId);

        /// <summary>Organization id (stored Clan stat) of one character, 0 when orgless.</summary>
        int GetOrganizationIdOf(int characterId);
    }
}
