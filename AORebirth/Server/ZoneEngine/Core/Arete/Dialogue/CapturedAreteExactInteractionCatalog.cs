// <copyright file="CapturedAreteExactInteractionCatalog.cs" company="AORebirth">
// Copyright (c) AORebirth. All rights reserved.
// </copyright>

namespace ZoneEngine.Core.Arete.Dialogue
{
    using System;

    /// <summary>
    /// Exact player-to-NPC reply observations retained from the reconciled Arete
    /// interaction extraction. The catalog deliberately stops when the observed
    /// sequence is exhausted; the corpus does not prove a repeat probability.
    /// </summary>
    internal static class CapturedAreteExactInteractionCatalog
    {
        internal const string MarioCarlesName = "Mario Carles";
        internal const string RoboticGuardDogName = "Robotic Guard Dog";
        internal const string ShadyGuyName = "Shady Guy";

        // tools-temp/arete-analysis/dialogue_trees.md:383-410.
        // The two separately captured "No you!" shouts are not interaction replies
        // and remain excluded because their trigger semantics are not captured.
        private static readonly string[] MarioCarlesReplies =
        {
            "Out of my way.",
            "Move.",
            "Out of my way.",
            "Move.",
            "Move.",
            "Out of my way.",
            "Out of my way.",
            "Out of my way.",
            "Out of my way.",
            "Move.",
            "Out of my way.",
            "Out of my way.",
            "Out of my way.",
            "Out of my way.",
            "Move.",
            "Move.",
            "Out of my way.",
            "Move.",
            "Out of my way.",
            "Out of my way.",
            "Move.",
            "Move.",
            "Out of my way.",
            "Out of my way.",
            "Out of my way.",
            "Move.",
            "Move."
        };

        // tools-temp/arete-analysis/dialogue_trees.md:491.
        private static readonly string[] RoboticGuardDogReplies =
        {
            "Woof woof woof!!!!"
        };

        // tools-temp/arete-analysis/dialogue_trees.md:534-536.
        private static readonly string[] ShadyGuyReplies =
        {
            "Useless..",
            "Useless..",
            "Useless.."
        };

        internal static bool IsCapturedReplyNpc(string npcName)
        {
            return string.Equals(npcName, MarioCarlesName, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(npcName, RoboticGuardDogName, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(npcName, ShadyGuyName, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool TryGetReply(string npcName, int observationOrdinal, out string reply)
        {
            reply = null;
            if (observationOrdinal < 0)
            {
                return false;
            }

            string[] observations = ResolveObservations(npcName);
            if (observations == null || observationOrdinal >= observations.Length)
            {
                return false;
            }

            reply = observations[observationOrdinal];
            return true;
        }

        internal static int GetObservationCount(string npcName)
        {
            string[] observations = ResolveObservations(npcName);
            return observations == null ? 0 : observations.Length;
        }

        private static string[] ResolveObservations(string npcName)
        {
            if (string.Equals(npcName, MarioCarlesName, StringComparison.OrdinalIgnoreCase))
            {
                return MarioCarlesReplies;
            }

            if (string.Equals(npcName, RoboticGuardDogName, StringComparison.OrdinalIgnoreCase))
            {
                return RoboticGuardDogReplies;
            }

            if (string.Equals(npcName, ShadyGuyName, StringComparison.OrdinalIgnoreCase))
            {
                return ShadyGuyReplies;
            }

            return null;
        }
    }
}
