namespace ZoneEngine_New.Core.Playfield.Locality
{
    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;
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

        /// <summary>The occupants for an allocation-free foreach on a hot path (struct enumerator). Never modify it.</summary>
        internal HashSet<Dynel> OccupantSet => _occupants;

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
        /// <paramref name="exclude"/> and <paramref name="alsoExclude"/> already received a direct copy.
        /// </summary>
        public void Announce(MessageBody message, Dynel? exclude = null, Dynel? alsoExclude = null)
        {
            // A message about a player some neighbors cannot see (sneaking past their Perception, or GM-hidden) skips
            // them: the player is despawned on their clients and must not leak moves or actions there.
            Player? concealed = ConcealedSubject(message, exclude, alsoExclude);

            if (!_grid.IsOutdoor && !_grid.IsDungeon)
            {
                SendToOccupants(_grid.OccupantsInAllCells(), message, exclude, alsoExclude, concealed);
                return;
            }

            _grid.CollectNeighbors(Id, _visibilityNeighborLevel, _neighborBuffer);
            foreach (int cellId in _neighborBuffer)
                SendToOccupants(_grid.OccupantsInCell(cellId), message, exclude, alsoExclude, concealed);
        }

        static void SendToOccupants(
            IEnumerable<Dynel> occupants,
            MessageBody message,
            Dynel? exclude,
            Dynel? alsoExclude,
            Player? concealed)
        {
            foreach (Dynel dynel in occupants)
            {
                if (IsExcluded(dynel, exclude, alsoExclude))
                    continue;

                if (dynel is not Player player || player.Session == null)
                    continue;

                if (concealed != null && !ReferenceEquals(player, concealed)
                    && LocalityVisibility.IsHiddenFrom(player, concealed))
                    continue;

                player.Session.Send(message);
            }
        }

        /// <summary>
        /// The player <paramref name="message"/> is about (its N3 identity), when that player is currently concealed
        /// from at least someone; null otherwise. The subject is one of the excluded dynels or an occupant of this cell,
        /// since callers announce from the subject's own cell.
        /// </summary>
        Player? ConcealedSubject(MessageBody message, Dynel? exclude, Dynel? alsoExclude)
        {
            if (message is not N3Message n3)
                return null;

            Identity subject = n3.Identity;
            Player? player = MatchPlayer(exclude, subject) ?? MatchPlayer(alsoExclude, subject);
            if (player == null)
            {
                foreach (Dynel dynel in _occupants)
                {
                    if (dynel is Player occupant && occupant.Identity == subject)
                    {
                        player = occupant;
                        break;
                    }
                }
            }

            return player != null
                   && (player.HiddenFromPlayers || player.Motor.State == Movement.MovementState.Sneak)
                ? player
                : null;
        }

        static Player? MatchPlayer(Dynel? dynel, Identity subject)
            => dynel is Player player && player.Identity == subject ? player : null;

        static bool IsExcluded(Dynel dynel, Dynel? exclude, Dynel? alsoExclude)
            => ReferenceEquals(dynel, exclude) || ReferenceEquals(dynel, alsoExclude);
    }
}
