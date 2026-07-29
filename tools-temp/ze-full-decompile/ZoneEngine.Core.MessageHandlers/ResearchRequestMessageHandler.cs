using System.Collections.Generic;
using AORebirth.Core.Components;
using AORebirth.Core.Network;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class ResearchRequestMessageHandler : BaseMessageHandler<ResearchRequestMessage, ResearchRequestMessageHandler>
{
	protected override void Read(ResearchRequestMessage message, IZoneClient client)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Expected O, but got Unknown
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Expected O, but got Unknown
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Expected O, but got Unknown
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Expected O, but got Unknown
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Expected O, but got Unknown
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Expected O, but got Unknown
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Expected O, but got Unknown
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Expected O, but got Unknown
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Expected O, but got Unknown
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Expected O, but got Unknown
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Expected O, but got Unknown
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Expected O, but got Unknown
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Expected O, but got Unknown
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_0156: Expected O, but got Unknown
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_016e: Expected O, but got Unknown
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Expected O, but got Unknown
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Expected O, but got Unknown
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b6: Expected O, but got Unknown
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ce: Expected O, but got Unknown
		//IL_01d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e6: Expected O, but got Unknown
		//IL_01e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fe: Expected O, but got Unknown
		//IL_0200: Unknown result type (might be due to invalid IL or missing references)
		//IL_0205: Unknown result type (might be due to invalid IL or missing references)
		//IL_0216: Expected O, but got Unknown
		//IL_0218: Unknown result type (might be due to invalid IL or missing references)
		//IL_021d: Unknown result type (might be due to invalid IL or missing references)
		//IL_022e: Expected O, but got Unknown
		//IL_0230: Unknown result type (might be due to invalid IL or missing references)
		//IL_0235: Unknown result type (might be due to invalid IL or missing references)
		//IL_0246: Expected O, but got Unknown
		//IL_0248: Unknown result type (might be due to invalid IL or missing references)
		//IL_024d: Unknown result type (might be due to invalid IL or missing references)
		//IL_025e: Expected O, but got Unknown
		//IL_0260: Unknown result type (might be due to invalid IL or missing references)
		//IL_0265: Unknown result type (might be due to invalid IL or missing references)
		//IL_0276: Expected O, but got Unknown
		//IL_0278: Unknown result type (might be due to invalid IL or missing references)
		//IL_027d: Unknown result type (might be due to invalid IL or missing references)
		//IL_028e: Expected O, but got Unknown
		//IL_0290: Unknown result type (might be due to invalid IL or missing references)
		//IL_0295: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a6: Expected O, but got Unknown
		//IL_02a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_02be: Expected O, but got Unknown
		//IL_02c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d6: Expected O, but got Unknown
		//IL_02d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ee: Expected O, but got Unknown
		//IL_02f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0306: Expected O, but got Unknown
		//IL_0308: Unknown result type (might be due to invalid IL or missing references)
		//IL_030d: Unknown result type (might be due to invalid IL or missing references)
		//IL_031e: Expected O, but got Unknown
		//IL_0320: Unknown result type (might be due to invalid IL or missing references)
		//IL_0325: Unknown result type (might be due to invalid IL or missing references)
		//IL_0336: Expected O, but got Unknown
		//IL_0338: Unknown result type (might be due to invalid IL or missing references)
		//IL_033d: Unknown result type (might be due to invalid IL or missing references)
		//IL_034e: Expected O, but got Unknown
		//IL_0350: Unknown result type (might be due to invalid IL or missing references)
		//IL_0355: Unknown result type (might be due to invalid IL or missing references)
		//IL_0366: Expected O, but got Unknown
		//IL_0368: Unknown result type (might be due to invalid IL or missing references)
		//IL_036d: Unknown result type (might be due to invalid IL or missing references)
		//IL_037e: Expected O, but got Unknown
		//IL_0380: Unknown result type (might be due to invalid IL or missing references)
		//IL_0385: Unknown result type (might be due to invalid IL or missing references)
		//IL_0396: Expected O, but got Unknown
		//IL_0398: Unknown result type (might be due to invalid IL or missing references)
		//IL_039d: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ae: Expected O, but got Unknown
		//IL_03b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c6: Expected O, but got Unknown
		//IL_03c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_03cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_03de: Expected O, but got Unknown
		//IL_03e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f6: Expected O, but got Unknown
		//IL_03f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_040e: Expected O, but got Unknown
		//IL_0410: Unknown result type (might be due to invalid IL or missing references)
		//IL_0415: Unknown result type (might be due to invalid IL or missing references)
		//IL_0426: Expected O, but got Unknown
		//IL_0428: Unknown result type (might be due to invalid IL or missing references)
		//IL_042d: Unknown result type (might be due to invalid IL or missing references)
		//IL_043e: Expected O, but got Unknown
		//IL_0440: Unknown result type (might be due to invalid IL or missing references)
		//IL_0445: Unknown result type (might be due to invalid IL or missing references)
		//IL_0456: Expected O, but got Unknown
		//IL_0458: Unknown result type (might be due to invalid IL or missing references)
		//IL_045d: Unknown result type (might be due to invalid IL or missing references)
		//IL_046e: Expected O, but got Unknown
		//IL_0470: Unknown result type (might be due to invalid IL or missing references)
		//IL_0475: Unknown result type (might be due to invalid IL or missing references)
		//IL_0486: Expected O, but got Unknown
		//IL_0488: Unknown result type (might be due to invalid IL or missing references)
		//IL_048d: Unknown result type (might be due to invalid IL or missing references)
		//IL_049e: Expected O, but got Unknown
		//IL_04a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b6: Expected O, but got Unknown
		//IL_04b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_04bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ce: Expected O, but got Unknown
		//IL_04d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_04e6: Expected O, but got Unknown
		//IL_04e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_04fe: Expected O, but got Unknown
		List<ResearchUpdateEntry> list = new List<ResearchUpdateEntry>();
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3000
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3010
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3011
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3012
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3020
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3021
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3022
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3030
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3040
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3041
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3050
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3051
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3052
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3060
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3070
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3071
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3072
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3080
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3081
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3082
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3090
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3100
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3101
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3102
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3110
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3111
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3112
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3120
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3130
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3131
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3132
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3140
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3141
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3142
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3200
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3201
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3202
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3203
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3204
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3205
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3206
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3207
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3208
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3209
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3210
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3211
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3212
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3213
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3214
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3215
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3216
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3217
		});
		list.Add(new ResearchUpdateEntry
		{
			ResearchId = 3218
		});
		BaseMessageHandler<ResearchUpdateMessage, ResearchUpdateMessageHandler>.Default.Send(client.Controller.Character, list.ToArray());
	}
}
