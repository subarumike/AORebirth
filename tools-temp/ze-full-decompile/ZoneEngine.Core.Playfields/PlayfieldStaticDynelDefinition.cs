using System.Collections.Generic;
using AORebirth.Core.Items;
using AORebirth.Core.Vector;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Playfields;

internal sealed class PlayfieldStaticDynelDefinition
{
	internal Identity Identity { get; private set; }

	internal ItemTemplate Template { get; private set; }

	internal List<GameTuple<CharacterStat, uint>> Stats { get; private set; }

	internal Coordinate Coordinate { get; private set; }

	internal Quaternion Heading { get; private set; }

	internal PlayfieldStaticDynelDefinition(Identity identity, ItemTemplate template, List<GameTuple<CharacterStat, uint>> stats, Coordinate coordinate, Quaternion heading)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		Identity = identity;
		Template = template;
		Stats = stats;
		Coordinate = coordinate;
		Heading = heading;
	}
}
