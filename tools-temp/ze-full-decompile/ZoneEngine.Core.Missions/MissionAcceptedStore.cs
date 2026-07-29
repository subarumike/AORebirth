using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Missions;

internal static class MissionAcceptedStore
{
	internal sealed class AcceptedMission
	{
		public Identity QuestIdentity;

		public int MissionIconId;

		public int Quality;

		public string ShortInfo;

		public DateTime ExpiryUtc;

		public QuestInfo Offer;

		public int MarkerPlayfield;

		public int EntranceLow;

		public int EntranceHigh;

		public float MarkerX;

		public float MarkerY;

		public float MarkerZ;
	}

	private static readonly object Sync = new object();

	private static readonly Dictionary<int, List<AcceptedMission>> ByCharacter = new Dictionary<int, List<AcceptedMission>>();

	public static void Register(int characterInstance, QuestInfo offer, DateTime expiryUtc)
	{
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		if (characterInstance == 0 || offer == null)
		{
			return;
		}
		int markerPlayfield = 0;
		int entranceLow = 0;
		int entranceHigh = 0;
		float markerX = 0f;
		float markerY = 0f;
		float markerZ = 0f;
		if (offer.QuestActions != null && offer.QuestActions.Length != 0 && offer.QuestActions[0] != null)
		{
			QuestActionList val = offer.QuestActions[0];
			Identity playfield = val.Playfield;
			markerPlayfield = ((Identity)(ref playfield)).Instance;
			entranceLow = val.Unknown18;
			entranceHigh = val.Unknown19;
			markerX = val.X;
			markerY = val.Y;
			markerZ = val.Z;
		}
		AcceptedMission acceptedMission = new AcceptedMission
		{
			QuestIdentity = offer.QuestIdentity,
			MissionIconId = ((offer.MissionIconId != 0) ? offer.MissionIconId : 11330),
			Quality = offer.Quality,
			ShortInfo = (offer.ShortInfo ?? string.Empty),
			ExpiryUtc = expiryUtc,
			Offer = offer,
			MarkerPlayfield = markerPlayfield,
			EntranceLow = entranceLow,
			EntranceHigh = entranceHigh,
			MarkerX = markerX,
			MarkerY = markerY,
			MarkerZ = markerZ
		};
		lock (Sync)
		{
			List<AcceptedMission> orCreateList_NoLock = GetOrCreateList_NoLock(characterInstance);
			int num = FindIndex_NoLock(orCreateList_NoLock, acceptedMission.QuestIdentity);
			if (num >= 0)
			{
				orCreateList_NoLock[num] = acceptedMission;
			}
			else
			{
				orCreateList_NoLock.Add(acceptedMission);
			}
			PruneExpired_NoLock(orCreateList_NoLock);
			TryWriteSidecar(characterInstance, orCreateList_NoLock);
		}
	}

	public static List<AcceptedMission> GetAll(int characterInstance)
	{
		lock (Sync)
		{
			if (!ByCharacter.TryGetValue(characterInstance, out var value) || value == null || value.Count == 0)
			{
				if (!TryReadSidecar(characterInstance, out var list) || list.Count <= 0)
				{
					return new List<AcceptedMission>();
				}
				ByCharacter[characterInstance] = list;
				value = list;
			}
			PruneExpired_NoLock(value);
			if (value.Count == 0)
			{
				ByCharacter.Remove(characterInstance);
				TryDeleteSidecar(characterInstance);
				return new List<AcceptedMission>();
			}
			return new List<AcceptedMission>(value);
		}
	}

	public static bool TryGet(int characterInstance, out AcceptedMission entry)
	{
		List<AcceptedMission> all = GetAll(characterInstance);
		if (all.Count == 0)
		{
			entry = null;
			return false;
		}
		entry = all[all.Count - 1];
		return true;
	}

	public static bool Remove(int characterInstance, Identity questIdentity)
	{
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		lock (Sync)
		{
			if (!ByCharacter.TryGetValue(characterInstance, out var value) || value == null)
			{
				if (!TryReadSidecar(characterInstance, out var list))
				{
					return false;
				}
				value = list;
				ByCharacter[characterInstance] = value;
			}
			int num = FindIndex_NoLock(value, questIdentity);
			if (num < 0)
			{
				return false;
			}
			value.RemoveAt(num);
			if (value.Count == 0)
			{
				ByCharacter.Remove(characterInstance);
				TryDeleteSidecar(characterInstance);
			}
			else
			{
				TryWriteSidecar(characterInstance, value);
			}
			return true;
		}
	}

	public static void Clear(int characterInstance)
	{
		lock (Sync)
		{
			ByCharacter.Remove(characterInstance);
		}
		TryDeleteSidecar(characterInstance);
	}

	private static List<AcceptedMission> GetOrCreateList_NoLock(int characterInstance)
	{
		if (!ByCharacter.TryGetValue(characterInstance, out var value) || value == null)
		{
			value = ((!TryReadSidecar(characterInstance, out var list)) ? new List<AcceptedMission>() : list);
			ByCharacter[characterInstance] = value;
		}
		return value;
	}

