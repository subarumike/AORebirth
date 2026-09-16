namespace ZoneEngine.Core.Missions
{
    using System;

    internal enum MissionAcgLifecycleState
    {
        Reserved = 1,
        Accepted = 2,
        Active = 3,
        CompletionStarted = 4,
        Completed = 5,
        Abandoned = 6,
        Expired = 7,
        CleanupPending = 8,
        Cleaned = 9,
        Invalid = 10
    }

    internal enum MissionAcgCleanupState
    {
        None = 0,
        KeyRemovalPending = 1,
        InstanceReleasePending = 2,
        Completed = 3,
        Failed = 4
    }

    internal sealed class MissionAcgOutdoorReturnStamp
    {
        private MissionAcgOutdoorReturnStamp(
            int acceptedQuestType,
            int acceptedQuestInstance,
            int livePlayfield2,
            int playfield,
            float x,
            float y,
            float z)
        {
            this.AcceptedQuestType = acceptedQuestType;
            this.AcceptedQuestInstance = acceptedQuestInstance;
            this.LivePlayfield2 = livePlayfield2;
            this.Playfield = playfield;
            this.X = x;
            this.Y = y;
            this.Z = z;
        }

        internal int AcceptedQuestType { get; private set; }

        internal int AcceptedQuestInstance { get; private set; }

        internal int LivePlayfield2 { get; private set; }

        internal int Playfield { get; private set; }

        internal float X { get; private set; }

        internal float Y { get; private set; }

        internal float Z { get; private set; }

        internal static MissionAcgOutdoorReturnStamp CreateGenerated(
            MissionAcgInstanceBinding binding)
        {
            if (binding == null
                || binding.AcceptedQuestIdentity == null
                || binding.ExteriorEntranceIdentity == null)
            {
                throw new ArgumentNullException("binding");
            }

            return new MissionAcgOutdoorReturnStamp(
                binding.AcceptedQuestIdentity.Type,
                binding.AcceptedQuestIdentity.Instance,
                binding.AllocatedLivePlayfield2,
                binding.ExteriorEntranceIdentity.Instance,
                binding.ExteriorX,
                binding.ExteriorY,
                binding.ExteriorZ);
        }

        internal static MissionAcgOutdoorReturnStamp CreateLegacy(
            int playfield,
            float x,
            float y,
            float z)
        {
            return new MissionAcgOutdoorReturnStamp(
                0,
                0,
                0,
                playfield,
                x,
                y,
                z);
        }

        internal bool Matches(MissionAcgInstanceBinding binding)
        {
            return binding != null
                   && binding.AcceptedQuestIdentity != null
                   && binding.ExteriorEntranceIdentity != null
                   && this.AcceptedQuestType
                      == binding.AcceptedQuestIdentity.Type
                   && this.AcceptedQuestInstance
                      == binding.AcceptedQuestIdentity.Instance
                   && this.LivePlayfield2
                      == binding.AllocatedLivePlayfield2
                   && this.Playfield
                      == binding.ExteriorEntranceIdentity.Instance
                   && this.X.Equals(binding.ExteriorX)
                   && this.Y.Equals(binding.ExteriorY)
                   && this.Z.Equals(binding.ExteriorZ);
        }
    }

    /// <summary>
    /// Minimal mutable state for one durable ACG binding. Captured layout evidence remains immutable.
    /// </summary>
    internal sealed class MissionAcgInstanceState
    {
        internal MissionAcgInstanceState(
            MissionAcgLifecycleState lifecycleState,
            MissionAcgCleanupState cleanupState,
            DateTime lastUpdatedUtc,
            DateTime? cleanupStartedUtc)
        {
            if (!Enum.IsDefined(typeof(MissionAcgLifecycleState), lifecycleState))
            {
                throw new ArgumentOutOfRangeException("lifecycleState");
            }

            if (!Enum.IsDefined(typeof(MissionAcgCleanupState), cleanupState))
            {
                throw new ArgumentOutOfRangeException("cleanupState");
            }

            if (lastUpdatedUtc == DateTime.MinValue)
            {
                throw new ArgumentOutOfRangeException("lastUpdatedUtc");
            }

            this.LifecycleState = lifecycleState;
            this.CleanupState = cleanupState;
            this.LastUpdatedUtc = ToUtc(lastUpdatedUtc);
            this.CleanupStartedUtc = cleanupStartedUtc.HasValue
                                         ? ToUtc(cleanupStartedUtc.Value)
                                         : (DateTime?)null;
        }

        internal MissionAcgLifecycleState LifecycleState { get; private set; }

        internal MissionAcgCleanupState CleanupState { get; private set; }

        internal DateTime LastUpdatedUtc { get; private set; }

        internal DateTime? CleanupStartedUtc { get; private set; }

        internal bool ReservesPlayfield
        {
            get
            {
                return this.LifecycleState != MissionAcgLifecycleState.Cleaned
                       && this.LifecycleState != MissionAcgLifecycleState.Invalid;
            }
        }

        internal bool CanEnter(DateTime nowUtc, DateTime expiryUtc)
        {
            DateTime now = ToUtc(nowUtc);
            return (this.LifecycleState == MissionAcgLifecycleState.Accepted
                    || this.LifecycleState == MissionAcgLifecycleState.Active)
                   && this.CleanupState == MissionAcgCleanupState.None
                   && ToUtc(expiryUtc) > now;
        }

        internal MissionAcgInstanceState Transition(
            MissionAcgLifecycleState nextLifecycle,
            MissionAcgCleanupState nextCleanup,
            DateTime nowUtc)
        {
            if (!IsAllowedTransition(this.LifecycleState, nextLifecycle))
            {
                throw new InvalidOperationException(
                    "Invalid ACG lifecycle transition "
                    + this.LifecycleState
                    + " -> "
                    + nextLifecycle
                    + ".");
            }

            DateTime now = ToUtc(nowUtc);
            DateTime? cleanupStarted = this.CleanupStartedUtc;
            if (!cleanupStarted.HasValue
                && nextCleanup != MissionAcgCleanupState.None)
            {
                cleanupStarted = now;
            }

            return new MissionAcgInstanceState(
                nextLifecycle,
                nextCleanup,
                now,
                cleanupStarted);
        }

        private static bool IsAllowedTransition(
            MissionAcgLifecycleState current,
            MissionAcgLifecycleState next)
        {
            if (current == next)
            {
                return true;
            }

            switch (current)
            {
                case MissionAcgLifecycleState.Reserved:
                    return next == MissionAcgLifecycleState.Accepted
                           || next == MissionAcgLifecycleState.Expired
                           || next == MissionAcgLifecycleState.CleanupPending
                           || next == MissionAcgLifecycleState.Cleaned
                           || next == MissionAcgLifecycleState.Invalid;
                case MissionAcgLifecycleState.Accepted:
                    return next == MissionAcgLifecycleState.Active
                           || next == MissionAcgLifecycleState.CompletionStarted
                           || next == MissionAcgLifecycleState.Abandoned
                           || next == MissionAcgLifecycleState.Expired
                           || next == MissionAcgLifecycleState.CleanupPending
                           || next == MissionAcgLifecycleState.Invalid;
                case MissionAcgLifecycleState.Active:
                    return next == MissionAcgLifecycleState.CompletionStarted
                           || next == MissionAcgLifecycleState.Abandoned
                           || next == MissionAcgLifecycleState.Expired
                           || next == MissionAcgLifecycleState.CleanupPending
                           || next == MissionAcgLifecycleState.Invalid;
                case MissionAcgLifecycleState.CompletionStarted:
                    return next == MissionAcgLifecycleState.Completed
                           || next == MissionAcgLifecycleState.Expired
                           || next == MissionAcgLifecycleState.CleanupPending
                           || next == MissionAcgLifecycleState.Invalid;
                case MissionAcgLifecycleState.Completed:
                case MissionAcgLifecycleState.Abandoned:
                case MissionAcgLifecycleState.Expired:
                    return next == MissionAcgLifecycleState.CleanupPending
                           || next == MissionAcgLifecycleState.Cleaned
                           || next == MissionAcgLifecycleState.Invalid;
                case MissionAcgLifecycleState.CleanupPending:
                    return next == MissionAcgLifecycleState.Cleaned
                           || next == MissionAcgLifecycleState.Invalid;
                case MissionAcgLifecycleState.Cleaned:
                case MissionAcgLifecycleState.Invalid:
                    return false;
                default:
                    return false;
            }
        }

        private static DateTime ToUtc(DateTime value)
        {
            return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        }
    }
}
