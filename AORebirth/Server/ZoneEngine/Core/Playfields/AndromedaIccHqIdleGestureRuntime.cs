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
    /// Capture-backed ICC HQ idle gestures (Natalia Akcora CharacterAction Action=100).
    /// Capture 20260719-Natalia-Akcoraanimation: Target.Instance cycles 62 / 2 / 25 about every 10s.
    /// </summary>
    internal static class AndromedaIccHqIdleGestureRuntime
    {
        private const double IntervalSeconds = 10.0;

        // Observed Target.Instance sequence from capture (hex 3E, 02, 19, …).
        private static readonly int[] NataliaGestureCycle = { 62, 2, 25, 62, 62, 62, 2 };

        private static readonly object Sync = new object();

        private static readonly List<GestureActor> Actors = new List<GestureActor>();

        internal static void RegisterNatalia(ICharacter character)
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
                if (character == null || character.Playfield == null || character.Stats[StatIds.health].Value <= 0)
                {
                    continue;
                }

                int animId = NataliaGestureCycle[actor.CycleIndex % NataliaGestureCycle.Length];
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

        private sealed class GestureActor
        {
            public ICharacter Character;

            public DateTime NextDueUtc;

            public int CycleIndex;
        }
    }
}