	private static int FindIndex_NoLock(List<AcceptedMission> list, Identity questIdentity)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		for (int i = 0; i < list.Count; i++)
		{
			AcceptedMission acceptedMission = list[i];
			if (acceptedMission != null && ((Identity)(ref acceptedMission.QuestIdentity)).Type == ((Identity)(ref questIdentity)).Type && ((Identity)(ref acceptedMission.QuestIdentity)).Instance == ((Identity)(ref questIdentity)).Instance)
			{
				return i;
			}
		}
		return -1;
	}

	private static void PruneExpired_NoLock(List<AcceptedMission> list)
	{
		DateTime utcNow = DateTime.UtcNow;
		for (int num = list.Count - 1; num >= 0; num--)
		{
			if (list[num] == null || list[num].ExpiryUtc <= utcNow)
			{
				list.RemoveAt(num);
			}
		}
	}

	private static void TryWriteSidecar(int characterInstance, List<AcceptedMission> list)
	{
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Expected I4, but got Unknown
		try
		{
			string path = SidecarDirectory();
			Directory.CreateDirectory(path);
			StringBuilder stringBuilder = new StringBuilder();
			foreach (AcceptedMission item in list)
			{
				if (item != null)
				{
					stringBuilder.AppendFormat(CultureInfo.InvariantCulture, "{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}|{12}", characterInstance, (int)((Identity)(ref item.QuestIdentity)).Type, ((Identity)(ref item.QuestIdentity)).Instance, item.MissionIconId, item.Quality, item.ExpiryUtc.Ticks, item.MarkerPlayfield, item.EntranceLow, item.EntranceHigh, item.MarkerX, item.MarkerY, item.MarkerZ, (item.ShortInfo ?? string.Empty).Replace('|', '/').Replace('\r', ' ').Replace('\n', ' '));
					stringBuilder.AppendLine();
				}
			}
			File.WriteAllText(SidecarPath(characterInstance), stringBuilder.ToString());
		}
		catch
		{
		}
	}

	private static bool TryReadSidecar(int characterInstance, out List<AcceptedMission> list)
	{
		//IL_01fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0217: Unknown result type (might be due to invalid IL or missing references)
		//IL_0219: Unknown result type (might be due to invalid IL or missing references)
		list = new List<AcceptedMission>();
		try
		{
			string path = SidecarPath(characterInstance);
			if (!File.Exists(path))
			{
				return false;
			}
			string[] array = File.ReadAllLines(path);
			DateTime utcNow = DateTime.UtcNow;
			string[] array2 = array;
			foreach (string text in array2)
			{
				string text2 = ((text == null) ? string.Empty : text.Trim());
				if (text2.Length == 0)
				{
					continue;
				}
				string[] array3 = text2.Split('|');
				if (array3.Length < 6 || !int.TryParse(array3[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) || !int.TryParse(array3[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result2) || !int.TryParse(array3[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result3) || !int.TryParse(array3[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result4) || !long.TryParse(array3[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result5))
				{
					continue;
				}
				DateTime dateTime = new DateTime(result5, DateTimeKind.Utc);
				if (!(dateTime <= utcNow))
				{
					int result6 = 0;
					int result7 = 0;
					int result8 = 0;
					float result9 = 0f;
					float result10 = 0f;
					float result11 = 0f;
					string shortInfo;
					if (array3.Length >= 13)
					{
						int.TryParse(array3[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out result6);
						int.TryParse(array3[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out result7);
						int.TryParse(array3[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out result8);
						float.TryParse(array3[9], NumberStyles.Float, CultureInfo.InvariantCulture, out result9);
						float.TryParse(array3[10], NumberStyles.Float, CultureInfo.InvariantCulture, out result10);
						float.TryParse(array3[11], NumberStyles.Float, CultureInfo.InvariantCulture, out result11);
						shortInfo = array3[12];
					}
					else
					{
						shortInfo = ((array3.Length > 6) ? array3[6] : string.Empty);
					}
					List<AcceptedMission> obj = list;
					AcceptedMission acceptedMission = new AcceptedMission();
					Identity questIdentity = default(Identity);
					((Identity)(ref questIdentity)).Type = (IdentityType)result;
					((Identity)(ref questIdentity)).Instance = result2;
					acceptedMission.QuestIdentity = questIdentity;
					acceptedMission.MissionIconId = result3;
					acceptedMission.Quality = result4;
					acceptedMission.ExpiryUtc = dateTime;
					acceptedMission.ShortInfo = shortInfo;
					acceptedMission.Offer = null;
					acceptedMission.MarkerPlayfield = result6;
					acceptedMission.EntranceLow = result7;
					acceptedMission.EntranceHigh = result8;
					acceptedMission.MarkerX = result9;
					acceptedMission.MarkerY = result10;
					acceptedMission.MarkerZ = result11;
					obj.Add(acceptedMission);
				}
			}
			return list.Count > 0;
		}
		catch
		{
			list = new List<AcceptedMission>();
			return false;
		}
	}

	private static void TryDeleteSidecar(int characterInstance)
	{
		try
		{
			string path = SidecarPath(characterInstance);
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch
		{
		}
	}

	private static string SidecarDirectory()
	{
		return Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", "mission-state");
	}

	private static string SidecarPath(int characterInstance)
	{
		return Path.Combine(SidecarDirectory(), characterInstance.ToString(CultureInfo.InvariantCulture) + ".txt");
	}
}
