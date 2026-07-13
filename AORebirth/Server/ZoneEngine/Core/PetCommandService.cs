#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;
    using AORebirth.Core.Nanos;
    using AORebirth.Core.Playfields;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;

    using Utility;

    #endregion

    /// <summary>
    /// Capture-backed MP pet commands from 20260710-220653.
    /// PetCommand.Unknown2 holds the command id; Unknown1=1 means all owned pets.
    /// </summary>
    internal static class PetCommandService
    {
        private static readonly Dictionary<int, PetHealCommandState> ActiveHealCommands =
            new Dictionary<int, PetHealCommandState>();

        private static readonly Dictionary<int, Identity> OwnerHealFocusSelection =
            new Dictionary<int, Identity>();

        public const int CommandFollow = 1;

        public const int CommandBehind = 2;

        public const int CommandWait = 4;

        public const int CommandGuard = 6;

        public const int CommandAttack = 7;

        public const int CommandTerminate = 10;

        public const int CommandHeal = 12;

        public const int CommandReport = 14;

        public static void HandleChatPetCommand(IZoneClient client, string[] cmdArgs)
        {
            ICharacter owner = client != null && client.Controller != null
                ? client.Controller.Character
                : null;
            if (owner == null || cmdArgs == null || cmdArgs.Length < 2)
            {
                return;
            }

            int commandIndex = 1;
            if (cmdArgs.Length >= 3 && cmdArgs[1].StartsWith("\"", StringComparison.Ordinal))
            {
                commandIndex = 2;
                while (commandIndex < cmdArgs.Length && !cmdArgs[commandIndex].EndsWith("\"", StringComparison.Ordinal))
                {
                    commandIndex++;
                }

                commandIndex++;
            }

            if (commandIndex >= cmdArgs.Length)
            {
                return;
            }

            int commandId;
            if (!TryResolveCommandId(cmdArgs[commandIndex], out commandId))
            {
                return;
            }

            ExecuteForAllOwnedPets(owner, client, commandId, Identity.None);
        }

        public static void HandlePetCommandMessage(
            IZoneClient client,
            ICharacter owner,
            int commandId,
            bool applyToAllPets,
            Identity petIdentity,
            Identity commandTarget)
        {
            if (owner == null || owner.Playfield == null || commandId <= 0)
            {
                return;
            }

            if (applyToAllPets || petIdentity.Instance == 0)
            {
                ExecuteForAllOwnedPets(owner, client, commandId, commandTarget);
                return;
            }

            ICharacter pet = ResolveOwnedPet(owner, petIdentity);
            if (pet == null)
            {
                return;
            }

            ExecuteForPet(owner, client, pet, commandId, commandTarget);
        }

        private static void ExecuteForAllOwnedPets(
            ICharacter owner,
            IZoneClient client,
            int commandId,
            Identity commandTarget)
        {
            foreach (int strain in PetRuntimeService.Default.GetActivePetStrains(owner))
            {
                ICharacter pet = PetRuntimeService.Default.GetActivePetInStrain(owner, strain);
                if (pet != null)
                {
                    ExecuteForPet(owner, client, pet, commandId, commandTarget);
                }
            }
        }

        private static void ExecuteForPet(
            ICharacter owner,
            IZoneClient client,
            ICharacter pet,
            int commandId,
            Identity commandTarget)
        {
            LogUtil.Debug(
                DebugInfoDetail.GameFunctions,
                string.Format(
                    "PetCommandExecute owner={0} pet={1} commandId={2} target={3}",
                    owner.Identity,
                    pet.Identity,
                    commandId,
                    commandTarget));

            if (client != null)
            {
                client.Server.Info(
                    client,
                    "PetCommand pet={0} commandId={1} target={2}",
                    pet.Identity,
                    commandId,
                    commandTarget);
            }

            var petController = pet.Controller as NPCController;
            if (petController == null)
            {
                return;
            }

            Playfield playfield = owner.Playfield as Playfield;
            if (playfield == null)
            {
                return;
            }

            switch (commandId)
            {
                case CommandFollow:
                case CommandGuard:
                    ActiveHealCommands.Remove(pet.Identity.Instance);
                    petController.Follow(owner.Identity, 2.0);
                    return;

                case CommandBehind:
                    ActiveHealCommands.Remove(pet.Identity.Instance);
                    petController.Follow(owner.Identity, 4.0);
                    return;

                case CommandWait:
                    ExecuteWait(owner, pet, petController, playfield);
                    return;

                case CommandAttack:
                    ActiveHealCommands.Remove(pet.Identity.Instance);
                    if (!PetCombatRules.IsPlayerOwnedMeleeCombatPet(pet))
                    {
                        return;
                    }

                    ExecuteAttack(owner, pet, petController, playfield, commandTarget);
                    return;

                case CommandHeal:
                    ExecuteHeal(owner, pet, petController, playfield, commandTarget);
                    return;

                case CommandReport:
                    ChatTextMessageHandler.Default.Send(
                        owner,
                        string.Format(
                            "{0}: HP {1}/{2}",
                            pet.Name,
                            pet.Stats[StatIds.health].Value,
                            pet.Stats[StatIds.life].Value));
                    return;

                case CommandTerminate:
                    ActiveHealCommands.Remove(pet.Identity.Instance);
                    PetRuntimeService.Default.TerminatePetByIdentity(owner, pet.Identity);
                    return;
            }
        }

        private static void ExecuteAttack(
            ICharacter owner,
            ICharacter pet,
            NPCController petController,
            Playfield playfield,
            Identity commandTarget)
        {
            Identity attackTarget = commandTarget;
            if (attackTarget.Instance == 0)
            {
                attackTarget = owner.SelectedTarget;
            }

            if (attackTarget.Instance == 0)
            {
                attackTarget = owner.FightingTarget;
            }

            if (attackTarget.Instance == 0 || attackTarget.Instance == pet.Identity.Instance)
            {
                return;
            }

            ICharacter attackTargetCharacter = owner.Playfield.FindByIdentity<ICharacter>(attackTarget);
            if (attackTargetCharacter == null)
            {
                return;
            }

            petController.StopFollow();
            pet.SetTarget(attackTarget);
            pet.SetFightingTarget(attackTarget);
            playfield.SuspendNpcRegen(attackTargetCharacter);
            playfield.ResetCombatTick(pet.Identity);
            playfield.AcquireNpcAggro(pet, attackTargetCharacter);
        }

        private static void ExecuteWait(
            ICharacter owner,
            ICharacter pet,
            NPCController petController,
            Playfield playfield)
        {
            ActiveHealCommands.Remove(pet.Identity.Instance);
            pet.SetFightingTarget(Identity.None);
            pet.SetTarget(Identity.None);
            playfield.Announce(
                new StopFightMessage
                {
                    Identity = pet.Identity,
                    Unknown1 = 1
                });
            playfield.ClearCombatTracking(pet.Identity);
            petController.StopFollow();
            FollowTargetMessageHandler.Default.Send(pet, pet.RawCoordinates);
        }

        internal static void ReturnPetToOwner(ICharacter pet)
        {
            if (pet == null || !PetCombatRules.IsPlayerOwnedPet(pet))
            {
                return;
            }

            ICharacter owner = PetCombatRules.ResolvePetOwner(pet);
            if (owner == null)
            {
                return;
            }

            var petController = pet.Controller as NPCController;
            if (petController == null)
            {
                return;
            }

            pet.SetFightingTarget(Identity.None);
            pet.SetTarget(Identity.None);
            petController.Follow(owner.Identity, 2.0);
        }

        internal static bool OnOwnerLookAtTarget(ICharacter owner, Identity lookTarget)
        {
            if (owner == null || lookTarget.Instance == 0)
            {
                return false;
            }

            Playfield playfield = owner.Playfield as Playfield;
            if (playfield == null)
            {
                return false;
            }

            Identity friendly = ResolveFriendlyHealTargetByInstance(owner, lookTarget, playfield);
            if (friendly.Instance == 0)
            {
                return false;
            }

            SetOwnerHealFocusSelection(owner, friendly);
            owner.SetTarget(friendly);

            if (HasActiveHealCommand(owner))
            {
                ApplyHealFocusToActivePets(owner, playfield, friendly, true);
            }

            return true;
        }

        internal static ICharacter ResolveOwnedPet(ICharacter owner, Identity petIdentity)
        {
            if (owner == null || petIdentity.Instance == 0)
            {
                return null;
            }

            foreach (int strain in PetRuntimeService.Default.GetActivePetStrains(owner))
            {
                ICharacter ownedPet = PetRuntimeService.Default.GetActivePetInStrain(owner, strain);
                if (ownedPet != null && ownedPet.Identity.Instance == petIdentity.Instance)
                {
                    return ownedPet;
                }
            }

            if (owner.Playfield == null)
            {
                return null;
            }

            ICharacter byIdentity = owner.Playfield.FindByIdentity<ICharacter>(petIdentity);
            if (byIdentity != null && byIdentity.Stats[StatIds.petmaster].Value == owner.Identity.Instance)
            {
                return byIdentity;
            }

            Playfield playfield = owner.Playfield as Playfield;
            if (playfield == null)
            {
                return null;
            }

            return FindCharacterByInstance(playfield, petIdentity.Instance, petIdentity);
        }

        internal static Identity ResolveHealCommandTarget(
            ICharacter owner,
            Identity healPetIdentity,
            Identity packetTarget)
        {
            if (owner == null || owner.Playfield == null)
            {
                return Identity.None;
            }

            Playfield playfield = owner.Playfield as Playfield;
            if (playfield == null)
            {
                return Identity.None;
            }

            // Capture 20260711-022256: heal owner sends Identities[1]=heal pet, not owner.
            bool healPetSentinel = packetTarget.Instance != 0
                && healPetIdentity.Instance != 0
                && packetTarget.Instance == healPetIdentity.Instance;
            if (healPetSentinel)
            {
                packetTarget = Identity.None;
            }

            if (packetTarget.Instance != 0)
            {
                Identity normalizedPacketTarget = ResolveFriendlyHealTargetByInstance(owner, packetTarget, playfield);
                if (normalizedPacketTarget.Instance != 0)
                {
                    SetOwnerHealFocusSelection(owner, normalizedPacketTarget);
                    return normalizedPacketTarget;
                }
            }

            if (healPetSentinel)
            {
                Identity storedSentinelSelection = GetOwnerHealFocusSelection(owner, playfield);
                if (storedSentinelSelection.Instance != 0)
                {
                    return storedSentinelSelection;
                }
            }
            else
            {
                Identity storedSelection = GetOwnerHealFocusSelection(owner, playfield);
                if (storedSelection.Instance != 0)
                {
                    return storedSelection;
                }
            }

            SetOwnerHealFocusSelection(owner, owner.Identity);
            return owner.Identity;
        }

        private static Identity GetOwnerHealFocusSelection(ICharacter owner, Playfield playfield)
        {
            if (owner == null || playfield == null)
            {
                return Identity.None;
            }

            Identity storedSelection;
            if (!OwnerHealFocusSelection.TryGetValue(owner.Identity.Instance, out storedSelection)
                || storedSelection.Instance == 0)
            {
                return Identity.None;
            }

            return ResolveFriendlyHealTargetByInstance(owner, storedSelection, playfield);
        }

        private static Identity ResolveFriendlyHealTargetByInstance(
            ICharacter owner,
            Identity target,
            Playfield playfield)
        {
            if (owner == null || playfield == null || target.Instance == 0)
            {
                return Identity.None;
            }

            if (target.Instance == owner.Identity.Instance)
            {
                return owner.Identity;
            }

            foreach (int strain in PetRuntimeService.Default.GetActivePetStrains(owner))
            {
                ICharacter ownedPet = PetRuntimeService.Default.GetActivePetInStrain(owner, strain);
                if (ownedPet == null
                    || ownedPet.Identity.Instance != target.Instance
                    || ownedPet.Stats[StatIds.health].Value <= 0)
                {
                    continue;
                }

                return ownedPet.Identity;
            }

            return NormalizeFriendlyHealIdentity(owner, target, playfield);
        }

        internal static void CommitHealTargetFromPacket(
            ICharacter owner,
            Identity healPetIdentity,
            Identity packetTarget)
        {
            if (owner == null
                || owner.Playfield == null
                || packetTarget.Instance == 0
                || (healPetIdentity.Instance != 0 && packetTarget.Instance == healPetIdentity.Instance))
            {
                return;
            }

            ResolveFriendlyHealTargetForSelection(owner, packetTarget);
        }

        internal static Identity ResolveFriendlyHealTargetForSelection(ICharacter owner, Identity target)
        {
            if (owner == null || owner.Playfield == null || target.Instance == 0)
            {
                return Identity.None;
            }

            Playfield playfield = owner.Playfield as Playfield;
            if (playfield == null)
            {
                return Identity.None;
            }

            Identity friendly = ResolveFriendlyHealTargetByInstance(owner, target, playfield);
            if (friendly.Instance != 0)
            {
                SetOwnerHealFocusSelection(owner, friendly);
            }

            return friendly;
        }

        private static void SetOwnerHealFocusSelection(ICharacter owner, Identity focus)
        {
            if (owner == null || focus.Instance == 0)
            {
                return;
            }

            OwnerHealFocusSelection[owner.Identity.Instance] = focus;
        }

        internal static bool HasActiveHealCommand(ICharacter owner)
        {
            if (owner == null || owner.Playfield == null)
            {
                return false;
            }

            Playfield playfield = owner.Playfield as Playfield;
            if (playfield == null)
            {
                return false;
            }

            foreach (int petInstance in ActiveHealCommands.Keys)
            {
                ICharacter pet = FindCharacterByInstance(playfield, petInstance, Identity.None);
                if (pet == null)
                {
                    continue;
                }

                ICharacter healOwner = PetCombatRules.ResolvePetOwner(pet);
                if (healOwner != null && healOwner.Identity.Instance == owner.Identity.Instance)
                {
                    return true;
                }
            }

            return false;
        }

        internal static void ProcessPetHealTick(ICharacter pet)
        {
            if (!PetCombatRules.IsPlayerOwnedHealingPet(pet) || pet.Playfield == null)
            {
                return;
            }

            PetHealCommandState healState;
            if (!ActiveHealCommands.TryGetValue(pet.Identity.Instance, out healState))
            {
                return;
            }

            ICharacter owner = PetCombatRules.ResolvePetOwner(pet);
            if (owner == null)
            {
                ActiveHealCommands.Remove(pet.Identity.Instance);
                return;
            }

            Playfield playfield = owner.Playfield as Playfield;
            if (playfield == null)
            {
                return;
            }

            if (DateTime.UtcNow < healState.NextCastUtc)
            {
                return;
            }

            Identity markedTarget = GetOwnerHealFocusSelection(owner, playfield);
            if (markedTarget.Instance != 0 && markedTarget.Instance != healState.FocusTarget.Instance)
            {
                healState.FocusTarget = markedTarget;
                healState.NextCastUtc = DateTime.UtcNow;
            }

            var petController = pet.Controller as NPCController;
            if (petController != null && healState.FocusTarget.Instance != 0)
            {
                SyncHealPetFollow(pet, petController, healState.FocusTarget);
            }

            ProcessHealCycle(owner, pet, playfield, ref healState);
            ActiveHealCommands[pet.Identity.Instance] = healState;
        }

        internal static void SyncOwnerHealSelectedTarget(ICharacter owner, Identity commandTarget)
        {
            if (owner == null || owner.Playfield == null)
            {
                return;
            }

            Playfield playfield = owner.Playfield as Playfield;
            if (playfield == null)
            {
                return;
            }

            Identity focus = ResolveHealCommandTarget(owner, playfield, commandTarget);
            if (focus.Instance != 0)
            {
                owner.SetTarget(focus);
                ApplyHealFocusToActivePets(owner, playfield, focus, false);
            }
        }

        private static Identity ResolveHealCommandTarget(
            ICharacter owner,
            Playfield playfield,
            Identity commandTarget)
        {
            ICharacter healPet = null;
            foreach (int petInstance in ActiveHealCommands.Keys)
            {
                ICharacter candidate = FindCharacterByInstance(playfield, petInstance, Identity.None);
                if (candidate == null || !PetCombatRules.IsPlayerOwnedHealingPet(candidate))
                {
                    continue;
                }

                ICharacter healOwner = PetCombatRules.ResolvePetOwner(candidate);
                if (healOwner != null && healOwner.Identity.Instance == owner.Identity.Instance)
                {
                    healPet = candidate;
                    break;
                }
            }

            Identity healPetIdentity = healPet != null ? healPet.Identity : Identity.None;
            return ResolveHealCommandTarget(owner, healPetIdentity, commandTarget);
        }

        private static void ApplyHealFocusToActivePets(
            ICharacter owner,
            Playfield playfield,
            Identity focus,
            bool triggerImmediateHeal)
        {
            var activePetInstances = new List<int>(ActiveHealCommands.Keys);
            foreach (int petInstance in activePetInstances)
            {
                PetHealCommandState healState;
                if (!ActiveHealCommands.TryGetValue(petInstance, out healState))
                {
                    continue;
                }

                ICharacter pet = FindCharacterByInstance(playfield, petInstance, Identity.None);
                if (pet == null || !PetCombatRules.IsPlayerOwnedHealingPet(pet))
                {
                    continue;
                }

                ICharacter healOwner = PetCombatRules.ResolvePetOwner(pet);
                if (healOwner == null || healOwner.Identity.Instance != owner.Identity.Instance)
                {
                    continue;
                }

                healState.FocusTarget = focus;
                healState.NextCastUtc = DateTime.UtcNow;
                ActiveHealCommands[petInstance] = healState;

                var petController = pet.Controller as NPCController;
                if (petController != null)
                {
                    SyncHealPetFollow(pet, petController, focus);
                }

                if (triggerImmediateHeal)
                {
                    ProcessHealCycle(owner, pet, playfield, ref healState);
                    ActiveHealCommands[petInstance] = healState;
                }
            }
        }

        private static void SyncHealPetFollow(ICharacter healPet, NPCController petController, Identity focus)
        {
            if (healPet == null || petController == null || focus.Instance == 0)
            {
                return;
            }

            petController.Follow(focus, 2.0);
        }

        internal static void OnOwnerSelectedTargetChanged(ICharacter owner)
        {
            if (owner == null || owner.Playfield == null)
            {
                return;
            }

            Playfield playfield = owner.Playfield as Playfield;
            if (playfield == null)
            {
                return;
            }

            Identity normalizedTarget = ResolveFriendlyHealTargetByInstance(owner, owner.SelectedTarget, playfield);
            if (normalizedTarget.Instance != 0)
            {
                SetOwnerHealFocusSelection(owner, normalizedTarget);
                owner.SetTarget(normalizedTarget);
                ApplyHealFocusToActivePets(owner, playfield, normalizedTarget, true);
            }
        }

        private static Identity NormalizeFriendlyHealIdentity(
            ICharacter owner,
            Identity target,
            Playfield playfield)
        {
            if (owner == null || playfield == null || target.Instance == 0)
            {
                return Identity.None;
            }

            if (target.Instance == owner.Identity.Instance)
            {
                return owner.Identity;
            }

            ICharacter targetCharacter = FindCharacterByInstance(playfield, target.Instance, target);
            if (targetCharacter == null
                || targetCharacter.Stats[StatIds.health].Value <= 0
                || !PetCombatRules.IsPlayerOwnedPet(targetCharacter)
                || targetCharacter.Stats[StatIds.petmaster].Value != owner.Identity.Instance)
            {
                return Identity.None;
            }

            return targetCharacter.Identity;
        }

        private static ICharacter FindCharacterByInstance(Playfield playfield, int instance, Identity hint)
        {
            if (playfield == null || instance == 0)
            {
                return null;
            }

            if (hint.Instance == instance && hint.Type != IdentityType.None)
            {
                ICharacter byHint = playfield.FindByIdentity<ICharacter>(hint);
                if (byHint != null)
                {
                    return byHint;
                }
            }

            ICharacter byCanBeAffected = playfield.FindByIdentity<ICharacter>(
                new Identity
                {
                    Type = IdentityType.CanbeAffected,
                    Instance = instance
                });
            if (byCanBeAffected != null)
            {
                return byCanBeAffected;
            }

            return ResolveCharacterFromPool(playfield, instance, hint);
        }

        private static ICharacter ResolveCharacterFromPool(Playfield playfield, int instance, Identity hint)
        {
            if (playfield == null || instance == 0)
            {
                return null;
            }

            Identity parent = playfield.Identity;

            if (hint.Instance == instance && hint.Type != IdentityType.None)
            {
                ICharacter byHint = Pool.Instance.GetObject(parent, hint) as ICharacter;
                if (byHint != null)
                {
                    return byHint;
                }
            }

            ICharacter byCanBeAffected = Pool.Instance.GetObject(
                parent,
                new Identity
                {
                    Type = IdentityType.CanbeAffected,
                    Instance = instance
                }) as ICharacter;
            if (byCanBeAffected != null)
            {
                return byCanBeAffected;
            }

            return Pool.Instance.GetObject<ICharacter>(
                new Identity
                {
                    Type = IdentityType.CanbeAffected,
                    Instance = instance
                });
        }

        private static ICharacter ResolveHealTargetCharacter(
            ICharacter owner,
            Playfield playfield,
            Identity target)
        {
            Identity normalized = ResolveFriendlyHealTargetByInstance(owner, target, playfield);
            if (normalized.Instance == 0)
            {
                return null;
            }

            if (owner != null && normalized.Instance == owner.Identity.Instance)
            {
                return owner;
            }

            return FindCharacterByInstance(playfield, normalized.Instance, normalized);
        }

        private static void ExecuteHeal(
            ICharacter owner,
            ICharacter pet,
            NPCController petController,
            Playfield playfield,
            Identity commandTarget)
        {
            if (!PetCombatRules.IsPlayerOwnedHealingPet(pet))
            {
                Identity followTarget = ResolveFollowTarget(owner, commandTarget);
                petController.Follow(followTarget, 2.0);
                return;
            }

            pet.SetFightingTarget(Identity.None);
            pet.SetTarget(Identity.None);
            playfield.Announce(
                new StopFightMessage
                {
                    Identity = pet.Identity,
                    Unknown1 = 1
                });

            SyncOwnerHealSelectedTarget(owner, commandTarget);

            Identity healFocus = ResolveHealCommandTarget(owner, pet.Identity, commandTarget);
            if (healFocus.Instance != 0)
            {
                SyncHealPetFollow(pet, petController, healFocus);
            }
            else
            {
                petController.Follow(owner.Identity, 2.0);
            }

            PetHealCommandState healState;
            if (!ActiveHealCommands.TryGetValue(pet.Identity.Instance, out healState))
            {
                healState = new PetHealCommandState();
            }

            healState.NextCastUtc = DateTime.UtcNow;
            healState.FocusTarget = healFocus;

            ActiveHealCommands[pet.Identity.Instance] = healState;

            if (healFocus.Instance != 0)
            {
                ApplyHealFocusToActivePets(owner, playfield, healFocus, true);
            }

            if (!healState.AnnouncedStart)
            {
                string ownerName = owner.Name ?? "Your";
                ChatTextMessageHandler.Default.Send(
                    owner,
                    string.Format(
                        "{0}'s pet, {1}: Commencing the healing process now, master.",
                        ownerName,
                        pet.Name));
                healState.AnnouncedStart = true;
                ActiveHealCommands[pet.Identity.Instance] = healState;
            }

            ProcessHealCycle(owner, pet, playfield, ref healState);
            ActiveHealCommands[pet.Identity.Instance] = healState;
        }

        private static Identity ResolveFollowTarget(ICharacter owner, Identity preferredTarget)
        {
            if (preferredTarget.Instance != 0
                && owner.Playfield != null
                && owner.Playfield.FindByIdentity<ICharacter>(preferredTarget) != null)
            {
                return preferredTarget;
            }

            return owner.Identity;
        }

        private static void ProcessHealCycle(
            ICharacter owner,
            ICharacter pet,
            Playfield playfield,
            ref PetHealCommandState healState)
        {
            Identity focus = healState.FocusTarget;
            if (focus.Instance == 0)
            {
                focus = GetOwnerHealFocusSelection(owner, playfield);
                if (focus.Instance != 0)
                {
                    healState.FocusTarget = focus;
                }
            }

            if (focus.Instance == 0)
            {
                healState.NextCastUtc = DateTime.UtcNow.AddSeconds(PetCombatRules.HealCastRetrySeconds);
                return;
            }

            ICharacter focusTarget = ResolveHealTargetCharacter(owner, playfield, focus);
            if (focusTarget == null || focusTarget.Stats[StatIds.health].Value <= 0)
            {
                healState.FocusTarget = Identity.None;
                ReturnPetToOwner(pet);
                healState.NextCastUtc = DateTime.UtcNow.AddSeconds(PetCombatRules.HealCastRetrySeconds);
                return;
            }

            var petController = pet.Controller as NPCController;
            if (petController != null)
            {
                SyncHealPetFollow(pet, petController, focus);
            }

            if (IsHealCandidateReady(pet, focusTarget))
            {
                if (TryCastPetHeal(owner, pet, focusTarget, playfield, ref healState))
                {
                    return;
                }
            }

            healState.NextCastUtc = DateTime.UtcNow.AddSeconds(PetCombatRules.HealCastRetrySeconds);
        }

        private static bool IsHealCandidateReady(ICharacter healPet, ICharacter candidate)
        {
            if (candidate == null
                || candidate.Stats[StatIds.health].Value <= 0
                || !NeedsHealing(candidate)
                || Playfield.GetCombatDistance(healPet, candidate) > PetCombatRules.HealCastRange)
            {
                return false;
            }

            return true;
        }

        private static bool NeedsHealing(ICharacter character)
        {
            return character.Stats[StatIds.health].Value < character.Stats[StatIds.life].Value;
        }

        private static bool TryCastPetHeal(
            ICharacter owner,
            ICharacter pet,
            ICharacter healTarget,
            Playfield playfield,
            ref PetHealCommandState healState)
        {
            int healNanoId;
            if (!PetRuntimeService.Default.TryGetHealNanoId(owner, pet, out healNanoId))
            {
                return false;
            }

            NanoFormula healNano;
            if (!NanoLoader.NanoList.TryGetValue(healNanoId, out healNano))
            {
                return false;
            }

            int nanoCost = PetHealNanoCatalog.GetNanoCastCost(healNano);
            if (nanoCost > 0 && pet.Stats[StatIds.currentnano].Value < nanoCost)
            {
                return false;
            }

            int healRoll;
            int healApplied;
            if (!PetHealNanoCatalog.TryRollHealAmount(healNano, healTarget, out healRoll, out healApplied))
            {
                return false;
            }

            if (healApplied <= 0)
            {
                return false;
            }

            if (nanoCost > 0)
            {
                pet.Stats[StatIds.currentnano].Value -= nanoCost;
                StatMessageHandler.Default.SendSingle(pet, (int)StatIds.currentnano, (uint)pet.Stats[StatIds.currentnano].Value);
            }

            CastNanoSpellMessageHandler.Default.SendPetCast(pet, healNanoId, healTarget.Identity);
            CharacterActionMessageHandler.Default.FinishNanoCasting(
                pet,
                CharacterActionType.FinishNanoCasting,
                Identity.None,
                1,
                healNanoId);
            CharacterActionMessageHandler.Default.SendPetNanoExecutedWithinOwnerNcu(owner, pet, healRoll);

            int healthBefore = healTarget.Stats[StatIds.health].Value;
            healTarget.Stats[StatIds.health].Value += healApplied;
            int healthAfter = healTarget.Stats[StatIds.health].Value;
            int actualHeal = healthAfter - healthBefore;
            if (actualHeal <= 0)
            {
                return false;
            }

            playfield.Announce(
                new HealthDamageMessage
                {
                    Identity = healTarget.Identity,
                    Unknown1 = healthAfter,
                    Unknown2 = actualHeal,
                    Unknown3 = (int)StatIds.flags,
                    Unknown4 = 0,
                    Target = pet.Identity,
                    Unknown5 = 0
                });

            playfield.Announce(
                new FormatFeedbackMessage
                {
                    Identity = owner.Identity,
                    Unknown = 1,
                    Unknown1 = 0,
                    FormattedMessage = string.Format(
                        "~&!!!\":$Dt11s\n{0}\x15{1}",
                        pet.Name,
                        PetHealNanoCatalog.GetHealNanoDisplayName(healNanoId)),
                    Unknown2 = 0
                });

            ChatTextMessageHandler.Default.Send(
                owner,
                string.Format(
                    "{0} executes {1} on {2}.",
                    pet.Name,
                    PetHealNanoCatalog.GetHealNanoDisplayName(healNanoId),
                    healTarget.Name));

            StatMessageHandler.Default.SendChanged(healTarget);
            StatMessageHandler.Default.SendChanged(pet);

            LogUtil.Debug(
                DebugInfoDetail.GameFunctions,
                string.Format(
                    "PetHealCast pet={0} target={1} nano={2} roll={3} applied={4} cost={5}",
                    pet.Identity,
                    healTarget.Identity,
                    healNanoId,
                    healRoll,
                    actualHeal,
                    nanoCost));

            healState.NextCastUtc = DateTime.UtcNow.AddSeconds(
                PetHealNanoCatalog.GetHealRechargeSeconds(healNanoId));
            return true;
        }

        private static bool TryResolveCommandId(string command, out int commandId)
        {
            commandId = 0;
            if (string.IsNullOrWhiteSpace(command))
            {
                return false;
            }

            string normalized = command.Trim();
            if (normalized.Equals("follow", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("follow me", StringComparison.OrdinalIgnoreCase))
            {
                commandId = CommandFollow;
                return true;
            }

            if (normalized.Equals("behind", StringComparison.OrdinalIgnoreCase))
            {
                commandId = CommandBehind;
                return true;
            }

            if (normalized.Equals("wait", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("stop", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("stay", StringComparison.OrdinalIgnoreCase))
            {
                commandId = CommandWait;
                return true;
            }

            if (normalized.Equals("guard", StringComparison.OrdinalIgnoreCase))
            {
                commandId = CommandGuard;
                return true;
            }

            if (normalized.Equals("attack", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("hunt", StringComparison.OrdinalIgnoreCase))
            {
                commandId = CommandAttack;
                return true;
            }

            if (normalized.Equals("heal", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("cast", StringComparison.OrdinalIgnoreCase))
            {
                commandId = CommandHeal;
                return true;
            }

            if (normalized.Equals("report", StringComparison.OrdinalIgnoreCase))
            {
                commandId = CommandReport;
                return true;
            }

            if (normalized.Equals("terminate", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("dismiss", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("release", StringComparison.OrdinalIgnoreCase))
            {
                commandId = CommandTerminate;
                return true;
            }

            int parsed;
            if (int.TryParse(normalized, out parsed) && parsed > 0)
            {
                commandId = parsed;
                return true;
            }

            return false;
        }

        private sealed class PetHealCommandState
        {
            public Identity FocusTarget { get; set; }

            public DateTime NextCastUtc { get; set; }

            public bool AnnouncedStart { get; set; }
        }
    }
}
