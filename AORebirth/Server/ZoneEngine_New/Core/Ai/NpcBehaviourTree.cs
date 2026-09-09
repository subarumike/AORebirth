namespace ZoneEngine_New.Core.Ai
{
    using GroveGames.BehaviourTree;
    using GroveGames.BehaviourTree.Nodes;
    using GroveGames.BehaviourTree.Nodes.Composites;
    using GroveGames.BehaviourTree.Nodes.Decorators;

    sealed class NpcBehaviourTree : BehaviourTree
    {
        readonly NpcBrain _brain;
        readonly NpcAiProfile _profile;

        public NpcBehaviourTree(IRoot root, NpcBrain brain, NpcAiProfile profile)
            : base(root)
        {
            _brain = brain;
            _profile = profile;
        }

        public override void SetupTree()
        {
            IParent root = Root.Selector("root");

            root.Conditional(_brain.ShouldLeash, "should-leash")
                .Sequence("leash")
                .Attach(new ReturnToSpawnNode(_brain))
                .Attach(new ResetNpcNode(_brain));

            IParent combat = root.Conditional(_brain.HasNearbyHate, "has-nearby-hate")
                .Sequence("combat")
                .Attach(new SelectHighestThreatNode(_brain));

            IParent actions = combat.Selector("combat-actions");
            actions.Attach(_profile.CombatExtras);
            actions.Sequence("default-fight")
                .Attach(new MoveTowardTargetNode(_brain))
                .Attach(new StartFightingNode(_brain));

            root.Conditional(() => _brain.PatrolEnabled, "patrol-enabled")
                .Attach(new PatrolNode(_brain));
            root.Attach(new IdleNode(_brain));
        }
    }
}
