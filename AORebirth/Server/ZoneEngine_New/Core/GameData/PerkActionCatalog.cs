namespace ZoneEngine_New.Core.GameData
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;

    using AORebirth.Core.GameData;

    using Utility;

    /// <summary>
    /// Perk action templates from PerkActions.json, keyed by the 4-char action hash packed big-endian
    /// ("CNRE" = 0x434E5245, the AddPerkAction / UsePerk Parameter2). An action is one template or QL
    /// low/high tiers picked by skill: skill x SkillModifier, 1000 per tier, QL interpolated inside the tier.
    /// </summary>
    public sealed class PerkActionCatalog
    {
        public const int SkillPerTier = 1000;

        static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        static readonly Lazy<PerkActionCatalog> DefaultCatalog = new(() =>
            LoadOrEmpty(GameDataPaths.ResolveRuntimeRoot()));

        readonly Dictionary<int, PerkActionDefinition> _actions;

        PerkActionCatalog(Dictionary<int, PerkActionDefinition> actions) => _actions = actions;

        public static PerkActionCatalog Empty { get; } = new(new Dictionary<int, PerkActionDefinition>());

        /// <summary>Catalog from the runtime GameData folder. Empty, with an error logged, when it cannot be loaded.</summary>
        public static PerkActionCatalog Default => DefaultCatalog.Value;

        public bool TryGet(int hash, out PerkActionDefinition definition)
            => _actions.TryGetValue(hash, out definition!);

        /// <summary>"CNRE" -> 0x434E5245.</summary>
        public static int PackHash(string hash)
        {
            if (hash is not { Length: 4 })
                throw new ArgumentException("Perk action hash must be 4 characters.", nameof(hash));
            return (hash[0] << 24) | (hash[1] << 16) | (hash[2] << 8) | hash[3];
        }

        public static PerkActionCatalog Load(string root)
        {
            string path = Path.Combine(root, GameDataPaths.PerkActionsFileName);
            ActionsFile file = JsonSerializer.Deserialize<ActionsFile>(File.ReadAllText(path), JsonOptions)
                ?? throw Invalid("empty file");

            var actions = new Dictionary<int, PerkActionDefinition>();
            foreach (ActionRow? row in file.PerkActions ?? [])
            {
                if (row?.Hash is not { Length: 4 })
                    throw Invalid("hash " + row?.Hash);

                var tiers = new List<PerkActionTier>();
                foreach (TierRow? tier in row.Tiers ?? [])
                {
                    if (tier == null || tier.LowId <= 0 || tier.HighId <= 0 || tier.LowQl < 1 || tier.HighQl <= tier.LowQl)
                        throw Invalid("tier of " + row.Hash);
                    tiers.Add(new PerkActionTier(tier.LowId, tier.HighId, tier.LowQl, tier.HighQl));
                }

                int actionId = row.ActionId ?? (tiers.Count > 0 ? tiers[0].LowId : 0);
                if (actionId <= 0 || row.SkillModifier is <= 0)
                    throw Invalid("action " + row.Hash);

                if (!actions.TryAdd(PackHash(row.Hash), new PerkActionDefinition(actionId, tiers.ToArray(), row.SkillModifier ?? 1.0)))
                    throw Invalid("duplicate hash " + row.Hash);
            }

            return new PerkActionCatalog(actions);
        }

        static PerkActionCatalog LoadOrEmpty(string root)
        {
            try
            {
                return Load(root);
            }
            catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
            {
                LogUtil.ErrorException(exception, "Perk action catalog not loaded from {0}; perk actions use their QL 1 template", root);
                return Empty;
            }
        }

        static InvalidDataException Invalid(string detail)
            => new(GameDataPaths.PerkActionsFileName + ": invalid " + detail);

        sealed class ActionsFile
        {
            public ActionRow?[]? PerkActions { get; set; }
        }

        sealed class ActionRow
        {
            public string? Hash { get; set; }

            public int? ActionId { get; set; }

            public double? SkillModifier { get; set; }

            public TierRow?[]? Tiers { get; set; }
        }

        sealed class TierRow
        {
            public int LowId { get; set; }

            public int HighId { get; set; }

            public int LowQl { get; set; }

            public int HighQl { get; set; }
        }
    }

    public readonly record struct PerkActionTier(int LowId, int HighId, int LowQl, int HighQl);

    /// <summary>One perk action: its granted template, optional QL tiers and the skill rate that picks them.</summary>
    public sealed record PerkActionDefinition(int ActionId, PerkActionTier[] Tiers, double SkillModifier)
    {
        /// <summary>
        /// The tier and QL for <paramref name="skill"/>: skill x <see cref="SkillModifier"/>, each
        /// <see cref="PerkActionCatalog.SkillPerTier"/> one tier, QL interpolated inside it; past the last tier
        /// caps at its high QL.
        /// </summary>
        public (PerkActionTier Tier, int Quality) Resolve(int skill)
        {
            if (Tiers.Length == 0)
                throw new InvalidOperationException("Perk action has no tiers.");

            const int perTier = PerkActionCatalog.SkillPerTier;
            long effective = Math.Max(1, (long)Math.Round(Math.Max(0, skill) * SkillModifier));
            int index = (int)Math.Min((effective - 1) / perTier, Tiers.Length - 1);
            long inTier = Math.Clamp(effective - (long)index * perTier, 1, perTier);
            PerkActionTier tier = Tiers[index];
            int quality = tier.LowQl + (int)((inTier - 1) * (tier.HighQl - tier.LowQl) / (perTier - 1));
            return (tier, Math.Clamp(quality, tier.LowQl, tier.HighQl));
        }
    }
}
