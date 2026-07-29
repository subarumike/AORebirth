using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;

namespace ZoneEngine.Core;

public static class WorldEntrySummary
{
	private sealed class Summary
	{
		private readonly Dictionary<string, int> categoryCounts = new Dictionary<string, int>();

		private readonly Dictionary<string, int> messageTypeCounts = new Dictionary<string, int>();

		private readonly HashSet<string> stableObjectKeys = new HashSet<string>();

		public DateTime ExpiresAt { get; private set; }

		private string CharacterIdentity { get; set; }

		private int DoorStatusLike { get; set; }

		private int InvalidObjects { get; set; }

		private int NoDecodedPosition { get; set; }

		private int ObjectsTotal { get; set; }

		private string Phase { get; set; }

		private string PlayfieldIdentity { get; set; }

		private int PlayfieldLike { get; set; }

		private int PositionBacked { get; set; }

		private int SimpleCharLike { get; set; }

		private int WeaponItemLike { get; set; }

		public Summary(string phase, string characterIdentity, string playfieldIdentity)
		{
			Phase = (string.IsNullOrEmpty(phase) ? "world_entry" : phase);
			CharacterIdentity = characterIdentity;
			PlayfieldIdentity = playfieldIdentity;
			ExpiresAt = DateTime.UtcNow + ActiveLifetime;
		}

		public void Record(N3Message message)
		{
			//IL_015e: Unknown result type (might be due to invalid IL or missing references)
			Classification classification = Classify(message);
			if (classification.Record)
			{
				ExpiresAt = DateTime.UtcNow + ActiveLifetime;
				ObjectsTotal++;
				Increment(messageTypeCounts, ((object)message).GetType().Name);
				Increment(categoryCounts, classification.Category);
				if (classification.HasPosition)
				{
					PositionBacked++;
				}
				else
				{
					NoDecodedPosition++;
				}
				if (classification.Category == "simple_char_like")
				{
					SimpleCharLike++;
				}
				else if (classification.Category == "playfield_like")
				{
					PlayfieldLike++;
				}
				else if (classification.Category == "door_status_like")
				{
					DoorStatusLike++;
				}
				else if (classification.Category == "weapon_item_like")
				{
					WeaponItemLike++;
				}
				if (TryGetStableIdentity(message, out var identity))
				{
					stableObjectKeys.Add(FormatIdentity(identity));
				}
				else
				{
					InvalidObjects++;
				}
				if (classification.ExpectsPosition && !classification.HasPosition)
				{
					InvalidObjects++;
				}
			}
		}

		public string Format()
		{
			return string.Format(CultureInfo.InvariantCulture, "world_entry_summary phase={0} char={1} playfield={2} objects_total={3} message_types={4} categories={5} simple_char_like={6} playfield_like={7} door_status_like={8} weapon_item_like={9} position_backed={10} no_decoded_position={11} stable_id_count={12} invalid_objects={13}", Phase, CharacterIdentity, PlayfieldIdentity, ObjectsTotal, FormatCounts(messageTypeCounts), FormatCounts(categoryCounts), SimpleCharLike, PlayfieldLike, DoorStatusLike, WeaponItemLike, PositionBacked, NoDecodedPosition, stableObjectKeys.Count, InvalidObjects);
		}

