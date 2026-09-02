namespace ZoneEngine.Core
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Packets;

    using Vector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;

    /// <summary>
    /// Cross-zone LFT Invite: client needs id→name dynel or it shows "NoName" and first
    /// Invite click often sends nothing. Seed viewer-only off-map SCFU + classic InfoPacket.
    /// HP/NP via CharacterInfoPacket only within 20m on the same playfield — never whole-zone.
    /// No Despawn, no ExchangeOnline ghosts, no InfoPacket Unknown=1 hacks.
    /// Same-PF: skip SCFU seed (real dynel already present). LFT already filters XP window.
    /// </summary>
    public static class LftInviteClientPresence
    {
        public const string LftSeedCommandPrefix = "#aorebirth-lft-seed";

        private const float OffMapY = -250000f;
        private const double VitalVisibilityRangeMeters = 20.0;

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

        /// <summary>
        /// LFT invite HP/NP: same playfield and within live-AO ~20m visibility — not whole zone.
        /// </summary>
        public static bool IsWithinVitalVisibilityRange(ICharacter viewer, ICharacter subject)
        {
            if (viewer == null || subject == null || IsRemoteFrom(viewer, subject))
            {
                return false;
            }

            var viewerDynel = viewer as Dynel;
            var subjectDynel = subject as Dynel;
            if (viewerDynel == null || subjectDynel == null)
            {
                return false;
            }

            try
            {
                Coordinate viewerPos = new AORebirth.Core.Vector.Coordinate(viewerDynel.Position);
                Coordinate subjectPos = new AORebirth.Core.Vector.Coordinate(subjectDynel.Position);
                if (viewerPos == null || subjectPos == null)
                {
                    return false;
                }

                return Coordinate.Distance2D(viewerPos, subjectPos) <= VitalVisibilityRangeMeters;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void ExchangeOnlinePresence(ICharacter character)
        {
            // Intentionally empty — login SCFU exchange caused visible doubles.
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

        /// <summary>
        /// Client Recruit/LFT TooHigh reads target level from dynel stat 54 (see AOSharp
        /// SimpleChar.Level → GetStat(Stat.Level)). Level is not playfield-announced in bulk
        /// stat sync — wire it whenever we seed invite visibility.
        /// </summary>
        public static void WireInviteLevelStatToViewer(ICharacter viewer, ICharacter subject)
        {
            if (viewer == null || subject == null || viewer.Identity.Instance == subject.Identity.Instance)
            {
                return;
            }

            int level = CombatXpRuntimeService.ResolveWireLevel(subject);
            if (level < 1)
            {
                level = 1;
            }
            else if (level > 220)
            {
                level = 220;
            }

            try
            {
                SendToRequester(
                    viewer,
                    new StatMessage
                    {
                        Identity = subject.Identity,
                        Unknown = 0,
                        Stats = new[]
                                  {
                                      new GameTuple<CharacterStat, uint>
                                      {
                                          Value1 = (CharacterStat)(int)StatIds.level,
                                          Value2 = (uint)level
                                      }
                                  }
                    });
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "LFT level-stat wire failed subject=" + subject.Identity.ToString(true)
                    + " err=" + ex.Message);
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
        /// Cross-PF: name via off-map SCFU (no HP/NP). Same-PF: CharacterInfoPacket with
        /// real level for Recruit/LFT (always). HP/NP only meaningful within ~20m on live AO.
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

            int remoteLevel = ReadLevel(remote);
            int infoLevel = remoteLevel;

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

            bool crossPlayfield = IsRemoteFrom(requester, remote);
            WireInviteLevelStatToViewer(requester, remote);
            if (!crossPlayfield)
            {
                SendTeamInviteVitals(requester, remote, displayName, infoLevel);
                SendSamePlayfieldScfuLevelPatch(requester, remote, displayName, infoLevel);
                return;
            }

            var remoteChar = remote as Character;
            if (remoteChar == null)
            {
                return;
            }

            try
            {
                SimpleCharFullUpdateMessage scfu = SimpleCharFullUpdate.ConstructMessage(remoteChar);
                scfu.Identity = new Identity
                {
                    Type = IdentityType.CanbeAffected,
                    Instance = remote.Identity.Instance
                };
                scfu.PlayfieldId = remote.Playfield != null
                        ? remote.Playfield.Identity.Instance
                        : requester.Playfield.Identity.Instance;

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

                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "LFT seed name=" + displayName
                    + " infoLvl=" + infoLevel
                    + " remoteLvl=" + remoteLevel
                    + " reqPf="
                    + requester.Playfield.Identity.Instance
                    + " remPf="
                    + (remote.Playfield != null ? remote.Playfield.Identity.Instance.ToString() : "?"));
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "LFT name seed failed name=" + displayName + " err=" + ex.Message);
            }
        }

        private static void SendSamePlayfieldScfuLevelPatch(
            ICharacter requester,
            ICharacter remote,
            string displayName,
            int level)
        {
            var remoteChar = remote as Character;
            if (remoteChar == null || requester == null)
            {
                return;
            }

            try
            {
                SimpleCharFullUpdateMessage scfu = SimpleCharFullUpdate.ConstructMessage(remoteChar);
                scfu.Level = (short)level;
                scfu.Name = displayName;
                scfu.CharacterFlags |= CharacterFlags.HasVisibleName;
                SendToRequester(requester, scfu);
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "LFT same-PF level patch failed name=" + displayName + " err=" + ex.Message);
            }
        }

        private static void SendTeamInviteVitals(
            ICharacter requester,
            ICharacter remote,
            string displayName,
            int infoLevel)
        {
            try
            {
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
                    "LFT vital seed failed name=" + displayName + " err=" + ex.Message);
            }
        }

        public static void QuietXpLevelOnViewer(ICharacter viewer, ICharacter subject)
        {
            // Unused — level handled in SCFU/Info seed above.
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
            return CombatXpRuntimeService.ResolveWireLevel(character);
        }
    }
}
