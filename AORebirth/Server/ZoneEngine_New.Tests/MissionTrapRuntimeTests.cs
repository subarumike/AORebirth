namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using AORebirth.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Nanos;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;

[TestClass]
public sealed class MissionTrapRuntimeTests
{
    const int Nano = 700001;

    [TestMethod]
    public void OptionalSkillPoliciesAreDisabledAndRepeatingTrapsFailClosed()
    {
        var policy = new MissionTrapInteractionPolicy();
        Assert.IsTrue(policy.IsValid);
        Assert.AreEqual(MissionTrapInteractionPolicy.DisabledMode, policy.DetectionMode);
        Assert.AreEqual(MissionTrapInteractionPolicy.DisabledMode, policy.DisarmMode);
        Assert.AreEqual(MissionTrapInteractionPolicy.ResourceDifficulty, policy.DetectionDifficultySource);
        policy.ConsumeOnTrigger = false;
        Assert.IsFalse(policy.IsValid);
        policy.ConsumeOnTrigger = true;
        policy.DetectionMode = "guessed-original-formula";
        Assert.IsFalse(policy.IsValid);
        policy.DetectionMode = MissionTrapInteractionPolicy.DisabledMode;
        policy.InteractionRange = float.NaN;
        Assert.IsFalse(policy.IsValid);
    }

    [DataTestMethod]
    [DataRow(-88, 1400)]
    [DataRow(-118, 1600)]
    public void PassiveSkillEffectUsesRecoveredCasterSequenceAndExistingExpiry(int delta, int duration)
    {
        var items = new StubItemBuilder().Add(PassiveSkill(delta, duration));
        Player player = TestWorld.CreatePlayer(100);
        player.Stats.Set(CharacterStat.AMSModifier, 200);
        // A buff invokes the normal player rebase. Establish real derived vitals
        // first so that rebase is not mistaken for trap damage.
        player.RebaseStats();
        int healthBefore = player.Stats.GetOrZero(CharacterStat.MaxHealth);
        Assert.IsTrue(healthBefore > 0);
        player.Stats.Set(CharacterStat.Health, healthBefore);
        var session = new InventoryActionTests.Session();
        session.BindPlayer(player);
        player.Session = session;
        Identity trap = new() { Type = IdentityType.Trap, Instance = 200 };
        DateTime now = DateTime.UtcNow;

        Assert.IsTrue(NanoRuntime.CanApplyEnvironmentalPassiveBuff(player, Nano, items));
        Assert.IsTrue(NanoRuntime.TryApplyEnvironmentalPassiveBuff(trap, player, Nano, items, now,
            () => session.Send(new ActionMessage
                { Identity = trap, Unknown = 0, ActionCode = 1, ActionIdentity = 109, Target = player.Identity })));
        Assert.AreEqual(200 + delta, player.Stats.GetOrZero(CharacterStat.AMSModifier));
        Assert.AreEqual(healthBefore, player.Stats.GetOrZero(CharacterStat.Health), "These recovered Skill effects are not damage.");
        Assert.AreEqual(trap, player.Buffs.Single().Source);
        Assert.IsFalse(player.IsInNanoRecharge(now));
        Assert.IsNull(player.PendingCast);

        MessageBody[] sequence = session.Messages.Where(message => message is CastNanoSpellMessage or ActionMessage
            || message is CharacterActionMessage action && action.Action is CharacterActionType.FinishNanoCasting
                or CharacterActionType.SetNanoDuration).ToArray();
        Assert.AreEqual(4, sequence.Length);
        Assert.IsTrue(sequence.Cast<N3Message>().All(message => message.Unknown == 0),
            "All four retained Smoke Bomb controls use pass-on zero for cast, event109, CA107 and CA98.");
        Assert.AreEqual(trap, ((CastNanoSpellMessage)sequence[0]).Caster);
        Assert.AreEqual(player.Identity, ((CastNanoSpellMessage)sequence[0]).Target);
        Assert.AreEqual(109, ((ActionMessage)sequence[1]).ActionIdentity);
        var finished = (CharacterActionMessage)sequence[2];
        Assert.AreEqual(player.Identity, finished.Identity);
        Assert.AreEqual(CharacterActionType.FinishNanoCasting, finished.Action);
        Assert.AreEqual(1, finished.Parameter1);
        Assert.AreEqual(Nano, finished.Parameter2);
        var timer = (CharacterActionMessage)sequence[3];
        Assert.AreEqual(CharacterActionType.SetNanoDuration, timer.Action);
        Assert.AreEqual(trap.Instance, timer.Parameter1);
        Assert.AreEqual(duration, timer.Parameter2);
        Assert.AreEqual(IdentityType.NanoProgram, timer.Target.Type);

        NanoRuntime.Tick(player, now.AddMilliseconds(duration * 10));
        Assert.AreEqual(0, player.Buffs.Count);
        Assert.AreEqual(200, player.Stats.GetOrZero(CharacterStat.AMSModifier));
        Assert.AreEqual(healthBefore, player.Stats.GetOrZero(CharacterStat.Health));
    }

