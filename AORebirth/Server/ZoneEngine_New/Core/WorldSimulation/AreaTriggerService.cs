namespace ZoneEngine_New.Core.WorldSimulation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;

    using PlayfieldType = ZoneEngine_New.Core.Playfield.Playfield;

    /// <summary>
    /// Playfield-owned SimpleItem signals and authored object actions. Signal masks are not Flags.
    /// Native queue overwrite, entry-pending transitions and odd-pulse timers are kept separate
    /// from the explicitly classified server compatibility policies in the content catalog.
    /// </summary>
    public sealed class AreaTriggerService : IDisposable
    {
        readonly PlayfieldType _playfield;
        readonly DynelRegistry _registry;
        readonly StaticObjectStateMachineCatalog _catalog;
        readonly IZoneLogger _logger;
        readonly Dictionary<Dynel, ObjectRuntime> _objects = new();
        double _time;
        double _lastPulse;
        bool _nextPulseOdd = true;
        bool _initialized;
        bool _disposed;

        public AreaTriggerService(PlayfieldType playfield, DynelRegistry registry,
            StaticObjectStateMachineCatalog catalog, IZoneLogger logger)
        {
            _playfield = playfield;
            _registry = registry;
            _catalog = catalog;
            _logger = logger;
        }

        public void Register(StaticDynel owner)
        {
            if (_disposed)
                return;
            ObjectRuntime runtime = Get(owner);
            if (_initialized)
            {
                Initialize(runtime);
            }
        }

        public void Initialize()
        {
            if (_disposed || _initialized)
                return;
            _initialized = true;
            // Initializers may emit signals. All placed objects must already be registered.
            foreach (ObjectRuntime runtime in _objects.Values.ToArray())
            {
                Initialize(runtime);
            }
        }

        void Initialize(ObjectRuntime runtime)
        {
            if (runtime.Initialized)
                return;
            runtime.Initialized = true;
            int resource = runtime.Owner.Stats.GetOrZero(CharacterStat.StateMachine);
            if (resource == 0)
                return;
            if (!_catalog.TryGet(resource, out StaticObjectStateMachine machine))
            {
                _logger.Warn($"Static object {runtime.Owner.Identity}: state-machine resource {resource} is unavailable.");
                return;
            }

            runtime.Machine = machine;
            if (machine.States.TryGetValue(0, out StaticObjectState? initializer))
            {
                RunEntry(runtime, initializer);
            }
            runtime.Owner.Stats.Set(CharacterStat.State, machine.InitialState);
            runtime.EntryPending = true;
        }

        public bool IsUseEnabled(StaticDynel owner)
            => !_disposed && (!_objects.TryGetValue(owner, out ObjectRuntime? runtime) || runtime.UseEnabled);

        public bool BeginUse(StaticDynel owner, Player user)
        {
            if (_disposed || !IsCurrent(owner) || !IsCurrent(user) || !IsUseEnabled(owner)
                || user.Session?.State != SessionState.InPlay)
                return false;

            ObjectRuntime runtime = Get(owner);
            var context = new AreaTriggerContext(owner, user, user, user, user.Session);
            runtime.Context = context;
            if (!owner.Template.SpellList.TryGetValue(EventType.OnUse, out List<ItemSpell>? rows))
                return true;

            // Leave unrelated static item behavior on its existing execution path. Only authored
            // trigger lists and bound state machines need the native retained-action scheduler.
            if (runtime.Machine == null && !rows.Any(IsAreaTrigger))
                return owner.Template.ExecuteOnUseSpells(user,
                    _playfield.GetRequiredService<IInventoryRepository>(),
                    _playfield.GetRequiredService<IItemBuilder>());

            return ApplyRows(rows, context);
        }

        /// <summary>Dispatches an already selected action; callers own its scheduling/requirements.</summary>
        public bool TryExecute(ItemSpell spell, AreaTriggerContext context)
            => ExecuteTrigger(spell, context, allowDetachedSource: false);

        bool ExecuteTrigger(ItemSpell spell, AreaTriggerContext context, bool allowDetachedSource)
        {
            if (_disposed || !IsAreaTrigger(spell) || (!IsCurrent(context.ExecutionTarget) && !allowDetachedSource)
                || !ReferenceEquals(context.Playfield, _playfield))
                return false;

            float x = context.ExecutionTarget.Position.xf;
            float y = context.ExecutionTarget.Position.yf;
            float z = context.ExecutionTarget.Position.zf;
            int offset = 0;
            if (spell.Is(FunctionType.RemoteAreaTrigger))
            {
                if (!spell.TryReadInt(0, out int ix) || !spell.TryReadInt(1, out int iy)
                    || !spell.TryReadInt(2, out int iz))
                    return false;
                x = ix;
                y = iy;
                z = iz;
                offset = 3;
            }
            if (!spell.TryReadInt(offset, out int mask) || !spell.TryReadInt(offset + 1, out int rawRadius))
                return false;
            float radius = rawRadius <= 0 ? 1 : rawRadius;
            float radiusSquared = radius * radius;

            // Registry order is the playfield's existing stable enumeration order. Characters are
            // deliberately excluded: native candidates must downcast to SimpleItem, not Beholder.
            foreach (Dynel candidate in _registry.Dynels())
            {
                if (!IsSimpleItem(candidate) || !IsCurrent(candidate))
                    continue;
                float dx = candidate.Position.xf - x;
                float dy = candidate.Position.yf - y;
                float dz = candidate.Position.zf - z;
                float distanceSquared = (dx * dx + dy * dy) + dz * dz;
                if (!(distanceSquared < radiusSquared))
                    continue;
                var signalContext = context with
                {
                    Owner = candidate,
                    Source = context.ExecutionTarget,
                    Target = null,
                    ExecutionTarget = candidate
                };
                Get(candidate).Signals.Enqueue(new Signal(mask, signalContext));
            }
            return true;
        }

        public void Tick(double deltaTime)
        {
            if (_disposed || !double.IsFinite(deltaTime) || deltaTime < 0)
                return;
            _time += (float)deltaTime;
            bool oddPulse = false;
            if (_time - _lastPulse >= 1.0)
            {
                _lastPulse = _time;
                oddPulse = _nextPulseOdd;
                _nextPulseOdd = !_nextPulseOdd;
            }

            foreach (ObjectRuntime runtime in _objects.Values.ToArray())
            {
                if (!IsCurrent(runtime.Owner))
                {
                    // Server compatibility: a retained, static-owned Remote action has an authored
                    // world location. Complete it on its original playfield after the source player
                    // leaves; never redirect its feedback or ordinary effects to another session.
                    runtime.Retained.RemoveAll(action => !CanCompleteWorldAction(action.Row, action.Context));
                    if (oddPulse)
                    {
                        PumpRetained(runtime);
                    }
                    if (runtime.Retained.Count == 0)
                    {
                        _objects.Remove(runtime.Owner);
                    }
                    continue;
                }
                if (_initialized && !runtime.Initialized)
                {
                    Initialize(runtime);
                }

                // Native SimpleItem Run pumps existing timers/actions before consuming signals.
                // Newly entered state rows are absent from this owner's already completed pump.
                if (oddPulse)
                {
                    PumpSignalTimers(runtime);
                    PumpRetained(runtime);
                }

                // Native SimpleItem Run drains the signal queue, then evaluates its machine once.
                // The opt-in serialization policy preserves separate simultaneous activations over
                // separate Runs; it never evaluates the machine once per signal within one Run.
                int take = runtime.Machine?.SerializeQueuedSignals == true
                    ? Math.Min(1, runtime.Signals.Count) : runtime.Signals.Count;
                for (int i = 0; i < take; i++)
                {
                    Signal signal = runtime.Signals.Dequeue();
                    runtime.Context = signal.Context;
                    int mask = signal.Mask;
                    if ((mask & 2) != 0)
                    {
                        if (runtime.Owner is StaticDynel placed
                            && placed.Template.SpellList.TryGetValue(EventType.OnUse, out List<ItemSpell>? rows))
                        {
                            ApplyRows(rows, signal.Context);
                        }
                        mask &= ~2;
                    }
                    if (mask != 0)
                    {
                        runtime.SignalMask = mask; // Native assignment, not accumulation.
                        ApplySignalPolicy(runtime, mask);
                    }
                }

                RunMachine(runtime);
            }
        }

        void RunMachine(ObjectRuntime runtime)
        {
            if (runtime.Machine == null
                || !runtime.Machine.States.TryGetValue(runtime.Owner.Stats.GetOrZero(CharacterStat.State), out StaticObjectState? state))
                return;
            if (runtime.EntryPending)
            {
                runtime.EntryPending = false;
                RunEntry(runtime, state);
            }
            foreach (StaticObjectTransition transition in state.Transitions)
            {
                if (!MeetsConditions(runtime, transition.Criteria))
                    continue;
                if (transition.TargetState != runtime.Owner.Stats.GetOrZero(CharacterStat.State))
                {
                    runtime.Owner.Stats.Set(CharacterStat.State, transition.TargetState, dirty: true);
                    runtime.Owner.FlushDirtyStats();
                    runtime.EntryPending = true;
                }
                break;
            }
        }

        void RunEntry(ObjectRuntime runtime, StaticObjectState state)
        {
            ApplyRows(state.EntrySpells, runtime.Context);
            foreach (StaticObjectSignalTimer timer in state.SignalTimers)
            {
                if (MeetsConditions(runtime, timer.Criteria))
                {
                    runtime.Timers.Add(new SignalTimer(timer.SignalMask, timer.OddPulseCount));
                }
            }
        }

        void PumpSignalTimers(ObjectRuntime runtime)
        {
            foreach (SignalTimer timer in runtime.Timers.ToArray())
            {
                timer.Remaining -= 1;
                if (timer.Remaining > 0)
                    continue;
                // Supported54001 records have no addressed target/auxiliary identity. Native timer
                // fallback reads the owner's current source at firing, not its source on entry.
                runtime.Signals.Enqueue(new Signal(timer.Mask,
                    runtime.Context with { Target = null, ExecutionTarget = runtime.Owner }));
                runtime.Timers.Remove(timer);
            }
        }

        bool ApplyRows(IReadOnlyList<ItemSpell> rows, AreaTriggerContext context)
        {
            bool admitted = rows.Count == 0;
            foreach (ItemSpell row in rows)
            {
                if (!TryResolveTarget(row, context, out AreaTriggerContext selected))
                    break; // Native unresolved source/supplied target terminates this source list.
                int interval = unchecked((int)row.TickInterval);
                if (row.TickCount is 0 or -1 || interval == int.MinValue)
                    continue;
                var action = new RetainedAction(row.Copy(), selected, _time);
                if (row.TickCount > 1 || interval != 0)
                {
                    Get(selected.ExecutionTarget).Retained.Add(action);
                    admitted = true;
                }
                else
                {
                    admitted |= ExecuteDue(action);
                }
            }
            return admitted;
        }

        bool TryResolveTarget(ItemSpell row, AreaTriggerContext context, out AreaTriggerContext selected)
        {
            Dynel? target = row.Target switch
            {
                0 or 1 => context.Owner,
                2 => context.Source,
                3 => context.Target,
                _ => null // Native remote chains (14/23) need their own proven resolver.
            };
            selected = context;
            if (target == null || !IsCurrent(target))
                return false;
            if (target is Player player && (!context.HasCurrentInvoker || !ReferenceEquals(player, context.Invoker)))
                return false;
            selected = context with { ExecutionTarget = target };
            return true;
        }

        void PumpRetained(ObjectRuntime runtime)
        {
            foreach (RetainedAction action in runtime.Retained.ToArray())
            {
                ExecuteDue(action);
            }
            runtime.Retained.RemoveAll(action => action.Remaining == 0);
        }

        bool ExecuteDue(RetainedAction action)
        {
            if (action.Remaining == 0)
                return false;
            bool detachedWorldAction = CanCompleteWorldAction(action.Row, action.Context);
            if ((!IsCurrent(action.Context.ExecutionTarget)
                || (action.Context.ExecutionTarget is Player && !action.Context.HasCurrentInvoker))
                && !detachedWorldAction)
            {
                action.Remaining = 0;
                return false;
            }
            int interval = unchecked((int)action.Row.TickInterval);
            if (action.LastTime == _time && interval > 0 && action.Remaining > 1)
            {
                action.LastTime -= interval / 100;
            }
            if (Math.Truncate((_time - action.LastTime) * 100) < Math.Abs(interval))
                return false;

            if (!MeetsRequirements(action.Row, action.Context))
            {
                action.Remaining--;
                return false;
            }
            action.LastTime = _time;
            if (action.Remaining > 0)
            {
                action.Remaining--;
            }
            return Execute(action.Row, action.Context);
        }

        bool MeetsRequirements(ItemSpell row, AreaTriggerContext context)
        {
            ObjectRuntime runtime = Get(context.Owner);
            RequirementSubject Subject(Dynel subject) => new(
                stat => subject.Stats.GetOrZero(stat),
                requirement => requirement.Operator == 20
                    ? ConsumeSignal(runtime, requirement.StatNumber)
                    : subject is Character character ? character.ResolveRequirement(requirement) : null);
            return ItemTemplate.MeetsRequirements(row.Requirements,
                Subject(context.ExecutionTarget), Subject(context.Source),
                context.Target != null ? Subject(context.Target) : null);
        }

        bool Execute(ItemSpell row, AreaTriggerContext context)
        {
            if (IsAreaTrigger(row))
                return ExecuteTrigger(row, context, CanCompleteWorldAction(row, context));
            Dynel target = context.ExecutionTarget;
            if (target is not Character)
            {
                switch ((FunctionType)row.FunctionType)
                {
                    case FunctionType.Set:
                        if (!row.TryReadInt(0, out int statId) || !row.TryReadInt(1, out int value))
                            return false;
                        WriteStat(target, (CharacterStat)statId, value);
                        return true;
                    case FunctionType.SetFlag:
                    case FunctionType.ClearFlag:
                    case FunctionType.ToggleFlag:
                        if (!row.TryReadInt(0, out int flagStatId) || !row.TryReadInt(1, out int bit)
                            || bit < 0 || bit > 31)
                            return false;
                        CharacterStat stat = (CharacterStat)flagStatId;
                        int old = target.Stats.GetOrZero(stat);
                        int flags = row.Is(FunctionType.SetFlag) ? old | (1 << bit)
                            : row.Is(FunctionType.ClearFlag) ? old & ~(1 << bit) : old ^ (1 << bit);
                        WriteStat(target, stat, flags);
                        return true;
                    case FunctionType.Mesh:
                        if (!row.TryReadMesh(out int mesh, out int texture) || texture != 0)
                            return false;
                        target.Stats.Set(CharacterStat.Mesh, mesh, dirty: true);
                        target.FlushDirtyStats();
                        return true;
                    case FunctionType.Name:
                        if (target is not StaticDynel named || !row.TryReadString(0, out string name))
                            return false;
                        named.RuntimeName = name;
                        _playfield.GetService<PlayfieldLocality>()?.Announce(named, named.BuildSpawnMessage(), includeSelf: true);
                        return true;
                }
            }

            // Static world actions above remain valid after the user leaves. Feedback and ordinary
            // player effects require the exact original session, never a replacement login.
            if (context.Owner is StaticDynel owner && context.HasCurrentInvoker)
                return owner.Template.ExecuteStaticSpell(row, context.Invoker!,
                    new SpellCriteria { TriggerContext = context }, requirementsAlreadyChecked: true);
            return false;
        }

        void WriteStat(Dynel target, CharacterStat stat, int value)
        {
            if (stat == CharacterStat.Flags)
            {
                ApplyFlags(target, value);
                return;
            }
            target.Stats.Set(stat, value, dirty: true);
            if (stat == CharacterStat.StateAction && value is 100 or 102
                && (target is Door || target.Identity.Type == IdentityType.Door))
            {
                int flags = target.Stats.GetOrZero(CharacterStat.Flags);
                ApplyFlags(target, value == 100 ? flags | 0x80 : flags & ~0x80);
            }
            if (stat == CharacterStat.State && _objects.TryGetValue(target, out ObjectRuntime? runtime))
            {
                runtime.EntryPending = true;
            }
            target.FlushDirtyStats();
        }

        void ApplyFlags(Dynel target, int flags, bool? closeInhibit = null)
        {
            if (_playfield.WorldAccess.Instance?.ApplyDoorFlags(_playfield, target, flags, closeInhibit) == true)
                return;
            target.Stats.Set(CharacterStat.Flags, flags, dirty: true);
            target.FlushDirtyStats();
        }

        void ApplySignalPolicy(ObjectRuntime runtime, int mask)
        {
            if (!_catalog.SignalPolicies.TryGetValue(mask, out StaticObjectSignalPolicy? policy))
                return;
            if (policy.UseEnabled is bool enabled)
            {
                runtime.UseEnabled = enabled;
            }
            if (runtime.Owner is not Door && runtime.Owner.Identity.Type != IdentityType.Door)
                return;
            int flags = runtime.Owner.Stats.GetOrZero(CharacterStat.Flags);
            bool? closeInhibit = null;
            foreach (StaticObjectDoorOperation operation in policy.DoorOperations)
            {
                switch (operation)
                {
                    case StaticObjectDoorOperation.Unlock:
                        flags &= ~0x40;
                        break;
                    case StaticObjectDoorOperation.Open:
                        runtime.Owner.Stats.Set(CharacterStat.StateAction, 100, dirty: true);
                        flags |= 0x80;
                        closeInhibit = true;
                        break;
                    case StaticObjectDoorOperation.Close:
                        runtime.Owner.Stats.Set(CharacterStat.StateAction, 102, dirty: true);
                        // Native false does not clear the client's hold-open latch. Retain it;
                        // the following authoritative Lock operation overrides the latch.
                        flags &= ~0x80;
                        break;
                    case StaticObjectDoorOperation.Lock:
                        flags |= 0x40;
                        break;
                }
            }
            if (policy.DoorOperations.Count != 0)
            {
                // A compatibility operation publishes its resulting door state once, not an
                // intermediate unlocked/closed or open/locked state between its ordered steps.
                ApplyFlags(runtime.Owner, flags, closeInhibit);
            }
            runtime.Owner.FlushDirtyStats();
        }

        static bool MeetsConditions(ObjectRuntime runtime, IReadOnlyList<StaticObjectCondition> conditions)
        {
            if (conditions.Count == 0)
                return true;
            // Validate the postfix shape before touching consumable leaves. There is no fallback
            // evaluation: a failed later leaf must not restore or consume a signal twice.
            int depth = 0;
            foreach (StaticObjectCondition condition in conditions)
            {
                if (condition.Operator is 3 or 4)
                {
                    if (depth < 2)
                        return false;
                    depth--;
                }
                else if (condition.Operator is 0 or 20)
                {
                    depth++;
                }
                else
                {
                    return false;
                }
            }
            if (depth != 1)
                return false;
            var values = new Stack<bool>();
            foreach (StaticObjectCondition condition in conditions)
            {
                if (condition.Operator is 3 or 4)
                {
                    bool right = values.Pop();
                    bool left = values.Pop();
                    values.Push(condition.Operator == 3 ? left | right : left & right);
                }
                else if (condition.Operator == 20)
                {
                    values.Push(ConsumeSignal(runtime, condition.Value1));
                }
                else
                {
                    int actual = runtime.Owner.Stats.GetOrZero((CharacterStat)condition.Value1);
                    values.Push(actual == condition.Value2);
                }
            }
            return values.Pop();
        }

        static bool ConsumeSignal(ObjectRuntime runtime, int mask)
        {
            if ((runtime.SignalMask & mask) == 0)
                return false;
            runtime.SignalMask &= ~mask;
            return true;
        }

        bool IsCurrent(Dynel dynel) => ReferenceEquals(dynel.Playfield, _playfield)
            && _registry.TryGet(dynel.Identity, out Dynel? current) && ReferenceEquals(current, dynel);

        bool CanCompleteWorldAction(ItemSpell row, AreaTriggerContext context)
            => row.Is(FunctionType.RemoteAreaTrigger) && context.Owner is StaticDynel
                && context.ExecutionTarget is Player && IsCurrent(context.Owner)
                && ReferenceEquals(context.Playfield, _playfield);

        static bool IsAreaTrigger(ItemSpell row)
            => row.Is(FunctionType.AreaTrigger) || row.Is(FunctionType.RemoteAreaTrigger);

        static bool IsSimpleItem(Dynel dynel) => dynel is StaticDynel or Door or LootableDynel;

        ObjectRuntime Get(Dynel owner)
        {
            if (!_objects.TryGetValue(owner, out ObjectRuntime? runtime))
            {
                runtime = new ObjectRuntime(owner);
                _objects.Add(owner, runtime);
            }
            return runtime;
        }

        public void Dispose()
        {
            _disposed = true;
            _objects.Clear();
        }

        sealed class ObjectRuntime(Dynel owner)
        {
            public Dynel Owner { get; } = owner;
            public AreaTriggerContext Context { get; set; } = new(owner, owner, null, null, null);
            public Queue<Signal> Signals { get; } = new();
            public List<RetainedAction> Retained { get; } = new();
            public List<SignalTimer> Timers { get; } = new();
            public StaticObjectStateMachine? Machine { get; set; }
            public int SignalMask { get; set; }
            public bool UseEnabled { get; set; } = true;
            public bool Initialized { get; set; }
            public bool EntryPending { get; set; }
        }

        sealed record Signal(int Mask, AreaTriggerContext Context);

        sealed class SignalTimer(int mask, int count)
        {
            public int Mask { get; } = mask;
            public float Remaining { get; set; } = count;
        }

        sealed class RetainedAction(ItemSpell row, AreaTriggerContext context, double now)
        {
            public ItemSpell Row { get; } = row;
            public AreaTriggerContext Context { get; } = context;
            public int Remaining { get; set; } = row.TickCount;
            public double LastTime { get; set; } = now;
        }
    }
}
