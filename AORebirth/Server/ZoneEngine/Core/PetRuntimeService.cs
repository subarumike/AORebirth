#region License



// Copyright (c) 2005-2014, CellAO Team

//

// All rights reserved.



#endregion



namespace ZoneEngine.Core

{

    #region Usings ...



    using System.Collections.Concurrent;



    using AORebirth.Core.Entities;

    using AORebirth.Core.NPCHandler;

    using AORebirth.Core.Playfields;

    using AORebirth.Core.Vector;

    using AORebirth.Enums;

    using AORebirth.Interfaces;

    using AORebirth.ObjectManager;



    using SmokeLounge.AOtomation.Messaging.GameData;



    using ZoneEngine.Core.Controllers;

    using ZoneEngine.Core.MessageHandlers;

    using ZoneEngine.Core.Packets;

    using Utility;



    #endregion



    public sealed class PetRuntimeService

    {

        private static readonly PetRuntimeService DefaultInstance = new PetRuntimeService();



        private readonly ConcurrentDictionary<int, Identity> activePetByOwnerInstance =

            new ConcurrentDictionary<int, Identity>();



        private readonly ConcurrentDictionary<int, PendingPetRestore> pendingRestoreByOwnerInstance =

            new ConcurrentDictionary<int, PendingPetRestore>();



        private PetRuntimeService()

        {

        }



        public static PetRuntimeService Default

        {

            get { return DefaultInstance; }

        }



        public bool SummonPet(ICharacter owner, string petHash, int petTypeId)

        {

            if (owner == null || owner.Playfield == null || string.IsNullOrWhiteSpace(petHash))

            {

                return false;

            }



            string mobHash = PetMobTemplateResolver.Resolve(petHash);

            if (string.IsNullOrWhiteSpace(mobHash))

            {

                LogUtil.Debug(DebugInfoDetail.GameFunctions, "SummonPet unknown pet hash " + petHash);

                return false;

            }



            this.DismissPet(owner, false);



            Coordinate ownerCoord = owner.Coordinates();

            var spawnCoord = new Coordinate(

                ownerCoord.x + 1.5f,

                ownerCoord.y,

                ownerCoord.z + 1.5f);



            var controller = new NPCController();

            int ownerLevel = owner.Stats[StatIds.level].Value;

            Character petCharacter = NonPlayerCharacterHandler.SpawnMobFromTemplate(

                mobHash,

                owner.Playfield.Identity,

                spawnCoord,

                owner.RawHeading,

                controller,

                ownerLevel);



            if (petCharacter == null)

            {

                return false;

            }



            petCharacter.Playfield = owner.Playfield;

            petCharacter.Stats[StatIds.side].Value = owner.Stats[StatIds.side].Value;

            petCharacter.Stats[StatIds.petmaster].Value = owner.Identity.Instance;

            petCharacter.Stats[StatIds.pettype].Value = petTypeId;

            petCharacter.Stats[StatIds.petstate].Value = 1;

            petCharacter.Stats[StatIds.petcounter].Value = 1;

            petCharacter.DoNotDoTimers = false;



            owner.Playfield.Announce(SimpleCharFullUpdate.ConstructMessage(petCharacter));

            AppearanceUpdateMessageHandler.Default.Send(petCharacter);

            AddPetMessageHandler.Default.SendAddPet(owner, petCharacter.Identity);

            controller.Follow(owner.Identity, 2.0);



            this.activePetByOwnerInstance[owner.Identity.Instance] = petCharacter.Identity;

            this.pendingRestoreByOwnerInstance[owner.Identity.Instance] = new PendingPetRestore

            {

                PetHash = petHash,

                PetTypeId = petTypeId,

                ShouldRestore = true

            };



            this.SyncOwnerPetCounter(owner, 1);



            LogUtil.Debug(

                DebugInfoDetail.GameFunctions,

                string.Format(

                    "SummonPet owner={0} pet={1} hash={2} mob={3} type={4}",

                    owner.Identity,

                    petCharacter.Identity,

                    petHash,

                    mobHash,

                    petTypeId));



            return true;

        }



        public void DismissPet(ICharacter owner)

        {

            this.DismissPet(owner, true);

        }



        public void StashPetForZoneTransfer(ICharacter owner)

