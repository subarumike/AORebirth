namespace AORebirth.Interfaces.Persistence.Missions
{
    using System.Collections.Generic;

    /// <summary>
    /// Optional capability on the existing, owner-locked mission transaction. It does not
    /// commit independently: item rows, mission state, objectives and rewards succeed together.
    /// Inputs are trusted server plans, never packet-owned row identities.
    /// </summary>
    public interface IMissionInventoryMutationTransaction
    {
        void ApplyInventoryMutation(
            IReadOnlyList<MissionItemInstanceData> grants,
            IReadOnlyList<MissionItemInstanceData> consumed);
    }
}
