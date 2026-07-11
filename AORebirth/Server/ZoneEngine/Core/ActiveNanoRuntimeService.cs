namespace ZoneEngine.Core
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Nanos;
    using AORebirth.Core.Network;
    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Packets;

    using Utility;

    #endregion

    public sealed class ActiveNanoRuntimeService
    {
        public static readonly ActiveNanoRuntimeService Default = new ActiveNanoRuntimeService();

        private const int MaxDurationCentiseconds = 36000000;

        private static readonly object Sync = new object();

        private static readonly Dictionary<int, Dictionary<int, Timer>> ExpiryTimersByCharacter =
            new Dictionary<int, Dictionary<int, Timer>>();

        private static readonly Dictionary<int, int> NextNanoInstanceByCharacter =
            new Dictionary<int, int>();

        private static readonly Dictionary<int, List<DBCharacterActiveNano>> ZoneTransferStashByCharacter =
            new Dictionary<int, List<DBCharacterActiveNano>>();

        private ActiveNanoRuntimeService()
        {
        }

        public bool ApplyActiveNano(
            ICharacter character,
            int nanoId,
            int durationCentiseconds,
            Identity durationPacketIdentity = default(Identity),
            int activeStrain = 0)
        {
            if (character == null || !NanoLoader.NanoList.ContainsKey(nanoId))
            {
                return false;
            }

            bool isSurgeryClinicNano = nanoId == SurgeryClinicInteractionRules.SurgeryClinicNanoId;
            if (!isSurgeryClinicNano && !this.CanActivateNano(character, nanoId))
            {
                return false;
            }

            NanoFormula nano = NanoLoader.NanoList[nanoId];
            int strain = activeStrain > 0 ? activeStrain : this.ResolveNanoStrain(character, nanoId);
            DateTime expiresAtUtc = durationCentiseconds > 0
                ? DateTime.UtcNow.AddMilliseconds((long)durationCentiseconds * 10L)
                : DateTime.MaxValue;

            IActiveNano existing;
            if (character.ActiveNanos.TryGetValue(strain, out existing) && existing != null && existing.ID == nanoId)
            {
                var existingState = existing as ActiveNanoState;
                if (existingState != null)
                {
                    existingState.TickCounter = durationCentiseconds;
                    existingState.TickInterval = durationCentiseconds;
                    existingState.ExpiresAtUtc = expiresAtUtc;
                    existingState.NcuCost = nano.NCUCost();
                    if (!durationPacketIdentity.Equals(default(Identity)))
                    {
                        existingState.DurationPacketIdentity = durationPacketIdentity;
                    }

                    this.CancelExpiryTimer(character.Identity.Instance, strain);
                    if (durationCentiseconds > 0)
                    {
                        this.ScheduleExpiry(character, strain, nanoId, durationCentiseconds);
                    }

                    this.SyncPersistedStore(character);
                    this.SyncCurrentNcuStat(character);
                    return true;
                }
            }

            this.RemoveActiveNanoByStrain(character, strain, true);

            var state = new ActiveNanoState
            {
                ID = nanoId,
                Instance = this.AllocateNanoInstance(character.Identity.Instance),
                Nanotype = nano.getItemAttribute(75),
                TickCounter = durationCentiseconds,
                TickInterval = durationCentiseconds,
                NcuCost = nano.NCUCost(),
                ExpiresAtUtc = expiresAtUtc,
                PlayfieldBound = isSurgeryClinicNano,
                DurationPacketIdentity = durationPacketIdentity,
                DurationParameter1 = character.Identity.Instance
            };

            character.ActiveNanos[strain] = state;
            this.SyncPersistedStore(character);
            this.SyncCurrentNcuStat(character);

            if (durationCentiseconds > 0)
            {
                this.ScheduleExpiry(character, strain, nanoId, durationCentiseconds);
            }

            return true;
        }

        public bool CanActivateNano(ICharacter character, int nanoId)
        {
            if (character == null || !NanoLoader.NanoList.ContainsKey(nanoId))
            {
                return false;
            }

            if (nanoId == SurgeryClinicInteractionRules.SurgeryClinicNanoId)
            {
                return true;
            }

            NanoFormula nano = NanoLoader.NanoList[nanoId];
            int strain = this.ResolveNanoStrain(character, nanoId);
            int newCost = nano.NCUCost();
            int projectedUsed = this.GetUsedNcu(character);

            IActiveNano existing;
            if (character.ActiveNanos.TryGetValue(strain, out existing))
            {
                projectedUsed -= this.GetNanoNcuCost(existing);
            }

            return projectedUsed + newCost <= this.GetMaxNcu(character);
        }

        public int ResolveNanoStrain(ICharacter character, int nanoId)
        {
            if (NanoEventRuntimeService.Default.HasSummonPetOnUse(nanoId))
            {
                string petHash = PetSummonNanoCatalog.GetPreferredPetHash(nanoId);
                int petStrain = PetSlotClassifier.ResolveStrain(petHash);
                if (petStrain > 0)
                {
                    return petStrain;
                }
            }

            return NanoLoader.NanoList[nanoId].NanoStrain();
        }

        public bool HasActiveNanoInStrain(ICharacter character, int nanoId, int strain)
        {
            if (character == null || strain <= 0)
            {
                return false;
            }

            IActiveNano activeNano;
            return character.ActiveNanos.TryGetValue(strain, out activeNano)
                && activeNano != null
                && activeNano.ID == nanoId;
        }

        public int GetUsedNcu(ICharacter character)
        {
            if (character == null)
            {
                return 0;
            }

            int used = 0;
            foreach (KeyValuePair<int, IActiveNano> entry in character.ActiveNanos)
            {
                ActiveNanoState state = entry.Value as ActiveNanoState;
                if (state != null && state.PlayfieldBound)
                {
                    continue;
                }

                used += this.GetNanoNcuCost(entry.Value);
            }

            return used;
        }

        public int GetMaxNcu(ICharacter character)
        {
            if (character == null)
            {
                return 0;
            }

            return Math.Max(0, character.Stats[StatIds.maxncu].Value);
        }

        public void SyncCurrentNcuStat(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            character.Stats[StatIds.currentncu].Value = this.GetUsedNcu(character);
            character.Stats[StatIds.currentncu].Changed = false;
        }

        public void HandleRemoveFriendlyNano(IZoneClient client, CharacterActionMessage message)
        {
            if (client == null || client.Controller == null || client.Controller.Character == null || message == null)
            {
                return;
            }

            ICharacter character = client.Controller.Character;
            List<ActiveNanoRemovalTarget> removalTargets = this.BuildRemovalTargets(character, message);
            if (removalTargets.Count == 0)
            {
                client.Server.Info(
                    client,
                    "RemoveFriendlyNano no targets target={0} p1={1} p2={2} active={3}",
                    message.Target,
                    message.Parameter1,
                    message.Parameter2,
                    character.ActiveNanos.Count);
                CharacterActionMessageHandler.Default.AcknowledgeRemoveFriendlyNano(
                    character,
                    message,
                    character.ActiveNanos.Count == 1 ? character.ActiveNanos.Values.First().ID : 0);
                return;
            }

            foreach (ActiveNanoRemovalTarget removalTarget in removalTargets)
            {
                this.RemoveActiveNanoByNanoId(character, removalTarget.NanoId, false);
            }

            this.SyncCurrentNcuStat(character);
            character.Stats.ClearChangedFlags();

            client.Server.Info(
                client,
                "RemoveFriendlyNano clearing nanoIds={0}",
                string.Join(",", removalTargets.Select(x => x.NanoId.ToString())));

            CharacterActionMessageHandler.Default.CompleteFriendlyNanoRemoval(
                character,
                message,
                removalTargets);

            this.SyncPersistedStore(character);
        }

        private List<ActiveNanoRemovalTarget> BuildRemovalTargets(ICharacter character, CharacterActionMessage message)
        {
            var targets = new List<ActiveNanoRemovalTarget>();
            int resolvedNanoId = this.ResolveNanoIdFromRemoveMessage(character, message);
            if (resolvedNanoId > 0)
            {
                Identity clearIdentity = this.ResolveClearIdentity(character, resolvedNanoId);
                int nanoInstance = this.ResolveNanoInstance(character, resolvedNanoId);
                int durationParameter1 = this.ResolveDurationParameter1(character, resolvedNanoId);
                targets.Add(
                    new ActiveNanoRemovalTarget(resolvedNanoId, clearIdentity, nanoInstance, durationParameter1));
                return targets;
            }

            foreach (KeyValuePair<int, IActiveNano> entry in character.ActiveNanos.ToList())
            {
                targets.Add(
                    new ActiveNanoRemovalTarget(
                        entry.Value.ID,
                        this.ResolveClearIdentity(character, entry.Value),
                        entry.Value.Instance,
                        this.ResolveDurationParameter1(character, entry.Value)));
            }

            return targets;
        }

        private Identity ResolveClearIdentity(ICharacter character, int nanoId)
        {
            foreach (IActiveNano activeNano in character.ActiveNanos.Values)
            {
                if (activeNano.ID != nanoId)
                {
                    continue;
                }

                return this.ResolveClearIdentity(character, activeNano);
            }

            return character.Identity;
        }

        private Identity ResolveClearIdentity(ICharacter character, IActiveNano activeNano)
        {
            ActiveNanoState state = activeNano as ActiveNanoState;
            if (state != null && state.DurationPacketIdentity.Instance != 0)
            {
                return state.DurationPacketIdentity;
            }

            return character.Identity;
        }

        private int ResolveNanoInstance(ICharacter character, int nanoId)
        {
            foreach (IActiveNano activeNano in character.ActiveNanos.Values)
            {
                if (activeNano.ID == nanoId)
                {
                    return activeNano.Instance;
                }
            }

            return 0;
        }

        private int ResolveDurationParameter1(ICharacter character, int nanoId)
        {
            foreach (IActiveNano activeNano in character.ActiveNanos.Values)
            {
                if (activeNano.ID != nanoId)
                {
                    continue;
                }

                return this.ResolveDurationParameter1(character, activeNano);
            }

            return character.Identity.Instance;
        }

        private int ResolveDurationParameter1(ICharacter character, IActiveNano activeNano)
        {
            ActiveNanoState state = activeNano as ActiveNanoState;
            if (state != null && state.DurationParameter1 > 0)
            {
                return state.DurationParameter1;
            }

            return character.Identity.Instance;
        }

        public sealed class ActiveNanoRemovalTarget
        {
            public ActiveNanoRemovalTarget(
                int nanoId,
                Identity clearIdentity,
                int nanoInstance,
                int durationParameter1)
            {
                this.NanoId = nanoId;
                this.ClearIdentity = clearIdentity;
                this.NanoInstance = nanoInstance;
                this.DurationParameter1 = durationParameter1;
            }

            public int NanoId { get; private set; }

            public Identity ClearIdentity { get; private set; }

            public int NanoInstance { get; private set; }

            public int DurationParameter1 { get; private set; }
        }

        public bool TryHandleRemoveFriendlyNano(IZoneClient client, CharacterActionMessage message)
        {
            this.HandleRemoveFriendlyNano(client, message);
            return true;
        }

        public void ForceFriendlyNanoRemoval(IZoneClient client, CharacterActionMessage message)
        {
            this.HandleRemoveFriendlyNano(client, message);
        }

        public void PrepareCharacterForLogin(ICharacter character)
        {
            if (character == null || character.ActiveNanos.Count == 0)
            {
                return;
            }

            this.CancelAllExpiryTimersForCharacter(character.Identity.Instance);
            character.ActiveNanos.Clear();
            this.SyncCurrentNcuStat(character);
        }

        public void PersistCharacterActiveNanos(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            this.SyncPersistedStore(character);
        }

        public void ClearPlayfieldBoundActiveNanos(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            int[] strains = character.ActiveNanos
                .Where(entry => entry.Value is ActiveNanoState && ((ActiveNanoState)entry.Value).PlayfieldBound)
                .Select(entry => entry.Key)
                .ToArray();

            foreach (int strain in strains)
            {
                this.RemoveActiveNanoByStrain(character, strain, true);
            }
        }

        public void RemoveActiveNanoInStrain(ICharacter character, int strain, bool notifyClient)
        {
            this.RemoveActiveNanoByStrain(character, strain, notifyClient);
        }

        public void HandlePlayfieldLeave(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            this.StashZoneTransferNanos(character);
            PetRuntimeService.Default.StashPetForZoneTransfer(character);
            this.ClearPlayfieldBoundActiveNanos(character);
            this.RevokeImplantAccessOnPlayfieldLeave(character);
            this.PersistCharacterActiveNanos(character);
        }

        public void RevokeImplantAccessOnPlayfieldLeave(ICharacter character)
        {
            Character concreteCharacter = character as Character;
            if (concreteCharacter != null)
            {
                concreteCharacter.GrantImplantAccess(-1);
            }
        }

        public void RestoreCharacterActiveNanos(ICharacter character, bool notifyClient)
        {
            if (character == null)
            {
                return;
            }

            int characterId = character.Identity.Instance;
            DateTime nowUtc = DateTime.UtcNow;
            List<DBCharacterActiveNano> persistedNanos = this.TakeZoneTransferStash(characterId);
            bool isZoneTransferRestore = persistedNanos != null && persistedNanos.Count > 0;

            if (!isZoneTransferRestore)
            {
                CharacterActiveNanosDao.Instance.DeleteExpiredActiveNanos(characterId, nowUtc);
                persistedNanos = CharacterActiveNanosDao.Instance.ReadActiveNanos(characterId);
            }

            if (persistedNanos == null || persistedNanos.Count == 0)
            {
                return;
            }

            var stillActive = new List<DBCharacterActiveNano>();
            bool restoredAny = false;

            foreach (DBCharacterActiveNano persisted in persistedNanos)
            {
                if (!NanoLoader.NanoList.ContainsKey(persisted.NanoId))
                {
                    continue;
                }

                DateTime expiresAtUtc = persisted.ExpiresAtUtcTicks > 0
                    ? new DateTime(persisted.ExpiresAtUtcTicks, DateTimeKind.Utc)
                    : DateTime.MaxValue;

                int remainingCentiseconds = this.GetRemainingDurationCentiseconds(
                    expiresAtUtc,
                    nowUtc,
                    persisted.DurationCentiseconds);
                bool isPermanentSummonPetNano = expiresAtUtc == DateTime.MaxValue
                    && NanoEventRuntimeService.Default.HasSummonPetOnUse(persisted.NanoId);
                if (remainingCentiseconds <= 0 && !isPermanentSummonPetNano)
                {
                    continue;
                }

                if (remainingCentiseconds <= 0 && isPermanentSummonPetNano)
                {
                    if (!isZoneTransferRestore
                        && !PetRuntimeService.Default.HasPendingRestoreForStrain(
                            characterId,
                            persisted.Strain))
                    {
                        continue;
                    }

                    remainingCentiseconds = 0;
                }

                stillActive.Add(persisted);
                this.RestoreActiveNano(character, persisted, expiresAtUtc, remainingCentiseconds);
                restoredAny = true;
            }

            CharacterActiveNanosDao.Instance.ReplaceActiveNanos(characterId, stillActive);

            if (!PetRuntimeService.Default.HasPendingRestore(characterId))
            {
                this.CleanupOrphanSummonPetNanos(character, notifyClient);
            }

            if (notifyClient && restoredAny)
            {
                this.NotifyClientActiveNanosRestored(character);
            }

            foreach (KeyValuePair<int, IActiveNano> entry in character.ActiveNanos.ToList())
            {
                if (!NanoEventRuntimeService.Default.HasSummonPetOnUse(entry.Value.ID))
                {
                    PetShellItemService.Default.GiveShellAfterNanoRestore(character, entry.Value.ID);
                }
            }

            this.SyncCurrentNcuStat(character);
        }

        public void CleanupOrphanSummonPetNanosAfterPetRestore(ICharacter character)
        {
            this.CleanupOrphanSummonPetNanos(character, true);
        }

        public void PurgeOrphanSummonNanoInStrain(ICharacter character, int strain, bool notifyClient)
        {
            if (character == null)
            {
                return;
            }

            IActiveNano activeNano;
            if (!character.ActiveNanos.TryGetValue(strain, out activeNano) || activeNano == null)
            {
                return;
            }

            if (!NanoEventRuntimeService.Default.HasSummonPetOnUse(activeNano.ID))
            {
                return;
            }

            if (PetRuntimeService.Default.HasActivePetInStrain(character, strain))
            {
                return;
            }

            if (PetRuntimeService.Default.HasPendingRestoreForStrain(
                character.Identity.Instance,
                strain))
            {
                return;
            }

            this.RemoveActiveNanoByStrain(character, strain, notifyClient);
        }

        private void CleanupOrphanSummonPetNanos(ICharacter character, bool notifyClient)
        {
            if (character == null)
            {
                return;
            }

            foreach (KeyValuePair<int, IActiveNano> entry in character.ActiveNanos.ToList())
            {
                if (!NanoEventRuntimeService.Default.HasSummonPetOnUse(entry.Value.ID))
                {
                    continue;
                }

                if (PetRuntimeService.Default.HasActivePetInStrain(character, entry.Key))
                {
                    continue;
                }

                if (PetRuntimeService.Default.HasPendingRestoreForStrain(
                    character.Identity.Instance,
                    entry.Key))
                {
                    continue;
                }

                this.RemoveActiveNanoByStrain(character, entry.Key, notifyClient);
            }
        }

        public void SchedulePostLoginNanoRestore(IZoneClient client)
        {
            if (client == null || client.Controller == null || client.Controller.Character == null)
            {
                return;
            }

            int characterId = client.Controller.Character.Identity.Instance;
            bool hasZoneTransferStash = this.HasZoneTransferStash(characterId);
            bool hasDbActiveNanos = CharacterActiveNanosDao.Instance.HasActiveNanos(characterId);
            bool hasPendingPetRestore = PetRuntimeService.Default.HasPendingRestore(characterId);
            if (!hasZoneTransferStash && !hasDbActiveNanos)
            {
                if (hasPendingPetRestore)
                {
                    PetRuntimeService.Default.ClearPendingRestoreForOwner(characterId);
                    LogUtil.Debug(
                        DebugInfoDetail.GameFunctions,
                        "Cleared stale pet pending restore on login char=" + characterId);
                }

                return;
            }

            int restoreDelayMilliseconds = hasZoneTransferStash ? 250 : 750;

            ThreadPool.QueueUserWorkItem(
                _ =>
                {
                    Thread.Sleep(restoreDelayMilliseconds);

                    ICharacter character = client.Controller != null ? client.Controller.Character : null;
                    if (character == null || character.Controller == null || character.Controller.Client == null)
                    {
                        return;
                    }

                    this.RestoreCharacterActiveNanos(character, true);
                });

            if (hasZoneTransferStash && hasPendingPetRestore)
            {
                ThreadPool.QueueUserWorkItem(
                    _ =>
                    {
                        Thread.Sleep(restoreDelayMilliseconds + 500);

                        ICharacter character = client.Controller != null ? client.Controller.Character : null;
                        if (character == null || character.Playfield == null)
                        {
                            return;
                        }

                        PetRuntimeService.Default.TryRestorePetAfterZoneIn(character);
                    });
            }
        }

        private void NotifyClientActiveNanosRestored(ICharacter character)
        {
            if (character == null || character.Controller == null || character.Controller.Client == null)
            {
                return;
            }

            foreach (KeyValuePair<int, IActiveNano> entry in character.ActiveNanos.ToList())
            {
                IActiveNano activeNano = entry.Value;
                CharacterActionMessageHandler.Default.NotifyActiveNanoDuration(
                    character,
                    character.Identity,
                    activeNano.ID,
                    activeNano.TickCounter);
            }

            SimpleCharFullUpdate.SendToOne(character, character.Controller.Client);
        }

        private void RestoreActiveNano(
            ICharacter character,
            DBCharacterActiveNano persisted,
            DateTime expiresAtUtc,
            int remainingCentiseconds)
        {
            NanoFormula nano = NanoLoader.NanoList[persisted.NanoId];
            var state = new ActiveNanoState
            {
                ID = persisted.NanoId,
                Instance = persisted.NanoInstance > 0
                    ? persisted.NanoInstance
                    : this.AllocateNanoInstance(character.Identity.Instance),
                Nanotype = nano.getItemAttribute(75),
                TickCounter = remainingCentiseconds,
                TickInterval = persisted.DurationCentiseconds > 0
                    ? persisted.DurationCentiseconds
                    : remainingCentiseconds,
                NcuCost = nano.NCUCost(),
                ExpiresAtUtc = expiresAtUtc,
                DurationPacketIdentity = character.Identity
            };

            if (state.DurationParameter1 <= 0)
            {
                state.DurationParameter1 = character.Identity.Instance;
            }

            character.ActiveNanos[persisted.Strain] = state;
            this.ScheduleExpiry(character, persisted.Strain, persisted.NanoId, remainingCentiseconds);
        }

        private bool RemoveActiveNanoByNanoId(ICharacter character, int nanoId, bool notifyClient)
        {
            if (character == null)
            {
                return false;
            }

            KeyValuePair<int, IActiveNano> match = character.ActiveNanos
                .FirstOrDefault(x => x.Value.ID == nanoId);
            if (match.Value == null)
            {
                return false;
            }

            this.RemoveActiveNanoByStrain(character, match.Key, notifyClient);
            return true;
        }

        private void RemoveActiveNanoByStrain(ICharacter character, int strain, bool notifyClient)
        {
            IActiveNano activeNano;
            if (!character.ActiveNanos.TryGetValue(strain, out activeNano))
            {
                return;
            }

            int nanoId = activeNano.ID;
            int nanoInstance = activeNano.Instance;
            character.ActiveNanos.Remove(strain);
            this.CancelExpiryTimer(character.Identity.Instance, strain);

            if (NanoEventRuntimeService.Default.HasSummonPetOnUse(nanoId))
            {
                PetRuntimeService.Default.DismissPetByStrain(character, strain);
            }

            if (notifyClient)
            {
                CharacterActionMessageHandler.Default.CompleteFriendlyNanoRemoval(
                    character,
                    nanoId,
                    character.Identity,
                    nanoInstance);
            }

            this.SyncPersistedStore(character);
            this.SyncCurrentNcuStat(character);
            character.Stats.ClearChangedFlags();
        }

        private int GetNanoNcuCost(IActiveNano activeNano)
        {
            if (activeNano == null)
            {
                return 0;
            }

            ActiveNanoState state = activeNano as ActiveNanoState;
            if (state != null && state.NcuCost > 0)
            {
                return state.NcuCost;
            }

            if (NanoLoader.NanoList.ContainsKey(activeNano.ID))
            {
                return Math.Max(0, NanoLoader.NanoList[activeNano.ID].NCUCost());
            }

            return 0;
        }

        private void ScheduleExpiry(ICharacter character, int strain, int nanoId, int durationCentiseconds)
        {
            int safeDurationCentiseconds = this.ClampDurationCentiseconds(durationCentiseconds);
            if (safeDurationCentiseconds <= 0)
            {
                return;
            }

            int characterId = character.Identity.Instance;
            this.CancelExpiryTimer(characterId, strain);

            Timer timer = null;
            timer = new Timer(
                _ =>
                {
                    try
                    {
                        IActiveNano activeNano;
                        if (!character.ActiveNanos.TryGetValue(strain, out activeNano) || activeNano.ID != nanoId)
                        {
                            return;
                        }

                        this.RemoveActiveNanoByStrain(character, strain, character.Controller != null && character.Controller.Client != null);
                    }
                    finally
                    {
                        if (timer != null)
                        {
                            timer.Dispose();
                        }

                        this.RemoveExpiryTimerEntry(characterId, strain);
                    }
                },
                null,
                (long)safeDurationCentiseconds * 10L,
                Timeout.Infinite);

            lock (Sync)
            {
                Dictionary<int, Timer> timers;
                if (!ExpiryTimersByCharacter.TryGetValue(characterId, out timers))
                {
                    timers = new Dictionary<int, Timer>();
                    ExpiryTimersByCharacter[characterId] = timers;
                }

                timers[strain] = timer;
            }
        }

        private void CancelExpiryTimer(int characterId, int strain)
        {
            lock (Sync)
            {
                Dictionary<int, Timer> timers;
                if (!ExpiryTimersByCharacter.TryGetValue(characterId, out timers))
                {
                    return;
                }

                Timer timer;
                if (!timers.TryGetValue(strain, out timer))
                {
                    return;
                }

                timer.Dispose();
                timers.Remove(strain);
                if (timers.Count == 0)
                {
                    ExpiryTimersByCharacter.Remove(characterId);
                }
            }
        }

        private void RemoveExpiryTimerEntry(int characterId, int strain)
        {
            lock (Sync)
            {
                Dictionary<int, Timer> timers;
                if (!ExpiryTimersByCharacter.TryGetValue(characterId, out timers))
                {
                    return;
                }

                timers.Remove(strain);
                if (timers.Count == 0)
                {
                    ExpiryTimersByCharacter.Remove(characterId);
                }
            }
        }

        private void StashZoneTransferNanos(ICharacter character)
        {
            int characterId = character.Identity.Instance;
            List<DBCharacterActiveNano> stash = character.ActiveNanos
                .Select(
                    entry =>
                    {
                        ActiveNanoState state = entry.Value as ActiveNanoState;
                        if (state == null || state.PlayfieldBound)
                        {
                            return null;
                        }

                        return new DBCharacterActiveNano
                        {
                            CharacterId = characterId,
                            NanoId = state.ID,
                            Strain = entry.Key,
                            NanoInstance = state.Instance,
                            DurationCentiseconds = state.TickInterval > 0 ? state.TickInterval : state.TickCounter,
                            ExpiresAtUtcTicks = state.ExpiresAtUtc.Ticks
                        };
                    })
                .Where(row => row != null)
                .ToList();

            lock (Sync)
            {
                if (stash.Count == 0)
                {
                    ZoneTransferStashByCharacter.Remove(characterId);
                }
                else
                {
                    ZoneTransferStashByCharacter[characterId] = stash;
                }
            }
        }

        public bool HasZoneTransferStash(int characterId)
        {
            lock (Sync)
            {
                List<DBCharacterActiveNano> stash;
                return ZoneTransferStashByCharacter.TryGetValue(characterId, out stash)
                    && stash != null
                    && stash.Count > 0;
            }
        }

        private List<DBCharacterActiveNano> TakeZoneTransferStash(int characterId)
        {
            lock (Sync)
            {
                List<DBCharacterActiveNano> stash;
                if (!ZoneTransferStashByCharacter.TryGetValue(characterId, out stash)
                    || stash == null
                    || stash.Count == 0)
                {
                    return null;
                }

                ZoneTransferStashByCharacter.Remove(characterId);
                return stash
                    .Select(
                        row => new DBCharacterActiveNano
                        {
                            CharacterId = row.CharacterId,
                            NanoId = row.NanoId,
                            Strain = row.Strain,
                            NanoInstance = row.NanoInstance,
                            DurationCentiseconds = row.DurationCentiseconds,
                            ExpiresAtUtcTicks = row.ExpiresAtUtcTicks
                        })
                    .ToList();
            }
        }

        private void CancelAllExpiryTimersForCharacter(int characterId)
        {
            lock (Sync)
            {
                Dictionary<int, Timer> timers;
                if (!ExpiryTimersByCharacter.TryGetValue(characterId, out timers))
                {
                    return;
                }

                foreach (Timer timer in timers.Values)
                {
                    timer.Dispose();
                }

                ExpiryTimersByCharacter.Remove(characterId);
            }
        }

        private void SyncPersistedStore(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            int characterId = character.Identity.Instance;
            IEnumerable<DBCharacterActiveNano> rows = character.ActiveNanos
                .Select(
                    entry =>
                    {
                        ActiveNanoState state = entry.Value as ActiveNanoState;
                        if (state == null || state.PlayfieldBound)
                        {
                            return null;
                        }

                        return new DBCharacterActiveNano
                        {
                            CharacterId = characterId,
                            NanoId = state.ID,
                            Strain = entry.Key,
                            NanoInstance = state.Instance,
                            DurationCentiseconds = state.TickInterval > 0 ? state.TickInterval : state.TickCounter,
                            ExpiresAtUtcTicks = state.ExpiresAtUtc.Ticks
                        };
                    })
                .Where(row => row != null);

            CharacterActiveNanosDao.Instance.ReplaceActiveNanos(characterId, rows);
        }

        private int GetRemainingDurationCentiseconds(
            DateTime expiresAtUtc,
            DateTime nowUtc,
            int originalDurationCentiseconds)
        {
            int remainingCentiseconds;
            if (expiresAtUtc == DateTime.MaxValue)
            {
                remainingCentiseconds = originalDurationCentiseconds;
            }
            else
            {
                double remainingMilliseconds = (expiresAtUtc - nowUtc).TotalMilliseconds;
                if (remainingMilliseconds <= 0)
                {
                    return 0;
                }

                long computedCentiseconds = (long)Math.Ceiling(remainingMilliseconds / 10D);
                if (computedCentiseconds > int.MaxValue)
                {
                    computedCentiseconds = int.MaxValue;
                }

                remainingCentiseconds = (int)computedCentiseconds;
            }

            if (originalDurationCentiseconds > 0)
            {
                remainingCentiseconds = Math.Min(remainingCentiseconds, originalDurationCentiseconds);
            }

            return this.ClampDurationCentiseconds(remainingCentiseconds);
        }

        private int ClampDurationCentiseconds(int durationCentiseconds)
        {
            if (durationCentiseconds <= 0)
            {
                return 0;
            }

            return Math.Min(durationCentiseconds, MaxDurationCentiseconds);
        }

        private int TryResolveNanoInstance(ICharacter character, CharacterActionMessage message, int nanoId)
        {
            if (message != null && message.Parameter1 > 0)
            {
                foreach (IActiveNano activeNano in character.ActiveNanos.Values)
                {
                    if (activeNano.Instance == message.Parameter1)
                    {
                        return activeNano.Instance;
                    }
                }
            }

            foreach (KeyValuePair<int, IActiveNano> entry in character.ActiveNanos)
            {
                if (entry.Value.ID == nanoId)
                {
                    return entry.Value.Instance;
                }
            }

            if (message != null && message.Parameter1 > 0)
            {
                return message.Parameter1;
            }

            return 0;
        }

        private int TryResolveNanoIdByInstance(ICharacter character, CharacterActionMessage message)
        {
            if (message.Parameter1 <= 0)
            {
                return 0;
            }

            foreach (IActiveNano activeNano in character.ActiveNanos.Values)
            {
                if (activeNano.Instance == message.Parameter1)
                {
                    return activeNano.ID;
                }
            }

            return 0;
        }

        private int AllocateNanoInstance(int characterId)
        {
            lock (Sync)
            {
                int nextInstance;
                if (!NextNanoInstanceByCharacter.TryGetValue(characterId, out nextInstance))
                {
                    nextInstance = 1;
                }

                NextNanoInstanceByCharacter[characterId] = nextInstance + 1;
                return nextInstance;
            }
        }

        private int ResolveNanoIdFromRemoveMessage(ICharacter character, CharacterActionMessage message)
        {
            if (message.Target.Type == IdentityType.NanoProgram && message.Target.Instance != 0)
            {
                return message.Target.Instance;
            }

            if (message.Parameter2 > 0)
            {
                return message.Parameter2;
            }

            if (message.Target.Instance > 0 && NanoLoader.NanoList.ContainsKey(message.Target.Instance))
            {
                return message.Target.Instance;
            }

            int nanoIdByInstance = this.TryResolveNanoIdByInstance(character, message);
            if (nanoIdByInstance > 0)
            {
                return nanoIdByInstance;
            }

            if (character.ActiveNanos.Count == 1)
            {
                return character.ActiveNanos.Values.First().ID;
            }

            return 0;
        }
    }
}
