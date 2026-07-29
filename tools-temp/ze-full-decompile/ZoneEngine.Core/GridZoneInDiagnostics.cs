using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AORebirth.Core.Entities;
using AORebirth.Core.Playfields;
using AORebirth.Core.Statels;
using AORebirth.Core.Vector;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.Playfields;

namespace ZoneEngine.Core;

public static class GridZoneInDiagnostics
{
	private sealed class GridZoneInContext
	{
		public int CharacterId { get; set; }

		public int SourcePlayfieldId { get; set; }

		public int SourceTerminalTypeId { get; set; }

		public int SourceTerminalInstance { get; set; }

		public int SourceTerminalTemplateId { get; set; }

		public int DestinationPlayfieldId { get; set; }

		public int DestinationStatelTypeId { get; set; }

		public int DestinationStatelInstance { get; set; }

		public int DestinationStatelTemplateId { get; set; }

		public int RawDestinationInstance { get; set; }

		public float LandingX { get; set; }

		public float LandingY { get; set; }

		public float LandingZ { get; set; }

		public string RouteKind { get; set; }

		public string Evidence { get; set; }

		public DateTime PendingExpiresAt { get; set; }

		public DateTime ActiveExpiresAt { get; set; }
	}

	private sealed class ObjectDetails
	{
		public bool HasIdentity { get; set; }

		public int PlayfieldId { get; set; }

		public int ObjectTypeId { get; set; }

		public int ObjectInstance { get; set; }

		public string ObjectTypeName { get; set; }

		public string Coordinates { get; set; }

		public string Heading { get; set; }

		public string ModelResourceMesh { get; set; }
	}

	private const int GridPlayfieldId = 152;

	private const int SuspiciousValue = 18;

	private const int GridExitTerminalTemplateId = 95351;

	private static readonly TimeSpan PendingContextLifetime = TimeSpan.FromSeconds(45.0);

	private static readonly TimeSpan ActiveZoneInLifetime = TimeSpan.FromSeconds(15.0);

	private static readonly object SyncRoot = new object();

	private static readonly Dictionary<int, GridZoneInContext> PendingContexts = new Dictionary<int, GridZoneInContext>();

	private static readonly Dictionary<int, GridZoneInContext> ActiveContexts = new Dictionary<int, GridZoneInContext>();

