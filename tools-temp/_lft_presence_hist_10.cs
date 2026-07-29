namespace ZoneEngine.Core
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.MessageHandlers;

    /// <summary>
    /// Online player resolve + InfoPacket-only name/level seed for LFT/team.
    /// Never Despawn or Stat-lie world Level — that broke visibility.
    /// Compatible XP pairs: real name + real Level (stops NoName false-warn).
    /// Incompatible pairs: real high Level so the client XP warn can still show.
    /// </summary>
    public static class LftInviteClientPresence
    {
        public const string LftSeedCommandPrefix = "#aorebirth-lft-seed";

        public static ICharacter ResolveOnlinePlayer(ICharacter requester, Identity targetIdentity)
        {
            if (targetIdentity.Instance == 0)
            {
                return null;
            }

            Identity typed = new Identity
            {
                Type = IdentityType.CanbeAffected,
                Instance = targetIdentity.Instance
            };

            if (requester != null && requester.Playfield != null)
            {
                IInstancedEntity local = requester.Playfield.FindByIdentity(typed);
                var localChar = local as ICharacter;
                if (localChar != null)
                {
                    return localChar;
                }

                localChar = Pool.Instance.GetObject<ICharacter>(requester.Playfield.Identity, typed);
                if (localChar != null)
                {
                    return localChar;
                }
            }

            ICharacter global = Pool.Instance.GetObject<ICharacter>(typed);
            if (global != null)
            {
                return global;
            }

            uint want = unchecked((uint)targetIdentity.Instance);
            foreach (ICharacter candidate in Pool.Instance.GetAll<ICharacter>((int)IdentityType.CanbeAffected))
            {
                if (candidate == null || candidate.Controller == null || candidate.Controller.Client == null)
                {
                    continue;
                }

                if (unchecked((uint)candidate.Identity.Instance) == want)
                {
                    return candidate;
                }
            }

            return null;
        }

        public static ICharacter ResolveOnlinePlayerByInstance(int characterInstance)
        {
            if (characterInstance == 0)
            {
                return null;
            }

            return ResolveOnlinePlayer(
                null,
                new Identity
                {
                    Type = IdentityType.CanbeAffected,
                    Instance = characterInstance
                });
        }

        public static bool IsRemoteFrom(ICharacter requester, ICharacter target)
        {
            if (requester == null || target == null || requester.Playfield == null)
            {
                return false;
            }

            if (target.Playfield == null)
            {
                return true;
            }

            return !target.Playfield.Identity.Equals(requester.Playfield.Identity);
        }

        public static void ExchangeOnlinePresence(ICharacter character)
        {
        }

        public static void SeedAllRemotesOnto(ICharacter viewer)
        {
        }

        public static void SeedCandidatesForSearcher(ICharacter searcher, IEnumerable<int> candidateInstances)
        {
            SeedCandidatesForSearcher(searcher, candidateInstances, null);
        }

        public static void SeedCandidatesForSearcher(
            ICharacter searcher,
            IEnumerable<int> candidateInstances,
            IDictionary<int, string> nameOverrides)
        {
            if (searcher == null || candidateInstances == null)
            {
                return;
            }

            foreach (int instance in candidateInstances)
            {
                ICharacter candidate = ResolveOnlinePlayerByInstance(instance);
                if (candidate == null || candidate.Identity.Equals(searcher.Identity))
                {
                    continue;
                }

                string nameOverride = null;
                if (nameOverrides != null)
                {
                    nameOverrides.TryGetValue(instance, out nameOverride);
                }

                SeedNameAndLevelOnly(searcher, candidate, nameOverride);
            }
        }

        public static void SeedForInviteLookup(ICharacter requester, ICharacter remote)
        {
            SeedNameAndLevelOnly(requester, remote, null);
        }

        public static void SeedForInviteLookup(ICharacter requester, ICharacter remote, string nameOverride)
        {
            SeedNameAndLevelOnly(requester, remote, nameOverride);
        }

        /// <summary>
        /// InfoPacket-only to requester about remote. No SCFU/Despawn/Stat.
        /// Level is always the remote's real Level (compatible → no warn; too-high → warn OK).
        /// </summary>
        public static void SeedNameAndLevelOnly(ICharacter requester, ICharacter remote, string nameOverride)
        {
            if (requester == null || remote == null)
            {
                return;
            }

            if (requester.Controller == null || requester.Controller.Client == null)
            {
                return;
            }

            string name = nameOverride;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = remote.Name;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                try
                {
                    name = remote.FirstName;
                }
                catch
                {
                    name = null;
                }
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                name = "Player";
            }

            int level = 1;
            try
            {
                level = remote.Stats[StatIds.level].Value;
            }
            catch
            {
                level = 1;
            }

            if (level < 1)
            {
                level = 1;
            }

            CharacterInfoPacketMessageHandler.Default.SendForTeamInvite(requester, remote, name, level);

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "LFT InfoPacket seed viewer=" + requester.Identity.ToString(true)
                + " subject=" + remote.Identity.ToString(true)
                + " name=" + name
                + " level=" + level
                + " tooHigh="
                + TeamXpShareWindow.IsTooHighForXpShare(
                    SafeLevel(requester),
                    level));
        }

        private static int SafeLevel(ICharacter character)
        {
            if (character == null)
            {
                return 1;
            }

            try
            {
                int level = character.Stats[StatIds.level].Value;
                return level < 1 ? 1 : level;
            }
            catch
            {
                return 1;
            }
        }
    }
}
