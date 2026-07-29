using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Playfields;

internal sealed class PlayfieldStaticDynelRuntimeService
{
	internal IEntity CreateStaticDynel(Identity playfieldIdentity, PlayfieldStaticDynelDefinition staticDynel)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected O, but got Unknown
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Expected I4, but got Unknown
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Expected I4, but got Unknown
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Expected I4, but got Unknown
		StaticDynel val = new StaticDynel(playfieldIdentity, staticDynel.Identity, staticDynel.Template);
		foreach (GameTuple<CharacterStat, uint> stat in staticDynel.Stats)
		{
			if (val.Stats.ContainsKey((int)stat.Value1))
			{
				val.Stats[(int)stat.Value1] = (int)stat.Value2;
			}
			else
			{
				val.Stats.Add((int)stat.Value1, (int)stat.Value2);
			}
		}
		val.Coordinate = staticDynel.Coordinate;
		val.Heading = staticDynel.Heading;
		return (IEntity)(object)val;
	}
}
