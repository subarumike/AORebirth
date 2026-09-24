namespace ZoneEngine_New.Core.Helpers
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Ai;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Playfield;

    /// <summary>
    /// Grid-enter / one-way proxy terminals must work regardless of combat.
    /// Official ToUse criteria include <c>isfightingme == 0</c>; the client tracks that from
    /// live fight state, so entry clears fight on the player and any NPC targeting them,
    /// and server-side requirement reads treat IsFightingMe as 0.
    /// </summary>
    internal static class GridEnterTerminal
    {
        /// <summary>
        /// True when OnUse runs <see cref="FunctionType.TeleportProxy2"/> (Grid enter and similar
        /// one-way proxy machines). Detected from template spells — no content id literals.
        /// </summary>
        internal static bool IsGridEnter(ItemTemplate template)
        {
            if (template?.SpellList == null)
                return false;

            if (!template.SpellList.TryGetValue(EventType.OnUse, out List<ItemSpell>? spells)
                || spells == null
                || spells.Count == 0)
                return false;

            for (int i = 0; i < spells.Count; i++)
            {
                if (spells[i].Is(FunctionType.TeleportProxy2))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// True while the player has a TeleportProxy2 terminal selected — proximity aggro must
        /// not re-arm combat and re-block the client's isfightingme check.
        /// </summary>
        internal static bool IsPlayerTargetingGridEnter(Player player)
        {
            if (player == null || player.Target.Instance == 0 || player.Playfield == null)
                return false;

            if (!player.Playfield.GetRequiredService<DynelRegistry>().TryGet(player.Target, out Dynel? dynel))
                return false;

            return dynel is StaticDynel staticDynel && IsGridEnter(staticDynel.Template);
        }

        internal static Func<CharacterStat, int> RequirementStats(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            return stat => stat == CharacterStat.IsFightingMe
                ? 0
                : player.Stats.GetOrZero(stat);
        }

        internal static void ClearCombatForEntry(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);

            // Always announce StopFight — client can keep isfightingme after a silent clear
            // even when FightingTarget is already None.
            var stopFight = new StopFightMessage
            {
                Identity = player.Identity,
                Unknown1 = 1
            };
            player.Session?.Send(stopFight);
            player.Cell?.Announce(stopFight);
            if (player.FightingTarget.Instance != 0)
                player.SetFightingTarget(Identity.None);

            Playfield? playfield = player.Playfield;
            if (playfield == null)
                return;

            foreach (Dynel dynel in playfield.GetRequiredService<DynelRegistry>().Dynels())
            {
                if (dynel is not NpcCharacter npc)
                    continue;

                NpcBrain? brain = npc.Brain;
                bool fightingPlayer = npc.FightingTarget.Instance == player.Identity.Instance
                    && npc.FightingTarget.Type == player.Identity.Type;
                bool hatesPlayer = brain != null && brain.Hate.Contains(player.Identity);
                if (!fightingPlayer && !hatesPlayer)
                    continue;

                brain?.Hate.Remove(player.Identity);
                if (brain != null)
                    brain.StopFighting();
                else if (fightingPlayer)
                    NpcAiCombat.AnnounceStopFight(npc);
            }
        }
    }
}
