namespace ZoneEngine.Core.Missions
{
    using System;

    internal sealed class MissionAcgBindingRecord
    {
        internal MissionAcgBindingRecord(
            MissionAcgInstanceBinding binding,
            MissionAcgInstanceState state,
            string recordPath)
        {
            if (binding == null)
            {
                throw new ArgumentNullException("binding");
            }

            if (state == null)
            {
                throw new ArgumentNullException("state");
            }

            this.Binding = binding;
            this.State = state;
            this.RecordPath = recordPath ?? string.Empty;
        }

        internal MissionAcgInstanceBinding Binding { get; private set; }

        internal MissionAcgInstanceState State { get; private set; }

        internal string RecordPath { get; private set; }

        internal MissionAcgBindingRecord WithState(MissionAcgInstanceState state)
        {
            return new MissionAcgBindingRecord(this.Binding, state, this.RecordPath);
        }
    }
}
