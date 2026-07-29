using System;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine.Core.Functions;
using ZoneEngine.Core.InternalMessages;

namespace ZoneEngine.Core.Playfields;

internal sealed class PlayfieldEnvironmentFunctionRuntimeService
{
	internal void ExecuteFunction(IMExecuteFunction imExecuteFunction, Func<Identity, INamedEntity> findNamedEntity, Action<Character, string> sendNoValidTargetMessage)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Expected O, but got Unknown
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Expected O, but got Unknown
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Expected O, but got Unknown
		//IL_015d: Expected O, but got Unknown
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Expected O, but got Unknown
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Expected O, but got Unknown
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Expected O, but got Unknown
		ITargetingEntity val = (ITargetingEntity)findNamedEntity(imExecuteFunction.User);
		INamedEntity val2 = (INamedEntity)(imExecuteFunction.Function.Target switch
		{
			1 => (object)(INamedEntity)val, 
			2 => throw new NotImplementedException("Target Wearer not implemented yet"), 
			3 => findNamedEntity(val.SelectedTarget), 
			14 => findNamedEntity(val.FightingTarget), 
			19 => (object)(INamedEntity)val, 
			23 => findNamedEntity(val.SelectedTarget), 
			26 => (object)(INamedEntity)val, 
			100 => (object)(INamedEntity)val, 
			_ => throw new NotImplementedException("Unknown target encountered: Target#:" + imExecuteFunction.Function.Target), 
		});
		if (val2 == null)
		{
			Character val3 = (Character)(object)((val is Character) ? val : null);
			if (val3 != null)
			{
				if (((Dynel)val3).Controller.Client != null)
				{
					sendNoValidTargetMessage(val3, "No valid target found");
				}
				return;
			}
		}
		FunctionCollection.Instance.CallFunction(imExecuteFunction.Function.FunctionType, (INamedEntity)val, (IEntity)(INamedEntity)val, (IInstancedEntity)(object)val2, imExecuteFunction.Function.Arguments.Values.ToArray());
	}
}
