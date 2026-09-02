namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Playfields;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Playfields.Content;

    using Coordinate = AORebirth.Core.Vector.Coordinate;

    #endregion

    /// <summary>
    /// Capture 20260826-135727 / 20260826-200902: Spinetooth Hatchling 3m automatic aggro on PF4310.
    /// </summary>
    internal static class NascenceFrontierSpinetoothMobCombat
    {
        private const float AggroRadiusMeters = 3.0f;

        private const string AggroChatLine = "Spinetooth Hatchling: Me much stronger than you.";

        private static readonly object Gate = new object();

        private static readonly HashSet<int> AggressiveMobs = new HashSet<int>();

        private static readonly HashSet<int> AggroChatSent = new HashSet<int>();

        internal static void RegisterAggressive(Identity identity)
        {
            if (identity.Instance == 0)
            {
                return;
            }

            lock (Gate)
            {
                AggressiveMobs.Add(identity.Instance);
            }
        }

        internal static void UnregisterAggressive(Identity identity)
        {
            if (identity.Instance == 0)
            {
                return;
            }

            lock (Gate)
            {
                AggressiveMobs.Remove(identity.Instance);
                AggroChatSent.Remove(identity.Instance);
            }
        }

        internal static ICharacter FindAutomaticAggroTarget(ICharacter npc)
        {
            if (npc == null || npc.Playfield == null)
            {
                return null;
            }

            if (npc.Playfield.Identity.Instance != NascenceLifeContentModule.FrontierPlayfieldId)
            {
                return null;
            }

            if (!string.Equals(npc.Name, "Spinetooth Hatchling", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            lock (Gate)
            {
                if (!AggressiveMobs.Contains(npc.Identity.Instance))
                {
                    return null;
                }
            }

            if (npc.FightingTarget.Instance != 0 || npc.Stats[StatIds.health].Value <= 0)
            {
                return null;
            }

            Playfield playfield = npc.Playfield as Playfield;
            if (playfield == null)
            {
                return null;
            }

            Coordinate npcPos = npc.CalculatePredictedPosition();
            ICharacter nearest = null;
            double nearestDist = AggroRadiusMeters;
            List<ICharacter> inRange = playfield.FindCharacterInRange(npc, AggroRadiusMeters);
            for (int i = 0; i < inRange.Count; i++)
            {
                ICharacter candidate = inRange[i];
                if (candidate == null
                    || candidate.Identity.Instance == npc.Identity.Instance
                    || !(candidate.Controller is PlayerController)
                    || candidate.Stats[StatIds.health].Value <= 0)
                {
                    continue;
                }

                double dist = candidate.CalculatePredictedPosition().coordinate.Distance2D(npcPos.coordinate);
                if (dist <= nearestDist)
                {
                    nearestDist = dist;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        internal static void TryNotifyAggroChat(ICharacter spinetooth, ICharacter player)
        {
            if (spinetooth == null || player == null)
            {
                return;
            }

            if (!string.Equals(spinetooth.Name, "Spinetooth Hatchling", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            lock (Gate)
            {
                if (!AggroChatSent.Add(spinetooth.Identity.Instance))
                {
                    return;
                }
            }

            // Capture 20260826-135727 NpcMessage Unk2=1 plain mob line.
            ChatTextMessageHandler.Default.Send(player, AggroChatLine, 0, 1, 0);
        }
    }
}
