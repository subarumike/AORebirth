using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AORebirth.Core.Entities;
using AORebirth.Core.Events;
using AORebirth.Core.Functions;
using AORebirth.Core.Network;
using AORebirth.Core.Playfields;
using AORebirth.Core.Requirements;
using AORebirth.Core.Statels;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using Cell.Core;
using MsgPack;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Playfields;

namespace ZoneEngine.Core.MessageHandlers;

public sealed class GridTerminalInteractionHandler
{
	private sealed class CapturedGridTerminalRoute
	{
		public int SourcePlayfieldId { get; private set; }

		public int SourceTerminalInstance { get; private set; }

		public int DestinationExitTerminalInstance { get; private set; }

		public float DestinationX { get; private set; }

		public float DestinationY { get; private set; }

		public float DestinationZ { get; private set; }

		public float HeadingX { get; private set; }

		public float HeadingY { get; private set; }

		public float HeadingZ { get; private set; }

		public float HeadingW { get; private set; }

		public string Evidence { get; private set; }

		public CapturedGridTerminalRoute(int sourcePlayfieldId, int sourceTerminalInstance, int destinationExitTerminalInstance, float destinationX, float destinationY, float destinationZ, float headingX, float headingY, float headingZ, float headingW, string evidence)
		{
			SourcePlayfieldId = sourcePlayfieldId;
			SourceTerminalInstance = sourceTerminalInstance;
			DestinationExitTerminalInstance = destinationExitTerminalInstance;
			DestinationX = destinationX;
			DestinationY = destinationY;
			DestinationZ = destinationZ;
			HeadingX = headingX;
			HeadingY = headingY;
			HeadingZ = headingZ;
			HeadingW = headingW;
			Evidence = evidence;
		}
	}

	public static readonly GridTerminalInteractionHandler Default = new GridTerminalInteractionHandler();

	private const int CapturedGridPlayfieldId = 152;

	private const int GridEnterTerminalTemplateId = 95350;

	private const int GridExitTerminalTemplateId = 95351;

	private const float GridDestinationTerminalClearance = 2.5f;

