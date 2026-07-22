namespace AORebirth.Core.Playfields
{
    using ZoneEngine.Core.Playfields;

    internal static class CapturedTempleOfThreeWindsCombatCatalog
    {
        internal const int DefenderNormalDamage = 43;
        internal const double DefenderFirstSuccessfulHitDelaySeconds = 10.915985;
        internal const double DefenderAttackRechargePolicySeconds = 10.915985;
        internal const int DefenderAttackInfoWeaponInstance = 1465538645;
        internal const int DefenderSpecialAttackLowTemplate = 205877;
        internal const int DefenderSpecialAttackHighTemplate = 205878;
        internal const string DefenderSpecialAttackName = "WZXU";

        internal const double TempleNamedMeleeRechargePolicySeconds = 3.2;
        internal const double YatilaOnePerFightStreamRechargePolicySeconds = 600.0;
        internal const double EternalSentinelRechargeSeconds = 5.67;
        internal const double CuratorFirstSuccessfulHitDelaySeconds = 3.3000414;
        internal const double CuratorRechargeSeconds = 5.8796741;
        internal const double NematetSlotTwoFirstHitDelaySeconds = 18.9557914;
        internal const double NematetSlotTwoRechargeSeconds = 40.0845743;
        internal const double NematetSlotZeroFirstHitDelaySeconds = 38.8398163;
        internal const double NematetSlotZeroRechargeSeconds = 10.06033;
        internal const double NematetSlotOneFirstHitDelaySeconds = 68.208179;
        internal const double GuardianSlotOneFirstHitDelaySeconds = 2.5179671;
        internal const double GuardianSlotZeroFirstHitDelaySeconds = 3.8369566;
        internal const double GuardianAttackRechargeSeconds = 4.25;
        internal const double GartuaFirstHitDelaySeconds = 5.3602725;
        internal const double GartuaAttackRechargeSeconds = 5.3;

        internal static CapturedEnemyCombatContract DefenderOfTheThree()
        {
            var normalAttack = new CapturedEnemyCombatAttackDefinition(
                DefenderNormalDamage,
                DefenderNormalDamage,
                0,
                NpcCombatAttackRules.MaxMeleeCombatDistance,
                DefenderAttackRechargePolicySeconds,
                false,
                -1,
                0,
                0,
                0,
                DefenderAttackInfoWeaponInstance,
                true);
            return CapturedEnemyCombatContract.CapturedSpecialSequence(
                "20260721-035526/040324: two normal local-player AttackInfo outcomes, both 43; "
                + "weapon slot 0, unknown 0, ammo -1, weapon instance 1465538645; "
                + "SpecialAttackWeapon contains 205877/205878 tag 1465538645 name WZXU and "
                + "239/239/239/25/0; repeat cadence is unresolved, so the observed "
                + "10.915985-second attack-to-first-success interval is the private policy",
                new CapturedEnemySpecialAttackSequenceDefinition(
                    DefenderFirstSuccessfulHitDelaySeconds,
                    null,
                    normalAttack,
                    new[]
                    {
                        new CapturedEnemySpecialAttackDefinition(
                            DefenderSpecialAttackLowTemplate,
                            DefenderSpecialAttackHighTemplate,
                            DefenderAttackInfoWeaponInstance,
                            DefenderSpecialAttackName)
                    },
                    239,
                    239,
                    239,
                    25,
                    0));
        }

        internal static CapturedEnemyCombatContract WindcallerYatila()
        {
            var normal = Attack(31, 56, TempleNamedMeleeRechargePolicySeconds, 6, -1, 0);
            var largeSpecial = Attack(
                269,
                269,
                YatilaOnePerFightStreamRechargePolicySeconds,
                3,
                -1,
                1179993922);
            var smallSpecial = Attack(
                65,
                65,
                YatilaOnePerFightStreamRechargePolicySeconds,
                2,
                -1,
                1497912661);
            var mediumSpecial = Attack(
                120,
                120,
                YatilaOnePerFightStreamRechargePolicySeconds,
                1,
                -1,
                1263026755);
            return CapturedEnemyCombatContract.CapturedParallelAttackSequence(
                "20260721-041439: Yatila emitted independent AttackInfo streams at slot 6 "
                + "for 31..56, slot 3 for 269, slot 2 for 65, and slot 1 for 120; "
                + "the three special streams were observed once each, so a one-per-fight "
                + "private recharge policy avoids inventing an unsupported repeat cadence",
                new CapturedEnemyParallelAttackSequenceDefinition(
                    new[]
                    {
                        new CapturedEnemyParallelAttackStreamDefinition(0.0, normal),
                        new CapturedEnemyParallelAttackStreamDefinition(0.0, largeSpecial),
                        new CapturedEnemyParallelAttackStreamDefinition(0.0, smallSpecial),
                        new CapturedEnemyParallelAttackStreamDefinition(0.0, mediumSpecial)
                    },
                    new[]
                    {
                        new CapturedEnemySpecialAttackDefinition(207327, 207328, 1179993922, "FUGB"),
                        new CapturedEnemySpecialAttackDefinition(207324, 207325, 1497912661, "YXUU"),
                        new CapturedEnemySpecialAttackDefinition(207321, 207322, 1263026755, "KHBC")
                    },
                    413,
                    413,
                    413,
                    33,
                    0));
        }

        internal static CapturedEnemyCombatContract ReverendGulard()
        {
            return SpecialSequence(
                "20260721-042139: first local hit 23, then eight captured 37-point normal hits; "
                + "slot 6, ammo -1, weapon instance 0, and SpecialAttackWeapon 603/603/603/23/0",
                23,
                37,
                603,
                23);
        }

        internal static CapturedEnemyCombatContract ReAnimator()
        {
            return SpecialSequence(
                "20260721-043204: exact level-60 generation emitted four 72-point normal hits; "
                + "slot 6, ammo -1, weapon instance 0, and SpecialAttackWeapon 446/446/446/35/0",
                72,
                72,
                446,
                35);
        }

        internal static CapturedEnemyCombatContract AcolyteBetany()
        {
            return SpecialSequence(
                "20260721-044256: two 30-point normal ranged hits at slot 6 with captured ammo "
                + "decrementing from 27 to 24; runtime keeps the opening count 27 and "
                + "SpecialAttackWeapon 502/502/502/19/0",
                30,
                30,
                502,
                19,
                27);
        }

        internal static CapturedEnemyCombatContract TheCurator()
        {
            return CapturedEnemyCombatContract.CapturedSpecialSequence(
                "20260721-225404: The Curator initiated combat before the player attacked, "
                + "then emitted one 33-point opening hit and two 57-point normal hits at "
                + "slot 0 with ammo -1 and weapon instance 1465538645; SpecialAttackWeapon "
                + "contained 205877/205878 tag 1465538645 name WZXU and 381/381/381/31/0",
                new CapturedEnemySpecialAttackSequenceDefinition(
                    CuratorFirstSuccessfulHitDelaySeconds,
                    Attack(33, 33, CuratorRechargeSeconds, 0, -1, 1465538645),
                    Attack(57, 57, CuratorRechargeSeconds, 0, -1, 1465538645),
                    new[]
                    {
                        new CapturedEnemySpecialAttackDefinition(
                            205877,
                            205878,
                            1465538645,
                            "WZXU")
                    },
                    381,
                    381,
                    381,
                    31,
                    0));
        }

        internal static CapturedEnemyCombatContract NematetTheCustodianOfTime()
        {
            return CapturedEnemyCombatContract.CapturedParallelAttackSequence(
                "20260721-225743: Nematet emitted captured local-player normal streams at "
                + "slot 2 for 82, slot 0 for 70, and slot 1 for 152; the four-entry "
                + "SpecialAttackWeapon packet contained FUGB, YHUU, KHBC, and USW1 with "
                + "494/494/494/38/0; stream start and repeat timing preserve the observed fight",
                new CapturedEnemyParallelAttackSequenceDefinition(
                    new[]
                    {
                        new CapturedEnemyParallelAttackStreamDefinition(
                            NematetSlotTwoFirstHitDelaySeconds,
                            Attack(82, 82, NematetSlotTwoRechargeSeconds, 2, -1, 1497912661)),
                        new CapturedEnemyParallelAttackStreamDefinition(
                            NematetSlotZeroFirstHitDelaySeconds,
                            Attack(70, 70, NematetSlotZeroRechargeSeconds, 0, -1, 1431525169)),
                        new CapturedEnemyParallelAttackStreamDefinition(
                            NematetSlotOneFirstHitDelaySeconds,
                            Attack(152, 152, YatilaOnePerFightStreamRechargePolicySeconds, 1, -1, 1263026755))
                    },
                    new[]
                    {
                        new CapturedEnemySpecialAttackDefinition(207327, 207328, 1179993922, "FUGB"),
                        new CapturedEnemySpecialAttackDefinition(207324, 207325, 1497912661, "YHUU"),
                        new CapturedEnemySpecialAttackDefinition(207321, 207322, 1263026755, "KHBC"),
                        new CapturedEnemySpecialAttackDefinition(163491, 163492, 1431525169, "USW1")
                    },
                    494,
                    494,
                    494,
                    38,
                    0));
        }

        internal static CapturedEnemyCombatContract GuardianOfTomorrow()
        {
            return CapturedEnemyCombatContract.CapturedParallelAttackSequence(
                "20260721-230426: Guardian was engaged by the player and emitted two independent "
                + "normal streams: slot 1 opened for 36 then repeated for 75 with weapon instance "
                + "1297107795, while slot 0 repeated for 75 with weapon instance 1397118030; "
                + "the two 173-point criticals remain report-only",
                new CapturedEnemyParallelAttackSequenceDefinition(
                    new[]
                    {
                        new CapturedEnemyParallelAttackStreamDefinition(
                            GuardianSlotOneFirstHitDelaySeconds,
                            Attack(36, 75, GuardianAttackRechargeSeconds, 1, -1, 1297107795)),
                        new CapturedEnemyParallelAttackStreamDefinition(
                            GuardianSlotZeroFirstHitDelaySeconds,
                            Attack(75, 75, GuardianAttackRechargeSeconds, 0, -1, 1397118030))
                    },
                    new[]
                    {
                        new CapturedEnemySpecialAttackDefinition(208298, 208299, 1297107795, "MPKS"),
                        new CapturedEnemySpecialAttackDefinition(208302, 208296, 1397118030, "SFTN")
                    },
                    511,
                    511,
                    511,
                    39,
                    0));
        }

        internal static CapturedEnemyCombatContract GartuaTheDoorkeeper()
        {
            return CapturedEnemyCombatContract.CapturedSpecialSequence(
                "20260721-230824: Gartua initiated combat and emitted eight normal local-player "
                + "hits from 76..114 at slot 6, ammo -1, weapon instance 0; the empty "
                + "SpecialAttackWeapon list carried 382/382/382/37/0",
                new CapturedEnemySpecialAttackSequenceDefinition(
                    GartuaFirstHitDelaySeconds,
                    null,
                    Attack(76, 114, GartuaAttackRechargeSeconds, 6, -1, 0),
                    new CapturedEnemySpecialAttackDefinition[0],
                    382,
                    382,
                    382,
                    37,
                    0));
        }

        internal static CapturedEnemyCombatContract ReanimatedCorpse()
        {
            return CapturedEnemyCombatContract.FixedAttack(
                "20260721-043204: Re-Animator room corpse adds produced exact 17-point local hits",
                17,
                17,
                5.717,
                6,
                0,
                0,
                -1);
        }

        internal static CapturedEnemyCombatContract EternalSentinel()
        {
            return CapturedEnemyCombatContract.FixedAttack(
                "20260721-041439/043204: Eternal Sentinel normal hits 17..18; critical 41 is report-only",
                17,
                18,
                EternalSentinelRechargeSeconds,
                6,
                0,
                0,
                -1);
        }

        internal static CapturedEnemyCombatContract For(string profileKey)
        {
            switch (profileKey)
            {
                case CapturedTempleOfThreeWindsLootDefinitions.DefenderProfileKey:
                    return DefenderOfTheThree();
                case CapturedTempleOfThreeWindsLootDefinitions.YatilaProfileKey:
                    return WindcallerYatila();
                case CapturedTempleOfThreeWindsLootDefinitions.GulardProfileKey:
                    return ReverendGulard();
                case CapturedTempleOfThreeWindsLootDefinitions.ReAnimatorProfileKey:
                    return ReAnimator();
                case CapturedTempleOfThreeWindsLootDefinitions.BetanyProfileKey:
                    return AcolyteBetany();
                case CapturedTempleOfThreeWindsLootDefinitions.CuratorProfileKey:
                    return TheCurator();
                case CapturedTempleOfThreeWindsLootDefinitions.NematetProfileKey:
                    return NematetTheCustodianOfTime();
                case CapturedTempleOfThreeWindsLootDefinitions.GuardianProfileKey:
                    return GuardianOfTomorrow();
                case CapturedTempleOfThreeWindsLootDefinitions.GartuaProfileKey:
                    return GartuaTheDoorkeeper();
                case "totw.647.encounter.re-animator.reanimated-corpse":
                    return ReanimatedCorpse();
                default:
                    return CapturedEnemyCombatContract.Unresolved(
                        "No capture-backed Temple combat contract for " + profileKey,
                        false);
            }
        }

        private static CapturedEnemyCombatContract SpecialSequence(
            string evidence,
            int openingDamage,
            int repeatingDamage,
            int specialUnknown,
            int specialUnknown4,
            int ammoCount = -1)
        {
            return CapturedEnemyCombatContract.CapturedSpecialSequence(
                evidence,
                new CapturedEnemySpecialAttackSequenceDefinition(
                    0.0,
                    Attack(
                        openingDamage,
                        openingDamage,
                        TempleNamedMeleeRechargePolicySeconds,
                        6,
                        ammoCount,
                        0),
                    Attack(
                        repeatingDamage,
                        repeatingDamage,
                        TempleNamedMeleeRechargePolicySeconds,
                        6,
                        ammoCount,
                        0),
                    new CapturedEnemySpecialAttackDefinition[0],
                    specialUnknown,
                    specialUnknown,
                    specialUnknown,
                    specialUnknown4,
                    0));
        }

        private static CapturedEnemyCombatAttackDefinition Attack(
            int minDamage,
            int maxDamage,
            double rechargeSeconds,
            int weaponSlot,
            int ammoCount,
            int weaponInstance)
        {
            return new CapturedEnemyCombatAttackDefinition(
                minDamage,
                maxDamage,
                0,
                NpcCombatAttackRules.MaxMeleeCombatDistance,
                rechargeSeconds,
                false,
                ammoCount,
                weaponSlot,
                0,
                0,
                weaponInstance,
                true);
        }
    }
}