    [TestMethod]
    public void UnsupportedInstantOrChildProgramsNeverBecomeTrapEffects()
    {
        Assert.IsFalse(NanoRuntime.SupportsEnvironmentalPassiveBuff(TestNanos.Create(Nano,
            durationCentiseconds: 0, modifiers: [TestNanos.Modify(CharacterStat.AMSModifier, -10)])));
        Assert.IsFalse(NanoRuntime.SupportsEnvironmentalPassiveBuff(TestNanos.Create(Nano, modifiers:
            [new ItemSpell { FunctionType = (int)FunctionType.CastNano, Arguments = [Nano + 1] }])));
        Assert.IsFalse(NanoRuntime.SupportsEnvironmentalPassiveBuff(TestNanos.Create(Nano, modifiers:
            [new ItemSpell { FunctionType = (int)FunctionType.Skill, Target = (int)ItemTarget.Target, Arguments = [276] }])));
    }

    [TestMethod]
    public void ParentedTrapPublicationRetainsAssociationAndIntroducesNoPhysicalBlocker()
    {
        using var world = new TrapWorld();
        var spawn = (TrapItemFullUpdateMessage)world.Trap.BuildSpawnMessage();
        Assert.AreEqual(world.Parent.Identity, spawn.Parent);
        Assert.AreEqual(world.Trap.Identity, spawn.Identity);
        Assert.AreEqual(2, spawn.TrapFlags);
        Assert.AreEqual(0, world.Trap.GetCollisionRadius());
        Assert.AreEqual(80, world.Trap.Stats.GetOrZero(CharacterStat.TrapDifficulty));
        var parent = (ChestItemFullUpdateMessage)world.Parent.BuildSpawnMessage();
        Assert.AreEqual(0, parent.UnknownArray.Length,
            "The chest's lockable authorization list is not a list of trap children.");
    }

    [TestMethod]
    public void TriggerCommitsBeforeEffectsAndCannotFireAgainAfterReconstruction()
    {
        using var world = new TrapWorld();
        world.BeforeCommit = _ => Assert.AreEqual(0, world.Player.Buffs.Count);
        Assert.IsTrue(world.Trap.TryTrigger(world.Player));
        Assert.AreEqual(MissionTrapState.Triggered, world.StoredState);
        Assert.AreEqual(world.StoredState, world.Trap.State);
        Assert.IsTrue(world.Trap.LastEffectApplied);
        Assert.IsTrue(world.Session.Messages.OfType<ActionMessage>().All(message => message.Unknown == 0),
            "Runtime event109 must preserve the thirteen captured control envelopes.");
        Assert.AreEqual(1, world.Player.Buffs.Count);
        Assert.IsFalse(world.Trap.TryTrigger(world.Player));
        Assert.AreEqual(1, world.Commits);
        Assert.AreEqual(0, ((TrapItemFullUpdateMessage)world.Trap.BuildSpawnMessage()).TrapFlags & 2);
        Assert.IsFalse(world.Trap.BuildSpawnCompanionMessages().OfType<TrapDisarmedMessage>().Any(),
            "Consumed triggering is not disarming.");

        world.Reconstruct();
        Assert.AreEqual(MissionTrapState.Triggered, world.Trap.State);
        Assert.IsFalse(world.Trap.TryTrigger(world.Player));
        Assert.AreEqual(1, world.Commits);
        Assert.AreEqual(1, world.Player.Buffs.Count);
    }

