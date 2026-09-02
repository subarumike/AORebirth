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
    using AORebirth.Core.Network;
    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Functions;
    using ZoneEngine.Core.MessageHandlers;

    using Utility;

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

        /// <summary>
        /// Reverse OnUse SetFlag bits when a nano leaves NCU (e.g. Overview of Nascence and
        /// Jobe 223767). Without this, MapsC stays set and Ctrl+5 PF map / red dots remain.
        /// </summary>
        public void ReverseOnUseSetFlags(ICharacter character, int nanoId)
        {
            if (character == null)
            {
                return;
            }

            NanoFormula nano;
            if (!NanoLoader.NanoList.TryGetValue(nanoId, out nano)
                || nano == null
                || nano.Events == null)
            {
                return;
            }

            foreach (Event nanoEvent in nano.Events.Where(x => x.EventType == EventType.OnUse))
            {
                if (nanoEvent.Functions == null)
                {
                    continue;
                }

                foreach (Function function in nanoEvent.Functions)
                {
                    if (function == null
                        || function.FunctionType != (int)FunctionType.SetFlag
                        || function.Arguments == null
                        || function.Arguments.Values == null
                        || function.Arguments.Values.Count < 2)
                    {
                        continue;
                    }

                    FunctionCollection.Instance.CallFunction(
                        (int)FunctionType.ClearFlag,
                        character,
                        character,
                        character,
                        function.Arguments.Values.ToArray());
                }
            }

            FlushChangedStats(character);
        }

        private static void FlushChangedStats(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            if (character.Controller != null)
            {
                character.Controller.SendChangedStats();
                return;
            }

            StatMessageHandler.Default.SendChanged(character);
        }

        /// <summary>
        /// Capture 20260830-110744: Overview of Nascence and Jobe (223767) gates Nascence/Jobe
        /// PF map (MapsC / mapareapart3). Client shows "Map Not Available" when MapsC==0;
        /// capture end profile MapsC=403669119 while the nano is in NCU.
        /// Live cast does not rely on MapsC Stat alone (client SetFlag on NCU add); stuck unlock
        /// is cleared by FullCharacter MapsC=0 + immediate SendCompressed + SQL persist.
        /// </summary>
        public void SyncOverviewMapFlags(ICharacter character, bool pushWire = true)
        {
            if (character == null)
            {
                return;
            }

            const int overviewOfNascenceAndJobeNanoId = 223767;
            const uint overviewMapsC = 403669119u;

            bool hasOverview = character.ActiveNanos != null
                && character.ActiveNanos.Values.Any(
                    x => x != null && x.ID == overviewOfNascenceAndJobeNanoId);

            uint desired = hasOverview ? overviewMapsC : 0u;
            int mapsCStatId = (int)StatIds.mapareapart3;

            // Capture 20260830-124309: client ClearFlag MapsC needs Buff remove when Overview
            // truly leaves NCU. That path is CompleteFriendlyNanoRemoval / SendRemoveNanoBuff
            // from RemoveActiveNanoByStrain (user cancel / expiry).
            // Do NOT Buff-remove from this sync: PrepareCharacterForLogin + ClientConnected call
            // SyncOverviewMapFlags while ActiveNanos are empty mid-zone, before delayed restore.
            // That spammed "Nanoprogram Overview of Nascence and Jobe terminated..." and bogus
            // XP/level chat on every playfield hop. MapsC StatMessage below is enough for the
            // interim "Map Not Available" until Overview is restored into NCU.

            // .Set updates BaseValue the same way ClearFlag/SetFlag do; SetBaseValue alone left
            // stale Values that later Write() persisted as unlocked MapsC.
            character.Stats[mapsCStatId].Set(desired);
            character.Stats[mapsCStatId].Changed = true;
            try
            {
                PersistMapsC(character, desired);
            }
            catch (Exception)
            {
                // SQL persist must not abort FullCharacter MapsC=0.
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    "MAPSC_SYNC char={0} hasOverview={1} mapsC={2} pushWire={3} activeCount={4}",
                    character.Identity.Instance,
                    hasOverview,
                    desired,
                    pushWire,
                    character.ActiveNanos != null ? character.ActiveNanos.Count : 0));
            Console.WriteLine(
                "MAPSC_SYNC char={0} hasOverview={1} mapsC={2} pushWire={3} buffClear=0",
                character.Identity.Instance,
                hasOverview,
                desired,
                pushWire);

            if (!pushWire)
            {
                return;
            }

            IZoneClient client = character.Controller != null ? character.Controller.Client : null;
            if (client != null)
            {
                // Same immediate SendCompressed pattern as XP-bar LastSaveXP (not buffered Send).
                client.SendCompressed(
                    new StatMessage
                    {
                        Identity = character.Identity,
                        Unknown = 0,
                        Stats =
                            new[]
                            {
                                new GameTuple<CharacterStat, uint>
                                {
                                    Value1 = CharacterStat.MapsC,
                                    Value2 = desired
                                }
                            }
                    });
            }
        }

        private static void PersistMapsC(ICharacter character, uint mapsC)
        {
            if (character == null)
            {
                return;
            }

            int characterId = character.Identity.Instance;
            DBStats stat = StatDao.Instance
                .GetAll(new { Type = 50000, Instance = characterId, StatId = (int)StatIds.mapareapart3 })
                .FirstOrDefault();

            if (stat == null)
            {
                StatDao.Instance.Add(
                    new DBStats
                    {
                        Type = 50000,
                        Instance = characterId,
                        StatId = (int)StatIds.mapareapart3,
                        StatValue = (int)mapsC
                    });
                return;
            }

            if (stat.StatValue == (int)mapsC)
            {
                return;
            }

            stat.StatValue = (int)mapsC;
            StatDao.Instance.Save(stat);
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

        public static bool TryResolveFinishNanoCastingParameter(
            NanoLandingResult landingResult,
            out int parameter)
        {
            switch (landingResult)
            {
                case NanoLandingResult.NotRequired:
                case NanoLandingResult.Landed:
                    parameter = 1;
                    return true;
                case NanoLandingResult.Resisted:
                    parameter = 3;
                    return true;
                default:
                    parameter = 0;
                    return false;
            }
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
            // Capture 20260806-pet-warp: nano 209488 warps pets; it is not a summon strain.
            if (nanoId == PetCommandService.WarpPetsNanoId)
            {
                return false;
            }

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

            if (nano.ID == PetCommandService.WarpPetsNanoId)
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
                    if (function == null)
                    {
                        continue;
                    }

                    if (function.FunctionType == SummonPetFunctionId)
                    {
                        return true;
                    }

                    if (function.FunctionType == SummonPetsFunctionId
                        && !IsPetWarpSummonPetsFunction(function))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Capture 20260806-pet-warp: SummonPets [0] warps living pets to the caster.
        /// </summary>
        private static bool IsPetWarpSummonPetsFunction(Function function)
        {
            if (function == null
                || function.Arguments == null
                || function.Arguments.Values == null
                || function.Arguments.Values.Count != 1)
            {
                return false;
            }

            try
            {
                return function.Arguments.Values[0].AsInt32() == 0;
            }
            catch
            {
                return false;
            }
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
