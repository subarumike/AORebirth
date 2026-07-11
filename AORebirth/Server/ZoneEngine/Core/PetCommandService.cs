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

            ICharacter pet = owner.Playfield.FindByIdentity<ICharacter>(petIdentity);
            if (pet == null || pet.Stats[StatIds.petmaster].Value != owner.Identity.Instance)
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
                    if (!PetCombatRules.IsPlayerOwnedAttackPet(pet))
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
            if (owner == null || lookTarget.Instance == 0 || !HasActiveHealCommand(owner))
            {
                return false;
            }

            Playfield playfield = owner.Playfield as Playfield;
            if (playfield == null)
            {
                return false;
            }

            Identity friendly = NormalizeFriendlyHealIdentity(owner, lookTarget, playfield);
            if (friendly.Instance == 0)
            {
                return false;
            }

            owner.SetTarget(friendly);
            ApplyHealFocusToActivePets(owner, playfield, friendly, true);
            return true;
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

            Identity liveFocus = ResolveLiveHealFocus(owner, playfield, Identity.None);
            if (liveFocus.Instance != 0 && liveFocus.Instance != healState.FocusTarget.Instance)
            {
                healState.FocusTarget = liveFocus;
                healState.RotateIndex = 0;
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

            Identity focus = ResolveLiveHealFocus(owner, playfield, commandTarget);
            if (focus.Instance != 0)
            {
                owner.SetTarget(focus);
                ApplyHealFocusToActivePets(owner, playfield, focus, false);
            }
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
                healState.RotateIndex = 0;
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

            Identity normalizedTarget = NormalizeFriendlyHealIdentity(owner, owner.SelectedTarget, playfield);
            if (normalizedTarget.Instance != 0)
            {
                owner.SetTarget(normalizedTarget);
                ApplyHealFocusToActivePets(owner, playfield, normalizedTarget, true);
                return;
            }

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

                healState.RotateIndex = 0;
                healState.NextCastUtc = DateTime.UtcNow;
                ProcessHealCycle(owner, pet, playfield, ref healState);
                ActiveHealCommands[petInstance] = healState;
            }
        }

        private static Identity ResolveLiveHealFocus(
            ICharacter owner,
            Playfield playfield,
            Identity commandTarget)
        {
            if (commandTarget.Instance != 0)
            {
                Identity normalizedCommandTarget = NormalizeFriendlyHealIdentity(owner, commandTarget, playfield);
                if (normalizedCommandTarget.Instance != 0)
                {
                    return normalizedCommandTarget;
                }
            }

            return NormalizeFriendlyHealIdentity(owner, owner.SelectedTarget, playfield);
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

            return playfield.FindByIdentity<ICharacter>(
                new Identity
                {
                    Type = IdentityType.CanbeAffected,
                    Instance = instance
                })
                ?? ResolveCharacterFromPool(playfield, instance);
        }

        private static ICharacter ResolveCharacterFromPool(Playfield playfield, int instance)
        {
            if (playfield == null || instance == 0)
            {
                return null;
            }

            ICharacter fromPool = Pool.Instance.GetObject<ICharacter>(
                playfield.Identity,
                new Identity
                {
                    Type = IdentityType.CanbeAffected,
                    Instance = instance
                });
            return fromPool;
        }

        private static ICharacter ResolveHealTargetCharacter(
            ICharacter owner,
            Playfield playfield,
            Identity target)
        {
            Identity normalized = NormalizeFriendlyHealIdentity(owner, target, playfield);
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

            Identity healFocus = ResolveLiveHealFocus(owner, playfield, commandTarget);
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

            healState.RotateIndex = 0;
            healState.NextCastUtc = DateTime.UtcNow;

            if (healFocus.Instance != 0)
            {
                healState.FocusTarget = healFocus;
            }

            ActiveHealCommands[pet.Identity.Instance] = healState;

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
                focus = ResolveLiveHealFocus(owner, playfield, Identity.None);
                if (focus.Instance != 0)
                {
                    healState.FocusTarget = focus;
                }
            }

            if (focus.Instance != 0)
            {
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

                if (IsHealCandidateReady(pet, focusTarget)
                    && TryCastPetHeal(owner, pet, focusTarget, playfield))
                {
                    healState.NextCastUtc = DateTime.UtcNow.AddSeconds(PetCombatRules.HealCastRetrySeconds);
                    return;
                }

                healState.NextCastUtc = DateTime.UtcNow.AddSeconds(PetCombatRules.HealCastRetrySeconds);
                return;
            }

            List<ICharacter> friendlyTargets = CollectFriendlyHealTargets(owner, pet, playfield);
            List<ICharacter> needyTargets = new List<ICharacter>();
            foreach (ICharacter candidate in friendlyTargets)
            {
                if (IsHealCandidateReady(pet, candidate))
                {
                    needyTargets.Add(candidate);
                }
            }

            if (needyTargets.Count == 0)
            {
                healState.NextCastUtc = DateTime.UtcNow.AddSeconds(PetCombatRules.HealCastRetrySeconds);
                return;
            }

            int startIndex = healState.RotateIndex % needyTargets.Count;
            for (int offset = 0; offset < needyTargets.Count; offset++)
            {
                int index = (startIndex + offset) % needyTargets.Count;
                if (TryCastPetHeal(owner, pet, needyTargets[index], playfield))
                {
                    healState.RotateIndex = (index + 1) % needyTargets.Count;
                    healState.NextCastUtc = DateTime.UtcNow.AddSeconds(PetCombatRules.HealCastRetrySeconds);
                    return;
                }
            }

            healState.NextCastUtc = DateTime.UtcNow.AddSeconds(PetCombatRules.HealCastRetrySeconds);
        }

        private static List<ICharacter> CollectFriendlyHealTargets(
            ICharacter owner,
            ICharacter healPet,
            Playfield playfield)
        {
            var targets = new List<ICharacter>();
            if (owner != null
                && owner.InPlayfield(playfield.Identity)
                && owner.Stats[StatIds.health].Value > 0)
            {
                targets.Add(owner);
            }

            foreach (int strain in PetRuntimeService.Default.GetActivePetStrains(owner))
            {
                ICharacter ownedPet = PetRuntimeService.Default.GetActivePetInStrain(owner, strain);
                if (ownedPet == null
                    || !ownedPet.InPlayfield(playfield.Identity)
                    || ownedPet.Stats[StatIds.health].Value <= 0)
                {
                    continue;
                }

                targets.Add(ownedPet);
            }

            return targets;
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
            Playfield playfield)
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
                    Unknown1 = actualHeal,
                    Unknown2 = 0,
                    Unknown3 = 0,
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

            public int RotateIndex { get; set; }

            public DateTime NextCastUtc { get; set; }

            public bool AnnouncedStart { get; set; }
        }
    }
}
