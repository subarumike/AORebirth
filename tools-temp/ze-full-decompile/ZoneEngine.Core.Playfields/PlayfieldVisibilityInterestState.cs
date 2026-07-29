using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Playfields;

internal sealed class PlayfieldVisibilityInterestState<TValue> where TValue : class
{
	private readonly object sync = new object();

	private readonly PlayfieldVisibilityInterestPolicy policy;

	private readonly UniformSpatialIndex<TValue> spatialIndex;

	private readonly Func<TValue, Identity> identityOf;

	private readonly Func<TValue, VisibilityPosition> positionOf;

	private readonly Func<TValue, TValue, bool> canShareVisibility;

	private readonly Func<TValue, bool> isActiveRecipient;

	private readonly Func<TValue, TValue, bool> isPinnedVisibility;

	private readonly Dictionary<ulong, TValue> valuesByIdentity = new Dictionary<ulong, TValue>();

	private readonly Dictionary<ulong, HashSet<ulong>> visibleSourcesByRecipient = new Dictionary<ulong, HashSet<ulong>>();

	private readonly Dictionary<ulong, HashSet<ulong>> visibleRecipientsBySource = new Dictionary<ulong, HashSet<ulong>>();

	private readonly HashSet<ulong> initializedRecipients = new HashSet<ulong>();

	internal int LastCandidateInspectionCount => spatialIndex.LastCandidateInspectionCount;

	internal PlayfieldVisibilityInterestState(PlayfieldVisibilityInterestPolicy policy, UniformSpatialIndex<TValue> spatialIndex, Func<TValue, Identity> identityOf, Func<TValue, VisibilityPosition> positionOf, Func<TValue, TValue, bool> canShareVisibility, Func<TValue, bool> isActiveRecipient, Func<TValue, TValue, bool> isPinnedVisibility)
	{
		this.policy = Require(policy, "policy");
		this.spatialIndex = Require(spatialIndex, "spatialIndex");
		this.identityOf = Require(identityOf, "identityOf");
		this.positionOf = Require(positionOf, "positionOf");
		this.canShareVisibility = Require(canShareVisibility, "canShareVisibility");
		this.isActiveRecipient = Require(isActiveRecipient, "isActiveRecipient");
		this.isPinnedVisibility = Require(isPinnedVisibility, "isPinnedVisibility");
	}

