namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    #endregion

    /// <summary>
    /// Capture-backed Rex Larsson idle social gestures (CharacterAction Action=100 / NpcSocialAnim).
    /// Capture 20260731-173525: Target.Instance cycles 0x2F (47) / 0x1F (31) about every 15s.
    /// Client shows these as Emote / Direction / Lookout style idle looks.
    /// </summary>
    internal static class AreteRexLarssonIdleGestureRuntime
    {
        private const double IntervalSeconds = 15.0;

        private const string RexName = "Rex Larsson";

        // Observed Target.Instance sequence: 0x2F, 0x1F, 0x2F (… / Lookout-style cycle).
        private static readonly int[] GestureCycle = { 0x2F, 0x1F };

        private static readonly object Sync = new object();

        private static readonly List<GestureActor> Actors = new List<GestureActor>();

        internal static void Register(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            lock (Sync)
            {
                Actors.RemoveAll(a => a.Character == null || ReferenceEquals(a.Character, character));
                Actors.Add(
                    new GestureActor
                    {
                        Character = character,
                        NextDueUtc = DateTime.UtcNow.AddSeconds(IntervalSeconds),
                        CycleIndex = 0
                    });
            }
        }

        internal static void Clear()
        {
            lock (Sync)
            {
                Actors.Clear();
            }
        }

        internal static void ProcessDue(DateTime utcNow)
        {
            GestureActor[] snapshot;
            lock (Sync)
            {
                Actors.RemoveAll(a => a.Character == null || a.Character.Playfield == null);
                snapshot = Actors.ToArray();
            }

            foreach (GestureActor actor in snapshot)
            {
                if (utcNow < actor.NextDueUtc)
                {
                    continue;
                }

                ICharacter character = actor.Character;
                if (character == null
                    || character.Playfield == null
                    || character.Stats[StatIds.health].Value <= 0)
                {
                    continue;
                }

                int animId = GestureCycle[actor.CycleIndex % GestureCycle.Length];
                actor.CycleIndex++;
                actor.NextDueUtc = utcNow.AddSeconds(IntervalSeconds);

                character.Playfield.Announce(
                    new CharacterActionMessage
                    {
                        Identity = character.Identity,
                        Unknown = 0,
                        Action = CharacterActionType.NpcSocialAnim,
                        Unknown1 = 0,
                        Target = new Identity { Type = IdentityType.None, Instance = animId },
                        Parameter1 = 0,
                        Parameter2 = 0,
                        Unknown2 = 0
                    });
            }
        }

        internal static bool IsRexLarsson(ICharacter character)
        {
            return character != null
                   && string.Equals(character.Name, RexName, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class GestureActor
        {
            public ICharacter Character;

            public DateTime NextDueUtc;

            public int CycleIndex;
        }
    }
}
