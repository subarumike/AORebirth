using System;
using System.Collections.Generic;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Playfields;

internal sealed class PlayfieldVisibilityFanoutRuntimeService
{
	internal void AnnounceToCharacterClients(IEnumerable<Character> characters, Action<Character> publishToCharacterClient)
	{
		Require(publishToCharacterClient, "publishToCharacterClient");
		foreach (Character character in characters)
		{
			if (character != null && ((Dynel)character).Controller.Client != null)
			{
				publishToCharacterClient(character);
			}
		}
	}

	internal void AnnounceToOtherCharacterClients(IEnumerable<Character> characters, Identity excludedIdentity, Action<Character> publishToCharacterClient)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		Require(publishToCharacterClient, "publishToCharacterClient");
		foreach (Character character in characters)
		{
			if (character != null && ((PooledObject)character).Identity != excludedIdentity)
			{
				publishToCharacterClient(character);
			}
		}
	}

	internal void FanoutExistingCharactersForScfu(ICharacter recipient, IEnumerable<ICharacter> characters, Func<ICharacter, bool> sendExistingCharacter, Action<ICharacter, bool, bool, bool> logVisibilityCandidate)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		Require(sendExistingCharacter, "sendExistingCharacter");
		Require(logVisibilityCandidate, "logVisibilityCandidate");
		Identity identity = ((IEntity)recipient).Identity;
		Identity identity2 = ((IEntity)((IInstancedEntity)recipient).Playfield).Identity;
		foreach (ICharacter character in characters)
		{
			bool flag = ((IEntity)character).Identity == identity;
			bool flag2 = ((IDynel)character).InPlayfield(identity2);
			bool arg = false;
			if (flag2 && !flag)
			{
				arg = sendExistingCharacter(character);
			}
			bool flag3 = ((IDynel)character).Controller != null && ((IDynel)character).Controller.Client != null;
			if (flag3 || flag)
			{
				logVisibilityCandidate(character, flag, flag2, arg);
			}
		}
	}

	private static void Require(Delegate callback, string name)
	{
		if ((object)callback == null)
		{
			throw new ArgumentNullException(name);
		}
	}
}
