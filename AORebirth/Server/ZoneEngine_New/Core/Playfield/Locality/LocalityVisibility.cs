namespace ZoneEngine_New.Core.Playfield.Locality
{
    using System;
    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Logging;

    /// <summary>
    /// Cell-neighbor interest management: enter via ISpawnable.BuildSpawnMessage, leave via Despawn.
    /// </summary>
    internal sealed class LocalityVisibility
    {
        private readonly CellGrid _grid;
        private readonly LocalityPolicy _policy;
        private readonly HashSet<Dynel> _tracked;
        private readonly Dictionary<ulong, HashSet<ulong>> _visibleSourcesByRecipient = new();
        private readonly Dictionary<ulong, HashSet<ulong>> _visibleRecipientsBySource = new();
        private readonly HashSet<ulong> _initializedRecipients = new();
        private readonly List<int> _neighborBuffer = new();
        private readonly Dictionary<ulong, Dynel> _byIdentity = new();

        internal LocalityVisibility(CellGrid grid, LocalityPolicy policy, HashSet<Dynel> tracked)
        {
            _grid = grid;
            _policy = policy;
            _tracked = tracked;
        }

        internal void Track(Dynel dynel)
        {
            _byIdentity[dynel.Identity.Long()] = dynel;
        }

        internal void Untrack(Dynel dynel)
        {
            ulong key = dynel.Identity.Long();
            DespawnSourceFromObservers(dynel);
            RemoveSourceState(key);
            ForgetRecipient(key);
            _byIdentity.Remove(key);
        }

        internal void Clear()
        {
            _visibleSourcesByRecipient.Clear();
            _visibleRecipientsBySource.Clear();
            _initializedRecipients.Clear();
            _byIdentity.Clear();
        }

        /// <summary>
        /// After the joining player's self packets: snapshot neighborhood to the player, then announce the player out.
        /// </summary>
        internal void ActivatePlayerVisibility(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);

            ulong recipientKey = player.Identity.Long();
            _initializedRecipients.Add(recipientKey);
            if (!_visibleSourcesByRecipient.ContainsKey(recipientKey))
            {
                _visibleSourcesByRecipient[recipientKey] = new HashSet<ulong>();
            }

            foreach (Dynel source in CollectCandidates(player))
            {
                TryEnterVisibility(player, source);
            }

            ReconcileSource(player);
        }

        internal void Reconcile(Dynel changed)
        {
            if (changed == null)
            {
                return;
            }

            if (changed is Player player
                && player.Session != null
                && _initializedRecipients.Contains(player.Identity.Long()))
            {
                ReconcileRecipient(player);
            }

            ReconcileSource(changed);
        }

        internal void Announce(Dynel source, MessageBody message, bool includeSelf)
        {
            if (includeSelf
                && source is Player self
                && self.Session != null)
            {
                self.Session.Send(message);
            }

            ulong sourceKey = source.Identity.Long();
            if (!_visibleRecipientsBySource.TryGetValue(sourceKey, out HashSet<ulong>? recipientKeys))
            {
                return;
            }

            foreach (ulong recipientKey in recipientKeys)
            {
                if (!_byIdentity.TryGetValue(recipientKey, out Dynel? recipientDynel)
                    || recipientDynel is not Player recipient
                    || recipient.Session == null)
                {
                    continue;
                }

                recipient.Session.Send(message);
            }
        }

        private void ReconcileRecipient(Player recipient)
        {
            HashSet<ulong> currentlyVisible = GetVisibleSourceKeys(recipient.Identity);
            HashSet<ulong> desired = new();
            foreach (Dynel source in CollectCandidates(recipient))
            {
                if (CanShare(recipient, source))
                {
                    desired.Add(source.Identity.Long());
                }
            }

            foreach (ulong sourceKey in currentlyVisible)
            {
                if (desired.Contains(sourceKey))
                {
                    continue;
                }

                if (!_byIdentity.TryGetValue(sourceKey, out Dynel? source))
                {
                    RemoveVisibleEntry(recipient.Identity.Long(), sourceKey);
                    continue;
                }

                if (!IsPinnedVisibility(recipient, source))
                {
                    LeaveVisibility(recipient, source);
                }
            }

            foreach (Dynel source in CollectCandidates(recipient))
            {
                TryEnterVisibility(recipient, source);
            }
        }

        private void ReconcileSource(Dynel source)
        {
            foreach (Player recipient in GetInitializedConnectedRecipients())
            {
                if (!CanShare(recipient, source))
                {
                    continue;
                }

                bool shouldBeVisible = IsInVisibilityNeighborhood(recipient, source)
                                       || IsPinnedVisibility(recipient, source);
                bool isVisible = IsVisibleToRecipient(recipient, source);
                if (shouldBeVisible && !isVisible)
                {
                    TryEnterVisibility(recipient, source);
                }
                else if (!shouldBeVisible && isVisible && !IsPinnedVisibility(recipient, source))
                {
                    LeaveVisibility(recipient, source);
                }
            }
        }

        private IEnumerable<Dynel> CollectCandidates(Player recipient)
        {
            if (!_grid.IsOutdoor)
            {
                foreach (Dynel dynel in _tracked)
                {
                    if (!ReferenceEquals(dynel, recipient))
                    {
                        yield return dynel;
                    }
                }

                yield break;
            }

            if (recipient.Cell == null)
            {
                yield break;
            }

            _grid.CollectNeighbors(recipient.Cell.Id, _policy.VisibilityNeighborLevel, _neighborBuffer);
            HashSet<int> neighborSet = new(_neighborBuffer);
            foreach (int cellId in _neighborBuffer)
            {
                foreach (Dynel dynel in _grid.OccupantsInCell(cellId))
                {
                    if (!ReferenceEquals(dynel, recipient))
                    {
                        yield return dynel;
                    }
                }
            }

            // Pinned pets outside the ring still need to appear in candidate set.
            foreach (Dynel dynel in _tracked)
            {
                if (ReferenceEquals(dynel, recipient) || !IsPinnedVisibility(recipient, dynel))
                {
                    continue;
                }

                if (dynel.Cell == null || !neighborSet.Contains(dynel.Cell.Id))
                {
                    yield return dynel;
                }
            }
        }

        private bool IsInVisibilityNeighborhood(Player recipient, Dynel source)
        {
            if (!_grid.IsOutdoor)
            {
                return !ReferenceEquals(recipient, source);
            }

            if (recipient.Cell == null || source.Cell == null)
            {
                return false;
            }

            _grid.CollectNeighbors(recipient.Cell.Id, _policy.VisibilityNeighborLevel, _neighborBuffer);
            return _neighborBuffer.Contains(source.Cell.Id);
        }

        private bool TryEnterVisibility(Player recipient, Dynel source)
        {
            if (!CanShare(recipient, source))
            {
                return false;
            }

            if (!MarkVisibleEntry(recipient, source))
            {
                return false;
            }

            MessageBody spawn = source.BuildSpawnMessage();
            if (spawn is SimpleCharFullUpdateMessage scfu)
                ScfuSendLog.Write(scfu);
            recipient.Session!.Send(spawn);

            if (source is Character character)
            {
                foreach (WeaponItemFullUpdateMessage wifu in character.BuildWeaponInstanceMessages())
                    recipient.Session.Send(wifu);
            }

            return true;
        }

        private void LeaveVisibility(Player recipient, Dynel source)
        {
            if (recipient.Session != null)
            {
                recipient.Session.Send(
                    new DespawnMessage
                    {
                        Identity = source.Identity,
                        Unknown = 1
                    });
            }

            RemoveVisibleEntry(recipient.Identity.Long(), source.Identity.Long());
        }

        private void DespawnSourceFromObservers(Dynel source)
        {
            ulong sourceKey = source.Identity.Long();
            if (!_visibleRecipientsBySource.TryGetValue(sourceKey, out HashSet<ulong>? recipientKeys))
            {
                return;
            }

            foreach (ulong recipientKey in recipientKeys)
            {
                if (!_byIdentity.TryGetValue(recipientKey, out Dynel? recipientDynel)
                    || recipientDynel is not Player recipient
                    || recipient.Session == null)
                {
                    continue;
                }

                recipient.Session.Send(
                    new DespawnMessage
                    {
                        Identity = source.Identity,
                        Unknown = 1
                    });
            }
        }

        private bool MarkVisibleEntry(Player recipient, Dynel source)
        {
            ulong recipientKey = recipient.Identity.Long();
            ulong sourceKey = source.Identity.Long();
            HashSet<ulong> visibleSources = GetOrCreate(_visibleSourcesByRecipient, recipientKey);
            if (!visibleSources.Add(sourceKey))
            {
                return false;
            }

            GetOrCreate(_visibleRecipientsBySource, sourceKey).Add(recipientKey);
            return true;
        }

        private void RemoveVisibleEntry(ulong recipientKey, ulong sourceKey)
        {
            if (_visibleSourcesByRecipient.TryGetValue(recipientKey, out HashSet<ulong>? visibleSources))
            {
                visibleSources.Remove(sourceKey);
            }

            if (_visibleRecipientsBySource.TryGetValue(sourceKey, out HashSet<ulong>? visibleRecipients))
            {
                visibleRecipients.Remove(recipientKey);
            }
        }

        private void ForgetRecipient(ulong recipientKey)
        {
            if (_visibleSourcesByRecipient.TryGetValue(recipientKey, out HashSet<ulong>? visibleSources))
            {
                foreach (ulong sourceKey in visibleSources)
                {
                    if (_visibleRecipientsBySource.TryGetValue(sourceKey, out HashSet<ulong>? recipients))
                    {
                        recipients.Remove(recipientKey);
                    }
                }

                _visibleSourcesByRecipient.Remove(recipientKey);
            }

            _initializedRecipients.Remove(recipientKey);
        }

        private void RemoveSourceState(ulong sourceKey)
        {
            if (!_visibleRecipientsBySource.TryGetValue(sourceKey, out HashSet<ulong>? visibleRecipients))
            {
                return;
            }

            foreach (ulong recipientKey in visibleRecipients)
            {
                if (_visibleSourcesByRecipient.TryGetValue(recipientKey, out HashSet<ulong>? visibleSources))
                {
                    visibleSources.Remove(sourceKey);
                }
            }

            _visibleRecipientsBySource.Remove(sourceKey);
        }

        private HashSet<ulong> GetVisibleSourceKeys(Identity recipientIdentity)
        {
            if (_visibleSourcesByRecipient.TryGetValue(recipientIdentity.Long(), out HashSet<ulong>? stored))
            {
                return new HashSet<ulong>(stored);
            }

            return new HashSet<ulong>();
        }

        private bool IsVisibleToRecipient(Player recipient, Dynel source)
        {
            return _visibleSourcesByRecipient.TryGetValue(recipient.Identity.Long(), out HashSet<ulong>? visibleSources)
                   && visibleSources.Contains(source.Identity.Long());
        }

        private IEnumerable<Player> GetInitializedConnectedRecipients()
        {
            foreach (ulong key in _initializedRecipients)
            {
                if (_byIdentity.TryGetValue(key, out Dynel? dynel)
                    && dynel is Player player
                    && player.Session != null)
                {
                    yield return player;
                }
            }
        }

        private static bool CanShare(Player recipient, Dynel source)
        {
            return recipient != null
                   && source != null
                   && !ReferenceEquals(recipient, source)
                   && recipient.Identity != source.Identity;
        }

        private static bool IsPinnedVisibility(Player recipient, Dynel source)
        {
            int petMaster = source.Stats.Get(CharacterStat.PetMaster);
            return !StatCollection.IsUnset(petMaster)
                   && petMaster != 0
                   && petMaster == recipient.Identity.Instance;
        }

        private static HashSet<ulong> GetOrCreate(Dictionary<ulong, HashSet<ulong>> map, ulong key)
        {
            if (!map.TryGetValue(key, out HashSet<ulong>? set))
            {
                set = new HashSet<ulong>();
                map.Add(key, set);
            }

            return set;
        }
    }
}
