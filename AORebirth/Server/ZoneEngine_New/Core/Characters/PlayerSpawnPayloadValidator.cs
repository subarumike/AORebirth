namespace ZoneEngine_New.Core.Characters
{
    using System;
    using System.Collections.Generic;
    using System.Collections;
    using System.Linq;
    using System.Reflection;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;

    /// <summary>Final semantic guard after rebase and before a player is published or serialized.</summary>
    public static class PlayerSpawnPayloadValidator
    {
        public static void RequireValid(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            var errors = new List<string>();

            foreach (CharacterStat stat in CharacterHydrationValidator.RequiredSpawnStats)
            {
                int value = player.Stats.Get(stat, StatDetail.Base);
                if (StatCollection.IsUnset(value)) errors.Add("missing-or-unset:" + (int)stat + ":" + stat);
            }

            foreach ((CharacterStat stat, int @base, int bonus, int full) in player.Stats.GetEntries())
            {
                if (StatCollection.IsUnset(@base) || StatCollection.IsUnset(bonus) || StatCollection.IsUnset(full))
                    errors.Add("unset-sentinel:" + (int)stat + ":" + stat);
            }

            int flags = player.Stats.Get(CharacterStat.Flags);
            if (!StatCollection.IsUnset(flags) && ((CharacterFlags)flags).HasFlag(CharacterFlags.Tower))
                errors.Add("player-flags-tower");

            int health = player.Stats.Get(CharacterStat.Health);
            int maxHealth = player.Stats.Get(CharacterStat.MaxHealth);
            if (health < 0 || maxHealth <= 0 || health > maxHealth) errors.Add("health-invalid");

            int currentNano = player.Stats.Get(CharacterStat.CurrentNano);
            int maxNano = player.Stats.Get(CharacterStat.MaxNanoEnergy);
            if (currentNano < 0 || maxNano <= 0 || currentNano > maxNano) errors.Add("nano-invalid");

            if (player.Stats.GetOrZero(CharacterStat.HeadMesh) <= 0) errors.Add("head-mesh-invalid");
            if (player.Playfield == null || player.Playfield.Identity.Instance <= 0) errors.Add("playfield-invalid");
            if (player.Position == null
                || float.IsNaN(player.Position.xf) || float.IsInfinity(player.Position.xf)
                || float.IsNaN(player.Position.yf) || float.IsInfinity(player.Position.yf)
                || float.IsNaN(player.Position.zf) || float.IsInfinity(player.Position.zf)) errors.Add("position-invalid");

            if (errors.Count > 0)
                throw new InvalidOperationException("Player spawn payload rejected: " + string.Join(",", errors));
        }

        /// <summary>Checks the actual two messages, including optional numeric fields and conditional shape.</summary>
        public static void RequireValidMessages(SimpleCharFullUpdateMessage spawn, FullCharacterMessage full)
        {
            ArgumentNullException.ThrowIfNull(spawn);
            ArgumentNullException.ThrowIfNull(full);
            var errors = new List<string>();
            RejectSentinels(spawn, "scfu", errors, new HashSet<object>(ReferenceEqualityComparer.Instance));
            RejectSentinels(full, "full", errors, new HashSet<object>(ReferenceEqualityComparer.Instance));
            if (spawn.Identity != full.Identity || spawn.Identity.Type != IdentityType.CanbeAffected || spawn.Identity.Instance <= 0)
                errors.Add("identity-invalid");
            if (spawn.PlayfieldId.GetValueOrDefault() <= 0 || string.IsNullOrWhiteSpace(spawn.Name)) errors.Add("identity-state-missing");
            if (spawn.CharacterFlags.HasFlag(CharacterFlags.Tower) || spawn.ScfuTowerUnk != 0)
                errors.Add("ordinary-player-tower");
            if (spawn.CharacterInfo is not SimplePcInfo pc)
                errors.Add("ordinary-player-info-shape");
            else
            {
                if (pc.StrengthBase <= 0 || pc.AgilityBase <= 0 || pc.StaminaBase <= 0
                    || pc.IntelligenceBase <= 0 || pc.SenseBase <= 0 || pc.PsychicBase <= 0)
                    errors.Add("primary-abilities-invalid");
                if (spawn.CharacterFlags.HasFlag(CharacterFlags.HasVisibleName)
                    && (pc.FirstName == null || pc.LastName == null)) errors.Add("visible-name-shape");
            }
            if (!spawn.HeadMesh.HasValue || spawn.HeadMesh.Value == 0 || spawn.VisualFlags < 0 || spawn.MonsterScale <= 0
                || spawn.Appearance == null || (int)spawn.Appearance.Breed < 1 || (int)spawn.Appearance.Breed > 4
                || spawn.Appearance.Race == 0) errors.Add("appearance-invalid");
            if (spawn.Health <= 0 || spawn.HealthDamage < 0 || spawn.HealthDamage > spawn.Health)
                errors.Add("health-invalid");
            if (full.Stats1 == null || full.Stats2 == null || full.Stats3 == null || full.Stats4 == null)
                errors.Add("full-stat-block-missing");
            else
            {
                var values = new Dictionary<int, long>();
                void Add(int id, long value)
                {
                    // Legacy and NewEngine repeat some stats between groups; conflicting values are invalid.
                    if (values.TryGetValue(id, out long previous) && previous != value) errors.Add("conflicting-stat:" + id);
                    values[id] = value;
                }
                foreach (var row in full.Stats1.Concat(full.Stats2)) Add(row.Value1, row.Value2);
                foreach (var row in full.Stats3) Add(row.Value1, row.Value2);
                foreach (var row in full.Stats4) Add(row.Value1, row.Value2);
                foreach (CharacterStat required in CharacterHydrationValidator.RequiredSpawnStats)
                    if (required != CharacterStat.HeadMesh && !values.ContainsKey((int)required)) errors.Add("missing-stat:" + required);
                if (values.TryGetValue((int)CharacterStat.Health, out long current)
                    && values.TryGetValue((int)CharacterStat.MaxHealth, out long maximum))
                {
                    // Existing SCFU presentation scales maxima above UInt16; durable/FullCharacter values stay exact.
                    long displayMax = Math.Min(maximum, ushort.MaxValue);
                    long displayCurrent = maximum > ushort.MaxValue ? current * ushort.MaxValue / maximum : current;
                    if (maximum <= 0 || current < 0 || current > maximum || spawn.Health != displayMax
                        || spawn.HealthDamage != displayMax - displayCurrent) errors.Add("full-scfu-health-mismatch");
                }
                if (values.TryGetValue((int)CharacterStat.Expansion, out long expansions) && expansions != spawn.Expansions)
                    errors.Add("expansion-mismatch");
                if (values.TryGetValue((int)CharacterStat.VisualFlags, out long visual) && visual != spawn.VisualFlags)
                    errors.Add("visual-flags-mismatch");
            }
            if (errors.Count > 0) throw new InvalidOperationException("Player wire payload rejected: " + string.Join(",", errors));
        }

        private static void RejectSentinels(object? value, string path, List<string> errors, HashSet<object> seen)
        {
            if (value == null || value is string) return;
            Type type = value.GetType();
            if (type.IsEnum) value = Convert.ToInt64(value);
            if (value is int or uint or long or ulong or float or double)
            {
                double number = Convert.ToDouble(value);
                if (number == (int)CharacterStat.Unset || double.IsNaN(number) || double.IsInfinity(number))
                    errors.Add("invalid-numeric:" + path);
                return;
            }
            if (type.IsPrimitive || !seen.Add(value)) return;
            if (value is IEnumerable sequence)
            {
                int index = 0;
                foreach (object? item in sequence) RejectSentinels(item, path + "[" + index++ + "]", errors, seen);
                return;
            }
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                if (property.CanRead && property.GetIndexParameters().Length == 0
                    && property.Name is not "RawBody" and not "UndecodedTail")
                    RejectSentinels(property.GetValue(value), path + "." + property.Name, errors, seen);
        }
    }
}