        {

            if (owner == null)

            {

                return;

            }



            int ownerInstance = owner.Identity.Instance;

            Identity petIdentity;

            if (this.activePetByOwnerInstance.TryGetValue(ownerInstance, out petIdentity))

            {

                PendingPetRestore pendingRestore;

                if (!this.pendingRestoreByOwnerInstance.TryGetValue(ownerInstance, out pendingRestore))

                {

                    pendingRestore = new PendingPetRestore();

                }



                pendingRestore.ShouldRestore = true;

                this.pendingRestoreByOwnerInstance[ownerInstance] = pendingRestore;



                LogUtil.Debug(

                    DebugInfoDetail.GameFunctions,

                    string.Format(

                        "StashPet owner={0} hash={1} type={2}",

                        owner.Identity,

                        pendingRestore.PetHash,

                        pendingRestore.PetTypeId));

            }



            this.DismissPet(owner, false);

        }



        public void TryRestorePetAfterZoneIn(ICharacter owner)

        {

            if (owner == null || owner.Playfield == null)

            {

                return;

            }



            PendingPetRestore pendingRestore;

            if (!this.pendingRestoreByOwnerInstance.TryGetValue(owner.Identity.Instance, out pendingRestore)

                || !pendingRestore.ShouldRestore

                || string.IsNullOrWhiteSpace(pendingRestore.PetHash))

            {

                return;

            }



            if (!this.HasActiveSummonPetNano(owner))

            {

                PendingPetRestore removed;

                this.pendingRestoreByOwnerInstance.TryRemove(owner.Identity.Instance, out removed);

                this.SyncOwnerPetCounter(owner, 0);

                return;

            }



            LogUtil.Debug(

                DebugInfoDetail.GameFunctions,

                string.Format(

                    "RestorePet owner={0} hash={1} type={2}",

                    owner.Identity,

                    pendingRestore.PetHash,

                    pendingRestore.PetTypeId));



            this.SummonPet(owner, pendingRestore.PetHash, pendingRestore.PetTypeId);

        }



        public bool HasPendingRestore(int ownerInstance)

        {

            PendingPetRestore pendingRestore;

            return this.pendingRestoreByOwnerInstance.TryGetValue(ownerInstance, out pendingRestore)

                && pendingRestore.ShouldRestore

                && !string.IsNullOrWhiteSpace(pendingRestore.PetHash);

        }



        private void DismissPet(ICharacter owner, bool clearRestoreState)

        {

            if (owner == null)

            {

                return;

            }



            int ownerInstance = owner.Identity.Instance;

            Identity petIdentity;

            if (!this.activePetByOwnerInstance.TryRemove(ownerInstance, out petIdentity))

            {

                if (clearRestoreState)

                {

                    PendingPetRestore removedRestore;

                    this.pendingRestoreByOwnerInstance.TryRemove(ownerInstance, out removedRestore);

                    this.SyncOwnerPetCounter(owner, 0);

                }



                return;

            }



            if (clearRestoreState)

            {

                PendingPetRestore removedRestore;

                this.pendingRestoreByOwnerInstance.TryRemove(ownerInstance, out removedRestore);

            }



            IPlayfield playfield = owner.Playfield;

            if (playfield != null)

            {

                RemovePetMessageHandler.Default.SendRemovePet(owner, petIdentity);



                ICharacter pet = playfield.FindByIdentity<ICharacter>(petIdentity);

                if (pet != null)

                {

                    Playfield concretePlayfield = playfield as Playfield;

                    if (concretePlayfield != null)

                    {

                        concretePlayfield.DespawnNpcImmediately(pet);

                    }

                    else

                    {

                        playfield.Announce(DespawnMessageHandler.Default.Create(petIdentity));

                        Pool.Instance.RemoveObject((Character)pet);

                    }

                }

                else

                {

                    playfield.Announce(DespawnMessageHandler.Default.Create(petIdentity));

                }

            }



            if (clearRestoreState)

            {

                this.SyncOwnerPetCounter(owner, 0);

            }

        }



        private void SyncOwnerPetCounter(ICharacter owner, int value)

        {

            if (owner == null)

            {

                return;

            }



            owner.Stats[StatIds.petcounter].Value = value;

            owner.SendChangedStats();

        }



        private bool HasActiveSummonPetNano(ICharacter owner)

        {

            foreach (IActiveNano activeNano in owner.ActiveNanos.Values)

            {

                if (activeNano != null && NanoEventRuntimeService.Default.HasSummonPetOnUse(activeNano.ID))

                {

                    return true;

                }

            }



            return false;

        }



        private sealed class PendingPetRestore

        {

            public string PetHash { get; set; }



            public int PetTypeId { get; set; }



            public bool ShouldRestore { get; set; }

        }

    }

}


