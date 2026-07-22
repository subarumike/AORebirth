namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;

    #endregion

    /// <summary>
    /// Capture 20260722-keeper-exect-nano: Adaptive Ambient Restoration aura.
    /// Live ticks every ~20s: CastNanoSpell(302365)+CastNanoSpell(child)+SpellList "Ambient Restoration"
    /// + Health heal on Keeper and team. Red sparkle VFX is the SpellList.
    /// </summary>
    internal static class AmbientRestorationAuraRuntime
    {
        internal const int ParentNanoId = CapturedSpellListVisualEffects.AmbientRestorationNanoId;

        private const double TickSeconds = 20.0;

        private static readonly object Sync = new object();

        private static readonly Dictionary<int, AuraState> States = new Dictionary<int, AuraState>();

        private sealed class AuraState
        {
            public int CharacterInstance;

            public DateTime NextTickUtc;
        }

        internal static bool IsAmbientRestorationNano(int nanoId)
        {
            return nanoId == ParentNanoId;
        }

        /// <summary>
        /// Start/refresh aura on cast. Immediate first pulse matches capture (cast == first tick).
        /// </summary>
        internal static void StartOrRefresh(ICharacter caster)
        {
            if (caster == null || caster.Identity.Instance == 0)
            {
                return;
            }

            lock (Sync)
            {
                States[caster.Identity.Instance] = new AuraState
                {
                    CharacterInstance = caster.Identity.Instance,
                    NextTickUtc = DateTime.UtcNow + TimeSpan.FromSeconds(TickSeconds)
                };
            }

            Pulse(caster);
            LogUtil.Debug(
                DebugInfoDetail.GameFunctions,
                "AmbientRestorationAura start caster=" + caster.Identity.ToString(true));
        }

        internal static void Clear(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            lock (Sync)
            {
                States.Remove(character.Identity.Instance);
            }
        }

        internal static void ProcessTick(ICharacter character)
        {
            if (character == null || !(character.Controller is PlayerController))
            {
                return;
            }

            if (character.Stats[StatIds.health].Value <= 0)
            {
                Clear(character);
                return;
            }

            AuraState state;
            lock (Sync)
            {
                if (!States.TryGetValue(character.Identity.Instance, out state))
                {
                    return;
                }

                if (state.NextTickUtc > DateTime.UtcNow)
                {
                    return;
                }

                state.NextTickUtc = DateTime.UtcNow + TimeSpan.FromSeconds(TickSeconds);
            }

            Pulse(character);
        }

        private static void Pulse(ICharacter caster)
        {
            if (caster == null || caster.Playfield == null)
            {
                return;
            }

            int childNanoId;
            int healAmount;
            ResolveHealTier(caster.Stats[StatIds.level].Value, out childNanoId, out healAmount);

            // Capture: CastNanoSpell parent + child with Unknown1=1 (server-driven aura pulse).
            CastNanoSpellMessageHandler.Default.SendTriggeredSelfCast(caster, ParentNanoId);
            CastNanoSpellMessageHandler.Default.SendTriggeredSelfCast(caster, childNanoId);

            foreach (ICharacter recipient in EnumerateHealRecipients(caster))
            {
                ApplyHeal(recipient, healAmount);
                CapturedSpellListVisualEffects.AnnounceAmbientRestoration(recipient);
            }
        }

        private static IEnumerable<ICharacter> EnumerateHealRecipients(ICharacter caster)
        {
            var yielded = new HashSet<int>();
            yield return caster;
            yielded.Add(caster.Identity.Instance);

            List<Identity> team;
            if (TeamRuntime.TryGetTeamMembers(caster, out team))
            {
                foreach (Identity memberId in team)
                {
                    if (memberId.Instance == 0 || yielded.Contains(memberId.Instance))
                    {
                        continue;
                    }

                    ICharacter member = Pool.Instance.GetObject<ICharacter>(
                                           caster.Playfield.Identity,
                                           memberId)
                                       ?? Pool.Instance.GetObject<ICharacter>(memberId);
                    if (member == null
                        || member.Stats[StatIds.health].Value <= 0
                        || !member.InPlayfield(caster.Playfield.Identity))
                    {
                        continue;
                    }

                    yielded.Add(member.Identity.Instance);
                    yield return member;
                }
            }
        }

        private static void ApplyHeal(ICharacter recipient, int healAmount)
        {
            if (recipient == null || healAmount <= 0)
            {
                return;
            }

            int maxLife = Math.Max(1, recipient.Stats[StatIds.life].Value);
            int current = recipient.Stats[StatIds.health].Value;
            int room = Math.Max(0, maxLife - current);
            int applied = Math.Min(healAmount, room);
            if (applied <= 0)
            {
                // Still show aura VFX even at full health (capture ticks at 83/83).
                return;
            }

            recipient.Stats[StatIds.health].Value = current + applied;
            recipient.SendChangedStats();
        }

        /// <summary>
        /// Live capture level-1 Keeper used child 300495 (Hit Health +10).
        /// Higher children 300496/497/498 are +53/+143/+243.
        /// </summary>
        private static void ResolveHealTier(int level, out int childNanoId, out int healAmount)
        {
            if (level >= 150)
            {
                childNanoId = 300498;
                healAmount = 243;
                return;
            }

            if (level >= 100)
            {
                childNanoId = 300497;
                healAmount = 143;
                return;
            }

            if (level >= 50)
            {
                childNanoId = 300496;
                healAmount = 53;
                return;
            }

            childNanoId = CapturedSpellListVisualEffects.AmbientRestorationChildNanoId;
            healAmount = 10;
        }
    }
}
