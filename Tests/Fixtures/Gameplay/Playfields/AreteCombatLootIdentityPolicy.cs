namespace AORebirth.Core.Playfields
{
    using System;

    internal static class AreteCombatLootIdentityPolicy
    {
        internal const string EngineerAutomatonName = "Engineer Automaton I";

        internal const string EngineerAutomatonLootProfileKey =
            "captured.arete.engineer-automaton-I.unsupported";

        internal static void Apply(
            LootGenerationContext context,
            CapturedEnemyCombatContract combatContract,
            int playfieldId,
            string actorName)
        {
            if (context == null)
            {
                return;
            }

            if (combatContract != null)
            {
                context.CombatReady = combatContract.IsCombatReady;
                context.CombatEvidenceSourceIdentity = combatContract.IsCombatReady
                                                           ? combatContract.EvidenceSourceIdentity
                                                           : combatContract.EvidenceSourceIdentityHint;
                context.CombatProfileSelector = combatContract.IsCombatReady
                                                    ? combatContract.CaptureProvenArchetypeId
                                                    : combatContract.EvidenceProfileSelectorHint;
            }

            if (playfieldId != 6553
                || !string.Equals(
                    actorName,
                    EngineerAutomatonName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            context.EnemyProfileKey = EngineerAutomatonLootProfileKey;
            context.SuppressMonsterDataFallbackLoot = true;
        }
    }
}
