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
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Events;
    using AORebirth.Core.Functions;
    using AORebirth.Core.Nanos;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Functions;

    #endregion

    public sealed class NanoEventRuntimeService
    {
        private static readonly int SummonPetFunctionId = (int)FunctionType.SummonPet;
        private static readonly int SummonPetsFunctionId = (int)FunctionType.SummonPets;

        private static readonly NanoEventRuntimeService DefaultInstance = new NanoEventRuntimeService();

        private readonly object modifierSync = new object();
        private readonly Dictionary<ModifierKey, List<AppliedModifier>> appliedModifiers =
            new Dictionary<ModifierKey, List<AppliedModifier>>();

        [ThreadStatic]
        private static NanoExecutionContext currentExecution;

        private NanoEventRuntimeService()
        {
        }

        public static NanoEventRuntimeService Default
        {
            get { return DefaultInstance; }
        }

        public void ExecuteOnUseEvents(ICharacter character, NanoFormula nano)
        {
            if (character == null || nano == null || nano.Events == null)
            {
                return;
            }

            NanoExecutionContext previous = currentExecution;
            currentExecution = new NanoExecutionContext(character.Identity.Instance, nano.ID);
            try
            {
                foreach (Event nanoEvent in nano.Events.Where(x => x.EventType == EventType.OnUse))
                {
                    nanoEvent.Perform(character, character);
                }
            }
            finally
            {
                currentExecution = previous;
            }
        }

        public bool ExecuteCapturedOnUseEvents(
            ICharacter caster,
            ICharacter target,
            NanoFormula nano,
            NanoLandingResult landingResult)
        {
            if (caster == null || target == null || nano == null || nano.Events == null)
            {
                return false;
            }

            if (nano.Defend != null && nano.Defend.Count > 0)
            {
                if (landingResult == NanoLandingResult.Unresolved)
                {
                    return false;
                }

                if (landingResult == NanoLandingResult.Resisted)
                {
                    return true;
                }
            }

            foreach (Function function in nano.Events
                .Where(x => x.EventType == EventType.OnUse && x.Functions != null)
                .SelectMany(x => x.Functions))
            {
                if (function == null
                    || function.FunctionType == (int)FunctionType.HeadText)
                {
                    continue;
                }

                if (function.TickCount > 1
                    || function.TickInterval > 0
                    || FunctionCollection.Instance.GetFunctionByNumber(function.FunctionType) == null)
                {
                    return false;
                }
            }

            NanoExecutionContext previous = currentExecution;
            currentExecution = new NanoExecutionContext(caster.Identity.Instance, nano.ID);
            try
            {
                foreach (Event nanoEvent in nano.Events.Where(x => x.EventType == EventType.OnUse))
                {
                    if (nanoEvent.Functions == null)
                    {
                        continue;
                    }

                    foreach (Function function in nanoEvent.Functions)
                    {
                        if (function == null)
                        {
                            continue;
                        }

                        // HeadText is a presentation instruction, not gameplay state.
                        // Captured encounter chat ownership is separate from nano effects.
                        if (function.FunctionType == (int)FunctionType.HeadText)
                        {
                            continue;
                        }

                        if (!FunctionCollection.Instance.CallFunction(
                                function.FunctionType,
                                caster,
                                caster,
                                target,
                                function.Arguments.Values.ToArray()))
                        {
                            return false;
                        }
                    }
                }
            }
            finally
            {
                currentExecution = previous;
            }

            return true;
        }

        public void RecordModifier(
            Character target,
            int statId,
            int delta,
            bool percentage)
        {
            NanoExecutionContext execution = currentExecution;
            if (target == null || execution == null || delta == 0)
            {
                return;
            }

            var key = new ModifierKey(target.Identity.Instance, execution.NanoId);
            lock (this.modifierSync)
            {
                if (execution.PreparedTargets.Add(target.Identity.Instance))
                {
                    this.RemoveModifiersLocked(key, target);
                }

                List<AppliedModifier> modifiers;
                if (!this.appliedModifiers.TryGetValue(key, out modifiers))
                {
                    modifiers = new List<AppliedModifier>();
                    this.appliedModifiers[key] = modifiers;
                }

                modifiers.Add(
                    new AppliedModifier(
                        execution.CasterIdentityInstance,
                        target,
                        statId,
                        delta,
                        percentage));
            }
        }

        public void RemoveModifiers(ICharacter target, int nanoId)
        {
            Character character = target as Character;
            if (character == null)
            {
                return;
            }

            lock (this.modifierSync)
            {
                this.RemoveModifiersLocked(
                    new ModifierKey(character.Identity.Instance, nanoId),
                    character);
            }
        }

        public void RemoveAllModifiers(ICharacter target)
        {
            Character character = target as Character;
            if (character == null)
            {
                return;
            }

            lock (this.modifierSync)
            {
                foreach (ModifierKey key in this.appliedModifiers.Keys
                    .Where(x => x.TargetIdentityInstance == character.Identity.Instance)
                    .ToList())
                {
                    this.RemoveModifiersLocked(key, character);
                }
            }
        }

        public void RemoveModifiersCastBy(int casterIdentityInstance)
        {
            if (casterIdentityInstance == 0)
            {
                return;
            }

            lock (this.modifierSync)
            {
                foreach (ModifierKey key in this.appliedModifiers
                    .Where(x => x.Value.Any(y => y.CasterIdentityInstance == casterIdentityInstance))
                    .Select(x => x.Key)
                    .ToList())
                {
                    Character target = this.appliedModifiers[key]
                        .Select(x => x.Target)
                        .FirstOrDefault(x => x != null);
                    if (target != null)
                    {
                        this.RemoveModifiersLocked(key, target);
                    }
                    else
                    {
                        this.appliedModifiers.Remove(key);
                    }
                }
            }
        }

        public int ActiveModifierCount
        {
            get
            {
                lock (this.modifierSync)
                {
                    return this.appliedModifiers.Values.Sum(x => x.Count);
                }
            }
        }

        private void RemoveModifiersLocked(ModifierKey key, Character target)
        {
            List<AppliedModifier> modifiers;
            if (!this.appliedModifiers.TryGetValue(key, out modifiers))
            {
                return;
            }

            foreach (AppliedModifier modifier in modifiers)
            {
                if (modifier.Percentage)
                {
                    target.Stats[modifier.StatId].PercentageModifier -= modifier.Delta;
                }
                else
                {
                    target.Stats[modifier.StatId].Modifier -= modifier.Delta;
                }
            }

            this.appliedModifiers.Remove(key);
        }

        public bool HasSummonPetOnUse(int nanoId)
        {
            if (PetSummonNanoCatalog.IsCatalogSummonNano(nanoId))
            {
                return true;
            }

            NanoFormula nano;
            if (!NanoLoader.NanoList.TryGetValue(nanoId, out nano))
            {
                return false;
            }

            return this.HasSummonPetOnUse(nano);
        }

        public bool HasSummonPetOnUse(NanoFormula nano)
        {
            if (nano == null || nano.Events == null)
            {
                return false;
            }

            foreach (Event nanoEvent in nano.Events.Where(x => x.EventType == EventType.OnUse))
            {
                if (nanoEvent.Functions == null)
                {
                    continue;
                }

                foreach (Function function in nanoEvent.Functions)
                {
                    if (function.FunctionType == SummonPetFunctionId
                        || function.FunctionType == SummonPetsFunctionId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool HasOffensiveHitOnUse(NanoFormula nano)
        {
            if (nano == null || nano.Events == null)
            {
                return false;
            }

            int hitFunctionId = (int)FunctionType.Hit;
            foreach (Event nanoEvent in nano.Events.Where(x => x.EventType == EventType.OnUse))
            {
                if (nanoEvent.Functions == null)
                {
                    continue;
                }

                foreach (Function function in nanoEvent.Functions)
                {
                    if (function.FunctionType != hitFunctionId
                        || function.Arguments == null
                        || function.Arguments.Values.Count < 2)
                    {
                        continue;
                    }

                    int amount = function.Arguments.Values[1].AsInt32();
                    if (amount < 0)
                    {
                        return true;
                    }

                    if (function.Arguments.Values.Count >= 3
                        && function.Arguments.Values[2].AsInt32() < 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private sealed class NanoExecutionContext
        {
            internal NanoExecutionContext(int casterIdentityInstance, int nanoId)
            {
                this.CasterIdentityInstance = casterIdentityInstance;
                this.NanoId = nanoId;
                this.PreparedTargets = new HashSet<int>();
            }

            internal int CasterIdentityInstance { get; private set; }

            internal int NanoId { get; private set; }

            internal HashSet<int> PreparedTargets { get; private set; }
        }

        private sealed class AppliedModifier
        {
            internal AppliedModifier(
                int casterIdentityInstance,
                Character target,
                int statId,
                int delta,
                bool percentage)
            {
                this.CasterIdentityInstance = casterIdentityInstance;
                this.Target = target;
                this.StatId = statId;
                this.Delta = delta;
                this.Percentage = percentage;
            }

            internal int CasterIdentityInstance { get; private set; }

            internal Character Target { get; private set; }

            internal int StatId { get; private set; }

            internal int Delta { get; private set; }

            internal bool Percentage { get; private set; }
        }

        private struct ModifierKey : IEquatable<ModifierKey>
        {
            internal ModifierKey(int targetIdentityInstance, int nanoId)
            {
                this.TargetIdentityInstance = targetIdentityInstance;
                this.NanoId = nanoId;
            }

            internal int TargetIdentityInstance { get; private set; }

            internal int NanoId { get; private set; }

            public bool Equals(ModifierKey other)
            {
                return this.TargetIdentityInstance == other.TargetIdentityInstance
                       && this.NanoId == other.NanoId;
            }

            public override bool Equals(object value)
            {
                return value is ModifierKey && this.Equals((ModifierKey)value);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (this.TargetIdentityInstance * 397) ^ this.NanoId;
                }
            }
        }
    }

    public enum NanoLandingResult
    {
        Unresolved,
        NotRequired,
        Landed,
        Resisted
    }
}
