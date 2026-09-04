namespace ZoneEngine_New.Core.Playfield.Locality
{
    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.Messages;

    using ZoneEngine_New.Core.Entities;

    public sealed class Cell
    {
        private readonly HashSet<Dynel> _occupants = [];
        private readonly CellGrid _grid;
        private readonly int _visibilityNeighborLevel;
        private readonly List<int> _neighborBuffer = [];

        public int Id { get; }

        internal Cell(int id, CellGrid grid, int visibilityNeighborLevel)
        {
            Id = id;
            _grid = grid;
            _visibilityNeighborLevel = visibilityNeighborLevel;
        }

        public IReadOnlyCollection<Dynel> Occupants => _occupants;

        internal int OccupantCount => _occupants.Count;

        internal void Add(Dynel dynel)
        {
            _occupants.Add(dynel);
        }

        internal void Remove(Dynel dynel)
        {
            _occupants.Remove(dynel);
        }

        /// <summary>
        /// Sends <paramref name="message"/> to connected players who can see this cell
        /// (this cell plus visibility-neighbor cells). Indoor playfields use the single cell.
        /// </summary>
        public void Announce(MessageBody message, Dynel? exclude = null)
        {
            if (!_grid.IsOutdoor)
            {
                SendToOccupants(_occupants, message, exclude);
                return;
            }

            _grid.CollectNeighbors(Id, _visibilityNeighborLevel, _neighborBuffer);
            foreach (int cellId in _neighborBuffer)
            {
                foreach (Dynel dynel in _grid.OccupantsInCell(cellId))
                {
                    if (ReferenceEquals(dynel, exclude))
                        continue;

                    if (dynel is Player player && player.Session != null)
                        player.Session.Send(message);
                }
            }
        }

        static void SendToOccupants(
            IEnumerable<Dynel> occupants,
            MessageBody message,
            Dynel? exclude)
        {
            foreach (Dynel dynel in occupants)
            {
                if (ReferenceEquals(dynel, exclude))
                    continue;

                if (dynel is Player player && player.Session != null)
                    player.Session.Send(message);
            }
        }
    }
}
