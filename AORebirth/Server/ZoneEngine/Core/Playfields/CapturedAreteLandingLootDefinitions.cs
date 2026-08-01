namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Capture-backed Arete Landing corpse loot from
    /// tools-temp/AOSharpLiveCapture/.../captures/arete part 1|2.
    /// One observed snapshot per InitialSnapshot corpse open (items + credits).
    /// Match by exact enemy name on playfield 6553.
    /// </summary>
    internal static class CapturedAreteLandingLootDefinitions
    {
        internal const int AreteLandingPlayfieldId = 6553;

        private const string Evidence =
            "AOSharpLiveCapture arete part 1 + arete part 2 corpse-loot-observations InitialSnapshot";

        private sealed class MobLootDefinition
        {
            public string ExactName;
            public string ProfileKey;
            public int MonsterData;
            public ObservedCorpseSnapshotDefinition[] Snapshots;
        }

        private static readonly MobLootDefinition[] Mobs =
        {
            new MobLootDefinition
            {
                ExactName = "32-V Docker",
                ProfileKey = "captured.arete.32-v-docker",
                MonsterData = 17649,
                Snapshots =
                    new[]
                    {
                        Snapshot("arete.32-v-docker.arete-part-1.corpsef5f802.0", 4),
                        Snapshot(
                            "arete.32-v-docker.arete-part-2.corpsef5f80e.1",
                            4,
                            Entry("arete.32-v-docker.arete-part-2.corpsef5f80e.1", 248318, 248318, 1, 1)
                            ),
                    }
            },
            new MobLootDefinition
            {
                ExactName = "Angry Minibull",
                ProfileKey = "captured.arete.angry-minibull",
                MonsterData = 30360,
                Snapshots =
                    new[]
                    {
                        Snapshot("arete.angry-minibull.arete-part-2.corpsef5f81b.0", 79),
                        Snapshot(
                            "arete.angry-minibull.arete-part-2.corpsef5f801.1",
                            59,
                            Entry("arete.angry-minibull.arete-part-2.corpsef5f801.1", 248310, 248310, 1, 1)
                            ),
                        Snapshot(
                            "arete.angry-minibull.arete-part-2.corpsef5f80b.2",
                            72,
                            Entry("arete.angry-minibull.arete-part-2.corpsef5f80b.2", 248325, 248325, 1, 1)
                            ),
                        Snapshot(
                            "arete.angry-minibull.arete-part-2.corpsef5f825.3",
                            53,
                            Entry("arete.angry-minibull.arete-part-2.corpsef5f825.3", 84156, 84155, 7, 1)
                            ),
                        Snapshot("arete.angry-minibull.arete-part-2.corpsef5f81a.4", 47),
                        Snapshot(
                            "arete.angry-minibull.arete-part-2.corpsef5f814.5",
                            47,
                            Entry("arete.angry-minibull.arete-part-2.corpsef5f814.5", 248330, 248330, 1, 1),
                            Entry("arete.angry-minibull.arete-part-2.corpsef5f814.5", 248330, 248330, 1, 1)
                            ),
                        Snapshot(
                            "arete.angry-minibull.arete-part-2.corpsef5f806.6",
                            47,
                            Entry("arete.angry-minibull.arete-part-2.corpsef5f806.6", 248310, 248310, 1, 1)
                            ),
                        Snapshot("arete.angry-minibull.arete-part-2.corpsef5f80d.7", 79),
                    }
            },
            new MobLootDefinition
            {
                ExactName = "Cleaning Robot",
                ProfileKey = "captured.arete.cleaning-robot",
                MonsterData = 297023,
                Snapshots =
                    new[]
                    {
                        Snapshot(
                            "arete.cleaning-robot.arete-part-1.corpsef5f818.0",
                            5,
                            Entry("arete.cleaning-robot.arete-part-1.corpsef5f818.0", 42620, 42620, 1, 1)
                            ),
                        Snapshot(
                            "arete.cleaning-robot.arete-part-1.corpsef5f802.1",
                            5,
                            Entry("arete.cleaning-robot.arete-part-1.corpsef5f802.1", 70563, 70563, 1, 1)
                            ),
                        Snapshot("arete.cleaning-robot.arete-part-1.corpsef5f803.2", 5),
                        Snapshot(
                            "arete.cleaning-robot.arete-part-1.corpsef5f802.3",
                            5,
                            Entry("arete.cleaning-robot.arete-part-1.corpsef5f802.3", 42620, 42620, 1, 1)
                            ),
                        Snapshot(
                            "arete.cleaning-robot.arete-part-1.corpsef5f802.4",
                            5,
                            Entry("arete.cleaning-robot.arete-part-1.corpsef5f802.4", 42620, 42620, 1, 1)
                            ),
                        Snapshot(
                            "arete.cleaning-robot.arete-part-1.corpsef5f802.5",
                            5,
                            Entry("arete.cleaning-robot.arete-part-1.corpsef5f802.5", 42620, 42620, 1, 1)
                            ),
                        Snapshot("arete.cleaning-robot.arete-part-1.corpsef5f802.6", 5),
                        Snapshot(
                            "arete.cleaning-robot.arete-part-1.corpsef5f802.7",
                            5,
                            Entry("arete.cleaning-robot.arete-part-1.corpsef5f802.7", 155685, 155685, 1, 1),
                            Entry("arete.cleaning-robot.arete-part-1.corpsef5f802.7", 84144, 84144, 1, 1),
                            Entry("arete.cleaning-robot.arete-part-1.corpsef5f802.7", 70559, 70559, 1, 1)
                            ),
                        Snapshot("arete.cleaning-robot.arete-part-1.corpsef5f809.8", 5),
                        Snapshot("arete.cleaning-robot.arete-part-1.corpsef5f807.9", 5),
                        Snapshot(
                            "arete.cleaning-robot.arete-part-1.corpsef5f80d.10",
                            5,
                            Entry("arete.cleaning-robot.arete-part-1.corpsef5f80d.10", 70560, 70560, 1, 1),
                            Entry("arete.cleaning-robot.arete-part-1.corpsef5f80d.10", 42620, 42620, 1, 1)
                            ),
                        Snapshot(
                            "arete.cleaning-robot.arete-part-1.corpsef5f805.11",
                            5,
                            Entry("arete.cleaning-robot.arete-part-1.corpsef5f805.11", 42620, 42620, 1, 1)
                            ),
                        Snapshot(
                            "arete.cleaning-robot.arete-part-1.corpsef5f80e.12",
                            5,
                            Entry("arete.cleaning-robot.arete-part-1.corpsef5f80e.12", 42620, 42620, 1, 1)
                            ),
                        Snapshot("arete.cleaning-robot.arete-part-1.corpsef5f81a.13", 5),
                        Snapshot("arete.cleaning-robot.arete-part-1.corpsef5f802.14", 5),
                        Snapshot(
                            "arete.cleaning-robot.arete-part-2.corpsef5f810.15",
                            5,
                            Entry("arete.cleaning-robot.arete-part-2.corpsef5f810.15", 42620, 42620, 1, 1)
                            ),
                    }
            },
            new MobLootDefinition
            {
                ExactName = "Cleanmeister Intelligence Robot",
                ProfileKey = "captured.arete.cleanmeister-intelligence-robot",
                MonsterData = 297023,
                Snapshots =
                    new[]
                    {
                        Snapshot(
                            "arete.cleanmeister-intelligence-robot.arete-part-1.corpsef5f801.0",
                            17,
                            Entry("arete.cleanmeister-intelligence-robot.arete-part-1.corpsef5f801.0", 42620, 42619, 2, 1),
                            Entry("arete.cleanmeister-intelligence-robot.arete-part-1.corpsef5f801.0", 85517, 27360, 2, 1),
                            Entry("arete.cleanmeister-intelligence-robot.arete-part-1.corpsef5f801.0", 85740, 85739, 2, 1),
                            Entry("arete.cleanmeister-intelligence-robot.arete-part-1.corpsef5f801.0", 123514, 123515, 2, 1),
                            Entry("arete.cleanmeister-intelligence-robot.arete-part-1.corpsef5f801.0", 154069, 150213, 2, 1),
                            Entry("arete.cleanmeister-intelligence-robot.arete-part-1.corpsef5f801.0", 161609, 161610, 2, 1),
                            Entry("arete.cleanmeister-intelligence-robot.arete-part-1.corpsef5f801.0", 162736, 162736, 7, 1),
                            Entry("arete.cleanmeister-intelligence-robot.arete-part-1.corpsef5f801.0", 206656, 206657, 2, 1)
                            ),
                        Snapshot(
                            "arete.cleanmeister-intelligence-robot.arete-part-1.corpsef5f808.1",
                            17,
                            Entry("arete.cleanmeister-intelligence-robot.arete-part-1.corpsef5f808.1", 85693, 27389, 2, 1),
                            Entry("arete.cleanmeister-intelligence-robot.arete-part-1.corpsef5f808.1", 135719, 135719, 1, 1),
                            Entry("arete.cleanmeister-intelligence-robot.arete-part-1.corpsef5f808.1", 123789, 123790, 2, 1),
                            Entry("arete.cleanmeister-intelligence-robot.arete-part-1.corpsef5f808.1", 129064, 129065, 2, 1),
                            Entry("arete.cleanmeister-intelligence-robot.arete-part-1.corpsef5f808.1", 160338, 160339, 2, 1),
                            Entry("arete.cleanmeister-intelligence-robot.arete-part-1.corpsef5f808.1", 162736, 162736, 7, 1),
                            Entry("arete.cleanmeister-intelligence-robot.arete-part-1.corpsef5f808.1", 201045, 201046, 2, 1)
                            ),
                        Snapshot(
                            "arete.cleanmeister-intelligence-robot.arete-part-2.corpsef5f813.2",
                            17,
                            Entry("arete.cleanmeister-intelligence-robot.arete-part-2.corpsef5f813.2", 85691, 22004, 2, 1),
                            Entry("arete.cleanmeister-intelligence-robot.arete-part-2.corpsef5f813.2", 70562, 85597, 2, 1),
                            Entry("arete.cleanmeister-intelligence-robot.arete-part-2.corpsef5f813.2", 122981, 122982, 2, 1),
                            Entry("arete.cleanmeister-intelligence-robot.arete-part-2.corpsef5f813.2", 130042, 130043, 2, 1),
                            Entry("arete.cleanmeister-intelligence-robot.arete-part-2.corpsef5f813.2", 160604, 160604, 20, 1),
                            Entry("arete.cleanmeister-intelligence-robot.arete-part-2.corpsef5f813.2", 162736, 162736, 7, 1),
                            Entry("arete.cleanmeister-intelligence-robot.arete-part-2.corpsef5f813.2", 201072, 201073, 2, 1)
                            ),
                    }
            },
            new MobLootDefinition
            {
                ExactName = "Desert Reet",
                ProfileKey = "captured.arete.desert-reet",
                MonsterData = 30365,
                Snapshots =
                    new[]
                    {
                        Snapshot(
                            "arete.desert-reet.arete-part-2.corpsef5f809.0",
                            29,
                            Entry("arete.desert-reet.arete-part-2.corpsef5f809.0", 42640, 42641, 5, 1)
                            ),
                        Snapshot("arete.desert-reet.arete-part-2.corpsef5f813.1", 29),
                        Snapshot(
                            "arete.desert-reet.arete-part-2.corpsef5f815.2",
                            35,
                            Entry("arete.desert-reet.arete-part-2.corpsef5f815.2", 70561, 85744, 6, 1),
                            Entry("arete.desert-reet.arete-part-2.corpsef5f815.2", 42640, 42641, 7, 1)
                            ),
                        Snapshot(
                            "arete.desert-reet.arete-part-2.corpsef5f821.3",
                            35,
                            Entry("arete.desert-reet.arete-part-2.corpsef5f821.3", 42640, 42641, 5, 1)
                            ),
                        Snapshot(
                            "arete.desert-reet.arete-part-2.corpsef5f823.4",
                            29,
                            Entry("arete.desert-reet.arete-part-2.corpsef5f823.4", 42640, 42641, 6, 1)
                            ),
                        Snapshot("arete.desert-reet.arete-part-2.corpsef5f827.5", 29),
                        Snapshot(
                            "arete.desert-reet.arete-part-2.corpsef5f81f.6",
                            35,
                            Entry("arete.desert-reet.arete-part-2.corpsef5f81f.6", 42640, 42641, 6, 1)
                            ),
                        Snapshot(
                            "arete.desert-reet.arete-part-2.corpsef5f806.7",
                            35,
                            Entry("arete.desert-reet.arete-part-2.corpsef5f806.7", 248328, 248328, 1, 1),
                            Entry("arete.desert-reet.arete-part-2.corpsef5f806.7", 70558, 85640, 5, 1),
                            Entry("arete.desert-reet.arete-part-2.corpsef5f806.7", 42640, 42641, 5, 1)
                            ),
                        Snapshot(
                            "arete.desert-reet.arete-part-2.corpsef5f815.8",
                            29,
                            Entry("arete.desert-reet.arete-part-2.corpsef5f815.8", 248328, 248328, 1, 1)
                            ),
                    }
            },
            new MobLootDefinition
            {
                ExactName = "Garbage Flea",
                ProfileKey = "captured.arete.garbage-flea",
                MonsterData = 17657,
                Snapshots =
                    new[]
                    {
                        Snapshot(
                            "arete.garbage-flea.arete-part-1.corpsef5f809.0",
                            11,
                            Entry("arete.garbage-flea.arete-part-1.corpsef5f809.0", 248322, 248322, 1, 1)
                            ),
                        Snapshot("arete.garbage-flea.arete-part-1.corpsef5f807.1", 11),
                        Snapshot("arete.garbage-flea.arete-part-1.corpsef5f81e.2", 5),
                        Snapshot("arete.garbage-flea.arete-part-1.corpsef5f802.3", 5),
                        Snapshot("arete.garbage-flea.arete-part-1.corpsef5f802.4", 11),
                        Snapshot(
                            "arete.garbage-flea.arete-part-1.corpsef5f815.5",
                            5,
                            Entry("arete.garbage-flea.arete-part-1.corpsef5f815.5", 248322, 248322, 1, 1)
                            ),
                        Snapshot(
                            "arete.garbage-flea.arete-part-1.corpsef5f823.6",
                            11,
                            Entry("arete.garbage-flea.arete-part-1.corpsef5f823.6", 70560, 85688, 2, 1),
                            Entry("arete.garbage-flea.arete-part-1.corpsef5f823.6", 248322, 248322, 1, 1)
                            ),
                        Snapshot(
                            "arete.garbage-flea.arete-part-1.corpsef5f806.7",
                            11,
                            Entry("arete.garbage-flea.arete-part-1.corpsef5f806.7", 248322, 248322, 1, 1)
                            ),
                        Snapshot("arete.garbage-flea.arete-part-1.corpsef5f817.8", 5),
                        Snapshot(
                            "arete.garbage-flea.arete-part-1.corpsef5f818.9",
                            5,
                            Entry("arete.garbage-flea.arete-part-1.corpsef5f818.9", 248322, 248322, 1, 1)
                            ),
                        Snapshot("arete.garbage-flea.arete-part-1.corpsef5f826.10", 5),
                    }
            },
            new MobLootDefinition
            {
                ExactName = "Gnarl the Roller",
                ProfileKey = "captured.arete.gnarl-the-roller",
                MonsterData = 17687,
                Snapshots =
                    new[]
                    {
                        Snapshot(
                            "arete.gnarl-the-roller.arete-part-2.corpsef5f829.0",
                            62,
                            Entry("arete.gnarl-the-roller.arete-part-2.corpsef5f829.0", 85750, 85749, 6, 1),
                            Entry("arete.gnarl-the-roller.arete-part-2.corpsef5f829.0", 85512, 85511, 6, 1),
                            Entry("arete.gnarl-the-roller.arete-part-2.corpsef5f829.0", 124106, 124107, 6, 1),
                            Entry("arete.gnarl-the-roller.arete-part-2.corpsef5f829.0", 122064, 122065, 6, 1),
                            Entry("arete.gnarl-the-roller.arete-part-2.corpsef5f829.0", 160103, 160104, 6, 1),
                            Entry("arete.gnarl-the-roller.arete-part-2.corpsef5f829.0", 162736, 162736, 7, 1),
                            Entry("arete.gnarl-the-roller.arete-part-2.corpsef5f829.0", 201087, 201088, 6, 1)
                            ),
                    }
            },
            new MobLootDefinition
            {
                ExactName = "Kneebreaker Alfonzo Rizzolo",
                ProfileKey = "captured.arete.kneebreaker-alfonzo-rizzolo",
                MonsterData = 165196,
                Snapshots =
                    new[]
                    {
                        Snapshot(
                            "arete.kneebreaker-alfonzo-rizzolo.arete-part-2.corpsef5f817.0",
                            23,
                            Entry("arete.kneebreaker-alfonzo-rizzolo.arete-part-2.corpsef5f817.0", 70561, 85744, 4, 1)
                            ),
                    }
            },
            new MobLootDefinition
            {
                ExactName = "Malfunctioning Cleaning Robot",
                ProfileKey = "captured.arete.malfunctioning-cleaning-robot",
                MonsterData = 297023,
                Snapshots =
                    new[]
                    {
                        Snapshot("arete.malfunctioning-cleaning-robot.arete-part-1.corpsef5f815.0", 5),
                        Snapshot("arete.malfunctioning-cleaning-robot.arete-part-1.corpsef5f81a.1", 5),
                    }
            },
            new MobLootDefinition
            {
                ExactName = "Rollerrat",
                ProfileKey = "captured.arete.rollerrat",
                MonsterData = 17687,
                Snapshots =
                    new[]
                    {
                        Snapshot(
                            "arete.rollerrat.arete-part-2.corpsef5f818.0",
                            35,
                            Entry("arete.rollerrat.arete-part-2.corpsef5f818.0", 248333, 248333, 1, 1)
                            ),
                        Snapshot(
                            "arete.rollerrat.arete-part-2.corpsef5f80f.1",
                            35,
                            Entry("arete.rollerrat.arete-part-2.corpsef5f80f.1", 70560, 85688, 7, 1)
                            ),
                        Snapshot(
                            "arete.rollerrat.arete-part-2.corpsef5f809.2",
                            35,
                            Entry("arete.rollerrat.arete-part-2.corpsef5f809.2", 70561, 85744, 7, 1)
                            ),
                        Snapshot(
                            "arete.rollerrat.arete-part-2.corpsef5f80b.3",
                            29,
                            Entry("arete.rollerrat.arete-part-2.corpsef5f80b.3", 84150, 84149, 4, 1)
                            ),
                        Snapshot("arete.rollerrat.arete-part-2.corpsef5f809.4", 29),
                        Snapshot("arete.rollerrat.arete-part-2.corpsef5f80a.5", 29),
                        Snapshot("arete.rollerrat.arete-part-2.corpsef5f827.6", 29),
                        Snapshot("arete.rollerrat.arete-part-2.corpsef5f80e.7", 35),
                        Snapshot("arete.rollerrat.arete-part-2.corpsef5f80e.8", 35),
                        Snapshot(
                            "arete.rollerrat.arete-part-2.corpsef5f811.9",
                            35,
                            Entry("arete.rollerrat.arete-part-2.corpsef5f811.9", 248333, 248333, 1, 1)
                            ),
                        Snapshot(
                            "arete.rollerrat.arete-part-2.corpsef5f81d.10",
                            35,
                            Entry("arete.rollerrat.arete-part-2.corpsef5f81d.10", 70559, 85689, 7, 1)
                            ),
                        Snapshot(
                            "arete.rollerrat.arete-part-2.corpsef5f82a.11",
                            35,
                            Entry("arete.rollerrat.arete-part-2.corpsef5f82a.11", 84158, 84157, 6, 1),
                            Entry("arete.rollerrat.arete-part-2.corpsef5f82a.11", 248333, 248333, 1, 1)
                            ),
                        Snapshot("arete.rollerrat.arete-part-2.corpsef5f82f.12", 29),
                        Snapshot(
                            "arete.rollerrat.arete-part-2.corpsef5f80b.13",
                            35,
                            Entry("arete.rollerrat.arete-part-2.corpsef5f80b.13", 70559, 85689, 5, 1),
                            Entry("arete.rollerrat.arete-part-2.corpsef5f80b.13", 248333, 248333, 1, 1)
                            ),
                        Snapshot("arete.rollerrat.arete-part-2.corpsef5f816.14", 35),
                        Snapshot("arete.rollerrat.arete-part-2.corpsef5f81a.15", 35),
                        Snapshot("arete.rollerrat.arete-part-2.corpsef5f827.16", 35),
                        Snapshot(
                            "arete.rollerrat.arete-part-2.corpsef5f81f.17",
                            35,
                            Entry("arete.rollerrat.arete-part-2.corpsef5f81f.17", 70558, 85640, 6, 1),
                            Entry("arete.rollerrat.arete-part-2.corpsef5f81f.17", 248333, 248333, 1, 1)
                            ),
                        Snapshot(
                            "arete.rollerrat.arete-part-2.corpsef5f803.18",
                            29,
                            Entry("arete.rollerrat.arete-part-2.corpsef5f803.18", 248333, 248333, 1, 1)
                            ),
                        Snapshot(
                            "arete.rollerrat.arete-part-2.corpsef5f819.19",
                            29,
                            Entry("arete.rollerrat.arete-part-2.corpsef5f819.19", 248333, 248333, 1, 1)
                            ),
                        Snapshot("arete.rollerrat.arete-part-2.corpsef5f805.20", 29),
                        Snapshot("arete.rollerrat.arete-part-2.corpsef5f82f.21", 29),
                        Snapshot("arete.rollerrat.arete-part-2.corpsef5f80b.22", 35),
                        Snapshot("arete.rollerrat.arete-part-2.corpsef5f80b.23", 35),
                        Snapshot("arete.rollerrat.arete-part-2.corpsef5f80d.24", 35),
                        Snapshot(
                            "arete.rollerrat.arete-part-2.corpsef5f80e.25",
                            35,
                            Entry("arete.rollerrat.arete-part-2.corpsef5f80e.25", 70559, 85689, 7, 1)
                            ),
                    }
            },
            new MobLootDefinition
            {
                ExactName = "Supreme Collector of Waste",
                ProfileKey = "captured.arete.supreme-collector-of-waste",
                MonsterData = 17714,
                Snapshots =
                    new[]
                    {
                        Snapshot(
                            "arete.supreme-collector-of-waste.arete-part-1.corpsef5f80c.0",
                            35,
                            Entry("arete.supreme-collector-of-waste.arete-part-1.corpsef5f80c.0", 42620, 42619, 4, 1),
                            Entry("arete.supreme-collector-of-waste.arete-part-1.corpsef5f80c.0", 123038, 123039, 5, 1),
                            Entry("arete.supreme-collector-of-waste.arete-part-1.corpsef5f80c.0", 160216, 160217, 5, 1),
                            Entry("arete.supreme-collector-of-waste.arete-part-1.corpsef5f80c.0", 160603, 160603, 20, 1),
                            Entry("arete.supreme-collector-of-waste.arete-part-1.corpsef5f80c.0", 160704, 160704, 24, 1)
                            ),
                        Snapshot(
                            "arete.supreme-collector-of-waste.arete-part-1.corpsef5f81c.1",
                            35,
                            Entry("arete.supreme-collector-of-waste.arete-part-1.corpsef5f81c.1", 85761, 85760, 3, 1),
                            Entry("arete.supreme-collector-of-waste.arete-part-1.corpsef5f81c.1", 85533, 85532, 3, 1),
                            Entry("arete.supreme-collector-of-waste.arete-part-1.corpsef5f81c.1", 124383, 124384, 3, 1),
                            Entry("arete.supreme-collector-of-waste.arete-part-1.corpsef5f81c.1", 121629, 121630, 3, 1),
                            Entry("arete.supreme-collector-of-waste.arete-part-1.corpsef5f81c.1", 160603, 160603, 20, 1),
                            Entry("arete.supreme-collector-of-waste.arete-part-1.corpsef5f81c.1", 162736, 162736, 7, 1),
                            Entry("arete.supreme-collector-of-waste.arete-part-1.corpsef5f81c.1", 201068, 201069, 3, 1)
                            ),
                        Snapshot(
                            "arete.supreme-collector-of-waste.arete-part-2.corpsef5f801.2",
                            35,
                            Entry("arete.supreme-collector-of-waste.arete-part-2.corpsef5f801.2", 70565, 85514, 3, 1),
                            Entry("arete.supreme-collector-of-waste.arete-part-2.corpsef5f801.2", 135719, 135719, 1, 1),
                            Entry("arete.supreme-collector-of-waste.arete-part-2.corpsef5f801.2", 124391, 124392, 3, 1),
                            Entry("arete.supreme-collector-of-waste.arete-part-2.corpsef5f801.2", 153083, 153084, 3, 1),
                            Entry("arete.supreme-collector-of-waste.arete-part-2.corpsef5f801.2", 160736, 160737, 3, 1),
                            Entry("arete.supreme-collector-of-waste.arete-part-2.corpsef5f801.2", 162736, 162736, 7, 1),
                            Entry("arete.supreme-collector-of-waste.arete-part-2.corpsef5f801.2", 201076, 201077, 3, 1)
                            ),
                    }
            },
            new MobLootDefinition
            {
                ExactName = "Waste Collector",
                ProfileKey = "captured.arete.waste-collector",
                MonsterData = 17714,
                Snapshots =
                    new[]
                    {
                        Snapshot(
                            "arete.waste-collector.arete-part-1.corpsef5f809.0",
                            11,
                            Entry("arete.waste-collector.arete-part-1.corpsef5f809.0", 248319, 248319, 1, 1),
                            Entry("arete.waste-collector.arete-part-1.corpsef5f809.0", 42620, 42619, 2, 1)
                            ),
                        Snapshot(
                            "arete.waste-collector.arete-part-1.corpsef5f805.1",
                            11,
                            Entry("arete.waste-collector.arete-part-1.corpsef5f805.1", 248315, 248315, 1, 1),
                            Entry("arete.waste-collector.arete-part-1.corpsef5f805.1", 42620, 42619, 2, 1)
                            ),
                        Snapshot(
                            "arete.waste-collector.arete-part-1.corpsef5f802.2",
                            11,
                            Entry("arete.waste-collector.arete-part-1.corpsef5f802.2", 248315, 248315, 1, 1),
                            Entry("arete.waste-collector.arete-part-1.corpsef5f802.2", 70562, 85597, 2, 1)
                            ),
                        Snapshot(
                            "arete.waste-collector.arete-part-1.corpsef5f813.3",
                            11,
                            Entry("arete.waste-collector.arete-part-1.corpsef5f813.3", 297289, 297289, 1, 1),
                            Entry("arete.waste-collector.arete-part-1.corpsef5f813.3", 42620, 42619, 2, 1)
                            ),
                        Snapshot(
                            "arete.waste-collector.arete-part-1.corpsef5f81b.4",
                            11,
                            Entry("arete.waste-collector.arete-part-1.corpsef5f81b.4", 42620, 42619, 2, 1)
                            ),
                        Snapshot("arete.waste-collector.arete-part-1.corpsef5f80e.5", 11),
                        Snapshot("arete.waste-collector.arete-part-1.corpsef5f813.6", 11),
                        Snapshot(
                            "arete.waste-collector.arete-part-1.corpsef5f806.7",
                            11,
                            Entry("arete.waste-collector.arete-part-1.corpsef5f806.7", 42620, 42619, 2, 1)
                            ),
                        Snapshot(
                            "arete.waste-collector.arete-part-1.corpsef5f819.8",
                            11,
                            Entry("arete.waste-collector.arete-part-1.corpsef5f819.8", 248319, 248319, 1, 1)
                            ),
                        Snapshot("arete.waste-collector.arete-part-1.corpsef5f821.9", 11),
                        Snapshot(
                            "arete.waste-collector.arete-part-1.corpsef5f826.10",
                            11,
                            Entry("arete.waste-collector.arete-part-1.corpsef5f826.10", 248315, 248315, 1, 1),
                            Entry("arete.waste-collector.arete-part-1.corpsef5f826.10", 248319, 248319, 1, 1),
                            Entry("arete.waste-collector.arete-part-1.corpsef5f826.10", 42620, 42619, 2, 1)
                            ),
                        Snapshot(
                            "arete.waste-collector.arete-part-1.corpsef5f813.11",
                            11,
                            Entry("arete.waste-collector.arete-part-1.corpsef5f813.11", 70561, 85744, 2, 1)
                            ),
                        Snapshot("arete.waste-collector.arete-part-1.corpsef5f814.12", 11),
                        Snapshot(
                            "arete.waste-collector.arete-part-1.corpsef5f814.13",
                            11,
                            Entry("arete.waste-collector.arete-part-1.corpsef5f814.13", 248334, 248334, 1, 1),
                            Entry("arete.waste-collector.arete-part-1.corpsef5f814.13", 70558, 85640, 2, 1),
                            Entry("arete.waste-collector.arete-part-1.corpsef5f814.13", 42620, 42619, 2, 1)
                            ),
                        Snapshot(
                            "arete.waste-collector.arete-part-2.corpsef5f810.14",
                            11,
                            Entry("arete.waste-collector.arete-part-2.corpsef5f810.14", 248315, 248315, 1, 1),
                            Entry("arete.waste-collector.arete-part-2.corpsef5f810.14", 70558, 85640, 2, 1)
                            ),
                        Snapshot(
                            "arete.waste-collector.arete-part-2.corpsef5f811.15",
                            11,
                            Entry("arete.waste-collector.arete-part-2.corpsef5f811.15", 248334, 248334, 1, 1),
                            Entry("arete.waste-collector.arete-part-2.corpsef5f811.15", 248319, 248319, 1, 1),
                            Entry("arete.waste-collector.arete-part-2.corpsef5f811.15", 42620, 42619, 2, 1)
                            ),
                    }
            },
        };

        private static readonly Dictionary<string, MobLootDefinition> ByExactName =
            BuildByExactName();

        private static Dictionary<string, MobLootDefinition> BuildByExactName()
        {
            Dictionary<string, MobLootDefinition> map =
                new Dictionary<string, MobLootDefinition>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Mobs.Length; i++)
            {
                map[Mobs[i].ExactName] = Mobs[i];
            }

            return map;
        }

        internal static bool TryRegister(
            LootTableRegistry registry,
            string enemyName,
            out string profileKey)
        {
            profileKey = null;
            if (registry == null || string.IsNullOrWhiteSpace(enemyName))
            {
                return false;
            }

            MobLootDefinition mob;
            if (!ByExactName.TryGetValue(enemyName.Trim(), out mob) || mob == null)
            {
                return false;
            }

            profileKey = mob.ProfileKey;
            string tableKey = "captured." + mob.ProfileKey;
            if (registry.ContainsTable(tableKey))
            {
                return true;
            }

            registry.RegisterTable(
                new LootTableDefinition
                {
                    LootTableKey = tableKey,
                    DisplayName = mob.ExactName + " Arete captured corpse",
                    TableType = LootTableType.EnemyType,
                    RollGroups = new LootGroupDefinition[0],
                    ObservedCorpseSnapshots = mob.Snapshots,
                    CreditsPolicy = new CreditsPolicyDefinition
                    {
                        Mode = CreditsPolicyMode.Unresolved,
                        Evidence = LootEvidenceConfidence.Unresolved
                    },
                    QualityPolicy = "captured-observed-corpse-snapshots",
                    Evidence = Evidence,
                    Confidence = LootEvidenceConfidence.ObservedAvailableLoot,
                    ItemPoolUnresolved = true,
                    Enabled = true
                });
            registry.RegisterAssignment(
                new LootAssignmentDefinition
                {
                    AssignmentKey = tableKey,
                    TargetType = LootAssignmentTargetType.EnemyType,
                    TargetKey = mob.ProfileKey,
                    LootTableKey = tableKey,
                    Priority = 0,
                    Conditions = new string[0],
                    Evidence = Evidence,
                    Confidence = LootEvidenceConfidence.ObservedAvailableLoot,
                    Enabled = true
                });
            return true;
        }

        internal static bool TryGetTypicalCredits(string enemyName, out int credits)
        {
            credits = 0;
            if (string.IsNullOrWhiteSpace(enemyName))
            {
                return false;
            }

            MobLootDefinition mob;
            if (!ByExactName.TryGetValue(enemyName.Trim(), out mob) || mob == null
                || mob.Snapshots == null || mob.Snapshots.Length == 0)
            {
                return false;
            }

            // Prefer a non-zero observed credit sample for empty-corpse guard.
            for (int i = 0; i < mob.Snapshots.Length; i++)
            {
                if (mob.Snapshots[i].Credits > 0)
                {
                    credits = mob.Snapshots[i].Credits;
                    return true;
                }
            }

            credits = mob.Snapshots[0].Credits;
            return true;
        }

        private static ObservedCorpseSnapshotDefinition Snapshot(
            string key,
            int credits,
            params LootEntryDefinition[] entries)
        {
            return new ObservedCorpseSnapshotDefinition
            {
                SnapshotKey = key,
                Credits = credits,
                Entries = entries ?? new LootEntryDefinition[0],
                Evidence = LootEvidenceConfidence.ProvenCapture,
                SelectionProbabilityEvidence = LootEvidenceConfidence.Unresolved,
                EvidenceReference = Evidence + "; " + key
            };
        }

        private static LootEntryDefinition Entry(
            string snapshotKey,
            int lowItemId,
            int highItemId,
            int quality,
            int quantity)
        {
            return new LootEntryDefinition
            {
                SelectionKey = snapshotKey,
                ItemTemplateId = lowItemId,
                HighItemTemplateId = highItemId,
                FixedQuality = quality,
                MinimumQuality = quality,
                MaximumQuality = quality,
                MinimumQuantity = quantity,
                MaximumQuantity = quantity,
                Weight = 0,
                DropChanceBasisPoints = 0,
                UniquePerCorpse = true,
                Semantics = LootSemantics.ObservedAvailable,
                Evidence = LootEvidenceConfidence.ObservedAvailableLoot,
                EvidenceReference = Evidence + "; " + snapshotKey,
                ProbabilityEvidence = "unresolved"
            };
        }
    }
}
