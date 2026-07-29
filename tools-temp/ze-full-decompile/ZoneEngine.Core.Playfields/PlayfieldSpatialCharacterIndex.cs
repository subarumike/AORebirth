using System;
using System.Collections.Generic;
using AORebirth.Core.Entities;
using AORebirth.Core.Vector;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Playfields;

internal sealed class PlayfieldSpatialCharacterIndex
{
	private readonly UniformSpatialIndex<ICharacter> index;

	internal int Count => index.Count;

	internal int LastCandidateInspectionCount => index.LastCandidateInspectionCount;

	internal UniformSpatialIndex<ICharacter> InnerIndex => index;

	internal PlayfieldSpatialCharacterIndex(float cellSize)
	{
		index = new UniformSpatialIndex<ICharacter>(cellSize);
	}

	internal PlayfieldSpatialCharacterIndex(PlayfieldVisibilityInterestPolicy policy)
		: this(RequirePolicy(policy).CellSize)
	{
	}

	internal void Upsert(ICharacter character)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		if (character == null)
		{
			throw new ArgumentNullException("character");
		}
		Coordinate val = ((IDynel)character).Coordinates();
		index.Upsert(((IEntity)character).Identity, new VisibilityPosition(val.x, val.y, val.z), character);
	}

	internal bool Remove(Identity identity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return index.Remove(identity);
	}

	internal IReadOnlyList<ICharacter> Query(Coordinate center, float radius)
	{
		if (center == null)
		{
			throw new ArgumentNullException("center");
		}
		return index.Query(new VisibilityPosition(center.x, center.y, center.z), radius);
	}

	internal void Clear()
	{
		index.Clear();
	}

	private static PlayfieldVisibilityInterestPolicy RequirePolicy(PlayfieldVisibilityInterestPolicy policy)
	{
		if (policy == null)
		{
			throw new ArgumentNullException("policy");
		}
		return policy;
	}
}
