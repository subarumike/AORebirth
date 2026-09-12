namespace ZoneEngine_New.Core.Mobs
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AORebirth.Core.Playfields;

    /// <summary>
    /// An accepted placement/variant adapter supplies ORIGINAL evidence identity.
    /// A newly allocated runtime dynel ID or native hash is not that evidence.
    /// </summary>
    internal sealed record CapturedNpcCombatEvidence(int ResourceId, string Name, int MonsterData,
        int Level, int SourceIdentity, string? ProfileSelector = null);

    internal static class CapturedNpcCombatResolver
    {
        private static readonly CapturedEnemyCombatProfileDefinition[] Profiles = CapturedEnemyCombatGeneratedProfiles.Create();

        internal static bool TryResolve(CapturedNpcCombatEvidence evidence,
            out CapturedEnemyCombatProfileDefinition profile, out string failure)
            => TryResolve(Profiles, evidence, out profile, out failure);

        internal static bool TryResolve(IEnumerable<CapturedEnemyCombatProfileDefinition> profiles,
            CapturedNpcCombatEvidence evidence, out CapturedEnemyCombatProfileDefinition profile, out string failure)
        {
            profile = null!;
            if (evidence == null || evidence.ResourceId <= 0 || string.IsNullOrWhiteSpace(evidence.Name)
                || evidence.MonsterData <= 0 || evidence.Level <= 0 || evidence.SourceIdentity == 0)
            { failure = "An exact accepted source identity, resource, appearance and level are required."; return false; }

            var matches = profiles.Where(candidate => candidate.ResourceId == evidence.ResourceId
                && candidate.MonsterData == evidence.MonsterData && candidate.Level == evidence.Level
                && string.Equals(candidate.Name, evidence.Name, StringComparison.Ordinal)
                && candidate.SourceIdentities.Contains(evidence.SourceIdentity)
                && (string.IsNullOrWhiteSpace(evidence.ProfileSelector)
                    || string.Equals(candidate.ProfileId, evidence.ProfileSelector, StringComparison.Ordinal))).ToArray();
            if (matches.Length != 1)
            { failure = "No unique accepted generated combat profile matches the complete source key."; return false; }
            if (!matches[0].CaptureRuntimeEvidenceSafe)
            { failure = "The exact generated profile has not passed capture and runtime-initialization evidence gates."; return false; }
            profile = matches[0];
            failure = string.Empty;
            return true;
        }
    }
}
