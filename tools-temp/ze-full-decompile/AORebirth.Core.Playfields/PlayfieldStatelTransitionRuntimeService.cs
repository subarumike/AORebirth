using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AORebirth.Core.Entities;
using AORebirth.Core.Events;
using AORebirth.Core.Statels;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core.Missions;

namespace AORebirth.Core.Playfields;

internal sealed class PlayfieldStatelTransitionRuntimeService
{
	private const int CapturedMontroyalEntrySourcePlayfieldId = 655;

	private const int CapturedMontroyalPrivateCityInstance = 1196045;

	private const int CapturedOwnedMontroyalPrivateCityInstance = 1196034;

	private const int CapturedOwnedPrivateCityOrganizationInstance = 1970177;

	private const float CapturedMontroyalEntrySourceX = 3140.412f;

	private const float CapturedMontroyalEntrySourceY = 51.54391f;

	private const float CapturedMontroyalEntrySourceZ = 799.8611f;

	private const float CapturedMontroyalEntryRadius = 2.5f;

	private const float CapturedMontroyalEntryVerticalTolerance = 6f;

	private const float CapturedMontroyalEntryDestinationX = 530.0042f;

	private const float CapturedMontroyalEntryDestinationY = 163.2545f;

	private const float CapturedMontroyalEntryDestinationZ = 580.9957f;

	private const float CapturedOwnedMontroyalEntryDestinationX = 528.6631f;

	private const float CapturedOwnedMontroyalEntryDestinationY = 163.2526f;

	private const float CapturedOwnedMontroyalEntryDestinationZ = 580.9919f;

	private const float UserConfirmedMontroyalExitSourceX = 530.4664f;

	private const float UserConfirmedMontroyalExitSourceY = 160.6381f;

	private const float UserConfirmedMontroyalExitSourceZ = 590.7054f;

	private const float UserConfirmedMontroyalExitRadius = 3f;

	private const float UserConfirmedMontroyalExitVerticalTolerance = 12f;

	private const float UserConfirmedMontroyalExitDestinationX = 3138.2f;

	private const float UserConfirmedMontroyalExitDestinationY = 51.4f;

	private const float UserConfirmedMontroyalExitDestinationZ = 812.8f;

	private const int CapturedSubwayPlayfieldId = 127;

	private const int CapturedSubwayEntrySourcePlayfieldId = 655;

	private const uint CapturedSubwayEntrySourceDoorInstance = 3222930063u;

	private const float CapturedSubwayEntrySourceX = 3305.5f;

	private const float CapturedSubwayEntrySourceY = 35.3f;

	private const float CapturedSubwayEntrySourceZ = 836.4f;

	private const float CapturedSubwayEntryRadius = 4f;

	private const float CapturedSubwayEntryVerticalTolerance = 8f;

	private const float CapturedSubwayEntranceLandingX = 65.80835f;

	private const float CapturedSubwayEntranceLandingY = 115.6148f;

	private const float CapturedSubwayEntranceLandingZ = 318.9879f;

	private const float CapturedSubwayEntranceHeadingX = 0f;

	private const float CapturedSubwayEntranceHeadingY = 0.7071124f;

	private const float CapturedSubwayEntranceHeadingZ = 0f;

	private const float CapturedSubwayEntranceHeadingW = 0.7071012f;

	private const int CapturedHoloDeckPlayfieldId = 7001;

	private const int CapturedHoloDeckEntrySourcePlayfieldId = 655;

	private const float CapturedHoloDeckEntrySourceX = 3245.94f;

	private const float CapturedHoloDeckEntrySourceY = 36.085f;

	private const float CapturedHoloDeckEntrySourceZ = 943.3943f;

	private const float CapturedHoloDeckEntryRadius = 2.5f;

	private const float CapturedHoloDeckEntryVerticalTolerance = 4f;

	private const float CapturedHoloDeckEntryLandingX = 183.01f;

	private const float CapturedHoloDeckEntryLandingY = 1.02f;

	private const float CapturedHoloDeckEntryLandingZ = 197.01f;

	private const float CapturedHoloDeckEntryLandingHeadingX = 0f;

	private const float CapturedHoloDeckEntryLandingHeadingY = 0.182956f;

	private const float CapturedHoloDeckEntryLandingHeadingZ = 0f;

	private const float CapturedHoloDeckEntryLandingHeadingW = 0.9831211f;

	private const float CapturedHoloDeckExitSourceX = 178.5387f;

