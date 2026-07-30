namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core;

    #endregion

    /// <summary>
    /// Tracks trash kills inside a mission instance for Clan/Omni token chance only.
    /// Each trash kill raises chance by 100/totalTrash percent; token grants require &gt;=86%.
    /// Token % never gates the rolled mission ItemRewards / cash / XP — those pay on objective complete.
    /// </summary>
    internal static class MissionTokenProgressTracker
    {
        private sealed class Session
        {
            public int PlayfieldInstance;

            public int CharacterInstance;

            public int TotalTrash;

            public int KilledTrash;

            public int Percent;
        }

        private static readonly object Sync = new object();

        private static readonly Dictionary<int, Session> ByPlayfield = new Dictionary<int, Session>();

        private static readonly Dictionary<int, Session> ByCharacter = new Dictionary<int, Session>();

        // Grey trash identities (no side textures) — kill does not raise token %.
        private static readonly HashSet<long> GreyTrash = new HashSet<long>();

        public static void RegisterGreyTrash(Identity identity)
        {
            if ((int)identity.Type == 0 || identity.Instance == 0)
            {
                return;
            }

            lock (Sync)
            {
                GreyTrash.Add(Key(identity));
            }
        }

        private static bool IsCountableTrash(Identity identity)
        {
            lock (Sync)
            {
                return !GreyTrash.Contains(Key(identity));
            }
        }

        private static long Key(Identity identity)
        {
            return ((long)(int)identity.Type << 32) | (uint)identity.Instance;
        }

        public static void ClearGreyTrash()
        {
            lock (Sync)
            {
                GreyTrash.Clear();
            }
        }

        public static void Begin(int playfieldInstance, int totalTrash)
        {
            if (playfieldInstance == 0 || !IsActiveMissionPlayfield(playfieldInstance))
            {
                return;
            }

            int total = totalTrash > 0 ? totalTrash : 0;
            lock (Sync)
            {
                Session existing;
                if (ByPlayfield.TryGetValue(playfieldInstance, out existing) && existing != null)
                {
                    existing.TotalTrash = total;
                    existing.KilledTrash = 0;
                    existing.Percent = total == 0 ? 100 : 0;
                    return;
                }

                ByPlayfield[playfieldInstance] = new Session
                {
                    PlayfieldInstance = playfieldInstance,
                    TotalTrash = total,
                    KilledTrash = 0,
                    Percent = total == 0 ? 100 : 0
                };
            }
        }

        public static void BindCharacter(int playfieldInstance, int characterInstance)
        {
            if (playfieldInstance == 0
                || characterInstance == 0
                || !IsActiveMissionPlayfield(playfieldInstance))
            {
                return;
            }

            lock (Sync)
            {
                Session session;
                if (!ByPlayfield.TryGetValue(playfieldInstance, out session) || session == null)
                {
                    session = new Session
                    {
                        PlayfieldInstance = playfieldInstance,
                        TotalTrash = 0,
                        KilledTrash = 0,
                        Percent = 100
                    };
                    ByPlayfield[playfieldInstance] = session;
                }

                session.CharacterInstance = characterInstance;
                ByCharacter[characterInstance] = session;
            }
        }

        public static void NotifyTrashKilled(ICharacter attacker, ICharacter victim)
        {
            if (attacker == null || victim == null || victim.Playfield == null)
            {
                return;
            }

            if (!MissionInstanceService.IsMissionInstancePlayfield(victim.Playfield.Identity.Instance))
            {
                return;
            }

            // Objective-only NPCs (FindPerson tag target, Broken Machine, Mission Cube) are not trash.
            if (MissionFindPersonService.IsFindPersonTarget(victim.Identity)
                || MissionMachineTracker.IsMissionMachine(victim.Identity)
                || MissionFindItemService.IsMissionCube(victim.Identity))
            {
                return;
            }

            // Only aggressive trash counts toward token %.
            if (!MissionInstanceMobCombat.IsAggressive(victim.Identity))
            {
                return;
            }

            // Grey trash = 0% contribution; only registered colored trash raises %.
            if (!IsCountableTrash(victim.Identity))
            {
                return;
            }

            int pf = victim.Playfield.Identity.Instance;
            if (!IsActiveMissionPlayfield(pf))
            {
                return;
            }

            int percent;
            bool changed;
            lock (Sync)
            {
                Session session;
                if (!ByPlayfield.TryGetValue(pf, out session) || session == null)
                {
                    session = new Session
                    {
                        PlayfieldInstance = pf,
                        CharacterInstance = attacker.Identity.Instance,
                        TotalTrash = 0,
                        KilledTrash = 0,
                        Percent = 0
                    };
                    ByPlayfield[pf] = session;
                    ByCharacter[attacker.Identity.Instance] = session;
                }
                else if (session.CharacterInstance == 0)
                {
                    session.CharacterInstance = attacker.Identity.Instance;
                    ByCharacter[attacker.Identity.Instance] = session;
                }

                if (session.TotalTrash <= 0)
                {
                    // Spawn race: still credit kills so % can climb once Begin stamps total.
                    session.KilledTrash++;
                    session.Percent = Math.Min(100, session.KilledTrash * 10);
                    percent = session.Percent;
                    changed = true;
                }
                else if (session.KilledTrash >= session.TotalTrash)
                {
                    percent = 100;
                    changed = false;
                }
                else
                {
                    session.KilledTrash++;
                    session.Percent = (session.KilledTrash * 100) / session.TotalTrash;
                    if (session.KilledTrash >= session.TotalTrash)
                    {
                        session.Percent = 100;
                    }

                    percent = session.Percent;
                    changed = true;
                }
            }

            if (!changed)
            {
                return;
            }

            attacker.Send(
                new FormatFeedbackMessage
                {
                    Identity = attacker.Identity,
                    Unknown = 1,
                    Unknown1 = 0,
                    Unknown2 = 0,
                    FormattedMessage = TokenBoardRuntime.ToYellowSystemFeedback(
                        percent >= 100
                            ? "Mission chance of token reward upped to 100% due to your heroic effort."
                            : string.Format(
                                "Mission chance of token reward upped to {0}%.",
                                percent))
                });

            MissionDiagnostics.Log(
                "TOKEN-PCT char={0} pf={1} percent={2}",
                attacker.Identity.Instance,
                pf,
                percent);
        }

        public static bool HasFullTokenChance(int characterInstance)
        {
            int playfieldInstance;
            int percent;
            lock (Sync)
            {
                Session session;
                if (!ByCharacter.TryGetValue(characterInstance, out session) || session == null)
                {
                    // No session (finished outside tracked instance) → do not grant tokens.
                    return false;
                }

                playfieldInstance = session.PlayfieldInstance;
                percent = session.Percent;
            }

            // Allocator-range PF2 sessions fail closed unless their exact binding is
            // still active and unexpired. Legacy non-ACG mission tracking is unchanged.
            return percent >= 86 && IsActiveMissionPlayfield(playfieldInstance);
        }

        public static void ClearCharacter(int characterInstance)
        {
            lock (Sync)
            {
                Session session;
                if (ByCharacter.TryGetValue(characterInstance, out session) && session != null)
                {
                    ByPlayfield.Remove(session.PlayfieldInstance);
                }

                ByCharacter.Remove(characterInstance);
            }
        }

        public static void ClearPlayfield(int playfieldInstance)
        {
            lock (Sync)
            {
                var characterInstances = new List<int>();
                foreach (KeyValuePair<int, Session> entry in ByCharacter)
                {
                    if (entry.Value != null
                        && entry.Value.PlayfieldInstance == playfieldInstance)
                    {
                        characterInstances.Add(entry.Key);
                    }
                }

                for (int i = 0; i < characterInstances.Count; i++)
                {
                    ByCharacter.Remove(characterInstances[i]);
                }

                ByPlayfield.Remove(playfieldInstance);
            }
        }

        internal static bool HasPlayfield(int playfieldInstance)
        {
            lock (Sync)
            {
                return ByPlayfield.ContainsKey(playfieldInstance);
            }
        }

        private static bool IsActiveMissionPlayfield(int playfieldInstance)
        {
            if (!MissionAcgAllocationService.IsAllocatableRange(playfieldInstance))
            {
                return true;
            }

            MissionAcgBindingRecord record;
            return MissionAcgBindingRuntime.TryResolveByLivePlayfield(
                       playfieldInstance,
                       out record)
                   && record != null
                   && record.Binding != null
                   && record.State != null
                   && record.State.CanEnter(DateTime.UtcNow, record.Binding.ExpiryUtc);
        }
    }
}
