using System;
using System.Collections.Generic;
using AORebirth.Core.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace AORebirth.Core.Playfields;

internal static class AndromedaIccHqIdleGestureRuntime
{
	private sealed class GestureActor
	{
		public ICharacter Character;

		public DateTime NextDueUtc;

		public int CycleIndex;
	}

	private const double IntervalSeconds = 10.0;

	private static readonly int[] NataliaGestureCycle = new int[7] { 62, 2, 25, 62, 62, 62, 2 };

	private static readonly object Sync = new object();

	private static readonly List<GestureActor> Actors = new List<GestureActor>();

	internal static void RegisterNatalia(ICharacter character)
	{
		if (character == null)
		{
			return;
		}
		lock (Sync)
		{
			Actors.RemoveAll((GestureActor a) => a.Character == null || a.Character == character);
			Actors.Add(new GestureActor
			{
				Character = character,
				NextDueUtc = DateTime.UtcNow.AddSeconds(10.0),
				CycleIndex = 0
			});
		}
	}

	internal static void Clear()
	{
		lock (Sync)
		{
			Actors.Clear();
		}
	}

	internal static void ProcessDue(DateTime utcNow)
	{
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Expected O, but got Unknown
		GestureActor[] array;
		lock (Sync)
		{
			Actors.RemoveAll((GestureActor a) => a.Character == null || ((IInstancedEntity)a.Character).Playfield == null);
			array = Actors.ToArray();
		}
		GestureActor[] array2 = array;
		foreach (GestureActor gestureActor in array2)
		{
			if (!(utcNow < gestureActor.NextDueUtc))
			{
				ICharacter character = gestureActor.Character;
				if (character != null && ((IInstancedEntity)character).Playfield != null && ((IStats)character).Stats[(StatIds)27].Value > 0)
				{
					int instance = NataliaGestureCycle[gestureActor.CycleIndex % NataliaGestureCycle.Length];
					gestureActor.CycleIndex++;
					gestureActor.NextDueUtc = utcNow.AddSeconds(10.0);
					IPlayfield playfield = ((IInstancedEntity)character).Playfield;
					CharacterActionMessage val = new CharacterActionMessage
					{
						Identity = ((IEntity)character).Identity,
						Unknown = 0,
						Action = (CharacterActionType)100,
						Unknown1 = 0
					};
					Identity target = default(Identity);
					((Identity)(ref target)).Type = (IdentityType)0;
					((Identity)(ref target)).Instance = instance;
					val.Target = target;
					val.Parameter1 = 0;
					val.Parameter2 = 0;
					val.Unknown2 = 0;
					playfield.Announce((MessageBody)val);
				}
			}
		}
	}
}
