namespace ZoneEngine.Core.Playfields
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Vector;

    using SmokeLounge.AOtomation.Messaging.GameData;

    internal sealed class PlayfieldSpatialCharacterIndex
    {
        private readonly UniformSpatialIndex<ICharacter> index;

        internal PlayfieldSpatialCharacterIndex(float cellSize)
        {
            this.index = new UniformSpatialIndex<ICharacter>(cellSize);
        }

        internal PlayfieldSpatialCharacterIndex(PlayfieldVisibilityInterestPolicy policy)
            : this(RequirePolicy(policy).CellSize)
        {
        }

        internal int Count
        {
            get
            {
                return this.index.Count;
            }
        }

        internal int LastCandidateInspectionCount
        {
            get
            {
                return this.index.LastCandidateInspectionCount;
            }
        }

        internal UniformSpatialIndex<ICharacter> InnerIndex
        {
            get { return this.index; }
        }

        internal void Upsert(ICharacter character)
        {
            if (character == null)
            {
                throw new ArgumentNullException("character");
            }

            Coordinate coordinate = character.Coordinates();
            this.index.Upsert(
                character.Identity,
                new VisibilityPosition(coordinate.x, coordinate.y, coordinate.z),
                character);
        }

        internal bool Remove(Identity identity)
        {
            return this.index.Remove(identity);
        }

        internal IReadOnlyList<ICharacter> Query(Coordinate center, float radius)
        {
            if (center == null)
            {
                throw new ArgumentNullException("center");
            }

            return this.index.Query(
                new VisibilityPosition(center.x, center.y, center.z),
                radius);
        }

        internal void Clear()
        {
            this.index.Clear();
        }

        private static PlayfieldVisibilityInterestPolicy RequirePolicy(
            PlayfieldVisibilityInterestPolicy policy)
        {
            if (policy == null)
            {
                throw new ArgumentNullException("policy");
            }

            return policy;
        }
    }
}
