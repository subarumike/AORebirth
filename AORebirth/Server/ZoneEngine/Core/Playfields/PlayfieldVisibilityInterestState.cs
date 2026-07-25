namespace ZoneEngine.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

    using SmokeLounge.AOtomation.Messaging.GameData;

    internal sealed class PlayfieldVisibilityInterestState<TValue>
        where TValue : class
    {
        private readonly object sync = new object();
        private readonly PlayfieldVisibilityInterestPolicy policy;
        private readonly UniformSpatialIndex<TValue> spatialIndex;
        private readonly Func<TValue, Identity> identityOf;
        private readonly Func<TValue, VisibilityPosition> positionOf;
        private readonly Func<TValue, TValue, bool> canShareVisibility;
        private readonly Func<TValue, bool> isActiveRecipient;
        private readonly Func<TValue, TValue, bool> isPinnedVisibility;
        private readonly Func<TValue, float> resolveEnterRadius;
        private readonly Func<TValue, float> resolveLeaveRadius;
        private readonly Dictionary<ulong, TValue> valuesByIdentity =
            new Dictionary<ulong, TValue>();
        private readonly Dictionary<ulong, HashSet<ulong>> visibleSourcesByRecipient =
            new Dictionary<ulong, HashSet<ulong>>();
        private readonly Dictionary<ulong, HashSet<ulong>> visibleRecipientsBySource =
            new Dictionary<ulong, HashSet<ulong>>();
        private readonly HashSet<ulong> initializedRecipients = new HashSet<ulong>();

        internal PlayfieldVisibilityInterestState(
            PlayfieldVisibilityInterestPolicy policy,
            UniformSpatialIndex<TValue> spatialIndex,
            Func<TValue, Identity> identityOf,
            Func<TValue, VisibilityPosition> positionOf,
            Func<TValue, TValue, bool> canShareVisibility,
            Func<TValue, bool> isActiveRecipient,
            Func<TValue, TValue, bool> isPinnedVisibility,
            Func<TValue, float> resolveEnterRadius = null,
            Func<TValue, float> resolveLeaveRadius = null)
        {
            this.policy = Require(policy, "policy");
            this.spatialIndex = Require(spatialIndex, "spatialIndex");
            this.identityOf = Require(identityOf, "identityOf");
            this.positionOf = Require(positionOf, "positionOf");
            this.canShareVisibility = Require(canShareVisibility, "canShareVisibility");
            this.isActiveRecipient = Require(isActiveRecipient, "isActiveRecipient");
            this.isPinnedVisibility = Require(isPinnedVisibility, "isPinnedVisibility");
            this.resolveEnterRadius = resolveEnterRadius;
            this.resolveLeaveRadius = resolveLeaveRadius;
        }

        private float EnterRadiusFor(TValue recipient)
        {
            return this.resolveEnterRadius != null
                       ? this.resolveEnterRadius(recipient)
                       : this.policy.EnterRadius;
        }

        private float LeaveRadiusFor(TValue recipient)
        {
            return this.resolveLeaveRadius != null
                       ? this.resolveLeaveRadius(recipient)
                       : this.policy.LeaveRadius;
        }

        internal int LastCandidateInspectionCount
        {
            get { return this.spatialIndex.LastCandidateInspectionCount; }
        }

        internal void Register(TValue value)
        {
            if (value == null)
            {
                return;
            }

            Identity identity = this.identityOf(value);
            this.spatialIndex.Upsert(identity, this.positionOf(value), value);
            lock (this.sync)
            {
                this.valuesByIdentity[identity.Long()] = value;
            }
        }

        internal void Unregister(Identity identity)
        {
            ulong identityKey = identity.Long();
            this.spatialIndex.Remove(identity);

            lock (this.sync)
            {
                this.valuesByIdentity.Remove(identityKey);
                this.RemoveRecipientStateUnlocked(identityKey);
                this.RemoveSourceStateUnlocked(identityKey);
            }
        }

        internal void Synchronize(IEnumerable<TValue> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException("values");
            }

            List<TValue> snapshot = values.Where(value => value != null).ToList();
            var currentIdentities = new HashSet<ulong>(
                snapshot.Select(value => this.identityOf(value).Long()));
            List<Identity> staleIdentities;

            foreach (TValue value in snapshot)
            {
                this.spatialIndex.Upsert(this.identityOf(value), this.positionOf(value), value);
            }

            lock (this.sync)
            {
                staleIdentities = this.valuesByIdentity
                    .Where(value => !currentIdentities.Contains(value.Key))
                    .Select(value => this.identityOf(value.Value))
                    .ToList();

                foreach (TValue value in snapshot)
                {
                    this.valuesByIdentity[this.identityOf(value).Long()] = value;
                }
            }

            foreach (Identity staleIdentity in staleIdentities)
            {
                this.Unregister(staleIdentity);
            }
        }

        internal ReadOnlyCollection<TValue> SelectInitialValues(TValue recipient)
        {
            if (recipient == null)
            {
                return new List<TValue>().AsReadOnly();
            }

            this.Register(recipient);
            Identity recipientIdentity = this.identityOf(recipient);
            VisibilityPosition recipientPosition = this.positionOf(recipient);
            float enterRadius = this.EnterRadiusFor(recipient);
            return this.spatialIndex.Query(recipientPosition, enterRadius)
                .Where(
                    source => source != null
                              && this.identityOf(source) != recipientIdentity
                              && this.canShareVisibility(recipient, source))
                .OrderBy(source => DistanceSquared(recipientPosition, this.positionOf(source)))
                .ThenBy(source => (int)this.identityOf(source).Type)
                .ThenBy(source => this.identityOf(source).Instance)
                .ToList()
                .AsReadOnly();
        }

        internal bool MarkVisibleEntry(TValue recipient, TValue source)
        {
            if (!this.CanShare(recipient, source))
            {
                return false;
            }

            ulong recipientKey = this.identityOf(recipient).Long();
            ulong sourceKey = this.identityOf(source).Long();
            lock (this.sync)
            {
                this.valuesByIdentity[recipientKey] = recipient;
                this.valuesByIdentity[sourceKey] = source;

                HashSet<ulong> visibleSources = GetOrCreate(
                    this.visibleSourcesByRecipient,
                    recipientKey);
                if (!visibleSources.Add(sourceKey))
                {
                    return false;
                }

                GetOrCreate(this.visibleRecipientsBySource, sourceKey).Add(recipientKey);
                return true;
            }
        }

        internal void CompleteInitialRecipient(TValue recipient)
        {
            if (recipient == null)
            {
                return;
            }

            this.Register(recipient);
            lock (this.sync)
            {
                ulong recipientKey = this.identityOf(recipient).Long();
                this.initializedRecipients.Add(recipientKey);
                GetOrCreate(this.visibleSourcesByRecipient, recipientKey);
            }
        }

        internal bool IsInitializedRecipient(Identity recipientIdentity)
        {
            lock (this.sync)
            {
                return this.initializedRecipients.Contains(recipientIdentity.Long());
            }
        }

        internal void ReconcileInitializedRecipients(
            TValue changedValue,
            Func<TValue, TValue, bool> enterVisibility,
            Action<TValue, Identity> leaveVisibility)
        {
            if (changedValue == null)
            {
                return;
            }

            Require(enterVisibility, "enterVisibility");
            Require(leaveVisibility, "leaveVisibility");

            this.Register(changedValue);
            Identity changedIdentity = this.identityOf(changedValue);
            if (this.IsInitializedRecipient(changedIdentity)
                && this.isActiveRecipient(changedValue))
            {
                this.ReconcileRecipient(changedValue, enterVisibility, leaveVisibility);
            }

            this.ReconcileSource(changedValue, enterVisibility, leaveVisibility);
        }

        internal ReadOnlyCollection<TValue> VisibleRecipientsForSource(Identity sourceIdentity)
        {
            List<TValue> recipients;
            lock (this.sync)
            {
                HashSet<ulong> recipientKeys;
                if (!this.visibleRecipientsBySource.TryGetValue(sourceIdentity.Long(), out recipientKeys))
                {
                    return new List<TValue>().AsReadOnly();
                }

                recipients = recipientKeys
                    .Select(this.ValueOrNullUnlocked)
                    .Where(
                        recipient => recipient != null
                                     && this.isActiveRecipient(recipient)
                                     && this.initializedRecipients.Contains(
                                         this.identityOf(recipient).Long()))
                    .OrderBy(recipient => (int)this.identityOf(recipient).Type)
                    .ThenBy(recipient => this.identityOf(recipient).Instance)
                    .ToList();
            }

            return recipients.AsReadOnly();
        }

        internal ReadOnlyCollection<TValue> VisibleSourcesForRecipient(Identity recipientIdentity)
        {
            List<TValue> sources;
            lock (this.sync)
            {
                HashSet<ulong> sourceKeys;
                if (!this.visibleSourcesByRecipient.TryGetValue(recipientIdentity.Long(), out sourceKeys))
                {
                    return new List<TValue>().AsReadOnly();
                }

                sources = sourceKeys
                    .Select(this.ValueOrNullUnlocked)
                    .Where(source => source != null)
                    .OrderBy(source => (int)this.identityOf(source).Type)
                    .ThenBy(source => this.identityOf(source).Instance)
                    .ToList();
            }

            return sources.AsReadOnly();
        }

        internal bool CanReceive(TValue source, TValue recipient)
        {
            if (!this.CanShare(recipient, source))
            {
                return false;
            }

            lock (this.sync)
            {
                ulong recipientKey = this.identityOf(recipient).Long();
                HashSet<ulong> visibleSources;
                return this.initializedRecipients.Contains(recipientKey)
                       && this.visibleSourcesByRecipient.TryGetValue(recipientKey, out visibleSources)
                       && visibleSources.Contains(this.identityOf(source).Long());
            }
        }

        internal void ForgetRecipient(Identity recipientIdentity)
        {
            lock (this.sync)
            {
                this.RemoveRecipientStateUnlocked(recipientIdentity.Long());
            }
        }

        internal void Clear()
        {
            this.spatialIndex.Clear();
            lock (this.sync)
            {
                this.valuesByIdentity.Clear();
                this.visibleSourcesByRecipient.Clear();
                this.visibleRecipientsBySource.Clear();
                this.initializedRecipients.Clear();
            }
        }

        private void ReconcileRecipient(
            TValue recipient,
            Func<TValue, TValue, bool> enterVisibility,
            Action<TValue, Identity> leaveVisibility)
        {
            VisibilityPosition recipientPosition = this.positionOf(recipient);
            float leaveRadius = this.LeaveRadiusFor(recipient);
            float enterRadius = this.EnterRadiusFor(recipient);
            List<TValue> leaveRadiusCandidates = this.spatialIndex
                .Query(recipientPosition, leaveRadius)
                .Where(source => this.CanShare(recipient, source))
                .ToList();
            var candidatesByIdentity = leaveRadiusCandidates.ToDictionary(
                source => this.identityOf(source).Long(),
                source => source);
            HashSet<ulong> currentlyVisible;

            lock (this.sync)
            {
                HashSet<ulong> stored;
                currentlyVisible = this.visibleSourcesByRecipient.TryGetValue(
                    this.identityOf(recipient).Long(),
                    out stored)
                    ? new HashSet<ulong>(stored)
                    : new HashSet<ulong>();
            }

            List<TValue> entering = leaveRadiusCandidates
                .Where(
                    source => !currentlyVisible.Contains(this.identityOf(source).Long())
                              && Distance(recipient, source) <= enterRadius)
                .OrderBy(source => DistanceSquared(recipientPosition, this.positionOf(source)))
                .ThenBy(source => (int)this.identityOf(source).Type)
                .ThenBy(source => this.identityOf(source).Instance)
                .ToList();

            List<TValue> leaving = currentlyVisible
                .Select(this.ValueForKey)
                .Where(
                    source => source != null
                              && !this.isPinnedVisibility(recipient, source)
                              && (!candidatesByIdentity.ContainsKey(this.identityOf(source).Long())
                                  || Distance(recipient, source) > leaveRadius))
                .OrderBy(source => (int)this.identityOf(source).Type)
                .ThenBy(source => this.identityOf(source).Instance)
                .ToList();

            foreach (TValue source in leaving)
            {
                leaveVisibility(recipient, this.identityOf(source));
                this.RemoveVisibleEntry(this.identityOf(recipient), this.identityOf(source));
            }

            foreach (TValue source in entering)
            {
                if (!this.MarkVisibleEntry(recipient, source))
                {
                    continue;
                }

                this.DeliverReservedEntry(recipient, source, enterVisibility);
            }
        }

        private void ReconcileSource(
            TValue source,
            Func<TValue, TValue, bool> enterVisibility,
            Action<TValue, Identity> leaveVisibility)
        {
            List<TValue> recipients;
            lock (this.sync)
            {
                Identity sourceIdentity = this.identityOf(source);
                recipients = this.initializedRecipients
                    .Select(this.ValueOrNullUnlocked)
                    .Where(
                        recipient => recipient != null
                                     && this.isActiveRecipient(recipient)
                                     && this.identityOf(recipient) != sourceIdentity)
                    .OrderBy(recipient => DistanceSquared(
                        this.positionOf(recipient),
                        this.positionOf(source)))
                    .ThenBy(recipient => (int)this.identityOf(recipient).Type)
                    .ThenBy(recipient => this.identityOf(recipient).Instance)
                    .ToList();
            }

            foreach (TValue recipient in recipients)
            {
                bool currentlyVisible = this.CanReceive(source, recipient);
                double distance = Distance(recipient, source);
                bool pinned = this.isPinnedVisibility(recipient, source);
                if (currentlyVisible
                    && (!this.CanShare(recipient, source)
                        || (distance > this.LeaveRadiusFor(recipient) && !pinned)))
                {
                    leaveVisibility(recipient, this.identityOf(source));
                    this.RemoveVisibleEntry(this.identityOf(recipient), this.identityOf(source));
                }
                else if (!currentlyVisible
                         && (distance <= this.EnterRadiusFor(recipient) || pinned)
                         && this.CanShare(recipient, source)
                         && this.MarkVisibleEntry(recipient, source))
                {
                    this.DeliverReservedEntry(recipient, source, enterVisibility);
                }
            }
        }

        private void DeliverReservedEntry(
            TValue recipient,
            TValue source,
            Func<TValue, TValue, bool> enterVisibility)
        {
            try
            {
                if (!enterVisibility(recipient, source))
                {
                    this.RemoveVisibleEntry(
                        this.identityOf(recipient),
                        this.identityOf(source));
                }
            }
            catch
            {
                this.RemoveVisibleEntry(
                    this.identityOf(recipient),
                    this.identityOf(source));
                throw;
            }
        }

        private void RemoveVisibleEntry(Identity recipientIdentity, Identity sourceIdentity)
        {
            ulong recipientKey = recipientIdentity.Long();
            ulong sourceKey = sourceIdentity.Long();
            lock (this.sync)
            {
                HashSet<ulong> visibleSources;
                if (this.visibleSourcesByRecipient.TryGetValue(recipientKey, out visibleSources))
                {
                    visibleSources.Remove(sourceKey);
                }

                HashSet<ulong> visibleRecipients;
                if (this.visibleRecipientsBySource.TryGetValue(sourceKey, out visibleRecipients))
                {
                    visibleRecipients.Remove(recipientKey);
                    if (visibleRecipients.Count == 0)
                    {
                        this.visibleRecipientsBySource.Remove(sourceKey);
                    }
                }
            }
        }

        private void RemoveRecipientStateUnlocked(ulong recipientKey)
        {
            this.initializedRecipients.Remove(recipientKey);
            HashSet<ulong> visibleSources;
            if (!this.visibleSourcesByRecipient.TryGetValue(recipientKey, out visibleSources))
            {
                return;
            }

            foreach (ulong sourceKey in visibleSources)
            {
                HashSet<ulong> visibleRecipients;
                if (this.visibleRecipientsBySource.TryGetValue(sourceKey, out visibleRecipients))
                {
                    visibleRecipients.Remove(recipientKey);
                    if (visibleRecipients.Count == 0)
                    {
                        this.visibleRecipientsBySource.Remove(sourceKey);
                    }
                }
            }

            this.visibleSourcesByRecipient.Remove(recipientKey);
        }

        private void RemoveSourceStateUnlocked(ulong sourceKey)
        {
            HashSet<ulong> visibleRecipients;
            if (!this.visibleRecipientsBySource.TryGetValue(sourceKey, out visibleRecipients))
            {
                return;
            }

            foreach (ulong recipientKey in visibleRecipients)
            {
                HashSet<ulong> visibleSources;
                if (this.visibleSourcesByRecipient.TryGetValue(recipientKey, out visibleSources))
                {
                    visibleSources.Remove(sourceKey);
                }
            }

            this.visibleRecipientsBySource.Remove(sourceKey);
        }

        private TValue ValueForKey(ulong identityKey)
        {
            lock (this.sync)
            {
                return this.ValueOrNullUnlocked(identityKey);
            }
        }

        private TValue ValueOrNullUnlocked(ulong identityKey)
        {
            TValue value;
            return this.valuesByIdentity.TryGetValue(identityKey, out value) ? value : null;
        }

        private bool CanShare(TValue recipient, TValue source)
        {
            return recipient != null
                   && source != null
                   && this.identityOf(recipient) != this.identityOf(source)
                   && this.canShareVisibility(recipient, source);
        }

        private double Distance(TValue left, TValue right)
        {
            return Math.Sqrt(DistanceSquared(this.positionOf(left), this.positionOf(right)));
        }

        private static double DistanceSquared(VisibilityPosition left, VisibilityPosition right)
        {
            double x = left.X - right.X;
            double z = left.Z - right.Z;
            return (x * x) + (z * z);
        }

        private static HashSet<ulong> GetOrCreate(
            IDictionary<ulong, HashSet<ulong>> values,
            ulong identityKey)
        {
            HashSet<ulong> result;
            if (!values.TryGetValue(identityKey, out result))
            {
                result = new HashSet<ulong>();
                values[identityKey] = result;
            }

            return result;
        }

        private static TDelegate Require<TDelegate>(TDelegate value, string name)
            where TDelegate : class
        {
            if (value == null)
            {
                throw new ArgumentNullException(name);
            }

            return value;
        }
    }
}
