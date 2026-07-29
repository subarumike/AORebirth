namespace ZoneEngine.Core
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Packets;

    using Vector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;

    /// <summary>
    /// LFT Invite XP warning is client-side: it looks up a LOCAL dynel by character id.
    /// Cross-PF targets missing → hardcoded "NoName is too high" (Yes-loop).
    /// Seed named ghosts onto each client at login (Zone-only — no Chat ISCom race).
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

            if (requester.Playfield.FindByIdentity(target.Identity) != null)
            {
                return false;
            }

            return target.Playfield == null
                   || !target.Playfield.Identity.Equals(requester.Playfield.Identity);
        }

        /// <summary>
        /// After zone-in: put every other online player onto this client as a named ghost,
        /// and put this player onto every other client's cache. Does not use Chat/ISCom.
        /// </summary>
        public static void ExchangeOnlinePresence(ICharacter character)
        {
            if (character == null || character.Controller == null || character.Controller.Client == null)
            {
                return;
            }

            foreach (ICharacter other in EnumerateOnlinePlayers())
            {
                if (other == null || other.Identity.Instance == character.Identity.Instance)
                {
                    continue;
                }

                SeedForInviteLookup(character, other, null);
                SeedForInviteLookup(other, character, null);
            }
        }

        public static void SeedAllRemotesOnto(ICharacter viewer)
        {
            if (viewer == null)
            {
                return;
            }

            foreach (ICharacter other in EnumerateOnlinePlayers())
            {
                if (other == null || other.Identity.Instance == viewer.Identity.Instance)
                {
                    continue;
                }

                SeedForInviteLookup(viewer, other, null);
            }
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

            // Always refresh the full remote set first (covers ISCom misses / late logins).
            SeedAllRemotesOnto(searcher);

            foreach (int candidateInstance in candidateInstances)
            {
                if (candidateInstance == 0 || candidateInstance == searcher.Identity.Instance)
                {
                    continue;
                }

                ICharacter remote = ResolveOnlinePlayer(
                    searcher,
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = candidateInstance
                    });
                if (remote == null)
                {
                    continue;
                }

                string nameOverride = null;
                if (nameOverrides != null)
                {
                    nameOverrides.TryGetValue(candidateInstance, out nameOverride);
                }

                SeedForInviteLookup(searcher, remote, nameOverride);
            }
        }

        public static void SeedForInviteLookup(ICharacter requester, ICharacter remote)
        {
            SeedForInviteLookup(requester, remote, null);
        }

        public static void SeedForInviteLookup(ICharacter requester, ICharacter remote, string nameOverride)
        {
            if (requester == null || remote == null || requester.Controller == null
                || requester.Controller.Client == null || requester.Playfield == null)
            {
                return;
            }

            var character = remote as Character;
            if (character == null)
            {
                return;
            }

            if (!IsRemoteFrom(requester, remote))
            {
                return;
            }

            int requesterLevel = ReadLevel(requester);
            string displayName = nameOverride;
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = character.Name;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = "Player";
            }

            try
            {
                SimpleCharFullUpdateMessage scfu = SimpleCharFullUpdate.ConstructMessage(character);
                scfu.Identity = remote.Identity;
                scfu.PlayfieldId = requester.Playfield.Identity.Instance;
                scfu.Level = (short)requesterLevel;
                scfu.Name = displayName;
                scfu.CharacterFlags |= CharacterFlags.HasVisibleName;

                var pcInfo = scfu.CharacterInfo as SimplePcInfo;
                if (pcInfo != null)
                {
                    pcInfo.FirstName = displayName;
                    pcInfo.LastName = string.Empty;
                }

                Coordinate requesterCoord = requester.Coordinates();
                scfu.Coordinates = new Vector3
                {
                    X = requesterCoord.x,
                    Y = requesterCoord.y,
                    Z = requesterCoord.z
                };

                requester.Send(new DespawnMessage { Identity = remote.Identity, Unknown = 0 });
                requester.Send(scfu);
                requester.Send(new CharInPlayMessage { Identity = remote.Identity, Unknown = 0x00 });
                requester.Send(scfu);
                requester.Send(new CharInPlayMessage { Identity = remote.Identity, Unknown = 0x00 });

                CharacterInfoPacketMessageHandler.Default.SendForTeamInvite(requester, remote, displayName, requesterLevel);
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "LFT seed failed name=" + displayName + " err=" + ex.Message);
            }
        }

        private static IEnumerable<ICharacter> EnumerateOnlinePlayers()
        {
            foreach (ICharacter candidate in Pool.Instance.GetAll<ICharacter>((int)IdentityType.CanbeAffected))
            {
                if (candidate == null || candidate.Controller == null || candidate.Controller.Client == null)
                {
                    continue;
                }

                // Players only (NPC family 0).
                try
                {
                    if (candidate.Stats[StatIds.npcfamily].Value != 0)
                    {
                        continue;
                    }
                }
                catch (Exception)
                {
                    continue;
                }

                yield return candidate;
            }
        }

        private static int ReadLevel(ICharacter character)
        {
            int level = 1;
            try
            {
                level = character.Stats[StatIds.level].Value;
            }
            catch (Exception)
            {
                level = 1;
            }

            if (level < 1)
            {
                level = 1;
            }

            if (level > 220)
            {
                level = 220;
            }

            return level;
        }
    }
}
