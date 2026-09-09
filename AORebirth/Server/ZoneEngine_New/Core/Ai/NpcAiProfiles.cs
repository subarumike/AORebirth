namespace ZoneEngine_New.Core.Ai
{
    using System;

    using GroveGames.BehaviourTree;
    using GroveGames.BehaviourTree.Nodes;

    public sealed class NpcAiProfile
    {
        public IChildTree CombatExtras { get; init; } = EmptyCombatExtras.Instance;

        public bool PatrolEnabled { get; init; }
    }

    public static class NpcAiProfiles
    {
        public static NpcAiProfile Default { get; } = new();

        /// <summary>
        /// Mid-fight extras that always succeed, used by tests to prove the combat slot can
        /// replace default chase/attack.
        /// </summary>
        public static NpcAiProfile HoldGround { get; } = new()
        {
            CombatExtras = HoldGroundCombatExtras.Instance
        };

        public static NpcAiProfile Resolve(string? hash)
        {
            _ = hash;
            return Default;
        }
    }

    public sealed class EmptyCombatExtras : ChildTree
    {
        public static EmptyCombatExtras Instance { get; } = new();

        public override void SetupTree(IParent parent)
        {
        }
    }

    public sealed class HoldGroundCombatExtras : ChildTree
    {
        public static HoldGroundCombatExtras Instance { get; } = new();

        public override void SetupTree(IParent parent)
        {
            ArgumentNullException.ThrowIfNull(parent);
            parent.Attach(new HoldGroundNode());
        }
    }

    sealed class HoldGroundNode : BehaviourNode
    {
        public HoldGroundNode()
            : base("hold-ground")
        {
        }

        public override NodeState Evaluate(float deltaTime)
            => _nodeState = NodeState.Success;
    }
}
