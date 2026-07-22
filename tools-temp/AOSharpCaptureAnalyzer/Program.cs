namespace AOSharpCaptureAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;

    using AORebirth.CaptureProtocol;

    internal static class Program
    {
        private const string HexMarker = " hex=";

        private const string AbmouthPacketHex =
            "0879000A000100E000000DB47944C065271B3A6B0000C35079607A11003A022A4A430015300843B28B514298374542C63F4100000000BF3695FE000000003F337068000004CB1141626D6F7574682053757072656D757300100812010000000096000000001E2854000002613A00A2001F000000001C8000000000000000800000000301000100010001000100000002000072000003F1000017A6000000000000000000000000000000010000000000000000000000020000000000000000000000030000000000000000000000040000000000000000000003F10000000000";

        private const string ReplacementInfectorPacketHex =
            "099F000A000100D900000DB47944C065271B3A6B0000C35079607AD0003A022A4A430015300843A7038C42933C6442C617A4000000003F374729000000003F32BB6F000004C809496E666563746F7200100812010000000096000A00001803C80000007CA50046001F000000001C0000000000000000800000000301000100010001000100000002000069000003F1000017A6000000000000000000000000000000010000000000000000000000020000000000000000000000030000000000000000000000040000000000000000000003F1000000020000";

        // Capture 20260613-181432, packets.hex.log sequence 669.
        private const string CharacterTowerFlagPacketHex =
            "610F000A000100EA00000DAD70CBBEF3271B3A6B0000C350782DE56F003A0A2A4A4300074B50455DF01841050A3D4450368200000000BF34917E800000003F35781F000004CB1B5368697070696E67204D616E6966657374205465726D696E616C00108A12010000000089000000001902D400000442900064001F000000001C8000000000000000000000000101000100010001000100000002000056000003F1000017A6000000000000000000000000000000010000000000000000000000020000000000000000000000030000000000000000000000040000000000000000000003F10000000000";

        // Capture 20260623-045431, packets.hex.log sequence 24.
        private const string LegacyVersion57PlayerPacketHex =
            "0007000A000101120000035600000012271B3A6B0000C35000000012003904006AC00012400D440480424323414844113FBE000000003F80000000000000B33BBD310000062A084D696B65646F63000008124102D2007F0000056E00000000000500C801320132013201320132000B54657374696E67204F72670A051400000000000064003F000000002A80000000000000008000000003010001000100010001000000030000000000000000000000000000000000009EE909C4000003F1000017A6000000000000000000000000000000010000000000000000000000020000000000000000000000030000000000000000000000040000000000000000000007E20000009EE900000000040000000000";

        // Capture 20260614-195107, packets.hex.log sequence 719.
        private const string MultipleTerminalSpecialAttackSlotsPacketHex =
            "63E8000A0001016000000DAD78CB984B271B3A6B0000C35078D3ACFF003A0A204F5300074B50456292F64206C7AD445B596000000000BF4F45D1000000003F163F1D0000A4CB174275726E696E6720436C65616E696E6720526F626F7400100C12010000000003FA0BB8000000030023220004883F00C8001F000000001C800000000000000080000000010100010001000100010000000200000D0000C35078671D5800000BD36D64726F6E6531000000000000000000000000000000000000000000000000000004894200000000000000006D64726F6E653200000000000000000000000000000000000000000000000000000489430000000000000000000003F1000017A6000000000000000000000000000000010000000000000000000000020000000000000000000000030000000000000000000000040000000000000000000003F1000007E2000495EB000495EC514B5349514B53490000000000";

        // Capture 20260614-200850, packets.hex.log sequence 5999.
        private const string DeclaredSlotBeforePlayerOpaqueExtensionPacketHex =
            "AB29000A0001017300000DAD78CB984B271B3A6B0000C35078D2E016003A00204FCA00074B5045611DE642508A3D4444846F00000000BF7C8EDC000000003E275A500000A58009436875636B666F6F0000081241000001BF0000002000000000000500120009000D0006000600060200550A00000000006E001F000000002A80000000000000008000000003010001000100010001000000030000000000000000000000000000000000009CAF0A0000C35078D3AE0000000BD30000CF1B00049D11000000000000EA600000919B0000CF1B0004614600000000002BF200002B1533000017A600000000000000000000000000000001000000000000000000000002000024BF0000000000000003000000000000000000000004000024B90000000000000BD3000003942700000000000000009CAF000000000400000FC40003399800033999000000644D4141540000A4310000A430000000904449495400011294000112950000008E425241570000000000";

        // Capture 20260614-215831, packets.hex.log sequence 11888.
        private const string TerminalOneByteSpecialAttackUnknown6PacketHex =
            "975A000A0001014D00000DAD78CB984B271B3A6B0000C35078D30B0B003A0A2B6F4B00074B504579FBE53C23D70A4427896200000000BF291CB1800000003F4030B60000A6281353414E4453544F524D204D61726175646572001008120100000000640000000007028A00000461F1005E001F000000001CC0D62D813EEB24C83F5BC9590302010100010001000100000002000001F40000C35078D45949000003F10000C35078D30B0B000000024579FBE53C23D70A442789624576D8E73F7453A94427957F000017A60000000000040DAC000000000000000100040DA8000000000000000200040DAA000000000000000300040DB0000000000000000400040DAE00000000000007E20000040E30000000000200000FC40003F81C0003F81D49444C5949444C590003F8190003F81A5146434B5146434B0003F8160003F8174251494F4251494F0000000000";

        private static int Main(string[] args)
        {
            Console.WriteLine(
                "AOSharpCaptureAnalyzer process bitness: "
                + (Environment.Is64BitProcess ? "64-bit" : "32-bit"));

            if (args.Length == 1 && string.Equals(args[0], "--self-test", StringComparison.Ordinal))
            {
                return RunSelfTest();
            }

            if (args.Length == 1
                && string.Equals(
                    args[0],
                    "--self-test-pf127-los-promotion",
                    StringComparison.Ordinal))
            {
                return Pf127LineOfSightPromotionValidator.RunSelfTest();
            }

            if (args.Length == 1
                && string.Equals(
                    args[0],
                    "--self-test-pf127-capture-snapshot",
                    StringComparison.Ordinal))
            {
                return Pf127CaptureSnapshot.RunSelfTest();
            }

            if (args.Length == 3
                && string.Equals(
                    args[0],
                    "--snapshot-pf127-capture",
                    StringComparison.Ordinal))
            {
                return SnapshotPf127Capture(args[1], args[2]);
            }

            if (args.Length >= 2
                && args.Length <= 3
                && string.Equals(args[0], "--promote-pf127-los", StringComparison.Ordinal))
            {
                return PromotePf127LineOfSight(
                    args[1],
                    args.Length == 3 ? args[2] : null);
            }

            if (args.Length == 0)
            {
                Console.Error.WriteLine("Usage: AOSharpCaptureAnalyzer <capture-folder> [capture-folder ...]");
                Console.Error.WriteLine("       AOSharpCaptureAnalyzer --self-test");
                Console.Error.WriteLine("       AOSharpCaptureAnalyzer --self-test-pf127-los-promotion");
                Console.Error.WriteLine("       AOSharpCaptureAnalyzer --self-test-pf127-capture-snapshot");
                Console.Error.WriteLine("       AOSharpCaptureAnalyzer --snapshot-pf127-capture <live-capture-folder> <new-snapshot-folder>");
                Console.Error.WriteLine("       AOSharpCaptureAnalyzer --promote-pf127-los <capture-folder> [reviewed-json-output]");
                return 2;
            }

            int failures = 0;
            foreach (string captureFolder in args)
            {
                failures += ExportCapture(captureFolder);
            }

            return failures == 0 ? 0 : 1;
        }

        private static int SnapshotPf127Capture(string sourceDirectory, string outputDirectory)
        {
            try
            {
                Pf127CaptureSnapshotResult result = Pf127CaptureSnapshot.Create(
                    sourceDirectory,
                    outputDirectory);
                Console.WriteLine(
                    "PF127 capture snapshot PASS output="
                    + result.OutputDirectory
                    + " manifest="
                    + result.ManifestPath);
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("PF127 capture snapshot FAIL: " + exception.Message);
                return 1;
            }
        }

        private static int PromotePf127LineOfSight(string captureFolder, string outputPath)
        {
            if (!Environment.Is64BitProcess)
            {
                Console.Error.WriteLine(
                    "PF127 LOS promotion requires a 64-bit AOSharpCaptureAnalyzer process. Rebuild and run the analyzer as AnyCPU with Prefer32Bit=false or as x64; the full PF127 geometry is not supported in a 32-bit process.");
                return 1;
            }

            try
            {
                Pf127LineOfSightPromotionResult result =
                    Pf127LineOfSightPromotionValidator.Promote(captureFolder, outputPath);
                Console.WriteLine(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "PF127 LOS promotion PASS variant={0} height={1:R} pairs={2} clear={3} blocked={4} nativeRejected={5} sourceSha256={6} outputSha256={7} output={8}",
                        result.ProbeVariant,
                        result.ProbeHeight,
                        result.PairCount,
                        result.ClearPairCount,
                        result.BlockedPairCount,
                        result.NativeDisagreementPairCount,
                        result.SourceSha256,
                        result.OutputSha256,
                        result.OutputPath));
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("PF127 LOS promotion FAIL: " + exception.Message);
                return 1;
            }
        }

        private static int ExportCapture(string captureFolder)
        {
            CapturePacketSet packetSet;
            try
            {
                packetSet = LoadCapturePackets(captureFolder);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(Path.GetFileName(captureFolder) + ": " + exception.Message);
                return 1;
            }

            string outputPath = Path.Combine(captureFolder, "scfu-appearance.csv");
            string errorPath = Path.Combine(captureFolder, "scfu-decode-errors.csv");
            string pendingOutputPath = Path.Combine(captureFolder, "scfu-appearance.pending.csv");
            string pendingErrorPath = Path.Combine(captureFolder, "scfu-decode-errors.pending.csv");
            DeleteIfExists(pendingOutputPath);
            DeleteIfExists(pendingErrorPath);
            int rows = 0;
            int failures = packetSet.SourceFailures;
            int incomplete = 0;
            using (var output = new StreamWriter(pendingOutputPath, false, new UTF8Encoding(false)))
            using (var errors = new StreamWriter(pendingErrorPath, false, new UTF8Encoding(false)))
            {
                output.WriteLine(RawScfuAppearanceCsv.Header);
                errors.WriteLine("CapturedUtc,Direction,Sequence,DecodeStatus,DecodeError,RawPacketHex,RawBodyHex");
                foreach (CapturedPacket capturedPacket in packetSet.Packets)
                {
                    RawSimpleCharFullUpdate message;
                    string decodeError;
                    bool decoded = RawSimpleCharFullUpdateDecoder.TryDecodePacket(
                        capturedPacket.Packet,
                        out message,
                        out decodeError);
                    if (!decoded)
                    {
                        failures++;
                    }
                    else if (!message.DecodeFullyConsumed)
                    {
                        incomplete++;
                    }

                    output.WriteLine(
                        RawScfuAppearanceCsv.FormatRow(
                            capturedPacket.Metadata,
                            capturedPacket.Packet,
                            message,
                            decodeError));
                    rows++;

                    if (!decoded)
                    {
                        errors.WriteLine(
                            string.Join(
                                ",",
                                Csv(capturedPacket.Metadata.CapturedUtc),
                                Csv(capturedPacket.Metadata.Direction),
                                Csv(capturedPacket.Metadata.Sequence),
                                Csv("decode_failed"),
                                Csv(decodeError),
                                Csv(RawScfuFormatting.ToHex(capturedPacket.Packet)),
                                Csv(PacketBodyHex(capturedPacket.Packet))));
                    }
                }
            }

            int result = failures + incomplete;
            if (result == 0)
            {
                PromoteFile(pendingOutputPath, outputPath);
                PromoteFile(pendingErrorPath, errorPath);
            }
            else
            {
                foreach (string failureMessage in packetSet.FailureMessages)
                {
                    Console.Error.WriteLine(Path.GetFileName(captureFolder) + ": " + failureMessage);
                }
            }

            Console.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}: SCFU rows={1} failures={2} incomplete={3} packetLog={4} rawFallback={5} outsideWindowPacketLog={6} outsideWindowRawIndex={7}",
                    Path.GetFileName(captureFolder),
                    rows,
                    failures,
                    incomplete,
                    packetSet.PacketLogRows,
                    packetSet.RawFallbackRows,
                    packetSet.PacketLogRowsOutsideCaptureWindow,
                    packetSet.RawIndexRowsOutsideCaptureWindow));
            return result;
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static void PromoteFile(string pendingPath, string outputPath)
        {
            if (File.Exists(outputPath))
            {
                File.Replace(pendingPath, outputPath, null, true);
            }
            else
            {
                File.Move(pendingPath, outputPath);
            }
        }

        private static CapturePacketSet LoadCapturePackets(string captureFolder)
        {
            string packetPath = Path.Combine(captureFolder, "packets.hex.log");
            string rawPacketPath = Path.Combine(captureFolder, "raw-packets.csv");
            if (!File.Exists(packetPath) && !File.Exists(rawPacketPath))
            {
                throw new FileNotFoundException(
                    "Neither packets.hex.log nor raw-packets.csv exists in the capture folder.");
            }

            CaptureExpectations expectations = ReadCaptureExpectations(captureFolder);
            PacketSourceReport packetLog = ReadPacketLog(packetPath, expectations);
            PacketSourceReport rawIndex = ReadRawPacketIndex(rawPacketPath, expectations);
            return ReconcileSources(packetLog, rawIndex, expectations);
        }

        private static PacketSourceReport ReadPacketLog(
            string path,
            CaptureExpectations expectations)
        {
            var report = new PacketSourceReport("packets.hex.log", File.Exists(path));
            if (!report.Exists)
            {
                return report;
            }

            foreach (string line in ReadSharedLines(path))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                int markerIndex = line.IndexOf(HexMarker, StringComparison.Ordinal);
                string[] prefix = markerIndex < 0
                                      ? line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
                                      : line.Substring(0, markerIndex)
                                            .Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                var metadata = new RawScfuCaptureMetadata
                {
                    CapturedUtc = prefix.Length > 0 ? prefix[0] : string.Empty,
                    Direction = prefix.Length > 1 ? prefix[1] : string.Empty,
                    Sequence = prefix.Length > 2 ? prefix[2].TrimStart('#') : string.Empty
                };

                if (!IsInsideCaptureWindow(metadata.CapturedUtc, expectations))
                {
                    report.RowsOutsideCaptureWindow++;
                    continue;
                }

                report.RawRowCount++;

                int declaredLength = 0;
                string error = markerIndex < 0
                                   ? "packet log row has no hex payload"
                                   : !TryReadLengthToken(prefix, out declaredLength)
                                         ? "packet log row has no valid len declaration"
                                         : string.Empty;
                byte[] packet = null;
                if (string.IsNullOrEmpty(error))
                {
                    try
                    {
                        packet = FromHex(line.Substring(markerIndex + HexMarker.Length).Trim());
                    }
                    catch (Exception exception)
                    {
                        error = "invalid packet hex: " + exception.Message;
                    }
                }

                AddSourcePacket(report, metadata, packet, declaredLength, "raw_complete", error);
            }

            return report;
        }

        private static PacketSourceReport ReadRawPacketIndex(
            string path,
            CaptureExpectations expectations)
        {
            var report = new PacketSourceReport("raw-packets.csv", File.Exists(path));
            if (!report.Exists)
            {
                return report;
            }

            using (IEnumerator<string> lines = ReadSharedLines(path).GetEnumerator())
            {
                if (!lines.MoveNext())
                {
                    report.HeaderValid = false;
                    report.Errors.Add("raw-packets.csv is empty");
                    return report;
                }

                List<string> headers = ParseCsvLine(lines.Current);
                var headerIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < headers.Count; i++)
                {
                    headerIndexes[headers[i].TrimStart('\uFEFF')] = i;
                }

                string[] requiredHeaders =
                {
                    "CapturedUtc",
                    "Direction",
                    "Sequence",
                    "PacketLength",
                    "PreservationStatus",
                    "RawHex"
                };
                foreach (string requiredHeader in requiredHeaders)
                {
                    if (!headerIndexes.ContainsKey(requiredHeader))
                    {
                        report.HeaderValid = false;
                        report.Errors.Add("raw-packets.csv is missing header " + requiredHeader);
                    }
                }

                while (lines.MoveNext())
                {
                    if (string.IsNullOrWhiteSpace(lines.Current))
                    {
                        continue;
                    }

                    List<string> values = ParseCsvLine(lines.Current);
                    var metadata = new RawScfuCaptureMetadata
                    {
                        CapturedUtc = CsvValue(values, headerIndexes, "CapturedUtc"),
                        ElapsedMilliseconds = CsvValue(values, headerIndexes, "ElapsedMilliseconds"),
                        Direction = CsvValue(values, headerIndexes, "Direction"),
                        GlobalOrdinal = CsvValue(values, headerIndexes, "GlobalOrdinal"),
                        Sequence = CsvValue(values, headerIndexes, "Sequence")
                    };

                    if (!IsInsideCaptureWindow(metadata.CapturedUtc, expectations))
                    {
                        report.RowsOutsideCaptureWindow++;
                        continue;
                    }

                    report.RawRowCount++;
                    int declaredLength;
                    string declaredLengthText = CsvValue(values, headerIndexes, "PacketLength");
                    string rawPacketHex = CsvValue(values, headerIndexes, "RawHex");
                    string error = !int.TryParse(
                                       declaredLengthText,
                                       NumberStyles.Integer,
                                       CultureInfo.InvariantCulture,
                                       out declaredLength)
                                       ? "raw packet row has invalid PacketLength"
                                       : string.IsNullOrWhiteSpace(rawPacketHex)
                                             ? "raw packet row has no RawHex"
                                             : string.Empty;
                    byte[] packet = null;
                    if (string.IsNullOrEmpty(error))
                    {
                        try
                        {
                            packet = FromHex(rawPacketHex);
                        }
                        catch (Exception exception)
                        {
                            error = "invalid packet hex: " + exception.Message;
                        }
                    }

                    AddSourcePacket(
                        report,
                        metadata,
                        packet,
                        declaredLength,
                        CsvValue(values, headerIndexes, "PreservationStatus"),
                        error);
                }
            }

            return report;
        }

        private static void AddSourcePacket(
            PacketSourceReport report,
            RawScfuCaptureMetadata metadata,
            byte[] packet,
            int declaredLength,
            string preservationStatus,
            string error)
        {
            string eventKey = BuildEventKey(metadata);
            if (string.IsNullOrEmpty(error) && string.IsNullOrEmpty(eventKey))
            {
                error = "raw packet row has no direction/sequence event key";
            }

            if (string.IsNullOrEmpty(error)
                && !string.Equals(preservationStatus, "raw_complete", StringComparison.OrdinalIgnoreCase))
            {
                error = "raw packet row preservation status is not raw_complete";
            }

            if (string.IsNullOrEmpty(error)
                && (packet == null || packet.Length == 0 || packet.Length != declaredLength))
            {
                error = string.Format(
                    CultureInfo.InvariantCulture,
                    "raw packet length mismatch: declared={0}, actual={1}",
                    declaredLength,
                    packet == null ? 0 : packet.Length);
            }

            if (string.IsNullOrEmpty(error) && IsSimpleCharFullUpdatePacket(packet))
            {
                int frameLength = (packet[6] << 8) | packet[7];
                if (frameLength != packet.Length)
                {
                    error = string.Format(
                        CultureInfo.InvariantCulture,
                        "SCFU frame length mismatch: header={0}, actual={1}",
                        frameLength,
                        packet.Length);
                }
            }

            if (!string.IsNullOrEmpty(error))
            {
                report.InvalidRowCount++;
                report.Errors.Add(
                    string.IsNullOrEmpty(eventKey)
                        ? error
                        : eventKey + ": " + error);
                return;
            }

            if (report.RecordByEvent.ContainsKey(eventKey))
            {
                report.InvalidRowCount++;
                report.Errors.Add(eventKey + ": duplicate event row in " + report.Name);
                return;
            }

            var record = new SourcePacketRecord
            {
                EventKey = eventKey,
                Metadata = metadata,
                Packet = packet,
                DeclaredLength = declaredLength,
                SourceName = report.Name
            };
            report.RecordByEvent[eventKey] = record;
            report.Records.Add(record);
            report.ValidRowCount++;
            if (IsSimpleCharFullUpdatePacket(packet))
            {
                report.ValidScfuRowCount++;
            }
        }

        private static CapturePacketSet ReconcileSources(
            PacketSourceReport packetLog,
            PacketSourceReport rawIndex,
            CaptureExpectations expectations)
        {
            var result = new CapturePacketSet
            {
                PacketLogRows = packetLog.ValidScfuRowCount,
                PacketLogRowsOutsideCaptureWindow = packetLog.RowsOutsideCaptureWindow,
                RawIndexRowsOutsideCaptureWindow = rawIndex.RowsOutsideCaptureWindow
            };
            bool packetLogComplete = IsSourceComplete(packetLog, expectations.ExpectedRawPackets);
            bool rawIndexComplete = IsSourceComplete(rawIndex, expectations.ExpectedRawPackets);
            PacketSourceReport authoritativeSource = packetLogComplete ^ rawIndexComplete
                                                         ? packetLogComplete ? packetLog : rawIndex
                                                         : null;
            var eventOrder = new List<string>();
            var evidenceByEvent = new Dictionary<string, ReconciledEvent>(StringComparer.Ordinal);
            var selectedRecords = new List<SourcePacketRecord>();
            AddSourceEvidence(packetLog, true, eventOrder, evidenceByEvent);
            AddSourceEvidence(rawIndex, false, eventOrder, evidenceByEvent);

            if (authoritativeSource != null)
            {
                PacketSourceReport otherSource = ReferenceEquals(authoritativeSource, packetLog)
                                                     ? rawIndex
                                                     : packetLog;
                foreach (SourcePacketRecord selected in authoritativeSource.Records)
                {
                    SourcePacketRecord matching;
                    if (otherSource.RecordByEvent.TryGetValue(selected.EventKey, out matching)
                        && ByteArraysEqual(selected.Packet, matching.Packet))
                    {
                        FillBlankMetadata(selected.Metadata, matching.Metadata);
                    }

                    selected.SelectedFromRawFallback = ReferenceEquals(authoritativeSource, rawIndex);
                    selected.ReconcileOrder = selectedRecords.Count;
                    selectedRecords.Add(selected);
                }
            }
            else
            {
                foreach (string eventKey in eventOrder)
                {
                    ReconciledEvent evidence = evidenceByEvent[eventKey];
                    SourcePacketRecord selected;
                    if (evidence.PacketLog != null && evidence.RawIndex != null)
                    {
                        if (!ByteArraysEqual(evidence.PacketLog.Packet, evidence.RawIndex.Packet))
                        {
                            result.AddFailure(eventKey + ": raw sink conflict");
                            continue;
                        }

                        selected = evidence.PacketLog;
                        selected.SelectedFromRawFallback = false;
                        FillBlankMetadata(selected.Metadata, evidence.RawIndex.Metadata);
                    }
                    else
                    {
                        selected = evidence.PacketLog ?? evidence.RawIndex;
                        selected.SelectedFromRawFallback = evidence.PacketLog == null;
                    }

                    selected.ReconcileOrder = selectedRecords.Count;
                    selectedRecords.Add(selected);
                }
            }

            selectedRecords.Sort(CompareSourcePacketRecords);
            foreach (SourcePacketRecord selected in selectedRecords)
            {
                result.ResolvedRawPacketCount++;
                if (IsSimpleCharFullUpdatePacket(selected.Packet))
                {
                    if (selected.SelectedFromRawFallback)
                    {
                        result.RawFallbackRows++;
                    }

                    result.Packets.Add(
                        new CapturedPacket
                        {
                            Metadata = selected.Metadata,
                            Packet = selected.Packet
                        });
                }
            }

            if (packetLog.RawRowCount == 0 && rawIndex.RawRowCount == 0)
            {
                result.AddFailure("both raw packet sources are empty");
            }

            if (expectations.RecaptureRequired.HasValue && expectations.RecaptureRequired.Value)
            {
                result.AddFailure("capture_info.json reports recaptureRequired=true");
            }

            if (expectations.ExpectedRawPackets.HasValue)
            {
                if (result.ResolvedRawPacketCount != expectations.ExpectedRawPackets.Value)
                {
                    result.AddFailure(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "resolved raw packet count mismatch: expected={0}, actual={1}",
                            expectations.ExpectedRawPackets.Value,
                            result.ResolvedRawPacketCount));
                }
            }
            else if (!packetLogComplete && !rawIndexComplete)
            {
                result.AddFailure("no structurally complete non-empty raw packet source is available");
            }

            if (expectations.ExpectedScfuPackets.HasValue
                && result.Packets.Count != expectations.ExpectedScfuPackets.Value)
            {
                result.AddFailure(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "resolved SCFU packet count mismatch: expected={0}, actual={1}",
                        expectations.ExpectedScfuPackets.Value,
                        result.Packets.Count));
            }

            if (!packetLogComplete
                && !rawIndexComplete
                && !expectations.ExpectedRawPackets.HasValue)
            {
                AppendSourceErrors(result, packetLog);
                AppendSourceErrors(result, rawIndex);
            }

            return result;
        }

        private static int CompareSourcePacketRecords(SourcePacketRecord left, SourcePacketRecord right)
        {
            DateTime leftTimestamp;
            DateTime rightTimestamp;
            bool hasLeftTimestamp = DateTime.TryParse(
                left.Metadata == null ? string.Empty : left.Metadata.CapturedUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out leftTimestamp);
            bool hasRightTimestamp = DateTime.TryParse(
                right.Metadata == null ? string.Empty : right.Metadata.CapturedUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out rightTimestamp);
            if (hasLeftTimestamp && hasRightTimestamp)
            {
                int timestampComparison = leftTimestamp.CompareTo(rightTimestamp);
                if (timestampComparison != 0)
                {
                    return timestampComparison;
                }
            }

            long leftOrdinal;
            long rightOrdinal;
            bool hasLeftOrdinal = long.TryParse(
                left.Metadata == null ? string.Empty : left.Metadata.GlobalOrdinal,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out leftOrdinal);
            bool hasRightOrdinal = long.TryParse(
                right.Metadata == null ? string.Empty : right.Metadata.GlobalOrdinal,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out rightOrdinal);
            if (hasLeftOrdinal && hasRightOrdinal)
            {
                int ordinalComparison = leftOrdinal.CompareTo(rightOrdinal);
                if (ordinalComparison != 0)
                {
                    return ordinalComparison;
                }
            }

            return left.ReconcileOrder.CompareTo(right.ReconcileOrder);
        }

        private static void AddSourceEvidence(
            PacketSourceReport source,
            bool packetLog,
            ICollection<string> eventOrder,
            IDictionary<string, ReconciledEvent> evidenceByEvent)
        {
            foreach (SourcePacketRecord record in source.Records)
            {
                ReconciledEvent evidence;
                if (!evidenceByEvent.TryGetValue(record.EventKey, out evidence))
                {
                    evidence = new ReconciledEvent();
                    evidenceByEvent[record.EventKey] = evidence;
                    eventOrder.Add(record.EventKey);
                }

                if (packetLog)
                {
                    evidence.PacketLog = record;
                }
                else
                {
                    evidence.RawIndex = record;
                }
            }
        }

        private static bool IsSourceComplete(PacketSourceReport source, int? expectedRawPackets)
        {
            return source.Exists
                   && source.HeaderValid
                   && source.RawRowCount > 0
                   && source.InvalidRowCount == 0
                   && source.ValidRowCount == source.RawRowCount
                   && (!expectedRawPackets.HasValue
                       || source.ValidRowCount == expectedRawPackets.Value);
        }

        private static void AppendSourceErrors(CapturePacketSet result, PacketSourceReport source)
        {
            foreach (string error in source.Errors)
            {
                result.AddFailure(source.Name + ": " + error);
            }
        }

        private static CaptureExpectations ReadCaptureExpectations(string captureFolder)
        {
            string path = Path.Combine(captureFolder, "capture_info.json");
            var result = new CaptureExpectations();
            if (!File.Exists(path))
            {
                return result;
            }

            string json = File.ReadAllText(path);
            DateTime captureStartUtc;
            if (TryReadJsonDateTime(json, "captureStartUtc", out captureStartUtc))
            {
                result.CaptureStartUtc = captureStartUtc;
            }

            DateTime captureEndUtc;
            if (TryReadJsonDateTime(json, "captureFinalizedUtc", out captureEndUtc)
                || TryReadJsonDateTime(json, "captureEndUtc", out captureEndUtc))
            {
                result.CaptureEndUtc = captureEndUtc;
            }

            if (result.CaptureEndUtc.HasValue)
            {
                int inbound;
                int outbound;
                if (TryReadJsonInt(json, "inboundRaw", out inbound)
                    && TryReadJsonInt(json, "outboundRaw", out outbound))
                {
                    result.ExpectedRawPackets = checked(inbound + outbound);
                }

                int scfu;
                if (TryReadJsonInt(json, "rawSimpleCharFullUpdatePackets", out scfu))
                {
                    result.ExpectedScfuPackets = scfu;
                }
            }

            bool recaptureRequired;
            if (TryReadJsonBool(json, "recaptureRequired", out recaptureRequired))
            {
                result.RecaptureRequired = recaptureRequired;
            }

            return result;
        }

        private static bool TryReadJsonInt(string json, string propertyName, out int value)
        {
            value = 0;
            Match match = Regex.Match(
                json ?? string.Empty,
                "\\\"" + Regex.Escape(propertyName) + "\\\"\\s*:\\s*(?<value>[0-9]+)",
                RegexOptions.CultureInvariant);
            return match.Success
                   && int.TryParse(
                       match.Groups["value"].Value,
                       NumberStyles.None,
                       CultureInfo.InvariantCulture,
                       out value);
        }

        private static bool TryReadJsonBool(string json, string propertyName, out bool value)
        {
            value = false;
            Match match = Regex.Match(
                json ?? string.Empty,
                "\\\"" + Regex.Escape(propertyName) + "\\\"\\s*:\\s*(?<value>true|false)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return match.Success && bool.TryParse(match.Groups["value"].Value, out value);
        }

        private static bool TryReadJsonDateTime(
            string json,
            string propertyName,
            out DateTime value)
        {
            value = default(DateTime);
            Match match = Regex.Match(
                json ?? string.Empty,
                "\\\"" + Regex.Escape(propertyName) + "\\\"\\s*:\\s*\\\"(?<value>[^\\\"]+)\\\"",
                RegexOptions.CultureInvariant);
            return match.Success
                   && DateTime.TryParse(
                       match.Groups["value"].Value,
                       CultureInfo.InvariantCulture,
                       DateTimeStyles.RoundtripKind,
                       out value);
        }

        private static bool IsInsideCaptureWindow(
            string capturedUtc,
            CaptureExpectations expectations)
        {
            if (expectations == null
                || (!expectations.CaptureStartUtc.HasValue
                    && !expectations.CaptureEndUtc.HasValue))
            {
                return true;
            }

            DateTime timestamp;
            if (!DateTime.TryParse(
                    capturedUtc ?? string.Empty,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out timestamp))
            {
                return true;
            }

            return (!expectations.CaptureStartUtc.HasValue
                    || timestamp >= expectations.CaptureStartUtc.Value)
                   && (!expectations.CaptureEndUtc.HasValue
                       || timestamp <= expectations.CaptureEndUtc.Value);
        }

        private static bool TryReadLengthToken(string[] prefix, out int value)
        {
            foreach (string token in prefix ?? new string[0])
            {
                if (token.StartsWith("len=", StringComparison.Ordinal)
                    && int.TryParse(
                        token.Substring(4),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out value))
                {
                    return true;
                }
            }

            value = 0;
            return false;
        }

        private static string BuildEventKey(RawScfuCaptureMetadata metadata)
        {
            if (metadata == null
                || string.IsNullOrWhiteSpace(metadata.Direction)
                || string.IsNullOrWhiteSpace(metadata.Sequence))
            {
                return string.Empty;
            }

            string direction = metadata.Direction.Trim().ToUpperInvariant();
            int sequence;
            if ((!string.Equals(direction, "IN", StringComparison.Ordinal)
                 && !string.Equals(direction, "OUT", StringComparison.Ordinal))
                || !int.TryParse(
                    metadata.Sequence.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out sequence)
                || sequence <= 0)
            {
                return string.Empty;
            }

            return direction + "|" + sequence.ToString(CultureInfo.InvariantCulture);
        }

        private static void FillBlankMetadata(RawScfuCaptureMetadata target, RawScfuCaptureMetadata source)
        {
            target.CapturedUtc = Prefer(target.CapturedUtc, source.CapturedUtc);
            target.ElapsedMilliseconds = Prefer(target.ElapsedMilliseconds, source.ElapsedMilliseconds);
            target.Direction = Prefer(target.Direction, source.Direction);
            target.GlobalOrdinal = Prefer(target.GlobalOrdinal, source.GlobalOrdinal);
            target.Sequence = Prefer(target.Sequence, source.Sequence);
        }

        private static string Prefer(string current, string candidate)
        {
            return string.IsNullOrEmpty(current) ? candidate : current;
        }

        private static bool ByteArraysEqual(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsSimpleCharFullUpdatePacket(byte[] packet)
        {
            return packet != null
                   && packet.Length >= RawSimpleCharFullUpdateDecoder.N3BodyOffset + 4
                   && ReadInt32BigEndian(packet, RawSimpleCharFullUpdateDecoder.N3BodyOffset)
                   == RawSimpleCharFullUpdateDecoder.SimpleCharFullUpdateType;
        }

        private static int ReadInt32BigEndian(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24)
                   | (bytes[offset + 1] << 16)
                   | (bytes[offset + 2] << 8)
                   | bytes[offset + 3];
        }

        private static string CsvValue(
            IList<string> values,
            IDictionary<string, int> indexes,
            string name)
        {
            int index;
            return indexes.TryGetValue(name, out index) && index >= 0 && index < values.Count
                       ? values[index]
                       : string.Empty;
        }

        private static List<string> ParseCsvLine(string line)
        {
            var values = new List<string>();
            var value = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < (line ?? string.Empty).Length; i++)
            {
                char current = line[i];
                if (current == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        value.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                }
                else if (current == ',' && !quoted)
                {
                    values.Add(value.ToString());
                    value.Clear();
                }
                else
                {
                    value.Append(current);
                }
            }

            values.Add(value.ToString());
            return values;
        }

        private static byte[] FromHex(string hex)
        {
            string value = (hex ?? string.Empty).Trim();
            if ((value.Length & 1) != 0)
            {
                throw new FormatException("Packet hex length is odd.");
            }

            var result = new byte[value.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = byte.Parse(
                    value.Substring(i * 2, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture);
            }

            return result;
        }

        private static string PacketBodyHex(byte[] packet)
        {
            if (packet == null || packet.Length <= RawSimpleCharFullUpdateDecoder.N3BodyOffset)
            {
                return string.Empty;
            }

            var body = new byte[packet.Length - RawSimpleCharFullUpdateDecoder.N3BodyOffset];
            Buffer.BlockCopy(packet, RawSimpleCharFullUpdateDecoder.N3BodyOffset, body, 0, body.Length);
            return RawScfuFormatting.ToHex(body);
        }

        private static IEnumerable<string> ReadSharedLines(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    yield return line;
                }
            }
        }

        private static int RunSelfTest()
        {
            try
            {
                byte[] abmouthPacket = FromHex(AbmouthPacketHex);
                RawSimpleCharFullUpdate abmouth = RawSimpleCharFullUpdateDecoder.DecodePacket(abmouthPacket);
                AssertEqual(224, abmouthPacket.Length, "Abmouth packet length");
                AssertEqual(208, abmouth.RawBody.Length, "Abmouth body length");
                AssertEqual(208, abmouth.BytesConsumed, "Abmouth bytes consumed");
                Assert(abmouth.DecodeFullyConsumed, "Abmouth complete decode");
                AssertEqual(0, abmouth.UndecodedTail.Length, "Abmouth tail length");
                AssertEqual(0xC350, abmouth.Identity.Type, "Abmouth identity type");
                AssertEqual(0x79607A11, abmouth.Identity.Instance, "Abmouth identity instance");
                AssertEqual(58, abmouth.Version, "Abmouth version");
                AssertEqual(0x022A4A43, abmouth.Flags, "Abmouth flags");
                AssertEqual(0x00153008, abmouth.PlayfieldId.GetValueOrDefault(), "Abmouth playfield");
                AssertEqual(0x43B28B51, FloatBits(abmouth.Position.X), "Abmouth position X bits");
                AssertEqual(0x42983745, FloatBits(abmouth.Position.Y), "Abmouth position Y bits");
                AssertEqual(0x42C63F41, FloatBits(abmouth.Position.Z), "Abmouth position Z bits");
                AssertEqual(unchecked((int)0xBF3695FE), FloatBits(abmouth.Heading.Y), "Abmouth heading Y bits");
                AssertEqual(0x3F337068, FloatBits(abmouth.Heading.W), "Abmouth heading W bits");
                AssertEqual(0x04CB, (int)abmouth.AppearanceValue, "Abmouth appearance");
                AssertEqual(0x10081201, abmouth.CharacterFlags, "Abmouth character flags");
                AssertEqual(150, abmouth.Npc.Family, "Abmouth NPC family");
                AssertEqual(0, abmouth.Npc.UnknownData, "Abmouth NPC unknown data");
                AssertEqual(0, abmouth.Npc.UnknownData2, "Abmouth NPC unknown data 2");
                Assert(!abmouth.Npc.UnknownData3.HasValue, "Abmouth NPC unknown data 3 absent");
                AssertEqual(30, abmouth.Level, "Abmouth level");
                AssertEqual(10324, abmouth.Health, "Abmouth health");
                AssertEqual(155962, (int)abmouth.MonsterData, "Abmouth monster data");
                AssertEqual(162, abmouth.MonsterScale, "Abmouth monster scale");
                AssertEqual("80000000000000008000000003010001000100010001000000020000", RawScfuFormatting.ToHex(abmouth.Unknown1), "Abmouth unknown1");
                AssertEqual(114, abmouth.RunSpeedBase, "Abmouth run speed");
                AssertEqual(0, abmouth.ActiveNanos.Length, "Abmouth active nanos");
                AssertEqual(5, abmouth.Textures.Length, "Abmouth textures");
                AssertEqual(0, abmouth.Meshes.Length, "Abmouth meshes");
                AssertEqual(0, abmouth.Flags2, "Abmouth flags2");
                AssertEqual(0, abmouth.Unknown2, "Abmouth unknown2");

                byte[] infectorPacket = FromHex(ReplacementInfectorPacketHex);
                RawSimpleCharFullUpdate infector = RawSimpleCharFullUpdateDecoder.DecodePacket(infectorPacket);
                AssertEqual(217, infectorPacket.Length, "Infector packet length");
                AssertEqual(201, infector.RawBody.Length, "Infector body length");
                AssertEqual(201, infector.BytesConsumed, "Infector bytes consumed");
                Assert(infector.DecodeFullyConsumed, "Infector complete decode");
                AssertEqual(0x79607AD0, infector.Identity.Instance, "Infector identity instance");
                AssertEqual(0x43A7038C, FloatBits(infector.Position.X), "Infector position X bits");
                AssertEqual(0x42933C64, FloatBits(infector.Position.Y), "Infector position Y bits");
                AssertEqual(0x42C617A4, FloatBits(infector.Position.Z), "Infector position Z bits");
                AssertEqual(0x3F374729, FloatBits(infector.Heading.Y), "Infector heading Y bits");
                AssertEqual(0x3F32BB6F, FloatBits(infector.Heading.W), "Infector heading W bits");
                AssertEqual(10, infector.Npc.UnknownData, "Infector NPC unknown data");
                AssertEqual("00000000000000008000000003010001000100010001000000020000", RawScfuFormatting.ToHex(infector.Unknown1), "Infector unknown1");
                AssertEqual(2, infector.Flags2, "Infector flags2");
                AssertEqual(0, infector.Unknown2, "Infector unknown2");
                Assert(infector.Unknown4.HasValue, "Infector unknown4 present");
                AssertEqual(0, infector.Unknown4.GetValueOrDefault(), "Infector unknown4");

                int abmouthActiveNanoMarker = FindBytes(
                    abmouthPacket,
                    new byte[] { 0x00, 0x00, 0x03, 0xF1 },
                    RawSimpleCharFullUpdateDecoder.N3BodyOffset);
                Assert(abmouthActiveNanoMarker > 0, "Abmouth run-speed alignment marker found");
                byte[] legacyRunSpeedPacket = InsertByte(
                    abmouthPacket,
                    abmouthActiveNanoMarker - 1,
                    0x00);
                RawSimpleCharFullUpdate legacyRunSpeed =
                    RawSimpleCharFullUpdateDecoder.DecodePacket(legacyRunSpeedPacket);
                Assert(legacyRunSpeed.DecodeFullyConsumed, "Legacy two-byte run speed fully decoded");
                AssertEqual(114, legacyRunSpeed.RunSpeedBase, "Legacy two-byte run speed value");
                Assert(
                    legacyRunSpeed.LegacyExtendedRunSpeedAlignment,
                    "Legacy two-byte run speed alignment recorded");

                byte[] playerOpaquePacket = ReplaceScfuTail(
                    abmouthPacket,
                    FromHex(
                        "00000FC4000000000000"
                        + "12950000008E425241570000000000"));
                RawSimpleCharFullUpdate playerOpaque =
                    RawSimpleCharFullUpdateDecoder.DecodePacket(playerOpaquePacket);
                Assert(playerOpaque.DecodeFullyConsumed, "Observed flags2 FC4 extension fully decoded");
                AssertEqual(15, playerOpaque.OpaqueExtension.Length, "Observed flags2 FC4 extension length");

                byte[] petBd3Packet = ReplaceScfuTail(
                    abmouthPacket,
                    FromHex(
                        "00000BD3000000"
                        + "3D0001D73E4D4557314D45573100000004791C100D00"));
                SetInt32BigEndian(petBd3Packet, 30, 0x0A2A4A43);
                RawSimpleCharFullUpdate petBd3 =
                    RawSimpleCharFullUpdateDecoder.DecodePacket(petBd3Packet);
                Assert(petBd3.DecodeFullyConsumed, "Observed pet flags2 BD3 extension fully decoded");
                AssertEqual(22, petBd3.OpaqueExtension.Length, "Observed pet flags2 BD3 extension length");

                byte[] pet7e2Packet = ReplaceScfuTail(
                    abmouthPacket,
                    FromHex("000007E200000004791C100D00"));
                SetInt32BigEndian(pet7e2Packet, 30, 0x0A2A4A43);
                RawSimpleCharFullUpdate pet7e2 =
                    RawSimpleCharFullUpdateDecoder.DecodePacket(pet7e2Packet);
                Assert(pet7e2.DecodeFullyConsumed, "Observed pet flags2 7E2 extension fully decoded");
                AssertEqual(6, pet7e2.OpaqueExtension.Length, "Observed pet flags2 7E2 extension length");

                byte[] terminalSpecialAttackPacket = ReplaceScfuTail(
                    abmouthPacket,
                    FromHex(
                        "000007E20003"
                        + "115D0003115E44425057444250570000"
                        + "000000"));
                RawSimpleCharFullUpdate terminalSpecialAttack =
                    RawSimpleCharFullUpdateDecoder.DecodePacket(terminalSpecialAttackPacket);
                Assert(
                    terminalSpecialAttack.DecodeFullyConsumed,
                    "Observed terminal special-attack slot omission fully decoded");
                Assert(
                    terminalSpecialAttack.TerminalSpecialAttackSlotOmitted,
                    "Observed terminal special-attack slot omission recorded");
                AssertEqual(
                    0,
                    terminalSpecialAttack.Unknown4.GetValueOrDefault(),
                    "Observed terminal special-attack Unknown4 preserved");

                RawSimpleCharFullUpdate characterTowerFlag =
                    RawSimpleCharFullUpdateDecoder.DecodePacket(
                        FromHex(CharacterTowerFlagPacketHex));
                Assert(
                    characterTowerFlag.DecodeFullyConsumed,
                    "CharacterTower flag without a wire byte fully decoded");
                AssertEqual(
                    "Shipping Manifest Terminal",
                    characterTowerFlag.Name,
                    "CharacterTower fixture name");
                AssertEqual(25, characterTowerFlag.Level, "CharacterTower fixture level");
                AssertEqual(724, characterTowerFlag.Health, "CharacterTower fixture health");
                AssertEqual(
                    279184,
                    (int)characterTowerFlag.MonsterData,
                    "CharacterTower fixture monster data");
                Assert(
                    !characterTowerFlag.TowerUnknown.HasValue,
                    "CharacterTower flag does not invent an absent byte");

                RawSimpleCharFullUpdate legacyVersion57Player =
                    RawSimpleCharFullUpdateDecoder.DecodePacket(
                        FromHex(LegacyVersion57PlayerPacketHex));
                Assert(
                    legacyVersion57Player.DecodeFullyConsumed,
                    "Legacy version-57 player layout fully decoded");
                AssertEqual(57, legacyVersion57Player.Version, "Legacy player version");
                AssertEqual("Mikedoc", legacyVersion57Player.Name, "Legacy player name");
                Assert(legacyVersion57Player.Player != null, "Legacy player metadata present");
                AssertEqual(
                    "Testing Org",
                    legacyVersion57Player.Player.OrgName,
                    "Legacy player int16 organization name");
                AssertEqual(10, legacyVersion57Player.Level, "Legacy player level");
                AssertEqual(1300, legacyVersion57Player.Health, "Legacy player health");
                AssertEqual(42, legacyVersion57Player.Unknown1.Length, "Legacy player Unknown1 length");

                RawSimpleCharFullUpdate multipleTerminalSlots =
                    RawSimpleCharFullUpdateDecoder.DecodePacket(
                        FromHex(MultipleTerminalSpecialAttackSlotsPacketHex));
                Assert(
                    multipleTerminalSlots.DecodeFullyConsumed,
                    "Multiple omitted terminal special-attack slots fully decoded");
                Assert(
                    multipleTerminalSlots.TerminalSpecialAttackSlotOmitted,
                    "Multiple omitted terminal special-attack slots recorded");
                AssertEqual(
                    4,
                    multipleTerminalSlots.SpecialAttacks.Length,
                    "Multiple omitted terminal special-attack declared count");
                Assert(
                    multipleTerminalSlots.SpecialAttacks[0] != null,
                    "Multiple omitted terminal special-attack first record present");
                AssertEqual(
                    "QKSI",
                    multipleTerminalSlots.SpecialAttacks[0].Name,
                    "Multiple omitted terminal special-attack name");
                Assert(
                    multipleTerminalSlots.SpecialAttacks[2] == null
                    && multipleTerminalSlots.SpecialAttacks[3] == null,
                    "Multiple omitted terminal special-attack records remain absent");
                AssertEqual(
                    0,
                    multipleTerminalSlots.Unknown4.GetValueOrDefault(),
                    "Multiple omitted terminal special-attack final flag preserved");

                RawSimpleCharFullUpdate declaredSlotBeforeOpaque =
                    RawSimpleCharFullUpdateDecoder.DecodePacket(
                        FromHex(DeclaredSlotBeforePlayerOpaqueExtensionPacketHex));
                Assert(
                    declaredSlotBeforeOpaque.DecodeFullyConsumed,
                    "Declared slot before player opaque extension fully decoded");
                Assert(
                    declaredSlotBeforeOpaque.TerminalSpecialAttackSlotOmitted,
                    "Declared slot before player opaque extension recorded");
                AssertEqual(
                    3,
                    declaredSlotBeforeOpaque.SpecialAttacks.Length,
                    "Player opaque fixture declared special-attack count");
                Assert(
                    declaredSlotBeforeOpaque.SpecialAttacks[0] != null
                    && declaredSlotBeforeOpaque.SpecialAttacks[1] != null
                    && declaredSlotBeforeOpaque.SpecialAttacks[2] == null,
                    "Player opaque fixture preserves two observed records");
                AssertEqual(
                    15,
                    declaredSlotBeforeOpaque.OpaqueExtension.Length,
                    "Player opaque fixture extension length");

                RawSimpleCharFullUpdate terminalOneByteUnknown6 =
                    RawSimpleCharFullUpdateDecoder.DecodePacket(
                        FromHex(TerminalOneByteSpecialAttackUnknown6PacketHex));
                Assert(
                    terminalOneByteUnknown6.DecodeFullyConsumed,
                    "Terminal one-byte special-attack Unknown6 fully decoded");
                AssertEqual(
                    3,
                    terminalOneByteUnknown6.SpecialAttacks.Length,
                    "Terminal one-byte special-attack declared count");
                Assert(
                    terminalOneByteUnknown6.SpecialAttacks[2] != null,
                    "Terminal one-byte special-attack final record present");
                AssertEqual(
                    0,
                    terminalOneByteUnknown6.SpecialAttacks[2].Unknown6,
                    "Terminal one-byte special-attack Unknown6 value");

                var truncated = new byte[infectorPacket.Length - 1];
                Buffer.BlockCopy(infectorPacket, 0, truncated, 0, truncated.Length);
                RawSimpleCharFullUpdate frameMismatchMessage;
                string frameMismatchError;
                Assert(
                    !RawSimpleCharFullUpdateDecoder.TryDecodePacket(
                        truncated,
                        out frameMismatchMessage,
                        out frameMismatchError),
                    "Frame length mismatch rejected");
                Assert(
                    frameMismatchError.IndexOf("frame length mismatch", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Frame length mismatch error");

                SetFrameLength(truncated);
                RawSimpleCharFullUpdate truncatedMessage;
                string truncatedError;
                Assert(
                    !RawSimpleCharFullUpdateDecoder.TryDecodePacket(truncated, out truncatedMessage, out truncatedError),
                    "Truncated packet rejected without escaping callback");
                Assert(truncatedMessage == null, "Truncated packet has no partial projection");
                Assert(truncatedError.IndexOf("ScfuUnknown4", StringComparison.Ordinal) >= 0, "Truncated packet field error");
                string failedRow = RawScfuAppearanceCsv.FormatRow(
                    new RawScfuCaptureMetadata(),
                    truncated,
                    null,
                    truncatedError);
                Assert(
                    failedRow.IndexOf(RawScfuFormatting.ToHex(truncated), StringComparison.Ordinal) >= 0,
                    "Truncated packet raw bytes preserved");

                byte[] badMarker = (byte[])abmouthPacket.Clone();
                int markerOffset = FindBytes(badMarker, new byte[] { 0x00, 0x00, 0x03, 0xF1 }, RawSimpleCharFullUpdateDecoder.N3BodyOffset);
                Assert(markerOffset >= 0, "Abmouth ActiveNanos marker found");
                badMarker[markerOffset + 3] = 0xF0;
                RawSimpleCharFullUpdate badMarkerMessage;
                string badMarkerError;
                Assert(
                    !RawSimpleCharFullUpdateDecoder.TryDecodePacket(badMarker, out badMarkerMessage, out badMarkerError),
                    "Bad X3F1 marker rejected");
                Assert(badMarkerError.IndexOf("ActiveNanos marker", StringComparison.Ordinal) >= 0, "Bad X3F1 field error");

                var extraTailPacket = new byte[abmouthPacket.Length + 1];
                Buffer.BlockCopy(abmouthPacket, 0, extraTailPacket, 0, abmouthPacket.Length);
                extraTailPacket[extraTailPacket.Length - 1] = 0xA5;
                SetFrameLength(extraTailPacket);
                RawSimpleCharFullUpdate extraTail = RawSimpleCharFullUpdateDecoder.DecodePacket(extraTailPacket);
                Assert(!extraTail.DecodeFullyConsumed, "Extra byte requires offline decode");
                AssertEqual(208, extraTail.BytesConsumed, "Extra-tail bytes consumed");
                AssertEqual(1, extraTail.UndecodedTail.Length, "Extra-tail length");
                AssertEqual(0xA5, extraTail.UndecodedTail[0], "Extra-tail byte");

                string completeRow = RawScfuAppearanceCsv.FormatRow(
                    new RawScfuCaptureMetadata(),
                    abmouthPacket,
                    abmouth,
                    string.Empty);
                AssertEqual(
                    ParseCsvLine(RawScfuAppearanceCsv.Header).Count,
                    ParseCsvLine(completeRow).Count,
                    "Shared SCFU CSV schema width");

                PacketSourceReport missingPacketLog = new PacketSourceReport("packets.hex.log", false);
                PacketSourceReport rawOnly = CreateSelfTestSource(
                    "raw-packets.csv",
                    abmouthPacket,
                    abmouthPacket.Length,
                    "raw_complete");
                CapturePacketSet rawOnlyResult = ReconcileSources(
                    missingPacketLog,
                    rawOnly,
                    SelfTestExpectations(1, 1));
                AssertEqual(0, rawOnlyResult.SourceFailures, "Raw-index-only fallback accepted");
                AssertEqual(1, rawOnlyResult.Packets.Count, "Raw-index-only SCFU count");
                AssertEqual(1, rawOnlyResult.RawFallbackRows, "Raw-index-only fallback count");

                PacketSourceReport badLengthOnly = CreateSelfTestSource(
                    "packets.hex.log",
                    abmouthPacket,
                    abmouthPacket.Length - 1,
                    "raw_complete");
                CapturePacketSet badLengthResult = ReconcileSources(
                    badLengthOnly,
                    new PacketSourceReport("raw-packets.csv", false),
                    SelfTestExpectations(1, 1));
                Assert(badLengthResult.SourceFailures > 0, "Sink-declared length mismatch rejected");

                byte[] badFramePacket = (byte[])abmouthPacket.Clone();
                int incorrectFrameLength = badFramePacket.Length - 1;
                badFramePacket[6] = (byte)(incorrectFrameLength >> 8);
                badFramePacket[7] = (byte)incorrectFrameLength;
                PacketSourceReport badFrameOnly = CreateSelfTestSource(
                    "packets.hex.log",
                    badFramePacket,
                    badFramePacket.Length,
                    "raw_complete");
                CapturePacketSet badFrameResult = ReconcileSources(
                    badFrameOnly,
                    new PacketSourceReport("raw-packets.csv", false),
                    SelfTestExpectations(1, 1));
                Assert(badFrameResult.SourceFailures > 0, "SCFU frame length mismatch rejected by source reconciliation");

                PacketSourceReport goodRawIndex = CreateSelfTestSource(
                    "raw-packets.csv",
                    abmouthPacket,
                    abmouthPacket.Length,
                    "raw_complete");
                CapturePacketSet recoveredResult = ReconcileSources(
                    badLengthOnly,
                    goodRawIndex,
                    SelfTestExpectations(1, 1));
                AssertEqual(0, recoveredResult.SourceFailures, "One good sink recovers one bad sink");
                AssertEqual(1, recoveredResult.Packets.Count, "Recovered SCFU count");

                CapturePacketSet recoveredFrameResult = ReconcileSources(
                    badFrameOnly,
                    goodRawIndex,
                    SelfTestExpectations(1, 1));
                AssertEqual(0, recoveredFrameResult.SourceFailures, "One good sink recovers a bad SCFU frame");

                PacketSourceReport conflictingPacketLog = CreateSelfTestSource(
                    "packets.hex.log",
                    abmouthPacket,
                    abmouthPacket.Length,
                    "raw_complete");
                PacketSourceReport conflictingRawIndex = CreateSelfTestSource(
                    "raw-packets.csv",
                    infectorPacket,
                    infectorPacket.Length,
                    "raw_complete");
                CapturePacketSet conflictResult = ReconcileSources(
                    conflictingPacketLog,
                    conflictingRawIndex,
                    SelfTestExpectations(1, 1));
                Assert(conflictResult.SourceFailures > 0, "Conflicting raw sinks rejected");

                CapturePacketSet emptyResult = ReconcileSources(
                    new PacketSourceReport("packets.hex.log", true),
                    new PacketSourceReport("raw-packets.csv", true),
                    SelfTestExpectations(1, 1));
                Assert(emptyResult.SourceFailures > 0, "Incomplete empty sources rejected");

                var boundedExpectations = new CaptureExpectations
                {
                    CaptureStartUtc = DateTime.Parse(
                        "2026-07-08T05:40:38.3778958Z",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind),
                    CaptureEndUtc = DateTime.Parse(
                        "2026-07-08T05:45:36.9142909Z",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind)
                };
                Assert(
                    IsInsideCaptureWindow(
                        "2026-07-08T05:45:36.9000000Z",
                        boundedExpectations),
                    "Packet within capture boundary retained");
                Assert(
                    !IsInsideCaptureWindow(
                        "2026-07-08T06:06:07.9373411Z",
                        boundedExpectations),
                    "Trailing packet after finalized legacy capture excluded");
                Console.WriteLine("SCFU decoder self-test PASS");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("SCFU decoder self-test FAIL: " + exception.Message);
                return 1;
            }
        }

        private static int FloatBits(float value)
        {
            return BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        }

        private static void SetFrameLength(byte[] packet)
        {
            if (packet == null || packet.Length < 8 || packet.Length > ushort.MaxValue)
            {
                throw new InvalidDataException("Self-test packet cannot encode its frame length.");
            }

            packet[6] = (byte)(packet.Length >> 8);
            packet[7] = (byte)packet.Length;
        }

        private static byte[] InsertByte(byte[] packet, int offset, byte value)
        {
            var result = new byte[packet.Length + 1];
            Buffer.BlockCopy(packet, 0, result, 0, offset);
            result[offset] = value;
            Buffer.BlockCopy(packet, offset, result, offset + 1, packet.Length - offset);
            SetFrameLength(result);
            return result;
        }

        private static byte[] ReplaceScfuTail(byte[] packet, byte[] replacement)
        {
            int prefixLength = packet.Length - 5;
            var result = new byte[prefixLength + replacement.Length];
            Buffer.BlockCopy(packet, 0, result, 0, prefixLength);
            Buffer.BlockCopy(replacement, 0, result, prefixLength, replacement.Length);
            SetFrameLength(result);
            return result;
        }

        private static void SetInt32BigEndian(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)(value >> 24);
            bytes[offset + 1] = (byte)(value >> 16);
            bytes[offset + 2] = (byte)(value >> 8);
            bytes[offset + 3] = (byte)value;
        }

        private static PacketSourceReport CreateSelfTestSource(
            string name,
            byte[] packet,
            int declaredLength,
            string preservationStatus)
        {
            var report = new PacketSourceReport(name, true)
            {
                RawRowCount = 1
            };
            AddSourcePacket(
                report,
                new RawScfuCaptureMetadata
                {
                    CapturedUtc = "2026-07-13T00:00:00.0000000Z",
                    Direction = "IN",
                    GlobalOrdinal = "1",
                    Sequence = "1"
                },
                packet,
                declaredLength,
                preservationStatus,
                string.Empty);
            return report;
        }

        private static CaptureExpectations SelfTestExpectations(int rawPackets, int scfuPackets)
        {
            return new CaptureExpectations
            {
                ExpectedRawPackets = rawPackets,
                ExpectedScfuPackets = scfuPackets,
                RecaptureRequired = false
            };
        }

        private static int FindBytes(byte[] haystack, byte[] needle, int start)
        {
            for (int i = start; i <= haystack.Length - needle.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void Assert(bool condition, string name)
        {
            if (!condition)
            {
                throw new InvalidDataException(name);
            }
        }

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidDataException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}: expected {1}, actual {2}",
                        name,
                        expected,
                        actual));
            }
        }

        private static string Csv(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }

        private sealed class CapturedPacket
        {
            internal RawScfuCaptureMetadata Metadata { get; set; }
            internal byte[] Packet { get; set; }
        }

        private sealed class CapturePacketSet
        {
            internal CapturePacketSet()
            {
                this.Packets = new List<CapturedPacket>();
                this.FailureMessages = new List<string>();
            }

            internal List<CapturedPacket> Packets { get; private set; }
            internal List<string> FailureMessages { get; private set; }
            internal int PacketLogRows { get; set; }
            internal int RawFallbackRows { get; set; }
            internal int SourceFailures { get; set; }
            internal int ResolvedRawPacketCount { get; set; }
            internal int PacketLogRowsOutsideCaptureWindow { get; set; }
            internal int RawIndexRowsOutsideCaptureWindow { get; set; }

            internal void AddFailure(string message)
            {
                this.SourceFailures++;
                this.FailureMessages.Add(message);
            }
        }

        private sealed class CaptureExpectations
        {
            internal int? ExpectedRawPackets { get; set; }
            internal int? ExpectedScfuPackets { get; set; }
            internal bool? RecaptureRequired { get; set; }
            internal DateTime? CaptureStartUtc { get; set; }
            internal DateTime? CaptureEndUtc { get; set; }
        }

        private sealed class PacketSourceReport
        {
            internal PacketSourceReport(string name, bool exists)
            {
                this.Name = name;
                this.Exists = exists;
                this.HeaderValid = true;
                this.Records = new List<SourcePacketRecord>();
                this.RecordByEvent = new Dictionary<string, SourcePacketRecord>(StringComparer.Ordinal);
                this.Errors = new List<string>();
            }

            internal string Name { get; private set; }
            internal bool Exists { get; private set; }
            internal bool HeaderValid { get; set; }
            internal int RawRowCount { get; set; }
            internal int ValidRowCount { get; set; }
            internal int InvalidRowCount { get; set; }
            internal int ValidScfuRowCount { get; set; }
            internal int RowsOutsideCaptureWindow { get; set; }
            internal List<SourcePacketRecord> Records { get; private set; }
            internal Dictionary<string, SourcePacketRecord> RecordByEvent { get; private set; }
            internal List<string> Errors { get; private set; }
        }

        private sealed class SourcePacketRecord
        {
            internal string EventKey { get; set; }
            internal string SourceName { get; set; }
            internal int DeclaredLength { get; set; }
            internal int ReconcileOrder { get; set; }
            internal bool SelectedFromRawFallback { get; set; }
            internal RawScfuCaptureMetadata Metadata { get; set; }
            internal byte[] Packet { get; set; }
        }

        private sealed class ReconciledEvent
        {
            internal SourcePacketRecord PacketLog { get; set; }
            internal SourcePacketRecord RawIndex { get; set; }
        }
    }
}
