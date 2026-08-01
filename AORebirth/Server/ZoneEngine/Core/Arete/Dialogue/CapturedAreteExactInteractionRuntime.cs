// <copyright file="CapturedAreteExactInteractionRuntime.cs" company="AORebirth">
// Copyright (c) AORebirth. All rights reserved.
// </copyright>

namespace ZoneEngine.Core.Arete.Dialogue
{
    using System;
    using System.Collections.Generic;
    using AORebirth.Core.Entities;
    using AORebirth.ObjectManager;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    /// <summary>
    /// Emits only captured direct-interaction replies for exact Arete NPC names.
    /// Runtime identities are intentionally excluded from eligibility because they
    /// regenerate; playfield plus captured NPC name binds the observation instead.
    /// </summary>
    internal static class CapturedAreteExactInteractionRuntime
    {
        private const int AreteLandingPlayfieldId = 6553;

        private static readonly object SyncRoot = new object();

        private static readonly Dictionary<string, int> ReplyOrdinals =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        internal static bool TryHandleTrade(ICharacter npc, Identity sourceIdentity)
        {
            if (npc == null
                || npc.Playfield == null
                || npc.Playfield.Identity.Instance != AreteLandingPlayfieldId
                || !CapturedAreteExactInteractionCatalog.IsCapturedReplyNpc(npc.Name))
            {
                return false;
            }

            ICharacter source = Pool.Instance.GetObject<ICharacter>(npc.Playfield.Identity, sourceIdentity);
            if (source == null || source.Controller == null || source.Controller.Client == null)
            {
                return false;
            }

            int ordinal = ClaimOrdinal(npc, source);
            string reply;
            if (!CapturedAreteExactInteractionCatalog.TryGetReply(npc.Name, ordinal, out reply))
            {
                // The actor is proven eligible, but no repeat policy is captured.
                // Claim the interaction and remain silent after exact observations end.
                return true;
            }

            source.Controller.Client.SendCompressed(
                new ChatTextMessage
                {
                    Identity = source.Identity,
                    Text = npc.Name + ": " + reply
                });
            return true;
        }

        private static int ClaimOrdinal(ICharacter npc, ICharacter source)
        {
            string key = npc.Playfield.Identity.Instance
                         + ":" + npc.Identity.ToString(true)
                         + ":" + source.Identity.ToString(true);
            lock (SyncRoot)
            {
                int ordinal;
                if (!ReplyOrdinals.TryGetValue(key, out ordinal))
                {
                    ordinal = 0;
                }

                ReplyOrdinals[key] = ordinal + 1;
                return ordinal;
            }
        }
    }
}
