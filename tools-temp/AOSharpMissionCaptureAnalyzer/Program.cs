namespace AOSharpMissionCaptureAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;

    using AOSharp.Common.GameData;
    using AOSharpLiveCapture;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Serialization;

    internal static class Program
    {
        private static readonly HashSet<string> MissionMessageTypes =
            new HashSet<string>(
                new[]
                {
                    "GenericCmd",
                    "Stat",
                    "SetStat",
                    "QuestAlternative",
                    "CreateQuest",
                    "SimpleItemFullUpdate",
                    "ContainerAddItem",
                    "QuestFullUpdate",
                    "Quest",
                    "N3Teleport",
                    "Teleport"
                },
                StringComparer.OrdinalIgnoreCase);

        private static int Main(string[] args)
        {
            if (args.Length == 1 && string.Equals(args[0], "--self-test", StringComparison.OrdinalIgnoreCase))
            {
                return RunSelfTest();
            }

            if (args.Length != 1)
            {
                Console.Error.WriteLine(
                    "Usage: AOSharpMissionCaptureAnalyzer.exe <capture-folder> | --self-test");
                return 2;
            }

            return ReplayCapture(Path.GetFullPath(args[0]));
        }

        private static int ReplayCapture(string captureFolder)
        {
            string rawCsvPath = Path.Combine(captureFolder, "raw-packets.csv");
            if (!Directory.Exists(captureFolder) || !File.Exists(rawCsvPath))
            {
                Console.Error.WriteLine("Mission replay FAIL: raw-packets.csv was not found in " + captureFolder);
                return 2;
            }

            List<RawPacketRow> rows;
            try
            {
                rows = LoadRows(rawCsvPath);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("Mission replay FAIL: " + exception.Message);
                return 1;
            }

            string stagingDirectory = Path.Combine(
                Path.GetTempPath(),
                "AOSharpMissionCaptureAnalyzer-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stagingDirectory);
            string stagedLogPath = Path.Combine(stagingDirectory, "mission-flow.log");
            string outputPath = Path.Combine(captureFolder, "mission-flow.replay.log");
            string errorPath = Path.Combine(captureFolder, "mission-flow.replay.errors.log");
            var errors = new List<string>();
            var counts = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int decoded = 0;
            int skipped = 0;

            try
            {
                var serializer = new MessageSerializer();
                var capture = new MissionFlowCapture(null);
                capture.BindSession(stagingDirectory);
                foreach (RawPacketRow row in rows.OrderBy(row => row.GlobalOrdinal))
                {
                    if (!MissionMessageTypes.Contains(row.N3TypeName))
                    {
                        skipped++;
                        continue;
                    }

                    try
                    {
                        Message decodedMessage = serializer.Deserialize(HexToBytes(row.RawHex));
                        var n3Message = decodedMessage == null ? null : decodedMessage.Body as N3Message;
                        if (n3Message == null)
                        {
                            errors.Add(
                                FormatError(row, "serializer returned no N3 message"));
                            continue;
                        }

                        capture.OnCapturedN3Message(
                            row.Direction,
                            row.CapturedUtc,
                            row.GlobalOrdinal,
                            row.Sequence,
                            n3Message);
                        decoded++;
                        Increment(counts, row.Direction + "-" + row.N3TypeName);
                    }
                    catch (Exception exception)
                    {
                        errors.Add(FormatError(row, exception.GetType().Name + ": " + exception.Message));
                    }
                }

                capture.Teardown();

                if (!File.Exists(stagedLogPath))
                {
                    throw new InvalidOperationException("mission-flow.log was not produced");
                }

                string missionLog = File.ReadAllText(stagedLogPath);
                CountMissionLogCategories(missionLog, counts);
                if (missionLog.IndexOf("[MISSION-FLOW-ERROR]", StringComparison.Ordinal) >= 0)
                {
                    errors.Add("mission-flow extractor emitted MISSION-FLOW-ERROR");
                }

                File.Copy(stagedLogPath, outputPath, true);
                WriteErrors(errorPath, errors);
            }
            catch (Exception exception)
            {
                errors.Add("analyzer: " + exception.GetType().Name + ": " + exception.Message);
                WriteErrors(errorPath, errors);
            }
            finally
            {
                try
                {
                    Directory.Delete(stagingDirectory, true);
                }
                catch
                {
                }
            }

            Console.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}: mission replay decoded={1} skipped={2} errors={3} output={4}",
                    Path.GetFileName(captureFolder),
                    decoded,
                    skipped,
                    errors.Count,
                    outputPath));
            foreach (KeyValuePair<string, int> count in counts)
            {
                Console.WriteLine(
                    count.Key + "=" + count.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (errors.Count > 0)
            {
                Console.Error.WriteLine("Mission replay FAIL: " + errorPath);
                return 1;
            }

            return 0;
        }

        private static int RunSelfTest()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AOSharpMissionCaptureAnalyzer-self-test-" + Guid.NewGuid().ToString("N"));
            string firstSession = Path.Combine(root, "first");
            string secondSession = Path.Combine(root, "second");
            Directory.CreateDirectory(firstSession);
            Directory.CreateDirectory(secondSession);

            try
            {
                var player = new Identity(IdentityType.SimpleChar, unchecked((int)0x3CAC6F14));
                var terminal = new Identity(IdentityType.MissionTerminal, unchecked((int)0xC000028F));
                var selectedOffer = new Identity(IdentityType.Mission, unchecked((int)0x556DA5CB));
                var acceptedQuest = new Identity(IdentityType.Mission, unchecked((int)0x556DA5D5));
                var missionKey = new Identity(IdentityType.MissionKey, 0x00F687D7);
                var otherPlayer = new Identity(IdentityType.SimpleChar, 1234);
                var capture = new MissionFlowCapture(null);
                capture.BindSession(firstSession);

                capture.OnN3MessageSent(
                    1,
                    new GenericCmdMessage
                    {
                        Identity = player,
                        User = player,
                        Action = GenericCmdAction.Use,
                        Target = terminal
                    });
                capture.OnN3MessageSent(
                    2,
                    new QuestAlternativeMessage
                    {
                        Identity = player,
                        Unknown1 = 1,
                        Unknown2 = 22,
                        Scope = MissionScope.Solo,
                        Terminal = terminal,
                        MissionSliders = new MissionSliders
                        {
                            Difficulty = 1,
                            GoodBad = 2,
                            OrderChaos = 3,
                            OpenHidden = 4,
                            PhysicalMystical = 5,
                            HeadonStealth = 6,
                            CreditsXp = 7
                        },
                        MissionDetails = new MissionInfo[0]
                    });
                capture.OnN3MessageReceived(
                    3,
                    new StatMessage
                    {
                        Identity = player,
                        Stats = new[]
                        {
                            new GameTuple<Stat, uint> { Value1 = Stat.Cash, Value2 = 9980 }
                        }
                    });
                capture.OnN3MessageReceived(
                    4,
                    new QuestAlternativeMessage
                    {
                        Identity = player,
                        Unknown1 = 1,
                        Unknown2 = 44,
                        Scope = MissionScope.Solo,
                        Terminal = terminal,
                        MissionSliders = new MissionSliders(),
                        MissionDetails = new[]
                        {
                            new MissionInfo
                            {
                                MissionIdentity = selectedOffer,
                                UnkChunk1 = new byte[] { 1, 2 },
                                Title = "Find the target",
                                Description = "Complete mission details",
                                TerminalIdentity = terminal,
                                RewardDescriptorVersion = 3,
                                Credits = 5000,
                                XpReward = 7000,
                                MissionItemData = new[]
                                {
                                    new MissionItemReward
                                    {
                                        LowId = 100,
                                        HighId = 101,
                                        Ql = 154,
                                        Unk = 0
                                    }
                                },
                                MissionIcon = 123,
                                Playfield = new Identity(IdentityType.Playfield, 570),
                                Location = new Vector3(10.5f, 20.5f, 30.5f)
                            }
                        }
                    });
                capture.OnN3MessageSent(
                    5,
                    new CreateQuestMessage
                    {
                        Identity = player,
                        MissionId = selectedOffer
                    });
                capture.OnN3MessageReceived(
                    6,
                    new SimpleItemFullUpdateMessage
                    {
                        Identity = missionKey,
                        OwnerType = (int)IdentityType.SimpleChar,
                        OwnerInstance = player.Instance,
                        PlayfieldId = 655,
                        StateMachine = new Identity(IdentityType.Terminal, 1000015),
                        Stats = new[]
                        {
                            new GameTuple<Stat, int>
                            {
                                Value1 = Stat.StaticInstance,
                                Value2 = 28577
                            },
                            new GameTuple<Stat, int>
                            {
                                Value1 = Stat.ACGItemTemplateID2,
                                Value2 = 28577
                            },
                            new GameTuple<Stat, int>
                            {
                                Value1 = Stat.ACGItemLevel,
                                Value2 = 1
                            }
                        }
                    });
                capture.OnN3MessageReceived(
                    7,
                    new QuestFullUpdateMessage
                    {
                        Identity = player,
                        Quests = new[]
                        {
                            new Quest
                            {
                                QuestId = acceptedQuest,
                                ShortInfo = "Find the target",
                                LongInfo = "Complete mission details",
                                MissionIconId = 123,
                                MissionItemData = new[]
                                {
                                    new MissionItemReward
                                    {
                                        LowId = 100,
                                        HighId = 101,
                                        Ql = 154,
                                        Unk = 0
                                    }
                                },
                                QuestActions = new[]
                                {
                                    new QuestActionInfo
                                    {
                                        Version = 1,
                                        Action = new Identity(IdentityType.Terminal, 77),
                                        PlayfieldId = new Identity(IdentityType.Playfield, 570),
                                        Position = new Vector3(10.5f, 20.5f, 30.5f)
                                    }
                                }
                            }
                        }
                    });
                capture.OnN3MessageReceived(
                    8,
                    new N3TeleportMessage
                    {
                        Identity = otherPlayer,
                        Playfield = new Identity(IdentityType.Playfield, 1419000)
                    });
                capture.OnN3MessageReceived(
                    9,
                    new N3TeleportMessage
                    {
                        Identity = player,
                        Destination = new Vector3(1, 2, 3),
                        Playfield = new Identity(IdentityType.Playfield, 1419000)
                    });
                capture.Teardown();

                string firstLog = File.ReadAllText(Path.Combine(firstSession, "mission-flow.log"));
                Require(firstLog, "[OUT-TERMINAL-USE]");
                Require(firstLog, "difficulty=1");
                Require(firstLog, "creditsXp=7");
                Require(firstLog, "mission=(Mission:556DA5CB)");
                Require(firstLog, "title=\"Find the target\"");
                Require(firstLog, "credits=5000");
                Require(firstLog, "xp=7000");
                Require(firstLog, "rewards=[100/101@154:0]");
                Require(firstLog, "selectedOffer=(Mission:556DA5CB)");
                Require(firstLog, "item=(MissionKey:F687D7)");
                Require(firstLog, "acceptedQuest=(Mission:556DA5D5)");
                Require(firstLog, "[IN-N3-TELEPORT]");
                if (CountOccurrences(firstLog, "[IN-N3-TELEPORT]") != 1)
                {
                    throw new InvalidOperationException("local-player teleport filtering failed");
                }

                capture.BindSession(secondSession);
                capture.OnPlayfieldInit(123);
                capture.Teardown();
                string secondLog = File.ReadAllText(Path.Combine(secondSession, "mission-flow.log"));
                Require(secondLog, "activeQuests=[]");
                if (secondLog.IndexOf("556DA5CB", StringComparison.OrdinalIgnoreCase) >= 0
                    || secondLog.IndexOf("556DA5D5", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    throw new InvalidOperationException("mission state leaked across capture sessions");
                }

                Console.WriteLine("AOSharp mission capture analyzer self-test PASS");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("AOSharp mission capture analyzer self-test FAIL: " + exception.Message);
                return 1;
            }
            finally
            {
                try
                {
                    Directory.Delete(root, true);
                }
                catch
                {
                }
            }
        }

        private static List<RawPacketRow> LoadRows(string path)
        {
            var rows = new List<RawPacketRow>();
            using (var reader = new System.IO.StreamReader(path, Encoding.UTF8, true))
            {
                string header = reader.ReadLine();
                if (!string.Equals(
                        header,
                        "CapturedUtc,ElapsedMilliseconds,Direction,GlobalOrdinal,Sequence,PacketLength,N3TypeValue,N3TypeName,IdentityType,IdentityInstance,PreservationStatus,RawHex",
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException("raw-packets.csv header is not recognized");
                }

                string line;
                int lineNumber = 1;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    string[] fields = ParseCsvLine(line);
                    if (fields.Length != 12)
                    {
                        throw new InvalidDataException(
                            "raw-packets.csv line "
                            + lineNumber.ToString(CultureInfo.InvariantCulture)
                            + " has "
                            + fields.Length.ToString(CultureInfo.InvariantCulture)
                            + " fields");
                    }

                    DateTime capturedUtc;
                    long globalOrdinal;
                    int sequence;
                    if (!DateTime.TryParse(
                            fields[0],
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind,
                            out capturedUtc)
                        || !long.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out globalOrdinal)
                        || !int.TryParse(fields[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out sequence))
                    {
                        throw new InvalidDataException(
                            "raw-packets.csv line "
                            + lineNumber.ToString(CultureInfo.InvariantCulture)
                            + " has invalid metadata");
                    }

                    rows.Add(
                        new RawPacketRow
                        {
                            CapturedUtc = capturedUtc,
                            Direction = fields[2],
                            GlobalOrdinal = globalOrdinal,
                            Sequence = sequence,
                            N3TypeName = fields[7],
                            PreservationStatus = fields[10],
                            RawHex = fields[11]
                        });
                }
            }

            return rows;
        }

        private static string[] ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;
            for (int index = 0; index < line.Length; index++)
            {
                char current = line[index];
                if (current == '"')
                {
                    if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                }
                else if (current == ',' && !quoted)
                {
                    fields.Add(field.ToString());
                    field.Length = 0;
                }
                else
                {
                    field.Append(current);
                }
            }

            if (quoted)
            {
                throw new InvalidDataException("unterminated CSV quote");
            }

            fields.Add(field.ToString());
            return fields.ToArray();
        }

        private static byte[] HexToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex) || (hex.Length & 1) != 0)
            {
                throw new InvalidDataException("raw packet hex length is invalid");
            }

            var bytes = new byte[hex.Length / 2];
            for (int index = 0; index < bytes.Length; index++)
            {
                bytes[index] = byte.Parse(
                    hex.Substring(index * 2, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture);
            }

            return bytes;
        }

        private static string FormatError(RawPacketRow row, string error)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "globalOrdinal={0} direction={1} sequence={2} type={3} preservation={4} error={5}",
                row.GlobalOrdinal,
                row.Direction,
                row.Sequence,
                row.N3TypeName,
                row.PreservationStatus,
                error);
        }

        private static void WriteErrors(string path, IEnumerable<string> errors)
        {
            File.WriteAllLines(path, errors, new UTF8Encoding(false));
        }

        private static void Increment(IDictionary<string, int> counts, string key)
        {
            int count;
            counts.TryGetValue(key, out count);
            counts[key] = count + 1;
        }

        private static void CountMissionLogCategories(
            string missionLog,
            IDictionary<string, int> counts)
        {
            using (var reader = new StringReader(missionLog))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    int open = line.IndexOf("[", StringComparison.Ordinal);
                    int close = open < 0 ? -1 : line.IndexOf(']', open + 1);
                    if (open < 0 || close <= open + 1)
                    {
                        continue;
                    }

                    Increment(counts, "LOG-" + line.Substring(open + 1, close - open - 1));
                }
            }
        }

        private static void Require(string text, string expected)
        {
            if (text.IndexOf(expected, StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("missing expected log fragment: " + expected);
            }
        }

        private static int CountOccurrences(string text, string value)
        {
            int count = 0;
            int offset = 0;
            while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += value.Length;
            }

            return count;
        }

        private sealed class RawPacketRow
        {
            public DateTime CapturedUtc { get; set; }

            public string Direction { get; set; }

            public long GlobalOrdinal { get; set; }

            public int Sequence { get; set; }

            public string N3TypeName { get; set; }

            public string PreservationStatus { get; set; }

            public string RawHex { get; set; }
        }
    }
}
