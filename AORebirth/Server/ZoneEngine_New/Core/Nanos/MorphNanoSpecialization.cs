namespace ZoneEngine_New.Core.Nanos
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using AORebirth.Enums;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield.Locality;

    /// <summary>
    /// Exact catalog-driven Phasefront/hoverboard self morphs. Scalar modifiers belong to the
    /// nano transaction; presentation is a reversible owner-only overlay, never persisted over
    /// the original character/equipment base. No inferred template, hoverboard SpellList or pet.
    /// </summary>
    public sealed class MorphNanoSpecialization : INanoSpecialization, INanoOwnerProjection, INanoActiveSetValidator
    {
        private sealed record Description(Dictionary<CharacterStat, int> Modifiers, int Shape, bool Flight, int Restrictions);
        private sealed class State(Player player, int nanoId, Description description)
        {
            public Player Player = player;
            public int NanoId = nanoId;
            public Description Description = description;
            public Dictionary<CharacterStat, int> Overlay = new();
            public bool FlightAllowed;
        }
        private readonly ConcurrentDictionary<int, State> _states = new();
        public bool Handles(int nanoId) => nanoId is 82835 or 281569 or 288546 or 270542;

        public bool TryPrepare(Player caster, Player target, NanoDefinition nano, out NanoSpecializationPlan plan)
        {
            plan = new(true, nano.DurationCentiseconds);
            if (!Handles(nano.Id) || !ReferenceEquals(caster, target) || nano.DurationCentiseconds <= 0
                || nano.Template.Defend.Count != 0 || target.IsPersistenceQuarantined
                || (_states.TryGetValue(target.Identity.Instance, out State? old)
                    && (!ReferenceEquals(old.Player, target) || old.NanoId != nano.Id))
                || !TryDescribe(target, nano, out Description description)) return false;
            // Old persisted morph bases have no original appearance field. Preserve/refuse that
            // ambiguous hydration instead of inventing a zero base or clearing an equipped vehicle.
            if (target.Stats.GetOrZero(CharacterStat.MonsterData, StatDetail.Base) != 0
                || !CanOverlay(target, description, old)) return false;
            plan = plan with { Modifiers = description.Modifiers,
                ScriptedChildren = nano.Id == 82835 ? [SparrowChildNanoSpecialization.NanoId] : [] };
            return true;
        }

        public bool IsValidActiveSet(Player player, IReadOnlyList<ActiveNanoRecord> active)
            => active.Count(a => Handles(a.NanoId)) <= 1;

        private static bool CanOverlay(Player player, Description description, State? old)
        {
            foreach (CharacterStat stat in new[] { CharacterStat.MonsterData, CharacterStat.CATMesh,
                CharacterStat.DisplayCATMesh, CharacterStat.IsVehicle })
            {
                if (stat == CharacterStat.IsVehicle && (!description.Flight || !AllowFlight(player))) continue;
                int desired = stat == CharacterStat.IsVehicle ? 1 : description.Shape;
                int prior = old?.Overlay.GetValueOrDefault(stat) ?? 0;
                long delta = desired - ((long)player.Stats.GetOrZero(stat) - prior);
                long bonus = (long)player.Stats.GetOrZero(stat, StatDetail.Bonus) - prior + delta;
                if (delta is <= int.MinValue or > int.MaxValue || bonus is < int.MinValue or > int.MaxValue) return false;
            }
            return true;
        }

        private static bool TryDescribe(Player player, NanoDefinition nano, out Description description)
        {
            var modifiers = new Dictionary<CharacterStat, int>();
            int shape = 0, restrictions = 0, children = 0; bool flight = false;
            description = new(modifiers, 0, false, 0);
            if (!nano.Template.SpellList.TryGetValue(EventType.OnUse, out var spells) || spells.Count == 0) return false;
            try
            {
                foreach (ItemSpell spell in spells)
                {
                    if (spell.Target is not ((int)ItemTarget.User or (int)ItemTarget.Self or (int)ItemTarget.Wearer)
                        || spell.TickCount is < 0 or > 1 || spell.TickInterval != 0
                        || !NanoRequirements.TryEvent(player, player, spell.Requirements, out bool met)) return false;
                    if (nano.Id == 82835 && spell.FunctionType == (int)FunctionType.CanFly)
                    {
                        if (spell.Arguments.Count != 0 || spell.Requirements.Count != 2
                            || spell.Requirements[0] is not { ChildOperator: (int)Operator.Unknown,
                                Operator: (int)Operator.EqualTo, StatNumber: 531, Target: (int)ItemTarget.Self, Value: 0 }
                            || spell.Requirements[1] is not { ChildOperator: (int)Operator.Unknown,
                                Operator: (int)Operator.And, StatNumber: 0, Target: (int)ItemTarget.Self, Value: 0 }) return false;
                        flight = true; // Capability stays; current playfield controls its projection after zoning.
                        continue;
                    }
                    if (!met) continue;
                    switch ((FunctionType)spell.FunctionType)
                    {
                        case FunctionType.Modify:
                        case FunctionType.ScalingModify:
                            if (spell.Requirements.Count != 0 || spell.Arguments.Count != 2
                                || !ItemUseFunctions.TryReadInt(spell.Arguments, 0, out int stat)
                                || !ItemUseFunctions.TryReadInt(spell.Arguments, 1, out int amount)
                                || stat == (int)CharacterStat.Cash || amount == int.MinValue) return false;
                            modifiers[(CharacterStat)stat] = checked(modifiers.GetValueOrDefault((CharacterStat)stat) + amount);
                            break;
                        case FunctionType.MonsterShape:
                            if (spell.Arguments.Count != 1 || !ItemUseFunctions.TryReadInt(spell.Arguments, 0, out int nextShape)
                                || nextShape <= 0 || (shape != 0 && shape != nextShape)) return false;
                            shape = nextShape; break;
                        case FunctionType.CanFly:
                            if (spell.Arguments.Count != 0) return false;
                            flight = true; break;
                        case FunctionType.RestrictAction:
                            if (spell.Arguments.Count != 1 || !ItemUseFunctions.TryReadInt(spell.Arguments, 0, out int flags)
                                || flags != 2) return false;
                            restrictions |= flags; break;
                        case FunctionType.CastNano:
                            if (nano.Id != 82835 || spell.Requirements.Count != 0 || spell.Arguments.Count != 1
                                || !ItemUseFunctions.TryReadInt(spell.Arguments, 0, out int child)
                                || child != SparrowChildNanoSpecialization.NanoId || ++children != 1) return false;
                            break; // NanoService owns this exact nested duration/wire transaction.
                        default: return false;
                    }
                }
            }
            catch (OverflowException) { return false; }
            if (shape == 0 || (nano.Id == 82835 && children != 1)) return false; // No guessed morph/child mapping.
            description = new(modifiers, shape, flight, restrictions); return true;
        }

        public void Applied(Player caster, Player target, NanoDefinition nano, ActiveNanoRecord? active)
        {
            if (active == null || !TryDescribe(target, nano, out Description description))
                throw new InvalidOperationException("Committed morph lost its prepared catalog projection.");
            if (_states.TryGetValue(target.Identity.Instance, out State? old)) RemoveOverlay(old);
            var state = new State(target, nano.Id, description); _states[target.Identity.Instance] = state;
            ApplyOverlay(state); SendAppearance(target, clearing: false); SendVisual(state, remove: false);
        }

        public void Restored(Player target, NanoDefinition nano, ActiveNanoRecord active)
        {
            if (!TryDescribe(target, nano, out Description description))
                throw new InvalidOperationException("Validated morph restore lost its catalog projection.");
            var state = new State(target, nano.Id, description); _states[target.Identity.Instance] = state;
            ApplyOverlay(state); // Before initial FullCharacter; no premature hydration packets.
        }

        public void Removed(Player target, int nanoId)
        {
            if (!TryState(target, out State state) || state.NanoId != nanoId
                || !_states.TryRemove(new KeyValuePair<int, State>(target.Identity.Instance, state))) return;
            SendVisual(state, remove: true); RemoveOverlay(state); SendAppearance(target, clearing: true);
        }
        public void Detached(Player player)
        {
            if (TryState(player, out State state)
                && _states.TryRemove(new KeyValuePair<int, State>(player.Identity.Instance, state))) RemoveOverlay(state);
        }
        public void Refresh(Player player)
        {
            if (!TryState(player, out State state)) return;
            if (state.FlightAllowed != AllowFlight(player)) { RemoveOverlay(state); ApplyOverlay(state); }
            player.Motor.RefreshFlightAuthority(); SendVisual(state, remove: false); SendAppearance(player, clearing: false);
        }
        public void ReapplyAfterRebase(Player player)
        {
            if (!TryState(player, out State state)) return;
            state.Overlay.Clear(); // Caller already cleared Bonus; do not subtract twice.
            ApplyOverlay(state);
        }
        public void Tick(Player player)
        {
            if (TryState(player, out State state) && state.FlightAllowed != AllowFlight(player)) Refresh(player);
        }
        public bool IsFightingRestricted(Player player) => TryState(player, out State state)
            && (state.Description.Restrictions & 2) != 0;
        private bool TryState(Player player, out State state) => _states.TryGetValue(player.Identity.Instance, out state!)
            && ReferenceEquals(state.Player, player);
        private static bool AllowFlight(Player player) => player.Playfield != null
            ? player.Playfield.Identity.Instance is < 4000 or > 4999 : player.Stats.GetOrZero((CharacterStat)531) == 0;

        private static void ApplyOverlay(State state)
        {
            state.FlightAllowed = AllowFlight(state.Player);
            SetFull(CharacterStat.MonsterData, state.Description.Shape);
            SetFull(CharacterStat.CATMesh, state.Description.Shape);
            SetFull(CharacterStat.DisplayCATMesh, state.Description.Shape);
            if (state.Description.Flight && state.FlightAllowed) SetFull(CharacterStat.IsVehicle, 1);
            void SetFull(CharacterStat stat, int desired)
            {
                int delta = checked(desired - state.Player.Stats.GetOrZero(stat));
                state.Overlay.Add(stat, delta); state.Player.Stats.AddBonus(stat, delta, dirty: true);
            }
        }
        private static void RemoveOverlay(State state)
        {
            foreach (var stat in state.Overlay) state.Player.Stats.AddBonus(stat.Key, checked(-stat.Value), dirty: true);
            state.Overlay.Clear();
        }
        private static bool Online(Player player) => !player.IsPersistenceQuarantined
            && player.Session is { State: SessionState.InPlay } session && ReferenceEquals(session.Player, player);
        private static void SendVisual(State state, bool remove)
        {
            Player player = state.Player;
            if (Online(player) && player.Playfield != null && MorphVisualPackets.TryBuild(state.NanoId, remove,
                player.Identity, player.Playfield.Identity.Instance, state.FlightAllowed, out byte[] packet)) player.Session!.Send(packet);
        }
        private static void SendAppearance(Player player, bool clearing)
        {
            player.Motor.RefreshFlightAuthority();
            if (!Online(player)) return;
            if (clearing)
            {
                Single(CharacterStat.MonsterData, Math.Max(0, player.Stats.GetOrZero(CharacterStat.MonsterData)));
                Single(CharacterStat.IsVehicle, Math.Max(0, player.Stats.GetOrZero(CharacterStat.IsVehicle)));
                Single(CharacterStat.Mesh, player.Stats.GetOrZero(CharacterStat.MonsterData) == 0
                    && player.Stats.GetOrZero(CharacterStat.IsVehicle) == 0 ? 0 : Math.Max(0, player.Stats.GetOrZero(CharacterStat.Mesh)));
            }
            Announce(player.BuildAppearanceUpdateMessage()); Announce(player.BuildSpawnMessage());
            void Single(CharacterStat stat, int value) => Announce(new StatMessage { Identity = player.Identity, Unknown = 0,
                Stats = [new GameTuple<CharacterStat, uint> { Value1 = stat, Value2 = (uint)value }] });
            void Announce(MessageBody body)
            {
                if (player.Playfield != null) player.Playfield.GetRequiredService<PlayfieldLocality>().Announce(player, body, includeSelf: true);
                else player.Session!.Send(body);
            }
        }
    }
}