	private const float CapturedHoloDeckExitSourceY = 1.02f;

	private const float CapturedHoloDeckExitSourceZ = 197.1772f;

	private const float CapturedHoloDeckExitRadius = 2.5f;

	private const float CapturedHoloDeckExitVerticalTolerance = 4f;

	private const float CapturedHoloDeckExitLandingX = 3245f;

	private const float CapturedHoloDeckExitLandingY = 35.715f;

	private const float CapturedHoloDeckExitLandingZ = 939f;

	private const float CapturedHoloDeckExitLandingHeadingX = 0f;

	private const float CapturedHoloDeckExitLandingHeadingY = -0.8022833f;

	private const float CapturedHoloDeckExitLandingHeadingZ = 0f;

	private const float CapturedHoloDeckExitLandingHeadingW = 0.5969435f;

	private static readonly Dictionary<int, DateTime> PostZoneCollisionGraceUntil = new Dictionary<int, DateTime>();

	private static readonly object PostZoneCollisionGraceLock = new object();

	private static readonly TimeSpan PostZoneCollisionGrace = TimeSpan.FromSeconds(3.0);

	private readonly Dictionary<int, HashSet<string>> statelEnterContacts = new Dictionary<int, HashSet<string>>();

	private readonly HashSet<int> statelCollisionInitializedCharacters = new HashSet<int>();

	private readonly HashSet<int> capturedSubwayEntryContacts = new HashSet<int>();

	private readonly HashSet<int> capturedHoloDeckEntryContacts = new HashSet<int>();

	private readonly HashSet<int> capturedHoloDeckExitContacts = new HashSet<int>();

	private readonly HashSet<int> missionExitDoorArmedCharacters = new HashSet<int>();

	private DateTime lastMissionEntryDiagUtc = DateTime.MinValue;

