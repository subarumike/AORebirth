#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core.Functions.GameFunctions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Packets;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    #endregion

    /// <summary>
    /// Beacon Warp / Team Beacon Warp (154914 / 154913).
    /// Engineer casts → warp team member(s) to caster from any Rubi-Ka playfield.
    /// Same-PF capture 20260808-Warp-single / 20260808-warp-team: SetPos + SCFU (~2m).
    /// Cross-PF RK: Playfield.Teleport into caster playfield at landing coords.
    /// Refused: Shadowlands, TotW (647/1931), Subway/Foreman's (127).
    /// </summary>
    internal static class TeamWarpRuntime
    {
        internal const float CapturedLandingOffsetMeters = 2.0f;

        internal const int BeaconWarpSelectedNanoId = 154914;

        internal const int TeamBeaconWarpNanoId = 154913;

        /// <summary>Subway (Strike Foreman / Foreman's).</summary>
        private const int RestrictedSubwayPlayfieldId = 127;

        /// <summary>Temple of Three Winds (legacy / inner).</summary>
        private const int RestrictedTotwLegacyPlayfieldId = 647;

        /// <summary>Temple of Three Winds (main).</summary>
        private const int RestrictedTotwPlayfieldId = 1931;

        private static readonly HashSet<int> RestrictedRubiKaPlayfieldIds =
            new HashSet<int>
            {
                RestrictedSubwayPlayfieldId,
                RestrictedTotwLegacyPlayfieldId,
                RestrictedTotwPlayfieldId
            };

        internal static bool IsBeaconWarpNano(int nanoId)
        {
            return nanoId == BeaconWarpSelectedNanoId || nanoId == TeamBeaconWarpNanoId;
        }

        internal static bool IsRubiKaPlayfieldId(int playfieldId)
        {
            return playfieldId > 0
                   && !AdventurerMorphFlightRuntime.IsShadowlandsPlayfield(playfieldId);
        }

        internal static bool IsRestrictedRubiKaPlayfieldId(int playfieldId)
        {
            return RestrictedRubiKaPlayfieldIds.Contains(playfieldId);
        }

        internal static bool TryRefuseShadowlands(ICharacter caster)
        {
            if (caster == null)
            {
                return true;
            }

            Playfield casterPlayfield = ResolveCharacterPlayfield(caster);
            if (casterPlayfield == null)
            {
                return true;
            }

            int playfieldId = casterPlayfield.Identity.Instance;
            if (IsRestrictedRubiKaPlayfieldId(playfieldId))
            {
                ChatTextMessageHandler.Default.Send(
                    caster,
                    "Beacon Warp does not work in this playfield (TotW / Foreman's / Subway).");
                return true;
            }

            if (IsRubiKaPlayfieldId(playfieldId)
                && caster.Stats[StatIds.expansionplayfield].Value == 0)
            {
                return false;
            }

            ChatTextMessageHandler.Default.Send(
                caster,
                "Beacon Warp only works on Rubi-Ka (not in Shadowlands).");
            return true;
        }

        internal static bool TryWarpTeamMemberToCaster(
            ICharacter caster,
            ICharacter member,
            int slotIndex,
            string evidenceTag)
        {
            if (caster == null || member == null)
            {
                return false;
            }

            if (member.Identity.Instance == caster.Identity.Instance)
            {
                return false;
            }

            Playfield casterPlayfield = ResolveCharacterPlayfield(caster);
            if (casterPlayfield == null)
            {
                return false;
            }

            // Keep Character.Playfield wired for downstream callers.
            if (caster.Playfield == null
                || caster.Playfield.Identity.Instance != casterPlayfield.Identity.Instance)
            {
                caster.Playfield = casterPlayfield;
            }

            int casterPfId = casterPlayfield.Identity.Instance;
            if (!IsRubiKaPlayfieldId(casterPfId))
            {
                ChatTextMessageHandler.Default.Send(
                    caster,
                    "Beacon Warp only works on Rubi-Ka (not in Shadowlands).");
                return false;
            }

            if (IsRestrictedRubiKaPlayfieldId(casterPfId))
            {
                ChatTextMessageHandler.Default.Send(
                    caster,
                    "Beacon Warp does not work in this playfield (TotW / Foreman's / Subway).");
                return false;
            }

            Playfield memberPlayfield = ResolveCharacterPlayfield(member);
            int memberPfId = memberPlayfield != null ? memberPlayfield.Identity.Instance : 0;
            if (memberPfId > 0 && !IsRubiKaPlayfieldId(memberPfId))
            {
                ChatTextMessageHandler.Default.Send(
                    caster,
                    "Beacon Warp: teammate is in Shadowlands and cannot be warped.");
                return false;
            }

            if (memberPfId > 0 && IsRestrictedRubiKaPlayfieldId(memberPfId))
            {
                ChatTextMessageHandler.Default.Send(
                    caster,
                    "Beacon Warp: teammate is in a restricted playfield (TotW / Foreman's / Subway).");
                return false;
            }

            Coordinate landing = ComputeLandingCoordinate(caster, slotIndex);
            IQuaternion heading = caster.Rotation ?? member.Rotation ?? new Quaternion(0, 0, 0, 1);
            var headingConcrete = new Quaternion(heading.xf, heading.yf, heading.zf, heading.wf);

            try
            {
                bool samePlayfield = memberPlayfield != null
                                     && memberPlayfield.Identity.Instance == casterPlayfield.Identity.Instance;
                if (!samePlayfield)
                {
                    samePlayfield = IsSamePlayfield(caster, member, casterPlayfield);
                }

                if (samePlayfield)
                {
                    return WarpSamePlayfield(
                        caster,
                        member,
                        casterPlayfield,
                        landing,
                        headingConcrete,
                        slotIndex,
                        evidenceTag);
                }

                return WarpAcrossRubiKaPlayfields(
                    caster,
                    member,
                    casterPlayfield,
                    memberPlayfield,
                    landing,
                    headingConcrete,
                    slotIndex,
                    evidenceTag);
            }
            catch (Exception ex)
            {
                LogUtil.ErrorException(ex);
                return false;
            }
        }

        internal static bool TryGetTeamRoster(ICharacter caster, out List<Identity> members)
        {
            return TeamRuntime.TryGetTeamMembers(caster, out members)
                   && members != null
                   && members.Count > 0;
        }

        /// <summary>
        /// Find online teammate anywhere (any Rubi-Ka playfield).
        /// </summary>
        internal static ICharacter FindOnlineTeamMember(Identity identity)
        {
            if (identity.Instance == 0)
            {
                return null;
            }

            int want = identity.Instance;
            uint wantU = unchecked((uint)want);

            // Prefer characters already attached to a ZoneClient (reliable playfield).
            if (Program.zoneServer != null)
            {
                foreach (KeyValuePair<Identity, string> entry in
                    Program.zoneServer.ListAvailablePlayfields(false))
                {
                    Playfield pf = Program.zoneServer.PlayfieldById(entry.Key) as Playfield;
                    if (pf == null)
                    {
                        continue;
                    }

                    foreach (ICharacter candidate in pf.EnumerateActiveCharacters())
                    {
                        if (candidate == null || candidate.Identity.Instance == 0)
                        {
                            continue;
                        }

                        if (unchecked((uint)candidate.Identity.Instance) != wantU)
                        {
                            continue;
                        }

                        if (candidate.Controller is PlayerController
                            || (candidate.Controller != null && candidate.Controller.Client != null))
                        {
                            return candidate;
                        }
                    }
                }
            }

            foreach (ICharacter candidate in Pool.Instance.GetAll<ICharacter>((int)IdentityType.CanbeAffected))
            {
                if (candidate == null || candidate.Identity.Instance == 0)
                {
                    continue;
                }

                if (unchecked((uint)candidate.Identity.Instance) != wantU)
                {
                    continue;
                }

                if (candidate.Controller is PlayerController)
                {
                    return candidate;
                }

                if (candidate.Controller != null && candidate.Controller.Client != null)
                {
                    return candidate;
                }
            }

            return Pool.Instance.GetObject<ICharacter>(
                new Identity { Type = IdentityType.CanbeAffected, Instance = want });
        }

        internal static Coordinate ComputeLandingCoordinate(ICharacter caster, int slotIndex)
        {
            Coordinate casterPos = caster.CalculatePredictedPosition();
            IQuaternion heading = caster.Rotation;
            float angle = slotIndex * 0.7f;
            float localX = CapturedLandingOffsetMeters * (float)Math.Sin(angle);
            float localZ = CapturedLandingOffsetMeters * (float)Math.Cos(angle);

            if (heading == null)
            {
                return new Coordinate(casterPos.x + localX, casterPos.y, casterPos.z + localZ);
            }

            IVector3 offset = Quaternion.RotateVector3(
                heading,
                new Vector3 { x = localX, y = 0f, z = localZ });
            return new Coordinate(
                casterPos.x + offset.xf,
                casterPos.y,
                casterPos.z + offset.zf);
        }

        /// <summary>
        /// Resolve the playfield the character is actually on.
        /// Character.Playfield Pool lookup often returns null even when online;
        /// ZoneClient.Playfield and ZoneServer enumeration are authoritative.
        /// </summary>
        private static Playfield ResolveCharacterPlayfield(ICharacter character)
        {
            if (character == null)
            {
                return null;
            }

            ZoneClient zoneClient = character.Controller != null
                                        ? character.Controller.Client as ZoneClient
                                        : null;
            if (zoneClient != null)
            {
                var clientPf = zoneClient.Playfield as Playfield;
                if (clientPf != null)
                {
                    return clientPf;
                }
            }

            var direct = character.Playfield as Playfield;
            if (direct != null)
            {
                return direct;
            }

            if (Program.zoneServer == null)
            {
                return null;
            }

            int want = character.Identity.Instance;
            foreach (KeyValuePair<Identity, string> entry in
                Program.zoneServer.ListAvailablePlayfields(false))
            {
                Playfield pf = Program.zoneServer.PlayfieldById(entry.Key) as Playfield;
                if (pf == null)
                {
                    continue;
                }

                foreach (ICharacter candidate in pf.EnumerateActiveCharacters())
                {
                    if (candidate != null && candidate.Identity.Instance == want)
                    {
                        return pf;
                    }
                }

                if (TryPoolContainsCharacter(pf.Identity, want))
                {
                    return pf;
                }

                Identity alt = AlternatePlayfieldIdentity(pf.Identity);
                if (!alt.Equals(Identity.None) && TryPoolContainsCharacter(alt, want))
                {
                    return pf;
                }
            }

            return null;
        }

        private static bool TryPoolContainsCharacter(Identity playfieldIdentity, int characterInstance)
        {
            try
            {
                var typed = new Identity
                            {
                                Type = IdentityType.CanbeAffected,
                                Instance = characterInstance
                            };
                return Pool.Instance.GetObject<ICharacter>(playfieldIdentity, typed) != null;
            }
            catch
            {
                return false;
            }
        }

        private static bool WarpSamePlayfield(
            ICharacter caster,
            ICharacter member,
            Playfield playfield,
            Coordinate landing,
            Quaternion headingConcrete,
            int slotIndex,
            string evidenceTag)
        {
            if (member.Playfield == null
                || member.Playfield.Identity.Instance != playfield.Identity.Instance)
            {
                member.Playfield = playfield;
            }

            ZoneClient memberClient = member.Controller != null
                                          ? member.Controller.Client as ZoneClient
                                          : null;
            if (memberClient != null)
            {
                memberClient.Playfield = playfield;
            }

            var landingSmoke = new SmokeLounge.AOtomation.Messaging.GameData.Vector3
                               {
                                   X = landing.x,
                                   Y = landing.y,
                                   Z = landing.z
                               };

            // Capture same-PF: Despawn → SetPos/SCFU → Appearance near caster (~2m).
            playfield.Despawn(member.Identity);

            member.SetCoordinates(landing, headingConcrete);
            Character memberConcrete = member as Character;
            if (memberConcrete != null)
            {
                memberConcrete.Position = new Vector3(landing.x, landing.y, landing.z);
                memberConcrete.Transform.Rotation = headingConcrete;
            }

            playfield.Announce(
                new SetPosMessage
                {
                    Identity = member.Identity,
                    Coordinates = landingSmoke,
                    Unknown1 = 1
                });

            playfield.RefreshCharacterVisibility(member, forceRefresh: true);
            AppearanceUpdateMessageHandler.Default.Send(member);

            if (memberClient != null)
            {
                SimpleCharFullUpdate.SendToOne(member, memberClient);
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} same-pf caster={1} member={2} slot={3} landing=({4:F2},{5:F2},{6:F2})",
                    evidenceTag,
                    caster.Identity.ToString(true),
                    member.Identity.ToString(true),
                    slotIndex,
                    landing.x,
                    landing.y,
                    landing.z));
            return true;
        }

        private static bool WarpAcrossRubiKaPlayfields(
            ICharacter caster,
            ICharacter member,
            Playfield casterPlayfield,
            Playfield memberPlayfield,
            Coordinate landing,
            Quaternion headingConcrete,
            int slotIndex,
            string evidenceTag)
        {
            if (memberPlayfield == null)
            {
                memberPlayfield = ResolveCharacterPlayfield(member);
            }

            if (memberPlayfield == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} cross-pf aborted: could not resolve member playfield caster={1} member={2}",
                        evidenceTag,
                        caster.Identity.ToString(true),
                        member.Identity.ToString(true)));
                return false;
            }

            Dynel memberDynel = member as Dynel;
            if (memberDynel == null)
            {
                return false;
            }

            // Dynel.Playfield Pool getter is often null; wire both Character and ZoneClient.
            member.Playfield = memberPlayfield;
            ZoneClient memberClient = member.Controller != null
                                          ? member.Controller.Client as ZoneClient
                                          : null;
            if (memberClient != null)
            {
                memberClient.Playfield = memberPlayfield;
            }

            // Playfield.Teleport no-ops when DoNotDoTimers is true.
            if (memberDynel.DoNotDoTimers)
            {
                memberDynel.DoNotDoTimers = false;
            }

            string fromPf = memberPlayfield.Identity.ToString(true);
            Identity destinationPlayfield = casterPlayfield.Identity;

            // Call Teleport on the resolved source playfield (not via Dynel getter).
            memberPlayfield.Teleport(memberDynel, landing, headingConcrete, destinationPlayfield);

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} cross-pf caster={1} destPf={2} member={3} fromPf={4} slot={5} landing=({6:F2},{7:F2},{8:F2})",
                    evidenceTag,
                    caster.Identity.ToString(true),
                    destinationPlayfield.ToString(true),
                    member.Identity.ToString(true),
                    fromPf,
                    slotIndex,
                    landing.x,
                    landing.y,
                    landing.z));
            return true;
        }

        private static bool IsSamePlayfield(ICharacter caster, ICharacter member, Playfield playfield)
        {
            if (caster == null || member == null || playfield == null)
            {
                return false;
            }

            int pfInstance = playfield.Identity.Instance;
            Playfield memberPf = ResolveCharacterPlayfield(member);
            if (memberPf != null && memberPf.Identity.Instance == pfInstance)
            {
                return true;
            }

            foreach (ICharacter candidate in playfield.EnumerateActiveCharacters())
            {
                if (candidate != null && candidate.Identity.Instance == member.Identity.Instance)
                {
                    return true;
                }
            }

            if (TryPoolContainsCharacter(playfield.Identity, member.Identity.Instance))
            {
                return true;
            }

            Identity altParent = AlternatePlayfieldIdentity(playfield.Identity);
            if (!altParent.Equals(Identity.None)
                && TryPoolContainsCharacter(altParent, member.Identity.Instance))
            {
                return true;
            }

            return false;
        }

        private static Identity AlternatePlayfieldIdentity(Identity playfieldIdentity)
        {
            if (playfieldIdentity.Instance == 0)
            {
                return Identity.None;
            }

            if (playfieldIdentity.Type == IdentityType.Playfield2)
            {
                return new Identity { Type = IdentityType.Playfield, Instance = playfieldIdentity.Instance };
            }

            if (playfieldIdentity.Type == IdentityType.Playfield)
            {
                return new Identity { Type = IdentityType.Playfield2, Instance = playfieldIdentity.Instance };
            }

            return Identity.None;
        }
    }
}
