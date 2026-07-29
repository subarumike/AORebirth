using System.Linq;
using AORebirth.Core.Entities;
using AORebirth.Core.Events;
using AORebirth.Core.Functions;
using AORebirth.Core.Nanos;
using AORebirth.Interfaces;
using MsgPack;

namespace ZoneEngine.Core;

public sealed class NanoEventRuntimeService
{
	private static readonly int SummonPetFunctionId = 53167;

	private static readonly int SummonPetsFunctionId = 53181;

	private static readonly NanoEventRuntimeService DefaultInstance = new NanoEventRuntimeService();

	public static NanoEventRuntimeService Default => DefaultInstance;

	private NanoEventRuntimeService()
	{
	}

	public void ExecuteOnUseEvents(ICharacter character, NanoFormula nano)
	{
		if (character == null || nano == null || nano.Events == null)
		{
			return;
		}
		foreach (Event item in nano.Events.Where((Event x) => (int)x.EventType == 0))
		{
			item.Perform(character, (IEntity)(object)character);
		}
	}

	public bool HasSummonPetOnUse(int nanoId)
	{
		if (PetSummonNanoCatalog.IsCatalogSummonNano(nanoId))
		{
			return true;
		}
		if (!NanoLoader.NanoList.TryGetValue(nanoId, out var value))
		{
			return false;
		}
		return HasSummonPetOnUse(value);
	}

	public bool HasSummonPetOnUse(NanoFormula nano)
	{
		if (nano == null || nano.Events == null)
		{
			return false;
		}
		foreach (Event item in nano.Events.Where((Event x) => (int)x.EventType == 0))
		{
			if (item.Functions == null)
			{
				continue;
			}
			foreach (Function function in item.Functions)
			{
				if (function.FunctionType == SummonPetFunctionId || function.FunctionType == SummonPetsFunctionId)
				{
					return true;
				}
			}
		}
		return false;
	}

	public bool HasOffensiveHitOnUse(NanoFormula nano)
	{
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		if (nano == null || nano.Events == null)
		{
			return false;
		}
		int num = 53002;
		foreach (Event item in nano.Events.Where((Event x) => (int)x.EventType == 0))
		{
			if (item.Functions == null)
			{
				continue;
			}
			foreach (Function function in item.Functions)
			{
				if (function.FunctionType != num || function.Arguments == null || function.Arguments.Values.Count < 2)
				{
					continue;
				}
				MessagePackObject val = function.Arguments.Values[1];
				int num2 = ((MessagePackObject)(ref val)).AsInt32();
				if (num2 < 0)
				{
					return true;
				}
				if (function.Arguments.Values.Count >= 3)
				{
					val = function.Arguments.Values[2];
					if (((MessagePackObject)(ref val)).AsInt32() < 0)
					{
						return true;
					}
				}
			}
		}
		return false;
	}
}
