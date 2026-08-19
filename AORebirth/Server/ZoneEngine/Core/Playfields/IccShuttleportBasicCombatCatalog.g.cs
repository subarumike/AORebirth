namespace AORebirth.Core.Playfields
{
    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Playfields;

    internal static class IccShuttleportBasicCombatCatalog
    {
        private const int IccShuttleportPlayfieldId = 4582;

        private const int IslandReetMonsterData = 30365;

        private const int IslandReetLevel = 1;

        private const int IslandReetEvidenceSourceIdentity = 0x0011CE48;

        private const string IslandReetAggregateCombatSha256 =
            "980e878a61bea869f03009a6657e3f15134b9d0b2a46cf98685842a24d543c6f";

        internal static CapturedEnemyCombatContract IslandReet()
        {
            return CapturedEnemyCombatContract.CapturedBasicOrdinaryCombat(
                IslandReetEvidence(),
                IslandReetEvidenceSourceIdentity,
                new CapturedBasicCombatContractDefinition(
                    "Island Reet",
                    IccShuttleportPlayfieldId,
                    IslandReetMonsterData,
                    IslandReetLevel,
                    IslandReetAggregateCombatSha256,
                    new[]
                    {
                        "ICC Shuttleport [PF 4582] - 20260819-014109",
                        "ICC Shuttleport [PF 4582] - 20260819-015104"
                    },
                    28,
                    15,
                    12,
                    12,
                    2,
                    8,
                    true,
                    CapturedBasicCombatFieldAuthority.Captured,
                    CapturedBasicCombatFieldAuthority.Captured,
                    CapturedBasicCombatFieldAuthority.Captured,
                    CapturedBasicCombatFieldAuthority.GovernedDerived,
                    CapturedBasicCombatFieldAuthority.GenericRuntimePolicy,
                    CapturedBasicCombatFieldAuthority.OptionalPositiveBehavior,
                    new[]
                    {
                        new CapturedBasicCombatStreamDefinition(
                            1,
                            1,
                            -1,
                            NpcCombatAttackRules.NormalAttackInfoHitType,
                            0,
                            0,
                            new[] { 3.034263d, 3.026428d },
                            new[]
                            {
                                11.197838d,
                                11.200547d,
                                6.222159d,
                                11.597101d,
                                12.110255d,
                                6.203458d
                            },
                            new[]
                            {
                                new CapturedBasicCombatDamageObservation(4, 0, "20260819-014109 enemy-combat.csv sequence=2013"),
                                new CapturedBasicCombatDamageObservation(8, 0, "20260819-014109 enemy-combat.csv sequence=2821"),
                                new CapturedBasicCombatDamageObservation(5, 0, "20260819-014109 enemy-combat.csv sequence=3586"),
                                new CapturedBasicCombatDamageObservation(6, 4, "20260819-014109 enemy-combat.csv sequence=3984"),
                                new CapturedBasicCombatDamageObservation(5, 0, "20260819-015104 enemy-combat.csv sequence=893"),
                                new CapturedBasicCombatDamageObservation(5, 0, "20260819-015104 enemy-combat.csv sequence=1711"),
                                new CapturedBasicCombatDamageObservation(5, 0, "20260819-015104 enemy-combat.csv sequence=2510"),
                                new CapturedBasicCombatDamageObservation(4, 0, "20260819-015104 enemy-combat.csv sequence=2911")
                            }),
                        new CapturedBasicCombatStreamDefinition(
                            0,
                            0,
                            -1,
                            NpcCombatAttackRules.NormalAttackInfoHitType,
                            0,
                            0,
                            new[] { 8.168862d, 8.420326d },
                            new[] { 13.100342d, 14.089759d },
                            new[]
                            {
                                new CapturedBasicCombatDamageObservation(7, 0, "20260819-014109 enemy-combat.csv sequence=2365"),
                                new CapturedBasicCombatDamageObservation(10, 0, "20260819-014109 enemy-combat.csv sequence=3320"),
                                new CapturedBasicCombatDamageObservation(12, 0, "20260819-015104 enemy-combat.csv sequence=1261"),
                                new CapturedBasicCombatDamageObservation(12, 0, "20260819-015104 enemy-combat.csv sequence=2223")
                            })
                    }),
                false,
                NpcAiProfile.Passive);
        }

        private static string IslandReetEvidence()
        {
            return "PF4582 Island Reet basic combat aggregate"
                   + "; combatCsvSha256[20260819-014109]=baafd2fdf72d862dc3a47becbf1df3d7c20e41f53332c5cf2ab6d49df37b9898"
                   + "; combatCsvSha256[20260819-015104]=66ec6fa5820a0e438c48aeadf72a7f8f9f8f1d8a5f1603d291c781dad80f4f00"
                   + "; combatCsvConcatSha256=3764a95cb9a982f2d0401fbf1504396f04685c2d0d76d87d913c207a3281c2fc"
                   + "; fullUpdateCsvSha256[20260819-014109]=1ada5361fc29f2733c389f23071fd8f1afc3a2671f6dedc5399e89b97c22ad67"
                   + "; fullUpdateCsvSha256[20260819-015104]=9a15dfbcab9181a88b7ff0a75ad6765d6dad5c3619f27ed621b9bff4a95a00eb"
                   + "; aggregateCombatSha256=" + IslandReetAggregateCombatSha256;
        }
    }
}
