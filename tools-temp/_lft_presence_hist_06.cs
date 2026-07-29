namespace ZoneEngine.Core
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Packets;

    using Vector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;

    /// <summary>
    /// LFT Invite GUI looks up a local dynel/name by id. Missing → "NoName is too high".
    /// Do NOT spawn visible ghosts (that duplicated players across zones). Seed only:
    /// InfoPacket (name + XP-safe level) and one off-map SCFU for the name table.
    /// </summary>
    public static class LftInviteClientPresence
    {
        public const string LftSeedCommandPrefix = "#aorebirth-lft-seed";

        /// <summary>Far under the map so the name exists without a visible double.</summary>
        private const float OffMapY = -250000f;

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

        /// <summary>No-op: login SCFU ghosts caused visible doubles in both zones.</summary>
        public static void ExchangeOnlinePresence(ICharacter character)
        {
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

                SeedNameAndLevelOnly(viewer, other, null);
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

                SeedNameAndLevelOnly(searcher, remote, nameOverride);
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
        /// Name + XP-safe level only. No near-player SCFU (no doubles). Never invites.
        /// </summary>
        public static void SeedNameAndLevelOnly(ICharacter requester, ICharacter remote, string nameOverride)
        {
            if (requester == null || remote == null || requester.Controller == null
                || requester.Controller.Client == null || requester.Playfield == null)
            {
                return;
            }

            if (requester.Identity.Instance == remote.Identity.Instance)
            {
                return;
            }

            var remoteChar = remote as Character;
            if (remoteChar == null)
            {
                return;
            }

            int remoteLevel = ReadLevel(remote);
            int requesterLevel = ReadLevel(requester);
            string displayName = nameOverride;
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = remote.Name;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = remote.FirstName;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = "Player";
            }

            bool reallyHigh = TeamXpShareWindow.IsTooHighForXpShare(requesterLevel, remoteLevel);
            // Compatible → Level 1 never trips "too high". Real high → real level (warn OK).
            int infoLevel = reallyHigh ? remoteLevel : 1;

            try
            {
                // Clear any old visible ghost from earlier broken seeds.
                SendToRequester(requester, DespawnMessageHandler.Default.Create(remote.Identity));

                // Off-map SCFU so id→name exists without standing next to the player.
                SimpleCharFullUpdateMessage scfu = SimpleCharFullUpdate.ConstructMessage(remoteChar);
                scfu.Identity = new Identity
                {
                    Type = IdentityType.CanbeAffected,
                    Instance = remote.Identity.Instance
                };
                scfu.PlayfieldId = requester.Playfield.Identity.Instance;
                scfu.Level = (short)infoLevel;
                scfu.Name = displayName;
                scfu.CharacterFlags |= CharacterFlags.HasVisibleName;
                scfu.Coordinates = new Vector3 { X = 0f, Y = OffMapY, Z = 0f };

                var pcInfo = scfu.CharacterInfo as SimplePcInfo;
                if (pcInfo != null)
                {
                    pcInfo.FirstName = displayName;
                    pcInfo.LastName = string.Empty;
                }

                SendToRequester(requester, scfu);
                SendToRequester(
                    requester,
                    new CharInPlayMessage { Identity = scfu.Identity, Unknown = 0x00 });

                CharacterInfoPacketMessageHandler.Default.SendForTeamInvite(
                    requester,
                    remote,
                    displayName,
                    infoLevel);
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "LFT name seed failed name=" + displayName + " err=" + ex.Message);
            }
        }

        private static void SendToRequester(ICharacter requester, MessageBody body)
        {
            if (requester == null || body == null)
            {
                return;
            }

            var zoneClient = requester.Controller != null
                                 ? requester.Controller.Client as ZoneClient
                                 : null;
            if (zoneClient != null && requester.Playfield != null)
            {
                requester.Playfield.Send(zoneClient, body);
                return;
            }

            if (zoneClient != null)
            {
                zoneClient.SendCompressed(body);
                return;
            }

            requester.Send(body);
        }

        private static IEnumerable<ICharacter> EnumerateOnlinePlayers()
        {
            foreach (ICharacter candidate in Pool.Instance.GetAll<ICharacter>((int)IdentityType.CanbeAffected))
            {
                if (candidate == null || candidate.Controller == null || candidate.Controller.Client == null)
                {
                    continue;
                }

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
