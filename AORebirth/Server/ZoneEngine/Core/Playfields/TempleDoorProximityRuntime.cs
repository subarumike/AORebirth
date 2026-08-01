namespace ZoneEngine.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Statels;

    using SmokeLounge.AOtomation.Messaging.GameData;

    internal sealed class CapturedDoorSnapshotEvidence
    {
        internal CapturedDoorSnapshotEvidence(
            int instance,
            bool observedClosed,
            bool observedOpen,
            bool observedInCollectorActivationBatch)
        {
            this.Instance = instance;
            this.ObservedClosed = observedClosed;
            this.ObservedOpen = observedOpen;
            this.ObservedInCollectorActivationBatch = observedInCollectorActivationBatch;
        }

        internal int Instance { get; private set; }

        internal bool ObservedClosed { get; private set; }

        internal bool ObservedOpen { get; private set; }

        internal bool ObservedInCollectorActivationBatch { get; private set; }
    }

    internal static class CapturedSubwayDoorSnapshotEvidence
    {
        internal const int PlayfieldId = 127;
        internal const int ExpectedDoorCount = 18;
        internal const int ExpectedCollectorActivationBatchCount = 10;

        // The PF127 corpus contains 1,134 DoorStatusUpdate observations for these
        // exact official statel identities. The analyzer's collector-activation batch
        // contains exactly ten closed doors. The enclosing collection was unarmed and
        // zoning-blocked, so that label is not evidence of a server entry trigger.
        // Every identity was observed closed eventually, and five were also observed
        // open. All observations remain evidence only until a trigger is proven.
        private static readonly CapturedDoorSnapshotEvidence[] Doors =
        {
            new CapturedDoorSnapshotEvidence(unchecked((int)0xC02D007F), true, false, false),
            new CapturedDoorSnapshotEvidence(unchecked((int)0xC02E007F), true, false, false),
            new CapturedDoorSnapshotEvidence(unchecked((int)0xC02F007F), true, true, false),
            new CapturedDoorSnapshotEvidence(unchecked((int)0xC030007F), true, true, false),
            new CapturedDoorSnapshotEvidence(unchecked((int)0xC031007F), true, false, true),
            new CapturedDoorSnapshotEvidence(unchecked((int)0xC032007F), true, false, true),
            new CapturedDoorSnapshotEvidence(unchecked((int)0xC033007F), true, false, true),
            new CapturedDoorSnapshotEvidence(unchecked((int)0xC034007F), true, false, true),
            new CapturedDoorSnapshotEvidence(unchecked((int)0xC035007F), true, true, true),
            new CapturedDoorSnapshotEvidence(unchecked((int)0xC036007F), true, false, true),
            new CapturedDoorSnapshotEvidence(unchecked((int)0xC037007F), true, false, true),
            new CapturedDoorSnapshotEvidence(unchecked((int)0xC038007F), true, false, true),
            new CapturedDoorSnapshotEvidence(unchecked((int)0xC03A007F), true, false, false),
            new CapturedDoorSnapshotEvidence(unchecked((int)0xC03B007F), true, false, false),
            new CapturedDoorSnapshotEvidence(unchecked((int)0xC03C007F), true, true, true),
            new CapturedDoorSnapshotEvidence(unchecked((int)0xC03D007F), true, false, true),
            new CapturedDoorSnapshotEvidence(unchecked((int)0xC03F007F), true, false, false),
            new CapturedDoorSnapshotEvidence(unchecked((int)0xC040007F), true, true, false)
        };

        internal static CapturedDoorSnapshotEvidence[] GetAll()
        {
            return Doors.ToArray();
        }

        internal static bool Contains(int instance)
        {
            return Doors.Any(door => door.Instance == instance);
        }

        internal static bool IsCollectorActivationBatchDoor(int instance)
        {
            CapturedDoorSnapshotEvidence evidence = Doors.FirstOrDefault(
                door => door.Instance == instance);
            return evidence != null && evidence.ObservedInCollectorActivationBatch;
        }
    }

    internal sealed class CapturedSubwayArrivalDoorEvidence
    {
        internal CapturedSubwayArrivalDoorEvidence(
            int instance,
            int templateId,
            string capturedName,
            float capturedX,
            float capturedY,
            float capturedZ)
        {
            this.Instance = instance;
            this.TemplateId = templateId;
            this.CapturedName = capturedName;
            this.CapturedX = capturedX;
            this.CapturedY = capturedY;
            this.CapturedZ = capturedZ;
        }

        internal int Instance { get; private set; }

        internal int TemplateId { get; private set; }

        internal string CapturedName { get; private set; }

        internal float CapturedX { get; private set; }

        internal float CapturedY { get; private set; }

        internal float CapturedZ { get; private set; }
    }

    internal static class CapturedSubwayArrivalDoorEvidenceSet
    {
        internal const int PlayfieldId = 127;
        internal const int ExpectedDoorCount = 6;
        internal const float CoordinateTolerance = 0.001f;

        // Official capture 20260717-012522 enters PF127 from PF655. After the
        // six door dynels spawn and PlayfieldAnarchyF arrives, raw packet rows
        // 358-359 and 379-382 send one closed DoorStatusUpdate for each door.
        // Runtime identities were mapped to playfields.dat statels by position;
        // maximum mapping error is 0.000056m and the nearest alternative is 7.996m.
        private static readonly CapturedSubwayArrivalDoorEvidence[] Doors =
        {
            new CapturedSubwayArrivalDoorEvidence(
                unchecked((int)0xC006007F),
                164818,
                "Exit to city",
                64.0083f,
                115.6938f,
                318.9879f),
            new CapturedSubwayArrivalDoorEvidence(
                unchecked((int)0xC007007F),
                164818,
                "Door",
                90.99688f,
                107.6085f,
                254.0146f),
            new CapturedSubwayArrivalDoorEvidence(
                unchecked((int)0xC00A007F),
                164818,
                "Door",
                108.0146f,
                107.6085f,
                236.9967f),
            new CapturedSubwayArrivalDoorEvidence(
                unchecked((int)0xC00B007F),
                164818,
                "Door to Abandoned Mall",
                145.0014f,
                107.6101f,
                259.9851f),
            new CapturedSubwayArrivalDoorEvidence(
                unchecked((int)0xC00C007F),
                164818,
                "Door to Abandoned Mall",
                152.9977f,
                107.6101f,
                259.9852f),
            new CapturedSubwayArrivalDoorEvidence(
                unchecked((int)0xC00D007F),
                164815,
                "Door to Subway",
                148.9995f,
                107.6101f,
                196.015f)
        };

        internal static CapturedSubwayArrivalDoorEvidence[] GetAll()
        {
            return Doors.ToArray();
        }

        internal static int GetOrder(int instance)
        {
            for (int index = 0; index < Doors.Length; index++)
            {
                if (Doors[index].Instance == instance)
                {
                    return index;
                }
            }

            return -1;
        }

        internal static bool MatchesOfficialStatel(
            int instance,
            int templateId,
            float x,
            float y,
            float z)
        {
            CapturedSubwayArrivalDoorEvidence evidence = Doors.FirstOrDefault(
                door => door.Instance == instance);
            if (evidence == null || evidence.TemplateId != templateId)
            {
                return false;
            }

            float dx = evidence.CapturedX - x;
            float dy = evidence.CapturedY - y;
            float dz = evidence.CapturedZ - z;
            return ((dx * dx) + (dy * dy) + (dz * dz))
                   <= (CoordinateTolerance * CoordinateTolerance);
        }

        internal static StatelData[] ResolveInitialStatusStatels(
            int playfieldId,
            IEnumerable<StatelData> statels,
            bool isExternalPlayfieldArrival)
        {
            if (playfieldId != PlayfieldId
                || !isExternalPlayfieldArrival
                || statels == null)
            {
                return new StatelData[0];
            }

            StatelData[] resolved = statels
                .Where(
                    statel => statel != null
                              && statel.Identity.Type == IdentityType.Door
                              && MatchesOfficialStatel(
                                  statel.Identity.Instance,
                                  statel.TemplateId,
                                  statel.X,
                                  statel.Y,
                                  statel.Z))
                .GroupBy(statel => statel.Identity)
                .Select(group => group.First())
                .OrderBy(statel => GetOrder(statel.Identity.Instance))
                .ToArray();
            if (resolved.Length != ExpectedDoorCount)
            {
                throw new InvalidOperationException(
                    "PF127 official arrival door count mismatch: " + resolved.Length);
            }

            return resolved;
        }
    }

    internal sealed class TempleDoorDefinition
    {
        internal TempleDoorDefinition(int instance, float x, float y, float z)
        {
            this.Instance = instance;
            this.X = x;
            this.Y = y;
            this.Z = z;
        }

        internal int Instance { get; private set; }

        internal float X { get; private set; }

        internal float Y { get; private set; }

        internal float Z { get; private set; }
    }

    internal sealed class TempleDoorTransition
    {
        internal TempleDoorTransition(int doorInstance, bool isOpen)
        {
            this.DoorInstance = doorInstance;
            this.IsOpen = isOpen;
        }

        internal int DoorInstance { get; private set; }

        internal bool IsOpen { get; private set; }
    }

    internal sealed class TempleDoorProximityRuntime
    {
        // The three PF1931 open snapshots land 0.231m, 0.253m, and 0.376m from
        // the official statel. The last preceding sample for the tightest trace is
        // 0.522m away, bounding the shared contact threshold at the 0.5m client cell.
        internal const float TriggerRadius = 0.5f;

        // Of four same-identity pairs, three span visibility/re-entry gaps. The
        // continuously observed doorway lifecycle closes at 5.293s, selecting the
        // integral five-second server hold. Close after the triggering recipient
        // leaves contact. No worker is created; playfield heartbeat time owns expiry.
        internal static readonly TimeSpan MinimumOpenHold = TimeSpan.FromSeconds(5);

        private readonly Dictionary<int, Dictionary<int, RecipientDoorState>> recipients =
            new Dictionary<int, Dictionary<int, RecipientDoorState>>();

        private readonly object sync = new object();

        internal int ActiveRecipientCount
        {
            get
            {
                lock (this.sync)
                {
                    return this.recipients.Count;
                }
            }
        }

        internal TempleDoorTransition[] Evaluate(
            int recipientId,
            float x,
            float y,
            float z,
            DateTime nowUtc,
            IEnumerable<TempleDoorDefinition> doors)
        {
            if (doors == null)
            {
                return new TempleDoorTransition[0];
            }

            lock (this.sync)
            {
                Dictionary<int, RecipientDoorState> states;
                if (!this.recipients.TryGetValue(recipientId, out states))
                {
                    states = new Dictionary<int, RecipientDoorState>();
                    this.recipients[recipientId] = states;
                }

                List<TempleDoorTransition> transitions = new List<TempleDoorTransition>();
                foreach (TempleDoorDefinition door in doors)
                {
                    RecipientDoorState state;
                    if (!states.TryGetValue(door.Instance, out state))
                    {
                        state = new RecipientDoorState();
                        states[door.Instance] = state;
                    }

                    bool inContact = IsInContact(door, x, y, z);
                    if (!state.IsOpen && inContact && !state.WasInContact)
                    {
                        state.IsOpen = true;
                        state.CloseNotBeforeUtc = nowUtc + MinimumOpenHold;
                        transitions.Add(new TempleDoorTransition(door.Instance, true));
                    }
                    else if (state.IsOpen
                             && !inContact
                             && nowUtc >= state.CloseNotBeforeUtc)
                    {
                        state.IsOpen = false;
                        transitions.Add(new TempleDoorTransition(door.Instance, false));
                    }

                    state.WasInContact = inContact;
                }

                return transitions.ToArray();
            }
        }

        internal void ResetRecipient(int recipientId)
        {
            lock (this.sync)
            {
                this.recipients.Remove(recipientId);
            }
        }

        internal void RemoveInactiveRecipients(ISet<int> activeRecipients)
        {
            lock (this.sync)
            {
                if (activeRecipients == null)
                {
                    this.recipients.Clear();
                    return;
                }

                int[] inactive = this.recipients.Keys
                    .Where(recipient => !activeRecipients.Contains(recipient))
                    .ToArray();
                foreach (int recipient in inactive)
                {
                    this.recipients.Remove(recipient);
                }
            }
        }

        internal void ResetAll()
        {
            lock (this.sync)
            {
                this.recipients.Clear();
            }
        }

        private static bool IsInContact(
            TempleDoorDefinition door,
            float x,
            float y,
            float z)
        {
            float dx = door.X - x;
            float dy = door.Y - y;
            float dz = door.Z - z;
            return ((dx * dx) + (dy * dy) + (dz * dz)) <= (TriggerRadius * TriggerRadius);
        }

        private sealed class RecipientDoorState
        {
            internal DateTime CloseNotBeforeUtc { get; set; }

            internal bool IsOpen { get; set; }

            internal bool WasInContact { get; set; }
        }
    }
}
