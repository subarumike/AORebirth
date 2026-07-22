namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;

    internal sealed class CapturedIntObservationCursor
    {
        private readonly Dictionary<int, Dictionary<int[], int>> nextIndexesByActor =
            new Dictionary<int, Dictionary<int[], int>>();

        internal int Select(int actorInstance, int[] observations)
        {
            if (observations == null || observations.Length == 0)
            {
                throw new InvalidOperationException("Captured integer observations are required.");
            }

            Dictionary<int[], int> nextIndexes;
            if (!this.nextIndexesByActor.TryGetValue(actorInstance, out nextIndexes))
            {
                nextIndexes = new Dictionary<int[], int>();
                this.nextIndexesByActor[actorInstance] = nextIndexes;
            }

            int index;
            if (!nextIndexes.TryGetValue(observations, out index)
                || index < 0
                || index >= observations.Length)
            {
                index = 0;
            }

            int selected = observations[index];
            nextIndexes[observations] = (index + 1) % observations.Length;
            return selected;
        }

        internal void Clear(int actorInstance)
        {
            this.nextIndexesByActor.Remove(actorInstance);
        }
    }
}
