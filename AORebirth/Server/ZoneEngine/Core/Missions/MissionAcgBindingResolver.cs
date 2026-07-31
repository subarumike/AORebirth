namespace ZoneEngine.Core.Missions
{
    using System;
    using System.Collections.Generic;

    internal static class MissionAcgBindingResolver
    {
        internal static bool TryResolveByKey(
            IEnumerable<MissionAcgBindingRecord> records,
            int ownerInstance,
            int missionKeyInstance,
            DateTime nowUtc,
            out MissionAcgBindingRecord resolved)
        {
            resolved = null;
            if (missionKeyInstance == 0)
            {
                return false;
            }

            foreach (MissionAcgBindingRecord record in records)
            {
                if (!IsAccessible(record, ownerInstance, nowUtc)
                    || record.Binding.MissionKeyIdentity.Instance
                       != missionKeyInstance)
                {
                    continue;
                }

                if (resolved != null)
                {
                    resolved = null;
                    return false;
                }

                resolved = record;
            }

            return resolved != null;
        }

        internal static bool TryResolveByExteriorMarker(
            IEnumerable<MissionAcgBindingRecord> records,
            int ownerInstance,
            int exteriorPlayfieldInstance,
            double x,
            double y,
            double z,
            double horizontalRadius,
            double verticalRadius,
            DateTime nowUtc,
            out MissionAcgBindingRecord resolved)
        {
            resolved = null;
            double radiusSquared = horizontalRadius * horizontalRadius;
            foreach (MissionAcgBindingRecord record in records)
            {
                if (!IsAccessible(record, ownerInstance, nowUtc))
                {
                    continue;
                }

                MissionAcgInstanceBinding binding = record.Binding;
                double dx = x - binding.ExteriorX;
                double dz = z - binding.ExteriorZ;
                if (binding.ExteriorEntranceIdentity.Instance
                    != exteriorPlayfieldInstance
                    || ((dx * dx) + (dz * dz)) > radiusSquared
                    || Math.Abs(y - binding.ExteriorY) > verticalRadius)
                {
                    continue;
                }

                if (resolved != null)
                {
                    resolved = null;
                    return false;
                }

                resolved = record;
            }

            return resolved != null;
        }

        internal static bool HasOwnedExteriorMarker(
            IEnumerable<MissionAcgBindingRecord> records,
            int ownerInstance,
            int exteriorPlayfieldInstance,
            double x,
            double y,
            double z,
            double horizontalRadius,
            double verticalRadius)
        {
            double radiusSquared = horizontalRadius * horizontalRadius;
            foreach (MissionAcgBindingRecord record in records)
            {
                if (record == null
                    || record.Binding == null
                    || !record.State.ReservesPlayfield
                    || record.Binding.OwnerIdentity.Instance != ownerInstance)
                {
                    continue;
                }

                MissionAcgInstanceBinding binding = record.Binding;
                double dx = x - binding.ExteriorX;
                double dz = z - binding.ExteriorZ;
                if (binding.ExteriorEntranceIdentity.Instance
                    == exteriorPlayfieldInstance
                    && ((dx * dx) + (dz * dz)) <= radiusSquared
                    && Math.Abs(y - binding.ExteriorY) <= verticalRadius)
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsAccessible(
            MissionAcgBindingRecord record,
            int ownerInstance,
            DateTime nowUtc)
        {
            return record != null
                   && record.Binding.ExplicitNoTeam
                   && record.Binding.TeamIdentity == null
                   && record.Binding.OwnerIdentity.Instance == ownerInstance
                   && record.State.CanEnter(nowUtc, record.Binding.ExpiryUtc);
        }
    }
}
