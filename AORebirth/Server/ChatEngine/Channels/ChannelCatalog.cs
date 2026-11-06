namespace ChatEngine.Channels
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    using AORebirth.Core.GameData;
    using AORebirth.Stats;

    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>One static channel from GameData ChatChannels.json.</summary>
    public sealed class ChannelDefinition
    {
        public ChannelDefinition(uint id, ChannelType type, string name, ChannelFlags flags, IReadOnlyList<ChannelCondition> conditions)
        {
            this.Id = id;
            this.Type = type;
            this.Name = name;
            this.Flags = flags;
            this.Conditions = conditions;
        }

        public uint Id { get; }

        public ChannelType Type { get; }

        public string Name { get; }

        public ChannelFlags Flags { get; }

        /// <summary>All must pass for a character to join; empty means everyone joins.</summary>
        public IReadOnlyList<ChannelCondition> Conditions { get; }
    }

    /// <summary>
    /// The static chat channels, from GameData <c>ChatChannels.json</c>. A character joins every channel whose
    /// conditions all pass, at chat login. Organization, team and raid channels are created per group at runtime and
    /// are rejected here.
    /// <code>
    /// { "Channels": [ { "Id": 1, "Type": "Shopping", "Name": "Shopping 1-50", "Flags": [ "CantIgnore" ],
    ///                   "Conditions": [ { "Kind": "Stat", "Stat": "Level", "Min": 1, "Max": 50 } ] } ] }
    /// </code>
    /// Id + Type is the wire identity and must be unique. Condition kinds:
    /// <list type="bullet">
    /// <item><c>Stat</c>: <c>Stat</c> (a stat name, or its number as a string), optional inclusive <c>Min</c>/<c>Max</c>, optional <c>Values</c>.</item>
    /// <item><c>Side</c>: <c>Sides</c> (Neutral, Clan, Omni, Monster, Advisor, Guardian, Gm).</item>
    /// <item><c>GmLevel</c>: account GM level, optional inclusive <c>Min</c>/<c>Max</c>.</item>
    /// <item><c>AnyOf</c>: at least one of the nested <c>Conditions</c> passes.</item>
    /// <item><c>Not</c>: none of the nested <c>Conditions</c> pass.</item>
    /// </list>
    /// </summary>
    public sealed class ChannelCatalog
    {
        private const int MaxConditionDepth = 8;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };

        /// <summary>Channel types the server creates per organization, team or raid.</summary>
        private static readonly HashSet<ChannelType> RuntimeOnlyTypes = new HashSet<ChannelType>
        {
            ChannelType.Organization, ChannelType.Team, ChannelType.Raid,
        };

        private ChannelCatalog(IReadOnlyList<ChannelDefinition> channels)
        {
            this.Channels = channels;
        }

        public IReadOnlyList<ChannelDefinition> Channels { get; }

        /// <summary>ChatChannels.json of this process's GameData (the override folder's copy when it has one).</summary>
        public static ChannelCatalog LoadRuntime()
        {
            return Load(GameDataPaths.ResolveFile(GameDataPaths.ResolveRuntimeRoot(), GameDataPaths.ChatChannelsFileName));
        }

        public static ChannelCatalog Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                throw new FileNotFoundException("GameData " + GameDataPaths.ChatChannelsFileName + " is required.", path);
            }

            FileModel file;
            try
            {
                file = JsonSerializer.Deserialize<FileModel>(File.ReadAllText(path), JsonOptions);
            }
            catch (JsonException e)
            {
                throw new InvalidDataException(GameDataPaths.ChatChannelsFileName + " is invalid: " + e.Message, e);
            }

            if (file?.Channels == null || file.Channels.Count == 0)
            {
                throw new InvalidDataException(GameDataPaths.ChatChannelsFileName + " lists no channels.");
            }

            var identities = new HashSet<(ChannelType, uint)>();
            var channels = new List<ChannelDefinition>(file.Channels.Count);
            for (int i = 0; i < file.Channels.Count; i++)
            {
                ChannelDefinition channel = ParseChannel(file.Channels[i], "channel " + i.ToString(CultureInfo.InvariantCulture));
                if (!identities.Add((channel.Type, channel.Id)))
                {
                    throw Invalid("channel " + i.ToString(CultureInfo.InvariantCulture), "repeats Type " + channel.Type + " Id " + channel.Id + ".");
                }

                channels.Add(channel);
            }

            return new ChannelCatalog(channels);
        }

        private static ChannelDefinition ParseChannel(ChannelModel model, string where)
        {
            if (model == null)
            {
                throw Invalid(where, "is empty.");
            }

            if (model.Id == null)
            {
                throw Invalid(where, "needs an Id.");
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                throw Invalid(where, "needs a Name.");
            }

            where += " (" + model.Name + ")";
            if (!TryParseName(model.Type, out ChannelType type))
            {
                throw Invalid(where, "has unknown Type '" + model.Type + "'.");
            }

            if (RuntimeOnlyTypes.Contains(type))
            {
                throw Invalid(where, "has Type " + type + ", which the server creates per group.");
            }

            ChannelFlags flags = ChannelFlags.None;
            foreach (string name in model.Flags ?? new List<string>())
            {
                if (!TryParseName(name, out ChannelFlags flag))
                {
                    throw Invalid(where, "has unknown flag '" + name + "'.");
                }

                flags |= flag;
            }

            return new ChannelDefinition(model.Id.Value, type, model.Name.Trim(), flags, ParseConditions(model.Conditions, where, 0));
        }

        private static IReadOnlyList<ChannelCondition> ParseConditions(List<ConditionModel> models, string where, int depth)
        {
            if (models == null || models.Count == 0)
            {
                return Array.Empty<ChannelCondition>();
            }

            if (depth >= MaxConditionDepth)
            {
                throw Invalid(where, "nests conditions deeper than " + MaxConditionDepth + ".");
            }

            var conditions = new ChannelCondition[models.Count];
            for (int i = 0; i < models.Count; i++)
            {
                conditions[i] = ParseCondition(models[i], where + " condition " + i.ToString(CultureInfo.InvariantCulture), depth);
            }

            return conditions;
        }

        private static ChannelCondition ParseCondition(ConditionModel model, string where, int depth)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Kind))
            {
                throw Invalid(where, "needs a Kind.");
            }

            switch (model.Kind.Trim().ToLowerInvariant())
            {
                case "stat":
                    Only(model, where, stat: true, range: true, values: true);
                    int statId = ResolveStat(model.Stat, where);
                    (int min, int max) = Range(model, where);
                    HashSet<int> values = model.Values == null || model.Values.Count == 0 ? null : new HashSet<int>(model.Values);
                    return new StatChannelCondition(statId, min, max, values);

                case "side":
                    Only(model, where, sides: true);
                    if (model.Sides == null || model.Sides.Count == 0)
                    {
                        throw Invalid(where, "needs Sides.");
                    }

                    var sides = new HashSet<int>();
                    foreach (string name in model.Sides)
                    {
                        if (!TryParseName(name, out Side side))
                        {
                            throw Invalid(where, "has unknown side '" + name + "'.");
                        }

                        sides.Add((int)side);
                    }

                    return new SideChannelCondition(sides);

                case "gmlevel":
                    Only(model, where, range: true);
                    (int gmMin, int gmMax) = Range(model, where);
                    return new GmLevelChannelCondition(gmMin, gmMax);

                case "anyof":
                case "not":
                    Only(model, where, nested: true);
                    if (model.Conditions == null || model.Conditions.Count == 0)
                    {
                        throw Invalid(where, "needs nested Conditions.");
                    }

                    IReadOnlyList<ChannelCondition> nested = ParseConditions(model.Conditions, where, depth + 1);
                    return model.Kind.Trim().Equals("not", StringComparison.OrdinalIgnoreCase)
                        ? new NotChannelCondition(nested)
                        : (ChannelCondition)new AnyOfChannelCondition(nested);

                default:
                    throw Invalid(where, "has unknown Kind '" + model.Kind + "'.");
            }
        }

        /// <summary>Rejects fields that the condition's kind ignores, so a misplaced field is not silently dropped.</summary>
        private static void Only(
            ConditionModel model, string where, bool stat = false, bool range = false, bool values = false, bool sides = false, bool nested = false)
        {
            if ((!stat && model.Stat != null) || (!range && (model.Min != null || model.Max != null))
                || (!values && model.Values != null) || (!sides && model.Sides != null) || (!nested && model.Conditions != null))
            {
                throw Invalid(where, "has fields that Kind " + model.Kind + " does not use.");
            }
        }

        private static (int Min, int Max) Range(ConditionModel model, string where)
        {
            int min = model.Min ?? int.MinValue;
            int max = model.Max ?? int.MaxValue;
            if (min > max)
            {
                throw Invalid(where, "has Min above Max.");
            }

            return (min, max);
        }

        private static int ResolveStat(string stat, string where)
        {
            if (string.IsNullOrWhiteSpace(stat))
            {
                throw Invalid(where, "needs a Stat.");
            }

            if (int.TryParse(stat, NumberStyles.None, CultureInfo.InvariantCulture, out int number))
            {
                return number;
            }

            try
            {
                return StatNamesDefaults.GetStatNumber(stat.Trim());
            }
            catch (Exception)
            {
                throw Invalid(where, "has unknown Stat '" + stat + "'.");
            }
        }

        /// <summary>A defined enum member by name only: a number such as "7" is not accepted as a name.</summary>
        private static bool TryParseName<T>(string name, out T value)
            where T : struct, Enum
        {
            value = default;
            return !string.IsNullOrWhiteSpace(name)
                && !char.IsDigit(name.Trim()[0])
                && Enum.TryParse(name.Trim(), true, out value)
                && Enum.IsDefined(typeof(T), value);
        }

        private static InvalidDataException Invalid(string where, string problem)
        {
            return new InvalidDataException(GameDataPaths.ChatChannelsFileName + " " + where + " " + problem);
        }

        private sealed class FileModel
        {
            public List<ChannelModel> Channels { get; set; }
        }

        private sealed class ChannelModel
        {
            public uint? Id { get; set; }

            public string Type { get; set; }

            public string Name { get; set; }

            public List<string> Flags { get; set; }

            public List<ConditionModel> Conditions { get; set; }
        }

        private sealed class ConditionModel
        {
            public string Kind { get; set; }

            public string Stat { get; set; }

            public int? Min { get; set; }

            public int? Max { get; set; }

            public List<int> Values { get; set; }

            public List<string> Sides { get; set; }

            public List<ConditionModel> Conditions { get; set; }
        }
    }
}
