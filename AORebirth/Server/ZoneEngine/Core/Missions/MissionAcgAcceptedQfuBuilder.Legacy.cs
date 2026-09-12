namespace ZoneEngine.Core.Missions
{
    using AORebirth.Core.Entities;
    using System;
    using SmokeLounge.AOtomation.Messaging.GameData;

    internal static partial class MissionAcgAcceptedQfuBuilder
    {
        // Legacy entity facade only; the packet builder itself depends on a concrete identity.
        internal static MissionAcgAcceptedQfuContract Build(ICharacter character, QuestInfo state,
            MissionAcgInstanceBinding binding, MissionAcgObjectiveRecord objective, int expirySeconds)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            return Build(character.Identity, state, binding, objective, expirySeconds);
        }
    }
}
