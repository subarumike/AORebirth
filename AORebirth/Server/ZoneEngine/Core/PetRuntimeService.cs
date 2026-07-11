#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core
{
    #region Usings ...

    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;
    using AORebirth.Core.Nanos;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Vector;
    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Packets;

    using Utility;

    #endregion

    public sealed class PetRuntimeService
    {
        private static readonly PetRuntimeService DefaultInstance = new PetRuntimeService();

        private readonly ConcurrentDictionary<PetSlotKey, Identity> activePetBySlot =
            new ConcurrentDictionary<PetSlotKey, Identity>();

        private readonly ConcurrentDictionary<PetSlotKey, PendingPetRestore> pendingRestoreBySlot =
            new ConcurrentDictionary<PetSlotKey, PendingPetRestore>();

        private PetRuntimeService()
        {
        }

        public static PetRuntimeService Default
        {
            get { return DefaultInstance; }
        }

        public bool SummonPet(
            ICharacter owner,
            string petHash,
            int petTypeId,
            int petSlotStrain = 0,
            int summonNanoId = 0)
        {
            if (owner == null || owner.Playfield == null || string.IsNullOrWhiteSpace(petHash))
            {
                return false;
            }

            petSlotStrain = PetSlotClassifier.ResolveStrain(petHash);

            ActiveNanoRuntimeService.Default.PurgeOrphanSummonNanoInStrain(
                owner,
                petSlotStrain,
                true);

            string mobHash = PetMobTemplateResolver.Resolve(petHash);
            if (string.IsNullOrWhiteSpace(mobHash))
            {
                LogUtil.Debug(DebugInfoDetail.GameFunctions, "SummonPet unknown pet hash " + petHash);
                ChatTextMessageHandler.Default.Send(
                    owner,
                    "Pet spawn failed: missing mob template for " + petHash
                        + ". Import hash BSLX into MySQL mobtemplate (see SqlTables/mobtemplate.sql).");
                return false;
            }

            DBMobTemplate mobTemplate = MobTemplateDao.Instance.GetMobTemplateByHash(mobHash);
            if (mobTemplate == null || string.IsNullOrWhiteSpace(mobTemplate.Name))
            {
                ChatTextMessageHandler.Default.Send(
                    owner,
                    "Pet spawn failed: mob template " + mobHash + " has no data in MySQL.");
                return false;
            }

            LogUtil.Debug(
                DebugInfoDetail.GameFunctions,
                string.Format(
                    "SummonPet template hash={0} name={1} monsterData={2} npcFamily={3}",
                    mobHash,
                    mobTemplate.Name,
                    mobTemplate.MonsterData,
                    mobTemplate.NPCFamily));

            this.PurgeOrphanPetMobsForOwner(owner);
            this.DismissPetByStrain(owner, petSlotStrain, true);

            Coordinate ownerCoord = owner.Coordinates();
            var spawnCoord = new Coordinate(
                ownerCoord.x + 1.5f,
                ownerCoord.y,
                ownerCoord.z + 1.5f);

            var controller = new NPCController();
            int spawnLevel = owner.Stats[StatIds.level].Value;
            Character petCharacter = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                mobHash,
                owner.Playfield.Identity,
                spawnCoord,
                owner.RawHeading,
                controller,
                spawnLevel);

            if (petCharacter == null)
            {
                ChatTextMessageHandler.Default.Send(owner, "Pet spawn failed: could not create pet mob.");
                return false;
            }

            int resolvedPetTypeId = petTypeId > 0 ? petTypeId : 1;

            petCharacter.Playfield = owner.Playfield;
            this.FinalizePetCharacter(petCharacter);
            this.ApplyHealingPetNanoPool(petCharacter, petSlotStrain);
            petCharacter.Stats[StatIds.petmaster].Value = owner.Identity.Instance;
            petCharacter.Stats[StatIds.pettype].Value = resolvedPetTypeId;
            petCharacter.Stats[StatIds.petstate].Value = PetSlotClassifier.CapturedPetStateValue;
            petCharacter.DoNotDoTimers = false;

            // Capture order (20260710-185528): SCFU -> petmaster -> AddPet -> flags -> owner SpellList
            // -> pet stats -> SetWantedDirection -> pet SpellLists -> SetPos.
            // All owner packets must use the same client send queue. Playfield.Announce goes through
            // the async bus and can deliver SpellList before SCFU, which creates a NoName ghost pet slot.
            ZoneClient ownerClient = owner.Controller != null ? owner.Controller.Client as ZoneClient : null;
            if (ownerClient == null)
            {
                this.DespawnUnlinkedPetMob(petCharacter);
                return false;
            }

            SimpleCharFullUpdateMessage petSpawnUpdate =
                this.ConstructPetSpawnFullUpdate(petCharacter, petSlotStrain);

            ownerClient.Server.Info(
                ownerClient,
                "SummonPet path=capture-scfu+serializer-link owner={0} pet={1} playfield={2} nano={3} strain={4}",
                owner.Identity,
                petCharacter.Identity,
                owner.Playfield.Identity.Instance,
                summonNanoId,
                petSlotStrain);

            if (summonNanoId > 0 && petSlotStrain == PetSlotClassifier.HealingPetStrain)
            {
                PetSummonCaptureWireReplayer.SendBelamorteScfuToOwner(
                    ownerClient,
                    owner,
                    petCharacter);
                this.SendPetStatToOwner(
                    ownerClient,
                    petCharacter.Identity,
                    StatIds.petmaster,
                    (uint)petCharacter.Stats[StatIds.petmaster].Value);
                AddPetMessageHandler.Default.SendAddPet(owner, petCharacter.Identity);
                this.SendPetStatToOwner(
                    ownerClient,
                    petCharacter.Identity,
                    StatIds.flags,
                    (uint)petCharacter.Stats[StatIds.flags].Value);
                this.SendBelamorteSummonPostLink(
                    ownerClient,
                    owner,
                    petCharacter,
                    summonNanoId,
                    petHash,
                    petTypeId);
            }
            else
            {
                ownerClient.SendCompressed(petSpawnUpdate);
                this.SendPetStatToOwner(
                    ownerClient,
                    petCharacter.Identity,
                    StatIds.petmaster,
                    (uint)petCharacter.Stats[StatIds.petmaster].Value);
                AddPetMessageHandler.Default.SendAddPet(owner, petCharacter.Identity);
                this.SendPetStatToOwner(
                    ownerClient,
                    petCharacter.Identity,
                    StatIds.flags,
                    (uint)petCharacter.Stats[StatIds.flags].Value);

                if (summonNanoId > 0)
                {
                    PetSummonSpellListService.SendOwnerPetSummon(
                        owner,
                        summonNanoId,
                        petHash,
                        petTypeId,
                        petSlotStrain);
                    this.SendPostSummonPetStatsToOwner(ownerClient, petCharacter);
                    this.SendPetWantedDirectionToOwner(ownerClient, petCharacter.Identity);
                    PetSummonSpellListService.SendPetSummonSpellLists(
                        owner,
                        petCharacter.Identity,
                        petSlotStrain);
                }

                Coordinate petCoord = petCharacter.Coordinates();
                ownerClient.SendCompressed(
                    new SetPosMessage
                    {
                        Identity = petCharacter.Identity,
                        Coordinates =
                            new SmokeLounge.AOtomation.Messaging.GameData.Vector3
                            {
                                X = petCoord.x,
                                Y = petCoord.y,
                                Z = petCoord.z
                            },
                        Unknown1 = 1
                    });
            }

            Playfield concretePlayfield = owner.Playfield as Playfield;
            if (concretePlayfield != null)
            {
                concretePlayfield.ActivateNpc(petCharacter);
                concretePlayfield.RegisterNpcHome(petCharacter);
            }

            owner.Playfield.AnnounceOthers(petSpawnUpdate, owner.Identity);

            if (summonNanoId > 0 && !this.HasActiveSummonPetNanoInStrain(owner, petSlotStrain))
            {
                this.RegisterOwnerSummonNano(owner, summonNanoId, petSlotStrain);
            }

            controller.Follow(owner.Identity, 2.0);

            var slotKey = new PetSlotKey(owner.Identity.Instance, petSlotStrain);
            this.activePetBySlot[slotKey] = petCharacter.Identity;
            this.pendingRestoreBySlot[slotKey] = new PendingPetRestore
            {
                PetHash = petHash,
                PetTypeId = petTypeId,
                PetSlotStrain = petSlotStrain,
                SummonNanoId = summonNanoId,
                ShouldRestore = true
            };

            this.SyncOwnerPetCounter(owner);

            IZoneClient ownerClientInfo = owner.Controller != null ? owner.Controller.Client as IZoneClient : null;
            if (ownerClientInfo != null)
            {
                ownerClientInfo.Server.Info(
                    ownerClientInfo,
                    "SummonPet owner={0} pet={1} hash={2} strain={3} nano={4}",
                    owner.Identity,
                    petCharacter.Identity,
                    petHash,
                    petSlotStrain,
                    summonNanoId);
            }

            LogUtil.Debug(
                DebugInfoDetail.GameFunctions,
                string.Format(
                    "SummonPet ok owner={0} pet={1} hash={2} strain={3} nano={4} type={5} level={6}",
                    owner.Identity,
                    petCharacter.Identity,
                    petHash,
                    petSlotStrain,
                    summonNanoId,
                    resolvedPetTypeId,
                    spawnLevel));

            return true;
        }

        public void DismissPet(ICharacter owner)
        {
            this.DismissAllPets(owner, true);
        }

        public void DismissPetByStrain(ICharacter owner, int petSlotStrain)
        {
            this.DismissPetByStrain(owner, petSlotStrain, true);
        }

        public void StashPetForZoneTransfer(ICharacter owner)
        {
            if (owner == null)
            {
                return;
            }

            int ownerInstance = owner.Identity.Instance;
            foreach (KeyValuePair<PetSlotKey, Identity> entry in this.GetActiveSlotsForOwner(ownerInstance))
            {
                PendingPetRestore pendingRestore;
                if (!this.pendingRestoreBySlot.TryGetValue(entry.Key, out pendingRestore))
                {
                    pendingRestore = new PendingPetRestore { PetSlotStrain = entry.Key.Strain };
                }

                pendingRestore.ShouldRestore = true;
                this.pendingRestoreBySlot[entry.Key] = pendingRestore;

                LogUtil.Debug(
                    DebugInfoDetail.GameFunctions,
                    string.Format(
                        "StashPet owner={0} hash={1} type={2} strain={3}",
                        owner.Identity,
                        pendingRestore.PetHash,
                        pendingRestore.PetTypeId,
                        pendingRestore.PetSlotStrain));
            }

            this.PurgeOrphanPetMobsForOwner(owner);
            this.DismissAllPets(owner, false);
        }

        public void TryRestorePetAfterZoneIn(ICharacter owner)
        {
            if (owner == null || owner.Playfield == null)
            {
                return;
            }

            this.PurgeOrphanPetMobsForOwner(owner);

            int ownerInstance = owner.Identity.Instance;
            foreach (KeyValuePair<PetSlotKey, PendingPetRestore> entry in this.GetPendingSlotsForOwner(ownerInstance))
            {
                PendingPetRestore pendingRestore = entry.Value;
                if (!pendingRestore.ShouldRestore || string.IsNullOrWhiteSpace(pendingRestore.PetHash))
                {
                    continue;
                }

                if (!this.HasActiveSummonPetNanoInStrain(owner, pendingRestore.PetSlotStrain))
                {
                    PendingPetRestore removed;
                    this.pendingRestoreBySlot.TryRemove(entry.Key, out removed);
                    continue;
                }

                LogUtil.Debug(
                    DebugInfoDetail.GameFunctions,
                    string.Format(
                        "RestorePet owner={0} hash={1} type={2} strain={3}",
                        owner.Identity,
                        pendingRestore.PetHash,
                        pendingRestore.PetTypeId,
                        pendingRestore.PetSlotStrain));

                this.SummonPet(
                    owner,
                    pendingRestore.PetHash,
                    pendingRestore.PetTypeId,
                    pendingRestore.PetSlotStrain,
                    pendingRestore.SummonNanoId);
            }

            this.SyncOwnerPetCounter(owner);
            ActiveNanoRuntimeService.Default.CleanupOrphanSummonPetNanosAfterPetRestore(owner);
        }

        public bool HasPendingRestore(int ownerInstance)
        {
            return this.pendingRestoreBySlot.Any(
                x => x.Key.OwnerInstance == ownerInstance
                    && x.Value.ShouldRestore
                    && !string.IsNullOrWhiteSpace(x.Value.PetHash));
        }

        public void OnCharacterDisconnected(ICharacter owner, bool preservePendingRestore)
        {
            if (owner == null)
            {
                return;
            }

            int ownerInstance = owner.Identity.Instance;
            if (preservePendingRestore)
            {
                foreach (PetSlotKey key in this.GetActiveSlotsForOwner(ownerInstance)
                    .Select(x => x.Key)
                    .ToList())
                {
                    Identity removed;
                    this.activePetBySlot.TryRemove(key, out removed);
                }

                return;
            }

            this.DismissAllPets(owner, true);
            this.ClearPendingRestoreForOwner(ownerInstance);
        }

        public void ClearPendingRestoreForOwner(int ownerInstance)
        {
            foreach (PetSlotKey key in this.pendingRestoreBySlot.Keys
                .Where(k => k.OwnerInstance == ownerInstance)
                .ToList())
            {
                PendingPetRestore removed;
                this.pendingRestoreBySlot.TryRemove(key, out removed);
            }
        }

        public bool HasPendingRestoreForStrain(int ownerInstance, int petSlotStrain)
        {
            PendingPetRestore pendingRestore;
            if (!this.pendingRestoreBySlot.TryGetValue(
                new PetSlotKey(ownerInstance, petSlotStrain),
                out pendingRestore))
            {
                return false;
            }

            return pendingRestore.ShouldRestore
                && !string.IsNullOrWhiteSpace(pendingRestore.PetHash);
        }

        public bool HasActivePetInStrain(ICharacter owner, int petSlotStrain)
        {
            if (owner == null)
            {
                return false;
            }

            return this.activePetBySlot.ContainsKey(new PetSlotKey(owner.Identity.Instance, petSlotStrain));
        }

        public ICharacter GetActivePetInStrain(ICharacter owner, int petSlotStrain)
        {
            if (owner == null || owner.Playfield == null)
            {
                return null;
            }

            Identity petIdentity;
            if (!this.activePetBySlot.TryGetValue(
                new PetSlotKey(owner.Identity.Instance, petSlotStrain),
                out petIdentity))
            {
                return null;
            }

            return owner.Playfield.FindByIdentity<ICharacter>(petIdentity);
        }

        public IEnumerable<int> GetActivePetStrains(ICharacter owner)
        {
            if (owner == null)
            {
                yield break;
            }

            int ownerInstance = owner.Identity.Instance;
            foreach (KeyValuePair<PetSlotKey, Identity> entry in this.GetActiveSlotsForOwner(ownerInstance))
            {
                yield return entry.Key.Strain;
            }
        }

        public bool TryGetSummonNanoId(ICharacter owner, ICharacter pet, out int summonNanoId)
        {
            summonNanoId = 0;
            if (owner == null || pet == null)
            {
                return false;
            }

            foreach (KeyValuePair<PetSlotKey, Identity> entry in
                this.GetActiveSlotsForOwner(owner.Identity.Instance))
            {
                if (entry.Value.Instance != pet.Identity.Instance)
                {
                    continue;
                }

                PendingPetRestore pendingRestore;
                if (this.pendingRestoreBySlot.TryGetValue(entry.Key, out pendingRestore)
                    && pendingRestore.SummonNanoId > 0)
                {
                    summonNanoId = pendingRestore.SummonNanoId;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetHealNanoId(ICharacter owner, ICharacter pet, out int healNanoId)
        {
            healNanoId = 0;
            if (owner == null || pet == null)
            {
                return false;
            }

            int summonNanoId;
            string petHash = null;
            foreach (KeyValuePair<PetSlotKey, Identity> entry in
                this.GetActiveSlotsForOwner(owner.Identity.Instance))
            {
                if (entry.Value.Instance != pet.Identity.Instance)
                {
                    continue;
                }

                PendingPetRestore pendingRestore;
                if (this.pendingRestoreBySlot.TryGetValue(entry.Key, out pendingRestore))
                {
                    summonNanoId = pendingRestore.SummonNanoId;
                    petHash = pendingRestore.PetHash;
                    return PetHealNanoCatalog.TryResolveHealNano(summonNanoId, petHash, out healNanoId);
                }
            }

            return false;
        }

        public void TerminatePetByIdentity(ICharacter owner, Identity petIdentity)
        {
            if (owner == null || petIdentity.Instance == 0)
            {
                return;
            }

            foreach (KeyValuePair<PetSlotKey, Identity> entry in this.GetActiveSlotsForOwner(owner.Identity.Instance))
            {
                if (entry.Value.Instance == petIdentity.Instance)
                {
                    this.TerminatePetByStrain(owner, entry.Key.Strain);
                    return;
                }
            }
        }

        public void TerminatePetByStrain(ICharacter owner, int petSlotStrain)
        {
            if (owner == null || petSlotStrain <= 0)
            {
                return;
            }

            IActiveNano activeNano;
            if (owner.ActiveNanos.TryGetValue(petSlotStrain, out activeNano)
                && activeNano != null
                && NanoEventRuntimeService.Default.HasSummonPetOnUse(activeNano.ID))
            {
                ActiveNanoRuntimeService.Default.RemoveActiveNanoInStrain(owner, petSlotStrain, true);
                return;
            }

            this.DismissPetByStrain(owner, petSlotStrain, true);
        }

        private void DismissPetByStrain(ICharacter owner, int petSlotStrain, bool clearRestoreState)
        {
            if (owner == null)
            {
                return;
            }

            var slotKey = new PetSlotKey(owner.Identity.Instance, petSlotStrain);
            Identity petIdentity;
            if (!this.activePetBySlot.TryRemove(slotKey, out petIdentity))
            {
                if (clearRestoreState)
                {
                    PendingPetRestore removedRestore;
                    this.pendingRestoreBySlot.TryRemove(slotKey, out removedRestore);
                    this.SyncOwnerPetCounter(owner);
                }

                return;
            }

            if (clearRestoreState)
            {
                PendingPetRestore removedRestore;
                this.pendingRestoreBySlot.TryRemove(slotKey, out removedRestore);
            }

            this.DespawnPetIdentity(owner, petIdentity);

            if (clearRestoreState)
            {
                this.SyncOwnerPetCounter(owner);
            }
        }

        private void DismissAllPets(ICharacter owner, bool clearRestoreState)
        {
            if (owner == null)
            {
                return;
            }

            int ownerInstance = owner.Identity.Instance;
            foreach (KeyValuePair<PetSlotKey, Identity> entry in this.GetActiveSlotsForOwner(ownerInstance).ToList())
            {
                Identity petIdentity;
                if (!this.activePetBySlot.TryRemove(entry.Key, out petIdentity))
                {
                    continue;
                }

                if (clearRestoreState)
                {
                    PendingPetRestore removedRestore;
                    this.pendingRestoreBySlot.TryRemove(entry.Key, out removedRestore);
                }

                this.DespawnPetIdentity(owner, petIdentity);
            }

            if (clearRestoreState)
            {
                this.SyncOwnerPetCounter(owner);
            }
        }

        private void DespawnPetIdentity(ICharacter owner, Identity petIdentity)
        {
            RemovePetMessageHandler.Default.SendRemovePet(owner, petIdentity);

            Character pet = this.ResolvePetCharacter(owner, petIdentity);
            if (pet != null)
            {
                Playfield petPlayfield = pet.Playfield as Playfield;
                if (petPlayfield != null)
                {
                    petPlayfield.DespawnNpcImmediately(pet);
                    return;
                }

                if (pet.Playfield != null)
                {
                    pet.Playfield.Announce(DespawnMessageHandler.Default.Create(petIdentity));
                }

                Pool.Instance.RemoveObject(pet);
                return;
            }

            if (owner.Playfield != null)
            {
                owner.Playfield.Announce(DespawnMessageHandler.Default.Create(petIdentity));
            }
        }

        private Character ResolvePetCharacter(ICharacter owner, Identity petIdentity)
        {
            if (owner != null && owner.Playfield != null)
            {
                Character onOwnerPlayfield = owner.Playfield.FindByIdentity<Character>(petIdentity);
                if (onOwnerPlayfield != null)
                {
                    return onOwnerPlayfield;
                }
            }

            Character pooledPet = Pool.Instance.GetObject<Character>(petIdentity);
            return pooledPet;
        }

        private void PurgeOrphanPetMobsForOwner(ICharacter owner)
        {
            if (owner == null)
            {
                return;
            }

            int ownerInstance = owner.Identity.Instance;
            var activePetInstances = new HashSet<int>(
                this.GetActiveSlotsForOwner(ownerInstance).Select(x => x.Value.Instance));

            foreach (Character pooledPet in Pool.Instance.GetAll<Character>((int)IdentityType.CanbeAffected).ToList())
            {
                if (pooledPet == null
                    || pooledPet.Identity.Instance == ownerInstance
                    || pooledPet.Stats[StatIds.petmaster].Value != ownerInstance)
                {
                    continue;
                }

                if (activePetInstances.Contains(pooledPet.Identity.Instance))
                {
                    continue;
                }

                this.DespawnPetIdentity(owner, pooledPet.Identity);
            }
        }

        private void DespawnUnlinkedPetMob(Character petCharacter)
        {
            if (petCharacter == null)
            {
                return;
            }

            Playfield petPlayfield = petCharacter.Playfield as Playfield;
            if (petPlayfield != null)
            {
                petPlayfield.DespawnNpcImmediately(petCharacter);
                return;
            }

            Pool.Instance.RemoveObject(petCharacter);
        }

        private IEnumerable<KeyValuePair<PetSlotKey, Identity>> GetActiveSlotsForOwner(int ownerInstance)
        {
            return this.activePetBySlot.Where(x => x.Key.OwnerInstance == ownerInstance);
        }

        private IEnumerable<KeyValuePair<PetSlotKey, PendingPetRestore>> GetPendingSlotsForOwner(int ownerInstance)
        {
            return this.pendingRestoreBySlot.Where(x => x.Key.OwnerInstance == ownerInstance);
        }

        private void SyncOwnerPetCounter(ICharacter owner)
        {
            // MP heal (1016) and attack (1015) pets are independent slots on the client.
            // Live AO never aggregates active pet count onto the owner's petcounter stat;
            // pushing that value makes the client reject a second pet category.
        }

        private SimpleCharFullUpdateMessage ConstructPetSpawnFullUpdate(
            Character petCharacter,
            int petSlotStrain)
        {
            SimpleCharFullUpdateMessage message = SimpleCharFullUpdate.ConstructMessage(petCharacter);
            if (petSlotStrain == PetSlotClassifier.HealingPetStrain)
            {
                PetSummonScfuExtensions.ApplyCapturedMpPetMetadata(message, petSlotStrain);
            }

            return message;
        }

        private void FinalizePetCharacter(Character petCharacter)
        {
            petCharacter.Stats.SetBaseValueWithoutTriggering((int)StatIds.visualflags, 31);
            petCharacter.Stats.SetBaseValueWithoutTriggering((int)StatIds.runspeed, 737);
            petCharacter.Stats.SetBaseValueWithoutTriggering((int)StatIds.expansion, 1);

            int temp = petCharacter.Stats[StatIds.level].Value;
            temp = petCharacter.Stats[StatIds.agility].Value;
            temp = petCharacter.Stats[StatIds.headmesh].Value;

            uint maxLife = (uint)petCharacter.Stats[StatIds.life].Value;
            petCharacter.Stats.SetBaseValueWithoutTriggering((int)StatIds.health, maxLife);
        }

        private void ApplyHealingPetNanoPool(Character petCharacter, int petSlotStrain)
        {
            if (petSlotStrain != PetSlotClassifier.HealingPetStrain)
            {
                return;
            }

            petCharacter.Stats.SetBaseValueWithoutTriggering(
                (int)StatIds.currentnano,
                (uint)PetCombatRules.HealingPetCapturedCurrentNano);
        }

        private void SendPetStatToOwner(
            ZoneClient ownerClient,
            Identity petIdentity,
            StatIds statId,
            uint statValue)
        {
            if (ownerClient == null)
            {
                return;
            }

            ownerClient.SendCompressed(
                new StatMessage
                {
                    Identity = petIdentity,
                    Stats = new[]
                    {
                        new GameTuple<CharacterStat, uint>
                        {
                            Value1 = (CharacterStat)(int)statId,
                            Value2 = statValue
                        }
                    }
                });
        }

        private const int SummonCaptureStatIdA = 0x4A5;

        private const int SummonCaptureStatIdB = 0x4A0;

        private void SendBelamorteSummonPostLink(
            ZoneClient ownerClient,
            ICharacter owner,
            Character petCharacter,
            int summonNanoId,
            string petHash,
            int petTypeId)
        {
            PetSummonSpellListService.SendOwnerPetSummon(
                owner,
                summonNanoId,
                petHash,
                petTypeId,
                PetSlotClassifier.HealingPetStrain);
            this.SendPostSummonPetStatsToOwner(ownerClient, petCharacter);
            this.SendPetWantedDirectionToOwner(ownerClient, petCharacter.Identity);
            PetSummonSpellListService.SendPetSummonSpellLists(
                owner,
                petCharacter.Identity,
                PetSlotClassifier.HealingPetStrain);
        }

        private void SendPostSummonPetStatsToOwner(ZoneClient ownerClient, Character petCharacter)
        {
            this.SendPetStatToOwner(
                ownerClient,
                petCharacter.Identity,
                StatIds.pettype,
                (uint)petCharacter.Stats[StatIds.pettype].Value);
            this.SendPetStatToOwner(ownerClient, petCharacter.Identity, StatIds.petstate, (uint)petCharacter.Stats[StatIds.petstate].Value);
            this.SendPetStatToOwner(ownerClient, petCharacter.Identity, StatIds.side, (uint)petCharacter.Stats[StatIds.side].Value);
            this.SendPetStatToOwner(ownerClient, petCharacter.Identity, StatIds.battlestationside, (uint)petCharacter.Stats[StatIds.battlestationside].Value);
            this.SendPetStatToOwner(ownerClient, petCharacter.Identity, StatIds.runspeed, (uint)petCharacter.Stats[StatIds.runspeed].Value);
            this.SendPetStatToOwner(ownerClient, petCharacter.Identity, StatIds.expansion, (uint)petCharacter.Stats[StatIds.expansion].Value);
            this.SendPetStatToOwner(ownerClient, petCharacter.Identity, (StatIds)SummonCaptureStatIdA, 0);
            this.SendPetStatToOwner(ownerClient, petCharacter.Identity, (StatIds)SummonCaptureStatIdB, 0);
        }

        private void SendPetWantedDirectionToOwner(ZoneClient ownerClient, Identity petIdentity)
        {
            if (ownerClient == null)
            {
                return;
            }

            ownerClient.SendCompressed(
                new SetWantedDirectionMessage
                {
                    Identity = petIdentity,
                    Unknown = 0,
                    DirectinVector =
                        new SmokeLounge.AOtomation.Messaging.GameData.Vector3
                        {
                            X = 0,
                            Y = -1,
                            Z = 0
                        }
                });
        }

        private void RegisterOwnerSummonNano(ICharacter owner, int summonNanoId, int petSlotStrain)
        {
            if (owner == null || summonNanoId <= 0 || owner.Playfield == null)
            {
                return;
            }

            if (!ActiveNanoRuntimeService.Default.ApplyActiveNano(
                owner,
                summonNanoId,
                0,
                default(Identity),
                petSlotStrain))
            {
                return;
            }

            // Live capture (20260710-185528) registers the summon nano via owner SpellList only.
            // SetNanoDuration(0) creates a ghost pet/nano slot (NoName) without a linked world pet.

            LogUtil.Debug(
                DebugInfoDetail.GameFunctions,
                string.Format(
                    "RegisterOwnerSummonNano owner={0} nano={1} strain={2}",
                    owner.Identity,
                    summonNanoId,
                    petSlotStrain));
        }

        private bool HasActiveSummonPetNanoInStrain(ICharacter owner, int petSlotStrain)
        {
            IActiveNano activeNano;
            if (!owner.ActiveNanos.TryGetValue(petSlotStrain, out activeNano) || activeNano == null)
            {
                return false;
            }

            return NanoEventRuntimeService.Default.HasSummonPetOnUse(activeNano.ID);
        }

        private struct PetSlotKey : IEquatable<PetSlotKey>
        {
            public PetSlotKey(int ownerInstance, int strain)
            {
                this.OwnerInstance = ownerInstance;
                this.Strain = strain;
            }

            public int OwnerInstance { get; private set; }

            public int Strain { get; private set; }

            public bool Equals(PetSlotKey other)
            {
                return this.OwnerInstance == other.OwnerInstance && this.Strain == other.Strain;
            }

            public override bool Equals(object obj)
            {
                if (obj is PetSlotKey)
                {
                    return this.Equals((PetSlotKey)obj);
                }

                return false;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (this.OwnerInstance * 397) ^ this.Strain;
                }
            }
        }

        private sealed class PendingPetRestore
        {
            public string PetHash { get; set; }

            public int PetTypeId { get; set; }

            public int PetSlotStrain { get; set; }

            public int SummonNanoId { get; set; }

            public bool ShouldRestore { get; set; }
        }
    }
}
