namespace AOSharpCaptureAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Serialization;

    internal static class Program
    {
        private const string HexMarker = " hex=";

        private static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Usage: AOSharpCaptureAnalyzer <capture-folder> [capture-folder ...]");
                return 2;
            }

            var resolver = new SerializerResolverBuilder<N3Message>().Build();
            var serializer = resolver.GetSerializer(typeof(SimpleCharFullUpdateMessage));
            int failures = 0;
            foreach (string captureFolder in args)
            {
                failures += ExportCapture(captureFolder, resolver, serializer);
            }

            return failures == 0 ? 0 : 1;
        }

        private static int ExportCapture(
            string captureFolder,
            SerializerResolver resolver,
            ISerializer serializer)
        {
            string packetPath = Path.Combine(captureFolder, "packets.hex.log");
            if (!File.Exists(packetPath))
            {
                Console.Error.WriteLine("Missing packet log: " + packetPath);
                return 1;
            }

            string outputPath = Path.Combine(captureFolder, "scfu-appearance.csv");
            int rows = 0;
            int failures = 0;
            using (var output = new System.IO.StreamWriter(outputPath, false, new UTF8Encoding(false)))
            {
                output.WriteLine("CapturedUtc,Direction,Sequence,Identity,Name,PositionX,PositionY,PositionZ,HeadingX,HeadingY,HeadingZ,HeadingW,Level,Health,HealthDamage,RunSpeed,NpcFamily,NpcLosHeight,CharacterFlags,AccountFlags,Expansions,VisualFlags,VisibleTitle,ScfuFlags,ScfuFlags2,Owner,AppearanceValue,Side,Fatness,Breed,Gender,Race,MonsterData,MonsterScale,HeadMesh,ScfuUnknown1Hex,ScfuUnknown2,ScfuUnknown3,ScfuUnknown4,Textures,Meshes,Waypoints,TextureOverrides");
                foreach (string line in ReadSharedLines(packetPath))
                {
                    if (line.IndexOf("n3=SimpleCharFullUpdate", StringComparison.Ordinal) < 0)
                    {
                        continue;
                    }

                    int markerIndex = line.IndexOf(HexMarker, StringComparison.Ordinal);
                    if (markerIndex < 0)
                    {
                        failures++;
                        continue;
                    }

                    try
                    {
                        byte[] packet = FromHex(line.Substring(markerIndex + HexMarker.Length));
                        if (packet.Length <= 16)
                        {
                            failures++;
                            continue;
                        }

                        SimpleCharFullUpdateMessage message;
                        using (var memory = new MemoryStream(packet, 16, packet.Length - 16, false))
                        using (var reader = new SmokeLounge.AOtomation.Messaging.Serialization.StreamReader(memory))
                        {
                            message = (SimpleCharFullUpdateMessage)serializer.Deserialize(
                                reader,
                                new SerializationContext(resolver));
                        }

                        string[] prefix = line.Substring(0, markerIndex).Split(' ');
                        output.WriteLine(
                            string.Join(
                                ",",
                                Csv(prefix.Length > 0 ? prefix[0] : string.Empty),
                                Csv(prefix.Length > 1 ? prefix[1] : string.Empty),
                                Csv(prefix.Length > 2 ? prefix[2] : string.Empty),
                                Csv(message.Identity.ToString()),
                                Csv(message.Name),
                                Csv(message.Position.X.ToString("R", CultureInfo.InvariantCulture)),
                                Csv(message.Position.Y.ToString("R", CultureInfo.InvariantCulture)),
                                Csv(message.Position.Z.ToString("R", CultureInfo.InvariantCulture)),
                                Csv(message.Heading.X.ToString("R", CultureInfo.InvariantCulture)),
                                Csv(message.Heading.Y.ToString("R", CultureInfo.InvariantCulture)),
                                Csv(message.Heading.Z.ToString("R", CultureInfo.InvariantCulture)),
                                Csv(message.Heading.W.ToString("R", CultureInfo.InvariantCulture)),
                                Csv(message.Level.ToString(CultureInfo.InvariantCulture)),
                                Csv(message.Health.ToString(CultureInfo.InvariantCulture)),
                                Csv(message.HealthDamage.ToString(CultureInfo.InvariantCulture)),
                                Csv(message.RunSpeedBase.ToString(CultureInfo.InvariantCulture)),
                                Csv(FormatNpcFamily(message.CharacterInfo)),
                                Csv(FormatNpcLosHeight(message.CharacterInfo)),
                                Csv(((int)message.CharacterFlags).ToString(CultureInfo.InvariantCulture)),
                                Csv(message.AccountFlags.ToString(CultureInfo.InvariantCulture)),
                                Csv(message.Expansions.ToString(CultureInfo.InvariantCulture)),
                                Csv(message.VisualFlags.ToString(CultureInfo.InvariantCulture)),
                                Csv(message.VisibleTitle.ToString(CultureInfo.InvariantCulture)),
                                Csv(message.Flags.ToString()),
                                Csv(message.Flags2.ToString()),
                                Csv(message.Owner.HasValue ? message.Owner.Value.ToString() : string.Empty),
                                Csv(message.Appearance == null ? string.Empty : message.Appearance.Value.ToString(CultureInfo.InvariantCulture)),
                                Csv(message.Appearance == null ? string.Empty : message.Appearance.Side.ToString()),
                                Csv(message.Appearance == null ? string.Empty : message.Appearance.Fatness.ToString()),
                                Csv(message.Appearance == null ? string.Empty : message.Appearance.Breed.ToString()),
                                Csv(message.Appearance == null ? string.Empty : message.Appearance.Gender.ToString()),
                                Csv(message.Appearance == null ? string.Empty : message.Appearance.Race.ToString(CultureInfo.InvariantCulture)),
                                Csv(message.MonsterData.ToString(CultureInfo.InvariantCulture)),
                                Csv(message.MonsterScale.ToString(CultureInfo.InvariantCulture)),
                                Csv(message.HeadMesh.HasValue ? message.HeadMesh.Value.ToString(CultureInfo.InvariantCulture) : string.Empty),
                                Csv(ToHex(message.ScfuUnk1)),
                                Csv(message.ScfuUnk2.ToString(CultureInfo.InvariantCulture)),
                                Csv(message.ScfuUnk3.ToString("R", CultureInfo.InvariantCulture)),
                                Csv(message.ScfuUnk4.ToString(CultureInfo.InvariantCulture)),
                                Csv(FormatTextures(message.Textures)),
                                Csv(FormatMeshes(message.Meshes)),
                                Csv(FormatWaypoints(message.Waypoints)),
                                Csv(FormatTextureOverrides(message.TextureOverrides))));
                        rows++;
                    }
                    catch (Exception exception)
                    {
                        failures++;
                        Console.Error.WriteLine(Path.GetFileName(captureFolder) + ": " + exception.Message);
                    }
                }
            }

            Console.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}: SCFU appearance rows={1} failures={2}",
                    Path.GetFileName(captureFolder),
                    rows,
                    failures));
            return failures;
        }

        private static string FormatTextures(IEnumerable<Texture> textures)
        {
            return string.Join(
                "|",
                (textures ?? Enumerable.Empty<Texture>()).Select(
                    value => string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}:{1}:{2}",
                        value.Place,
                        value.Id,
                        value.Unknown)));
        }

        private static string FormatMeshes(IEnumerable<Mesh> meshes)
        {
            return string.Join(
                "|",
                (meshes ?? Enumerable.Empty<Mesh>()).Select(
                    value => string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}:{1}:{2}:{3}",
                        value.Position,
                        value.Id,
                        value.OverrideTextureId,
                        value.Layer)));
        }

        private static string FormatTextureOverrides(
            IEnumerable<SimpleCharInfo.TextureOverride> overrides)
        {
            return string.Join(
                "|",
                (overrides ?? Enumerable.Empty<SimpleCharInfo.TextureOverride>()).Select(
                    value => string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}:{1}:{2}:{3}",
                        value.Name,
                        value.TextureId,
                        value.Unknown1,
                        value.Unknown2)));
        }

        private static string FormatWaypoints(IEnumerable<AOSharp.Common.GameData.Vector3> waypoints)
        {
            return string.Join(
                "|",
                (waypoints ?? Enumerable.Empty<AOSharp.Common.GameData.Vector3>()).Select(
                    value => string.Format(
                        CultureInfo.InvariantCulture,
                        "{0:R}:{1:R}:{2:R}",
                        value.X,
                        value.Y,
                        value.Z)));
        }

        private static string FormatNpcFamily(SimpleCharInfo characterInfo)
        {
            var npc = characterInfo as SimpleCharInfo.NPCInfo;
            return npc == null ? string.Empty : npc.Family.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatNpcLosHeight(SimpleCharInfo characterInfo)
        {
            var npc = characterInfo as SimpleCharInfo.NPCInfo;
            return npc == null ? string.Empty : npc.LosHeight.ToString(CultureInfo.InvariantCulture);
        }

        private static byte[] FromHex(string hex)
        {
            string value = hex.Trim();
            if ((value.Length & 1) != 0)
            {
                throw new FormatException("Packet hex length is odd.");
            }

            var result = new byte[value.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = byte.Parse(value.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return result;
        }

        private static string ToHex(IEnumerable<byte> bytes)
        {
            if (bytes == null)
            {
                return string.Empty;
            }

            return string.Concat(bytes.Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
        }

        private static IEnumerable<string> ReadSharedLines(string path)
        {
            using (var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite))
            using (var reader = new System.IO.StreamReader(stream, Encoding.UTF8, true))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    yield return line;
                }
            }
        }

        private static string Csv(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }
    }
}
