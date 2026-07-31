namespace ZoneEngine.Core.Playfields
{
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Statels;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.MessageHandlers;

    internal sealed class CapturedPlayfieldDoorStatusRuntimeService
    {
        private const int TempleOfThreeWindsPlayfieldId = 1931;

        internal int SendInitialStatuses(
            ICharacter character,
            int playfieldId,
            IEnumerable<StatelData> statels)
        {
            if (character == null)
            {
                return 0;
            }

            Identity[] doors = this.ResolveInitialStatusDoors(playfieldId, statels);
            int sent = 0;
            foreach (Identity door in doors)
            {
                DoorStatusUpdateMessageHandler.Default.SendStatus(character, door, false);
                sent++;
            }

            return sent;
        }

        internal Identity[] ResolveInitialStatusDoors(
            int playfieldId,
            IEnumerable<StatelData> statels)
        {
            if (playfieldId != TempleOfThreeWindsPlayfieldId || statels == null)
            {
                return new Identity[0];
            }

            return statels
                .Where(statel => statel != null && statel.Identity.Type == IdentityType.Door)
                .Select(statel => statel.Identity)
                .Distinct()
                .ToArray();
        }
    }
}
