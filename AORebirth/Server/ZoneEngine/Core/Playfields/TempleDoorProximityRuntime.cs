namespace ZoneEngine.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

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