	internal void Register(TValue value)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		if (value == null)
		{
			return;
		}
		Identity identity = identityOf(value);
		spatialIndex.Upsert(identity, positionOf(value), value);
		lock (sync)
		{
			valuesByIdentity[((Identity)(ref identity)).Long()] = value;
		}
	}

	internal void Unregister(Identity identity)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		ulong num = ((Identity)(ref identity)).Long();
		spatialIndex.Remove(identity);
		lock (sync)
		{
			valuesByIdentity.Remove(num);
			RemoveRecipientStateUnlocked(num);
			RemoveSourceStateUnlocked(num);
		}
	}

	internal void Synchronize(IEnumerable<TValue> values)
	{
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0177: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		if (values == null)
		{
			throw new ArgumentNullException("values");
		}
		List<TValue> list = values.Where((TValue value) => value != null).ToList();
		HashSet<ulong> currentIdentities = new HashSet<ulong>(list.Select(delegate(TValue value)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			Identity val2 = identityOf(value);
			return ((Identity)(ref val2)).Long();
		}));
		foreach (TValue item in list)
		{
			spatialIndex.Upsert(identityOf(item), positionOf(item), item);
		}
		List<Identity> list2;
		lock (sync)
		{
			list2 = (from value in valuesByIdentity
				where !currentIdentities.Contains(value.Key)
				select identityOf(value.Value)).ToList();
			foreach (TValue item2 in list)
			{
				Dictionary<ulong, TValue> dictionary = valuesByIdentity;
				Identity val = identityOf(item2);
				dictionary[((Identity)(ref val)).Long()] = item2;
			}
		}
		foreach (Identity item3 in list2)
		{
			Unregister(item3);
		}
	}

	internal ReadOnlyCollection<TValue> SelectInitialValues(TValue recipient)
	{
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		if (recipient == null)
		{
			return new List<TValue>().AsReadOnly();
		}
		Register(recipient);
		Identity recipientIdentity = identityOf(recipient);
		VisibilityPosition recipientPosition = positionOf(recipient);
		return (from source in spatialIndex.Query(recipientPosition, policy.EnterRadius)
			where source != null && identityOf(source) != recipientIdentity && canShareVisibility(recipient, source)
			orderby DistanceSquared(recipientPosition, positionOf(source))
			select source).ThenBy(delegate(TValue source)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Expected I4, but got Unknown
			Identity val2 = identityOf(source);
			return (int)((Identity)(ref val2)).Type;
		}).ThenBy(delegate(TValue source)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			Identity val = identityOf(source);
			return ((Identity)(ref val)).Instance;
		}).ToList()
			.AsReadOnly();
	}

	internal bool MarkVisibleEntry(TValue recipient, TValue source)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		if (!CanShare(recipient, source))
		{
			return false;
		}
		Identity val = identityOf(recipient);
		ulong num = ((Identity)(ref val)).Long();
		val = identityOf(source);
		ulong num2 = ((Identity)(ref val)).Long();
		lock (sync)
		{
			valuesByIdentity[num] = recipient;
			valuesByIdentity[num2] = source;
			HashSet<ulong> orCreate = GetOrCreate(visibleSourcesByRecipient, num);
			if (!orCreate.Add(num2))
			{
				return false;
			}
			GetOrCreate(visibleRecipientsBySource, num2).Add(num);
			return true;
		}
	}

	internal void CompleteInitialRecipient(TValue recipient)
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		if (recipient == null)
		{
			return;
		}
		Register(recipient);
		lock (sync)
		{
			Identity val = identityOf(recipient);
			ulong num = ((Identity)(ref val)).Long();
			initializedRecipients.Add(num);
			GetOrCreate(visibleSourcesByRecipient, num);
		}
	}

	internal bool IsInitializedRecipient(Identity recipientIdentity)
	{
		lock (sync)
		{
			return initializedRecipients.Contains(((Identity)(ref recipientIdentity)).Long());
		}
	}

	internal void ReconcileInitializedRecipients(TValue changedValue, Func<TValue, TValue, bool> enterVisibility, Action<TValue, Identity> leaveVisibility)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		if (changedValue != null)
		{
			Require(enterVisibility, "enterVisibility");
			Require(leaveVisibility, "leaveVisibility");
			Register(changedValue);
			Identity recipientIdentity = identityOf(changedValue);
			if (IsInitializedRecipient(recipientIdentity) && isActiveRecipient(changedValue))
			{
				ReconcileRecipient(changedValue, enterVisibility, leaveVisibility);
			}
			ReconcileSource(changedValue, enterVisibility, leaveVisibility);
		}
	}

	internal ReadOnlyCollection<TValue> VisibleRecipientsForSource(Identity sourceIdentity)
	{
		List<TValue> list;
		lock (sync)
		{
			if (!visibleRecipientsBySource.TryGetValue(((Identity)(ref sourceIdentity)).Long(), out var value))
			{
				return new List<TValue>().AsReadOnly();
			}
			list = value.Select(ValueOrNullUnlocked).Where(delegate(TValue recipient)
			{
				//IL_0023: Unknown result type (might be due to invalid IL or missing references)
				//IL_0028: Unknown result type (might be due to invalid IL or missing references)
				int result;
				if (recipient != null && isActiveRecipient(recipient))
				{
					HashSet<ulong> hashSet = initializedRecipients;
					Identity val3 = identityOf(recipient);
					result = (hashSet.Contains(((Identity)(ref val3)).Long()) ? 1 : 0);
				}
				else
				{
					result = 0;
				}
				return (byte)result != 0;
			}).OrderBy(delegate(TValue recipient)
			{
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				//IL_000f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0015: Expected I4, but got Unknown
				Identity val2 = identityOf(recipient);
				return (int)((Identity)(ref val2)).Type;
			})
				.ThenBy(delegate(TValue recipient)
				{
					//IL_0007: Unknown result type (might be due to invalid IL or missing references)
					//IL_000c: Unknown result type (might be due to invalid IL or missing references)
					Identity val = identityOf(recipient);
					return ((Identity)(ref val)).Instance;
				})
				.ToList();
		}
		return list.AsReadOnly();
	}

	internal ReadOnlyCollection<TValue> VisibleSourcesForRecipient(Identity recipientIdentity)
	{
		List<TValue> list;
		lock (sync)
		{
			if (!visibleSourcesByRecipient.TryGetValue(((Identity)(ref recipientIdentity)).Long(), out var value))
			{
				return new List<TValue>().AsReadOnly();
			}
			list = (from source in value.Select(ValueOrNullUnlocked)
				where source != null
				select source).OrderBy(delegate(TValue source)
			{
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				//IL_000f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0015: Expected I4, but got Unknown
				Identity val2 = identityOf(source);
				return (int)((Identity)(ref val2)).Type;
			}).ThenBy(delegate(TValue source)
			{
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				Identity val = identityOf(source);
				return ((Identity)(ref val)).Instance;
			}).ToList();
		}
		return list.AsReadOnly();
	}

	internal bool CanReceive(TValue source, TValue recipient)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		if (!CanShare(recipient, source))
		{
			return false;
		}
		lock (sync)
		{
			Identity val = identityOf(recipient);
			ulong num = ((Identity)(ref val)).Long();
			int result;
			if (initializedRecipients.Contains(num) && visibleSourcesByRecipient.TryGetValue(num, out var value))
			{
				HashSet<ulong> hashSet = value;
				val = identityOf(source);
				result = (hashSet.Contains(((Identity)(ref val)).Long()) ? 1 : 0);
			}
			else
			{
				result = 0;
			}
			return (byte)result != 0;
		}
	}

	internal void ForgetRecipient(Identity recipientIdentity)
	{
		lock (sync)
		{
			RemoveRecipientStateUnlocked(((Identity)(ref recipientIdentity)).Long());
		}
	}

	internal void Clear()
	{
		spatialIndex.Clear();
		lock (sync)
		{
			valuesByIdentity.Clear();
			visibleSourcesByRecipient.Clear();
			visibleRecipientsBySource.Clear();
			initializedRecipients.Clear();
		}
	}

	private void ReconcileRecipient(TValue recipient, Func<TValue, TValue, bool> enterVisibility, Action<TValue, Identity> leaveVisibility)
	{
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dd: Unknown result type (might be due to invalid IL or missing references)
		VisibilityPosition recipientPosition = positionOf(recipient);
		List<TValue> source2 = (from source in spatialIndex.Query(recipientPosition, policy.LeaveRadius)
			where CanShare(recipient, source)
			select source).ToList();
		Dictionary<ulong, TValue> candidatesByIdentity = source2.ToDictionary(delegate(TValue source)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			Identity val8 = identityOf(source);
			return ((Identity)(ref val8)).Long();
		}, (TValue source) => source);
		HashSet<ulong> currentlyVisible;
		lock (sync)
		{
			Dictionary<ulong, HashSet<ulong>> dictionary = visibleSourcesByRecipient;
			Identity val = identityOf(recipient);
			currentlyVisible = (dictionary.TryGetValue(((Identity)(ref val)).Long(), out var value) ? new HashSet<ulong>(value) : new HashSet<ulong>());
		}
		List<TValue> list = (from source in source2.Where(delegate(TValue source)
			{
				//IL_0012: Unknown result type (might be due to invalid IL or missing references)
				//IL_0017: Unknown result type (might be due to invalid IL or missing references)
				HashSet<ulong> hashSet = currentlyVisible;
				Identity val7 = identityOf(source);
				return !hashSet.Contains(((Identity)(ref val7)).Long()) && Distance(recipient, source) <= (double)policy.EnterRadius;
			})
			orderby DistanceSquared(recipientPosition, positionOf(source))
			select source).ThenBy(delegate(TValue source)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Expected I4, but got Unknown
			Identity val6 = identityOf(source);
			return (int)((Identity)(ref val6)).Type;
		}).ThenBy(delegate(TValue source)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			Identity val5 = identityOf(source);
			return ((Identity)(ref val5)).Instance;
		}).ToList();
		List<TValue> list2 = currentlyVisible.Select(ValueForKey).Where(delegate(TValue source)
		{
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			int result;
			if (source != null && !isPinnedVisibility(recipient, source))
			{
				Dictionary<ulong, TValue> dictionary2 = candidatesByIdentity;
				Identity val4 = identityOf(source);
				result = ((!dictionary2.ContainsKey(((Identity)(ref val4)).Long()) || Distance(recipient, source) > (double)policy.LeaveRadius) ? 1 : 0);
			}
			else
			{
				result = 0;
			}
			return (byte)result != 0;
		}).OrderBy(delegate(TValue source)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Expected I4, but got Unknown
			Identity val3 = identityOf(source);
			return (int)((Identity)(ref val3)).Type;
		})
			.ThenBy(delegate(TValue source)
			{
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0011: Unknown result type (might be due to invalid IL or missing references)
				Identity val2 = identityOf(source);
				return ((Identity)(ref val2)).Instance;
			})
			.ToList();
		foreach (TValue item in list2)
		{
			leaveVisibility(recipient, identityOf(item));
			RemoveVisibleEntry(identityOf(recipient), identityOf(item));
		}
		foreach (TValue item2 in list)
		{
			if (MarkVisibleEntry(recipient, item2))
			{
				DeliverReservedEntry(recipient, item2, enterVisibility);
			}
		}
	}

	private void ReconcileSource(TValue source, Func<TValue, TValue, bool> enterVisibility, Action<TValue, Identity> leaveVisibility)
	{
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		List<TValue> list;
		lock (sync)
		{
			Identity sourceIdentity = identityOf(source);
			list = (from recipient in initializedRecipients.Select(ValueOrNullUnlocked)
				where recipient != null && isActiveRecipient(recipient) && identityOf(recipient) != sourceIdentity
				orderby DistanceSquared(positionOf(recipient), positionOf(source))
				select recipient).ThenBy(delegate(TValue recipient)
			{
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0011: Unknown result type (might be due to invalid IL or missing references)
				//IL_0014: Unknown result type (might be due to invalid IL or missing references)
				//IL_001a: Expected I4, but got Unknown
				Identity val2 = identityOf(recipient);
				return (int)((Identity)(ref val2)).Type;
			}).ThenBy(delegate(TValue recipient)
			{
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0011: Unknown result type (might be due to invalid IL or missing references)
				Identity val = identityOf(recipient);
				return ((Identity)(ref val)).Instance;
			}).ToList();
		}
		foreach (TValue item in list)
		{
			bool flag = CanReceive(source, item);
			double num = Distance(item, source);
			bool flag2 = isPinnedVisibility(item, source);
			if (flag && (!CanShare(item, source) || (num > (double)policy.LeaveRadius && !flag2)))
			{
				leaveVisibility(item, identityOf(source));
				RemoveVisibleEntry(identityOf(item), identityOf(source));
			}
			else if (!flag && (num <= (double)policy.EnterRadius || flag2) && CanShare(item, source) && MarkVisibleEntry(item, source))
			{
				DeliverReservedEntry(item, source, enterVisibility);
			}
		}
	}

	private void DeliverReservedEntry(TValue recipient, TValue source, Func<TValue, TValue, bool> enterVisibility)
	{
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (!enterVisibility(recipient, source))
			{
				RemoveVisibleEntry(identityOf(recipient), identityOf(source));
			}
		}
		catch
		{
			RemoveVisibleEntry(identityOf(recipient), identityOf(source));
			throw;
		}
	}

	private void RemoveVisibleEntry(Identity recipientIdentity, Identity sourceIdentity)
	{
		ulong num = ((Identity)(ref recipientIdentity)).Long();
		ulong num2 = ((Identity)(ref sourceIdentity)).Long();
		lock (sync)
		{
			if (visibleSourcesByRecipient.TryGetValue(num, out var value))
			{
				value.Remove(num2);
			}
			if (visibleRecipientsBySource.TryGetValue(num2, out var value2))
			{
				value2.Remove(num);
				if (value2.Count == 0)
				{
					visibleRecipientsBySource.Remove(num2);
				}
			}
		}
	}

	private void RemoveRecipientStateUnlocked(ulong recipientKey)
	{
		initializedRecipients.Remove(recipientKey);
		if (!visibleSourcesByRecipient.TryGetValue(recipientKey, out var value))
		{
			return;
		}
		foreach (ulong item in value)
		{
			if (visibleRecipientsBySource.TryGetValue(item, out var value2))
			{
				value2.Remove(recipientKey);
				if (value2.Count == 0)
				{
					visibleRecipientsBySource.Remove(item);
				}
			}
		}
		visibleSourcesByRecipient.Remove(recipientKey);
	}

	private void RemoveSourceStateUnlocked(ulong sourceKey)
	{
		if (!visibleRecipientsBySource.TryGetValue(sourceKey, out var value))
		{
			return;
		}
		foreach (ulong item in value)
		{
			if (visibleSourcesByRecipient.TryGetValue(item, out var value2))
			{
				value2.Remove(sourceKey);
			}
		}
		visibleRecipientsBySource.Remove(sourceKey);
	}

	private TValue ValueForKey(ulong identityKey)
	{
		lock (sync)
		{
			return ValueOrNullUnlocked(identityKey);
		}
	}

	private TValue ValueOrNullUnlocked(ulong identityKey)
	{
		TValue value;
		return valuesByIdentity.TryGetValue(identityKey, out value) ? value : null;
	}

	private bool CanShare(TValue recipient, TValue source)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		return recipient != null && source != null && identityOf(recipient) != identityOf(source) && canShareVisibility(recipient, source);
	}

	private double Distance(TValue left, TValue right)
	{
		return Math.Sqrt(DistanceSquared(positionOf(left), positionOf(right)));
	}

	private static double DistanceSquared(VisibilityPosition left, VisibilityPosition right)
	{
		double num = left.X - right.X;
		double num2 = left.Z - right.Z;
		return num * num + num2 * num2;
	}

	private static HashSet<ulong> GetOrCreate(IDictionary<ulong, HashSet<ulong>> values, ulong identityKey)
	{
		if (!values.TryGetValue(identityKey, out var value))
		{
			value = (values[identityKey] = new HashSet<ulong>());
		}
		return value;
	}

	private static TDelegate Require<TDelegate>(TDelegate value, string name) where TDelegate : class
	{
		if (value == null)
		{
			throw new ArgumentNullException(name);
		}
		return value;
	}
}