		private static Classification Classify(N3Message message)
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Expected O, but got Unknown
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Expected O, but got Unknown
			//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bd: Expected O, but got Unknown
			//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f0: Expected O, but got Unknown
			if (message is SimpleCharFullUpdateMessage)
			{
				SimpleCharFullUpdateMessage val = (SimpleCharFullUpdateMessage)message;
				return Classification.RecordObject("simple_char_like", expectsPosition: true, val.Coordinates != null);
			}
			if (message is PlayfieldAnarchyFMessage)
			{
				PlayfieldAnarchyFMessage val2 = (PlayfieldAnarchyFMessage)message;
				return Classification.RecordObject("playfield_like", expectsPosition: true, val2.CharacterCoordinates != null);
			}
			if (message is DoorStatusUpdateMessage)
			{
				return Classification.RecordObject("door_status_like", expectsPosition: false, hasPosition: false);
			}
			if (message is WeaponItemFullUpdateMessage)
			{
				return Classification.RecordObject("weapon_item_like", expectsPosition: false, hasPosition: false);
			}
			if (message is VendingMachineFullUpdateMessage)
			{
				VendingMachineFullUpdateMessage val3 = (VendingMachineFullUpdateMessage)message;
				return Classification.RecordObject("vending_machine_full_update", expectsPosition: true, val3.Coordinates != null);
			}
			if (message is SimpleItemFullUpdateMessage)
			{
				SimpleItemFullUpdateMessage val4 = (SimpleItemFullUpdateMessage)message;
				return Classification.RecordObject("simple_item_full_update", expectsPosition: true, val4.Coordinate != null);
			}
			if (message is FullCharacterMessage)
			{
				return Classification.RecordObject("full_character", expectsPosition: false, hasPosition: false);
			}
			if (message is CharInPlayMessage)
			{
				return Classification.RecordObject("char_in_play", expectsPosition: false, hasPosition: false);
			}
			if (message is PlayfieldAllTowersMessage || message is PlayfieldAllCitiesMessage)
			{
				return Classification.RecordObject("playfield_ready", expectsPosition: false, hasPosition: false);
			}
			return Classification.Skip();
		}

