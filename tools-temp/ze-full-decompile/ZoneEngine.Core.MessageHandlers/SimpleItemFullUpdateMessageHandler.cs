using System;
using System.Collections.Generic;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class SimpleItemFullUpdateMessageHandler : BaseMessageHandler<SimpleItemFullUpdateMessage, SimpleItemFullUpdateMessageHandler>
{
	public void Send(ICharacter character, StaticDynel dynel)
	{
		((AbstractMessageHandler<SimpleItemFullUpdateMessage>)(object)this).Send(character, DynelFiller(dynel), false);
	}

	private MessageDataFiller<SimpleItemFullUpdateMessage> DynelFiller(StaticDynel dynel)
	{
		return delegate(SimpleItemFullUpdateMessage x)
		{
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Expected O, but got Unknown
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_0089: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a5: Expected O, but got Unknown
			//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				x.Coordinate = new Vector3(dynel.Coordinate.x, dynel.Coordinate.y, dynel.Coordinate.z);
				x.Heading = new Quaternion
				{
					X = dynel.Heading.X,
					Y = dynel.Heading.Y,
					Z = dynel.Heading.Z,
					W = dynel.Heading.W
				};
				((N3Message)x).Identity = ((PooledObject)dynel).Identity;
				x.Owner = Identity.None;
				Identity unknown = ((PooledObject)dynel).Parent;
				x.Playfield = ((Identity)(ref unknown)).Instance;
				unknown = default(Identity);
				((Identity)(ref unknown)).Type = (IdentityType)1000015;
				((Identity)(ref unknown)).Instance = 0;
				x.Unknown1 = unknown;
				x.Unknown2 = 0;
				x.Unknown3 = 111;
				x.MsgVersion = 11;
				((N3Message)x).Unknown = 0;
				List<GameTuple<CharacterStat, uint>> list = new List<GameTuple<CharacterStat, uint>>();
				foreach (KeyValuePair<int, int> stat in dynel.Stats)
				{
					list.Add(new GameTuple<CharacterStat, uint>
					{
						Value1 = (CharacterStat)stat.Key,
						Value2 = (uint)stat.Value
					});
				}
				x.Stats = list.ToArray();
				x.Name = "";
			}
			catch (Exception ex)
			{
				LogUtil.Debug((DebugInfoDetail)512, ex.Message + ex.StackTrace);
			}
		};
	}
}
