namespace ZoneEngine_New.Core.Ai
{
    using System.Collections.Generic;

    using GroveGames.BehaviourTree.Nodes;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Metrics;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    sealed class ReturnToSpawnNode : BehaviourNode
    {
        readonly NpcBrain _brain;

        public ReturnToSpawnNode(NpcBrain brain)
            : base("return-to-spawn")
        {
            _brain = brain;
        }

        public override NodeState Evaluate(float deltaTime)
        {
            TickStallWatch.Stage("node.return-to-spawn", _brain.Npc.Identity.Instance);
            _brain.StopFighting();
            if (_brain.HasArrivedHome())
                return _nodeState = NodeState.Success;

            _brain.PathTo(_brain.Home);
            return _nodeState = NodeState.Running;
        }
    }

    sealed class ResetNpcNode : BehaviourNode
    {
        readonly NpcBrain _brain;

        public ResetNpcNode(NpcBrain brain)
            : base("reset")
        {
            _brain = brain;
        }

        public override NodeState Evaluate(float deltaTime)
        {
            TickStallWatch.Stage("node.reset", _brain.Npc.Identity.Instance);
            _brain.ResetOutOfCombat();
            return _nodeState = NodeState.Success;
        }
    }

    sealed class SelectHighestThreatNode : BehaviourNode
    {
        readonly NpcBrain _brain;

        public SelectHighestThreatNode(NpcBrain brain)
            : base("select-threat")
        {
            _brain = brain;
        }

        public override NodeState Evaluate(float deltaTime)
        {
            TickStallWatch.Stage("node.select-threat", _brain.Npc.Identity.Instance);
            return _nodeState = _brain.TrySelectHighestThreat() ? NodeState.Success : NodeState.Failure;
        }
    }

    sealed class MoveTowardTargetNode : BehaviourNode
    {
        readonly NpcBrain _brain;

        public MoveTowardTargetNode(NpcBrain brain)
            : base("move-toward-target")
        {
            _brain = brain;
        }

        public override NodeState Evaluate(float deltaTime)
        {
            TickStallWatch.Stage("node.move-toward-target", _brain.Npc.Identity.Instance);
            Character? target = _brain.ResolveCurrentTarget();
            if (target == null)
                return _nodeState = NodeState.Failure;

            if (_brain.IsInAttackRange(target))
            {
                _brain.StopPathing();
                return _nodeState = NodeState.Success;
            }

            _brain.PathTo(target.Position);
            return _nodeState = NodeState.Running;
        }
    }

    sealed class StartFightingNode : BehaviourNode
    {
        readonly NpcBrain _brain;

        public StartFightingNode(NpcBrain brain)
            : base("start-fighting")
        {
            _brain = brain;
        }

        public override NodeState Evaluate(float deltaTime)
        {
            TickStallWatch.Stage("node.start-fighting", _brain.Npc.Identity.Instance);
            Character? target = _brain.ResolveCurrentTarget();
            if (target == null)
                return _nodeState = NodeState.Failure;

            if (_brain.Npc.FightingTarget != target.Identity)
                _brain.Npc.StartFighting(target.Identity, 0);

            return _nodeState = NodeState.Success;
        }
    }

    sealed class PatrolNode : BehaviourNode
    {
        readonly NpcBrain _brain;
        int _index;

        public PatrolNode(NpcBrain brain)
            : base("patrol")
        {
            _brain = brain;
        }

        public override NodeState Evaluate(float deltaTime)
        {
            TickStallWatch.Stage("node.patrol", _brain.Npc.Identity.Instance);
            IReadOnlyList<Vector3> points = _brain.PatrolWaypoints;
            if (points.Count == 0)
                return _nodeState = NodeState.Failure;

            if (_index >= points.Count)
                _index = 0;

            Vector3 dest = points[_index];
            if (NpcAiRules.IsNearby(_brain.Npc.Position, dest, NpcAiRules.ArriveHomeMeters))
            {
                _index = (_index + 1) % points.Count;
                dest = points[_index];
            }

            _brain.PathTo(dest);
            return _nodeState = NodeState.Running;
        }

        public override void Reset()
        {
            _index = 0;
        }
    }

    sealed class IdleNode : BehaviourNode
    {
        readonly NpcBrain _brain;

        public IdleNode(NpcBrain brain)
            : base("idle")
        {
            _brain = brain;
        }

        public override NodeState Evaluate(float deltaTime)
        {
            TickStallWatch.Stage("node.idle", _brain.Npc.Identity.Instance);
            _brain.StopPathing();
            return _nodeState = NodeState.Success;
        }
    }

    static class NpcAiCombat
    {
        public static void AnnounceStopFight(Character npc)
        {
            if (npc.FightingTarget.Instance == 0)
                return;

            npc.Cell?.Announce(
                new StopFightMessage
                {
                    Identity = npc.Identity,
                    Unknown1 = 1
                });
            npc.SetFightingTarget(Identity.None);
        }
    }
}