	internal static void ArmPostZoneCollisionGrace(ICharacter character)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		if (character == null)
		{
			return;
		}
		lock (PostZoneCollisionGraceLock)
		{
			Dictionary<int, DateTime> postZoneCollisionGraceUntil = PostZoneCollisionGraceUntil;
			Identity identity = ((IEntity)character).Identity;
			postZoneCollisionGraceUntil[((Identity)(ref identity)).Instance] = DateTime.UtcNow + PostZoneCollisionGrace;
		}
	}

	internal static bool IsCapturedMontroyalPrivateCityInstance(int playfieldInstance)
	{
		return playfieldInstance == 1196045 || playfieldInstance == 1196034;
	}

	internal static int ResolveCapturedMontroyalPrivateCityInstance(int organizationInstance, int organizationCityId)
	{
		if (organizationCityId > 0)
		{
			return organizationCityId;
		}
		return (organizationInstance == 1970177) ? 1196034 : 1196045;
	}

	internal static bool IsCapturedOwnedPrivateCityOrganization(int organizationInstance)
	{
		return organizationInstance == 1970177;
	}

	internal void ClearContactState(int dynelId)
	{
		statelEnterContacts.Remove(dynelId);
		statelCollisionInitializedCharacters.Remove(dynelId);
		capturedSubwayEntryContacts.Remove(dynelId);
	}

	internal void PrimeStatelCollisionContacts(ICharacter dynel, IEnumerable<StatelData> collisionStatels)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		Identity identity = ((IEntity)dynel).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		if (!statelEnterContacts.TryGetValue(instance, out var value))
		{
			value = new HashSet<string>();
			statelEnterContacts[instance] = value;
		}
		foreach (StatelData collisionStatel in collisionStatels)
		{
			if (IsInStatelCollisionRange(collisionStatel, dynel))
			{
				string item = BuildStatelContactKey(collisionStatel);
				value.Add(item);
			}
		}
		statelCollisionInitializedCharacters.Add(instance);
	}

	internal void CheckStatelCollision(ICharacter dynel, Identity playfieldIdentity, IEnumerable<StatelData> collisionStatels, Func<ICharacter, int> resolvePrivateCityDestinationPlayfield, Func<ICharacter, int> resolveCharacterOrganizationInstance, Action<ICharacter> stopMovement, Action<ICharacter> sendCapturedPrivateCityEntrySocialStatus, Action<Dynel, Coordinate, Quaternion, int> teleportToPlayfield)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ad: Invalid comparison between Unknown and I4
		//IL_020e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0213: Unknown result type (might be due to invalid IL or missing references)
		//IL_0252: Unknown result type (might be due to invalid IL or missing references)
		//IL_0257: Unknown result type (might be due to invalid IL or missing references)
		//IL_026a: Unknown result type (might be due to invalid IL or missing references)
		//IL_026f: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cf: Unknown result type (might be due to invalid IL or missing references)
		if (IsPostZoneCollisionGraceActive(dynel) || TryHandleMissionInstanceEntry(dynel, playfieldIdentity, stopMovement, teleportToPlayfield) || TryHandleMissionInstanceExit(dynel, playfieldIdentity, stopMovement, teleportToPlayfield) || TryHandleCapturedSubwayProxyEntry(dynel, playfieldIdentity, stopMovement, teleportToPlayfield) || TryHandleCapturedHoloDeckEntry(dynel, playfieldIdentity, stopMovement, teleportToPlayfield) || TryHandleCapturedHoloDeckExit(dynel, playfieldIdentity, stopMovement, teleportToPlayfield) || TryHandleCapturedMontroyalPrivateCityEntry(dynel, playfieldIdentity, resolvePrivateCityDestinationPlayfield, resolveCharacterOrganizationInstance, stopMovement, sendCapturedPrivateCityEntrySocialStatus, teleportToPlayfield) || TryHandleUserConfirmedMontroyalPrivateCityExit(dynel, playfieldIdentity, stopMovement, teleportToPlayfield))
		{
			return;
		}
		Identity identity = ((IEntity)dynel).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		bool flag = statelCollisionInitializedCharacters.Contains(instance);
		if (!statelEnterContacts.TryGetValue(instance, out var value))
		{
			value = new HashSet<string>();
			statelEnterContacts[instance] = value;
		}
		foreach (StatelData collisionStatel in collisionStatels)
		{
			string item = BuildStatelContactKey(collisionStatel);
			bool flag2 = IsInStatelCollisionRange(collisionStatel, dynel);
			bool flag3 = value.Contains(item);
			if (!flag2)
			{
				if (flag3)
				{
					value.Remove(item);
				}
				continue;
			}
			foreach (Event item2 in collisionStatel.Events.Where((Event x) => (int)x.EventType == 22 || (int)x.EventType == 16 || (int)x.EventType == 3))
			{
				if ((int)item2.EventType == 16)
				{
					if (!flag)
					{
						value.Add(item);
						continue;
					}
					if (flag3)
					{
						continue;
					}
					value.Add(item);
				}
				else
				{
					if (flag3)
					{
						continue;
					}
					value.Add(item);
				}
				identity = collisionStatel.Identity;
				LogUtil.Debug((DebugInfoDetail)8192, "Stepped on Statel " + ((Identity)(ref identity)).ToString(true));
				LogUtil.Debug((DebugInfoDetail)8192, ((object)item2).ToString());
				CultureInfo invariantCulture = CultureInfo.InvariantCulture;
				object[] array = new object[7];
				identity = ((IEntity)dynel).Identity;
				array[0] = ((Identity)(ref identity)).ToString(true);
				identity = ((IEntity)((IInstancedEntity)dynel).Playfield).Identity;
				array[1] = ((Identity)(ref identity)).Instance;
				array[2] = ((IDynel)dynel).RawCoordinates.X;
				array[3] = ((IDynel)dynel).RawCoordinates.Y;
				array[4] = ((IDynel)dynel).RawCoordinates.Z;
				identity = collisionStatel.Identity;
				array[5] = ((Identity)(ref identity)).ToString(true);
				array[6] = item2.EventType;
				LogUtil.Debug((DebugInfoDetail)128, string.Format(invariantCulture, "Statel collision firing character={0} playfield={1} coords={2:F1},{3:F1},{4:F1} statel={5} event={6}", array));
				item2.Perform(dynel, (IEntity)(object)collisionStatel);
			}
		}
		if (!flag)
		{
			statelCollisionInitializedCharacters.Add(instance);
		}
	}

	private static Coordinate ResolveCapturedMontroyalEntryDestination(int destinationPlayfieldId)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Expected O, but got Unknown
		return (destinationPlayfieldId == 1196034) ? new Coordinate(528.6631f, 163.2526f, 580.9919f) : new Coordinate(530.0042f, 163.2545f, 580.9957f);
	}

	private static string BuildStatelContactKey(StatelData sd)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Expected I4, but got Unknown
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		CultureInfo invariantCulture = CultureInfo.InvariantCulture;
		object[] array = new object[5];
		Identity identity = sd.Identity;
		array[0] = (int)((Identity)(ref identity)).Type;
		identity = sd.Identity;
		array[1] = ((Identity)(ref identity)).Instance;
		array[2] = sd.X;
		array[3] = sd.Y;
		array[4] = sd.Z;
		return string.Format(invariantCulture, "{0}:{1}:{2:0.###}:{3:0.###}:{4:0.###}", array);
	}

	private static bool IsInStatelCollisionRange(StatelData sd, ICharacter dynel)
	{
		float num = sd.X - ((IDynel)dynel).RawCoordinates.X;
		float num2 = sd.Z - ((IDynel)dynel).RawCoordinates.Z;
		float num3 = (float)Math.Sqrt(num * num + num2 * num2);
		float num4 = Math.Abs(sd.Y - ((IDynel)dynel).RawCoordinates.Y);
		return num3 < 2f && num4 <= 6f;
	}

	internal static bool IsPostZoneCollisionGraceActive(ICharacter dynel)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		if (dynel == null)
		{
			return false;
		}
		lock (PostZoneCollisionGraceLock)
		{
			Dictionary<int, DateTime> postZoneCollisionGraceUntil = PostZoneCollisionGraceUntil;
			Identity identity = ((IEntity)dynel).Identity;
			if (!postZoneCollisionGraceUntil.TryGetValue(((Identity)(ref identity)).Instance, out var value))
			{
				return false;
			}
			if (DateTime.UtcNow < value)
			{
				return true;
			}
			Dictionary<int, DateTime> postZoneCollisionGraceUntil2 = PostZoneCollisionGraceUntil;
			identity = ((IEntity)dynel).Identity;
			postZoneCollisionGraceUntil2.Remove(((Identity)(ref identity)).Instance);
			return false;
		}
	}

	private bool TryHandleMissionInstanceEntry(ICharacter character, Identity playfieldIdentity, Action<ICharacter> stopMovement, Action<Dynel, Coordinate, Quaternion, int> teleportToPlayfield)
	{
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0291: Unknown result type (might be due to invalid IL or missing references)
		//IL_0296: Unknown result type (might be due to invalid IL or missing references)
		//IL_0329: Unknown result type (might be due to invalid IL or missing references)
		//IL_032e: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ae: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || ((IDynel)character).Controller == null || ((IDynel)character).Controller.Client == null || ((IInstancedEntity)character).DoNotDoTimers)
		{
			return false;
		}
		Dynel val = (Dynel)(object)((character is Dynel) ? character : null);
		if (val == null)
		{
			return false;
		}
		if (!MissionKeyGrantService.HasMissionKey(character))
		{
			return false;
		}
		float x = ((IDynel)character).RawCoordinates.X;
		float y = ((IDynel)character).RawCoordinates.Y;
		float z = ((IDynel)character).RawCoordinates.Z;
		int instance = ((Identity)(ref playfieldIdentity)).Instance;
		bool flag = false;
		string text = null;
		double num = double.MaxValue;
		Identity identity = ((IEntity)character).Identity;
		List<MissionAcceptedStore.AcceptedMission> all = MissionAcceptedStore.GetAll(((Identity)(ref identity)).Instance);
		for (int i = 0; i < all.Count; i++)
		{
			MissionAcceptedStore.AcceptedMission acceptedMission = all[i];
			if (acceptedMission != null && acceptedMission.MarkerPlayfield != 0 && acceptedMission.MarkerPlayfield == instance)
			{
				double num2 = x - acceptedMission.MarkerX;
				double num3 = z - acceptedMission.MarkerZ;
				double num4 = num2 * num2 + num3 * num3;
				if (num4 < num)
				{
					num = num4;
				}
				double num5 = Math.Abs(y - acceptedMission.MarkerY);
				if (num4 <= 64.0 && num5 <= 12.0)
				{
					flag = true;
					text = "marker";
					break;
				}
			}
		}
		if (!flag && instance == 735)
		{
			float[][] romeEntranceSpots = MissionInstanceService.RomeEntranceSpots;
			foreach (float[] array in romeEntranceSpots)
			{
				double num6 = x - array[0];
				double num7 = z - array[2];
				double num8 = num6 * num6 + num7 * num7;
				if (num8 < num)
				{
					num = num8;
				}
				double num9 = Math.Abs(y - array[1]);
				if (num8 <= 16.0 && num9 <= 8.0)
				{
					flag = true;
					text = "rome";
					break;
				}
			}
		}
		DateTime utcNow = DateTime.UtcNow;
		if (!flag && (utcNow - lastMissionEntryDiagUtc).TotalMilliseconds >= 1500.0)
		{
			lastMissionEntryDiagUtc = utcNow;
			object[] array2 = new object[7];
			identity = ((IEntity)character).Identity;
			array2[0] = ((Identity)(ref identity)).Instance;
			array2[1] = instance;
			array2[2] = x;
			array2[3] = y;
			array2[4] = z;
			array2[5] = ((num == double.MaxValue) ? (-1.0) : Math.Sqrt(num));
			array2[6] = all.Count;
			MissionDiagnostics.Log("ENTRY-CHECK char={0} pf={1} hasKey=true pos=({2:F2},{3:F2},{4:F2}) nearestDist={5:F2} near=false missions={6}", array2);
		}
		if (!flag)
		{
			return false;
		}
		object[] array3 = new object[6];
		identity = ((IEntity)character).Identity;
		array3[0] = ((Identity)(ref identity)).Instance;
		array3[1] = text;
		array3[2] = instance;
		array3[3] = x;
		array3[4] = y;
		array3[5] = z;
		MissionDiagnostics.Log("ENTRY-TELEPORT char={0} reason={1} pf={2} pos=({3:F2},{4:F2},{5:F2})", array3);
		stopMovement(character);
		if (!MissionInstanceService.TryEnterMissionInstance(((IDynel)character).Controller.Client))
		{
			return false;
		}
		CultureInfo invariantCulture = CultureInfo.InvariantCulture;
		object[] array4 = new object[6];
		identity = ((IEntity)character).Identity;
		array4[0] = ((Identity)(ref identity)).ToString(true);
		array4[1] = text;
		array4[2] = instance;
		array4[3] = x;
		array4[4] = y;
		array4[5] = z;
		LogUtil.Debug((DebugInfoDetail)64, string.Format(invariantCulture, "Mission instance entry teleport character={0} reason={1} sourcePf={2} source=({3:F3},{4:F3},{5:F3})", array4));
		return true;
	}

	private bool TryHandleMissionInstanceExit(ICharacter character, Identity playfieldIdentity, Action<ICharacter> stopMovement, Action<Dynel, Coordinate, Quaternion, int> teleportToPlayfield)
	{
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || !MissionInstanceService.IsMissionInstancePlayfield(((Identity)(ref playfieldIdentity)).Instance) || ((IDynel)character).Controller == null || ((IDynel)character).Controller.Client == null || ((IInstancedEntity)character).DoNotDoTimers)
		{
			return false;
		}
		Dynel val = (Dynel)(object)((character is Dynel) ? character : null);
		if (val == null)
		{
			return false;
		}
		MissionInstanceService.ResolveInteriorExitDoor(((Identity)(ref playfieldIdentity)).Instance, out var x, out var y, out var z);
		float x2 = ((IDynel)character).RawCoordinates.X;
		float y2 = ((IDynel)character).RawCoordinates.Y;
		float z2 = ((IDynel)character).RawCoordinates.Z;
		double num = x2 - x;
		double num2 = z2 - z;
		double num3 = num * num + num2 * num2;
		double num4 = Math.Abs(y2 - y);
		Identity identity = ((IEntity)character).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		if (num3 > 100.0 || num4 > 12.0)
		{
			missionExitDoorArmedCharacters.Add(instance);
			return false;
		}
		if (!missionExitDoorArmedCharacters.Contains(instance))
		{
			return false;
		}
		if (num3 > 12.25 || num4 > 8.0)
		{
			return false;
		}
		missionExitDoorArmedCharacters.Remove(instance);
		if (!MissionInstanceService.TryExitMissionInstance(((IDynel)character).Controller.Client))
		{
			return false;
		}
		stopMovement(character);
		MissionDiagnostics.Log("EXIT-PROXIMITY char={0} door=({1:F1},{2:F1},{3:F1})", instance, x, y, z);
		return true;
	}

	private bool TryHandleCapturedSubwayProxyEntry(ICharacter character, Identity playfieldIdentity, Action<ICharacter> stopMovement, Action<Dynel, Coordinate, Quaternion, int> teleportToPlayfield)
	{
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Expected O, but got Unknown
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		//IL_0190: Expected O, but got Unknown
		//IL_01f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f8: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || ((Identity)(ref playfieldIdentity)).Instance != 655 || ((IDynel)character).Controller == null || ((IDynel)character).Controller.Client == null || ((IInstancedEntity)character).DoNotDoTimers)
		{
			return false;
		}
		Dynel val = (Dynel)(object)((character is Dynel) ? character : null);
		if (val == null)
		{
			return false;
		}
		float x = ((IDynel)character).RawCoordinates.X;
		float y = ((IDynel)character).RawCoordinates.Y;
		float z = ((IDynel)character).RawCoordinates.Z;
		double num = x - 3305.5f;
		double num2 = z - 836.4f;
		double num3 = num * num + num2 * num2;
		double num4 = Math.Abs(y - 35.3f);
		bool flag = num3 <= 16.0 && num4 <= 8.0;
		Identity identity = ((IEntity)character).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		if (!flag)
		{
			capturedSubwayEntryContacts.Remove(instance);
			return false;
		}
		if (capturedSubwayEntryContacts.Contains(instance) || !statelCollisionInitializedCharacters.Contains(instance))
		{
			capturedSubwayEntryContacts.Add(instance);
			return false;
		}
		capturedSubwayEntryContacts.Add(instance);
		Coordinate val2 = new Coordinate(65.80835f, 115.6148f, 318.9879f);
		Quaternion arg = new Quaternion(0.0, 0.7071123719215393, 0.0, 0.7071012258529663);
		((IStats)character).Stats[(StatIds)193].BaseValue = 3222930063u;
		((IStats)character).Stats[(StatIds)192].BaseValue = 655u;
		stopMovement(character);
		teleportToPlayfield(val, val2, arg, 127);
		CultureInfo invariantCulture = CultureInfo.InvariantCulture;
		object[] array = new object[10];
		identity = ((IEntity)character).Identity;
		array[0] = ((Identity)(ref identity)).ToString(true);
		array[1] = ((Identity)(ref playfieldIdentity)).Instance;
		array[2] = x;
		array[3] = y;
		array[4] = z;
		array[5] = 3222930063u;
		array[6] = 127;
		array[7] = val2.x;
		array[8] = val2.y;
		array[9] = val2.z;
		LogUtil.Debug((DebugInfoDetail)64, string.Format(invariantCulture, "Subway proxy entry teleport character={0} sourcePf={1} source=({2:F3},{3:F3},{4:F3}) sourceDoor={5:X8} destPf={6} dest=({7:F3},{8:F3},{9:F3}) evidence=server_log_20260708_1634 user_extended_location_20260708_2135", array));
		return true;
	}

	private bool TryHandleCapturedHoloDeckEntry(ICharacter character, Identity playfieldIdentity, Action<ICharacter> stopMovement, Action<Dynel, Coordinate, Quaternion, int> teleportToPlayfield)
	{
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Expected O, but got Unknown
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		//IL_0190: Expected O, but got Unknown
		//IL_01db: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e0: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || ((Identity)(ref playfieldIdentity)).Instance != 655 || ((IDynel)character).Controller == null || ((IDynel)character).Controller.Client == null || ((IInstancedEntity)character).DoNotDoTimers)
		{
			return false;
		}
		Dynel val = (Dynel)(object)((character is Dynel) ? character : null);
		if (val == null)
		{
			return false;
		}
		float x = ((IDynel)character).RawCoordinates.X;
		float y = ((IDynel)character).RawCoordinates.Y;
		float z = ((IDynel)character).RawCoordinates.Z;
		double num = x - 3245.94f;
		double num2 = z - 943.3943f;
		double num3 = num * num + num2 * num2;
		double num4 = Math.Abs(y - 36.085f);
		bool flag = num3 <= 6.25 && num4 <= 4.0;
		Identity identity = ((IEntity)character).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		if (!flag)
		{
			capturedHoloDeckEntryContacts.Remove(instance);
			return false;
		}
		if (capturedHoloDeckEntryContacts.Contains(instance) || !statelCollisionInitializedCharacters.Contains(instance))
		{
			capturedHoloDeckEntryContacts.Add(instance);
			return false;
		}
		capturedHoloDeckEntryContacts.Add(instance);
		Coordinate val2 = new Coordinate(183.01f, 1.02f, 197.01f);
		Quaternion arg = new Quaternion(0.0, 0.18295599520206451, 0.0, 0.9831210970878601);
		((IStats)character).Stats[(StatIds)192].BaseValue = 655u;
		stopMovement(character);
		teleportToPlayfield(val, val2, arg, 7001);
		CultureInfo invariantCulture = CultureInfo.InvariantCulture;
		object[] array = new object[9];
		identity = ((IEntity)character).Identity;
		array[0] = ((Identity)(ref identity)).ToString(true);
		array[1] = ((Identity)(ref playfieldIdentity)).Instance;
		array[2] = x;
		array[3] = y;
		array[4] = z;
		array[5] = 7001;
		array[6] = val2.x;
		array[7] = val2.y;
		array[8] = val2.z;
		LogUtil.Debug((DebugInfoDetail)64, string.Format(invariantCulture, "HoloDeck entry teleport character={0} sourcePf={1} source=({2:F3},{3:F3},{4:F3}) destPf={5} dest=({6:F3},{7:F3},{8:F3}) evidence=20260719-155043", array));
		return true;
	}

	private bool TryHandleCapturedHoloDeckExit(ICharacter character, Identity playfieldIdentity, Action<ICharacter> stopMovement, Action<Dynel, Coordinate, Quaternion, int> teleportToPlayfield)
	{
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Expected O, but got Unknown
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		//IL_0190: Expected O, but got Unknown
		//IL_01db: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e0: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || ((Identity)(ref playfieldIdentity)).Instance != 7001 || ((IDynel)character).Controller == null || ((IDynel)character).Controller.Client == null || ((IInstancedEntity)character).DoNotDoTimers)
		{
			return false;
		}
		Dynel val = (Dynel)(object)((character is Dynel) ? character : null);
		if (val == null)
		{
			return false;
		}
		float x = ((IDynel)character).RawCoordinates.X;
		float y = ((IDynel)character).RawCoordinates.Y;
		float z = ((IDynel)character).RawCoordinates.Z;
		double num = x - 178.5387f;
		double num2 = z - 197.1772f;
		double num3 = num * num + num2 * num2;
		double num4 = Math.Abs(y - 1.02f);
		bool flag = num3 <= 6.25 && num4 <= 4.0;
		Identity identity = ((IEntity)character).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		if (!flag)
		{
			capturedHoloDeckExitContacts.Remove(instance);
			return false;
		}
		if (capturedHoloDeckExitContacts.Contains(instance) || !statelCollisionInitializedCharacters.Contains(instance))
		{
			capturedHoloDeckExitContacts.Add(instance);
			return false;
		}
		capturedHoloDeckExitContacts.Add(instance);
		Coordinate val2 = new Coordinate(3245f, 35.715f, 939f);
		Quaternion arg = new Quaternion(0.0, -0.8022832870483398, 0.0, 0.5969434976577759);
		((IStats)character).Stats[(StatIds)192].BaseValue = 7001u;
		stopMovement(character);
		teleportToPlayfield(val, val2, arg, 655);
		CultureInfo invariantCulture = CultureInfo.InvariantCulture;
		object[] array = new object[9];
		identity = ((IEntity)character).Identity;
		array[0] = ((Identity)(ref identity)).ToString(true);
		array[1] = ((Identity)(ref playfieldIdentity)).Instance;
		array[2] = x;
		array[3] = y;
		array[4] = z;
		array[5] = 655;
		array[6] = val2.x;
		array[7] = val2.y;
		array[8] = val2.z;
		LogUtil.Debug((DebugInfoDetail)64, string.Format(invariantCulture, "HoloDeck exit teleport character={0} sourcePf={1} source=({2:F3},{3:F3},{4:F3}) destPf={5} dest=({6:F3},{7:F3},{8:F3}) evidence=20260719-155043", array));
		return true;
	}

	private bool TryHandleCapturedMontroyalPrivateCityEntry(ICharacter character, Identity playfieldIdentity, Func<ICharacter, int> resolvePrivateCityDestinationPlayfield, Func<ICharacter, int> resolveCharacterOrganizationInstance, Action<ICharacter> stopMovement, Action<ICharacter> sendCapturedPrivateCityEntrySocialStatus, Action<Dynel, Coordinate, Quaternion, int> teleportToPlayfield)
	{
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Expected O, but got Unknown
		//IL_0164: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || ((Identity)(ref playfieldIdentity)).Instance != 655 || ((IDynel)character).Controller == null || ((IDynel)character).Controller.Client == null || ((IInstancedEntity)character).DoNotDoTimers)
		{
			return false;
		}
		Dynel val = (Dynel)(object)((character is Dynel) ? character : null);
		if (val == null)
		{
			return false;
		}
		float x = ((IDynel)character).RawCoordinates.X;
		float y = ((IDynel)character).RawCoordinates.Y;
		float z = ((IDynel)character).RawCoordinates.Z;
		double num = x - 3140.412f;
		double num2 = z - 799.8611f;
		double num3 = num * num + num2 * num2;
		double num4 = Math.Abs(y - 51.54391f);
		if (num3 > 6.25 || num4 > 6.0)
		{
			return false;
		}
		int num5 = resolvePrivateCityDestinationPlayfield(character);
		if (num5 <= 0)
		{
			return false;
		}
		Coordinate val2 = ResolveCapturedMontroyalEntryDestination(num5);
		Quaternion arg = new Quaternion(0.0, 1.0, 0.0, -4.371138828673793E-08);
		stopMovement(character);
		sendCapturedPrivateCityEntrySocialStatus(character);
		teleportToPlayfield(val, val2, arg, num5);
		CultureInfo invariantCulture = CultureInfo.InvariantCulture;
		object[] array = new object[10];
		Identity identity = ((IEntity)character).Identity;
		array[0] = ((Identity)(ref identity)).ToString(true);
		array[1] = ((Identity)(ref playfieldIdentity)).Instance;
		array[2] = x;
		array[3] = y;
		array[4] = z;
		array[5] = num5;
		array[6] = val2.x;
		array[7] = val2.y;
		array[8] = val2.z;
		array[9] = resolveCharacterOrganizationInstance(character);
		LogUtil.Debug((DebugInfoDetail)64, string.Format(invariantCulture, "Montroyal private city entry teleport character={0} sourcePf={1} source=({2:F3},{3:F3},{4:F3}) destPf={5} dest=({6:F3},{7:F3},{8:F3}) org={9} evidence=live_capture_20260622-101935 live_capture_20260623-021643", array));
		return true;
	}

	private bool TryHandleUserConfirmedMontroyalPrivateCityExit(ICharacter character, Identity playfieldIdentity, Action<ICharacter> stopMovement, Action<Dynel, Coordinate, Quaternion, int> teleportToPlayfield)
	{
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Expected O, but got Unknown
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Expected O, but got Unknown
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || !IsCapturedMontroyalPrivateCityInstance(((Identity)(ref playfieldIdentity)).Instance) || ((IDynel)character).Controller == null || ((IDynel)character).Controller.Client == null || ((IInstancedEntity)character).DoNotDoTimers)
		{
			return false;
		}
		Dynel val = (Dynel)(object)((character is Dynel) ? character : null);
		if (val == null)
		{
			return false;
		}
		float x = ((IDynel)character).RawCoordinates.X;
		float y = ((IDynel)character).RawCoordinates.Y;
		float z = ((IDynel)character).RawCoordinates.Z;
		double num = x - 530.4664f;
		double num2 = z - 590.7054f;
		double num3 = num * num + num2 * num2;
		double num4 = Math.Abs(y - 160.6381f);
		if (num3 > 9.0 || num4 > 12.0)
		{
			return false;
		}
		Coordinate val2 = new Coordinate(3138.2f, 51.4f, 812.8f);
		Quaternion arg = new Quaternion(0.0, 0.9991580843925476, 0.0, 0.041025109589099884);
		stopMovement(character);
		teleportToPlayfield(val, val2, arg, 655);
		CultureInfo invariantCulture = CultureInfo.InvariantCulture;
		object[] array = new object[9];
		Identity identity = ((IEntity)character).Identity;
		array[0] = ((Identity)(ref identity)).ToString(true);
		array[1] = ((Identity)(ref playfieldIdentity)).Instance;
		array[2] = x;
		array[3] = y;
		array[4] = z;
		array[5] = 655;
		array[6] = val2.x;
		array[7] = val2.y;
		array[8] = val2.z;
		LogUtil.Debug((DebugInfoDetail)64, string.Format(invariantCulture, "Montroyal private city exit teleport character={0} sourceInstance={1} source=({2:F3},{3:F3},{4:F3}) destPf={5} dest=({6:F3},{7:F3},{8:F3}) evidence=live_capture_20260622-101935 user_extended_location_20260622_180812", array));
		return true;
	}
}
