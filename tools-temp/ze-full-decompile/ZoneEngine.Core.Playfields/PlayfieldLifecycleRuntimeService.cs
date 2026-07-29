using System;
using System.Threading;
using AORebirth.Core.Entities;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Playfields;

internal sealed class PlayfieldLifecycleRuntimeService
{
	internal void PreparePlayfieldTransfer(Dynel dynel, Action<int> clearTransferContactState, Action<Dynel> disableTimers)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		Require(clearTransferContactState, "clearTransferContactState");
		Require(disableTimers, "disableTimers");
		Thread.Sleep(200);
		Identity identity = ((PooledObject)dynel).Identity;
		clearTransferContactState(((Identity)(ref identity)).Instance);
		disableTimers(dynel);
		Thread.Sleep(1000);
	}

	private static void Require(Delegate callback, string name)
	{
		if ((object)callback == null)
		{
			throw new ArgumentNullException(name);
		}
	}
}
