namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Events;
    using AORebirth.Core.Functions;
    using AORebirth.Core.Nanos;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Textures;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Playfields;

    internal sealed class OrdinaryEnemyRuntimeService
    {
        private readonly OrdinaryEnemyCatalog catalog;

        private readonly NpcPatrolReplayCoordinator patrolReplay;

        private readonly PlayfieldDynelRegistry dynelRegistry;

        private readonly Action<ICharacter> activateNpc;

        private readonly Random spawnRandom;

        private readonly Func<int, int> levelSelector;

        private readonly Dictionary<int, OrdinaryEnemyLevelSelectionState> levelSelectionBySource =
            new Dictionary<int, OrdinaryEnemyLevelSelectionState>();

        private readonly Dictionary<int, OrdinaryEnemyRuntimeDefinition> activeByRuntimeIdentity =
            new Dictionary<int, OrdinaryEnemyRuntimeDefinition>();

        private readonly Dictionary<int, int> activeRuntimeIdentityBySource =
            new Dictionary<int, int>();

        private readonly Dictionary<int, OrdinaryEnemySupportNanoRuntimeState>
            supportNanoStateByRuntimeIdentity =
                new Dictionary<int, OrdinaryEnemySupportNanoRuntimeState>();

        private readonly Dictionary<int, Dictionary<int, OrdinaryEnemyTransientNanoEffectState>>
            transientNanoEffectsByRecipient =
                new Dictionary<int, Dictionary<int, OrdinaryEnemyTransientNanoEffectState>>();

        internal OrdinaryEnemyRuntimeService(
            OrdinaryEnemyCatalog catalog,
            NpcPatrolReplayCoordinator patrolReplay,
            PlayfieldDynelRegistry dynelRegistry,
            Action<ICharacter> activateNpc,
            Func<int, int> levelSelector = null)
        {
            this.catalog = catalog;
            this.patrolReplay = patrolReplay;
            this.dynelRegistry = dynelRegistry;
            this.activateNpc = activateNpc;
            if (levelSelector == null)
            {
                this.spawnRandom = new Random();
                this.levelSelector = this.spawnRandom.Next;
            }
            else
            {
                this.levelSelector = levelSelector;
            }
        }

        internal bool SpawnFromPopulation(
            Playfield playfield,
            Identity playfieldIdentity,
            OrdinaryEnemySpawnDefinition spawn,
            int generation,
            out Identity runtimeIdentity,
            out OrdinaryEnemySpawnGeneration selectedGeneration)
        {
            runtimeIdentity = Identity.None;
            selectedGeneration = null;
            if (spawn == null)
            {
                return false;
            }

            if (this.activeRuntimeIdentityBySource.ContainsKey(spawn.SourceIdentity))
            {
                return false;
            }

            OrdinaryEnemyProfile profile;
            if (!this.catalog.TryGetProfile(spawn.ProfileKey, out profile))
            {
                SubwayVisibilityDiagnosticSelection.RecordPopulationFailure(
                    spawn.SourceIdentity,
                    "profile lookup failed");
                return false;
            }
            bool spawned;
            OrdinaryEnemySpawnGeneration spawnGeneration;
            try
            {
                OrdinaryEnemyLevelSelectionState selectionState;
                if (!this.levelSelectionBySource.TryGetValue(spawn.SourceIdentity, out selectionState))
                {
                    selectionState = new OrdinaryEnemyLevelSelectionState();
                    this.levelSelectionBySource.Add(spawn.SourceIdentity, selectionState);
                }

                spawnGeneration = selectionState.ResolveForGeneration(
                    spawn.LevelDefinition,
                    generation,
                    this.levelSelector);
                spawned = this.Spawn(
                    playfield,
                    playfieldIdentity,
                    spawn,
                    profile,
                    spawnGeneration,
                    out runtimeIdentity);
            }
            catch (Exception exception)
            {
                SubwayVisibilityDiagnosticSelection.RecordPopulationFailure(
                    spawn.SourceIdentity,
                    "materialization exception: " + exception.GetType().Name);
                throw;
            }

            if (spawned)
            {
                selectedGeneration = spawnGeneration;
            }
            else
            {
                SubwayVisibilityDiagnosticSelection.RecordPopulationFailure(
                    spawn.SourceIdentity,
                    "runtime materialization returned false");
            }

            return spawned;
        }

        internal void ClearRuntimeState(int playfieldInstance)
        {
            this.RemoveAllTransientNanoEffects();
            foreach (int runtimeIdentity in this.activeByRuntimeIdentity.Keys.ToArray())
            {
                OrdinaryEnemyRuntimeRegistry.Remove(runtimeIdentity);
                SubwayVisibilityDiagnosticSelection.RemoveRuntimeIdentity(runtimeIdentity);
                CapturedEnemyCombatRuntimeRegistry.Remove(runtimeIdentity);
            }

            this.activeByRuntimeIdentity.Clear();
            this.activeRuntimeIdentityBySource.Clear();
            this.supportNanoStateByRuntimeIdentity.Clear();
            this.transientNanoEffectsByRecipient.Clear();
            this.levelSelectionBySource.Clear();
            OrdinaryEnemyRuntimeRegistry.RemoveForPlayfield(playfieldInstance);
        }

        internal bool ReleasePopulationRuntime(
            ICharacter target,
            out OrdinaryEnemyRuntimeDefinition definition)
        {
            definition = null;
            if (target == null || !this.activeByRuntimeIdentity.TryGetValue(target.Identity.Instance, out definition)) return false;

            this.activeByRuntimeIdentity.Remove(target.Identity.Instance);
            this.activeRuntimeIdentityBySource.Remove(definition.Spawn.SourceIdentity);
            this.supportNanoStateByRuntimeIdentity.Remove(target.Identity.Instance);
            this.RemoveTransientNanoEffectsForCaster(target.Identity.Instance);
            this.RemoveTransientNanoEffectsForRecipient(target);
            return true;
        }

        internal void NotifyCharacterDied(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            this.supportNanoStateByRuntimeIdentity.Remove(character.Identity.Instance);
            this.RemoveTransientNanoEffectsForCaster(character.Identity.Instance);
            this.RemoveTransientNanoEffectsForRecipient(character);
        }

        internal void ProcessExpiredSupportNanoEffects(DateTime utcNow)
        {
            foreach (OrdinaryEnemyTransientNanoEffectState state in this.transientNanoEffectsByRecipient
                .SelectMany(value => value.Value.Values)
                .Where(
                    value => value.PeriodicSchedule != null
                             && value.PeriodicSchedule.RemainingTicks > 0
                             && value.PeriodicSchedule.NextTickAtUtc <= utcNow)
                .ToArray())
            {
                ICharacter recipient = this.dynelRegistry.FindByIdentity<ICharacter>(
                    state.RecipientIdentity);
                this.ProcessPeriodicNanoTicks(state, recipient, utcNow);
            }

            foreach (OrdinaryEnemyTransientNanoEffectState state in this.transientNanoEffectsByRecipient
                .SelectMany(value => value.Value.Values)
                .Where(value => value.ExpiresAtUtc <= utcNow)
                .ToArray())
            {
                ICharacter recipient = this.dynelRegistry.FindByIdentity<ICharacter>(
                    state.RecipientIdentity);
                this.RemoveTransientNanoEffect(state, recipient);
            }
        }

        internal bool TryProcessSupportNano(ICharacter caster, DateTime utcNow)
        {
            OrdinaryEnemyRuntimeDefinition definition;
            OrdinaryEnemySupportNanoRuntimeState state;
            if (caster == null
                || caster.Stats[StatIds.health].Value <= 0
                || !this.activeByRuntimeIdentity.TryGetValue(caster.Identity.Instance, out definition)
                || definition.Profile.SupportNano == null
                || !this.supportNanoStateByRuntimeIdentity.TryGetValue(
                    caster.Identity.Instance,
                    out state))
            {
                return false;
            }

            OrdinaryEnemySupportNanoProfile profile = definition.Profile.SupportNano;
            bool blocksOtherActions = !profile.AllowCombatActionsDuringCast;
            if (state.CastInProgress)
            {
                if (utcNow < state.FinishAtUtc)
                {
                    return blocksOtherActions;
                }

                this.FinishSupportNanoCast(caster, profile, state, utcNow);
                state.CastInProgress = false;
                state.TargetIdentity = Identity.None;
                return blocksOtherActions;
            }

            if ((!profile.CastWhileFighting && caster.FightingTarget.Instance != 0)
                || utcNow < state.NextCastAtUtc)
            {
                return false;
            }

            state.NextCastAtUtc = utcNow.AddSeconds(profile.RepeatSeconds);
            if (!this.RollSupportNanoChance(profile.CastChanceBasisPoints))
            {
                return false;
            }

            ICharacter target = this.FindSupportNanoTarget(caster, profile);
            if (target == null)
            {
                return false;
            }

            int resolvedModifierStatId;
            int resolvedModifierDelta;
            if (profile.ResolvePrimaryModifierFromNanoData
                && !TryResolveNanoDataStaticModifier(
                    profile.PrimaryNanoId,
                    out resolvedModifierStatId,
                    out resolvedModifierDelta))
            {
                return false;
            }

            int remainingNano;
            if (!OrdinaryEnemySupportNanoRuntimeRules.TrySpendNano(
                caster.Stats[StatIds.currentnano].Value,
                profile.NanoCost,
                out remainingNano))
            {
                return false;
            }

            if (profile.NanoCost > 0)
            {
                caster.Stats[StatIds.currentnano].Value = remainingNano;
                StatMessageHandler.Default.AnnounceSingle(
                    caster,
                    (int)StatIds.currentnano,
                    (uint)remainingNano);
            }

            if (blocksOtherActions)
            {
                caster.Controller.StopMovement();
            }

            CastNanoSpellMessageHandler.Default.SendNpcCast(
                caster,
                profile.PrimaryNanoId,
                target.Identity);
            state.CastInProgress = true;
            state.TargetIdentity = target.Identity;
            state.FinishAtUtc = utcNow.AddSeconds(profile.CastSeconds);
            return blocksOtherActions;
        }

        internal ICharacter FindAutomaticAggroTarget(ICharacter npc)
        {
            OrdinaryEnemyRuntimeDefinition definition;
            if (npc == null
                || !OrdinaryEnemyRuntimeRegistry.TryGet(npc.Identity.Instance, out definition)
                || definition.Profile.Aggression.Mode != OrdinaryEnemyAggressionMode.Auto
                || !definition.Profile.Aggression.AutomaticAggroRadius.HasValue)
            {
                return null;
            }

            return this.dynelRegistry
                .FindCharactersInRange(
                    npc,
                    (float)definition.Profile.Aggression.AutomaticAggroRadius.Value)
                .Where(
                    candidate => candidate != null
                                 && candidate.Identity != npc.Identity
                                 && candidate.Controller is PlayerController
                                 && candidate.Stats[StatIds.health].Value > 0)
                .OrderBy(candidate => candidate.Coordinates().coordinate.Distance2D(npc.Coordinates().coordinate))
                .ThenBy(candidate => candidate.Identity.Instance)
                .FirstOrDefault();
        }

        internal void TryReturnToSpawn(ICharacter npc)
        {
            OrdinaryEnemyRuntimeDefinition definition;
            NPCController controller = npc == null ? null : npc.Controller as NPCController;
            if (controller == null
                || npc.FightingTarget.Instance != 0
                || controller.IsFollowing()
                || !OrdinaryEnemyRuntimeRegistry.TryGet(npc.Identity.Instance, out definition)
                || !definition.Profile.Aggression.ReturnToSpawn
                || definition.Spawn.MovementMode != OrdinaryEnemyMovementMode.Static)
            {
                return;
            }

            var home = new AORebirth.Core.Vector.Vector3(
                definition.Spawn.X,
                definition.Spawn.Y,
                definition.Spawn.Z);
            if (npc.Coordinates().coordinate.Distance2D(home) <= 0.5)
            {
                return;
            }

            controller.MoveTo(
                new SmokeLounge.AOtomation.Messaging.GameData.Vector3
                {
                    X = definition.Spawn.X,
                    Y = definition.Spawn.Y,
                    Z = definition.Spawn.Z
                });
        }

        private bool Spawn(
            Playfield playfield,
            Identity playfieldIdentity,
            OrdinaryEnemySpawnDefinition spawn,
            OrdinaryEnemyProfile profile,
            OrdinaryEnemySpawnGeneration spawnGeneration,
            out Identity runtimeIdentity)
        {
            runtimeIdentity = Identity.None;
            var controller = new NPCController();
            OrdinaryEnemySpawnVariant variant = spawnGeneration.SelectedVariant;
            Character character = this.ConstructCharacter(
                playfield,
                playfieldIdentity,
                spawn,
                variant,
                profile,
                controller);
            if (character == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Ordinary enemy spawn construction failed profile=" + profile.ProfileKey);
                return false;
            }

            ApplyStats(character, variant, profile);
            ApplyAppearance(character, profile);
            this.ApplyMovement(character, controller, spawn);

            string combatFailure;
            CapturedEnemyCombatContract combatContract =
                profile.Combat.ResolveContract(spawn.SourceIdentity, variant);
            bool combatReady = CapturedEnemyCombatRuntime.Prepare(
                character,
                controller,
                combatContract,
                out combatFailure);
            if (!combatReady)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Ordinary enemy combat contract incomplete sourceIdentity=SimpleChar:{0:X8} profile={1} reason={2}",
                        spawn.SourceIdentity,
                        profile.ProfileKey,
                        combatFailure));
                CapturedEnemyCombatRuntimeRegistry.Remove(character.Identity.Instance);
                return false;
            }

            character.DoNotDoTimers = false;
            var runtimeDefinition = new OrdinaryEnemyRuntimeDefinition(
                spawn,
                profile,
                spawnGeneration);
            OrdinaryEnemyRuntimeRegistry.Register(character.Identity.Instance, runtimeDefinition);
            this.activateNpc(character);
            this.activeByRuntimeIdentity[character.Identity.Instance] = runtimeDefinition;
            this.activeRuntimeIdentityBySource[spawn.SourceIdentity] = character.Identity.Instance;
            if (profile.SupportNano != null)
            {
                this.supportNanoStateByRuntimeIdentity[character.Identity.Instance] =
                    new OrdinaryEnemySupportNanoRuntimeState
                    {
                        NextCastAtUtc = DateTime.UtcNow.AddSeconds(
                            this.SelectSupportNanoInitialDelay(profile.SupportNano))
                    };
            }
            SubwayVisibilityDiagnosticSelection.RegisterRuntimeIdentity(
                character.Identity.Instance,
                spawn.SourceIdentity);
            playfield.AnnounceSpawnedCharacterVisibility(character, Identity.None);
            runtimeIdentity = character.Identity;

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Ordinary enemy spawned sourceIdentity=SimpleChar:{0:X8} serverIdentity={1} profile={2} name={3} monsterData={4} level={5} position=({6},{7},{8}) combatModel={9} combatReady={10}",
                    spawn.SourceIdentity,
                    character.Identity,
                    profile.ProfileKey,
                    profile.DisplayName,
                    profile.MonsterData,
                    variant.Level,
                    spawn.X,
                    spawn.Y,
                    spawn.Z,
                    combatContract.AttackModel,
                    combatReady));
            return true;
        }

        private ICharacter FindSupportNanoTarget(
            ICharacter caster,
            OrdinaryEnemySupportNanoProfile profile)
        {
            if (this.RollSupportNanoChance(profile.SelfTargetChanceBasisPoints))
            {
                return caster;
            }

            ICharacter target = this.dynelRegistry
                .FindCharactersInRange(caster, (float)profile.TargetRange)
                .Where(
                    candidate => candidate != null
                                 && candidate.Identity != caster.Identity
                                 && candidate.Stats[StatIds.health].Value > 0
                                 && IsOrdinaryEnemy(candidate))
                .OrderBy(
                    candidate => candidate.Coordinates().coordinate.Distance2D(
                        caster.Coordinates().coordinate))
                .ThenBy(candidate => candidate.Identity.Instance)
                .FirstOrDefault();
            return target ?? (profile.FallbackToSelf ? caster : null);
        }

        private double SelectSupportNanoInitialDelay(OrdinaryEnemySupportNanoProfile profile)
        {
            return OrdinaryEnemySupportNanoRuntimeRules.SelectInitialDelaySeconds(
                profile,
                this.levelSelector);
        }

        private bool RollSupportNanoChance(int chanceBasisPoints)
        {
            return OrdinaryEnemySupportNanoRuntimeRules.RollChance(
                chanceBasisPoints,
                this.levelSelector);
        }

        internal static bool TryResolveNanoDataStaticModifier(
            int nanoId,
            out int statId,
            out int modifierDelta)
        {
            NanoFormula nano;
            statId = 0;
            modifierDelta = 0;
            return NanoLoader.NanoList.TryGetValue(nanoId, out nano)
                   && TryResolveNanoDataStaticModifier(
                       nano,
                       out statId,
                       out modifierDelta);
        }

        internal static bool TryResolveNanoDataStaticModifier(
            NanoFormula nano,
            out int statId,
            out int modifierDelta)
        {
            statId = 0;
            modifierDelta = 0;
            if (nano == null
                || nano.Events == null
                || nano.Events.Count != 1)
            {
                return false;
            }

            Event onUse = nano.Events[0];
            if (onUse == null
                || onUse.EventType != EventType.OnUse
                || onUse.Functions == null
                || onUse.Functions.Count != 1)
            {
                return false;
            }

            Function function = onUse.Functions[0];
            if (function == null
                || function.FunctionType != (int)FunctionType.Skill
                || function.Target != (int)ItemTarget.Target
                || function.TickCount != 1
                || function.TickInterval != 0
                || !function.dolocalstats
                || function.Requirements == null
                || function.Requirements.Count != 0
                || function.Arguments == null
                || function.Arguments.Values == null
                || function.Arguments.Values.Count != 2)
            {
                return false;
            }

            statId = function.Arguments.Values[0].AsInt32();
            modifierDelta = function.Arguments.Values[1].AsInt32();
            return statId > 0 && modifierDelta != 0;
        }

        private static bool IsOrdinaryEnemy(ICharacter candidate)
        {
            OrdinaryEnemyRuntimeDefinition ignored;
            return candidate != null
                   && OrdinaryEnemyRuntimeRegistry.TryGet(
                       candidate.Identity.Instance,
                       out ignored);
        }

        private void FinishSupportNanoCast(
            ICharacter caster,
            OrdinaryEnemySupportNanoProfile profile,
            OrdinaryEnemySupportNanoRuntimeState state,
            DateTime utcNow)
        {
            CharacterActionMessageHandler.Default.FinishNanoCasting(
                caster,
                CharacterActionType.FinishNanoCasting,
                Identity.None,
                1,
                profile.PrimaryNanoId);

            ICharacter target = this.dynelRegistry.FindByIdentity<ICharacter>(state.TargetIdentity);
            if (target == null || target.Stats[StatIds.health].Value <= 0)
            {
                return;
            }

            int primaryModifierDelta = profile.PrimaryModifierDelta;
            int[] primaryAffectedStatIds = profile.AffectedStatIds;
            if (profile.ResolvePrimaryModifierFromNanoData)
            {
                int primaryModifierStatId;
                if (!TryResolveNanoDataStaticModifier(
                    profile.PrimaryNanoId,
                    out primaryModifierStatId,
                    out primaryModifierDelta))
                {
                    return;
                }

                primaryAffectedStatIds = new[] { primaryModifierStatId };
            }

            bool primaryFirstActivation = profile.HasPeriodicStatHit
                ? this.ApplyOrRefreshPeriodicNanoHit(caster, target, profile, utcNow)
                : this.ApplyOrRefreshTransientNanoEffect(
                    caster,
                    target,
                    profile.PrimaryNanoId,
                    profile.PrimaryStrain,
                    primaryModifierDelta,
                    profile,
                    utcNow,
                    primaryAffectedStatIds);

            if (primaryFirstActivation)
            {
                BuffMessageHandler.Default.SendAddNanoBuff(target, profile.PrimaryNanoId);
            }

            CharacterActionMessageHandler.Default.NotifyActiveNanoDurationToPlayfield(
                caster,
                target.Identity,
                profile.PrimaryNanoId,
                profile.DurationParameter);
            if (profile.HasTriggeredSelfEffect)
            {
                bool triggeredSelfFirstActivation = this.ApplyOrRefreshTransientNanoEffect(
                    caster,
                    caster,
                    profile.TriggeredSelfNanoId,
                    profile.TriggeredSelfStrain,
                    profile.TriggeredSelfModifierDelta,
                    profile,
                    utcNow);
                CastNanoSpellMessageHandler.Default.SendTriggeredSelfCast(
                    caster,
                    profile.TriggeredSelfNanoId);
                if (triggeredSelfFirstActivation)
                {
                    BuffMessageHandler.Default.SendAddNanoBuff(caster, profile.TriggeredSelfNanoId);
                }

                CharacterActionMessageHandler.Default.NotifyActiveNanoDurationToPlayfield(
                    caster,
                    caster.Identity,
                    profile.TriggeredSelfNanoId,
                    profile.DurationParameter);
            }
        }

        private bool ApplyOrRefreshPeriodicNanoHit(
            ICharacter caster,
            ICharacter recipient,
            OrdinaryEnemySupportNanoProfile profile,
            DateTime utcNow)
        {
            this.ApplyPeriodicNanoStatHit(
                recipient,
                profile.PeriodicStatId,
                profile.PeriodicStatDelta);

            Dictionary<int, OrdinaryEnemyTransientNanoEffectState> recipientEffects;
            if (!this.transientNanoEffectsByRecipient.TryGetValue(
                recipient.Identity.Instance,
                out recipientEffects))
            {
                recipientEffects = new Dictionary<int, OrdinaryEnemyTransientNanoEffectState>();
                this.transientNanoEffectsByRecipient.Add(
                    recipient.Identity.Instance,
                    recipientEffects);
            }

            OrdinaryEnemyTransientNanoEffectState existing;
            if (recipientEffects.TryGetValue(profile.PrimaryNanoId, out existing))
            {
                existing.CasterInstance = caster.Identity.Instance;
                existing.PeriodicSchedule.Refresh(profile, utcNow);
                existing.ExpiresAtUtc = existing.PeriodicSchedule.ExpiresAtUtc;
                RefreshProjectedActiveNano(
                    recipient,
                    existing,
                    profile.DurationParameter);
                return false;
            }

            foreach (OrdinaryEnemyTransientNanoEffectState replaced in recipientEffects.Values
                .Where(
                    value => value.Strain == profile.PrimaryStrain
                             && value.NanoId != profile.PrimaryNanoId)
                .ToArray())
            {
                this.RemoveTransientNanoEffect(replaced, recipient);
            }

            if (!this.transientNanoEffectsByRecipient.ContainsKey(recipient.Identity.Instance))
            {
                this.transientNanoEffectsByRecipient.Add(
                    recipient.Identity.Instance,
                    recipientEffects);
            }

            int activeNanoKey = ResolveAvailableActiveNanoKey(
                recipient,
                profile.PrimaryStrain,
                profile.PrimaryNanoId);
            var periodicSchedule = new OrdinaryEnemyPeriodicNanoSchedule(profile, utcNow);
            var state = new OrdinaryEnemyTransientNanoEffectState
            {
                RecipientIdentity = recipient.Identity,
                NanoId = profile.PrimaryNanoId,
                Strain = profile.PrimaryStrain,
                ModifierDelta = 0,
                StatIds = new int[0],
                CasterInstance = caster.Identity.Instance,
                ActiveNanoKey = activeNanoKey,
                ExpiresAtUtc = periodicSchedule.ExpiresAtUtc,
                PeriodicStatId = profile.PeriodicStatId,
                PeriodicStatDelta = profile.PeriodicStatDelta,
                PeriodicSchedule = periodicSchedule
            };
            recipientEffects.Add(profile.PrimaryNanoId, state);
            recipient.ActiveNanos[activeNanoKey] = new ActiveNanoState
            {
                ID = profile.PrimaryNanoId,
                Instance = profile.PrimaryNanoId,
                Nanotype = 0,
                TickCounter = profile.DurationParameter,
                TickInterval = profile.DurationParameter,
                NcuCost = profile.NcuCost,
                ExpiresAtUtc = state.ExpiresAtUtc,
                PlayfieldBound = true,
                DurationPacketIdentity = recipient.Identity,
                DurationParameter1 = caster.Identity.Instance
            };
            return true;
        }

        private bool ApplyOrRefreshTransientNanoEffect(
            ICharacter caster,
            ICharacter recipient,
            int nanoId,
            int strain,
            int modifierDelta,
            OrdinaryEnemySupportNanoProfile profile,
            DateTime utcNow,
            int[] affectedStatIds = null)
        {
            Dictionary<int, OrdinaryEnemyTransientNanoEffectState> recipientEffects;
            if (!this.transientNanoEffectsByRecipient.TryGetValue(
                recipient.Identity.Instance,
                out recipientEffects))
            {
                recipientEffects = new Dictionary<int, OrdinaryEnemyTransientNanoEffectState>();
                this.transientNanoEffectsByRecipient.Add(
                    recipient.Identity.Instance,
                    recipientEffects);
            }

            OrdinaryEnemyTransientNanoEffectState existing;
            if (recipientEffects.TryGetValue(nanoId, out existing))
            {
                existing.CasterInstance = caster.Identity.Instance;
                existing.ExpiresAtUtc = utcNow.AddSeconds(profile.EffectLifetimeSeconds);
                RefreshProjectedActiveNano(
                    recipient,
                    existing,
                    profile.DurationParameter);
                return false;
            }

            foreach (OrdinaryEnemyTransientNanoEffectState replaced in recipientEffects.Values
                .Where(value => value.Strain == strain && value.NanoId != nanoId)
                .ToArray())
            {
                this.RemoveTransientNanoEffect(replaced, recipient);
            }

            if (!this.transientNanoEffectsByRecipient.ContainsKey(recipient.Identity.Instance))
            {
                this.transientNanoEffectsByRecipient.Add(
                    recipient.Identity.Instance,
                    recipientEffects);
            }

            int activeNanoKey = ResolveAvailableActiveNanoKey(recipient, strain, nanoId);
            var state = new OrdinaryEnemyTransientNanoEffectState
            {
                RecipientIdentity = recipient.Identity,
                NanoId = nanoId,
                Strain = strain,
                ModifierDelta = modifierDelta,
                StatIds = (int[])(affectedStatIds ?? profile.AffectedStatIds).Clone(),
                CasterInstance = caster.Identity.Instance,
                ActiveNanoKey = activeNanoKey,
                ExpiresAtUtc = utcNow.AddSeconds(profile.EffectLifetimeSeconds)
            };
            foreach (int statId in state.StatIds)
            {
                recipient.Stats[statId].Modifier += modifierDelta;
            }

            recipientEffects.Add(nanoId, state);
            recipient.ActiveNanos[activeNanoKey] = new ActiveNanoState
            {
                ID = nanoId,
                Instance = nanoId,
                Nanotype = 0,
                TickCounter = profile.DurationParameter,
                TickInterval = profile.DurationParameter,
                NcuCost = profile.NcuCost,
                ExpiresAtUtc = state.ExpiresAtUtc,
                PlayfieldBound = true,
                DurationPacketIdentity = recipient.Identity,
                DurationParameter1 = caster.Identity.Instance
            };
            return true;
        }

        private void ProcessPeriodicNanoTicks(
            OrdinaryEnemyTransientNanoEffectState state,
            ICharacter recipient,
            DateTime utcNow)
        {
            if (state == null
                || state.PeriodicSchedule == null
                || recipient == null
                || recipient.Stats[StatIds.health].Value <= 0)
            {
                return;
            }

            int dueTicks = state.PeriodicSchedule.ConsumeDueTicks(utcNow);
            for (int i = 0; i < dueTicks; i++)
            {
                this.ApplyPeriodicNanoStatHit(
                    recipient,
                    state.PeriodicStatId,
                    state.PeriodicStatDelta);
            }
        }

        private void ApplyPeriodicNanoStatHit(ICharacter recipient, int statId, int delta)
        {
            if (recipient == null || statId != (int)StatIds.currentnano || delta <= 0)
            {
                return;
            }

            int maximum = Math.Max(0, recipient.Stats[StatIds.maxnanoenergy].Value);
            int current = Math.Max(0, recipient.Stats[StatIds.currentnano].Value);
            int updated = OrdinaryEnemySupportNanoRuntimeRules.ApplyPositiveCappedDelta(
                current,
                maximum,
                delta);
            if (updated <= current)
            {
                return;
            }

            recipient.Stats[StatIds.currentnano].Value = updated;
            StatMessageHandler.Default.AnnounceSingle(
                recipient,
                (int)StatIds.currentnano,
                (uint)updated);
        }

        private static int ResolveAvailableActiveNanoKey(
            ICharacter recipient,
            int strain,
            int nanoId)
        {
            IActiveNano activeNano;
            if (!recipient.ActiveNanos.TryGetValue(strain, out activeNano)
                || activeNano == null
                || activeNano.ID == nanoId)
            {
                return strain;
            }

            int key = -nanoId;
            while (recipient.ActiveNanos.ContainsKey(key))
            {
                key--;
            }

            return key;
        }

        private static void RefreshProjectedActiveNano(
            ICharacter recipient,
            OrdinaryEnemyTransientNanoEffectState state,
            int durationParameter)
        {
            IActiveNano activeNano;
            if (!recipient.ActiveNanos.TryGetValue(state.ActiveNanoKey, out activeNano)
                || activeNano == null
                || activeNano.ID != state.NanoId)
            {
                return;
            }

            activeNano.TickCounter = durationParameter;
            activeNano.TickInterval = durationParameter;
            var projected = activeNano as ActiveNanoState;
            if (projected != null)
            {
                projected.ExpiresAtUtc = state.ExpiresAtUtc;
                projected.DurationPacketIdentity = recipient.Identity;
                projected.DurationParameter1 = state.CasterInstance;
            }
        }

        private void RemoveAllTransientNanoEffects()
        {
            foreach (OrdinaryEnemyTransientNanoEffectState state in this.transientNanoEffectsByRecipient
                .SelectMany(value => value.Value.Values)
                .ToArray())
            {
                ICharacter recipient = this.dynelRegistry.FindByIdentity<ICharacter>(
                    state.RecipientIdentity);
                this.RemoveTransientNanoEffect(state, recipient);
            }
        }

        private void RemoveTransientNanoEffectsForRecipient(ICharacter recipient)
        {
            Dictionary<int, OrdinaryEnemyTransientNanoEffectState> recipientEffects;
            if (recipient == null
                || !this.transientNanoEffectsByRecipient.TryGetValue(
                    recipient.Identity.Instance,
                    out recipientEffects))
            {
                return;
            }

            foreach (OrdinaryEnemyTransientNanoEffectState state in recipientEffects.Values.ToArray())
            {
                this.RemoveTransientNanoEffect(state, recipient);
            }
        }

        private void RemoveTransientNanoEffectsForCaster(int casterInstance)
        {
            foreach (OrdinaryEnemyTransientNanoEffectState state in this.transientNanoEffectsByRecipient
                .SelectMany(value => value.Value.Values)
                .Where(value => value.CasterInstance == casterInstance)
                .ToArray())
            {
                ICharacter recipient = this.dynelRegistry.FindByIdentity<ICharacter>(
                    state.RecipientIdentity);
                this.RemoveTransientNanoEffect(state, recipient);
            }
        }

        private void RemoveTransientNanoEffect(
            OrdinaryEnemyTransientNanoEffectState state,
            ICharacter recipient)
        {
            if (state == null)
            {
                return;
            }

            if (recipient != null)
            {
                foreach (int statId in state.StatIds)
                {
                    recipient.Stats[statId].Modifier -= state.ModifierDelta;
                }

                IActiveNano activeNano;
                if (recipient.ActiveNanos.TryGetValue(state.ActiveNanoKey, out activeNano)
                    && activeNano != null
                    && activeNano.ID == state.NanoId)
                {
                    recipient.ActiveNanos.Remove(state.ActiveNanoKey);
                }
            }

            Dictionary<int, OrdinaryEnemyTransientNanoEffectState> recipientEffects;
            if (this.transientNanoEffectsByRecipient.TryGetValue(
                state.RecipientIdentity.Instance,
                out recipientEffects))
            {
                recipientEffects.Remove(state.NanoId);
                if (recipientEffects.Count == 0)
                {
                    this.transientNanoEffectsByRecipient.Remove(
                        state.RecipientIdentity.Instance);
                }
            }
        }

        private Character ConstructCharacter(
            Playfield playfield,
            Identity playfieldIdentity,
            OrdinaryEnemySpawnDefinition spawn,
            OrdinaryEnemySpawnVariant variant,
            OrdinaryEnemyProfile profile,
            NPCController controller)
        {
            Character character;
            if (profile.ConstructionMode == OrdinaryEnemyConstructionMode.TemplateBacked)
            {
                character = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                    profile.TemplateHash,
                    playfieldIdentity,
                    new Coordinate { x = spawn.X, y = spawn.Y, z = spawn.Z },
                    new AORebirth.Core.Vector.Quaternion(0, 0, 0, 1),
                    controller,
                    variant.Level);
            }
            else
            {
                int instance = Pool.Instance.GetFreeInstance<Character>(1000000, IdentityType.CanbeAffected);
                var identity = new Identity { Type = IdentityType.CanbeAffected, Instance = instance };
                character = new Character(playfieldIdentity, identity, controller);
                character.Read();
                controller.Character = character;
            }

            if (character == null)
            {
                return null;
            }

            character.Playfield = playfield;
            character.Name = profile.DisplayName;
            character.FirstName = string.Empty;
            character.LastName = string.Empty;
            character.Coordinates(new Coordinate { x = spawn.X, y = spawn.Y, z = spawn.Z });
            character.RawHeading =
                new AORebirth.Core.Vector.Quaternion(
                    spawn.HeadingX,
                    spawn.HeadingY,
                    spawn.HeadingZ,
                    spawn.HeadingW);
            return character;
        }

        private void ApplyMovement(
            Character character,
            NPCController controller,
            OrdinaryEnemySpawnDefinition spawn)
        {
            character.Waypoints.Clear();
            foreach (OrdinaryEnemyWaypoint waypoint in spawn.Waypoints)
            {
                character.AddWaypoint(
                    new AORebirth.Core.Vector.Vector3(waypoint.X, waypoint.Y, waypoint.Z),
                    false);
            }

            if (character.Waypoints.Count > 1)
            {
                controller.State = CharacterState.Patrolling;
            }

            if (!spawn.UseCapturedPatrolReplay)
            {
                return;
            }

            this.patrolReplay.AssignCapturedSubwayReplay(
                spawn.SourceIdentity,
                segments =>
                {
                    if (segments == null || segments.Length == 0)
                    {
                        return;
                    }

                    var start = spawn.UseSpawnAsPatrolStart
                        ? new AORebirth.Core.Vector.Vector3(spawn.X, spawn.Y, spawn.Z)
                        : new AORebirth.Core.Vector.Vector3(
                            segments[0].StartX,
                            segments[0].StartY,
                            segments[0].StartZ);
                    var end = new AORebirth.Core.Vector.Vector3(
                        segments[0].EndX,
                        segments[0].EndY,
                        segments[0].EndZ);
                    character.Coordinates(start);
                    character.Waypoints.Clear();
                    character.AddWaypoint(start, false);
                    character.AddWaypoint(end, false);
                    controller.SetCapturedPatrolReplaySegments(
                        segments,
                        false,
                        true,
                        spawn.UseSpawnAsPatrolStart);
                    controller.State = CharacterState.Patrolling;
                });
        }

        private static void ApplyStats(
            Character character,
            OrdinaryEnemySpawnVariant variant,
            OrdinaryEnemyProfile profile)
        {
            OrdinaryEnemyAppearanceProfile appearance = profile.Appearance;
            SetMobStat(character, StatIds.side, appearance.Side, profile.ConstructionMode);
            SetMobStat(character, StatIds.fatness, appearance.Fatness, profile.ConstructionMode);
            SetMobStat(character, StatIds.breed, appearance.Breed, profile.ConstructionMode);
            SetMobStat(character, StatIds.sex, appearance.Sex, profile.ConstructionMode);
            SetMobStat(character, StatIds.race, appearance.Race, profile.ConstructionMode);
            SetMobStat(character, StatIds.flags, appearance.CharacterFlags, profile.ConstructionMode);
            SetMobStat(character, StatIds.accountflags, appearance.AccountFlags, profile.ConstructionMode);
            SetMobStat(character, StatIds.expansion, appearance.Expansions, profile.ConstructionMode);
            SetMobStat(character, StatIds.npcfamily, appearance.NpcFamily, profile.ConstructionMode);
            SetMobStat(character, StatIds.losheight, appearance.NpcLosHeight, profile.ConstructionMode);
            SetMobStat(character, StatIds.monsterdata, profile.MonsterData, profile.ConstructionMode);
            SetMobStat(character, StatIds.monsterscale, variant.MonsterScale, profile.ConstructionMode);
            SetMobStat(character, StatIds.visualflags, appearance.VisualFlags, profile.ConstructionMode);
            SetMobStat(character, StatIds.currentmovementmode, (int)MoveModes.Run, profile.ConstructionMode);
            SetMobStat(character, StatIds.prevmovementmode, (int)MoveModes.Run, profile.ConstructionMode);
            SetMobStat(character, StatIds.runspeed, variant.RunSpeed, profile.ConstructionMode);
            SetMobStat(character, StatIds.profession, 1, profile.ConstructionMode);
            SetMobStat(character, StatIds.titlelevel, 1, profile.ConstructionMode);
            SetMobStat(character, StatIds.level, variant.Level, profile.ConstructionMode);
            SetMobStat(character, StatIds.life, variant.Health, profile.ConstructionMode);
            SetMobStat(
                character,
                StatIds.health,
                Math.Max(0, variant.Health - variant.HealthDamage),
                profile.ConstructionMode);
            int spawnNanoPool = profile.SupportNano == null
                ? 0
                : profile.SupportNano.ResolveSpawnNanoPool(variant.Level);
            if (spawnNanoPool > 0)
            {
                SetMobStat(
                    character,
                    StatIds.maxnanoenergy,
                    spawnNanoPool,
                    profile.ConstructionMode);
                SetMobStat(
                    character,
                    StatIds.currentnano,
                    spawnNanoPool,
                    profile.ConstructionMode);
            }

            if (profile.ConstructionMode == OrdinaryEnemyConstructionMode.CapturedDirect)
            {
                SetMobStat(character, StatIds.headmesh, appearance.HeadMesh, profile.ConstructionMode);
            }
        }

        private static void ApplyAppearance(Character character, OrdinaryEnemyProfile profile)
        {
            OrdinaryEnemyAppearanceProfile appearance = profile.Appearance;
            if (profile.ConstructionMode == OrdinaryEnemyConstructionMode.TemplateBacked)
            {
                if (appearance.HeadMesh > 0)
                {
                    SetHeadMesh(character, appearance.HeadMesh);
                }
                else if (appearance.ClearTemplateHeadWhenZero)
                {
                    character.MeshLayer.RemoveMesh(0, 0, 0, 4);
                    character.SocialMeshLayer.RemoveMesh(0, 0, 0, 4);
                }
            }

            if (appearance.ReplaceTextures)
            {
                character.Textures.Clear();
            }

            foreach (OrdinaryEnemyTextureProfile texture in appearance.Textures)
            {
                character.Textures.Add(new AOTextures(texture.Place, texture.Id));
            }

            foreach (OrdinaryEnemyMeshProfile mesh in appearance.Meshes)
            {
                character.MeshLayer.AddMesh(mesh.Position, (int)mesh.Id, mesh.OverrideTextureId, mesh.Layer);
                character.SocialMeshLayer.AddMesh(
                    mesh.Position,
                    (int)mesh.Id,
                    mesh.OverrideTextureId,
                    mesh.Layer);
            }
        }

        private static void SetHeadMesh(Character character, int headMesh)
        {
            int existingHeadMesh = character.Stats[StatIds.headmesh].Value;
            if (existingHeadMesh != 0 && existingHeadMesh != headMesh)
            {
                character.MeshLayer.RemoveMesh(0, existingHeadMesh, 0, 4);
                character.SocialMeshLayer.RemoveMesh(0, existingHeadMesh, 0, 4);
            }

            character.Stats[StatIds.headmesh].Value = headMesh;
            character.Stats[StatIds.headmesh].BaseValue = (uint)headMesh;
            character.MeshLayer.AddMesh(0, headMesh, 0, 4);
            character.SocialMeshLayer.AddMesh(0, headMesh, 0, 4);
        }

        private static void SetMobStat(
            ICharacter character,
            StatIds stat,
            int value,
            OrdinaryEnemyConstructionMode constructionMode)
        {
            if (constructionMode == OrdinaryEnemyConstructionMode.TemplateBacked)
            {
                character.Stats[stat].Value = value;
                character.Stats[stat].BaseValue = (uint)value;
                return;
            }

            character.Stats.SetBaseValueWithoutTriggering((int)stat, (uint)Math.Max(0, value));
        }

    }

    internal sealed class OrdinaryEnemySupportNanoRuntimeState
    {
        internal DateTime NextCastAtUtc { get; set; }
        internal bool CastInProgress { get; set; }
        internal Identity TargetIdentity { get; set; }
        internal DateTime FinishAtUtc { get; set; }
    }

    internal sealed class OrdinaryEnemyTransientNanoEffectState
    {
        internal Identity RecipientIdentity { get; set; }
        internal int NanoId { get; set; }
        internal int Strain { get; set; }
        internal int ModifierDelta { get; set; }
        internal int[] StatIds { get; set; }
        internal int CasterInstance { get; set; }
        internal int ActiveNanoKey { get; set; }
        internal DateTime ExpiresAtUtc { get; set; }
        internal int PeriodicStatId { get; set; }
        internal int PeriodicStatDelta { get; set; }
        internal OrdinaryEnemyPeriodicNanoSchedule PeriodicSchedule { get; set; }
    }

    internal sealed class OrdinaryEnemyRuntimeDefinition
    {
        internal OrdinaryEnemyRuntimeDefinition(
            OrdinaryEnemySpawnDefinition spawn,
            OrdinaryEnemyProfile profile,
            OrdinaryEnemySpawnGeneration spawnGeneration)
        {
            this.Spawn = spawn;
            this.Profile = profile;
            this.SpawnGeneration = spawnGeneration;
        }

        internal OrdinaryEnemySpawnDefinition Spawn { get; private set; }
        internal OrdinaryEnemyProfile Profile { get; private set; }
        internal OrdinaryEnemySpawnGeneration SpawnGeneration { get; private set; }
    }

    internal static class OrdinaryEnemyRuntimeRegistry
    {
        private static readonly object Sync = new object();

        private static readonly Dictionary<int, OrdinaryEnemyRuntimeDefinition> Definitions =
            new Dictionary<int, OrdinaryEnemyRuntimeDefinition>();

        internal static void Register(int serverInstance, OrdinaryEnemyRuntimeDefinition definition)
        {
            lock (Sync)
            {
                Definitions[serverInstance] = definition;
            }
        }

        internal static bool TryGet(int serverInstance, out OrdinaryEnemyRuntimeDefinition definition)
        {
            lock (Sync)
            {
                return Definitions.TryGetValue(serverInstance, out definition);
            }
        }

        internal static void Remove(int serverInstance)
        {
            lock (Sync)
            {
                Definitions.Remove(serverInstance);
            }
        }

        internal static void RemoveForPlayfield(int playfieldInstance)
        {
            lock (Sync)
            {
                foreach (int serverInstance in Definitions
                    .Where(value => value.Value.Spawn.PlayfieldInstance == playfieldInstance)
                    .Select(value => value.Key)
                    .ToArray())
                {
                    Definitions.Remove(serverInstance);
                }
            }
        }
    }
}
