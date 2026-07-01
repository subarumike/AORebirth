namespace ZoneEngine.Core.Playfields
{
    public sealed class NpcPatrolReplayCoordinator
    {
        public delegate void AssignPatrolReplaySegments(NpcPatrolReplaySegment[] segments);

        private readonly CapturedAreteRobotContentProvider capturedRobotContentProvider;

        public NpcPatrolReplayCoordinator(CapturedAreteRobotContentProvider capturedRobotContentProvider)
        {
            this.capturedRobotContentProvider = capturedRobotContentProvider;
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
