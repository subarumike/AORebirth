namespace ZoneEngine.Core.Playfields
{
    public sealed class NpcPatrolReplayCoordinator
    {
        public delegate void AssignPatrolReplaySegments(NpcPatrolReplaySegment[] segments);

        private readonly CapturedAreteRobotContentProvider capturedRobotContentProvider;

        private readonly CapturedSubwayContentProvider capturedSubwayContentProvider;

        public NpcPatrolReplayCoordinator(CapturedAreteRobotContentProvider capturedRobotContentProvider)
            : this(capturedRobotContentProvider, null)
        {
        }

        internal NpcPatrolReplayCoordinator(
            CapturedAreteRobotContentProvider capturedRobotContentProvider,
            CapturedSubwayContentProvider capturedSubwayContentProvider)
        {
            this.capturedRobotContentProvider = capturedRobotContentProvider;
            this.capturedSubwayContentProvider = capturedSubwayContentProvider;
        }

        public NpcPatrolReplaySegment[] BuildCapturedAreteRobotSegments(int sourceInstance)
        {
            CapturedAreteRobotPatrolReplaySegment[] segments =
                this.capturedRobotContentProvider.GetPatrolReplaySegments(sourceInstance);
            var result = new NpcPatrolReplaySegment[segments.Length];
            for (int i = 0; i < segments.Length; i++)
            {
                result[i] = new NpcPatrolReplaySegment(
                    segments[i].DelayAfterSeconds,
                    segments[i].StartX,
                    segments[i].StartY,
                    segments[i].StartZ,
                    segments[i].EndX,
                    segments[i].EndY,
                    segments[i].EndZ);
            }

            return result;
        }

        public void AssignCapturedAreteRobotReplay(
            int sourceInstance,
            AssignPatrolReplaySegments assignSegments)
        {
            assignSegments(this.BuildCapturedAreteRobotSegments(sourceInstance));
        }

        internal NpcPatrolReplaySegment[] BuildCapturedSubwaySegments(int sourceInstance)
        {
            CapturedSubwayPatrolReplaySegment[] segments =
                this.capturedSubwayContentProvider == null
                    ? new CapturedSubwayPatrolReplaySegment[0]
                    : this.capturedSubwayContentProvider.GetPatrolReplaySegments(sourceInstance);
            var result = new NpcPatrolReplaySegment[segments.Length];
            for (int i = 0; i < segments.Length; i++)
            {
                result[i] = new NpcPatrolReplaySegment(
                    segments[i].DelayAfterSeconds,
                    segments[i].StartX,
                    segments[i].StartY,
                    segments[i].StartZ,
                    segments[i].EndX,
                    segments[i].EndY,
                    segments[i].EndZ);
            }

            return result;
        }

        internal void AssignCapturedSubwayReplay(
            int sourceInstance,
            AssignPatrolReplaySegments assignSegments)
        {
            assignSegments(this.BuildCapturedSubwaySegments(sourceInstance));
        }
    }

    public sealed class NpcPatrolReplaySegment
    {
        public NpcPatrolReplaySegment(
            double delayAfterSeconds,
            float startX,
            float startY,
            float startZ,
            float endX,
            float endY,
            float endZ)
        {
            this.DelayAfterSeconds = delayAfterSeconds;
            this.StartX = startX;
            this.StartY = startY;
            this.StartZ = startZ;
            this.EndX = endX;
            this.EndY = endY;
            this.EndZ = endZ;
        }

        public double DelayAfterSeconds { get; private set; }

        public float StartX { get; private set; }

        public float StartY { get; private set; }

        public float StartZ { get; private set; }

        public float EndX { get; private set; }

        public float EndY { get; private set; }

        public float EndZ { get; private set; }
    }
}
