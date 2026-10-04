namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using ZoneEngine_New.Core.GameData;

    /// <summary>
    /// Item and NPC hashes an OnUse function would spawn. A use whose hash the server has no data for is blocked
    /// up front (nothing runs, nothing is spent) so the player can report it instead of losing the item.
    /// </summary>
    internal static class SpawnHashCheck
    {
        /// <summary>
        /// The first hash in <paramref name="template"/>'s OnUse functions that <paramref name="gameData"/> cannot
        /// resolve: item hashes for SpawnItem / RndSpawnItem, NPC hashes for the monster spawns and pet summons.
        /// </summary>
        public static bool TryFindMissingHash(ItemTemplate template, IGameData gameData, out string hash)
        {
            ArgumentNullException.ThrowIfNull(template);
            ArgumentNullException.ThrowIfNull(gameData);
            hash = string.Empty;

            if (!template.SpellList.TryGetValue(EventType.OnUse, out List<ItemSpell>? spells))
                return false;

            foreach (ItemSpell spell in spells)
            {
                bool itemHash = spell.Is(FunctionType.SpawnItem) || spell.Is(FunctionType.RndSpawnItem);
                bool npcHash = spell.Is(FunctionType.SpawnMonster) || spell.Is(FunctionType.SpawnMonster2)
                    || spell.Is(FunctionType.SpawnMonsterRot) || spell.Is(FunctionType.RndSpawnMonster)
                    || spell.Is(FunctionType.DelayedSpawnNpc) || spell.Is(FunctionType.SummonPet)
                    || spell.Is(FunctionType.SummonPets);
                if (!itemHash && !npcHash)
                    continue;

                // The random variants list several hashes; the others name one, first.
                bool random = spell.Is(FunctionType.RndSpawnItem) || spell.Is(FunctionType.RndSpawnMonster);
                int count = random ? spell.ArgumentCount : Math.Min(1, spell.ArgumentCount);
                for (int i = 0; i < count; i++)
                {
                    if (!spell.TryReadString(i, out string candidate) || string.IsNullOrWhiteSpace(candidate))
                        continue;

                    candidate = candidate.Trim();
                    bool resolves = itemHash ? gameData.CanResolveItemHash(candidate) : gameData.CanResolveMobHash(candidate);
                    if (!resolves)
                    {
                        hash = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>The chat line the player gets for a blocked use, in warning orange (client font tag, as GM output).</summary>
        public static string NotImplementedText(string hash)
            => "<font color=#FF8C00>" + hash + " is not implemented yet. Please create a bug report.</font>";
    }
}