	private static readonly CapturedGridTerminalRoute[] CapturedGridTerminalRoutes = new CapturedGridTerminalRoute[26]
	{
		new CapturedGridTerminalRoute(567, -1073675721, -1072889704, 177.7f, 3.8f, 181.7f, 0f, 0f, 0f, 1f, "user supplied Newland grid-side exit anchor 2026-06-22;source Terminal:C0010237;PF152 nearest exit Terminal:C00D0098"),
		new CapturedGridTerminalRoute(640, -1073544576, -1073020776, 156f, 3.8f, 185.1f, 0f, 0f, 0f, 1f, "user supplied Tir grid-side exit anchor 2026-06-22;source Terminal:C0030280;PF152 nearest exit Terminal:C00B0098"),
		new CapturedGridTerminalRoute(710, -1073413434, -1073741672, 165.2f, 3.8f, 235f, 0f, 0f, 0f, 1f, "user supplied Omni Trade grid-side exit anchor 2026-06-22;source Terminal:C00502C6;PF152 nearest exit Terminal:C0000098"),
		new CapturedGridTerminalRoute(540, -1073085924, -1068892008, 210.2f, 3.8f, 172.8f, 0f, 0f, 0f, 1f, "user supplied Old Athen grid-side exit anchor 2026-06-22;source Terminal:C00A021C;PF152 nearest exit Terminal:C04A0098"),
		new CapturedGridTerminalRoute(556, -1073610196, -1068433256, 202.1f, 3.8f, 249.8f, 0f, 0f, 0f, 1f, "user supplied Coast of Peace grid-side landing 2026-06-22;source Terminal:C002022C;PF152 nearest exit Terminal:C0510098"),
		new CapturedGridTerminalRoute(565, -1073413579, -1072955240, 169.5f, 37.4f, 165.2f, 0f, 0f, 0f, 1f, "user supplied Newland Desert grid-side landing 2026-06-22;source Terminal:C0050235;PF152 nearest exit Terminal:C00C0098"),
		new CapturedGridTerminalRoute(635, -1073544581, -1073413992, 188.7f, 37.4f, 211.1f, 0f, 0f, 0f, 1f, "user supplied Stret East Bank grid-side landing 2026-06-22;source Terminal:C003027B;PF152 nearest exit Terminal:C0050098"),
		new CapturedGridTerminalRoute(646, -1073479034, -1073020776, 155.4f, 3.8f, 185.5f, 0f, 0f, 0f, 1f, "user supplied Tir County grid-side landing 2026-06-22;source Terminal:C0040286;PF152 nearest exit Terminal:C00B0098"),
		new CapturedGridTerminalRoute(656, -1073610096, -1068367720, 219.2f, 3.8f, 246.4f, 0f, 0f, 0f, 1f, "user supplied Coast of Tranquility grid-side landing 2026-06-22;source Terminal:C0020290;PF152 nearest exit Terminal:C0520098"),
		new CapturedGridTerminalRoute(6007, -1073735817, -1067974504, 209.4f, 3.8f, 210.5f, 0f, 0.707108f, 0f, 0.707105f, "user supplied Unicorn Defence Hub grid-side exit anchor 2026-06-22;source Terminal:C0001777;PF152 nearest exit Terminal:C0580098"),
		new CapturedGridTerminalRoute(655, -1073610097, -1068629864, 234.3062f, 3.775f, 212.8138f, 0f, 1f, 0f, -4.371139E-08f, "captures/20260621-091447/events.log:255-256,321-322,645-646;PF152 nearest exit Terminal:C04E0098"),
		new CapturedGridTerminalRoute(705, -1073347903, -1073676136, 174.8353f, 37.375f, 240.2071f, 0f, 1f, 0f, -4.371139E-08f, "captures/20260622-003221/events.log:2420-2423,2624-2625;PF152 nearest exit Terminal:C0010098"),
		new CapturedGridTerminalRoute(730, -1073741094, -1073676136, 174.8353f, 37.375f, 240.2071f, 0f, 1f, 0f, -4.371139E-08f, "captures/20260622-003221/events.log:3109-3112,3313-3314;PF152 nearest exit Terminal:C0010098"),
		new CapturedGridTerminalRoute(665, -1073741159, -1073086312, 239.6f, 37.4f, 221.6f, 0f, 0f, 0f, 1f, "user supplied Broken Shores grid-side landing 2026-06-22;source Terminal:C0000299;PF152 nearest exit Terminal:C00A0098"),
		new CapturedGridTerminalRoute(685, -1073413459, -1073217384, 215.9f, 37.4f, 225.7f, 0f, 0f, 0f, 1f, "user supplied Galway County grid-side landing 2026-06-22;source Terminal:C00502AD;PF152 nearest exit Terminal:C0080098"),
		new CapturedGridTerminalRoute(695, -1073282377, -1073479528, 185.1f, 37.4f, 227.4f, 0f, 0f, 0f, 1f, "user supplied Lush Fields Harry's grid-side landing 2026-06-22;source Terminal:C00702B7;PF152 nearest exit Terminal:C0040098"),
		new CapturedGridTerminalRoute(695, -1073216841, -1073545064, 188.4104f, 37.37542f, 234.9863f, 0f, 0.9952047f, 0f, 0.09781352f, "captures/20260622-003221/events.log:3806-3809,4010-4011;PF152 nearest exit Terminal:C0030098"),
		new CapturedGridTerminalRoute(670, -1073544546, -1073282920, 203.2488f, 37.38467f, 222.7339f, 0f, -0.9641039f, 0f, 0.2655251f, "captures/20260622-003221/events.log:5421-5424,5639-5640;PF152 nearest exit Terminal:C0070098"),
		new CapturedGridTerminalRoute(560, -1073348048, -1072824168, 183.9474f, 44.015f, 150.8788f, 0f, 0.7062106f, 0f, 0.7080019f, "captures/20260622-003221/events.log:7519-7522,7733-7734;PF152 nearest exit Terminal:C00E0098"),
		new CapturedGridTerminalRoute(705, -1073544511, -1073610600, 180.2f, 37.4f, 248f, 0f, 0f, 0f, 1f, "user supplied Omni-1 Entertainment South grid-side landing 2026-06-22;source Terminal:C00302C1;PF152 nearest exit Terminal:C0020098"),
		new CapturedGridTerminalRoute(700, -1073610052, -1073151848, 224.5596f, 44.005f, 231.8543f, 0f, -0.7107669f, 0f, 0.7034276f, "captures/20260622-003221/events.log:8380-8383,8594-8595;PF152 nearest exit Terminal:C0090098"),
		new CapturedGridTerminalRoute(505, -1071447559, -1072693096, 215.8618f, 43.995f, 151.7285f, 0f, 0.6904334f, 0f, 0.7233959f, "captures/20260622-003221/events.log:9339-9342,9563-9564;PF152 nearest exit Terminal:C0100098"),
		new CapturedGridTerminalRoute(760, -1073413384, -1073348456, 196.6f, 37.4f, 208.1f, 0f, 0f, 0f, 1f, "user supplied 4 Holes grid-side landing 2026-06-22;source Terminal:C00502F8;PF152 nearest exit Terminal:C0060098"),
		new CapturedGridTerminalRoute(800, -1073478880, -1068760936, 234.4f, 3.8f, 198.9f, 0f, 0f, 0f, 1f, "user supplied Borealis grid-side landing 2026-06-22;source Terminal:C0040320;PF152 nearest exit Terminal:C04C0098"),
		new CapturedGridTerminalRoute(6101, -1073735723, -1069023080, 218.3f, 3.8f, 190.7f, 0f, 0f, 0f, 1f, "user supplied Three Craters West grid-side landing 2026-06-22;source Terminal:C00017D5;PF152 nearest exit Terminal:C0480098"),
		new CapturedGridTerminalRoute(6102, -1073735722, -1069023080, 218.3f, 3.8f, 190.7f, 0f, 0f, 0f, 1f, "user supplied Three Craters East grid-side landing 2026-06-22;source Terminal:C00017D6;PF152 nearest exit Terminal:C0480098")
	};

