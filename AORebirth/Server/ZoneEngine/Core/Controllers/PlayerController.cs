#region License

// Copyright (c) 2005-2014, CellAO Team
// 
// 
// All rights reserved.
// 
// 
// Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
// 
// 
//     * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//     * Neither the name of the CellAO Team nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.
// 
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
// EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
// PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
// PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
// LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
// NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

#endregion

namespace ZoneEngine.Core.Controllers
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Events;
    using AORebirth.Core.Functions;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Nanos;
    using AORebirth.Core.Network;
    using AORebirth.Core.Requirements;
    using AORebirth.Core.Statels;
    using AORebirth.Core.Vector;
    using AORebirth.Database.Dao;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;
    
    using ZoneEngine.Core;
    using ZoneEngine.Core.Functions;
    using ZoneEngine.Core.Functions.GameFunctions;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Packets;
    using ZoneEngine.Core.Playfields;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;

    #endregion

    /// <summary>
    /// </summary>
    public class PlayerController : IController
    {
        // All functions return true if reply should be sent, false if no reply needed

        /// <summary>
        /// </summary>
        private Utility.WeakReference<ICharacter> character;

        private bool disposed = false;

        private CharacterState state = CharacterState.Idle;

        public PlayerController(IZoneClient client)
        {
            this.Client = client;
        }

        public CharacterState State
        {
            get
            {
                return this.state;
            }
            set
            {
                this.state = value;
            }
        }

        /// <summary>
        /// </summary>
        public ICharacter Character
        {
            get
            {
                // Disconnect/dispose can run before Character was bound (or after the weak
                // ref was never set). Callers already null-check; do not throw NRE here.
                if (this.character == null)
                {
                    return null;
                }

                return this.character.Target;
            }

            set
            {
                if (value == null)
                {
                    throw new Exception("Dont try to weak reference null");
                }

                this.character = new Utility.WeakReference<ICharacter>(value);
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public IZoneClient Client { get; set; }

        public void CallFunction(Function function, IEntity caller)
        {
            // CellAO always applies CallFunction on Character (no target resolve).
            // Teleport(53016) must stay on the caster — target resolve broke SL statues.
            // SaveChar(53032) Insurance Terminal must also stay on the caster.
            if (function != null
                && (function.FunctionType == (int)FunctionType.Teleport
                    || function.FunctionType == (int)FunctionType.SaveChar))
            {
                FunctionCollection.Instance.CallFunction(
                    function.FunctionType,
                    this.Character,
                    caller,
                    this.Character,
                    function.Arguments.Values.ToArray());
                return;
            }

            IInstancedEntity functionTarget;
            if (!this.TryResolveFunctionTarget(function, out functionTarget))
            {
                return;
            }

            FunctionCollection.Instance.CallFunction(
                function.FunctionType,
                this.Character,
                caller,
                functionTarget,
                function.Arguments.Values.ToArray());
        }

        private bool TryResolveFunctionTarget(Function function, out IInstancedEntity functionTarget)
        {
            functionTarget = this.Character;
            if (function == null || this.Character == null || this.Character.Playfield == null)
            {
                return functionTarget != null;
            }

            switch ((ItemTarget)function.Target)
            {
                case ItemTarget.Target:
                case ItemTarget.Selectedtarget:
                {
                    Identity preferred = this.Character.SelectedTarget;
                    if (preferred.Instance == 0
                        || preferred.Instance == this.Character.Identity.Instance)
                    {
                        // Attack perk actions (Quick Bash etc.) often need fighting target when
                        // UsePerk wire Target is the caster (capture 20260715-194155).
                        preferred = this.Character.FightingTarget;
                    }

                    if (preferred.Instance == 0
                        || preferred.Instance == this.Character.Identity.Instance)
                    {
                        // Inventory treatment (rechargers/stims): no other target → self.
                        functionTarget = this.Character;
                        return true;
                    }

                    functionTarget = this.Character.Playfield.FindByIdentity(preferred);
                    if (functionTarget == null)
                    {
                        functionTarget = this.Character;
                    }

                    return true;
                }

                case ItemTarget.Fightingtarget:
                {
                    Identity fightTarget = this.Character.FightingTarget.Instance != 0
                        ? this.Character.FightingTarget
                        : this.Character.SelectedTarget;
                    if (fightTarget.Instance == 0)
                    {
                        return false;
                    }

                    functionTarget = this.Character.Playfield.FindByIdentity(fightTarget);
                    return functionTarget != null;
                }
            }

            functionTarget = this.Character;
            return true;
        }

        public void MoveTo(Vector3 destination)
        {
            FollowTargetMessageHandler.Default.Send(this.Character, this.Character.RawCoordinates, destination);
        }

        public void Run()
        {
            this.Character.UpdateMoveType(25); // Magic number 25 = Run
        }

        public void StopMovement()
        {
            this.Character.UpdateMoveType(2); // Magic number: Stop movement
        }

        public void Walk()
        {
            this.Character.UpdateMoveType(24); // Magic number 24 = Walk
        }

        public bool SaveToDatabase
        {
            get
            {
                return true;
            }
        }

        public bool IsFollowing()
        {
            return false;
        }

        public void DoFollow()
        {
            throw new NotImplementedException();
        }

        public void StartPatrolling()
        {
            throw new NotImplementedException();
        }

        #region Generic character actions

        /// <summary>
        /// </summary>
        /// <param name="target">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool LookAt(Identity target)
        {
            // TODO: add Team lookup here too (F1-F6 for example)
            if (target.Instance == 0)
            {
                return false;
            }

            if (target.Instance == this.Character.Identity.Instance)
            {
                this.Character.SetTarget(this.Character.Identity);
                return true;
            }

            if (this.Character.Playfield != null)
            {
                ICharacter resolved = Pool.Instance.GetObject<ICharacter>(this.Character.Playfield.Identity, target)
                    ?? Pool.Instance.GetObject<ICharacter>(
                        this.Character.Playfield.Identity,
                        new Identity
                        {
                            Type = IdentityType.CanbeAffected,
                            Instance = target.Instance
                        });
                if (resolved != null)
                {
                    this.Character.SetTarget(resolved.Identity);
                    return true;
                }
            }

            if (Pool.Instance.Contains(this.Character.Playfield.Identity, target))
            {
                this.Character.SetTarget(target);
                return true;
            }

            return false;
        }

        /// <summary>
        /// </summary>
        /// <param name="nanoId">
        /// </param>
        /// <param name="target">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool CastNano(int nanoId, Identity target)
        {
            // Procedure:
            // 1. Check if nano can be casted (criteria to Use (3))
            // 2. Lock nanocasting ability
            // 3. Wait for cast attack delay
            // 4. Check target's restance to the nano
            // 5. Execute nanos gamefunctions
            // 6. Wait for nano recharge delay
            // 7. Unlock nano casting

            // Crystal 300440 uploads program 300439; remap mistaken uploaded/cast ids.
            nanoId = SummonedBucketheadTechnodealerRuntime.NormalizeNanoId(nanoId);

            if (!NanoLoader.NanoList.ContainsKey(nanoId))
            {
                ChatTextMessageHandler.Default.Send(this.Character, "Unknown nano program.");
                return false;
            }

            if (!this.Character.UploadedNanos.Any(x => x.NanoId == nanoId)
                && !SummonedBucketheadTechnodealerRuntime.HasUploadedSummonNano(this.Character, nanoId))
            {
                PetShellItemService.Default.TryEnsureNanoUploaded(this.Character, nanoId);
            }

            if (!this.Character.UploadedNanos.Any(x => x.NanoId == nanoId)
                && !SummonedBucketheadTechnodealerRuntime.HasUploadedSummonNano(this.Character, nanoId))
            {
                ChatTextMessageHandler.Default.Send(
                    this.Character,
                    "Nano is not uploaded. Use the nano crystal first.");
                return false;
            }

            if (NanoEventRuntimeService.Default.HasSummonPetOnUse(nanoId))
            {
                int petStrain = ActiveNanoRuntimeService.Default.ResolveNanoStrain(this.Character, nanoId);
                ActiveNanoRuntimeService.Default.PurgeOrphanSummonNanoInStrain(
                    this.Character,
                    petStrain,
                    true);
            }

            if (!TeamWarpRuntime.IsBeaconWarpNano(nanoId)
                && !ActiveNanoRuntimeService.Default.CanActivateNano(this.Character, nanoId))
            {
                ChatTextMessageHandler.Default.Send(this.Character, "Not enough NCU to activate this nano.");
                return false;
            }

            if (NanoEventRuntimeService.Default.HasSummonPetOnUse(nanoId)
                && !PetShellCatalog.UsesShellOnSummon(
                    this.Character.Stats[StatIds.profession].Value,
                    nanoId))
            {
                // Shell-based casts create/refresh a shell item, not a living pet.
                // Living-pet uniqueness is enforced when the shell is used.
                PetSummonParams summonParams;
                if (PetSummonNanoCatalog.TryResolve(this.Character, nanoId, out summonParams))
                {
                    int summonPetStrain = PetSlotClassifier.ResolveStrain(summonParams.PetHash);
                    if (summonPetStrain == PetSlotClassifier.RegularPetStrain
                        && PetRuntimeService.Default.HasLivingAttackPet(this.Character))
                    {
                        ChatTextMessageHandler.Default.Send(this.Character, "You can have just 1 Attack Pet.");
                        return false;
                    }

                    if (summonPetStrain == PetSlotClassifier.HealingPetStrain
                        && PetRuntimeService.Default.HasLivingHealingPet(this.Character))
                    {
                        ChatTextMessageHandler.Default.Send(this.Character, "You can have just 1 Heal Pet.");
                        return false;
                    }

                    if (PetSlotClassifier.IsBureaucratCompanionStrain(summonPetStrain)
                        && PetRuntimeService.Default.HasLivingBureaucratCompanionPet(this.Character))
                    {
                        ChatTextMessageHandler.Default.Send(
                            this.Character,
                            "You can have just 1 Bureaucrat Companion Pet.");
                        return false;
                    }
                }
            }

            NanoFormula nano = NanoLoader.NanoList[nanoId];
            int strain = NanoEventRuntimeService.Default.HasSummonPetOnUse(nanoId)
                ? ActiveNanoRuntimeService.Default.ResolveNanoStrain(this.Character, nanoId)
                : nano.NanoStrain();

            if (NanoEventRuntimeService.Default.HasSummonPetOnUse(nanoId)
                && (target.Type == IdentityType.None || target.Instance == 0))
            {
                target = this.Character.Identity;
            }

            CastNanoSpellMessageHandler.Default.Send(this.Character, nanoId, target);

            // CharacterAction 107 - Finish nano casting
            int attackDelay = this.Character.CalculateNanoAttackTime(nano);
            Console.WriteLine("Attack-Delay: " + attackDelay);
            if (attackDelay != 1234567890)
            {
                Thread.Sleep(attackDelay * 10);
            }

            // Check here for nanoresist of the target, maybe the 1 in finishnanocasting is kind of did land/didnt land flag
            CharacterActionMessageHandler.Default.FinishNanoCasting(
                this.Character,
                CharacterActionType.FinishNanoCasting,
                Identity.None,
                1,
                nanoId);

            // TODO: Calculate nanocost modifiers etc.
            this.Character.Stats[StatIds.currentnano].Value -= nano.getItemAttribute(407);

            int duration = nano.getItemAttribute(8);
            bool isSummonPetNano = NanoEventRuntimeService.Default.HasSummonPetOnUse(nanoId);
            bool usesShell = isSummonPetNano
                && PetShellCatalog.UsesShellOnSummon(
                    this.Character.Stats[StatIds.profession].Value,
                    nanoId);

            if (usesShell)
            {
                // Live AO shell nanos (20260713-142159): FinishNanoCasting + shell item only.
                // SetNanoDuration creates a ghost NoName pet slot without a linked world pet.
                PetShellItemService.Default.TryGiveShellForNano(this.Character, nanoId);
            }
            else if (isSummonPetNano)
            {
                PetSummonParams summonParams;
                if (PetSummonNanoCatalog.TryResolve(this.Character, nanoId, out summonParams))
                {
                    PetRuntimeService.Default.SummonPet(
                        this.Character,
                        summonParams.PetHash,
                        summonParams.PetTypeId,
                        strain,
                        nanoId);
                }
                else
                {
                    string preferredHash = PetSummonNanoCatalog.GetPreferredPetHash(nanoId);
                    string hint = string.IsNullOrWhiteSpace(preferredHash)
                        ? "Could not resolve a pet for this nano."
                        : "Could not resolve a pet for this nano. Import mob template "
                            + preferredHash
                            + " into the MySQL database.";
                    ChatTextMessageHandler.Default.Send(this.Character, hint);
                }
            }
            else if (SummonedBucketheadTechnodealerRuntime.IsSummonNano(nanoId))
            {
                // Dedicated path: do not depend on FunctionCollection SpawnMonster2 registration.
                SummonedBucketheadTechnodealerRuntime.EnsureSpawnedAfterCast(this.Character, nanoId);
            }
            else if (AmbientRestorationAuraRuntime.IsAmbientRestorationNano(nanoId))
            {
                // Capture 20260722-keeper-exect-nano: 20s aura pulse
                // (CastNanoSpell pair + SpellList red sparkles + heal Keeper and team).
                // Do not ExecuteOnUse TeamCast of all four children — live ticks one tier.
                AmbientRestorationAuraRuntime.StartOrRefresh(this.Character);
            }
            else
            {
                // Instant OnUse Hit/effects (e.g. Trader Weak/Patchy Health Funnel):
                // cast target must be SelectedTarget so Function.Target resolves to the mob.
                if (target.Instance != 0)
                {
                    this.Character.SetTarget(target);
                }

                NanoEventRuntimeService.Default.ExecuteOnUseEvents(this.Character, nano);
                // Flush OnUse stat writes. MapsC SetFlag (585) is a no-op; Sync after duration.
                this.SendChangedStats();
                // Mongo Slam (100198) taunt AoE is in nanos.dat OnUse only.
                // Do not inject slam effects onto Composite Utility Expertise (287046).
                Character slamCaster = this.Character as Character;
                if (slamCaster != null)
                {
                    MongoSlamRuntimeService.ApplyCaptureBackedSlamEffects(slamCaster, nanoId);
                }

                // Capture 20260723-053632 Sparrow Flight: SpellList after OnUse morph/flight.
                AdventurerMorphFlightRuntime.OnMorphNanoApplied(this.Character, nanoId);

                // Instant Hit drain nanos must not be treated as NCU buffs on the caster.
                if (duration > 0 && !NanoEventRuntimeService.Default.HasOffensiveHitOnUse(nano))
                {
                    // Capture 20260806-085523: self-cast Target is often None — duration
                    // Identity must still be the caster SimpleChar or cancel cannot reverse morph.
                    Identity durationIdentity = (target.Type != IdentityType.None && target.Instance != 0)
                                                   ? target
                                                   : this.Character.Identity;
                    CharacterActionMessageHandler.Default.SetNanoDuration(
                        this.Character,
                        durationIdentity,
                        nanoId,
                        duration);
                }
                else if (duration <= 0
                         && AdventurerMorphFlightRuntime.IsMorphFlightNano(nanoId)
                         && !NanoEventRuntimeService.Default.HasOffensiveHitOnUse(nano))
                {
                    // Some vehicle nanos report attribute 8 as 0; still need an NCU entry.
                    Identity durationIdentity = (target.Type != IdentityType.None && target.Instance != 0)
                                                   ? target
                                                   : this.Character.Identity;
                    CharacterActionMessageHandler.Default.SetNanoDuration(
                        this.Character,
                        durationIdentity,
                        nanoId,
                        AdventurerMorphFlightRuntime.FallbackNcuDurationCentiseconds);
                }

                // Sole MapsC writer: unlock only while Overview is in ActiveNanos.
                NanoEventRuntimeService.Default.SyncOverviewMapFlags(this.Character);
            }

            Thread.Sleep(nano.getItemAttribute(210) * 10); // Recharge Delay
            return false;
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool Search()
        {
            // Procedure:
            // 1. Gather stealthed entities inside range
            // 2. Check against each entities concealment skill
            // 3. Unhide successful found entities
            // 4. Lock search action for ?? seconds

            return false;
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool Sneak()
        {
            // Procedure: 
            // 1. Gather surrounding mobs/players
            // 2. Check concealment against their perception skill
            // 3. Vanish for successful rolled chars/mobs

            return false;
        }

        /// <summary>
        /// </summary>
        /// <param name="visualFlag">
        /// </param>
        /// <returns>
        /// </returns>
        public bool ChangeVisualFlag(int visualFlag)
        {
            // Procedure:
            // 1. Set visualFlags stat
            // 2. Send AppearanceUpdate
            this.Character.Stats[StatIds.visualflags].Value = visualFlag;
            AppearanceUpdateMessageHandler.Default.Send(this.Character);
            return false;
        }

        /// <summary>
        /// </summary>
        /// <param name="moveType">
        /// </param>
        /// <param name="newCoordinates">
        /// </param>
        /// <param name="heading">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool Move(int moveType, Coordinate newCoordinates, Quaternion heading)
        {
            // Procedure:
            // 1. Check if new coordinates are plausible (in range of runspeed since last update)
            // 2. Set coordinates & heading

            // Is this correct? Shouldnt the client input be compared to the prediction and then be overridden to prevent teleportation exploits? 
            // - Algorithman

            // give it a bit uncertainty (2.0f)
            LogUtil.Debug(
                DebugInfoDetail.Movement,
                newCoordinates.ToString() + "<->" + this.Character.Coordinates().ToString());
            // if (newCoordinates.Distance2D(this.Character.Coordinates) < 2.0f)
            {
                this.Character.SetCoordinates(newCoordinates, heading);
                this.Character.UpdateMoveType((byte)moveType);
            }
            /*
            else
            {
                this.Character.StopMovement();
            }
            */
            return true;
        }

        /// <summary>
        /// </summary>
        /// <param name="sourceContainerType">
        /// </param>
        /// <param name="sourcePlacement">
        /// </param>
        /// <param name="target">
        /// </param>
        /// <param name="targetPlacement">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool ContainerAddItem(int sourceContainerType, int sourcePlacement, Identity target, int targetPlacement)
        {
            return InventoryContainerRuntimeService.Default.MovePlayerControllerContainerItem(
                this.Character,
                sourceContainerType,
                sourcePlacement,
                target,
                targetPlacement);
        }

        /// <summary>
        /// </summary>
        /// <param name="target">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool Follow(Identity target)
        {
            // Procedure:
            // 1. Check if target is still ingame
            // 2. Find a path to target and head accordingly
            // 3. Start movement (if not already)
            // 4. Start Pathfinding loop

            throw new NotImplementedException();
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool Stand()
        {
            // Procedure:
            // 1. Update characters move mode
            // 2. Announce the action to the playfield (or range)
            // 3. If logout timer pending, cancel pending logout timer

            if (this.Character.InLogoutTimerPeriod())
            {
                this.Character.StopLogoutTimer();
            }

            this.Character.UpdateMoveType(37); // Magic number -> Stand
            return true;
        }

        /// <summary>
        /// </summary>
        /// <param name="action">
        /// </param>
        /// <param name="parameter1">
        /// </param>
        /// <param name="parameter2">
        /// </param>
        /// <param name="parameter3">
        /// </param>
        /// <param name="parameter4">
        /// </param>
        /// <param name="parameter5">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool SocialAction(
            SocialAction action,
            byte parameter1,
            byte parameter2,
            byte parameter3,
            byte parameter4,
            int parameter5)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// </summary>
        /// <param name="target">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool Trade(Identity target)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Player specific actions

        /// <summary>
        /// </summary>
        /// <param name="itemPosition">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool UseItem(Identity itemPosition)
        {
            return InventoryContainerRuntimeService.Default.UseInventoryItem(this.Character, itemPosition);
        }

        public bool TryUseBackpackContainer(Identity itemPosition)
        {
            return InventoryContainerRuntimeService.Default.TryUseBackpackContainer(this.Character, itemPosition);
        }

        public bool UseStatel(Identity identity, EventType eventType = EventType.OnUse)
        {
            if (PlayfieldLoader.PFData.ContainsKey(this.Character.Playfield.Identity.Instance))
            {
                StatelData sd =
                    PlayfieldLoader.PFData[this.Character.Playfield.Identity.Instance].Statels.FirstOrDefault(
                        x => (x.Identity.Type == identity.Type) && (x.Identity.Instance == identity.Instance));

                if (sd != null)
                {
                    Event onUse = sd.Events.FirstOrDefault(x => x.EventType == eventType);
                    if (onUse != null)
                    {
                        onUse.Perform(this.Character, sd);
                    }
                }
            }
            return true;
        }

        public void SendChatText(string text)
        {
            ChatTextMessageHandler.Default.Send(this.Character, text);
        }

        /// <summary>
        /// </summary>
        /// <param name="container">
        /// </param>
        /// <param name="slotNumber">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool DeleteItem(int container, int slotNumber)
        {
            return InventoryContainerRuntimeService.Default.DeletePlayerControllerContainerItem(
                this.Character,
                container,
                slotNumber);
        }

        /// <summary>
        /// </summary>
        /// <param name="targetItem">
        /// </param>
        /// <param name="stackCount">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool SplitItemStack(Identity targetItem, int stackCount)
        {
            // Procedure:
            // 1. Check if Item exists
            // 2. Check if stackCount<item's stack - 1
            // 3. Create new item from old item with stack=stackCount
            // 4. Decrease old item's stack
            // 5. Add new item to inventory

            throw new NotImplementedException();
        }

        /// <summary>
        /// </summary>
        /// <param name="sourceItem">
        /// </param>
        /// <param name="targetItem">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool JoinItemStack(Identity sourceItem, Identity targetItem)
        {
            // Procedure:
            // 1. Check if items are the same itemid's
            // 2. Add sourceItem stack to targetItem
            // 3. Delete sourceItem

            throw new NotImplementedException();
        }

        /// <summary>
        /// </summary>
        /// <param name="sourceItem">
        /// </param>
        /// <param name="targetItem">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool CombineItems(Identity sourceItem, Identity targetItem)
        {
            // Procedure: 
            // See TradeSkillReceiver.TradeSkillBuildPressed

            throw new NotImplementedException();
        }

        /// <summary>
        /// </summary>
        /// <param name="inventoryPageId">
        /// </param>
        /// <param name="slotNumber">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool TradeSkillSourceChanged(int inventoryPageId, int slotNumber)
        {
            // Procedure see TradeSkillReceiver.TradeSkillSourceChanged

            throw new NotImplementedException();
        }

        /// <summary>
        /// </summary>
        /// <param name="inventoryPageId">
        /// </param>
        /// <param name="slotNumber">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool TradeSkillTargetChanged(int inventoryPageId, int slotNumber)
        {
            // Procedure see TradeSkillReceiver.TradeSkillTargetChanged

            throw new NotImplementedException();
        }

        /// <summary>
        /// </summary>
        /// <param name="targetItem">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool TradeSkillBuildPressed(Identity targetItem)
        {
            // Procedure see TradeSkillReceiver.TradeSkillBuildPressed

            throw new NotImplementedException();
        }

        /// <summary>
        /// </summary>
        /// <param name="command">
        /// </param>
        /// <param name="target">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool ChatCommand(string command, Identity target)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool Logout()
        {
            // Procedure: 
            // 1. Sit down (if not already)
            // 2. Check if we are a GM
            // 2.1. Save character and logout immediately
            // 3. Start logout timer
            // 4. Save character
            // 5. Logout

            throw new NotImplementedException();
        }

        public void LogoffCharacter()
        {
            CharacterDao.Instance.SetOffline(this.Character.Identity.Instance);
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool Login()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool StopLogout()
        {
            // Procedure:
            // 1. Stop pending logout timer
            // 2. Go back to previous move mode (dunno if really needed)

            throw new NotImplementedException();
        }

        /// <summary>
        /// </summary>
        /// <param name="target">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool GetTargetInfo(Identity target)
        {
            // Procedure:
            // 1. Gather data
            // 2. Send to client

            throw new NotImplementedException();
        }

        /// <summary>
        /// </summary>
        /// <param name="target">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool TeamInvite(Identity target)
        {
            return TeamRuntime.Invite(this.Character, target);
        }

        /// <summary>
        /// </summary>
        /// <param name="target">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool TeamKickMember(Identity target)
        {
            // Procedure:
            // 1. Kick Team member
            // 2. Send Team update message

            return TeamRuntime.Kick(this.Character, target);
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool TeamLeave()
        {
            // Procedure:
            // 1. Leave the team
            // 2. Send Team update message

            return TeamRuntime.Leave(this.Character);
        }

        /// <summary>
        /// </summary>
        /// <param name="target">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool TransferTeamLeadership(Identity target)
        {
            // Procedure:
            // 1. Transfer Leadership
            // 2. Send Team update message

            ChatTextMessageHandler.Default.Send(this.Character, "Team leadership transfer is not wired yet.");
            return false;
        }

        /// <summary>
        /// </summary>
        /// <param name="target">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool TeamJoinRequest(Identity target)
        {
            return TeamRuntime.Invite(this.Character, target, 0);
        }

        /// <summary>
        /// </summary>
        /// <param name="accept">
        /// </param>
        /// <param name="requester">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool TeamJoinReply(bool accept, Identity requester)
        {
            // Procedure:
            // 1. If accept==true
            // 2.    Call requester's TeamJoinAccepted
            // 3. else
            // 4.    Call requester's TeamJoinRejected

            return TeamRuntime.Reply(this.Character, accept, requester);
        }

        /// <summary>
        /// </summary>
        /// <param name="newTeamMember">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool TeamJoinAccepted(Identity newTeamMember)
        {
            // Procedure:
            // 1. If on team exists yet, create one
            // 2. Add yourself as TeamLeader
            // 3. Add newTeamMember
            // 4. Send out TeamMemberInfo etc. to all team members

            return TeamRuntime.AcceptDirect(this.Character, newTeamMember);
        }

        /// <summary>
        /// </summary>
        /// <param name="rejectingIdentity">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool TeamJoinRejected(Identity rejectingIdentity)
        {
            // Procedure: 
            // 1. Send back negative reply

            return TeamRuntime.RejectDirect(this.Character, rejectingIdentity);
        }

        /// <summary>
        /// </summary>
        /// <param name="client">
        /// </param>
        public void SendChangedStats()
        {
            Dictionary<int, uint> toPlayfield = new Dictionary<int, uint>();
            Dictionary<int, uint> toPlayer = new Dictionary<int, uint>();

            this.Character.Stats.GetChangedStats(toPlayer, toPlayfield);

            CombatXpRuntimeService.RemoveWireManagedStatsFromBulk(toPlayer);
            CombatXpRuntimeService.RemoveWireManagedStatsFromBulk(toPlayfield);

            StatMessageHandler.Default.SendBulk(this.Character, toPlayer, toPlayfield);
        }

        public void SendCombatHealthStatWire(uint wireHealth)
        {
            StatMessageHandler.Default.SendSingle(this.Character, (int)StatIds.health, wireHealth);
            StatMessageHandler.Default.AnnounceSingle(this.Character, (int)StatIds.health, wireHealth);
            this.Character.Stats[(int)StatIds.health].Changed = false;
        }

        #endregion

        ~PlayerController()
        {
            this.Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            LogUtil.Debug(DebugInfoDetail.Memory, "Disposing of PlayerController");

            if (disposing)
            {
                if (!this.disposed)
                {
                    // Only remove the link to client here, client will be disposed on its own
                    this.Client = null;
                }
            }
            this.disposed = true;
        }
    }

    internal static class TeamRuntime
    {
        private static readonly object Sync = new object();

        private static readonly Dictionary<int, Identity> PendingInvites = new Dictionary<int, Identity>();

        /// <summary>inviteeInstance → (inviterInstance, utc ticks) after decline — block re-invite spam.</summary>
        private static readonly Dictionary<int, long> DeclinedInviteUntilUtcTicks = new Dictionary<int, long>();

        private static readonly TimeSpan DeclineCooldown = TimeSpan.FromSeconds(30);

        private static readonly Dictionary<int, List<Identity>> TeamMembers = new Dictionary<int, List<Identity>>();

        private static readonly Dictionary<int, int> CharacterTeams = new Dictionary<int, int>();

        /// <summary>teamId → leader character instance.</summary>
        private static readonly Dictionary<int, int> TeamLeaders = new Dictionary<int, int>();

        private static int nextTeamId = 1;

        public static bool Invite(ICharacter inviter, Identity targetIdentity)
        {
            return Invite(inviter, targetIdentity, 0);
        }

        /// <param name="parameter2">
        /// Live 20260815-194517: first click is TeamRequestInvite p2=0; Yes on TooHigh
        /// is p2=1. Invite the target only after Yes (or when levels are in range).
        /// </param>
        public static bool Invite(ICharacter inviter, Identity targetIdentity, int parameter2)
        {
            ICharacter target = FindInviteTarget(inviter, targetIdentity);

            if (target == null || target.Identity.Equals(inviter.Identity))
            {
                ChatTextMessageHandler.Default.Send(
                    inviter,
                    "Team invite target is not available"
                    + (targetIdentity.Instance != 0 ? " (id " + targetIdentity.Instance + ")." : "."));
                return false;
            }

            int targetLevel = CombatXpRuntimeService.ResolveWireLevel(target);

            // Live TooHigh 20260815-194517: OUT 0x1A p2=0 → IN 0xA9, invite only after p2=1.
            // Live TooLow 20260815-222131: OUT 0x1A p2=0 → IN 0xA8, invite only after p2=1.
            // Every current team member must be in XP range with the invitee.
            if (parameter2 != 1)
            {
                ICharacter conflictMember;
                bool tooHigh;
                if (TryFindTeamXpConflict(inviter, target, targetLevel, out conflictMember, out tooHigh))
                {
                    string conflictName = conflictMember != null && !string.IsNullOrEmpty(conflictMember.Name)
                        ? conflictMember.Name
                        : "the team";
                    if (tooHigh)
                    {
                        CharacterActionMessageHandler.Default.SendTeamInviteAck(inviter, target);
                        ChatTextMessageHandler.Default.Send(
                            inviter,
                            target.Name + " is too high for " + conflictName + ".");
                    }
                    else
                    {
                        CharacterActionMessageHandler.Default.SendTeamInviteTooLow(inviter, target);
                        ChatTextMessageHandler.Default.Send(
                            inviter,
                            target.Name + " is too low for " + conflictName + ".");
                    }

                    return true;
                }
            }

            lock (Sync)
            {
                Identity existingInviter;
                if (PendingInvites.TryGetValue(target.Identity.Instance, out existingInviter)
                    && existingInviter.Instance == inviter.Identity.Instance)
                {
                    // Already waiting on this invite — do not re-send popup (No then re-invite loop).
                    ChatTextMessageHandler.Default.Send(
                        inviter,
                        "Team invite already pending for " + target.Name + ".");
                    return true;
                }

                long untilTicks;
                if (DeclinedInviteUntilUtcTicks.TryGetValue(target.Identity.Instance, out untilTicks)
                    && DateTime.UtcNow.Ticks < untilTicks)
                {
                    ChatTextMessageHandler.Default.Send(
                        inviter,
                        target.Name + " declined recently. Wait a moment before inviting again.");
                    return false;
                }

                PendingInvites[target.Identity.Instance] = inviter.Identity;
            }

            // Seed name/level on inviter before 0x1A (capture 20260815-194517: InfoRequest + LookAt
            // pre-wire level; same-PF Recruit needs it too or client false TooHigh).
            {
                string armedName;
                LftInviteArm.TryGetArmedName(inviter, target.Identity, out armedName);
                LftInviteClientPresence.SeedForInviteLookup(inviter, target, armedName);
            }

            // Popup delivery: CA TeamRequestInvite (0x1A) works same- and cross-PF
            // (log proved remote=True TeamInvite-only left invitee with no popup).
            // Live captures also use TeamInvite for cross-zone name; send both.
            // Accept is gated on Parameter2==1 only — dual wire no longer auto-joins.
            bool remoteInvite = LftInviteClientPresence.IsRemoteFrom(inviter, target);
            if (remoteInvite)
            {
                TeamInviteMessageHandler.Default.Send(target, inviter);
            }

            CharacterActionMessageHandler.Default.SendTeamInviteRequest(target, inviter);

            ChatTextMessageHandler.Default.Send(inviter, "Team invite sent to " + target.Name + ".");
            ChatTextMessageHandler.Default.Send(
                target,
                inviter.Name + " invited you to a team. Click Yes on the invite (or /team accept).");
            // Keep PendingInvites until Accept (live L60: TeamRequestReply 0x15 p2=1).
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "Team invite pending inviter=" + inviter.Identity.ToString(true)
                + " target=" + target.Identity.ToString(true)
                + " remote=" + remoteInvite
                + " inviterPf="
                + (inviter.Playfield != null ? inviter.Playfield.Identity.Instance.ToString() : "?")
                + " targetPf="
                + (target.Playfield != null ? target.Playfield.Identity.Instance.ToString() : "?"));

            return true;
        }

        public static bool Reply(ICharacter character, bool accept, Identity requester)
        {
            Identity inviterIdentity = Identity.None;
            lock (Sync)
            {
                if (!PendingInvites.TryGetValue(character.Identity.Instance, out inviterIdentity))
                {
                    inviterIdentity = requester;
                }
            }

            // AcceptTeamRequest / reply Target may be self; keep pending inviter when present.
            if (inviterIdentity.Equals(Identity.None)
                || inviterIdentity.Instance == character.Identity.Instance)
            {
                lock (Sync)
                {
                    Identity pending;
                    if (PendingInvites.TryGetValue(character.Identity.Instance, out pending))
                    {
                        inviterIdentity = pending;
                    }
                }
            }

            // Cross-zone Accept Target is the inviter identity from the popup.
            if ((inviterIdentity.Equals(Identity.None)
                 || inviterIdentity.Instance == character.Identity.Instance)
                && requester.Instance != 0
                && requester.Instance != character.Identity.Instance)
            {
                inviterIdentity = new Identity
                {
                    Type = IdentityType.CanbeAffected,
                    Instance = requester.Instance
                };
            }

            if (inviterIdentity.Equals(Identity.None)
                || inviterIdentity.Instance == character.Identity.Instance)
            {
                ChatTextMessageHandler.Default.Send(character, "No pending team invite.");
                return false;
            }

            ICharacter inviter = LftInviteClientPresence.ResolveOnlinePlayer(character, inviterIdentity)
                                ?? ResolveOnlineCharacter(character, inviterIdentity)
                                ?? FindOnlineCharacterByInstance(inviterIdentity.Instance);
            if (inviter == null)
            {
                ChatTextMessageHandler.Default.Send(character, "The team inviter is no longer available.");
                lock (Sync)
                {
                    PendingInvites.Remove(character.Identity.Instance);
                }

                return false;
            }

            if (!accept)
            {
                lock (Sync)
                {
                    PendingInvites.Remove(character.Identity.Instance);
                    DeclinedInviteUntilUtcTicks[character.Identity.Instance] =
                        DateTime.UtcNow.Add(DeclineCooldown).Ticks;
                }

                CharacterActionMessageHandler.Default.SendTeamRequestDeclined(inviter, character);
                ChatTextMessageHandler.Default.Send(character, "Team invite declined.");
                ChatTextMessageHandler.Default.Send(inviter, character.Name + " declined your team invite.");
                return true;
            }

            // Already teamed (stale auto-join / double Accept): re-push roster to both clients.
            int characterTeam;
            int inviterTeam;
            lock (Sync)
            {
                CharacterTeams.TryGetValue(character.Identity.Instance, out characterTeam);
                CharacterTeams.TryGetValue(inviter.Identity.Instance, out inviterTeam);
            }

            if (characterTeam != 0 && characterTeam == inviterTeam)
            {
                List<Identity> members;
                lock (Sync)
                {
                    members = TeamMembers.ContainsKey(characterTeam)
                                  ? TeamMembers[characterTeam].ToList()
                                  : new List<Identity>();
                }

                if (members.Count > 0)
                {
                    BroadcastTeamJoined(characterTeam, members, character.Identity);
                }

                lock (Sync)
                {
                    PendingInvites.Remove(character.Identity.Instance);
                }

                ChatTextMessageHandler.Default.Send(character, "Team roster refreshed.");
                return true;
            }

            // Cross-zone join: InfoPacket name only (no SCFU ghosts).
            if (LftInviteClientPresence.IsRemoteFrom(inviter, character))
            {
                LftInviteClientPresence.SeedNameAndLevelOnly(character, inviter, null);
                LftInviteClientPresence.SeedNameAndLevelOnly(inviter, character, null);
            }

            Join(inviter, character);
            lock (Sync)
            {
                PendingInvites.Remove(character.Identity.Instance);
            }

            return true;
        }

        public static bool AcceptDirect(ICharacter leader, Identity newMemberIdentity)
        {
            ICharacter newMember = ResolveOnlineCharacter(leader, newMemberIdentity);
            if (newMember == null)
            {
                ChatTextMessageHandler.Default.Send(leader, "Team member is not available.");
                return false;
            }

            Join(leader, newMember);
            return true;
        }

        public static bool RejectDirect(ICharacter inviter, Identity rejectingIdentity)
        {
            ICharacter rejectingCharacter = ResolveOnlineCharacter(inviter, rejectingIdentity);
            if (rejectingCharacter != null)
            {
                ChatTextMessageHandler.Default.Send(
                    inviter,
                    rejectingCharacter.Name + " declined your team invite.");
            }

            return true;
        }

        /// <summary>
        /// Disconnect / leave-game: drop from team so remaining clients clear the gray slot.
        /// Zone transfers must not call this.
        /// </summary>
        public static void OnCharacterDisconnected(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            lock (Sync)
            {
                if (!CharacterTeams.ContainsKey(character.Identity.Instance))
                {
                    return;
                }
            }

            Leave(character, notifyLeavingCharacter: false);
        }

        public static bool Leave(ICharacter character)
        {
            return Leave(character, notifyLeavingCharacter: true);
        }

        private static bool Leave(ICharacter character, bool notifyLeavingCharacter)
        {
            int teamId;
            List<Identity> remainingMembers;
            bool wasLeader;
            lock (Sync)
            {
                if (!CharacterTeams.TryGetValue(character.Identity.Instance, out teamId))
                {
                    // Repair desync: still listed in a team roster without CharacterTeams entry.
                    foreach (KeyValuePair<int, List<Identity>> entry in TeamMembers)
                    {
                        if (entry.Value != null
                            && entry.Value.Any(x => x.Instance == character.Identity.Instance))
                        {
                            teamId = entry.Key;
                            CharacterTeams[character.Identity.Instance] = teamId;
                            break;
                        }
                    }
                }

                if (!CharacterTeams.TryGetValue(character.Identity.Instance, out teamId))
                {
                    // Client still shows a team window but server has no membership —
                    // always clear the UI so Leave is never a dead end.
                    ForceClearStuckTeamUi(character);
                    if (notifyLeavingCharacter)
                    {
                        ChatTextMessageHandler.Default.Send(character, "Team window cleared.");
                    }

                    return true;
                }

                int leaderInstance;
                wasLeader = TeamLeaders.TryGetValue(teamId, out leaderInstance)
                            && leaderInstance == character.Identity.Instance;

                remainingMembers = TeamMembers[teamId];
                remainingMembers.RemoveAll(x => x.Instance == character.Identity.Instance);
                CharacterTeams.Remove(character.Identity.Instance);
                if (remainingMembers.Count == 0)
                {
                    TeamMembers.Remove(teamId);
                    TeamLeaders.Remove(teamId);
                }
                else if (wasLeader)
                {
                    TeamLeaders[teamId] = remainingMembers[0].Instance;
                }
            }

            // Capture leave: TeamMemberLeft only — never re-broadcast TeamMember roster
            // (that stacked duplicate names in the team window).
            ApplyTeamStats(character, memberCount: 0);
            CharacterActionMessageHandler.Default.SendTeamMemberLeft(
                character,
                character.Identity,
                teamId);
            foreach (Identity remaining in remainingMembers.ToList())
            {
                ICharacter member = FindOnlineCharacterByInstance(remaining.Instance)
                                    ?? Pool.Instance.GetObject<ICharacter>(remaining);
                if (member != null)
                {
                    CharacterActionMessageHandler.Default.SendTeamMemberLeft(
                        member,
                        character.Identity,
                        teamId);
                }
            }

            if (notifyLeavingCharacter)
            {
                ChatTextMessageHandler.Default.Send(character, "You left the team.");
            }

            NotifyMembers(remainingMembers, character.Name + " left the team.");

            // 2-person team: when one leaves, dissolve the last member so the UI clears.
            if (remainingMembers.Count == 1)
            {
                DissolveSoloMember(remainingMembers[0], teamId);
            }
            else if (remainingMembers.Count > 1)
            {
                // Promote leader signals only — do not resend TeamMember list.
                NotifyLeadershipStats(teamId, remainingMembers);
            }

            return true;
        }

        /// <summary>
        /// Client team window stuck after server lost CharacterTeams (zone / Yes-No bug).
        /// </summary>
        private static void ForceClearStuckTeamUi(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            ApplyTeamStats(character, memberCount: 0);
            SendTeamStatSingle(character, StatIds.teamside, 0);
            SendTeamStatSingle(character, StatIds.socialstatus, 4);
            CharacterActionMessageHandler.Default.SendTeamMemberLeft(
                character,
                character.Identity,
                0);
        }

        /// <summary>
        /// Last member after a 2-person leave: clear their team window completely.
        /// </summary>
        private static void DissolveSoloMember(Identity lastMemberIdentity, int teamId)
        {
            lock (Sync)
            {
                CharacterTeams.Remove(lastMemberIdentity.Instance);
                TeamMembers.Remove(teamId);
                TeamLeaders.Remove(teamId);
            }

            ICharacter last = FindOnlineCharacterByInstance(lastMemberIdentity.Instance)
                              ?? Pool.Instance.GetObject<ICharacter>(lastMemberIdentity);
            if (last == null)
            {
                return;
            }

            ApplyTeamStats(last, memberCount: 0);
            SendTeamStatSingle(last, StatIds.teamside, 0);
            SendTeamStatSingle(last, StatIds.socialstatus, 4);
            CharacterActionMessageHandler.Default.SendTeamMemberLeft(
                last,
                last.Identity,
                teamId);
            ChatTextMessageHandler.Default.Send(last, "Your team has been disbanded.");
        }

        /// <summary>
        /// After a leave with 3+ remaining: update SocialStatus / AcceptTeamRequest for new leader
        /// without re-announcing roster (avoids duplicate name rows).
        /// </summary>
        private static void NotifyLeadershipStats(int teamId, List<Identity> members)
        {
            int leaderInstance;
            lock (Sync)
            {
                if (!TeamLeaders.TryGetValue(teamId, out leaderInstance) && members.Count > 0)
                {
                    leaderInstance = members[0].Instance;
                    TeamLeaders[teamId] = leaderInstance;
                }
            }

            foreach (Identity memberIdentity in members)
            {
                ICharacter member = Pool.Instance.GetObject<ICharacter>(memberIdentity);
                if (member == null)
                {
                    continue;
                }

                bool isLeader = member.Identity.Instance == leaderInstance;
                int social = isLeader ? 7 : 5;
                ApplyTeamStats(member, members.Count, socialStatus: social, sendWireSingles: true);
                SendTeamStatSingle(member, StatIds.socialstatus, social);
                if (isLeader)
                {
                    CharacterActionMessageHandler.Default.SendTeamRequestReplyAck(member);
                    CharacterActionMessageHandler.Default.SendAcceptTeamRequest(member, teamId);
                    SendTeamStatSingle(member, StatIds.socialstatus, social);
                }
            }
        }

        /// <summary>
        /// Invitee must be in XP range of every current team member (solo = inviter).
        /// TooHigh uses the lowest conflicting member; TooLow uses the highest.
        /// </summary>
        private static bool TryFindTeamXpConflict(
            ICharacter inviter,
            ICharacter invitee,
            int inviteeLevel,
            out ICharacter conflictMember,
            out bool tooHigh)
        {
            conflictMember = null;
            tooHigh = false;
            if (inviter == null || invitee == null)
            {
                return false;
            }

            List<ICharacter> roster = new List<ICharacter>();
            roster.Add(inviter);

            List<Identity> ids;
            if (TryGetTeamMembers(inviter, out ids) && ids != null)
            {
                for (int i = 0; i < ids.Count; i++)
                {
                    if (ids[i].Instance == inviter.Identity.Instance)
                    {
                        continue;
                    }

                    ICharacter member = ResolveOnlineCharacter(inviter, ids[i])
                                        ?? FindOnlineCharacterByInstance(ids[i].Instance);
                    if (member == null || member.Identity.Instance == invitee.Identity.Instance)
                    {
                        continue;
                    }

                    roster.Add(member);
                }
            }

            ICharacter tooHighMember = null;
            int tooHighMemberLevel = int.MaxValue;
            ICharacter tooLowMember = null;
            int tooLowMemberLevel = int.MinValue;

            for (int i = 0; i < roster.Count; i++)
            {
                ICharacter member = roster[i];
                int memberLevel = CombatXpRuntimeService.ResolveWireLevel(member);
                if (TeamXpShareWindow.IsTooHighForXpShare(memberLevel, inviteeLevel))
                {
                    if (memberLevel < tooHighMemberLevel)
                    {
                        tooHighMember = member;
                        tooHighMemberLevel = memberLevel;
                    }
                }
                else if (TeamXpShareWindow.IsTooLowForXpShare(memberLevel, inviteeLevel))
                {
                    if (memberLevel > tooLowMemberLevel)
                    {
                        tooLowMember = member;
                        tooLowMemberLevel = memberLevel;
                    }
                }
            }

            if (tooHighMember != null)
            {
                conflictMember = tooHighMember;
                tooHigh = true;
                return true;
            }

            if (tooLowMember != null)
            {
                conflictMember = tooLowMember;
                tooHigh = false;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns current team roster (includes self) when character is on a team.
        /// </summary>
        public static bool TryGetTeamMembers(ICharacter character, out List<Identity> members)
        {
            members = null;
            if (character == null)
            {
                return false;
            }

            lock (Sync)
            {
                int teamId;
                if (!CharacterTeams.TryGetValue(character.Identity.Instance, out teamId))
                {
                    return false;
                }

                List<Identity> roster;
                if (!TeamMembers.TryGetValue(teamId, out roster) || roster == null || roster.Count == 0)
                {
                    return false;
                }

                members = roster.ToList();
                return true;
            }
        }

        public static bool Kick(ICharacter leader, Identity targetIdentity)
        {
            ICharacter target = ResolveOnlineCharacter(leader, targetIdentity);
            if (target == null)
            {
                ChatTextMessageHandler.Default.Send(leader, "Team kick target is not available.");
                return false;
            }

            int teamId;
            lock (Sync)
            {
                if (!CharacterTeams.TryGetValue(leader.Identity.Instance, out teamId)
                    || !CharacterTeams.ContainsKey(target.Identity.Instance)
                    || CharacterTeams[target.Identity.Instance] != teamId)
                {
                    ChatTextMessageHandler.Default.Send(leader, target.Name + " is not in your team.");
                    return false;
                }

                int leaderInstance;
                if (!TeamLeaders.TryGetValue(teamId, out leaderInstance)
                    || leaderInstance != leader.Identity.Instance)
                {
                    ChatTextMessageHandler.Default.Send(leader, "Only the team leader can kick.");
                    return false;
                }
            }

            Leave(target);
            ChatTextMessageHandler.Default.Send(leader, target.Name + " was removed from the team.");
            return true;
        }

        public static bool TryHandleChatCommand(ICharacter character, string[] args)
        {
            if (args == null || args.Length < 2)
            {
                ChatTextMessageHandler.Default.Send(
                    character,
                    "Team commands: /team invite <name>, /team accept, /team decline, /team leave.");
                return true;
            }

            string action = args[1].ToLowerInvariant();
            if (action == "accept")
            {
                return Reply(character, true, Identity.None);
            }

            if ((action == "decline") || (action == "reject"))
            {
                return Reply(character, false, Identity.None);
            }

            if (action == "leave")
            {
                return Leave(character);
            }

            if (action == "invite" && args.Length >= 3)
            {
                ICharacter target = FindOnlineCharacterByName(character, args[2]);
                if (target == null)
                {
                    ChatTextMessageHandler.Default.Send(character, "Could not find online character " + args[2] + ".");
                    return false;
                }

                return Invite(character, target.Identity);
            }

            ChatTextMessageHandler.Default.Send(character, "Unknown team command.");
            return false;
        }

        private static void Join(ICharacter leader, ICharacter newMember)
        {
            // Invitee must not stay on a leftover solo/ghost team from a prior leave bug.
            int existingMemberTeam;
            lock (Sync)
            {
                if (CharacterTeams.TryGetValue(newMember.Identity.Instance, out existingMemberTeam))
                {
                    int leaderTeam;
                    if (!CharacterTeams.TryGetValue(leader.Identity.Instance, out leaderTeam)
                        || existingMemberTeam != leaderTeam)
                    {
                        // Leave without holding Sync (Leave takes Sync itself).
                    }
                    else
                    {
                        existingMemberTeam = 0;
                    }
                }
                else
                {
                    existingMemberTeam = 0;
                }
            }

            if (existingMemberTeam != 0)
            {
                Leave(newMember, notifyLeavingCharacter: false);
            }

            int teamId;
            List<Identity> members;
            lock (Sync)
            {
                if (!CharacterTeams.TryGetValue(leader.Identity.Instance, out teamId))
                {
                    // Capture team ids are large TeamWindow instances (e.g. 0x0280F02A).
                    teamId = unchecked((int)(0x02800000 + (uint)nextTeamId));
                    nextTeamId++;
                    CharacterTeams[leader.Identity.Instance] = teamId;
                    TeamMembers[teamId] = new List<Identity> { leader.Identity };
                    TeamLeaders[teamId] = leader.Identity.Instance;
                }

                members = TeamMembers[teamId];
                if (!members.Any(x => x.Instance == newMember.Identity.Instance))
                {
                    members.Add(newMember.Identity);
                }

                CharacterTeams[newMember.Identity.Instance] = teamId;
                // Character is no longer looking for a team.
                Program.ISComClient.Send(
     new AORebirth.Communication.Messages.ChatCommand
     {
         CharacterId = newMember.Identity.Instance,
         ChatCommandString = "#aorebirth-lft-remove"
     });

            }

            BroadcastTeamJoined(teamId, members.ToList(), newMember.Identity);
            ChatTextMessageHandler.Default.Send(
                leader,
                newMember.Name + " has joined your team.");
            ChatTextMessageHandler.Default.Send(
                newMember,
                "You joined " + leader.Name + "'s team.");
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "Team joined teamId=" + teamId
                + " leader=" + leader.Identity.ToString(true)
                + " member=" + newMember.Identity.ToString(true));
        }

        private static void BroadcastTeamJoined(
            int teamId,
            List<Identity> members,
            Identity newMemberIdentity)
        {
            var teamIdentity = new Identity
            {
                Type = IdentityType.TeamWindow,
                Instance = teamId
            };

            int leaderInstance;
            lock (Sync)
            {
                if (!TeamLeaders.TryGetValue(teamId, out leaderInstance) && members.Count > 0)
                {
                    leaderInstance = members[0].Instance;
                    TeamLeaders[teamId] = leaderInstance;
                }
            }

            foreach (Identity viewerIdentity in members)
            {
                // Cross-PF: Pool.GetObject(identity) alone misses the other playfield.
                ICharacter viewer = FindOnlineCharacterByInstance(viewerIdentity.Instance)
                                   ?? LftInviteClientPresence.ResolveOnlinePlayer(null, viewerIdentity)
                                   ?? Pool.Instance.GetObject<ICharacter>(viewerIdentity);
                if (viewer == null)
                {
                    continue;
                }

                bool isLeader = viewer.Identity.Instance == leaderInstance;
                // Capture 20260728-234012 / 20260729-003944 LFT accept:
                //   Member: TeamSide=2 + SocialStatus=5 + TeamMember(self/others)
                //   Leader: SocialStatus=7 + 0x15 ack + AcceptTeamRequest + TeamMembers
                int social = isLeader ? 7 : 5;
                ApplyTeamStats(viewer, members.Count, teamSide: 2, socialStatus: social, sendWireSingles: true);
                SendTeamStatSingle(viewer, StatIds.teamside, 2);
                SendTeamStatSingle(viewer, StatIds.socialstatus, social);

                if (isLeader)
                {
                    CharacterActionMessageHandler.Default.SendTeamRequestReplyAck(viewer);
                    SendTeamStatSingle(viewer, StatIds.socialstatus, social);
                }

                SendTeamMemberAnnounce(viewer, viewer, teamIdentity);

                if (isLeader)
                {
                    CharacterActionMessageHandler.Default.SendAcceptTeamRequest(viewer, teamId);
                    SendTeamStatSingle(viewer, StatIds.socialstatus, social);
                }

                foreach (Identity memberIdentity in members)
                {
                    if (memberIdentity.Instance == viewer.Identity.Instance)
                    {
                        continue;
                    }

                    ICharacter member = FindOnlineCharacterByInstance(memberIdentity.Instance)
                                       ?? ResolveOnlineCharacter(viewer, memberIdentity)
                                       ?? Pool.Instance.GetObject<ICharacter>(memberIdentity);
                    if (member == null)
                    {
                        continue;
                    }

                    SendTeamMemberAnnounce(viewer, member, teamIdentity);

                    int life = member.Stats[StatIds.life].Value;
                    int nano = member.Stats[StatIds.maxnanoenergy].Value;
                    if (life <= 0)
                    {
                        life = member.Stats[StatIds.health].Value;
                    }

                    if (life <= 0)
                    {
                        life = 1;
                    }

                    if (nano <= 0)
                    {
                        nano = member.Stats[StatIds.currentnano].Value;
                    }

                    if (nano <= 0)
                    {
                        nano = 469;
                    }

                    TeamMemberInfoMessageHandler.Default.Send(viewer, member.Identity, life, nano);
                    SendTeamStatSingle(viewer, StatIds.socialstatus, social);
                }
            }
        }

        private static void SendTeamMemberAnnounce(ICharacter viewer, ICharacter member, Identity teamIdentity)
        {
            // Client XP "too high" dialog compares teammate Level from this packet.
            // Clamp garbage / unset stats so two low-level chars never look like 60+.
            int level = 1;
            try
            {
                level = member.Stats[StatIds.level].Value;
            }
            catch
            {
                level = 1;
            }

            if (level < 1 || level > 220)
            {
                level = 1;
            }

            short unknown5 = 3;
            try
            {
                int profession = member.Stats[StatIds.profession].Value;
                if (profession != 0)
                {
                    unknown5 = (short)profession;
                }
            }
            catch
            {
                unknown5 = 3;
            }

            TeamMemberMessageHandler.Default.Send(
                viewer,
                member.Identity,
                teamIdentity,
                member.Name,
                level,
                unknown5);
        }

        private static void UpdateTeamMemberStats(int teamId)
        {
            List<Identity> members;
            int leaderInstance = 0;
            lock (Sync)
            {
                if (!TeamMembers.TryGetValue(teamId, out members))
                {
                    return;
                }

                members = members.ToList();
                TeamLeaders.TryGetValue(teamId, out leaderInstance);
            }

            int memberCount = members.Count;
            foreach (Identity memberIdentity in members)
            {
                ICharacter member = Pool.Instance.GetObject<ICharacter>(memberIdentity)
                                   ?? FindOnlineCharacterByInstance(memberIdentity.Instance);
                if (member == null)
                {
                    continue;
                }

                bool isLeader = member.Identity.Instance == leaderInstance;
                ApplyTeamStats(member, memberCount, socialStatus: isLeader ? 7 : 5);
            }
        }

        private static void ApplyTeamStats(
            ICharacter character,
            int memberCount,
            int? teamSide = null,
            int? socialStatus = null,
            bool sendWireSingles = false)
        {
            bool inTeam = memberCount > 0;
            int resolvedTeamSide = teamSide ?? (inTeam ? 2 : 0);
            // Capture: members SocialStatus=5, leader SocialStatus=7; leave → 4.
            int social = socialStatus ?? (inTeam ? 5 : 4);
            // Do not put TeamWindow id into StatIds.team, and do not send stat 6 on join.
            character.Stats[StatIds.numberofteammembers].Value = memberCount;
            character.Stats[StatIds.numberofteammembers].BaseValue = (uint)memberCount;
            character.Stats[StatIds.teamside].Value = resolvedTeamSide;
            character.Stats[StatIds.teamside].BaseValue = (uint)resolvedTeamSide;
            character.Stats[StatIds.socialstatus].Value = social;
            character.Stats[StatIds.socialstatus].BaseValue = (uint)social;
            if (sendWireSingles)
            {
                SendTeamStatSingle(character, StatIds.socialstatus, social);
            }
            else
            {
                character.Controller.SendChangedStats();
            }
        }

        private static void SendTeamStatSingle(ICharacter character, StatIds statId, int value)
        {
            character.Stats[statId].Value = value;
            character.Stats[statId].BaseValue = (uint)value;
            StatMessageHandler.Default.SendSingle(character, (int)statId, (uint)value);
        }

        private static void NotifyMembers(List<Identity> members, string text)
        {
            if (members == null)
            {
                return;
            }

            foreach (Identity identity in members.ToList())
            {
                ICharacter member = Pool.Instance.GetObject<ICharacter>(identity);
                if (member != null)
                {
                    ChatTextMessageHandler.Default.Send(member, text);
                }
            }
        }

        private static ICharacter FindInviteTarget(ICharacter inviter, Identity targetIdentity)
        {
            if (targetIdentity.Instance == 0)
            {
                return null;
            }

            ICharacter target = FindOnlineCharacterByInstance(targetIdentity.Instance);
            if (target != null)
            {
                return target;
            }

            var typed = new Identity
            {
                Type = IdentityType.CanbeAffected,
                Instance = targetIdentity.Instance
            };

            // Same lookup ZoneServer uses for chat commands (cross-playfield).
            target = Pool.Instance.GetObject<ICharacter>(typed);
            if (target != null)
            {
                return target;
            }

            return ResolveOnlineCharacter(inviter, typed);
        }

        private static ICharacter ResolveOnlineCharacter(ICharacter reference, Identity identity)
        {
            if (reference == null || identity.Instance == 0)
            {
                return null;
            }

            if (reference.Playfield != null)
            {
                ICharacter samePf = Pool.Instance.GetObject<ICharacter>(
                    reference.Playfield.Identity,
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = identity.Instance
                    });
                if (samePf != null)
                {
                    return samePf;
                }
            }

            return FindOnlineCharacterByInstance(identity.Instance);
        }

        private static ICharacter FindOnlineCharacterByInstance(int instance)
        {
            if (instance == 0)
            {
                return null;
            }

            uint want = unchecked((uint)instance);

            // All playfields, any player with a client — required for LFT cross-zone invites.
            foreach (ICharacter candidate in Pool.Instance.GetAll<ICharacter>((int)IdentityType.CanbeAffected))
            {
                if (candidate == null || candidate.Identity.Instance == 0)
                {
                    continue;
                }

                if (unchecked((uint)candidate.Identity.Instance) != want)
                {
                    continue;
                }

                if (candidate.Controller is PlayerController)
                {
                    return candidate;
                }

                if (candidate.Controller != null && candidate.Controller.Client != null)
                {
                    return candidate;
                }
            }

            return Pool.Instance.GetObject<ICharacter>(
                new Identity { Type = IdentityType.CanbeAffected, Instance = instance });
        }

        private static ICharacter FindOnlineCharacterByName(ICharacter reference, string name)
        {
            if (reference == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return Pool.Instance.GetAll<ICharacter>((int)IdentityType.CanbeAffected)
                .FirstOrDefault(
                    x => x != null
                         && x.Controller is PlayerController
                         && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
