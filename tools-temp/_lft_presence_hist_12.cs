namespace ZoneEngine.Core
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    /// <summary>
    /// Online resolve + Recruit XP-warn quieting.
    /// Never patch CharacterInfoPacket Unknown (that broke Info UI).
    /// Never Despawn / never rewrite world Level on the character object.
    /// Quiet = Stat Level (and TitleLevel) to ONE viewer about subject, clamped into XP window.
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

                QuietXpLevelOnViewer(searcher, candidate);
            }
        }

        public static void SeedForInviteLookup(ICharacter requester, ICharacter remote)
        {
            QuietXpLevelOnViewer(requester, remote);
        }

        public static void SeedForInviteLookup(ICharacter requester, ICharacter remote, string nameOverride)
        {
            QuietXpLevelOnViewer(requester, remote);
        }

        public static void SeedNameAndLevelOnly(ICharacter requester, ICharacter remote, string nameOverride)
        {
            QuietXpLevelOnViewer(requester, remote);
        }

        /// <summary>
        /// Tell viewer that subject is inside the XP share band so Recruit does not pop "too high".
        /// Wire-only Stat; does not change server-side Stats; does not touch InfoPacket.
        /// </summary>
        public static void QuietXpLevelOnViewer(ICharacter viewer, ICharacter subject)
        {
            if (viewer == null || subject == null)
            {
                return;
            }

            if (viewer.Controller == null || viewer.Controller.Client == null)
            {
                return;
            }

            if (viewer.Identity.Equals(subject.Identity))
            {
                return;
            }

            int viewerLevel = SafeLevel(viewer);
            int subjectLevel = SafeLevel(subject);
            int min;
            int max;
            TeamXpShareWindow.TryGetRange(viewerLevel, out min, out max);

            int quietLevel = subjectLevel;
            if (quietLevel < min)
            {
                quietLevel = min;
            }

            if (quietLevel > max)
            {
                quietLevel = max;
            }

            if (quietLevel < 1)
            {
                quietLevel = 1;
            }

            if (quietLevel > 220)
            {
                quietLevel = 220;
            }

            // TitleLevel 1 keeps title band from fighting Level quiet.
            int quietTitle = 1;

            try
            {
                var message = new StatMessage
                              {
                                  Identity = subject.Identity,
                                  Stats =
                                      new[]
                                      {
                                          new GameTuple<CharacterStat, uint>
                                          {
                                              Value1 = (CharacterStat)StatIds.level,
                                              Value2 = (uint)quietLevel
                                          },
                                          new GameTuple<CharacterStat, uint>
                                          {
                                              Value1 = (CharacterStat)StatIds.titlelevel,
                                              Value2 = (uint)quietTitle
                                          }
                                      }
                              };

                viewer.Controller.Client.SendCompressed(message);

                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "Recruit quiet Level viewer=" + viewer.Identity.ToString(true)
                    + " subject=" + subject.Identity.ToString(true)
                    + " real=" + subjectLevel
                    + " quiet=" + quietLevel
                    + " window=" + min + "-" + max);
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Recruit quiet Level failed: " + ex.Message);
            }
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