    [TestMethod]
    public void FailedDurableTransitionLeavesEffectsAndTrapStateUntouched()
    {
        using var world = new TrapWorld();
        world.AllowCommit = false;
        Assert.IsFalse(world.Trap.TryTrigger(world.Player));
        Assert.AreEqual(MissionTrapState.Armed, world.Trap.State);
        Assert.AreEqual(0, world.Player.Buffs.Count);
        Assert.IsFalse(world.Session.Messages.OfType<CastNanoSpellMessage>().Any());
    }

    [TestMethod]
    public void UnknownDurableOutcomeDoesNotBreakChestOpeningOrRetryEffects()
    {
        using var world = new TrapWorld();
        world.BeforeCommit = _ => throw new InvalidOperationException("simulated lost commit acknowledgement");
        world.Parent.AfterOpen += player => world.Trap.TryTrigger(player);
        Assert.IsTrue(world.Parent.TryUse(world.Player));
        Assert.IsTrue(world.Parent.IsOpen);
        Assert.AreEqual(0, world.Player.Buffs.Count);
        Assert.IsFalse(world.Trap.IsArmed);
        Assert.IsFalse(world.Trap.TryTrigger(world.Player));
        Assert.AreEqual(1, world.Attempts);
    }

    [TestMethod]
    public void MissingEffectOrDisabledOriginalFlagCannotConsumeATrap()
    {
        using var missing = new TrapWorld(includeEffect: false);
        Assert.IsFalse(missing.Trap.TryTrigger(missing.Player));
        Assert.AreEqual(0, missing.Commits);
        using var disabled = new TrapWorld(spawnFlags: 0);
        Assert.IsFalse(disabled.Trap.IsArmed);
        Assert.IsFalse(disabled.Trap.TryTrigger(disabled.Player));
        Assert.AreEqual(0, disabled.Commits);
    }

    [TestMethod]
    public void ParentAfterOpenFiresOnlyAfterSuccessfulOpeningAndNeverOnClose()
    {
        using var world = new TrapWorld();
        int opened = 0;
        world.Parent.AfterOpen += player =>
        {
            opened++;
            Assert.IsTrue(world.Parent.IsOpen);
            world.Trap.TryTrigger(player);
        };
        Assert.IsTrue(world.Parent.TryUse(world.Player));
        Assert.AreEqual(1, opened);
        Assert.AreEqual(1, world.Commits);
        Assert.IsTrue(world.Parent.TryUse(world.Player));
        Assert.AreEqual(1, opened);
        Assert.AreEqual(1, world.Commits);
    }

    [TestMethod]
    public void DetectionIsPerConnectionAndDoesNotRevealDisableOrTrigger()
    {
        var policy = new MissionTrapInteractionPolicy
        {
            DetectionMode = MissionTrapInteractionPolicy.SkillAtLeastDifficultyMode,
            DetectionDifficultySource = MissionTrapInteractionPolicy.ResourceDifficulty
        };
        using var world = new TrapWorld(policy);
        world.Player.Stats.Set(CharacterStat.Perception, 79);
        Assert.IsFalse(world.Trap.TryDetect(world.Player));
        world.Player.Stats.Set(CharacterStat.Perception, 80);
        Assert.IsTrue(world.Trap.TryDetect(world.Player));
        Assert.IsFalse(world.Trap.TryDetect(world.Player));
        Assert.AreEqual(MissionTrapState.Detected, world.Trap.State);
        Assert.IsTrue(world.Trap.IsArmed);
        Assert.AreEqual(0, world.Player.Buffs.Count);
        var notification = world.Session.Messages.OfType<CharacterActionMessage>()
            .Single(message => message.Action == CharacterActionType.TrapDetected);
        Assert.AreEqual((int)world.Trap.Identity.Type, notification.Parameter1);
        Assert.AreEqual(world.Trap.Identity.Instance, notification.Parameter2);

        var reconnected = new InventoryActionTests.Session();
        reconnected.BindPlayer(world.Player);
        world.Player.Session = reconnected;
        Assert.IsTrue(world.Trap.TryDetect(world.Player));
        Assert.AreEqual(1, reconnected.Messages.OfType<CharacterActionMessage>()
            .Count(message => message.Action == CharacterActionType.TrapDetected));
        Assert.AreEqual(1, world.Commits, "A connection-local notification must not rewrite the global state.");
    }

