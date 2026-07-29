using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AORebirth.Core.Entities;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Playfields;

internal sealed class PlayfieldVisibilityInterestRuntimeService
{
	private readonly PlayfieldVisibilityInterestPolicy policy;

	private readonly PlayfieldVisibilityInterestState<ICharacter> state;

	internal PlayfieldVisibilityInterestPolicy Policy => policy;

	internal int LastCandidateInspectionCount => state.LastCandidateInspectionCount;

	internal PlayfieldVisibilityInterestRuntimeService(PlayfieldVisibilityInterestPolicy policy, PlayfieldSpatialCharacterIndex spatialIndex)
	{
		if (policy == null)
		{
			throw new ArgumentNullException("policy");
		}
		if (spatialIndex == null)
		{
			throw new ArgumentNullException("spatialIndex");
		}
		this.policy = policy;
		state = new PlayfieldVisibilityInterestState<ICharacter>(policy, spatialIndex.InnerIndex, IdentityOf, PositionOf, CanShareVisibility, IsConnectedRecipient, IsPinnedVisibility);
	}

	internal void Register(ICharacter character)
	{
		state.Register(character);
	}

	internal void Unregister(Identity identity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		state.Unregister(identity);
	}

	internal void Synchronize(IEnumerable<ICharacter> characters)
	{
		state.Synchronize(characters);
	}

	internal ReadOnlyCollection<ICharacter> SelectInitialCharacters(ICharacter recipient)
	{
		if (recipient == null || ((IInstancedEntity)recipient).Playfield == null)
		{
			return new List<ICharacter>().AsReadOnly();
		}
		return state.SelectInitialValues(recipient);
	}

	internal bool MarkVisibleEntry(ICharacter recipient, ICharacter source)
	{
		return state.MarkVisibleEntry(recipient, source);
	}

	internal void CompleteInitialRecipient(ICharacter recipient)
	{
		state.CompleteInitialRecipient(recipient);
	}

	internal bool IsInitializedRecipient(Identity recipientIdentity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return state.IsInitializedRecipient(recipientIdentity);
	}

	internal void ReconcileInitializedRecipients(ICharacter changedCharacter, Func<ICharacter, ICharacter, bool> enterVisibility, Action<ICharacter, Identity> leaveVisibility)
	{
		state.ReconcileInitializedRecipients(changedCharacter, enterVisibility, leaveVisibility);
	}

	internal ReadOnlyCollection<ICharacter> VisibleRecipientsForSource(Identity sourceIdentity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return state.VisibleRecipientsForSource(sourceIdentity);
	}

	internal ReadOnlyCollection<ICharacter> VisibleSourcesForRecipient(Identity recipientIdentity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return state.VisibleSourcesForRecipient(recipientIdentity);
	}

	internal bool CanReceive(ICharacter source, ICharacter recipient)
	{
		return state.CanReceive(source, recipient);
	}

	internal void ForgetRecipient(Identity recipientIdentity)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		state.ForgetRecipient(recipientIdentity);
	}

	internal void Clear()
	{
		state.Clear();
	}

	private static Identity IdentityOf(ICharacter character)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		return ((IEntity)character).Identity;
	}

	private static VisibilityPosition PositionOf(ICharacter character)
	{
		Coordinate val = ((IDynel)character).Coordinates();
		return new VisibilityPosition(val.x, val.y, val.z);
	}

	private static bool CanShareVisibility(ICharacter recipient, ICharacter source)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		return recipient != null && source != null && ((IEntity)recipient).Identity != ((IEntity)source).Identity && ((IInstancedEntity)recipient).Playfield != null && ((IDynel)source).InPlayfield(((IEntity)((IInstancedEntity)recipient).Playfield).Identity);
	}

	private static bool IsConnectedRecipient(ICharacter character)
	{
		return character != null && ((IDynel)character).Controller != null && ((IDynel)character).Controller.Client != null;
	}

	private static bool IsPinnedVisibility(ICharacter recipient, ICharacter source)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		int result;
		if (recipient != null && source != null && ((IStats)source).Stats[(StatIds)196].Value > 0)
		{
			int value = ((IStats)source).Stats[(StatIds)196].Value;
			Identity identity = ((IEntity)recipient).Identity;
			result = ((value == ((Identity)(ref identity)).Instance) ? 1 : 0);
		}
		else
		{
			result = 0;
		}
		return (byte)result != 0;
	}
}
