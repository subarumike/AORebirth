namespace ZoneEngine.Core
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Packets;

    using Vector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;

    /// <summary>
    /// Kill client XP "too high" invite dialog. InfoPacket Level=1 alone is NOT enough
    /// when the target is visible (log 06:00 infoLvl=1 still warned). Client uses dynel Level.
    /// Push Stat Level=1 on the target identity to the viewer, plus SCFU/InfoPacket Level=1.
    /// </summary>
    public static class LftInviteClientPresence
    {
        public const string LftSeedCommandPrefix = "#aorebirth-lft-seed";

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
        /// Force viewer-side Level=1 for target (Stat + SCFU + InfoPacket). No Despawn. Never invites.
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

            const int infoLevel = 1;

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

            try
            {
                // Dynel Level is what the client uses for the warn (InfoPacket Level=1 was ignored).
                SendToRequester(
                    requester,
                    new StatMessage
                    {
                        Identity = remote.Identity,
                        Unknown = 0,
                        Stats =
                            new[]
                            {
                                new GameTuple<CharacterStat, uint>
                                {
                                    Value1 = (CharacterStat)StatIds.level,
                                    Value2 = 1
                                },
                                new GameTuple<CharacterStat, uint>
                                {
                                    Value1 = (CharacterStat)StatIds.titlelevel,
                                    Value2 = 1
                                }
                            }
                    });

                bool remotePf = IsRemoteFrom(requester, remote);
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

                if (remotePf)
                {
                    scfu.Coordinates = new Vector3 { X = 0f, Y = OffMapY, Z = 0f };
                }
                else if (remote.Coordinates != null)
                {
                    scfu.Coordinates = new Vector3
                    {
                        X = remote.Coordinates.x,
                        Y = remote.Coordinates.y,
                        Z = remote.Coordinates.z
                    };
                }

                var pcInfo = scfu.CharacterInfo as SimplePcInfo;
                if (pcInfo != null)
                {
                    pcInfo.FirstName = displayName;
                    pcInfo.LastName = string.Empty;
                }

                SendToRequester(requester, scfu);
                if (remotePf)
                {
                    SendToRequester(
                        requester,
                        new CharInPlayMessage { Identity = scfu.Identity, Unknown = 0x00 });
                }

                CharacterInfoPacketMessageHandler.Default.SendForTeamInvite(
                    requester,
                    remote,
                    displayName,
                    infoLevel);

                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "LFT suppress-warn name=" + displayName
                    + " infoLvl=1 via Stat+SCFU"
                    + " remotePf=" + remotePf
                    + " reqPf=" + requester.Playfield.Identity.Instance
                    + " remPf="
                    + (remote.Playfield != null ? remote.Playfield.Identity.Instance.ToString() : "?"));
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "LFT suppress-warn failed name=" + displayName + " err=" + ex.Message);
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
    }
}
