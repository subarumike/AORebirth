namespace ZoneEngine.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Statels;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.MessageHandlers;

    internal sealed class CapturedPlayfieldDoorStatusRuntimeService
    {
        private const int SubwayPlayfieldId = CapturedSubwayArrivalDoorEvidenceSet.PlayfieldId;
        private const int TempleOfThreeWindsPlayfieldId = 1931;
        private const int TempleExteriorEntryDoorInstance = unchecked((int)0xC024078B);
        private const int ExpectedTempleInternalDoorCount = 43;

        private readonly TempleDoorProximityRuntime proximityRuntime =
            new TempleDoorProximityRuntime();

        private TempleDoorDefinition[] doors = new TempleDoorDefinition[0];

        internal int ActiveRecipientCount
        {
            get { return this.proximityRuntime.ActiveRecipientCount; }
        }

        internal void Configure(int playfieldId, IEnumerable<StatelData> statels)
        {
            this.doors = playfieldId == TempleOfThreeWindsPlayfieldId
                ? ResolveTempleDoorStatels(statels)
                    .Select(
                        statel => new TempleDoorDefinition(
                            statel.Identity.Instance,
                            statel.X,
                            statel.Y,
                            statel.Z))
                    .ToArray()
                : new TempleDoorDefinition[0];
            this.proximityRuntime.ResetAll();
        }

        internal int SendInitialStatuses(
            ICharacter character,
            int playfieldId,
            IEnumerable<StatelData> statels,
            bool isExternalPlayfieldArrival)
        {
            if (character == null)
            {
                return 0;
            }

            StatelData[] resolvedDoors = ResolveInitialStatusStatels(
                playfieldId,
                statels,
                isExternalPlayfieldArrival);
            this.proximityRuntime.ResetRecipient(character.Identity.Instance);
            int sent = 0;
            foreach (StatelData door in resolvedDoors)
            {
                DoorStatusUpdateMessageHandler.Default.SendStatus(character, door.Identity, false);
                sent++;
            }

            return sent;
        }

        internal Identity[] ResolveInitialStatusDoors(
            int playfieldId,
            IEnumerable<StatelData> statels,
            bool isExternalPlayfieldArrival)
        {
            return ResolveInitialStatusStatels(
                    playfieldId,
                    statels,
                    isExternalPlayfieldArrival)
                .Select(statel => statel.Identity)
                .ToArray();
        }

        internal void ProcessCharacters(IEnumerable<ICharacter> characters, DateTime nowUtc)
        {
            if (this.doors.Length == 0 || characters == null)
            {
                return;
            }

            HashSet<int> activeRecipients = new HashSet<int>();
            foreach (ICharacter character in characters)
            {
                if (character == null
                    || character.Controller == null
                    || character.Controller.Client == null)
                {
                    continue;
                }

                int recipientId = character.Identity.Instance;
                activeRecipients.Add(recipientId);
                TempleDoorTransition[] transitions = this.proximityRuntime.Evaluate(
                    recipientId,
                    (float)character.Position.x,
                    (float)character.Position.y,
                    (float)character.Position.z,
                    nowUtc,
                    this.doors);
                foreach (TempleDoorTransition transition in transitions)
                {
                    DoorStatusUpdateMessageHandler.Default.SendStatus(
                        character,
                        new Identity
                        {
                            Type = IdentityType.Door,
                            Instance = transition.DoorInstance
                        },
                        transition.IsOpen);
                }
            }

            this.proximityRuntime.RemoveInactiveRecipients(activeRecipients);
        }

        internal void Clear()
        {
            this.doors = new TempleDoorDefinition[0];
            this.proximityRuntime.ResetAll();
        }

        private static StatelData[] ResolveInitialStatusStatels(
            int playfieldId,
            IEnumerable<StatelData> statels,
            bool isExternalPlayfieldArrival)
        {
            if (statels == null)
            {
                return new StatelData[0];
            }

            if (playfieldId == TempleOfThreeWindsPlayfieldId)
            {
                return ResolveTempleDoorStatels(statels);
            }

            if (playfieldId == SubwayPlayfieldId && isExternalPlayfieldArrival)
            {
                return CapturedSubwayArrivalDoorEvidenceSet.ResolveInitialStatusStatels(
                    playfieldId,
                    statels,
                    true);
            }

            return new StatelData[0];
        }

        private static StatelData[] ResolveTempleDoorStatels(
            IEnumerable<StatelData> statels)
        {
            if (statels == null)
            {
                return new StatelData[0];
            }

            // playfields.dat exposes 44 PF1931 Door statels. The official room graph marks
            // C024078B as EntryHall's roomIndex=-1 exterior link; the remaining 43 are the
            // internal automatic doors represented by the captured runtime family.
            StatelData[] resolved = statels
                .Where(
                    statel => statel != null
                              && statel.Identity.Type == IdentityType.Door
                              && statel.Identity.Instance != TempleExteriorEntryDoorInstance)
                .GroupBy(statel => statel.Identity)
                .Select(group => group.First())
                .ToArray();
            if (resolved.Length != ExpectedTempleInternalDoorCount)
            {
                throw new InvalidOperationException(
                    "PF1931 official internal door count mismatch: " + resolved.Length);
            }

            return resolved;
        }
    }
}
