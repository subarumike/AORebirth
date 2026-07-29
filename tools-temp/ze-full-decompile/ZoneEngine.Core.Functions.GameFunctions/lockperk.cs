using System;
using System.Threading;
using AORebirth.Core.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using MsgPack;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class lockperk : FunctionPrototype
{
	public override FunctionType FunctionId => (FunctionType)53187;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Expected O, but got Unknown
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		Character character = (Character)(object)((self is Character) ? self : null);
		if (character == null || ((Dynel)character).Controller == null || ((Dynel)character).Controller.Client == null)
		{
			return false;
		}
		if (!TryReadArguments(arguments, out var packetId, out var durationSeconds))
		{
			return false;
		}
		character.LockPerkPacket(packetId, durationSeconds);
		((Dynel)character).Controller.Client.SendCompressed((MessageBody)new CharacterActionMessage
		{
			Identity = ((PooledObject)character).Identity,
			Unknown = 0,
			Action = (CharacterActionType)207,
			Unknown1 = 0,
			Target = Identity.None,
			Parameter1 = packetId,
			Parameter2 = 1,
			Unknown2 = 0
		});
		int delayMs = Math.Max(1, durationSeconds) * 1000;
		ThreadPool.QueueUserWorkItem(delegate
		{
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_007a: Unknown result type (might be due to invalid IL or missing references)
			//IL_007b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0086: Unknown result type (might be due to invalid IL or missing references)
			//IL_008e: Unknown result type (might be due to invalid IL or missing references)
			//IL_009b: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a8: Expected O, but got Unknown
			Thread.Sleep(delayMs);
			if (((Dynel)character).Controller != null && ((Dynel)character).Controller.Client != null)
			{
				((Dynel)character).Controller.Client.SendCompressed((MessageBody)new CharacterActionMessage
				{
					Identity = ((PooledObject)character).Identity,
					Unknown = 0,
					Action = (CharacterActionType)206,
					Unknown1 = 0,
					Target = Identity.None,
					Parameter1 = 0,
					Parameter2 = packetId,
					Unknown2 = 0
				});
			}
		});
		LogUtil.Debug((DebugInfoDetail)256, $"LockPerk char={((PooledObject)character).Identity} packetId={packetId} duration={durationSeconds}s");
		return true;
	}

	internal static bool TryReadArguments(MessagePackObject[] arguments, out int packetId, out int durationSeconds)
	{
		packetId = 0;
		durationSeconds = 0;
		if (arguments == null || arguments.Length < 2)
		{
			return false;
		}
		if (arguments.Length >= 3)
		{
			packetId = ((MessagePackObject)(ref arguments[1])).AsInt32();
			durationSeconds = ((MessagePackObject)(ref arguments[2])).AsInt32();
			return packetId > 0 && durationSeconds > 0;
		}
		packetId = ((MessagePackObject)(ref arguments[0])).AsInt32();
		durationSeconds = ((MessagePackObject)(ref arguments[1])).AsInt32();
		return packetId > 0 && durationSeconds > 0;
	}
}