	public static void RecordGridEntry(ICharacter character, StatelData sourceTerminal, StatelData destinationTerminal, Coordinate landing, string routeKind, string evidence, int rawDestinationInstance)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Expected I4, but got Unknown
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		if (character != null && sourceTerminal != null && landing != null)
		{
			GridZoneInContext gridZoneInContext = new GridZoneInContext();
			Identity identity = ((IEntity)character).Identity;
			gridZoneInContext.CharacterId = ((Identity)(ref identity)).Instance;
			gridZoneInContext.SourcePlayfieldId = sourceTerminal.PlayfieldId;
			identity = sourceTerminal.Identity;
			gridZoneInContext.SourceTerminalTypeId = (int)((Identity)(ref identity)).Type;
			identity = sourceTerminal.Identity;
			gridZoneInContext.SourceTerminalInstance = ((Identity)(ref identity)).Instance;
			gridZoneInContext.SourceTerminalTemplateId = sourceTerminal.TemplateId;
			gridZoneInContext.DestinationPlayfieldId = 152;
			int destinationStatelTypeId;
			if (destinationTerminal != null)
			{
				identity = destinationTerminal.Identity;
				destinationStatelTypeId = (int)((Identity)(ref identity)).Type;
			}
			else
			{
				destinationStatelTypeId = 0;
			}
			gridZoneInContext.DestinationStatelTypeId = destinationStatelTypeId;
			int destinationStatelInstance;
			if (destinationTerminal != null)
			{
				identity = destinationTerminal.Identity;
				destinationStatelInstance = ((Identity)(ref identity)).Instance;
			}
			else
			{
				destinationStatelInstance = 0;
			}
			gridZoneInContext.DestinationStatelInstance = destinationStatelInstance;
			gridZoneInContext.DestinationStatelTemplateId = destinationTerminal?.TemplateId ?? 0;
			gridZoneInContext.RawDestinationInstance = rawDestinationInstance;
			gridZoneInContext.LandingX = landing.x;
			gridZoneInContext.LandingY = landing.y;
			gridZoneInContext.LandingZ = landing.z;
			gridZoneInContext.RouteKind = routeKind ?? string.Empty;
			gridZoneInContext.Evidence = evidence ?? string.Empty;
			gridZoneInContext.PendingExpiresAt = DateTime.UtcNow + PendingContextLifetime;
			GridZoneInContext gridZoneInContext2 = gridZoneInContext;
			lock (SyncRoot)
			{
				PendingContexts[gridZoneInContext2.CharacterId] = gridZoneInContext2;
			}
			LogGridRoute(gridZoneInContext2);
			LogGridExitComparison(gridZoneInContext2);
			WarnIfSuspiciousRouteValues(gridZoneInContext2);
		}
	}

	public static void BeginGridZoneIn(ZoneClient client)
	{
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		if (client == null || client.Controller == null || client.Controller.Character == null)
		{
			return;
		}
		ICharacter character = client.Controller.Character;
		if (((IInstancedEntity)character).Playfield == null)
		{
			return;
		}
		Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		if (((Identity)(ref identity)).Instance != 152)
		{
			return;
		}
		GridZoneInContext value;
		lock (SyncRoot)
		{
			CleanupExpiredContexts();
			Dictionary<int, GridZoneInContext> pendingContexts = PendingContexts;
			identity = ((IEntity)character).Identity;
			if (!pendingContexts.TryGetValue(((Identity)(ref identity)).Instance, out value))
			{
				Coordinate val = ((IDynel)character).Coordinates();
				GridZoneInContext gridZoneInContext = new GridZoneInContext();
				identity = ((IEntity)character).Identity;
				gridZoneInContext.CharacterId = ((Identity)(ref identity)).Instance;
				gridZoneInContext.DestinationPlayfieldId = 152;
				gridZoneInContext.LandingX = val.x;
				gridZoneInContext.LandingY = val.y;
				gridZoneInContext.LandingZ = val.z;
				gridZoneInContext.RouteKind = "GridZoneInWithoutRecordedSource";
				gridZoneInContext.Evidence = "No in-process grid terminal context was recorded before this zone login.";
				value = gridZoneInContext;
			}
			else
			{
				Dictionary<int, GridZoneInContext> pendingContexts2 = PendingContexts;
				identity = ((IEntity)character).Identity;
				pendingContexts2.Remove(((Identity)(ref identity)).Instance);
			}
			value.ActiveExpiresAt = DateTime.UtcNow + ActiveZoneInLifetime;
			Dictionary<int, GridZoneInContext> activeContexts = ActiveContexts;
			identity = ((IEntity)character).Identity;
			activeContexts[((Identity)(ref identity)).Instance] = value;
		}
		CultureInfo invariantCulture = CultureInfo.InvariantCulture;
		object[] array = new object[11];
		identity = ((IEntity)character).Identity;
		array[0] = ((Identity)(ref identity)).ToString(true);
		array[1] = 152;
		array[2] = IdentityPart(value.SourceTerminalTypeId, value.SourceTerminalInstance);
		array[3] = value.SourceTerminalTemplateId;
		array[4] = IdentityPart(value.DestinationStatelTypeId, value.DestinationStatelInstance);
		array[5] = value.DestinationStatelTemplateId;
		array[6] = value.LandingX;
		array[7] = value.LandingY;
		array[8] = value.LandingZ;
		array[9] = value.RouteKind;
		array[10] = value.Evidence;
		LogUtil.Debug((DebugInfoDetail)128, string.Format(invariantCulture, "GRID_ZONE_IN_BEGIN char={0} playfield={1} sourceTerminal={2} sourceTemplate={3} destStatel={4} destTemplate={5} landing=({6:F3},{7:F3},{8:F3}) route={9} evidence={10}", array));
	}

	public static void LogOutboundMessage(ZoneClient client, MessageBody messageBody)
	{
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		if (client == null || messageBody == null || client.Controller == null || client.Controller.Character == null)
		{
			return;
		}
		GridZoneInContext value;
		lock (SyncRoot)
		{
			CleanupExpiredContexts();
			Dictionary<int, GridZoneInContext> activeContexts = ActiveContexts;
			Identity identity = ((IEntity)client.Controller.Character).Identity;
			if (!activeContexts.TryGetValue(((Identity)(ref identity)).Instance, out value))
			{
				return;
			}
		}
		N3Message val = (N3Message)(object)((messageBody is N3Message) ? messageBody : null);
		if (val != null)
		{
			ObjectDetails objectDetails = BuildObjectDetails(client, val);
			if (objectDetails.HasIdentity)
			{
				LogUtil.Debug((DebugInfoDetail)128, string.Format(CultureInfo.InvariantCulture, "GRID_ZONE_IN_OBJECT message={0} playfield={1} sourceTerminal={2} sourceTemplate={3} destStatel={4} destTemplate={5} object={6} objectTypeId={7} objectTypeName={8} coords={9} heading={10} modelResourceMesh={11}", ((object)val).GetType().Name, objectDetails.PlayfieldId, IdentityPart(value.SourceTerminalTypeId, value.SourceTerminalInstance), value.SourceTerminalTemplateId, IdentityPart(value.DestinationStatelTypeId, value.DestinationStatelInstance), value.DestinationStatelTemplateId, IdentityPart(objectDetails.ObjectTypeId, objectDetails.ObjectInstance), objectDetails.ObjectTypeId, objectDetails.ObjectTypeName, objectDetails.Coordinates, objectDetails.Heading, objectDetails.ModelResourceMesh));
				WarnIfSuspiciousObjectValues(value, objectDetails, ((object)val).GetType().Name);
				WarnIfVehicle(objectDetails, ((object)val).GetType().Name);
			}
		}
	}

	private static ObjectDetails BuildObjectDetails(ZoneClient client, N3Message message)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Expected I4, but got Unknown
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Expected I4, but got Unknown
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Expected O, but got Unknown
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Expected O, but got Unknown
		//IL_0195: Unknown result type (might be due to invalid IL or missing references)
		//IL_019c: Expected O, but got Unknown
		//IL_0205: Unknown result type (might be due to invalid IL or missing references)
		//IL_020c: Expected O, but got Unknown
		//IL_02cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0247: Unknown result type (might be due to invalid IL or missing references)
		//IL_024e: Expected O, but got Unknown
		//IL_0271: Unknown result type (might be due to invalid IL or missing references)
		//IL_0276: Unknown result type (might be due to invalid IL or missing references)
		//IL_0309: Unknown result type (might be due to invalid IL or missing references)
		//IL_030e: Unknown result type (might be due to invalid IL or missing references)
		ObjectDetails objectDetails = new ObjectDetails();
		Identity val = message.Identity;
		objectDetails.ObjectTypeId = (int)((Identity)(ref val)).Type;
		val = message.Identity;
		objectDetails.ObjectInstance = ((Identity)(ref val)).Instance;
		val = message.Identity;
		objectDetails.ObjectTypeName = ResolveIdentityTypeName((int)((Identity)(ref val)).Type);
		val = message.Identity;
		int hasIdentity;
		if ((int)((Identity)(ref val)).Type == 0)
		{
			val = message.Identity;
			hasIdentity = ((((Identity)(ref val)).Instance != 0) ? 1 : 0);
		}
		else
		{
			hasIdentity = 1;
		}
		objectDetails.HasIdentity = (byte)hasIdentity != 0;
		objectDetails.PlayfieldId = 152;
		objectDetails.Coordinates = "n/a";
		objectDetails.Heading = "n/a";
		objectDetails.ModelResourceMesh = "n/a";
		ObjectDetails objectDetails2 = objectDetails;
		if (message is PlayfieldAnarchyFMessage)
		{
			PlayfieldAnarchyFMessage val2 = (PlayfieldAnarchyFMessage)message;
			val = val2.PlayfieldId2;
			objectDetails2.PlayfieldId = ((Identity)(ref val)).Instance;
			objectDetails2.Coordinates = FormatVector(val2.CharacterCoordinates);
			objectDetails2.ModelResourceMesh = string.Format(CultureInfo.InvariantCulture, "playfieldX={0};playfieldZ={1}", val2.PlayfieldX, val2.PlayfieldZ);
			return objectDetails2;
		}
		if (message is VendingMachineFullUpdateMessage)
		{
			VendingMachineFullUpdateMessage val3 = (VendingMachineFullUpdateMessage)message;
			objectDetails2.PlayfieldId = val3.PlayfieldId;
			objectDetails2.Coordinates = FormatVector(val3.Coordinates);
			objectDetails2.Heading = FormatQuaternion(val3.Heading);
			objectDetails2.ModelResourceMesh = BuildStatMeshSummary(val3.Stats, val3.TypeIdentifier);
			return objectDetails2;
		}
		if (message is SimpleCharFullUpdateMessage)
		{
			SimpleCharFullUpdateMessage val4 = (SimpleCharFullUpdateMessage)message;
			objectDetails2.PlayfieldId = val4.PlayfieldId.GetValueOrDefault(152);
			objectDetails2.Coordinates = FormatVector(val4.Coordinates);
			objectDetails2.Heading = FormatQuaternion(val4.Heading);
			objectDetails2.ModelResourceMesh = BuildSimpleCharMeshSummary(val4);
			return objectDetails2;
		}
		if (message is WeaponItemFullUpdateMessage)
		{
			WeaponItemFullUpdateMessage val5 = (WeaponItemFullUpdateMessage)message;
			objectDetails2.PlayfieldId = val5.PlayfieldId;
			objectDetails2.ModelResourceMesh = BuildStatMeshSummary(val5.Stats, 0);
			return objectDetails2;
		}
		if (message is FullCharacterMessage)
		{
			FullCharacterMessage message2 = (FullCharacterMessage)message;
			Coordinate coordinate = ((IDynel)client.Controller.Character).Coordinates();
			val = ((IEntity)((IInstancedEntity)client.Controller.Character).Playfield).Identity;
			objectDetails2.PlayfieldId = ((Identity)(ref val)).Instance;
			objectDetails2.Coordinates = FormatCoordinate(coordinate);
			objectDetails2.Heading = FormatCoreQuaternion(((IDynel)client.Controller.Character).Heading);
			objectDetails2.ModelResourceMesh = BuildFullCharacterMeshSummary(message2);
			return objectDetails2;
		}
		if (((IEntity)client.Controller.Character).Identity == message.Identity)
		{
			Coordinate coordinate2 = ((IDynel)client.Controller.Character).Coordinates();
			val = ((IEntity)((IInstancedEntity)client.Controller.Character).Playfield).Identity;
			objectDetails2.PlayfieldId = ((Identity)(ref val)).Instance;
			objectDetails2.Coordinates = FormatCoordinate(coordinate2);
			objectDetails2.Heading = FormatCoreQuaternion(((IDynel)client.Controller.Character).Heading);
		}
		return objectDetails2;
	}

	private static string BuildSimpleCharMeshSummary(SimpleCharFullUpdateMessage message)
	{
		List<string> list = new List<string>
		{
			"monsterData=" + message.MonsterData.ToString(CultureInfo.InvariantCulture),
			"monsterScale=" + message.MonsterScale.ToString(CultureInfo.InvariantCulture)
		};
		if (message.HeadMesh.HasValue)
		{
			list.Add("headMesh=" + message.HeadMesh.Value.ToString(CultureInfo.InvariantCulture));
		}
		if (message.Meshes != null && message.Meshes.Length != 0)
		{
			list.Add("meshes=" + string.Join("|", message.Meshes.Select((Mesh x) => string.Format(CultureInfo.InvariantCulture, "{0}:{1}:{2}:{3}", x.Position, x.Id, x.OverrideTextureId, x.Layer)).ToArray()));
		}
		return string.Join(";", list.ToArray());
	}

	private static string BuildStatMeshSummary(GameTuple<CharacterStat, uint>[] stats, int typeIdentifier)
	{
		List<string> list = new List<string>();
		if (typeIdentifier != 0)
		{
			list.Add("typeIdentifier=" + typeIdentifier.ToString(CultureInfo.InvariantCulture));
		}
		if (stats == null)
		{
			return (list.Count == 0) ? "n/a" : string.Join(";", list.ToArray());
		}
		AddStatValue(list, stats, 12, "mesh");
		AddStatValue(list, stats, 64, "headMesh");
		AddStatValue(list, stats, 359, "monsterData");
		AddStatValue(list, stats, 702, "templateId");
		return (list.Count == 0) ? "n/a" : string.Join(";", list.ToArray());
	}

	private static string BuildFullCharacterMeshSummary(FullCharacterMessage message)
	{
		List<string> list = new List<string>();
		AddStatValue(list, message.Stats1, 12, "mesh");
		AddStatValue(list, message.Stats1, 64, "headMesh");
		AddStatValue(list, message.Stats1, 359, "monsterData");
		AddStatValue(list, message.Stats2, 12, "mesh2");
		AddStatValue(list, message.Stats2, 64, "headMesh2");
		AddStatValue(list, message.Stats2, 359, "monsterData2");
		return (list.Count == 0) ? "n/a" : string.Join(";", list.ToArray());
	}

	private static void AddStatValue(ICollection<string> parts, GameTuple<CharacterStat, uint>[] stats, int statId, string label)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Invalid comparison between Unknown and I4
		if (stats == null)
		{
			return;
		}
		foreach (GameTuple<CharacterStat, uint> val in stats)
		{
			if ((int)val.Value1 == statId)
			{
				parts.Add(label + "=" + val.Value2.ToString(CultureInfo.InvariantCulture));
				break;
			}
		}
	}

	private static void AddStatValue(ICollection<string> parts, GameTuple<int, uint>[] stats, int statId, string label)
	{
		if (stats == null)
		{
			return;
		}
		foreach (GameTuple<int, uint> val in stats)
		{
			if (val.Value1 == statId)
			{
				parts.Add(label + "=" + val.Value2.ToString(CultureInfo.InvariantCulture));
				break;
			}
		}
	}

	private static void WarnIfSuspiciousObjectValues(GridZoneInContext context, ObjectDetails details, string messageName)
	{
		WarnIfSuspiciousValue("message=" + messageName + " playfieldId", details.PlayfieldId, context);
		WarnIfSuspiciousValue("message=" + messageName + " objectInstance", details.ObjectInstance, context);
		WarnIfSuspiciousValue("message=" + messageName + " objectTypeId", details.ObjectTypeId, context);
	}

	private static void WarnIfSuspiciousRouteValues(GridZoneInContext context)
	{
		WarnIfSuspiciousValue("sourcePlayfieldId", context.SourcePlayfieldId, context);
		WarnIfSuspiciousValue("sourceTerminalTypeId", context.SourceTerminalTypeId, context);
		WarnIfSuspiciousValue("sourceTerminalInstance", context.SourceTerminalInstance, context);
		WarnIfSuspiciousValue("sourceTerminalTemplateId", context.SourceTerminalTemplateId, context);
		WarnIfSuspiciousValue("destinationPlayfieldId", context.DestinationPlayfieldId, context);
		WarnIfSuspiciousValue("destinationStatelTypeId", context.DestinationStatelTypeId, context);
		WarnIfSuspiciousValue("destinationStatelInstance", context.DestinationStatelInstance, context);
		WarnIfSuspiciousValue("destinationStatelTemplateId", context.DestinationStatelTemplateId, context);
		WarnIfSuspiciousValue("rawDestinationInstance", context.RawDestinationInstance, context);
	}

	private static void WarnIfSuspiciousValue(string field, int value, GridZoneInContext context)
	{
		if (value == 18)
		{
			LogUtil.Debug((DebugInfoDetail)512, string.Format(CultureInfo.InvariantCulture, "GRID_ZONE_IN_WARNING value_0x12 field={0} value={1} sourceTerminal={2} destStatel={3} note=0x12 maps to StatIds.Stamina when interpreted as a character stat id; it is not a known IdentityType in AORebirth.", field, value, IdentityPart(context.SourceTerminalTypeId, context.SourceTerminalInstance), IdentityPart(context.DestinationStatelTypeId, context.DestinationStatelInstance)));
		}
	}

	private static void WarnIfVehicle(ObjectDetails details, string messageName)
	{
		if (details.ObjectTypeName.IndexOf("Vehicle", StringComparison.OrdinalIgnoreCase) >= 0 || messageName.IndexOf("Vehicle", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			LogUtil.Debug((DebugInfoDetail)512, string.Format(CultureInfo.InvariantCulture, "GRID_ZONE_IN_WARNING vehicle_route message={0} object={1} objectTypeId={2} objectTypeName={3}", messageName, IdentityPart(details.ObjectTypeId, details.ObjectInstance), details.ObjectTypeId, details.ObjectTypeName));
		}
	}

	private static void LogGridRoute(GridZoneInContext context)
	{
		LogUtil.Debug((DebugInfoDetail)128, string.Format(CultureInfo.InvariantCulture, "GRID_ROUTE sourcePf={0} sourceTerminal={1} sourceTemplate={2} rawDestTerminal={3} destPf={4} destStatel={5} destTemplate={6} landing=({7:F3},{8:F3},{9:F3}) route={10} evidence={11}", context.SourcePlayfieldId, IdentityPart(context.SourceTerminalTypeId, context.SourceTerminalInstance), context.SourceTerminalTemplateId, IdentityPart(51005, context.RawDestinationInstance), context.DestinationPlayfieldId, IdentityPart(context.DestinationStatelTypeId, context.DestinationStatelInstance), context.DestinationStatelTemplateId, context.LandingX, context.LandingY, context.LandingZ, context.RouteKind, context.Evidence));
	}

	private static void LogGridExitComparison(GridZoneInContext context)
	{
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		if (!PlayfieldLoader.PFData.TryGetValue(152, out var value))
		{
			return;
		}
		StatelData val = FindGridExit(value, context.DestinationStatelInstance);
		StatelData val2 = FindGridExit(value, context.RawDestinationInstance);
		if (val != null)
		{
			string text = FormatNearbyStatels(value, val);
			string text2 = ((val2 == null) ? "missing" : FormatNearbyStatels(value, val2));
			CultureInfo invariantCulture = CultureInfo.InvariantCulture;
			object[] obj = new object[5]
			{
				IdentityPart(context.SourceTerminalTypeId, context.SourceTerminalInstance),
				null,
				null,
				null,
				null
			};
			Identity identity;
			object obj2;
			if (val2 != null)
			{
				identity = val2.Identity;
				obj2 = ((Identity)(ref identity)).ToString(true);
			}
			else
			{
				obj2 = "missing";
			}
			obj[1] = obj2;
			identity = val.Identity;
			obj[2] = ((Identity)(ref identity)).ToString(true);
			obj[3] = text2;
			obj[4] = text;
			LogUtil.Debug((DebugInfoDetail)128, string.Format(invariantCulture, "GRID_EXIT_COMPARE sourceTerminal={0} rawExit={1} expectedExit={2} rawNearby=[{3}] expectedNearby=[{4}]", obj));
		}
	}

	private static StatelData FindGridExit(PlayfieldData gridPlayfield, int instance)
	{
		return gridPlayfield.Statels.FirstOrDefault(delegate(StatelData x)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			Identity identity = x.Identity;
			return ((Identity)(ref identity)).Instance == instance && x.TemplateId == 95351;
		});
	}

	private static string FormatNearbyStatels(PlayfieldData playfield, StatelData center)
	{
		return string.Join("|", (from x in playfield.Statels
			where Distance2D(x, center) <= 8f
			orderby Distance2D(x, center)
			select x).ThenBy(delegate(StatelData x)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			Identity identity2 = x.Identity;
			return ((Identity)(ref identity2)).Instance;
		}).Take(12).Select(delegate(StatelData x)
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Expected I4, but got Unknown
			CultureInfo invariantCulture = CultureInfo.InvariantCulture;
			object[] array = new object[7];
			Identity identity = x.Identity;
			array[0] = ((Identity)(ref identity)).ToString(true);
			array[1] = x.TemplateId;
			identity = x.Identity;
			array[2] = ResolveIdentityTypeName((int)((Identity)(ref identity)).Type);
			array[3] = x.X;
			array[4] = x.Y;
			array[5] = x.Z;
			array[6] = Distance2D(x, center);
			return string.Format(invariantCulture, "{0};template={1};type={2};coords=({3:F1},{4:F1},{5:F1});distance={6:F2}", array);
		})
			.ToArray());
	}

	private static float Distance2D(StatelData left, StatelData right)
	{
		float num = left.X - right.X;
		float num2 = left.Z - right.Z;
		return (float)Math.Sqrt(num * num + num2 * num2);
	}

	private static string ResolveIdentityTypeName(int typeId)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		if (Enum.IsDefined(typeof(IdentityType), typeId))
		{
			IdentityType val = (IdentityType)typeId;
			return ((object)(IdentityType)(ref val)).ToString();
		}
		return "UnknownIdentityType";
	}

	private static string IdentityPart(int typeId, int instance)
	{
		return string.Format(CultureInfo.InvariantCulture, "{0}:{1}", typeId.ToString("X8", CultureInfo.InvariantCulture), instance.ToString("X8", CultureInfo.InvariantCulture));
	}

	private static string FormatCoordinate(Coordinate coordinate)
	{
		if (coordinate == null)
		{
			return "n/a";
		}
		return string.Format(CultureInfo.InvariantCulture, "({0:F3},{1:F3},{2:F3})", coordinate.x, coordinate.y, coordinate.z);
	}

	private static string FormatVector(Vector3 vector)
	{
		return string.Format(CultureInfo.InvariantCulture, "({0:F3},{1:F3},{2:F3})", vector.X, vector.Y, vector.Z);
	}

	private static string FormatCoreQuaternion(Quaternion heading)
	{
		if (heading == null)
		{
			return "n/a";
		}
		return string.Format(CultureInfo.InvariantCulture, "({0:F6},{1:F6},{2:F6},{3:F6})", heading.xf, heading.yf, heading.zf, heading.wf);
	}

	private static string FormatQuaternion(Quaternion heading)
	{
		if (heading == null)
		{
			return "n/a";
		}
		return string.Format(CultureInfo.InvariantCulture, "({0:F6},{1:F6},{2:F6},{3:F6})", heading.X, heading.Y, heading.Z, heading.W);
	}

	private static void CleanupExpiredContexts()
	{
		DateTime now = DateTime.UtcNow;
		foreach (int item in (from x in PendingContexts
			where x.Value.PendingExpiresAt <= now
			select x.Key).ToList())
		{
			PendingContexts.Remove(item);
		}
		foreach (int item2 in (from x in ActiveContexts
			where x.Value.ActiveExpiresAt <= now
			select x.Key).ToList())
		{
			ActiveContexts.Remove(item2);
		}
	}
}
