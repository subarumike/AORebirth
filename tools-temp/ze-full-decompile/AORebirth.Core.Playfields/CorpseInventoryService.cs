using System;
using System.Collections.Generic;
using System.Linq;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace AORebirth.Core.Playfields;

internal sealed class CorpseInventoryService
{
	private readonly Dictionary<int, CorpseState> states = new Dictionary<int, CorpseState>();

	private readonly object sync = new object();

	internal IDictionary<int, CorpseState> States => states;

	internal CorpseState Create(CorpseState state)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Invalid comparison between Unknown and I4
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		if (state != null)
		{
			Identity corpseIdentity = state.CorpseIdentity;
			if ((int)((Identity)(ref corpseIdentity)).Type == 51050)
			{
				lock (sync)
				{
					Dictionary<int, CorpseState> dictionary = states;
					corpseIdentity = state.CorpseIdentity;
					if (dictionary.ContainsKey(((Identity)(ref corpseIdentity)).Instance))
					{
						corpseIdentity = state.CorpseIdentity;
						throw new InvalidOperationException("Duplicate corpse identity: " + ((object)(Identity)(ref corpseIdentity)).ToString());
					}
					state.LootItems = state.LootItems ?? new List<CorpseLootItem>();
					state.VisibleRecipients = state.VisibleRecipients ?? new HashSet<Identity>();
					state.LastMutationAtUtc = state.CreatedAtUtc;
					Dictionary<int, CorpseState> dictionary2 = states;
					corpseIdentity = state.CorpseIdentity;
					dictionary2.Add(((Identity)(ref corpseIdentity)).Instance, state);
					return state;
				}
			}
		}
		throw new ArgumentException("A corpse identity is required.", "state");
	}

	internal bool TryGet(Identity corpseIdentity, out CorpseState state)
	{
		lock (sync)
		{
			return states.TryGetValue(((Identity)(ref corpseIdentity)).Instance, out state);
		}
	}

	internal CorpseState Get(Identity corpseIdentity)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		CorpseState state;
		return TryGet(corpseIdentity, out state) ? state : null;
	}

	internal CorpseLootItem[] EnumerateItems(Identity corpseIdentity)
	{
		lock (sync)
		{
			CorpseState value;
			return states.TryGetValue(((Identity)(ref corpseIdentity)).Instance, out value) ? (from x in value.LootItems
				where !x.Looted
				orderby x.Slot
				select x).ToArray() : new CorpseLootItem[0];
		}
	}

	internal bool RemoveItem(Identity corpseIdentity, int slot, DateTime mutationAtUtc)
	{
		lock (sync)
		{
			if (!states.TryGetValue(((Identity)(ref corpseIdentity)).Instance, out var value))
			{
				return false;
			}
			CorpseLootItem corpseLootItem = value.LootItems.FirstOrDefault((CorpseLootItem x) => x.Slot == slot && !x.Looted);
			if (corpseLootItem == null)
			{
				return false;
			}
			corpseLootItem.Looted = true;
			value.LastMutationAtUtc = mutationAtUtc;
			return true;
		}
	}

	internal bool RemoveCredits(Identity corpseIdentity, DateTime mutationAtUtc)
	{
		lock (sync)
		{
			if (!states.TryGetValue(((Identity)(ref corpseIdentity)).Instance, out var value) || value.CreditsLooted || value.Credits <= 0)
			{
				return false;
			}
			value.CreditsLooted = true;
			value.LastMutationAtUtc = mutationAtUtc;
			return true;
		}
	}

	internal void MarkOpened(Identity corpseIdentity, bool opened, DateTime mutationAtUtc)
	{
		lock (sync)
		{
			if (states.TryGetValue(((Identity)(ref corpseIdentity)).Instance, out var value))
			{
				value.Opened = opened;
				value.LastMutationAtUtc = mutationAtUtc;
			}
		}
	}

	internal bool IsEmpty(Identity corpseIdentity)
	{
		lock (sync)
		{
			CorpseState value;
			return states.TryGetValue(((Identity)(ref corpseIdentity)).Instance, out value) && value.IsEmpty;
		}
	}

	internal bool Remove(int corpseInstance)
	{
		lock (sync)
		{
			return states.Remove(corpseInstance);
		}
	}

	internal int ClearPlayfield(int playfieldId)
	{
		lock (sync)
		{
			int[] array = (from x in states
				where x.Value.PlayfieldId == playfieldId
				select x.Key).ToArray();
			int[] array2 = array;
			foreach (int key in array2)
			{
				states.Remove(key);
			}
			return array.Length;
		}
	}

	internal void ClearAll()
	{
		lock (sync)
		{
			states.Clear();
		}
	}
}
