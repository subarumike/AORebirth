namespace ZoneEngine_New.Core.Characters
{
    using System;
    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;

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
    }
}
