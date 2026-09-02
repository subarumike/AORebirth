namespace ZoneEngine.Core.Playfields.Locality
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Playfields;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Missions;

    internal sealed class PlayfieldLocalityVisibility
    {
        private readonly object sync = new object();
        private readonly IPlayfieldCellLayout layout;
        private readonly PlayfieldLocalityPolicy policy;
        private readonly PlayfieldDynelCellRegistry cells;
        private readonly Identity playfieldIdentity;
        private readonly List<int> neighborBuffer = new List<int>();
        private readonly Dictionary<ulong, HashSet<ulong>> visibleSourcesByRecipient =
            new Dictionary<ulong, HashSet<ulong>>();
        private readonly Dictionary<ulong, HashSet<ulong>> visibleRecipientsBySource =
            new Dictionary<ulong, HashSet<ulong>>();
        private readonly HashSet<ulong> initializedRecipients = new HashSet<ulong>();

        internal PlayfieldLocalityVisibility(
            Identity playfieldIdentity,
            IPlayfieldCellLayout layout,
            PlayfieldLocalityPolicy policy,
            PlayfieldDynelCellRegistry cells)
        {
            this.playfieldIdentity = playfieldIdentity;
            this.layout = layout;
            this.policy = policy;
            this.cells = cells;
        }

        internal int LastCandidateCount { get; private set; }

        internal void Synchronize(IEnumerable<ICharacter> characters)
        {
            this.cells.Synchronize(characters);
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
            lock (this.sync)
            {
                this.visibleSourcesByRecipient.Clear();
                this.visibleRecipientsBySource.Clear();
                this.initializedRecipients.Clear();
            }
        }

        internal IList<ICharacter> SelectInitialCharacters(ICharacter recipient)
        {
            if (recipient == null)
            {
                return new List<ICharacter>();
            }

            this.cells.Register(recipient);
            List<ICharacter> candidates = this.CollectCandidates(recipient).ToList();
            this.LastCandidateCount = candidates.Count;
            return candidates
                .Where(source => source != null && source.Identity != recipient.Identity)
                .OrderBy(source => (int)source.Identity.Type)
                .ThenBy(source => source.Identity.Instance)
                .ToList();
        }

        internal void CompleteInitialRecipient(ICharacter recipient)
        {
            if (recipient == null)
            {
                return;
            }

            this.cells.Register(recipient);
            lock (this.sync)
            {
                ulong recipientKey = recipient.Identity.Long();
                this.initializedRecipients.Add(recipientKey);
                if (!this.visibleSourcesByRecipient.ContainsKey(recipientKey))
                {
                    this.visibleSourcesByRecipient[recipientKey] = new HashSet<ulong>();
                }
            }
        }

        internal bool MarkVisibleEntry(ICharacter recipient, ICharacter source)
        {
            if (!this.CanShare(recipient, source))
            {
                return false;
            }

            ulong recipientKey = recipient.Identity.Long();
            ulong sourceKey = source.Identity.Long();
            lock (this.sync)
            {
                HashSet<ulong> visibleSources = GetOrCreate(this.visibleSourcesByRecipient, recipientKey);
                if (!visibleSources.Add(sourceKey))
                {
                    return false;
                }

                GetOrCreate(this.visibleRecipientsBySource, sourceKey).Add(recipientKey);
                return true;
            }
        }

        internal void RemoveVisibleEntry(Identity recipientIdentity, Identity sourceIdentity)
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
                }
            }
        }

        internal IReadOnlyList<ICharacter> VisibleRecipientsForSource(Identity sourceIdentity)
        {
            lock (this.sync)
            {
                HashSet<ulong> recipientKeys;
                if (!this.visibleRecipientsBySource.TryGetValue(sourceIdentity.Long(), out recipientKeys))
                {
                    return new List<ICharacter>();
                }

                return recipientKeys
                    .Select(this.ResolveCharacter)
                    .Where(
                        recipient =>
                            recipient != null
                            && this.IsConnectedRecipient(recipient)
                            && this.initializedRecipients.Contains(recipient.Identity.Long()))
                    .OrderBy(recipient => (int)recipient.Identity.Type)
                    .ThenBy(recipient => recipient.Identity.Instance)
                    .ToList();
            }
        }

        internal bool CanReceive(ICharacter source, ICharacter recipient)
        {
            if (!this.CanShare(recipient, source))
            {
                return false;
            }

            lock (this.sync)
            {
                ulong recipientKey = recipient.Identity.Long();
                HashSet<ulong> visibleSources;
                return this.initializedRecipients.Contains(recipientKey)
                       && this.visibleSourcesByRecipient.TryGetValue(recipientKey, out visibleSources)
                       && visibleSources.Contains(source.Identity.Long());
            }
        }

        internal void Reconcile(
            ICharacter changedCharacter,
            Func<ICharacter, ICharacter, bool> enterVisibility,
            Action<ICharacter, Identity> leaveVisibility)
        {
            if (changedCharacter == null)
            {
                return;
            }

            this.cells.Move(changedCharacter);
            Identity changedIdentity = changedCharacter.Identity;

            if (this.IsInitializedRecipient(changedIdentity) && this.IsConnectedRecipient(changedCharacter))
            {
                this.ReconcileRecipient(changedCharacter, enterVisibility, leaveVisibility);
            }

            this.ReconcileSource(changedCharacter, enterVisibility, leaveVisibility);
        }

        internal void UnregisterSource(Identity sourceIdentity)
        {
            ulong sourceKey = sourceIdentity.Long();
            lock (this.sync)
            {
                this.RemoveSourceStateUnlocked(sourceKey);
            }
        }

        private void ReconcileRecipient(
            ICharacter recipient,
            Func<ICharacter, ICharacter, bool> enterVisibility,
            Action<ICharacter, Identity> leaveVisibility)
        {
            HashSet<ulong> currentlyVisible = this.GetVisibleSourceKeys(recipient.Identity);
            HashSet<ulong> desired = new HashSet<ulong>(
                this.CollectCandidates(recipient)
                    .Where(source => this.CanShare(recipient, source))
                    .Select(source => source.Identity.Long()));

            foreach (ulong sourceKey in currentlyVisible.Where(key => !desired.Contains(key)).ToList())
            {
                ICharacter source = this.ResolveCharacter(sourceKey);
                if (source != null && !this.IsPinnedVisibility(recipient, source))
                {
                    leaveVisibility(recipient, source.Identity);
                    this.RemoveVisibleEntry(recipient.Identity, source.Identity);
                }
            }

            foreach (ICharacter source in this.CollectCandidates(recipient))
            {
                if (source == null || !this.CanShare(recipient, source))
                {
                    continue;
                }

                ulong sourceKey = source.Identity.Long();
                if (currentlyVisible.Contains(sourceKey))
                {
                    continue;
                }

                if (this.MarkVisibleEntry(recipient, source))
                {
                    enterVisibility(recipient, source);
                }
            }
        }

        private void ReconcileSource(
            ICharacter source,
            Func<ICharacter, ICharacter, bool> enterVisibility,
            Action<ICharacter, Identity> leaveVisibility)
        {
            foreach (ICharacter recipient in this.GetInitializedConnectedRecipients())
            {
                if (!this.CanShare(recipient, source))
                {
                    continue;
                }

                bool shouldBeVisible = this.IsInVisibilityNeighborhood(recipient, source)
                                       || this.IsPinnedVisibility(recipient, source);
                bool isVisible = this.IsVisibleToRecipient(recipient, source);
                if (shouldBeVisible && !isVisible)
                {
                    if (this.MarkVisibleEntry(recipient, source))
                    {
                        enterVisibility(recipient, source);
                    }
                }
                else if (!shouldBeVisible && isVisible && !this.IsPinnedVisibility(recipient, source))
                {
                    leaveVisibility(recipient, source.Identity);
                    this.RemoveVisibleEntry(recipient.Identity, source.Identity);
                }
            }
        }

        private IEnumerable<ICharacter> CollectCandidates(ICharacter recipient)
        {
            if (this.layout.IsIndoor)
            {
                return this.cells.AllRegisteredCharacters()
                    .Where(
                        c =>
                            c != null
                            && c.InPlayfield(this.playfieldIdentity)
                            && (this.IsPinnedVisibility(recipient, c)
                                || c.Identity != recipient.Identity));
            }

            int recipientCellId;
            if (!this.cells.TryGetCellId(recipient, out recipientCellId) || recipientCellId < 0)
            {
                return Enumerable.Empty<ICharacter>();
            }

            this.cells.CollectNeighborCells(recipientCellId, this.policy.VisibilityNeighborLevel, this.neighborBuffer);
            IEnumerable<ICharacter> neighborhood = this.cells.GetCharactersInCells(this.neighborBuffer);
            return neighborhood.Where(
                c =>
                    c != null
                    && c.InPlayfield(this.playfieldIdentity)
                    && (c.Identity == recipient.Identity
                        || this.IsInCells(c, this.neighborBuffer)
                        || this.IsPinnedVisibility(recipient, c)));
        }

        private bool IsInVisibilityNeighborhood(ICharacter recipient, ICharacter source)
        {
            if (this.layout.IsIndoor)
            {
                return source.Identity != recipient.Identity;
            }

            int recipientCellId;
            int sourceCellId;
            if (!this.cells.TryGetCellId(recipient, out recipientCellId)
                || !this.cells.TryGetCellId(source, out sourceCellId)
                || recipientCellId < 0
                || sourceCellId < 0)
            {
                return false;
            }

            this.cells.CollectNeighborCells(recipientCellId, this.policy.VisibilityNeighborLevel, this.neighborBuffer);
            return this.neighborBuffer.Contains(sourceCellId);
        }

        private bool IsInCells(ICharacter character, List<int> cellIds)
        {
            int cellId;
            return this.cells.TryGetCellId(character, out cellId) && cellIds.Contains(cellId);
        }

        private bool IsVisibleToRecipient(ICharacter recipient, ICharacter source)
        {
            lock (this.sync)
            {
                HashSet<ulong> visibleSources;
                return this.visibleSourcesByRecipient.TryGetValue(recipient.Identity.Long(), out visibleSources)
                       && visibleSources.Contains(source.Identity.Long());
            }
        }

        private HashSet<ulong> GetVisibleSourceKeys(Identity recipientIdentity)
        {
            lock (this.sync)
            {
                HashSet<ulong> stored;
                return this.visibleSourcesByRecipient.TryGetValue(recipientIdentity.Long(), out stored)
                           ? new HashSet<ulong>(stored)
                           : new HashSet<ulong>();
            }
        }

        private IEnumerable<ICharacter> GetInitializedConnectedRecipients()
        {
            lock (this.sync)
            {
                return this.initializedRecipients
                    .Select(this.ResolveCharacter)
                    .Where(recipient => recipient != null && this.IsConnectedRecipient(recipient))
                    .ToList();
            }
        }

        private bool IsInitializedRecipient(Identity recipientIdentity)
        {
            lock (this.sync)
            {
                return this.initializedRecipients.Contains(recipientIdentity.Long());
            }
        }

        private bool CanShare(ICharacter recipient, ICharacter source)
        {
            return recipient != null
                   && source != null
                   && recipient.Identity != source.Identity
                   && recipient.Playfield != null
                   && source.InPlayfield(recipient.Playfield.Identity);
        }

        private bool IsConnectedRecipient(ICharacter character)
        {
            return character != null
                   && character.Controller != null
                   && character.Controller.Client != null;
        }

        private bool IsPinnedVisibility(ICharacter recipient, ICharacter source)
        {
            if (recipient != null
                && source != null
                && source.Stats[StatIds.petmaster].Value > 0
                && source.Stats[StatIds.petmaster].Value == recipient.Identity.Instance)
            {
                return true;
            }

            if (recipient != null
                && source != null
                && recipient.Playfield != null
                && source.Controller is NPCController
                && source.Stats[StatIds.health].Value > 0
                && (NascenceDungeon1Rules.IsDungeonPlayfield(recipient.Playfield.Identity.Instance)
                    || NascenceDungeon2Rules.IsDungeonPlayfield(recipient.Playfield.Identity.Instance)
                    || NascenceDungeon3Rules.IsDungeonPlayfield(recipient.Playfield.Identity.Instance)
                    || NascenceDungeon4Rules.IsDungeonPlayfield(recipient.Playfield.Identity.Instance)))
            {
                return true;
            }

            if (recipient != null
                && source != null
                && recipient.Playfield != null
                && MissionInstanceService.IsMissionInstancePlayfield(recipient.Playfield.Identity.Instance))
            {
                return this.IsInVisibilityNeighborhood(recipient, source);
            }

            return false;
        }

        private ICharacter ResolveCharacter(ulong identityKey)
        {
            foreach (ICharacter character in this.cells.AllRegisteredCharacters())
            {
                if (character.Identity.Long() == identityKey)
                {
                    return character;
                }
            }

            return null;
        }

        private static HashSet<ulong> GetOrCreate(Dictionary<ulong, HashSet<ulong>> map, ulong key)
        {
            HashSet<ulong> set;
            if (!map.TryGetValue(key, out set))
            {
                set = new HashSet<ulong>();
                map.Add(key, set);
            }

            return set;
        }

        private void RemoveRecipientStateUnlocked(ulong recipientKey)
        {
            HashSet<ulong> visibleSources;
            if (this.visibleSourcesByRecipient.TryGetValue(recipientKey, out visibleSources))
            {
                foreach (ulong sourceKey in visibleSources.ToList())
                {
                    HashSet<ulong> recipients;
                    if (this.visibleRecipientsBySource.TryGetValue(sourceKey, out recipients))
                    {
                        recipients.Remove(recipientKey);
                    }
                }

                this.visibleSourcesByRecipient.Remove(recipientKey);
            }

            this.initializedRecipients.Remove(recipientKey);
        }

        private void RemoveSourceStateUnlocked(ulong sourceKey)
        {
            HashSet<ulong> visibleRecipients;
            if (this.visibleRecipientsBySource.TryGetValue(sourceKey, out visibleRecipients))
            {
                foreach (ulong recipientKey in visibleRecipients.ToList())
                {
                    HashSet<ulong> visibleSources;
                    if (this.visibleSourcesByRecipient.TryGetValue(recipientKey, out visibleSources))
                    {
                        visibleSources.Remove(sourceKey);
                    }
                }

                this.visibleRecipientsBySource.Remove(sourceKey);
            }
        }
    }
}
