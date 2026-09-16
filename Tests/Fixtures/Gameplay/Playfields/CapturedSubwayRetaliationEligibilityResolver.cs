namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;

    internal static class CapturedSubwayRetaliationEligibilityResolver
    {
        private const int SubwayPlayfieldResourceId = 127;

        private const string DiscardedPetRetaliationEvidence =
            "20260708-143600,20260709-205921,20260709-210452,20260709-220439: "
            + "27 player-attack-to-NPC-Attack retaliation rows and 41 Discarded Pet "
            + "AttackInfo rows prove retaliation eligibility; the exact catalog remains "
            + "the sole owner of packet and damage semantics";

        private const string MuggerRetaliationEvidence =
            "20260709-193914,20260709-205921,20260709-210452,20260709-212115,"
            + "20260709-212336,20260709-220439,20260709-222339,20260709-225408,"
            + "20260710-205400,20260710-211430,20260719-021022: 20 retaliation rows "
            + "and 41 Mugger AttackInfo rows prove retaliation eligibility; the exact "
            + "catalog remains the sole owner of packet and damage semantics";

        private static readonly Dictionary<int, CapturedSubwayRetaliationBinding> Bindings =
            new Dictionary<int, CapturedSubwayRetaliationBinding>
            {
                { 0x794DF1E5, new CapturedSubwayRetaliationBinding("Discarded Pet", 17720, 5, DiscardedPetRetaliationEvidence) },
                { 0x794E83C1, new CapturedSubwayRetaliationBinding("Discarded Pet", 17720, 7, DiscardedPetRetaliationEvidence) },
                { 0x79528F6A, new CapturedSubwayRetaliationBinding("Discarded Pet", 17720, 9, DiscardedPetRetaliationEvidence) },
                { 0x79528FDA, new CapturedSubwayRetaliationBinding("Discarded Pet", 17720, 8, DiscardedPetRetaliationEvidence) },
                { 0x795317D6, new CapturedSubwayRetaliationBinding("Discarded Pet", 17720, 5, DiscardedPetRetaliationEvidence) },
                { 0x7953AA04, new CapturedSubwayRetaliationBinding("Discarded Pet", 17720, 10, DiscardedPetRetaliationEvidence) },
                { 0x7953AA1B, new CapturedSubwayRetaliationBinding("Discarded Pet", 17720, 10, DiscardedPetRetaliationEvidence) },
                { 0x7953AA82, new CapturedSubwayRetaliationBinding("Discarded Pet", 17720, 10, DiscardedPetRetaliationEvidence) },
                { 0x7953AC01, new CapturedSubwayRetaliationBinding("Discarded Pet", 17720, 6, DiscardedPetRetaliationEvidence) },
                { 0x7953AD3C, new CapturedSubwayRetaliationBinding("Discarded Pet", 17720, 8, DiscardedPetRetaliationEvidence) },
                { 0x7953AD5F, new CapturedSubwayRetaliationBinding("Discarded Pet", 17720, 10, DiscardedPetRetaliationEvidence) },
                { 0x7953AD6D, new CapturedSubwayRetaliationBinding("Discarded Pet", 17720, 10, DiscardedPetRetaliationEvidence) },
                { 0x7953AD6F, new CapturedSubwayRetaliationBinding("Discarded Pet", 17720, 10, DiscardedPetRetaliationEvidence) },
                { 0x7953AD74, new CapturedSubwayRetaliationBinding("Discarded Pet", 17720, 10, DiscardedPetRetaliationEvidence) },
                { 0x7953AF53, new CapturedSubwayRetaliationBinding("Discarded Pet", 17720, 6, DiscardedPetRetaliationEvidence) },
                { 0x7953AF66, new CapturedSubwayRetaliationBinding("Discarded Pet", 17720, 5, DiscardedPetRetaliationEvidence) },
                { 0x7953AF74, new CapturedSubwayRetaliationBinding("Discarded Pet", 17720, 5, DiscardedPetRetaliationEvidence) },
                { 0x7953AF99, new CapturedSubwayRetaliationBinding("Discarded Pet", 17720, 6, DiscardedPetRetaliationEvidence) },
                { 0x79557C09, new CapturedSubwayRetaliationBinding("Discarded Pet", 17720, 9, DiscardedPetRetaliationEvidence) },
                { 0x79557C26, new CapturedSubwayRetaliationBinding("Discarded Pet", 17720, 7, DiscardedPetRetaliationEvidence) },
                { 0x79557C31, new CapturedSubwayRetaliationBinding("Discarded Pet", 17720, 5, DiscardedPetRetaliationEvidence) },
                { 0x79557C8B, new CapturedSubwayRetaliationBinding("Discarded Pet", 17720, 10, DiscardedPetRetaliationEvidence) },
                { 0x79557CA7, new CapturedSubwayRetaliationBinding("Discarded Pet", 17720, 8, DiscardedPetRetaliationEvidence) },
                { 0x79557CAB, new CapturedSubwayRetaliationBinding("Discarded Pet", 17720, 10, DiscardedPetRetaliationEvidence) },
                { 0x79557CAD, new CapturedSubwayRetaliationBinding("Discarded Pet", 17720, 10, DiscardedPetRetaliationEvidence) },
                { 0x7957E411, new CapturedSubwayRetaliationBinding("Discarded Pet", 17720, 10, DiscardedPetRetaliationEvidence) },
                { 0x7957E4A5, new CapturedSubwayRetaliationBinding("Discarded Pet", 17720, 6, DiscardedPetRetaliationEvidence) },
                { 0x7957E4B1, new CapturedSubwayRetaliationBinding("Discarded Pet", 17720, 5, DiscardedPetRetaliationEvidence) },
                { 0x7957E4BC, new CapturedSubwayRetaliationBinding("Discarded Pet", 17720, 8, DiscardedPetRetaliationEvidence) },
                { 0x7953AA11, new CapturedSubwayRetaliationBinding("Mugger", 203734, 8, MuggerRetaliationEvidence) },
                { 0x795450D4, new CapturedSubwayRetaliationBinding("Mugger", 203734, 5, MuggerRetaliationEvidence) },
                { 0x7957E5C6, new CapturedSubwayRetaliationBinding("Mugger", 203734, 9, MuggerRetaliationEvidence) },
                { 0x7957E5C7, new CapturedSubwayRetaliationBinding("Mugger", 203734, 8, MuggerRetaliationEvidence) },
                { 0x7957E5C8, new CapturedSubwayRetaliationBinding("Mugger", 203734, 8, MuggerRetaliationEvidence) }
            };

        internal static bool TryResolveExact(
            int resourceId,
            string name,
            int monsterData,
            int level,
            int sourceIdentity,
            CapturedEnemyCombatContract baseline,
            out CapturedEnemyCombatContract resolved,
            out string failure)
        {
            resolved = baseline;
            failure = string.Empty;
            if (resourceId != SubwayPlayfieldResourceId)
            {
                failure = "captured retaliation eligibility is limited to PF127";
                return false;
            }

            CapturedSubwayRetaliationBinding binding;
            if (!Bindings.TryGetValue(sourceIdentity, out binding)
                || !binding.Matches(name, monsterData, level))
            {
                failure = "no exact capture-proven PF127 retaliation binding";
                return false;
            }

            if (baseline == null
                || baseline.EvidenceSourceIdentityHint != sourceIdentity)
            {
                failure = "runtime combat baseline is absent or belongs to another source identity";
                return false;
            }

            CapturedEnemyCombatContract eligible =
                baseline.WithCaptureProvenRetaliationEligibility(binding.Evidence);
            CapturedEnemyCombatContract candidate;
            string catalogFailure;
            if (!CapturedEnemyCombatProfileCatalog.TryResolve(
                    resourceId,
                    name,
                    monsterData,
                    level,
                    sourceIdentity,
                    eligible,
                    out candidate,
                    out catalogFailure)
                || candidate == null
                || !candidate.IsCombatReady)
            {
                failure = "exact PF127 combat catalog resolution failed: "
                          + (string.IsNullOrWhiteSpace(catalogFailure)
                                 ? "resolved contract is not combat-ready"
                                 : catalogFailure);
                return false;
            }

            resolved = candidate.WithCaptureProvenRetaliationEligibility(
                binding.Evidence);
            return true;
        }

        private sealed class CapturedSubwayRetaliationBinding
        {
            internal CapturedSubwayRetaliationBinding(
                string name,
                int monsterData,
                int level,
                string evidence)
            {
                this.Name = name;
                this.MonsterData = monsterData;
                this.Level = level;
                this.Evidence = evidence;
            }

            private string Name { get; set; }

            private int MonsterData { get; set; }

            private int Level { get; set; }

            internal string Evidence { get; private set; }

            internal bool Matches(string name, int monsterData, int level)
            {
                return string.Equals(this.Name, name, StringComparison.Ordinal)
                       && this.MonsterData == monsterData
                       && this.Level == level;
            }
        }
    }
}
