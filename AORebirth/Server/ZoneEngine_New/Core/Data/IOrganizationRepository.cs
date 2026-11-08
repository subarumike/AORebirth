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

        /// <summary>Organization invite lockout for this character, or null when none exists.</summary>
        AORebirth.Interfaces.Persistence.Characters.PersistedOrgLockoutData GetLockout(int characterId);
    }
}