    [TestMethod]
    public void ConfiguredDifficultyIsExplicitAndMissingResourceDifficultyFailsClosed()
    {
        var policy = new MissionTrapInteractionPolicy
        {
            DetectionMode = MissionTrapInteractionPolicy.SkillAtLeastDifficultyMode,
            DetectionDifficulty = 1
        };
        using var world = new TrapWorld(policy, includeDifficulty: false);
        world.Player.Stats.Set(CharacterStat.Perception, 1000);
        Assert.IsFalse(world.Trap.TryDetect(world.Player));
        policy.DetectionDifficultySource = MissionTrapInteractionPolicy.ConfiguredDifficulty;
        using var configured = new TrapWorld(policy, includeDifficulty: false);
        configured.Player.Stats.Set(CharacterStat.Perception, 1);
        Assert.IsTrue(configured.Trap.TryDetect(configured.Player));
    }

    [TestMethod]
    public void RevealIsSeparateAndReconstructionPreservesItsOriginalActor()
    {
        var policy = new MissionTrapInteractionPolicy
        {
            DetectionMode = MissionTrapInteractionPolicy.SkillAtLeastDifficultyMode
        };
        using var world = new TrapWorld(policy);
        world.Player.Stats.Set(CharacterStat.Perception, 80);
        Assert.IsFalse(world.Trap.TryReveal(world.Player));
        Assert.IsTrue(world.Trap.TryDetect(world.Player));
        Assert.AreEqual(MissionTrapState.Detected, world.StoredState);
        Assert.IsTrue(world.Trap.TryReveal(world.Player));
        Assert.AreEqual(MissionTrapState.Detected | MissionTrapState.Revealed, world.StoredState);
        Assert.AreEqual(world.Player.Identity, world.Trap.RevealedBy);
        Assert.AreEqual(2, ((TrapItemFullUpdateMessage)world.Trap.BuildSpawnMessage()).TrapFlags,
            "Remembered detection and reveal are not interchangeable spawn flags.");
        world.Reconstruct();
        var reveal = world.Trap.BuildSpawnCompanionMessages().OfType<ActionMessage>().Single();
        Assert.AreEqual(112, reveal.ActionIdentity);
        Assert.AreEqual(world.Player.Identity, reveal.Target);
        Assert.IsTrue(world.Trap.IsArmed);
        Assert.AreEqual(0, world.Player.Buffs.Count);
    }

