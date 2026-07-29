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
    /// LFT Invite runs a client-side XP check against a local dynel.
    /// Cross-playfield targets are missing → "NoName is too high" and Yes loops forever
    /// (no TeamRequestInvite is ever sent). Seed a one-client SCFU+CharInPlay ghost with
    /// the real name and the requester's level so the dialog passes.
    /// </summary>
    public static class LftInviteClientPresence
    {
        public const string LftSeedCommandPrefix = "#aorebirth-lft-seed";

        public static ICharacter ResolveOnlinePlayer(ICharacter requester, Identity targetIdentity)
        {
            if (requester == null || targetIdentity.Instance == 0)
            {
                return null;
            }

            Identity typed = new Identity
            {
                Type = IdentityType.CanbeAffected,
                Instance = targetIdentity.Instance
            };

            if (requester.Playfield != null)
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

        /// <summary>
        /// Resolve the ISCom ChatCommand searcher when Pool.GetObject misses.
        /// </summary>
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

        /// <summary>
        /// True when the target is online but not in the requester's playfield.
        /// </summary>
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

            // Already a real local dynel — do not ghost-duplicate.
            if (!IsRemoteFrom(requester, remote))
            {
                return;
            }

            int requesterLevel = 1;
            try
            {
                requesterLevel = requester.Stats[StatIds.level].Value;
            }
            catch (Exception)
            {
                requesterLevel = 1;
            }

            if (requesterLevel < 1)
            {
                requesterLevel = 1;
            }

            if (requesterLevel > 220)
            {
                requesterLevel = 220;
            }

            string displayName = nameOverride;
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = character.Name;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = "Player";
            }

            SimpleCharFullUpdateMessage scfu = SimpleCharFullUpdate.ConstructMessage(character);
            scfu.Identity = remote.Identity;
            scfu.PlayfieldId = requester.Playfield.Identity.Instance;
            scfu.Level = (short)requesterLevel;
            scfu.Name = displayName;

            Coordinate requesterCoord = requester.Coordinates();
            // Same spot as requester — out-of-bounds / underground ghosts are often ignored.
            scfu.Coordinates = new Vector3
            {
                X = requesterCoord.x,
                Y = requesterCoord.y,
                Z = requesterCoord.z
            };

            // Clear any prior bad ghost (empty name / wrong PF) then flood SCFU+CharInPlay.
            requester.Send(new DespawnMessage { Identity = remote.Identity, Unknown = 0 });
            requester.Send(scfu);
            requester.Send(new CharInPlayMessage { Identity = remote.Identity, Unknown = 0x00 });
            requester.Send(scfu);
            requester.Send(new CharInPlayMessage { Identity = remote.Identity, Unknown = 0x00 });

            // Inspect/XP dialog typed name+level.
            CharacterInfoPacketMessageHandler.Default.Send(requester, remote);

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "LFT seed ghost name=" + displayName
                + " id=" + remote.Identity.Instance
                + " for=" + requester.Identity.Instance
                + " lvl=" + requesterLevel
                + " pf=" + requester.Playfield.Identity.Instance);
        }
    }
}