		private static string FormatCounts(Dictionary<string, int> counts)
		{
			if (counts.Count == 0)
			{
				return "none";
			}
			List<string> list = new List<string>(counts.Keys);
			list.Sort(StringComparer.Ordinal);
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < list.Count; i++)
			{
				if (i > 0)
				{
					stringBuilder.Append('|');
				}
				string text = list[i];
				stringBuilder.Append(text);
				stringBuilder.Append(':');
				stringBuilder.Append(counts[text].ToString(CultureInfo.InvariantCulture));
			}
			return stringBuilder.ToString();
		}

		private static bool TryGetStableIdentity(N3Message message, out Identity identity)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_007b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0090: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
			identity = message.Identity;
			if (IsStableIdentity(identity))
			{
				return true;
			}
			VendingMachineFullUpdateMessage val = (VendingMachineFullUpdateMessage)(object)((message is VendingMachineFullUpdateMessage) ? message : null);
			if (val != null && IsStableIdentity(val.NpcIdentity))
			{
				identity = val.NpcIdentity;
				return true;
			}
			SimpleItemFullUpdateMessage val2 = (SimpleItemFullUpdateMessage)(object)((message is SimpleItemFullUpdateMessage) ? message : null);
			if (val2 != null && IsStableIdentity(val2.Owner))
			{
				identity = val2.Owner;
				return true;
			}
			WeaponItemFullUpdateMessage val3 = (WeaponItemFullUpdateMessage)(object)((message is WeaponItemFullUpdateMessage) ? message : null);
			if (val3 != null && IsStableIdentity(val3.Owner))
			{
				identity = val3.Owner;
				return true;
			}
			return false;
		}
	}

	private sealed class Classification
	{
		public string Category { get; private set; }

		public bool ExpectsPosition { get; private set; }

		public bool HasPosition { get; private set; }

		public bool Record { get; private set; }

		private Classification()
		{
		}

		public static Classification RecordObject(string category, bool expectsPosition, bool hasPosition)
		{
			return new Classification
			{
				Category = category,
				ExpectsPosition = expectsPosition,
				HasPosition = hasPosition,
				Record = true
			};
		}

		public static Classification Skip()
		{
			return new Classification
			{
				Record = false
			};
		}
	}

	private static readonly TimeSpan ActiveLifetime = TimeSpan.FromSeconds(30.0);

	private static readonly object SyncRoot = new object();

	private static readonly Dictionary<int, Summary> ActiveSummaries = new Dictionary<int, Summary>();

	public static void Begin(ZoneClient client, string phase)
	{
		try
		{
			if (!TryGetCharacterContext(client, out var characterId, out var characterIdentity, out var playfieldIdentity))
			{
				return;
			}
			lock (SyncRoot)
			{
				CleanupExpiredSummaries();
				ActiveSummaries[characterId] = new Summary(phase, characterIdentity, playfieldIdentity);
			}
		}
		catch (Exception exception)
		{
			LogDiagnosticError("begin", exception);
		}
	}

	public static void RecordOutboundMessage(ZoneClient client, MessageBody messageBody)
	{
		try
		{
			if (!TryGetCharacterContext(client, out var characterId, out var _, out var _))
			{
				return;
			}
			N3Message val = (N3Message)(object)((messageBody is N3Message) ? messageBody : null);
			if (val == null)
			{
				return;
			}
			lock (SyncRoot)
			{
				CleanupExpiredSummaries();
				if (ActiveSummaries.TryGetValue(characterId, out var value))
				{
					value.Record(val);
				}
			}
		}
		catch (Exception exception)
		{
			LogDiagnosticError("record", exception);
		}
	}

	public static void Complete(ZoneClient client)
	{
		try
		{
			if (!TryGetCharacterContext(client, out var characterId, out var _, out var _))
			{
				return;
			}
			Summary value = null;
			lock (SyncRoot)
			{
				CleanupExpiredSummaries();
				if (ActiveSummaries.TryGetValue(characterId, out value))
				{
					ActiveSummaries.Remove(characterId);
				}
			}
			if (value != null)
			{
				LogUtil.Debug((DebugInfoDetail)128, value.Format());
			}
		}
		catch (Exception exception)
		{
			LogDiagnosticError("complete", exception);
		}
	}

	private static bool TryGetCharacterContext(ZoneClient client, out int characterId, out string characterIdentity, out string playfieldIdentity)
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		characterId = 0;
		characterIdentity = "none";
		playfieldIdentity = "none";
		if (client == null || client.Controller == null || client.Controller.Character == null)
		{
			return false;
		}
		Identity identity = ((IEntity)client.Controller.Character).Identity;
		characterId = ((Identity)(ref identity)).Instance;
		characterIdentity = FormatIdentity(((IEntity)client.Controller.Character).Identity);
		if (((IInstancedEntity)client.Controller.Character).Playfield != null)
		{
			playfieldIdentity = FormatIdentity(((IEntity)((IInstancedEntity)client.Controller.Character).Playfield).Identity);
		}
		return characterId != 0;
	}

	private static void CleanupExpiredSummaries()
	{
		DateTime utcNow = DateTime.UtcNow;
		List<int> list = new List<int>();
		foreach (KeyValuePair<int, Summary> activeSummary in ActiveSummaries)
		{
			if (activeSummary.Value.ExpiresAt <= utcNow)
			{
				list.Add(activeSummary.Key);
			}
		}
		foreach (int item in list)
		{
			ActiveSummaries.Remove(item);
		}
	}

	private static void LogDiagnosticError(string action, Exception exception)
	{
		LogUtil.Debug((DebugInfoDetail)512, string.Format(CultureInfo.InvariantCulture, "world_entry_summary_error action={0} error={1}", action, exception.Message));
	}

	private static bool IsStableIdentity(Identity identity)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		return (int)((Identity)(ref identity)).Type != 0 || ((Identity)(ref identity)).Instance != 0;
	}

	private static string FormatIdentity(Identity identity)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Expected I4, but got Unknown
		return string.Format(CultureInfo.InvariantCulture, "{0}:{1:X8}", (int)((Identity)(ref identity)).Type, ((Identity)(ref identity)).Instance);
	}

	private static void Increment(IDictionary<string, int> counts, string key)
	{
		if (!counts.TryGetValue(key, out var value))
		{
			value = 0;
		}
		counts[key] = value + 1;
	}
}
