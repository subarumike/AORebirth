using System;
using System.Globalization;
using System.IO;
using System.Threading;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using SmokeLounge.AOtomation.Messaging.Serialization;

namespace ZoneEngine.Core.Missions;

internal static class MissionRollService
{
	private const int MissionTerminalIdentityTypeRaw = 56001;

	private const int MissionIdentityTypeRaw = 56003;

	private static readonly object InitLock = new object();

	private static SerializerResolver serializerResolver;

	private static ISerializer questAlternativeSerializer;

	private static byte[] templateBody;

	private static byte[][] libraryBodies;

	private static int questInstanceSeed = Math.Max(1431736320, (int)(DateTime.UtcNow.Ticks & 0x3FFFFFFF));

	internal static byte[] TemplateBody
	{
		get
		{
			EnsureInitialized();
			return templateBody;
		}
	}

	public static QuestAlternativeMessage BuildRollResponse(QuestAlternativeMessage request, Identity character, int characterLevel, int terminalPlayfieldId = 0)
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0202: Unknown result type (might be due to invalid IL or missing references)
		//IL_0204: Unknown result type (might be due to invalid IL or missing references)
		//IL_0206: Unknown result type (might be due to invalid IL or missing references)
		//IL_0215: Unknown result type (might be due to invalid IL or missing references)
		//IL_021a: Unknown result type (might be due to invalid IL or missing references)
		//IL_021c: Unknown result type (might be due to invalid IL or missing references)
		//IL_021e: Unknown result type (might be due to invalid IL or missing references)
		//IL_022d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0232: Unknown result type (might be due to invalid IL or missing references)
		//IL_0234: Unknown result type (might be due to invalid IL or missing references)
		//IL_0236: Unknown result type (might be due to invalid IL or missing references)
		//IL_031b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0320: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ad: Unknown result type (might be due to invalid IL or missing references)
		EnsureInitialized();
		int missionQuality = MissionLevelTable.GetMissionQuality(characterLevel, request.LevelSlider);
		Random rng = new Random((Environment.TickCount * 397) ^ ((Identity)(ref character)).Instance ^ missionQuality);
		MissionLocationSide missionLocationSide = MissionLocationPool.ResolveTerminalSide(terminalPlayfieldId);
		QuestAlternativeMessage val = DecodeRollBody(PickLibraryBody(rng));
		Identity missionTerminalIdentity = val.MissionTerminalIdentity;
		Identity missionTerminalIdentity2 = request.MissionTerminalIdentity;
		((N3Message)val).Identity = character;
		val.MissionTerminalIdentity = missionTerminalIdentity2;
		val.VersionId = request.VersionId;
		val.Unknown5 = request.Unknown5;
		val.LevelSlider = request.LevelSlider;
		val.GoodBadSlider = request.GoodBadSlider;
		val.OrderChaosSlider = request.OrderChaosSlider;
		val.OpenHiddenSlider = request.OpenHiddenSlider;
		val.PhysicalMysticalSlider = request.PhysicalMysticalSlider;
		val.HeadOnStealthSlider = request.HeadOnStealthSlider;
		val.MoneyExperienceSlider = request.MoneyExperienceSlider;
		val.Unknown4 = Environment.TickCount;
		QuestInfo[] questInfos = val.QuestInfos;
		if (questInfos == null || questInfos.Length == 0)
		{
			MissionDiagnostics.Log("ROLL-EMPTY-LIBRARY fallback to single template");
			val = DecodeTemplate();
			questInfos = val.QuestInfos;
			missionTerminalIdentity = val.MissionTerminalIdentity;
			((N3Message)val).Identity = character;
			val.MissionTerminalIdentity = missionTerminalIdentity2;
		}
		int[] array = new int[questInfos.Length];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = -1;
		}
		MissionDiagnostics.Log("ROLL-SIDE terminalPf={0} side={1}", terminalPlayfieldId, missionLocationSide);
		int[] allowedSpotIndexes = BuildAllowedSpotIndexes(missionLocationSide);
		for (int j = 0; j < questInfos.Length; j++)
		{
			QuestInfo val2 = questInfos[j];
			if (val2 != null)
			{
				MissionRollType type = MissionTypeCatalog.TypeFromIcon(val2.MissionIconId);
				ApplyPoolLocation(val2, rng, array, j, allowedSpotIndexes);
				Identity questIdentity = default(Identity);
				((Identity)(ref questIdentity)).Type = (IdentityType)56003;
				((Identity)(ref questIdentity)).Instance = NextQuestInstance();
				val2.QuestIdentity = questIdentity;
				val2.Unknown5 = RetargetTerminal(val2.Unknown5, missionTerminalIdentity, missionTerminalIdentity2);
				val2.Unknown14 = RetargetTerminal(val2.Unknown14, missionTerminalIdentity, missionTerminalIdentity2);
				val2.Unknown23 = RetargetTerminal(val2.Unknown23, missionTerminalIdentity, missionTerminalIdentity2);
				val2.Quality = missionQuality;
				ApplyMoneyExperienceSlider(val2, request.MoneyExperienceSlider, missionQuality);
				ApplyMaliReward(val2, missionQuality, rng, type);
				int num = 0;
				float num2 = 0f;
				float num3 = 0f;
				if (val2.QuestActions != null && val2.QuestActions.Length != 0 && val2.QuestActions[0] != null)
				{
					questIdentity = val2.QuestActions[0].Playfield;
					num = ((Identity)(ref questIdentity)).Instance;
					num2 = val2.QuestActions[0].X;
					num3 = val2.QuestActions[0].Z;
				}
				object[] obj = new object[10]
				{
					j,
					MissionTypeCatalog.TypeName(type),
					val2.MissionIconId,
					val2.Quality,
					null,
					null,
					null,
					null,
					null,
					null
				};
				questIdentity = val2.QuestIdentity;
				obj[4] = ((Identity)(ref questIdentity)).Instance;
				obj[5] = num;
				obj[6] = num2;
				obj[7] = num3;
				obj[8] = ((val2.ItemRewards != null && val2.ItemRewards.Length != 0) ? val2.ItemRewards[0].LowId : 0);
				obj[9] = ((val2.ItemRewards != null && val2.ItemRewards.Length != 0) ? val2.ItemRewards[0].Quality : 0);
				MissionDiagnostics.Log("ROLL-OFFER slot={0} type={1} icon={2} ql={3} quest={4:X8} pf={5} xz=({6:F0},{7:F0}) rewardLow={8} rewardQl={9}", obj);
			}
		}
		val.QuestInfos = questInfos;
		RestoreStringTerminators(val);
		return val;
	}

	internal static QuestAlternativeMessage DecodeTemplate()
	{
		EnsureInitialized();
		QuestAlternativeMessage val = Deserialize(templateBody);
		RestoreStringTerminators(val);
		return val;
	}

	internal static byte[] SerializeBody(QuestAlternativeMessage message)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected O, but got Unknown
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Expected O, but got Unknown
		EnsureInitialized();
		using MemoryStream memoryStream = new MemoryStream();
		StreamWriter val = new StreamWriter((Stream)memoryStream);
		try
		{
			questAlternativeSerializer.Serialize(val, new SerializationContext(serializerResolver), (object)message, (PropertyMetaData)null);
			return memoryStream.ToArray();
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private static QuestInfo CloneArchetype(QuestInfo[] archetypes, int index)
	{
		QuestInfo[] questInfos = DecodeTemplate().QuestInfos;
		int num = index;
		if (questInfos == null || questInfos.Length == 0)
		{
			return (archetypes != null && archetypes.Length != 0) ? archetypes[0] : null;
		}
		if (num < 0)
		{
			num = 0;
		}
		if (num >= questInfos.Length)
		{
			num = questInfos.Length - 1;
		}
		return questInfos[num];
	}

	private static byte[] PickLibraryBody(Random rng)
	{
		if (libraryBodies == null || libraryBodies.Length == 0)
		{
			return templateBody;
		}
		return libraryBodies[rng.Next(libraryBodies.Length)];
	}

	private static QuestAlternativeMessage DecodeRollBody(byte[] body)
	{
		EnsureInitialized();
		QuestAlternativeMessage val = Deserialize(body);
		RestoreStringTerminators(val);
		return val;
	}

	private static void ApplyPoolLocation(QuestInfo offer, Random rng, int[] usedSpotIndexes, int slot, int[] allowedSpotIndexes)
	{
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		if (offer != null && offer.QuestActions != null && offer.QuestActions.Length != 0 && MissionLocationPool.Spots != null && MissionLocationPool.Spots.Length != 0)
		{
			QuestActionList val = offer.QuestActions[0];
			if (val != null)
			{
				int num = (usedSpotIndexes[slot] = PickDistinctSpotIndex(rng, usedSpotIndexes, slot, allowedSpotIndexes));
				MissionLocationPool.Spot spot = MissionLocationPool.Spots[num];
				Identity playfield = default(Identity);
				((Identity)(ref playfield)).Type = (IdentityType)40016;
				((Identity)(ref playfield)).Instance = spot.Playfield;
				val.Playfield = playfield;
				val.Unknown18 = spot.EntranceLow;
				val.Unknown19 = spot.EntranceHigh;
				val.X = spot.X;
				val.Y = spot.Y;
				val.Z = spot.Z;
			}
		}
	}

	private static int PickDistinctSpotIndex(Random rng, int[] usedSpotIndexes, int slot, int[] allowedSpotIndexes)
	{
		int num = MissionLocationPool.Spots.Length;
		int[] array = allowedSpotIndexes;
		if (array == null || array.Length == 0)
		{
			array = new int[num];
			for (int i = 0; i < num; i++)
			{
				array[i] = i;
			}
		}
		for (int j = 0; j < 32; j++)
		{
			int num2 = array[rng.Next(array.Length)];
			bool flag = false;
			int playfield = MissionLocationPool.Spots[num2].Playfield;
			for (int k = 0; k < slot; k++)
			{
				if (usedSpotIndexes[k] >= 0 && (usedSpotIndexes[k] == num2 || MissionLocationPool.Spots[usedSpotIndexes[k]].Playfield == playfield))
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				return num2;
			}
		}
		return array[rng.Next(array.Length)];
	}

	private static int[] BuildAllowedSpotIndexes(MissionLocationSide terminalSide)
	{
		int num = MissionLocationPool.Spots.Length;
		if (terminalSide == MissionLocationSide.Neutral)
		{
			int[] array = new int[num];
			for (int i = 0; i < num; i++)
			{
				array[i] = i;
			}
			return array;
		}
		int num2 = 0;
		for (int j = 0; j < num; j++)
		{
			if (MissionLocationPool.IsSpotAllowedForTerminal(MissionLocationPool.Spots[j].Playfield, terminalSide))
			{
				num2++;
			}
		}
		if (num2 == 0)
		{
			int[] array2 = new int[num];
			for (int k = 0; k < num; k++)
			{
				array2[k] = k;
			}
			return array2;
		}
		int[] array3 = new int[num2];
		int num3 = 0;
		for (int l = 0; l < num; l++)
		{
			if (MissionLocationPool.IsSpotAllowedForTerminal(MissionLocationPool.Spots[l].Playfield, terminalSide))
			{
				array3[num3++] = l;
			}
		}
		return array3;
	}

	private static void ApplyMaliReward(QuestInfo offer, int missionQuality, Random rng, MissionRollType type)
	{
		if (!MissionRewardCatalog.TryPickReward(missionQuality, rng, out var reward, out var itemName, out var isNano))
		{
			MissionDiagnostics.Log("ROLL-REWARD-MISS ql={0} type={1} catalogItems={2} err={3}", missionQuality, MissionTypeCatalog.TypeName(type), MissionRewardCatalog.ItemCount, MissionRewardCatalog.LastLoadError ?? string.Empty);
			return;
		}
		if (offer.ItemRewards != null && offer.ItemRewards.Length != 0)
		{
			offer.ItemRewards[0].LowId = reward.LowId;
			offer.ItemRewards[0].HighId = reward.HighId;
			offer.ItemRewards[0].Quality = reward.Quality;
		}
		else
		{
			offer.ItemRewards = (QuestItemShort[])(object)new QuestItemShort[1] { reward };
		}
		MissionDiagnostics.Log("ROLL-REWARD ql={0} type={1} nano={2} low={3} high={4} rewardQl={5} name={6}", missionQuality, MissionTypeCatalog.TypeName(type), isNano, reward.LowId, reward.HighId, reward.Quality, itemName ?? string.Empty);
	}

	private static void ApplyMoneyExperienceSlider(QuestInfo offer, byte moneyExperienceSlider, int missionQuality)
	{
		int num;
		if (moneyExperienceSlider <= 100)
		{
			num = moneyExperienceSlider;
		}
		else
		{
			num = 50 + (sbyte)moneyExperienceSlider / 2;
			if (num < 0)
			{
				num = 0;
			}
			if (num > 100)
			{
				num = 100;
			}
		}
		int num2 = ((offer.CashReward > 0) ? offer.CashReward : Math.Max(100, missionQuality * 50));
		int num3 = ((offer.ExperienceReward > 0) ? offer.ExperienceReward : Math.Max(100, missionQuality * 200));
		offer.CashReward = Math.Max(1, num2 * (150 - num) / 100);
		offer.ExperienceReward = Math.Max(1, num3 * (50 + num) / 100);
	}

	private static int NextQuestInstance()
	{
		int num = Interlocked.Increment(ref questInstanceSeed) & 0x7FFFFFFF;
		return (num == 0) ? NextQuestInstance() : num;
	}

	private static void RestoreStringTerminators(QuestAlternativeMessage message)
	{
		if (message.QuestInfos == null)
		{
			return;
		}
		QuestInfo[] questInfos = message.QuestInfos;
		foreach (QuestInfo val in questInfos)
		{
			if (val != null && val.Info != null && !val.Info.EndsWith("\0", StringComparison.Ordinal))
			{
				val.Info += "\0";
			}
		}
	}

	private static Identity RetargetTerminal(Identity value, Identity from, Identity to)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		if ((int)((Identity)(ref value)).Type == 56001 && ((Identity)(ref value)).Instance == ((Identity)(ref from)).Instance)
		{
			((Identity)(ref value)).Type = ((Identity)(ref to)).Type;
			((Identity)(ref value)).Instance = ((Identity)(ref to)).Instance;
		}
		return value;
	}

	private static void EnsureInitialized()
	{
		if (questAlternativeSerializer != null)
		{
			return;
		}
		lock (InitLock)
		{
			if (questAlternativeSerializer == null)
			{
				SerializerResolverBuilder<MessageBody> val = new SerializerResolverBuilder<MessageBody>();
				SerializerResolver val2 = ((SerializerResolverBuilder)val).Build();
				ISerializer serializer = val2.GetSerializer(typeof(QuestAlternativeMessage));
				byte[] array = HexToBytes("1E4C000A000110E600000DAD765A6F8A5C4366090000C350765A6F8A0004060101FFFF019C65F31253010000DAC1C000028F050000DAC35556839A0000000F0000000000000000000000005468616E6B20796F7520666F722063686F6F73696E672074686973202E2E2E00000001745468616E6B20796F7520666F722063686F6F73696E672074686973207465726D696E616C2E204D79207375636365737320646570656E6473206F6E20796F757220737563636573733A20576520726570726573656E742074686520536F63696F2D416E7468726F706F6C6F676963616C20536F63696574792C20616E64206E65656420736F6D652068656C702E202052756D6F72732073617920746861742046617573746F204B6570706C65722069732068656C70696E67206D7574616E7473206275696C64696E67206120686976652E2020576520646F206E6F74206B6E6F772077686572652068652F73686520697320617420746865206D6F6D656E742C20706C6561736520747261636B2068696D2F68657220646F776E2C20616E642077697468207468652075746D6F737420636172652C20617070726F6163682074686973206368617261637465722C206F62736572766520697420616E642074656C6C20757320776861742068617070656E7321000000DAC1C000028F000000060001855900000000000F98F8000003F1000003F1000007E200018C2C00018C2D000000AF000000000000000000000000000000004D535245000000AF00000000000000000000000000000000000000000000000000002C470000000000000B40000007E20000001000000000000000000000000000000000000111D3534C3136000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000006A5C808600000000000000000000000000009C50000001F90000898E0000AE16449FDA3D423B5B8F457990DE000003F100000000000000000000000000000006000003F10000000000000465000000000000000000000000000000000000000000000001000003F1010000DAC35556839B0000000F0000000000000000000000004D697373696F6E2041737369676E6D656E7420373432392D3332332D2E2E2E000000014B4D697373696F6E2041737369676E6D656E7420373432392D3332332D343A20576520726570726573656E742074686520536F63696F2D416E7468726F706F6C6F676963616C20536F63696574792C20616E64206E65656420736F6D652068656C702E202052756D6F72732073617920746861742053746566616E204D657373616D6F72652069732068656C70696E67206D7574616E7473206275696C64696E67206120686976652E2020576520646F206E6F74206B6E6F772077686572652068652F73686520697320617420746865206D6F6D656E742C20706C6561736520747261636B2068696D2F68657220646F776E2C20616E642077697468207468652075746D6F737420636172652C20617070726F6163682074686973206368617261637465722C206F62736572766520697420616E642074656C6C20757320776861742068617070656E7321000000DAC1C000028F0000000600016A9000000000000F6028000003F1000003F1000007E20000B13D0000B13D000000AF000000000000000000000000000000004D535245000000AF00000000000000000000000000000000000000000000000000002C470000000000000B40000007E20000001000000000000000000000000000000000000111D341563036000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000006A5C808600000000000000000000000000009C50000001F90000898D0000AE1543EFD99A419666794516DF48000003F100000000000000000000000000000006000003F10000000000000466000000000000000000000000000000000000000000000001000003F1020000DAC35556839C0000000F00000000000000000000000057656C6C2E204E6F74206D75636820746F2063686F6F73652066726F2E2E2E000000016457656C6C2E204E6F74206D75636820746F2063686F6F73652066726F6D207269676874206E6F772E205374696C6C2C2074686973206D6967687420626520736F6D657468696E6720666F7220796F753A204F6E652045796520496D706C616E743A2056656869636C65204169722C205368696E7920686173206265656E2073746F6C656E2066726F6D206F7572206C61627320696E2050657270657475616C2057617374656C616E64732E202054686520746869657665732068617665206E6F74206265656E2061626C6520746F206D6F7665206974206661722C20616E64207765206861766520747261636B6564206974732070726F677265737320746F2074686520313037362E322C20313233342E3720696E204D616E7469732048756E74696E672047726F756E642E2020506C6561736520676F20746865726520616E6420676574206974206261636B2068657265206265666F726520343820686F7572732E000000DAC1C000028F000000060001B5B600000000000E144B000003F1000003F1000007E200007B1C00007B1C00000001000000000000000000000000000000004D535245000000AF00000000000000000000000000000000000000000000000000002C410000000000000B40000007E200000008000111D3465557520000DAC1C000028F0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000006A5C808600000000000000000000000000009C500000023A0000B1AE0000B13444868733420E9E10449A5600000003F100000000000000000000000000000006000003F100000000000004AC000000000000000000000000000000000000000000000001000003F1030000DAC35556839D0000000F0000000000000000000000004D697373696F6E2041737369676E6D656E74204D58494C3A205765202E2E2E00000002684D697373696F6E2041737369676E6D656E74204D58494C3A205765206E65656420796F75722068656C702E2054686520526164617220446973706C617920666F756E64206174204D6F727420696E207468652061726561206F6620456172736E6573742057617374656C616E64732C20617420323738302E372C203533342E382C20686173206265656E2064697361626C6564206279206F70706F73696E672074726F6F70732E2E2E206D7574616E7420726966662D72616666206F722077686174657665722E2E2E2049206E656564206E6F74207374726573732074686520746163746963616C20696D706F7274616E6365206F6620526164617220446973706C6179732C20646F20493F20416E797761792C20616674657220612074686F726F756768207365617263682C207765206861766520666F756E64207468617420746865205375622D41746F6D6963204D656D6F72792053746F7261676520636F6D706F6E656E74206E65656473207265706C6163656D656E7420616674657220796F75206861766520736563757265642074686520617265612E20596F7572206D697373696F6E206F626A65637469766520776F756C64207468656E20626520746F20696E7374616C6C20746865205375622D41746F6D6963204D656D6F72792053746F7261676520636F72726563746C7920696E2074686520526164617220446973706C61792C20616E6420676574206F75742077697468696E20343820686F7572732E205265776172643A203131303436323920585020616E6420363236383920637265646974732E20476F6F64206C75636B2C204D61727469616C204172746973742E000000DAC1C000028F000000060000F4E1000000000010DAF5000003F1000003F1000007E20001B4A90001B4AA000000AF000000000000000000000000000000004D535245000000AF00000000000000000000000000000000000000000000000000002C4E0000000000000B40000007E200000008000111D353554D45000111D3524144490000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000006A5C808600000000000000000000000000009C50000002300000A3F70000AFE6452DCAFF419C36C54405B3BC000003F100000000000000000000000000000006000003F10000000000000403000000000000000000000000000000000000000000000001000003F1060000DAC35556839E0000000F00000000000000000000000048692C204920726570726573656E742061207665727920696E666C752E2E2E000000030348692C204920726570726573656E742061207665727920696E666C75656E7469616C20617274206469737472696275746F72207769746820636F6E6E656374696F6E7320696E206869676820706C616365732E20536F6D652074696D652061676F2C207765206D616465206120736572696573206F662072656D616B6573206F66207468652041727420436F6E7461696E657220666F722074686520527562692D4B61206D61726B65742E205361646C792C20736F6D657468696E672077656E742077726F6E67207769746820746865206C6F676973746963732C20616E642074776963652074686520616C6C6F77656420616D6F756E74207761732070726F64756365642E205468697320726573756C74656420696E20612064726F7020696E206D61726B65742076616C756520666F7220616C6C20636F706965732C20616E64206F757220636C69656E7473207765726520766572792075707365742E2057652061726520747279696E672076657279206861726420746F2074616B65206974206F7574206F6620746865206D61726B65742C206275742074686973206861732070726F76656E20746F206265207665727920646966666963756C742E20416E79686F772C2077652068617665206174206C61737420666F756E64206120636F7079206F66207468652041727420436F6E7461696E657220696E204D6F72742E20496620796F7520676F2074686572652C20616E6420616374697661746520796F7572206D697373696F6E20626561636F6E2C2069742073686F756C6420706F696E7420796F7520746F776172642061206275696C742D696E204750532064657669636520696E20746865206F626A6563742E204974206D69676874206265206C6F636174656420756E64657267726F756E642C206974206D69676874206E6F742E20576520646F206E6F74206B6E6F772E2054696D696E67206973206F6620677265617420696D706F7274616E6365202D20796F75206861766520343820686F7572732E20476F6F64206C75636B2C204D61727469616C204172746973742E000000DAC1C000028F0000000600014E33000000000010736B000003F1000003F1000003F10000000000000000000000004D535245000000AF00000000000000000000000000000000000000000000000000002C490000000000000B40000007E20000000F000111D34152434F00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000006A5C808600000000000000000000000000009C50000002300000A3F60000AFE64456E889422731A745615E98000003F100000000000000000000000000000006000003F10000000000000416000000000000000000000000000000000000000000000001000003F109");
				byte[] array2 = new byte[array.Length - 16];
				Array.Copy(array, 16, array2, 0, array2.Length);
				serializerResolver = val2;
				questAlternativeSerializer = serializer;
				templateBody = array2;
				string[] capturedRollBodiesHex = MissionRollCaptureLibrary.CapturedRollBodiesHex;
				byte[][] array3 = new byte[capturedRollBodiesHex.Length][];
				for (int i = 0; i < capturedRollBodiesHex.Length; i++)
				{
					array3[i] = HexToBytes(capturedRollBodiesHex[i]);
				}
				libraryBodies = array3;
				MissionDiagnostics.Log("ROLL-LIBRARY loaded={0} fallbackTemplateBytes={1}", libraryBodies.Length, templateBody.Length);
			}
		}
	}

	private static QuestAlternativeMessage Deserialize(byte[] body)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Expected O, but got Unknown
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Expected O, but got Unknown
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Expected O, but got Unknown
		using MemoryStream memoryStream = new MemoryStream(body);
		StreamReader val = new StreamReader((Stream)memoryStream);
		try
		{
			return (QuestAlternativeMessage)questAlternativeSerializer.Deserialize(val, new SerializationContext(serializerResolver), (PropertyMetaData)null);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
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
