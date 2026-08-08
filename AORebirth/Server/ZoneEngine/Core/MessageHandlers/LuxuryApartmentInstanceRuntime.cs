namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Controllers;

    #endregion

    /// <summary>
    /// Capture 20260806-220142 / 20260806-213039:
    /// Each access card (281129) owns a private apartment playfield instance.
    /// Strangers cannot share an instance. Team members may join only when the
    /// team leader is already inside their apartment.
    /// </summary>
    public static class LuxuryApartmentInstanceRuntime
    {
        private static readonly object Sync = new object();

        // Capture base: first apartment 0x19E000 / building 0x5E3820; later 0x1A8801 / 0x5E3821.
        private const int PlayfieldBase = 0x0019E000;

        private const int BuildingBase = 0x005E3820;

        private const int MaxSlots = 0x400;

        private static int nextSlot;

        private static readonly Dictionary<int, ApartmentLease> LeaseByOwner =
            new Dictionary<int, ApartmentLease>();

        private static readonly Dictionary<int, ApartmentLease> LeaseByPlayfield =
            new Dictionary<int, ApartmentLease>();

        private sealed class ApartmentLease
        {
            public int OwnerCharacterInstance;

            public int PlayfieldId;

            public int BuildingInstance;
        }

        /// <summary>
        /// True for any Rebirth-allocated apartment instance in the 0x19E000 band.
        /// Must not depend on in-memory leases — characters log in with a saved PF id
        /// after ZoneEngine restart (leases are empty).
        /// </summary>
        public static bool IsLuxuryApartmentPlayfield(int playfieldInstance)
        {
            return IsAllocatedApartmentBand(playfieldInstance);
        }

        public static bool TryGetBuildingInstance(int playfieldInstance, out int buildingInstance)
        {
            buildingInstance = LuxuryApartmentSunriseRules.LuxuryApartmentBuildingInstance;
            if (!IsAllocatedApartmentBand(playfieldInstance))
            {
                return false;
            }

            lock (Sync)
            {
                ApartmentLease lease;
                if (LeaseByPlayfield.TryGetValue(playfieldInstance, out lease) && lease != null)
                {
                    buildingInstance = lease.BuildingInstance;
                    return true;
                }
            }

            buildingInstance = BuildingBase + (playfieldInstance - PlayfieldBase);
            return true;
        }

        /// <summary>
        /// Re-bind owner → playfield after login into a saved apartment instance.
        /// Without this, restart clears leases and the next lobby entry allocates a new PF.
        /// </summary>
        public static void RehydrateLeaseFromLogin(ICharacter character)
        {
            if (character == null || character.Playfield == null)
            {
                return;
            }

            int playfieldId = character.Playfield.Identity.Instance;
            if (!IsAllocatedApartmentBand(playfieldId))
            {
                return;
            }

            int ownerId = character.Identity.Instance;
            int buildingInstance = BuildingBase + (playfieldId - PlayfieldBase);
            lock (Sync)
            {
                ApartmentLease existingOwner;
                if (LeaseByOwner.TryGetValue(ownerId, out existingOwner)
                    && existingOwner != null
                    && existingOwner.PlayfieldId != playfieldId)
                {
                    LeaseByPlayfield.Remove(existingOwner.PlayfieldId);
                }

                var lease = new ApartmentLease
                            {
                                OwnerCharacterInstance = ownerId,
                                PlayfieldId = playfieldId,
                                BuildingInstance = buildingInstance
                            };
                LeaseByOwner[ownerId] = lease;
                LeaseByPlayfield[playfieldId] = lease;
            }

            LogUtil.Debug(
                DebugInfoDetail.Zoning,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "LuxuryApartment rehydrate login char={0} pf={1} building={2:X}",
                    character.Identity.ToString(true),
                    playfieldId,
                    buildingInstance));
        }

        private static bool IsAllocatedApartmentBand(int playfieldInstance)
        {
            return playfieldInstance >= PlayfieldBase
                   && playfieldInstance < PlayfieldBase + MaxSlots;
        }

        /// <summary>
        /// Resolve destination apartment for entry: team-leader occupied instance, else personal lease.
        /// </summary>
        public static bool TryResolveEntryDestination(
            ICharacter character,
            out int playfieldId,
            out int buildingInstance)
        {
            playfieldId = 0;
            buildingInstance = 0;
            if (character == null)
            {
                return false;
            }

            if (TryResolveTeamLeaderOccupiedApartment(character, out playfieldId, out buildingInstance))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Zoning,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "LuxuryApartment team-join char={0} destPf={1} building={2:X} evidence=20260806-220142",
                        character.Identity.ToString(true),
                        playfieldId,
                        buildingInstance));
                return true;
            }

            return EnsurePersonalApartment(character, out playfieldId, out buildingInstance);
        }

        public static bool EnsurePersonalApartment(
            ICharacter character,
            out int playfieldId,
            out int buildingInstance)
        {
            playfieldId = 0;
            buildingInstance = 0;
            if (character == null)
            {
                return false;
            }

            int ownerId = character.Identity.Instance;
            lock (Sync)
            {
                ApartmentLease lease;
                if (LeaseByOwner.TryGetValue(ownerId, out lease) && lease != null)
                {
                    playfieldId = lease.PlayfieldId;
                    buildingInstance = lease.BuildingInstance;
                    return true;
                }

                lease = AllocateNewLease(ownerId);
                LeaseByOwner[ownerId] = lease;
                LeaseByPlayfield[lease.PlayfieldId] = lease;
                playfieldId = lease.PlayfieldId;
                buildingInstance = lease.BuildingInstance;
            }

            LogUtil.Debug(
                DebugInfoDetail.Zoning,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "LuxuryApartment personal lease char={0} pf={1} building={2:X} evidence=20260806-220142",
                    character.Identity.ToString(true),
                    playfieldId,
                    buildingInstance));
            return true;
        }

        private static bool TryResolveTeamLeaderOccupiedApartment(
            ICharacter character,
            out int playfieldId,
            out int buildingInstance)
        {
            playfieldId = 0;
            buildingInstance = 0;

            List<Identity> members;
            if (!TeamRuntime.TryGetTeamMembers(character, out members) || members == null)
            {
                return false;
            }

            ICharacter leader = null;
            foreach (Identity memberId in members)
            {
                ICharacter member = ResolveOnlineCharacter(memberId);
                if (member == null)
                {
                    continue;
                }

                // Capture-backed team: leader SocialStatus=7, member=5.
                if (member.Stats[StatIds.socialstatus].Value == 7)
                {
                    leader = member;
                    break;
                }
            }

            if (leader == null || leader.Identity.Instance == character.Identity.Instance)
            {
                return false;
            }

            if (leader.Playfield == null
                || !IsLuxuryApartmentPlayfield(leader.Playfield.Identity.Instance))
            {
                // Leader must enter first.
                return false;
            }

            playfieldId = leader.Playfield.Identity.Instance;
            return TryGetBuildingInstance(playfieldId, out buildingInstance);
        }

        private static ApartmentLease AllocateNewLease(int ownerCharacterInstance)
        {
            for (int n = 0; n < MaxSlots; n++)
            {
                nextSlot = (nextSlot + 1) % MaxSlots;
                int playfieldId = PlayfieldBase + nextSlot;
                int buildingInstance = BuildingBase + nextSlot;
                if (LeaseByPlayfield.ContainsKey(playfieldId))
                {
                    continue;
                }

                return new ApartmentLease
                       {
                           OwnerCharacterInstance = ownerCharacterInstance,
                           PlayfieldId = playfieldId,
                           BuildingInstance = buildingInstance
                       };
            }

            // Extremely unlikely collision fallback: salt with owner id.
            int salted = PlayfieldBase + (Math.Abs(ownerCharacterInstance) % MaxSlots);
            while (LeaseByPlayfield.ContainsKey(salted))
            {
                salted++;
            }

            return new ApartmentLease
                   {
                       OwnerCharacterInstance = ownerCharacterInstance,
                       PlayfieldId = salted,
                       BuildingInstance = BuildingBase + (salted - PlayfieldBase)
                   };
        }

        private static ICharacter ResolveOnlineCharacter(Identity identity)
        {
            try
            {
                ICharacter fromPool = Pool.Instance.GetObject<ICharacter>(identity);
                if (fromPool != null)
                {
                    return fromPool;
                }
            }
            catch
            {
            }

            return null;
        }
    }
}