	private GridTerminalInteractionHandler()
	{
	}

	public bool TryHandleCapturedUse(IZoneClient client, Identity target)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Expected O, but got Unknown
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Expected O, but got Unknown
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		ICharacter character = client.Controller.Character;
		StatelData statelData = GetStatelData(character, target);
		if (!TryGetCapturedGridTerminalRoute(character, target, statelData, out var route))
		{
			return false;
		}
		Dynel val = (Dynel)(object)((character is Dynel) ? character : null);
		if (val == null)
		{
			return false;
		}
		character.StopMovement();
		((IStats)character).Stats[(StatIds)193].BaseValue = 0u;
		((IStats)character).Stats[(StatIds)192].BaseValue = 0u;
		Coordinate val2 = new Coordinate(route.DestinationX, route.DestinationY, route.DestinationZ);
		Quaternion val3 = new Quaternion((double)route.HeadingX, (double)route.HeadingY, (double)route.HeadingZ, (double)route.HeadingW);
		if (!TryGetGridTeleportProxy2Destination(statelData, out var _, out var _, out var destinationInstance))
		{
			destinationInstance = 0;
		}
		TryGetGridDestinationTerminal(152, route.DestinationExitTerminalInstance, out var destinationTerminal);
		GridZoneInDiagnostics.RecordGridEntry(character, statelData, destinationTerminal, val2, "CapturedGridTerminalRoute", route.Evidence, destinationInstance);
		IPlayfield playfield = ((IInstancedEntity)character).Playfield;
		Identity val4 = default(Identity);
		((Identity)(ref val4)).Type = (IdentityType)51101;
		((Identity)(ref val4)).Instance = 152;
		playfield.Teleport(val, val2, (IQuaternion)(object)val3, val4);
		((IClient)client).Server.Info((IClient)(object)client, "Captured grid terminal use handled char={0} target={1} sourcePf={2} destPf={3} destExit={4:X8} dest=({5:F3},{6:F3},{7:F3}) evidence={8}", new object[9]
		{
			((IEntity)character).Identity,
			target,
			route.SourcePlayfieldId,
			152,
			(uint)route.DestinationExitTerminalInstance,
			val2.x,
			val2.y,
			val2.z,
			route.Evidence
		});
		return true;
	}

	public bool TryHandleGridEnterUse(IZoneClient client, Identity target)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f6: Expected O, but got Unknown
		//IL_0216: Unknown result type (might be due to invalid IL or missing references)
		//IL_021d: Expected O, but got Unknown
		//IL_0229: Unknown result type (might be due to invalid IL or missing references)
		//IL_0230: Expected O, but got Unknown
		//IL_02ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c1: Expected O, but got Unknown
		//IL_02cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fc: Expected O, but got Unknown
		//IL_0313: Unknown result type (might be due to invalid IL or missing references)
		//IL_0320: Unknown result type (might be due to invalid IL or missing references)
		//IL_0342: Unknown result type (might be due to invalid IL or missing references)
		//IL_018c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		ICharacter character = client.Controller.Character;
		StatelData statelData = GetStatelData(character, target);
		if (!IsGridEnterTerminal(character, target, statelData))
		{
			return false;
		}
		if (!TryGetGridTeleportProxy2Destination(statelData, out var teleportFunction, out var destinationPlayfieldId, out var destinationInstance))
		{
			((IClient)client).Server.Info((IClient)(object)client, "Grid enter terminal route skipped; no supported TeleportProxy2 char={0} target={1} sourcePf={2} template={3}", new object[4]
			{
				((IEntity)character).Identity,
				target,
				statelData.PlayfieldId,
				statelData.TemplateId
			});
			return false;
		}
		if (!GridTeleportRequirementsPass(character, teleportFunction))
		{
			SendGridTerminalRequirementFeedback(character, statelData, teleportFunction);
			((IClient)client).Server.Info((IClient)(object)client, "Grid enter terminal use blocked by requirements char={0} target={1} sourcePf={2} computerLiteracy={3} isfightingme={4}", new object[5]
			{
				((IEntity)character).Identity,
				target,
				statelData.PlayfieldId,
				((IStats)character).Stats[(StatIds)161].Value,
				((IStats)character).Stats[(StatIds)410].Value
			});
			return true;
		}
		Dynel val = (Dynel)(object)((character is Dynel) ? character : null);
		if (val == null)
		{
			return false;
		}
		if (!TryGetGridDestinationTerminal(destinationPlayfieldId, destinationInstance, out var destinationTerminal))
		{
			SendGridTerminalFeedback(character, "Grid terminal route is unavailable.");
			((IClient)client).Server.Info((IClient)(object)client, "Grid enter terminal destination missing char={0} target={1} sourcePf={2} destPf={3} destInstance={4:X8}", new object[5]
			{
				((IEntity)character).Identity,
				target,
				statelData.PlayfieldId,
				destinationPlayfieldId,
				(uint)destinationInstance
			});
			return true;
		}
		Quaternion val2 = new Quaternion((double)destinationTerminal.HeadingX, (double)destinationTerminal.HeadingY, (double)destinationTerminal.HeadingZ, (double)destinationTerminal.HeadingW);
		Quaternion.Normalize((IQuaternion)(object)val2);
		Vector3 val3 = new Vector3((double)destinationTerminal.X, (double)destinationTerminal.Y, (double)destinationTerminal.Z);
		Vector3 val4 = (Vector3)val2.RotateVector3((IVector3)(object)Vector3.AxisZ);
		val3.x += val4.x * 2.5;
		val3.z += val4.z * 2.5;
		character.StopMovement();
		((IStats)character).Stats[(StatIds)193].BaseValue = 0u;
		((IStats)character).Stats[(StatIds)192].BaseValue = 0u;
		GridZoneInDiagnostics.RecordGridEntry(character, statelData, destinationTerminal, new Coordinate(val3), "GridTeleportProxy2TerminalRoute", "playfields.dat Enter The Grid template 95350 TeleportProxy2 -> PF152; destination template 95351", destinationInstance);
		IPlayfield playfield = ((IInstancedEntity)character).Playfield;
		Coordinate val5 = new Coordinate(val3);
		Identity val6 = default(Identity);
		((Identity)(ref val6)).Type = (IdentityType)51101;
		((Identity)(ref val6)).Instance = 152;
		playfield.Teleport(val, val5, (IQuaternion)(object)val2, val6);
		((IClient)client).Server.Info((IClient)(object)client, "Grid enter terminal use handled char={0} target={1} sourcePf={2} destPf={3} destTerminal={4} dest=({5:F3},{6:F3},{7:F3}) evidence={8}", new object[9]
		{
			((IEntity)character).Identity,
			target,
			statelData.PlayfieldId,
			destinationPlayfieldId,
			destinationTerminal.Identity,
			val3.x,
			val3.y,
			val3.z,
			"playfields.dat Enter The Grid template 95350 TeleportProxy2 -> PF152; destination template 95351"
		});
		return true;
	}

	private StatelData GetStatelData(ICharacter character, Identity target)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || ((IInstancedEntity)character).Playfield == null)
		{
			return null;
		}
		Dictionary<int, PlayfieldData> pFData = PlayfieldLoader.PFData;
		Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		if (!pFData.TryGetValue(((Identity)(ref identity)).Instance, out var value))
		{
			return null;
		}
		return value.Statels.FirstOrDefault(delegate(StatelData x)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			Identity identity2 = x.Identity;
			int result;
			if (((Identity)(ref identity2)).Type == ((Identity)(ref target)).Type)
			{
				identity2 = x.Identity;
				result = ((((Identity)(ref identity2)).Instance == ((Identity)(ref target)).Instance) ? 1 : 0);
			}
			else
			{
				result = 0;
			}
			return (byte)result != 0;
		});
	}

	private bool TryGetCapturedGridTerminalRoute(ICharacter character, Identity target, StatelData statelData, out CapturedGridTerminalRoute route)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Invalid comparison between Unknown and I4
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		route = null;
		if (character == null || ((IInstancedEntity)character).Playfield == null)
		{
			return false;
		}
		if ((int)((Identity)(ref target)).Type != 51005 || statelData == null || statelData.TemplateId != 95350)
		{
			return false;
		}
		int playfieldId = statelData.PlayfieldId;
		Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		if (playfieldId == ((Identity)(ref identity)).Instance)
		{
			identity = statelData.Identity;
			if (((Identity)(ref identity)).Type == ((Identity)(ref target)).Type)
			{
				identity = statelData.Identity;
				if (((Identity)(ref identity)).Instance == ((Identity)(ref target)).Instance)
				{
					route = CapturedGridTerminalRoutes.FirstOrDefault(delegate(CapturedGridTerminalRoute x)
					{
						//IL_0011: Unknown result type (might be due to invalid IL or missing references)
						//IL_0016: Unknown result type (might be due to invalid IL or missing references)
						int sourcePlayfieldId = x.SourcePlayfieldId;
						Identity identity2 = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
						return sourcePlayfieldId == ((Identity)(ref identity2)).Instance && x.SourceTerminalInstance == ((Identity)(ref target)).Instance;
					});
					return route != null;
				}
			}
		}
		return false;
	}

	private bool IsGridEnterTerminal(ICharacter character, Identity target, StatelData statelData)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Invalid comparison between Unknown and I4
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || ((IInstancedEntity)character).Playfield == null)
		{
			return false;
		}
		if ((int)((Identity)(ref target)).Type != 51005 || statelData == null || statelData.TemplateId != 95350)
		{
			return false;
		}
		if (TryGetCapturedGridTerminalRoute(character, target, statelData, out var _))
		{
			return false;
		}
		int playfieldId = statelData.PlayfieldId;
		Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		int result;
		if (playfieldId == ((Identity)(ref identity)).Instance)
		{
			identity = statelData.Identity;
			if (((Identity)(ref identity)).Type == ((Identity)(ref target)).Type)
			{
				identity = statelData.Identity;
				result = ((((Identity)(ref identity)).Instance == ((Identity)(ref target)).Instance) ? 1 : 0);
				goto IL_00ae;
			}
		}
		result = 0;
		goto IL_00ae;
		IL_00ae:
		return (byte)result != 0;
	}

	private bool TryGetGridTeleportProxy2Destination(StatelData statelData, out Function teleportFunction, out int destinationPlayfieldId, out int destinationInstance)
	{
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		teleportFunction = null;
		destinationPlayfieldId = 0;
		destinationInstance = 0;
		if (statelData == null)
		{
			return false;
		}
		foreach (Event item in statelData.Events.Where((Event x) => (int)x.EventType == 0))
		{
			foreach (Function item2 in item.Functions.Where((Function x) => x.FunctionType == 53083))
			{
				if (item2.Arguments.Values.Count >= 3)
				{
					MessagePackObject val = item2.Arguments.Values[1];
					int num = ((MessagePackObject)(ref val)).AsInt32();
					if (num == 152)
					{
						val = item2.Arguments.Values[2];
						int num2 = ((MessagePackObject)(ref val)).AsInt32();
						teleportFunction = item2;
						destinationPlayfieldId = num;
						destinationInstance = -1073741824 | num | (num2 << 16);
						return true;
					}
				}
			}
		}
		return false;
	}

	private bool GridTeleportRequirementsPass(ICharacter character, Function teleportFunction)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Invalid comparison between Unknown and I4
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Invalid comparison between Unknown and I4
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Invalid comparison between Unknown and I4
		bool flag = true;
		for (int i = 0; i < teleportFunction.Requirements.Count; i++)
		{
			Requirement val = teleportFunction.Requirements[i];
			if (i == 0 && (int)val.ChildOperator == 3)
			{
				flag = false;
			}
			flag = (((int)val.ChildOperator != 3) ? (flag & val.CheckRequirement((IInstancedEntity)(object)character)) : (flag | val.CheckRequirement((IInstancedEntity)(object)character)));
			if (!flag && (int)val.ChildOperator != 3)
			{
				return false;
			}
		}
		return flag;
	}

	private void SendGridTerminalRequirementFeedback(ICharacter character, StatelData statelData, Function teleportFunction)
	{
		if (TryGetGreaterThanRequirement(teleportFunction, (StatIds)161, out var value) && ((IStats)character).Stats[(StatIds)161].Value <= value)
		{
			SendGridTerminalFeedback(character, GetGridTerminalSystemText(statelData, "Computer") ?? ("Your skill in Computer Literacy needs to be " + (value + 1).ToString(CultureInfo.InvariantCulture) + " or better to activate this terminal."));
		}
		else if (HasEqualToZeroRequirement(teleportFunction, (StatIds)410) && ((IStats)character).Stats[(StatIds)410].Value != 0)
		{
			SendGridTerminalFeedback(character, GetGridTerminalSystemText(statelData, "combat") ?? "This terminal can not be activated while you are in combat.");
		}
		else
		{
			SendGridTerminalFeedback(character, "Grid terminal requirements are not met.");
		}
	}

	private bool TryGetGreaterThanRequirement(Function function, StatIds statId, out int value)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Invalid comparison between I4 and Unknown
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Invalid comparison between Unknown and I4
		foreach (Requirement requirement in function.Requirements)
		{
			if (requirement.Statnumber == (int)statId && (int)requirement.Operator == 2)
			{
				value = requirement.Value;
				return true;
			}
		}
		value = 0;
		return false;
	}

	private bool HasEqualToZeroRequirement(Function function, StatIds statId)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		return function.Requirements.Any((Requirement x) => x.Statnumber == (int)statId && (int)x.Operator == 0 && x.Value == 0);
	}

	private string GetGridTerminalSystemText(StatelData statelData, string contains)
	{
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		if (statelData == null)
		{
			return null;
		}
		foreach (Event item in statelData.Events.Where((Event x) => (int)x.EventType == 0))
		{
			foreach (Function item2 in item.Functions.Where((Function x) => x.FunctionType == 53044))
			{
				if (item2.Arguments.Values.Count != 0)
				{
					MessagePackObject val = item2.Arguments.Values[0];
					string text = ((MessagePackObject)(ref val)).AsString();
					if (text.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0)
					{
						return text;
					}
				}
			}
		}
		return null;
	}

	private bool TryGetGridDestinationTerminal(int destinationPlayfieldId, int destinationInstance, out StatelData destinationTerminal)
	{
		destinationTerminal = null;
		if (!PlayfieldLoader.PFData.TryGetValue(destinationPlayfieldId, out var value))
		{
			return false;
		}
		destinationTerminal = value.Statels.FirstOrDefault(delegate(StatelData x)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Invalid comparison between Unknown and I4
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Invalid comparison between Unknown and I4
			Identity identity = x.Identity;
			if ((int)((Identity)(ref identity)).Type != 51005)
			{
				identity = x.Identity;
				if ((int)((Identity)(ref identity)).Type != 51016)
				{
					goto IL_004f;
				}
			}
			identity = x.Identity;
			if (((Identity)(ref identity)).Instance != destinationInstance)
			{
				goto IL_004f;
			}
			int result = ((x.TemplateId == 95351) ? 1 : 0);
			goto IL_0050;
			IL_0050:
			return (byte)result != 0;
			IL_004f:
			result = 0;
			goto IL_0050;
		});
		return destinationTerminal != null;
	}

	private void SendGridTerminalFeedback(ICharacter character, string text)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Expected O, but got Unknown
		IZoneClient client = ((IDynel)character).Controller.Client;
		FormatFeedbackMessage val = new FormatFeedbackMessage
		{
			Identity = ((IEntity)character).Identity,
			Unknown1 = 0,
			FormattedMessage = text,
			Unknown2 = 0
		};
		Identity identity = ((IEntity)character).Identity;
		client.SendCompressed((MessageBody)val, ((Identity)(ref identity)).Instance);
	}
}
