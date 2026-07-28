namespace ZoneEngine.Core.Missions
{
    using System;

    internal sealed class MissionAcgEntryPlan
    {
        internal MissionAcgEntryPlan(
            MissionAcgBindingRecord record,
            MissionAcgLayoutBundle bundle)
        {
            if (record == null || bundle == null)
            {
                throw new ArgumentNullException(record == null ? "record" : "bundle");
            }

            this.Record = record;
            this.AcceptedQuestIdentity = record.Binding.AcceptedQuestIdentity;
            this.MissionKeyIdentity = record.Binding.MissionKeyIdentity;
            this.BuildingIdentity = record.Binding.AcgBuildingIdentity;
            this.AllocatedLivePlayfield2 = record.Binding.AllocatedLivePlayfield2;
            this.BundleId = bundle.LayoutId;
            this.GeneratorPayloadSha256 = bundle.GeneratorPayloadSha256;
            this.GeneratorPayload = bundle.CopyGeneratorPayload();
        }

        internal MissionAcgBindingRecord Record { get; private set; }

        internal MissionAcgIdentityRecord AcceptedQuestIdentity { get; private set; }

        internal MissionAcgIdentityRecord MissionKeyIdentity { get; private set; }

        internal MissionAcgIdentityRecord BuildingIdentity { get; private set; }

        internal int AllocatedLivePlayfield2 { get; private set; }

        internal string BundleId { get; private set; }

        internal string GeneratorPayloadSha256 { get; private set; }

        internal byte[] GeneratorPayload { get; private set; }
    }

    internal static class MissionAcgEntryResolver
    {
        internal static bool TryResolveByKey(
            int ownerInstance,
            int missionKeyInstance,
            DateTime nowUtc,
            out MissionAcgEntryPlan plan)
        {
            MissionAcgBindingRecord record;
            if (!MissionAcgBindingRuntime.TryResolveByMissionKey(
                ownerInstance,
                missionKeyInstance,
                nowUtc,
                out record))
            {
                plan = null;
                return false;
            }

            return TryCreatePlan(record, out plan);
        }

        internal static bool TryResolveByEntrance(
            int ownerInstance,
            MissionAcgIdentityRecord exteriorEntrance,
            int entranceLow,
            int entranceHigh,
            DateTime nowUtc,
            out MissionAcgEntryPlan plan)
        {
            MissionAcgBindingRecord record;
            if (!MissionAcgBindingRuntime.TryResolveByEntrance(
                ownerInstance,
                exteriorEntrance,
                entranceLow,
                entranceHigh,
                nowUtc,
                out record))
            {
                plan = null;
                return false;
            }

            return TryCreatePlan(record, out plan);
        }

        internal static bool TryCreatePlan(
            MissionAcgBindingRecord record,
            out MissionAcgEntryPlan plan)
        {
            plan = null;
            if (record == null)
            {
                return false;
            }

            MissionAcgLayoutBundle bundle =
                MissionAcgBindingRuntime.Catalog.FindByLayoutId(
                    record.Binding.SelectedBundleId);
            if (bundle == null
                || !bundle.BuildingIdentity.Equals(record.Binding.AcgBuildingIdentity)
                || !string.Equals(
                    bundle.GeneratorPayloadSha256,
                    record.Binding.SelectedBundlePayloadSha256,
                    StringComparison.OrdinalIgnoreCase)
                || bundle.SourcePlayfield2 == record.Binding.AllocatedLivePlayfield2
                || record.Binding.AllocatedLivePlayfield2
                   == MissionAcgAllocationService.LegacySharedPlayfield2)
            {
                return false;
            }

            plan = new MissionAcgEntryPlan(record, bundle);
            return true;
        }
    }
}
