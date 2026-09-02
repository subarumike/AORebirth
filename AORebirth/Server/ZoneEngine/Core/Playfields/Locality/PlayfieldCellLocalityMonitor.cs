namespace ZoneEngine.Core.Playfields.Locality
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.Controllers;

    internal sealed class PlayfieldCellLocalityMonitor
    {
        private readonly IPlayfieldCellLayout layout;
        private readonly PlayfieldLocalityPolicy policy;
        private readonly PlayfieldDynelCellRegistry cells;
        private readonly PlayfieldCellResourceHub resourceHub;
        private readonly HashSet<int> desired = new HashSet<int>();
        private readonly HashSet<int> nextDesired = new HashSet<int>();
        private readonly List<int> neighborBuffer = new List<int>();
        private readonly List<int> foundBuffer = new List<int>();
        private readonly List<int> lostBuffer = new List<int>();

        internal PlayfieldCellLocalityMonitor(
            IPlayfieldCellLayout layout,
            PlayfieldLocalityPolicy policy,
            PlayfieldDynelCellRegistry cells,
            PlayfieldCellResourceHub resourceHub)
        {
            this.layout = layout;
            this.policy = policy;
            this.cells = cells;
            this.resourceHub = resourceHub;
        }

        internal void UpdatePlayers(IEnumerable<ICharacter> players)
        {
            if (this.layout.IsIndoor)
            {
                this.ClearDesired();
                return;
            }

            this.nextDesired.Clear();
            if (players != null)
            {
                foreach (ICharacter player in players)
                {
                    if (player == null || player.Controller == null || player.Controller.Client == null)
                    {
                        continue;
                    }

                    int cellId;
                    if (!this.cells.TryGetCellId(player, out cellId) || cellId < 0)
                    {
                        continue;
                    }

                    this.cells.CollectNeighborCells(cellId, this.policy.VisibilityNeighborLevel, this.neighborBuffer);
                    foreach (int neighbor in this.neighborBuffer)
                    {
                        this.nextDesired.Add(neighbor);
                    }
                }
            }

            this.foundBuffer.Clear();
            this.lostBuffer.Clear();
            foreach (int id in this.nextDesired)
            {
                if (!this.desired.Contains(id))
                {
                    this.foundBuffer.Add(id);
                }
            }

            foreach (int id in this.desired)
            {
                if (!this.nextDesired.Contains(id))
                {
                    this.lostBuffer.Add(id);
                }
            }

            this.desired.Clear();
            foreach (int id in this.nextDesired)
            {
                this.desired.Add(id);
            }

            if (this.foundBuffer.Count > 0)
            {
                this.resourceHub.NotifyCellsFound(this.foundBuffer);
            }

            if (this.lostBuffer.Count > 0)
            {
                this.resourceHub.NotifyCellsLost(this.lostBuffer);
            }
        }

        internal void Clear()
        {
            this.ClearDesired();
            this.resourceHub.Clear();
        }

        private void ClearDesired()
        {
            if (this.desired.Count > 0)
            {
                this.lostBuffer.Clear();
                this.lostBuffer.AddRange(this.desired);
                this.resourceHub.NotifyCellsLost(this.lostBuffer);
                this.desired.Clear();
            }
        }
    }
}
