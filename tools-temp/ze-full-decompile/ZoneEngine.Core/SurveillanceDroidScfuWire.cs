using System;
using System.Net;
using AORebirth.Core.Entities;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core;

internal static class SurveillanceDroidScfuWire
{
	private const int ZoneServerSenderId = 854;

	private const int HeaderLength = 16;

	private const int HeaderReceiverOffset = 12;

	private const int HeaderSenderOffset = 8;

	private const int N3IdentityInstanceOffset = 24;

	private const int ScfuPlayfieldOffset = 34;

	private const int ScfuCoordOffset = 38;

	private const int ScfuHeadingOffset = 50;

	private static readonly byte[] ScfuCapturePacket = Hex("0000000A000101000000035600000000271B3A6B0000C35078E0FC8A003A0A2A4A53000FF02D455EF84A40A38520444D17E7800000003F145353800000003F50A6D9000005C8135375727665696C6C616E63652044726F69640010081201000000008900000000060045000003353E006E001F000000001C000000000000000000000000030100010001000100010000000300001400000FC463616D65726100000000000000000000000000000000000000000000000000000003351B000000000000000063616D65726120676C6F770000000000000000000000000000000000000000000003A8A6000000000000000063616D657261206C656E736500000000000000000000000000000000000000000003A8A80000000000000000000003F1000017A6000000000000000000000000000000010000000000000000000000020000000000000000000000030000000000000000000000040000000000000000000003F10000000000");

	public static bool IsSurveillanceDroid(Character character)
	{
		return character != null && (string.Equals(((Dynel)character).Name, "Surveillance Droid", StringComparison.OrdinalIgnoreCase) || ((Dynel)character).Stats[(StatIds)359].Value == 210238);
	}

	public static void SendToRecipient(ZoneClient recipientClient, Character droid)
	{
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		object obj;
		if (recipientClient == null)
		{
			obj = null;
		}
		else
		{
			IController controller = recipientClient.Controller;
			obj = ((controller != null) ? controller.Character : null);
		}
		if (obj != null && droid != null && ((Dynel)droid).Playfield != null)
		{
			Identity identity = ((IEntity)recipientClient.Controller.Character).Identity;
			byte[] array = BuildPacket(droid, ((Identity)(ref identity)).Instance);
			if (array != null)
			{
				ServerBase server = ((ClientBase)recipientClient).Server;
				object[] array2 = new object[3];
				identity = ((IEntity)recipientClient.Controller.Character).Identity;
				array2[0] = ((Identity)(ref identity)).Instance;
				array2[1] = ((PooledObject)droid).Identity;
				array2[2] = array.Length;
				server.Info((IClient)(object)recipientClient, "SurveillanceDroidWire SCFU recipient={0} droid={1} len={2}", array2);
				recipientClient.EnqueueOutboundCompressedBuffer(array);
			}
		}
	}

	internal static byte[] BuildPacket(Character droid, int recipientInstance)
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		if (droid == null || ((Dynel)droid).Playfield == null)
		{
			return null;
		}
		byte[] array = (byte[])ScfuCapturePacket.Clone();
		Identity identity = ((PooledObject)droid).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		identity = ((IEntity)((Dynel)droid).Playfield).Identity;
		int instance2 = ((Identity)(ref identity)).Instance;
		Coordinate val = ((Dynel)droid).Coordinates();
		Quaternion heading = ((Dynel)droid).Heading;
		PatchHeader(array, recipientInstance);
		WriteInt32BigEndian(array, 24, instance);
		WriteInt32BigEndian(array, 34, instance2);
		WriteFloat(array, 38, val.x);
		WriteFloat(array, 42, val.y);
		WriteFloat(array, 46, val.z);
		WriteFloat(array, 50, heading.xf);
		WriteFloat(array, 54, heading.yf);
		WriteFloat(array, 58, heading.zf);
		WriteFloat(array, 62, heading.wf);
		return array;
	}

	private static void PatchHeader(byte[] packet, int recipientInstance)
	{
		WriteInt32BigEndian(packet, 8, 854);
		WriteInt32BigEndian(packet, 12, recipientInstance);
		ushort num = (ushort)packet.Length;
		packet[6] = (byte)(num >> 8);
		packet[7] = (byte)num;
	}

	private static void WriteInt32BigEndian(byte[] buffer, int offset, int value)
	{
		byte[] bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value));
		Buffer.BlockCopy(bytes, 0, buffer, offset, 4);
	}

	private static void WriteFloat(byte[] buffer, int offset, float value)
	{
		byte[] bytes = BitConverter.GetBytes(value);
		if (BitConverter.IsLittleEndian)
		{
			Array.Reverse(bytes);
		}
		Buffer.BlockCopy(bytes, 0, buffer, offset, 4);
	}

	private static byte[] Hex(string hex)
	{
		byte[] array = new byte[hex.Length / 2];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
		}
		return array;
	}
}