    [TestMethod]
    public void ChildFirstVisibilityOnReconnectPublishesParentThenChildThenSavedReveal()
    {
        var policy = new MissionTrapInteractionPolicy
        {
            DetectionMode = MissionTrapInteractionPolicy.SkillAtLeastDifficultyMode
        };
        using var world = new TrapWorld(policy);
        world.Player.Stats.Set(CharacterStat.Perception, 80);
        Assert.IsTrue(world.Trap.TryDetect(world.Player));
        Assert.IsTrue(world.Trap.TryReveal(world.Player));
        world.Reconstruct();
        var reconnected = new InventoryActionTests.Session();
        reconnected.BindPlayer(world.Player);
        world.Player.Session = reconnected;

        // Present the child as the first candidate, independently of HashSet order.
        object visibility = typeof(PlayfieldLocality).GetField("_visibility", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(world.Locality)!;
        MethodInfo prepare = visibility.GetType().GetMethod("PrepareRecipient", BindingFlags.Instance | BindingFlags.NonPublic)!;
        MethodInfo enter = visibility.GetType().GetMethod("TryEnterVisibility", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.IsTrue((bool)prepare.Invoke(visibility, [world.Player])!);
        Assert.IsTrue((bool)enter.Invoke(visibility, [world.Player, world.Trap])!);

        Assert.AreEqual(3, reconnected.Messages.Count);
        var parent = (ChestItemFullUpdateMessage)reconnected.Messages[0];
        var child = (TrapItemFullUpdateMessage)reconnected.Messages[1];
        var revealed = (ActionMessage)reconnected.Messages[2];
        Assert.AreEqual(world.Parent.Identity, parent.Identity);
        Assert.AreEqual(0, parent.UnknownArray.Length, "Chest authorization data stays unchanged.");
        Assert.AreEqual(parent.Identity, child.Parent);
        Assert.AreEqual(world.Trap.Identity, child.Identity);
        Assert.AreEqual(112, revealed.ActionIdentity);
        Assert.AreEqual(child.Identity, revealed.Identity);
        Assert.AreEqual(world.Player.Identity, revealed.Target);
        Assert.IsFalse((bool)enter.Invoke(visibility, [world.Player, world.Parent])!);
        Assert.AreEqual(3, reconnected.Messages.Count, "The later parent candidate must not duplicate its spawn.");
    }

    [TestMethod]
    public void ChildVisibilityFailsClosedWhenItsExactParentIsUntracked()
    {
        using var world = new TrapWorld();
        world.Locality.UnregisterDynel(world.Parent);
        var reconnected = new InventoryActionTests.Session();
        reconnected.BindPlayer(world.Player);
        world.Player.Session = reconnected;
        object visibility = typeof(PlayfieldLocality).GetField("_visibility", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(world.Locality)!;
        MethodInfo prepare = visibility.GetType().GetMethod("PrepareRecipient", BindingFlags.Instance | BindingFlags.NonPublic)!;
        MethodInfo enter = visibility.GetType().GetMethod("TryEnterVisibility", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.IsTrue((bool)prepare.Invoke(visibility, [world.Player])!);
        Assert.IsFalse((bool)enter.Invoke(visibility, [world.Player, world.Trap])!);
        Assert.AreEqual(0, reconnected.Messages.Count);
    }

    [TestMethod]
    public void PostCommitEffectFailureRemainsConsumedWithoutRetry()
    {
        using var world = new TrapWorld();
        world.BeforeCommit = _ => world.Items.Add(new ItemTemplate { Id = Nano });
        Assert.IsTrue(world.Trap.TryTrigger(world.Player));
        Assert.AreEqual(MissionTrapState.Triggered, world.StoredState);
        Assert.IsFalse(world.Trap.LastEffectApplied);
        Assert.AreEqual(0, world.Player.Buffs.Count);
        Assert.IsFalse(world.Trap.TryTrigger(world.Player));
        Assert.AreEqual(1, world.Attempts);
    }

    [TestMethod]
    public void DisarmUsesTheActualHeldFlaggedToolWithoutInventedConsumption()
    {
        var policy = new MissionTrapInteractionPolicy
        {
            DisarmMode = MissionTrapInteractionPolicy.SkillAtLeastDifficultyMode
        };
        using var world = new TrapWorld(policy);
        Item tool = TestWorld.CreateItem(instanceId: 9100, can: CanFlags.DisarmTraps);
        world.Player.Inventory.Inventory.Add(64, tool);
        world.Player.Stats.Set(CharacterStat.TrapDisarm, 79);
        Assert.IsFalse(world.Trap.TryDisarm(world.Player, tool));
        world.Player.Stats.Set(CharacterStat.TrapDisarm, 80);
        Assert.IsTrue(world.Trap.TryDisarm(world.Player, tool));
        Assert.AreEqual(MissionTrapState.Disarmed, world.Trap.State);
        Assert.AreEqual(0, ((ChestItemFullUpdateMessage)world.Parent.BuildSpawnMessage()).UnknownArray.Length);
        Assert.AreSame(tool, world.Player.Inventory.Inventory.Content[64]);
        Assert.IsFalse(world.Trap.TryTrigger(world.Player));
        Assert.AreEqual(0, world.Player.Buffs.Count);
        Assert.AreEqual(0, ((TrapItemFullUpdateMessage)world.Trap.BuildSpawnMessage()).TrapFlags & 2);
        Assert.IsInstanceOfType<TrapDisarmedMessage>(world.Trap.BuildSpawnCompanionMessages().Single());
    }

    [TestMethod]
    public void StaleForeignDisconnectedAndDistantActorsCannotTrigger()
    {
        using var world = new TrapWorld();
        world.Session.State = SessionState.Closed;
        Assert.IsFalse(world.Trap.TryTrigger(world.Player));
        world.Session.State = SessionState.InPlay;
        world.Player.Position = new AORebirth.Core.Vector.Vector3(100, 0, 0);
        Assert.IsFalse(world.Trap.TryTrigger(world.Player));
        world.Player.Position = world.Parent.Position;
        world.Registry.UnregisterExact(world.Trap);
        Assert.IsFalse(world.Trap.TryTrigger(world.Player));
        Assert.AreEqual(0, world.Commits);
    }

    [TestMethod]
    public void OneSharedTrapCannotBeClaimedAgainByAnotherAdmittedPlayer()
    {
        using var world = new TrapWorld();
        Player other = TestWorld.CreatePlayer(222);
        var session = new InventoryActionTests.Session();
        session.BindPlayer(other);
        other.Session = session;
        other.Playfield = world.Player.Playfield;
        other.Position = world.Player.Position;
        world.Registry.Register(other);
        Assert.IsTrue(world.Trap.TryTrigger(world.Player));
        Assert.IsFalse(world.Trap.TryTrigger(other));
        Assert.AreEqual(0, other.Buffs.Count);
        Assert.AreEqual(1, world.Commits);
    }

    static NanoSpell PassiveSkill(int delta = -88, int duration = 1400) => TestNanos.Create(Nano,
        durationCentiseconds: duration, can: CanFlags.ApplyOnHostile,
        modifiers:
        [
            new ItemSpell { FunctionType = (int)FunctionType.Skill, Target = (int)ItemTarget.Target,
                Arguments = [(int)CharacterStat.AMSModifier, delta] },
            new ItemSpell { FunctionType = (int)FunctionType.TauntNpc, Target = (int)ItemTarget.Target,
                Arguments = [149] }
        ]);

    sealed class TrapWorld : IDisposable
    {
        readonly InventoryActionTests.World _world = new();
        readonly ServiceProvider _services;
        readonly PlayfieldLocality _locality = new(4582, null);
        readonly MissionTrapInteractionPolicy _policy;
        readonly byte _flags;
        readonly bool _includeDifficulty;
        public readonly StubItemBuilder Items = new();
        public readonly DynelRegistry Registry = new();
        public readonly Chest Parent;
        public Player Player => _world.Player;
        public InventoryActionTests.Session Session => _world.Session;
        public PlayfieldLocality Locality => _locality;
        public MissionTrap Trap { get; private set; } = null!;
        public MissionTrapState StoredState;
        public Identity StoredRevealedBy;
        public int Commits;
        public int Attempts;
        public bool AllowCommit = true;
        public Action<MissionTrapState>? BeforeCommit;

        public TrapWorld(MissionTrapInteractionPolicy? policy = null, bool includeEffect = true,
            byte spawnFlags = 2, bool includeDifficulty = true)
        {
            _policy = policy ?? new();
            _flags = spawnFlags;
            _includeDifficulty = includeDifficulty;
            if (includeEffect)
                Items.Add(PassiveSkill());
            _services = new ServiceCollection().AddSingleton(Registry).AddSingleton(_locality)
                .AddSingleton<IItemBuilder>(Items).BuildServiceProvider();
            typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Player.Playfield, _services);
            typeof(Playfield).GetField("_lifecycleSync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Player.Playfield, new Lock());
            typeof(Playfield).GetField("_pendingStatRebases", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Player.Playfield, new ConcurrentDictionary<Character, byte>());
            Registry.Register(Player);
            _locality.RegisterDynel(Player);
            _locality.ActivatePlayerVisibility(Player);
            Parent = MissionChestProtocolTests.CreateCapturedChest(locked: false);
            Parent.Playfield = Player.Playfield;
            Parent.Position = Player.Position;
            Registry.Register(Parent);
            _locality.RegisterDynel(Parent);
            Reconstruct();
            Session.Messages.Clear();
        }

        public void Reconstruct()
        {
            if (Trap != null)
            {
                _locality.UnregisterDynel(Trap);
                Registry.UnregisterExact(Trap);
            }
            Identity identity = new() { Type = IdentityType.Trap, Instance = 8100 };
            var stats = _includeDifficulty
                ? new[] { new GameTuple<CharacterStat, uint> { Value1 = CharacterStat.TrapDifficulty, Value2 = 80 } }
                : Array.Empty<GameTuple<CharacterStat, uint>>();
            Trap = new MissionTrap(identity, Parent, new TrapItemFullUpdateMessage
            {
                Identity = identity, Parent = Parent.Identity, TrapFlags = _flags, Stats = stats
            }, Nano, _policy, StoredState, (trap, player, next) =>
            {
                Attempts++;
                BeforeCommit?.Invoke(next);
                if (!AllowCommit || trap.State != StoredState)
                    return false;
                StoredState = next;
                if ((next & MissionTrapState.Revealed) != 0)
                    StoredRevealedBy = player.Identity;
                Commits++;
                return true;
            }, StoredRevealedBy);
            Registry.Register(Trap);
            _locality.RegisterDynel(Trap);
        }

        public void Dispose()
        {
            _services.Dispose();
            _world.Dispose();
        }
    }
}
