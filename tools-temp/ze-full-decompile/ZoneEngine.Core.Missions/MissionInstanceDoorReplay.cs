using System;
using System.Globalization;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Core.Playfields;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;

namespace ZoneEngine.Core.Missions;

internal static class MissionInstanceDoorReplay
{
	public static void SendForCharacter(IZoneClient client, ICharacter character)
	{
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		if (client == null || character == null || ((IInstancedEntity)character).Playfield == null)
		{
			return;
		}
		Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		if (!MissionInstanceService.IsMissionInstancePlayfield(((Identity)(ref identity)).Instance) || !(client is ZoneClient zoneClient))
		{
			return;
		}
		identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		int playfieldId = MissionInstanceShapeCatalog.PickShape(instance, null)?.CapturedPlayfieldId ?? instance;
		int num = 0;
		try
		{
			string[] array = MissionInstanceDynelCapture.GetDoors(playfieldId);
			string[] hexPackets = MissionInstanceDynelCapture.GetChests(playfieldId);
			if (array == null || array.Length == 0)
			{
				array = MissionInstanceDoorCapture.CapturedDoorPacketHex;
				hexPackets = null;
			}
			num += SendPackets(zoneClient, character, array, 1982512161);
			num += SendPackets(zoneClient, character, hexPackets, 1982512161);
		}
		catch (Exception ex)
		{
			LogUtil.ErrorException(ex);
		}
		object[] array2 = new object[3];
		identity = ((IEntity)character).Identity;
		array2[0] = ((Identity)(ref identity)).Instance;
		array2[1] = instance;
		array2[2] = num;
		MissionDiagnostics.Log("DOOR-CHEST-REPLAY char={0} pf={1} sent={2}", array2);
	}

	private static int SendPackets(ZoneClient zoneClient, ICharacter character, string[] hexPackets, int capturedCharacterInstance)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		if (hexPackets == null || hexPackets.Length == 0)
		{
			return 0;
		}
		int num = 0;
		foreach (string text in hexPackets)
		{
			if (!string.IsNullOrEmpty(text))
			{
				byte[] array = HexToBytes(text);
				Identity identity = ((IEntity)character).Identity;
				ReplaceInstance(array, capturedCharacterInstance, ((Identity)(ref identity)).Instance);
				identity = ((IEntity)character).Identity;
				ReplaceInstance(array, 1985637684, ((Identity)(ref identity)).Instance);
				identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
				RetargetPlayfieldIds(array, ((Identity)(ref identity)).Instance);
				zoneClient.SendCompressed(array);
				num++;
			}
		}
		return num;
	}

	private static void RetargetPlayfieldIds(byte[] packet, int livePlayfieldId)
	{
		int[] shapePlayfieldIds = MissionInstanceDynelCapture.ShapePlayfieldIds;
		for (int i = 0; i < shapePlayfieldIds.Length; i++)
		{
			ReplaceInstance(packet, shapePlayfieldIds[i], livePlayfieldId);
		}
		ReplaceInstance(packet, 1413198, livePlayfieldId);
	}

	private static void ReplaceInstance(byte[] packet, int from, int to)
	{
		if (packet == null || from == to)
		{
			return;
		}
		byte b = (byte)(from >> 24);
		byte b2 = (byte)(from >> 16);
		byte b3 = (byte)(from >> 8);
		byte b4 = (byte)from;
		for (int i = 0; i + 4 <= packet.Length; i++)
		{
			if (packet[i] == b && packet[i + 1] == b2 && packet[i + 2] == b3 && packet[i + 3] == b4)
			{
				packet[i] = (byte)(to >> 24);
				packet[i + 1] = (byte)(to >> 16);
				packet[i + 2] = (byte)(to >> 8);
				packet[i + 3] = (byte)to;
			}
		}
	}

	private static byte[] HexToBytes(string hex)
	{
		byte[] array = new byte[hex.Length / 2];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber);
		}
		return array;
	}
}
