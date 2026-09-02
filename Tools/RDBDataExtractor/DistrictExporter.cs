namespace AORebirth.Tools.RDBDataExtractor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using AODB;
    using AODB.Common.RDBObjects;
    using AODB.Common.Structs;
    using AORebirth.Core.GameData;

    internal sealed class DistrictExporter
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private readonly RdbController controller;
        private readonly string outputDirectory;

        internal DistrictExporter(RdbController controller, string outputDirectory)
        {
            if (controller == null)
            {
                throw new ArgumentNullException("controller");
            }

            this.controller = controller;
            this.outputDirectory = outputDirectory;
        }

        internal bool HasDistrictRecordType()
        {
            return this.controller.RecordTypeToId.ContainsKey(
                (int)ResourceTypeId.PlayfieldDistrictInfo);
        }

        internal IEnumerable<int> EnumerateDistrictIds()
        {
            if (!this.HasDistrictRecordType())
            {
                return Enumerable.Empty<int>();
            }

            return this.controller
                .RecordTypeToId[(int)ResourceTypeId.PlayfieldDistrictInfo]
                .Keys
                .OrderBy(id => id);
        }

        internal bool TryHasDistrictRecord(int playfieldId)
        {
            if (!this.HasDistrictRecordType())
            {
                return false;
            }

            return this.controller
                .RecordTypeToId[(int)ResourceTypeId.PlayfieldDistrictInfo]
                .ContainsKey(playfieldId);
        }

        /// <summary>
        /// Writes Districts.json and/or Spawns.json. Existing files are skipped unless
        /// <paramref name="overwrite"/> is true. Returns written and skipped file counts.
        /// </summary>
        internal ExportFileCounts Export(int playfieldId, bool overwrite)
        {
            string folder = Path.Combine(
                this.outputDirectory,
                playfieldId.ToString());
            string districtsPath = Path.Combine(folder, GameDataPaths.DistrictsFileName);
            string spawnsPath = Path.Combine(folder, GameDataPaths.SpawnsFileName);

            bool writeDistricts = overwrite || !File.Exists(districtsPath);
            bool writeSpawns = overwrite || !File.Exists(spawnsPath);
            if (!writeDistricts && !writeSpawns)
            {
                return new ExportFileCounts(0, 2);
            }

            PlayfieldDistrictInfo info = this.controller.Get<PlayfieldDistrictInfo>(
                ResourceTypeId.PlayfieldDistrictInfo,
                playfieldId);
            if (info == null)
            {
                throw new InvalidOperationException(
                    "PlayfieldDistrictInfo record "
                    + playfieldId
                    + " was not found.");
            }

            Directory.CreateDirectory(folder);

            int written = 0;
            int skipped = 0;
            if (writeDistricts)
            {
                PlayfieldDistrictsData districts = MapDistricts(info);
                WriteJson(districtsPath, districts);
                written++;
            }
            else
            {
                skipped++;
            }

            if (writeSpawns)
            {
                PlayfieldSpawnsData spawns = MapSpawns(info);
                WriteJson(spawnsPath, spawns);
                written++;
            }
            else
            {
                skipped++;
            }

            return new ExportFileCounts(written, skipped);
        }

        private static PlayfieldDistrictsData MapDistricts(PlayfieldDistrictInfo info)
        {
            List<PlayfieldDistrictEntry> districts = new List<PlayfieldDistrictEntry>();
            if (info.Districts != null)
            {
                for (int index = 0; index < info.Districts.Count; index++)
                {
                    DistrictData district = info.Districts[index];
                    districts.Add(
                        new PlayfieldDistrictEntry
                        {
                            DistrictIndex = index,
                            Name = district.Name,
                            Centre = ToPosition(district.Centre),
                            Stats = CopyUshorts(district.Stats),
                            NpcMinLevel = district.NpcMinLevel,
                            NpcMaxLevel = district.NpcMaxLevel,
                            LandControlMinLevel = district.LandControlMinLevel,
                            LandControlMaxLevel = district.LandControlMaxLevel,
                            RespawnChance = district.RespawnChance,
                            RespawnTime = district.RespawnTime,
                            FightMode = district.FightMode,
                            SpawnInfos = MapSpawnInfos(district.SpawnInfos),
                            MusicPairs = MapMusicPairs(district.MusicPairs),
                            SpawnPoints = MapDistrictSpawnPoints(district.SpawnPoints),
                        });
                }
            }

            return new PlayfieldDistrictsData
            {
                SchemaVersion = PlayfieldDistrictsData.SupportedSchemaVersion,
                RecordType = info.RecordType,
                RecordId = info.RecordId,
                RecordVersion = info.RecordVersion,
                FormatVersion = info.FormatVersion,
                ZoneCount = info.ZoneCount,
                ZoneToDistrictMap = info.ZoneToDistrictMap,
                Districts = districts.ToArray(),
            };
        }

        private static PlayfieldSpawnsData MapSpawns(PlayfieldDistrictInfo info)
        {
            List<PlayfieldSpawnEntry> spawns = new List<PlayfieldSpawnEntry>();
            if (info.Districts != null)
            {
                for (int districtIndex = 0; districtIndex < info.Districts.Count; districtIndex++)
                {
                    DistrictData district = info.Districts[districtIndex];
                    if (district.HashSpawnPoints == null)
                    {
                        continue;
                    }

                    foreach (HashSpawnPoint spawn in district.HashSpawnPoints)
                    {
                        spawns.Add(MapSpawn(districtIndex, spawn));
                    }
                }
            }

            return new PlayfieldSpawnsData
            {
                SchemaVersion = PlayfieldSpawnsData.SupportedSchemaVersion,
                RecordType = (int)ResourceTypeId.PlayfieldDistrictInfo,
                PlayfieldId = info.RecordId,
                Spawns = spawns.ToArray(),
            };
        }

        private static PlayfieldSpawnEntry MapSpawn(int districtIndex, HashSpawnPoint spawn)
        {
            return new PlayfieldSpawnEntry
            {
                DistrictIndex = districtIndex,
                Hash = spawn.Hash,
                HashText = spawn.HashText,
                ManifestHash = spawn.ManifestHash,
                MinLevel = spawn.MinLevel,
                MaxLevel = spawn.MaxLevel,
                RespawnChance = spawn.RespawnChance,
                Flags = spawn.Flags,
                RespawnTime = spawn.RespawnTime,
                Version7Extra = spawn.Version7Extra,
                HasOptionalFlagBlock = spawn.HasOptionalFlagBlock,
                NativeFlags = spawn.NativeFlags,
                MoreFlags = spawn.MoreFlags,
                AssistanceRadius = spawn.AssistanceRadius,
                UnknownOptionalU8 = spawn.UnknownOptionalU8,
                Angle = spawn.Angle,
                AngleW = spawn.AngleW,
                Position = ToPosition(spawn.Position),
                Radius = spawn.Radius,
                AdditionalPoints = MapAdditionalPoints(spawn.AdditionalPoints),
                Extensions = MapExtensions(spawn.Extensions),
            };
        }

        private static PlayfieldDistrictSpawnInfo[] MapSpawnInfos(
            IList<DistrictSpawnInfo> source)
        {
            if (source == null || source.Count == 0)
            {
                return new PlayfieldDistrictSpawnInfo[0];
            }

            PlayfieldDistrictSpawnInfo[] mapped = new PlayfieldDistrictSpawnInfo[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                DistrictSpawnInfo item = source[index];
                mapped[index] = new PlayfieldDistrictSpawnInfo
                {
                    Hash = item.Hash,
                    HashText = item.HashText,
                    Count = item.Count,
                };
            }

            return mapped;
        }

        private static PlayfieldDistrictMusicPair[] MapMusicPairs(
            IList<DistrictMusicPair> source)
        {
            if (source == null || source.Count == 0)
            {
                return new PlayfieldDistrictMusicPair[0];
            }

            PlayfieldDistrictMusicPair[] mapped = new PlayfieldDistrictMusicPair[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                DistrictMusicPair item = source[index];
                mapped[index] = new PlayfieldDistrictMusicPair
                {
                    Id = item.Id,
                    Value = item.Value,
                };
            }

            return mapped;
        }

        private static PlayfieldDistrictSpawnPoint[] MapDistrictSpawnPoints(
            IList<DistrictSpawnPoint> source)
        {
            if (source == null || source.Count == 0)
            {
                return new PlayfieldDistrictSpawnPoint[0];
            }

            PlayfieldDistrictSpawnPoint[] mapped = new PlayfieldDistrictSpawnPoint[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                DistrictSpawnPoint item = source[index];
                mapped[index] = new PlayfieldDistrictSpawnPoint
                {
                    Position = ToPosition(item.Position),
                    Radius = item.Radius,
                };
            }

            return mapped;
        }

        private static PlayfieldRotationSpawnPoint[] MapAdditionalPoints(
            IList<RotationSpawnPoint> source)
        {
            if (source == null || source.Count == 0)
            {
                return new PlayfieldRotationSpawnPoint[0];
            }

            PlayfieldRotationSpawnPoint[] mapped = new PlayfieldRotationSpawnPoint[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                RotationSpawnPoint item = source[index];
                mapped[index] = new PlayfieldRotationSpawnPoint
                {
                    Angle = item.Angle,
                    AngleW = item.AngleW,
                    Position = ToPosition(item.Position),
                    Radius = item.Radius,
                };
            }

            return mapped;
        }

        private static PlayfieldHashSpawnExtensionBlock MapExtensions(
            HashSpawnExtensionBlock source)
        {
            if (source == null)
            {
                return null;
            }

            PlayfieldHashSpawnExtensionEvent[] events = new PlayfieldHashSpawnExtensionEvent[0];
            if (source.Events != null && source.Events.Count > 0)
            {
                events = new PlayfieldHashSpawnExtensionEvent[source.Events.Count];
                for (int index = 0; index < source.Events.Count; index++)
                {
                    HashSpawnExtensionEvent item = source.Events[index];
                    events[index] = new PlayfieldHashSpawnExtensionEvent
                    {
                        Unknown1 = item.Unknown1,
                        Unknown2 = item.Unknown2,
                        Name = item.Name,
                    };
                }
            }

            return new PlayfieldHashSpawnExtensionBlock
            {
                Field0 = source.Field0,
                Field1 = source.Field1,
                Field2 = source.Field2,
                Count3F1 = source.Count3F1,
                Events = events,
                TrailingUnknown = source.TrailingUnknown,
                OpaqueTail = source.OpaqueTail,
            };
        }

        private static float[] ToPosition(Vector3 position)
        {
            return new float[]
            {
                position.X,
                position.Y,
                position.Z,
            };
        }

        private static ushort[] CopyUshorts(ushort[] source)
        {
            if (source == null || source.Length == 0)
            {
                return new ushort[0];
            }

            ushort[] copy = new ushort[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private static void WriteJson(string path, object value)
        {
            string json = JsonSerializer.Serialize(value, JsonOptions);
            File.WriteAllText(path, json + Environment.NewLine);
        }
    }
}
