using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.Entities;
using AORebirth.Core.Nanos;
using AORebirth.Core.Network;
using AORebirth.Core.Playfields;
using AORebirth.Core.Textures;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.Playfields;
using ZoneEngine.Core.Subway.Quests;

namespace ZoneEngine.Core.Packets;

public static class SimpleCharFullUpdate
{
	private const int SubwayPlayfieldResource = 127;

	private static readonly byte[] CapturedSubwayFilthFleaExtendedTextureOverrideData = new byte[48]
	{
		0, 0, 7, 226, 77, 97, 116, 101, 114, 105,
		97, 108, 32, 35, 57, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 59, 129,
		0, 0, 0, 0, 0, 0, 0, 1
	};

	private static readonly byte[] CapturedSubwayThiefUnknown1 = new byte[28]
	{
		63, 188, 194, 39, 61, 85, 187, 161, 190, 137,
		240, 78, 2, 2, 1, 1, 0, 1, 0, 1,
		0, 1, 0, 0, 0, 2, 0, 0
	};

	public static SimpleCharFullUpdateMessage ConstructMessage(Character character)
	{
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0399: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a3: Expected O, but got Unknown
		//IL_03e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ed: Expected O, but got Unknown
		//IL_04fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0505: Expected O, but got Unknown
		//IL_0506: Unknown result type (might be due to invalid IL or missing references)
		//IL_050b: Unknown result type (might be due to invalid IL or missing references)
		//IL_051e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0523: Unknown result type (might be due to invalid IL or missing references)
		//IL_0536: Unknown result type (might be due to invalid IL or missing references)
		//IL_053b: Unknown result type (might be due to invalid IL or missing references)
		//IL_054e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0553: Unknown result type (might be due to invalid IL or missing references)
		//IL_0567: Unknown result type (might be due to invalid IL or missing references)
		//IL_05f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_05fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0656: Unknown result type (might be due to invalid IL or missing references)
		//IL_065b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0668: Unknown result type (might be due to invalid IL or missing references)
		//IL_0675: Unknown result type (might be due to invalid IL or missing references)
		//IL_0687: Expected O, but got Unknown
		//IL_068a: Unknown result type (might be due to invalid IL or missing references)
		//IL_068f: Unknown result type (might be due to invalid IL or missing references)
		//IL_069d: Unknown result type (might be due to invalid IL or missing references)
		//IL_06ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_06b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_06cc: Expected O, but got Unknown
		//IL_06cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_06d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0612: Unknown result type (might be due to invalid IL or missing references)
		//IL_061b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0620: Unknown result type (might be due to invalid IL or missing references)
		//IL_0624: Unknown result type (might be due to invalid IL or missing references)
		//IL_0632: Unknown result type (might be due to invalid IL or missing references)
		//IL_0637: Unknown result type (might be due to invalid IL or missing references)
		//IL_0646: Unknown result type (might be due to invalid IL or missing references)
		//IL_070e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0748: Unknown result type (might be due to invalid IL or missing references)
		//IL_0782: Unknown result type (might be due to invalid IL or missing references)
		//IL_07bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_07fb: Expected O, but got Unknown
		//IL_0933: Unknown result type (might be due to invalid IL or missing references)
		//IL_093a: Expected O, but got Unknown
		//IL_09e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0907: Unknown result type (might be due to invalid IL or missing references)
		//IL_090c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0916: Unknown result type (might be due to invalid IL or missing references)
		//IL_0922: Expected O, but got Unknown
		//IL_0c04: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c6a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c74: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c76: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d11: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d1b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e5d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e62: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e6a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e72: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e7b: Expected O, but got Unknown
		//IL_0e7d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e82: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e8a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e92: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e9b: Expected O, but got Unknown
		//IL_0e9d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ea2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0eaa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0eb2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ebb: Expected O, but got Unknown
		//IL_0ebd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ec2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0eca: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ed2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0edb: Expected O, but got Unknown
		//IL_0edd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ee2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0eea: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ef2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0efb: Expected O, but got Unknown
		//IL_0ff4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ffb: Expected O, but got Unknown
		//IL_1159: Unknown result type (might be due to invalid IL or missing references)
		//IL_10ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_10b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_10c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_10cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_10dc: Expected O, but got Unknown
		//IL_12b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_12b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_12c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_12dc: Expected O, but got Unknown
		//IL_11a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_11b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_11b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_152e: Unknown result type (might be due to invalid IL or missing references)
		//IL_1532: Unknown result type (might be due to invalid IL or missing references)
		//IL_1537: Unknown result type (might be due to invalid IL or missing references)
		//IL_153f: Unknown result type (might be due to invalid IL or missing references)
		//IL_154c: Expected O, but got Unknown
		//IL_154f: Unknown result type (might be due to invalid IL or missing references)
		//IL_1559: Unknown result type (might be due to invalid IL or missing references)
		//IL_155b: Unknown result type (might be due to invalid IL or missing references)
		//IL_13fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_1405: Expected O, but got Unknown
		//IL_1405: Unknown result type (might be due to invalid IL or missing references)
		//IL_140c: Expected O, but got Unknown
		//IL_167a: Unknown result type (might be due to invalid IL or missing references)
		//IL_167e: Unknown result type (might be due to invalid IL or missing references)
		//IL_1688: Unknown result type (might be due to invalid IL or missing references)
		//IL_168a: Unknown result type (might be due to invalid IL or missing references)
		//IL_1473: Unknown result type (might be due to invalid IL or missing references)
		//IL_1478: Unknown result type (might be due to invalid IL or missing references)
		//IL_1486: Unknown result type (might be due to invalid IL or missing references)
		//IL_1494: Unknown result type (might be due to invalid IL or missing references)
		//IL_14a7: Expected O, but got Unknown
		Dictionary<int, int> dictionary = new Dictionary<int, int>();
		List<AOTextures> list = new List<AOTextures>();
		List<AONano> list2 = new List<AONano>();
		bool flag;
		bool flag2;
		Identity value;
		int instance;
		Coordinate val;
		Identity identity;
		Quaternion heading;
		uint baseValue;
		uint baseValue2;
		uint baseValue3;
		uint baseValue4;
		uint baseValue5;
		string text;
		int num;
		int value2;
		int value3;
		int value4;
		uint baseValue6;
		uint baseValue7;
		uint baseValue8;
		uint baseValue9;
		uint baseValue10;
		uint baseValue11;
		string firstName;
		string lastName;
		int length;
		string organizationName;
		int num2;
		int value5;
		int value6;
		int value7;
		int value8;
		int value9;
		uint baseValue12;
		int count;
		int value10;
		List<AOMeshs> meshs;
		int value11;
		int value12;
		int value13;
		int value14;
		lock (character)
		{
			flag = (((Dynel)character).Stats[(StatIds)673].Value & 0x40) > 0;
			flag2 = (((Dynel)character).Stats[(StatIds)673].Value & 0x20) > 0;
			value = ((IEntity)((Dynel)character).Playfield).Identity;
			instance = ((Identity)(ref value)).Instance;
			val = ((Dynel)character).Coordinates();
			identity = ((PooledObject)character).Identity;
			heading = ((Dynel)character).Heading;
			baseValue = ((Dynel)character).Stats[(StatIds)33].BaseValue;
			baseValue2 = ((Dynel)character).Stats[(StatIds)47].BaseValue;
			baseValue3 = ((Dynel)character).Stats[(StatIds)4].BaseValue;
			baseValue4 = ((Dynel)character).Stats[(StatIds)59].BaseValue;
			baseValue5 = ((Dynel)character).Stats[(StatIds)89].BaseValue;
			text = ((Dynel)character).Name;
			num = ((Dynel)character).Stats[(StatIds)0].Value;
			if (((Dynel)character).Stats[(StatIds)215].Value > 0)
			{
				num &= -268435457;
				num |= 0x800000;
				if (text != null && text.IndexOf("[GM]", StringComparison.OrdinalIgnoreCase) < 0)
				{
					text += " [GM]";
				}
			}
			value2 = ((Dynel)character).Stats[(StatIds)660].Value;
			value3 = ((Dynel)character).Stats[(StatIds)389].Value;
			value4 = ((Dynel)character).Stats[(StatIds)214].Value;
			baseValue6 = ((Dynel)character).Stats[(StatIds)16].BaseValue;
			baseValue7 = ((Dynel)character).Stats[(StatIds)18].BaseValue;
			baseValue8 = ((Dynel)character).Stats[(StatIds)17].BaseValue;
			baseValue9 = ((Dynel)character).Stats[(StatIds)20].BaseValue;
			baseValue10 = ((Dynel)character).Stats[(StatIds)19].BaseValue;
			baseValue11 = ((Dynel)character).Stats[(StatIds)21].BaseValue;
			firstName = character.FirstName;
			lastName = character.LastName;
			length = ((Dynel)character).OrganizationName.Length;
			organizationName = ((Dynel)character).OrganizationName;
			num2 = (int)((Dynel)character).Stats[(StatIds)54].BaseValue;
			if (num2 <= 0)
			{
				num2 = 1;
			}
			value5 = ((Dynel)character).Stats[(StatIds)1].Value;
			value6 = ((Dynel)character).Stats[(StatIds)359].Value;
			value7 = ((Dynel)character).Stats[(StatIds)360].Value;
			value8 = ((Dynel)character).Stats[(StatIds)673].Value;
			value9 = ((Dynel)character).Stats[(StatIds)173].Value;
			baseValue12 = ((Dynel)character).Stats[(StatIds)156].BaseValue;
			count = ((Dynel)character).Textures.Count;
			value10 = ((Dynel)character).Stats[(StatIds)64].Value;
			foreach (int key in character.SocialTab.Keys)
			{
				dictionary.Add(key, character.SocialTab[key]);
			}
			foreach (AOTextures texture in ((Dynel)character).Textures)
			{
				list.Add(new AOTextures(texture.place, texture.Texture));
			}
			meshs = MeshLayers.GetMeshs(character, flag2, flag);
			foreach (KeyValuePair<int, IActiveNano> activeNano in character.ActiveNanos)
			{
				AONano val2 = new AONano();
				val2.ID = activeNano.Value.ID;
				val2.Instance = activeNano.Value.Instance;
				val2.NanoStrain = activeNano.Key;
				val2.Nanotype = activeNano.Value.Nanotype;
				val2.TickCounter = activeNano.Value.TickCounter;
				val2.TickInterval = activeNano.Value.TickInterval;
				val2.Value3 = activeNano.Value.Value3;
				list2.Add(val2);
			}
			value11 = ((Dynel)character).Stats[(StatIds)466].Value;
			value12 = ((Dynel)character).Stats[(StatIds)455].Value;
			value13 = ((Dynel)character).Stats[(StatIds)196].Value;
			value14 = ((Dynel)character).Stats[(StatIds)27].Value;
		}
		SimpleCharFullUpdateMessage val3 = new SimpleCharFullUpdateMessage();
		value = ((PooledObject)character).Identity;
		OrdinaryEnemyRuntimeDefinition definition;
		bool flag3 = OrdinaryEnemyRuntimeRegistry.TryGet(((Identity)(ref value)).Instance, out definition);
		value = ((PooledObject)character).Identity;
		CapturedEncounterRuntimeDefinition definition2;
		bool flag4 = CapturedEncounterRuntimeRegistry.TryGet(((Identity)(ref value)).Instance, out definition2);
		value = ((PooledObject)character).Identity;
		CapturedSubwayVendorRuntimeDefinition runtime;
		bool flag5 = CapturedSubwayVendorRuntimeRegistry.TryGet(((Identity)(ref value)).Instance, out runtime);
		value = ((PooledObject)character).Identity;
		WindcallerKarrecNpcRuntimeDefinition runtime2;
		bool flag6 = WindcallerKarrecNpcRuntimeRegistry.TryGet(((Identity)(ref value)).Instance, out runtime2);
		((N3Message)val3).Identity = identity;
		val3.Version = 57;
		if (flag4 || flag5 || flag6 || (flag3 && definition.Profile.Appearance.ScfuProfile == OrdinaryEnemyScfuProfile.CapturedThief) || (instance == 127 && character.Waypoints != null && character.Waypoints.Count > 1))
		{
			val3.Version = 58;
		}
		else if (value13 != 0)
		{
			val3.Version = 58;
		}
		val3.PlayfieldId = instance;
		value = character.FightingTarget;
		if (((Identity)(ref value)).Instance != 0)
		{
			value = default(Identity);
			Identity fightingTarget = character.FightingTarget;
			((Identity)(ref value)).Type = ((Identity)(ref fightingTarget)).Type;
			fightingTarget = character.FightingTarget;
			((Identity)(ref value)).Instance = ((Identity)(ref fightingTarget)).Instance;
			val3.FightingTarget = value;
		}
		val3.Coordinates = new Vector3
		{
			X = val.x,
			Y = val.y,
			Z = val.z
		};
		val3.Heading = new Quaternion
		{
			W = heading.wf,
			X = heading.xf,
			Y = heading.yf,
			Z = heading.zf
		};
		val3.Appearance = new Appearance
		{
			Side = (Side)(flag6 ? runtime2.Content.Side : (flag5 ? runtime.Content.Side : (flag4 ? definition2.Side : ((int)baseValue)))),
			Fatness = (Fatness)(flag6 ? runtime2.Content.Fatness : (flag5 ? runtime.Content.Fatness : (flag4 ? definition2.Fatness : ((int)baseValue2)))),
			Breed = (Breed)(flag6 ? runtime2.Content.Breed : (flag5 ? runtime.Content.Breed : (flag4 ? definition2.Breed : ((int)baseValue3)))),
			Gender = (Gender)(flag6 ? runtime2.Content.Sex : (flag5 ? runtime.Content.Sex : (flag4 ? definition2.Sex : ((int)baseValue4)))),
			Race = (flag6 ? ((uint)runtime2.Content.Race) : (flag5 ? ((uint)runtime.Content.Race) : (flag4 ? ((uint)definition2.Race) : baseValue5)))
		};
		if (flag6)
		{
			val3.Appearance.Value = (uint)runtime2.Content.AppearanceValue;
		}
		else if (flag5)
		{
			val3.Appearance.Value = (uint)runtime.Content.AppearanceValue;
		}
		else if (flag4)
		{
			val3.Appearance.Value = definition2.AppearanceValue;
		}
		else if (flag3 && definition.Profile.Appearance.ScfuProfile == OrdinaryEnemyScfuProfile.CapturedThief)
		{
			val3.Appearance.Value = definition.Profile.Appearance.AppearanceValue;
		}
		val3.Name = text;
		val3.CharacterFlags = (CharacterFlags)num;
		val3.AccountFlags = (short)value2;
		val3.Expansions = (short)value3;
		bool flag7 = flag6 || flag5 || ((Dynel)character).Controller is NPCController || (value12 != 1234567890 && value12 != 0);
		if (flag7)
		{
			SimpleNpcInfo characterInfo = new SimpleNpcInfo
			{
				Family = (short)value12,
				LosHeight = (short)value11
			};
			val3.CharacterInfo = (SimpleCharacterInfo)(object)characterInfo;
		}
		else
		{
			SimplePcInfo val4 = new SimplePcInfo();
			val4.CurrentNano = (uint)value4;
			val4.Team = 0;
			val4.Swim = 5;
			val4.StrengthBase = (short)Math.Min(baseValue6, 32767L);
			val4.AgilityBase = (short)Math.Min(baseValue8, 32767L);
			val4.StaminaBase = (short)Math.Min(baseValue7, 32767L);
			val4.IntelligenceBase = (short)Math.Min(baseValue10, 32767L);
			val4.SenseBase = (short)Math.Min(baseValue9, 32767L);
			val4.PsychicBase = (short)Math.Min(baseValue11, 32767L);
			if (((Enum)val3.CharacterFlags).HasFlag((Enum)(object)(CharacterFlags)4194304))
			{
				val4.FirstName = firstName;
				val4.LastName = lastName;
			}
			if (length != 0)
			{
				val4.OrgName = organizationName;
			}
			val3.CharacterInfo = (SimpleCharacterInfo)(object)val4;
		}
		val3.Level = (short)num2;
		int num3 = value5;
		int num4 = value14;
		if (value5 > 65535)
		{
			num3 = 65535;
			if (value5 > 0)
			{
				num4 = (int)((long)value14 * 65535L / value5);
				if (num4 < 0)
				{
					num4 = 0;
				}
				else if (num4 > num3)
				{
					num4 = num3;
				}
			}
			else
			{
				num4 = 0;
			}
		}
		val3.Health = num3;
		val3.HealthDamage = num3 - num4;
		if (instance == 152 || instance == 4107)
		{
			val3.MonsterData = 99902u;
		}
		else
		{
			val3.MonsterData = (uint)value6;
		}
		val3.MonsterScale = (short)value7;
		val3.VisualFlags = (short)value8;
		val3.VisibleTitle = 0;
		byte[] obj = new byte[42]
		{
			128, 0, 0, 0, 0, 0, 0, 0, 128, 0,
			0, 0, 0, 1, 0, 1, 0, 1, 0, 1,
			0, 1, 0, 0, 0, 3, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0
		};
		obj[12] = (byte)value9;
		val3.Unknown1 = obj;
		if (flag7)
		{
			byte[] obj2 = new byte[28]
			{
				0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
				0, 0, 0, 1, 0, 1, 0, 1, 0, 1,
				0, 1, 0, 0, 0, 2, 0, 0
			};
			obj2[12] = (byte)value9;
			val3.Unknown1 = obj2;
		}
		if (flag3 && definition.Profile.Appearance.ScfuProfile == OrdinaryEnemyScfuProfile.CapturedThief)
		{
			val3.Unknown1 = CapturedSubwayThiefUnknown1;
			val3.AdditionalFlags = (SimpleCharFullUpdateFlags)134217736;
			val3.SuppressedFlags = (SimpleCharFullUpdateFlags)2097152;
		}
		else if (value13 != 0)
		{
			val3.AdditionalFlags = (SimpleCharFullUpdateFlags)167772168;
		}
		else if (string.Equals(text, "Surveillance Droid", StringComparison.Ordinal) || (flag7 && value6 == 210238))
		{
			SimpleCharFullUpdateFlags val5 = (SimpleCharFullUpdateFlags)170543699;
			val3.Name = "Surveillance Droid";
			val3.Appearance.Value = 1480u;
			val3.MonsterData = 210238u;
			val3.MonsterScale = 110;
			val3.CharacterFlags = (CharacterFlags)268964353;
			val3.Unknown1 = SurveillanceDroidRuntime.CapturedUnknown1;
			val3.Flags2 = 0;
			val3.Unknown2 = 0;
			val3.AdditionalFlags = val5;
			val3.SuppressedFlags = ~val5;
			SimpleCharacterInfo characterInfo2 = val3.CharacterInfo;
			SimpleNpcInfo val6 = (SimpleNpcInfo)(object)((characterInfo2 is SimpleNpcInfo) ? characterInfo2 : null);
			if (val6 != null)
			{
				val6.Family = 137;
				val6.LosHeight = 0;
				val6.UnknownData = 0;
			}
		}
		else if (!flag6 && !flag5 && !flag4 && ((Dynel)character).Controller is NPCController && AndromedaIccHqSpawn.IsAndromedaCityNpcPlayfield(instance))
		{
			val3.AdditionalFlags = (SimpleCharFullUpdateFlags)134217736;
			val3.SuppressedFlags = (SimpleCharFullUpdateFlags)2097152;
			if (AndromedaIccHqSpawn.NeedsNataliaScfuFlag7(text))
			{
				val3.AdditionalFlags = (SimpleCharFullUpdateFlags)(val3.AdditionalFlags | 0x400000);
			}
			if (AndromedaIccHqSpawn.TryGetExtendedTextureOverride(text, out var data))
			{
				val3.ExtendedTextureOverrideData = data;
			}
		}
		if (AlexAreaMobRuntime.TryGetExtendedTextureOverride(text, out var data2))
		{
			val3.ExtendedTextureOverrideData = data2;
		}
		if (SurveillanceDroidRuntime.TryGetExtendedTextureOverride(text, out var data3))
		{
			val3.ExtendedTextureOverrideData = data3;
		}
		int num5 = (flag6 ? runtime2.Content.HeadMesh : (flag5 ? runtime.Content.HeadMesh : (flag4 ? definition2.HeadMesh : value10)));
		if (num5 != 0)
		{
			val3.HeadMesh = (uint)num5;
		}
		val3.RunSpeedBase = (flag6 ? ((short)runtime2.Content.RunSpeed) : (flag5 ? ((short)runtime.Content.RunSpeed) : (flag4 ? ((short)definition2.CapturedScfuRunSpeedBase) : ((short)baseValue12))));
		if (string.Equals(text, "Surveillance Droid", StringComparison.Ordinal) || value6 == 210238)
		{
			val3.RunSpeedBase = 20;
			val3.HeadMesh = null;
			val3.Meshes = Array.Empty<Mesh>();
			val3.Textures = (Texture[])(object)new Texture[5]
			{
				new Texture
				{
					Place = 0,
					Id = 0,
					Unknown = 0
				},
				new Texture
				{
					Place = 1,
					Id = 0,
					Unknown = 0
				},
				new Texture
				{
					Place = 2,
					Id = 0,
					Unknown = 0
				},
				new Texture
				{
					Place = 3,
					Id = 0,
					Unknown = 0
				},
				new Texture
				{
					Place = 4,
					Id = 0,
					Unknown = 0
				}
			};
		}
		if (flag3 && definition.Profile.Appearance.ScfuProfile == OrdinaryEnemyScfuProfile.CapturedFilthFlea)
		{
			val3.ExtendedTextureOverrideData = CapturedSubwayFilthFleaExtendedTextureOverrideData;
		}
		else if (value13 != 0 && PetBureaucratGuardianAppearance.IsGuardianPet((ICharacter)(object)character))
		{
			val3.ExtendedTextureOverrideData = PetSummonScfuExtensions.CloneGuardianExtendedTextureOverrideData();
			val3.VisualFlags = 31;
		}
		val3.ActiveNanos = ((IEnumerable<AONano>)list2).Select((Func<AONano, ActiveNano>)delegate(AONano nano)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0058: Expected O, but got Unknown
			ActiveNano val14 = new ActiveNano();
			Identity nanoIdentity2 = default(Identity);
			((Identity)(ref nanoIdentity2)).Type = (IdentityType)53019;
			((Identity)(ref nanoIdentity2)).Instance = nano.ID;
			val14.NanoIdentity = nanoIdentity2;
			val14.NanoInstance = nano.Instance;
			val14.Time1 = nano.TickCounter;
			val14.Time2 = nano.TickInterval;
			return val14;
		}).ToArray();
		if (character.Waypoints != null && character.Waypoints.Count > 1)
		{
			val3.Waypoints = ((IEnumerable<Waypoint>)character.Waypoints).Select((Func<Waypoint, Vector3>)((Waypoint waypoint) => new Vector3
			{
				X = (float)waypoint.Position.x,
				Y = (float)waypoint.Position.y,
				Z = (float)waypoint.Position.z
			})).ToArray();
		}
		List<Texture> list3 = new List<Texture>();
		AOTextures val7 = new AOTextures(0, 0);
		for (int i = 0; i < 5; i++)
		{
			val7.Texture = 0;
			val7.place = i;
			for (int j = 0; j < count; j++)
			{
				if (list[j].place == i)
				{
					val7.Texture = list[j].Texture;
					break;
				}
			}
			if (flag2)
			{
				if (flag)
				{
					val7.Texture = dictionary[i];
				}
				else if (dictionary[i] != 0)
				{
					val7.Texture = dictionary[i];
				}
			}
			list3.Add(new Texture
			{
				Place = val7.place,
				Id = val7.Texture,
				Unknown = 0
			});
		}
		val3.Textures = list3.ToArray();
		val3.Meshes = ((IEnumerable<AOMeshs>)meshs).Select((Func<AOMeshs, Mesh>)((AOMeshs aoMesh) => new Mesh
		{
			Position = (byte)aoMesh.Position,
			Id = (uint)aoMesh.Mesh,
			OverrideTextureId = aoMesh.OverrideTexture,
			Layer = (byte)aoMesh.Layer
		})).ToArray();
		val3.Flags2 = 0;
		val3.Unknown2 = 0;
		if (flag4)
		{
			SimpleCharFullUpdateFlags val8 = (SimpleCharFullUpdateFlags)definition2.CapturedScfuFlags;
			SimpleCharacterInfo characterInfo3 = val3.CharacterInfo;
			SimpleNpcInfo val9 = (SimpleNpcInfo)(object)((characterInfo3 is SimpleNpcInfo) ? characterInfo3 : null);
			if (val9 != null)
			{
				val9.Family = (short)definition2.NpcFamily;
				val9.LosHeight = (short)definition2.NpcLosHeight;
				val9.UnknownData = (byte)definition2.CapturedScfuNpcUnknownData;
			}
			val3.AdditionalFlags = val8;
			val3.SuppressedFlags = ~val8;
			val3.Flags2 = (byte)definition2.CapturedScfuFlags2;
			val3.Unknown1 = definition2.CapturedScfuUnknown1.ToArray();
			val3.Unknown2 = (byte)definition2.CapturedScfuUnknown2;
			val3.Textures = ((IEnumerable<CapturedSubwayTextureDefinition>)definition2.Textures).Select((Func<CapturedSubwayTextureDefinition, Texture>)((CapturedSubwayTextureDefinition texture) => new Texture
			{
				Place = texture.Place,
				Id = texture.Id,
				Unknown = texture.Unknown
			})).ToArray();
			val3.Meshes = ((IEnumerable<CapturedSubwayMeshDefinition>)definition2.Meshes).Select((Func<CapturedSubwayMeshDefinition, Mesh>)((CapturedSubwayMeshDefinition mesh) => new Mesh
			{
				Position = (byte)mesh.Position,
				Id = mesh.Id,
				OverrideTextureId = mesh.OverrideTextureId,
				Layer = (byte)mesh.Layer
			})).ToArray();
			val3.Waypoints = ((IEnumerable<CapturedSubwayWaypointDefinition>)definition2.Waypoints).Select((Func<CapturedSubwayWaypointDefinition, Vector3>)((CapturedSubwayWaypointDefinition waypoint) => new Vector3
			{
				X = waypoint.X,
				Y = waypoint.Y,
				Z = waypoint.Z
			})).ToArray();
		}
		else if (flag6)
		{
			WindcallerKarrecNpcDefinition content = runtime2.Content;
			val3.CharacterInfo = (SimpleCharacterInfo)new SimpleNpcInfo
			{
				Family = (short)content.NpcFamily,
				LosHeight = (short)content.NpcLosHeight
			};
			val3.CharacterFlags = (CharacterFlags)content.CharacterFlags;
			val3.AccountFlags = 0;
			val3.Expansions = 0;
			val3.AdditionalFlags = (SimpleCharFullUpdateFlags)8;
			val3.SuppressedFlags = (SimpleCharFullUpdateFlags)0;
			val3.VisualFlags = (short)content.VisualFlags;
			val3.Flags2 = 0;
			val3.Unknown1 = content.CapturedScfuUnknown1.ToArray();
			val3.Unknown2 = 0;
			val3.VisibleTitle = (byte)content.VisibleTitle;
			val3.ActiveNanos = ((IEnumerable<WindcallerKarrecNpcActiveNanoDefinition>)content.ActiveNanos).Select((Func<WindcallerKarrecNpcActiveNanoDefinition, ActiveNano>)delegate(WindcallerKarrecNpcActiveNanoDefinition nano)
			{
				//IL_0000: Unknown result type (might be due to invalid IL or missing references)
				//IL_0005: Unknown result type (might be due to invalid IL or missing references)
				//IL_0008: Unknown result type (might be due to invalid IL or missing references)
				//IL_002a: Unknown result type (might be due to invalid IL or missing references)
				//IL_0031: Unknown result type (might be due to invalid IL or missing references)
				//IL_003e: Unknown result type (might be due to invalid IL or missing references)
				//IL_004b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0059: Expected O, but got Unknown
				ActiveNano val13 = new ActiveNano();
				Identity nanoIdentity = default(Identity);
				((Identity)(ref nanoIdentity)).Type = (IdentityType)nano.NanoIdentityType;
				((Identity)(ref nanoIdentity)).Instance = nano.NanoIdentityInstance;
				val13.NanoIdentity = nanoIdentity;
				val13.NanoInstance = nano.NanoInstance;
				val13.Time1 = nano.Time1;
				val13.Time2 = nano.Time2;
				return val13;
			}).ToArray();
			val3.Textures = ((IEnumerable<WindcallerKarrecNpcTextureDefinition>)content.Textures).Select((Func<WindcallerKarrecNpcTextureDefinition, Texture>)((WindcallerKarrecNpcTextureDefinition texture) => new Texture
			{
				Place = texture.Place,
				Id = texture.Id,
				Unknown = texture.Unknown
			})).ToArray();
			val3.Meshes = ((IEnumerable<WindcallerKarrecNpcMeshDefinition>)content.Meshes).Select((Func<WindcallerKarrecNpcMeshDefinition, Mesh>)((WindcallerKarrecNpcMeshDefinition mesh) => new Mesh
			{
				Position = (byte)mesh.Position,
				Id = mesh.Id,
				OverrideTextureId = mesh.OverrideTextureId,
				Layer = (byte)mesh.Layer
			})).ToArray();
			Vector3 currentPosition = new Vector3();
			Vector3 destination = new Vector3();
			bool hasActivePatrolDestination = ((Dynel)character).Controller is NPCController nPCController && nPCController.TryGetCapturedPatrolReplayProjection(out currentPosition, out destination);
			WindcallerKarrecNpcWaypointDefinition windcallerKarrecNpcWaypointDefinition = content.ResolveScfuCoordinates(hasActivePatrolDestination, val3.Coordinates.X, val3.Coordinates.Y, val3.Coordinates.Z, currentPosition.xf, currentPosition.yf, currentPosition.zf);
			val3.Coordinates = new Vector3
			{
				X = windcallerKarrecNpcWaypointDefinition.X,
				Y = windcallerKarrecNpcWaypointDefinition.Y,
				Z = windcallerKarrecNpcWaypointDefinition.Z
			};
			val3.Waypoints = ((IEnumerable<WindcallerKarrecNpcWaypointDefinition>)content.ResolveScfuWaypoints(hasActivePatrolDestination, currentPosition.xf, currentPosition.yf, currentPosition.zf, destination.xf, destination.yf, destination.zf)).Select((Func<WindcallerKarrecNpcWaypointDefinition, Vector3>)((WindcallerKarrecNpcWaypointDefinition waypoint) => new Vector3
			{
				X = waypoint.X,
				Y = waypoint.Y,
				Z = waypoint.Z
			})).ToArray();
		}
		else if (flag5)
		{
			CapturedSubwayVendorDefinition content2 = runtime.Content;
			SimpleCharFullUpdateFlags val10 = (SimpleCharFullUpdateFlags)content2.CapturedScfuFlags;
			val3.CharacterInfo = (SimpleCharacterInfo)new SimpleNpcInfo
			{
				Family = 0,
				LosHeight = 0
			};
			val3.AdditionalFlags = val10;
			val3.SuppressedFlags = ~val10;
			val3.Flags2 = 0;
			val3.Unknown1 = content2.CapturedScfuUnknown1.ToArray();
			val3.Unknown2 = 0;
			val3.VisibleTitle = 0;
			val3.Textures = ((IEnumerable<CapturedSubwayVendorTextureDefinition>)content2.Textures).Select((Func<CapturedSubwayVendorTextureDefinition, Texture>)((CapturedSubwayVendorTextureDefinition texture) => new Texture
			{
				Place = texture.Place,
				Id = texture.Id,
				Unknown = texture.Unknown
			})).ToArray();
			val3.Meshes = ((IEnumerable<CapturedSubwayVendorMeshDefinition>)content2.Meshes).Select((Func<CapturedSubwayVendorMeshDefinition, Mesh>)((CapturedSubwayVendorMeshDefinition mesh) => new Mesh
			{
				Position = (byte)mesh.Position,
				Id = mesh.Id,
				OverrideTextureId = mesh.OverrideTextureId,
				Layer = (byte)mesh.Layer
			})).ToArray();
			val3.Waypoints = ((IEnumerable<CapturedSubwayVendorWaypointDefinition>)content2.Waypoints).Select((Func<CapturedSubwayVendorWaypointDefinition, Vector3>)((CapturedSubwayVendorWaypointDefinition waypoint) => new Vector3
			{
				X = waypoint.X,
				Y = waypoint.Y,
				Z = waypoint.Z
			})).ToArray();
		}
		else if (flag3 && definition.Spawn.HasCapturedScfuOverride)
		{
			OrdinaryEnemySpawnDefinition spawn = definition.Spawn;
			OrdinaryEnemyAppearanceProfile appearance = definition.Profile.Appearance;
			SimpleCharFullUpdateFlags val12 = (val3.AdditionalFlags = (SimpleCharFullUpdateFlags)spawn.CapturedScfuFlags);
			val3.SuppressedFlags = ~val12;
			val3.Flags2 = (byte)spawn.CapturedScfuFlags2;
			val3.Unknown1 = spawn.CapturedScfuUnknown1.ToArray();
			val3.Unknown2 = (byte)spawn.CapturedScfuUnknown2;
			val3.VisibleTitle = (byte)appearance.VisibleTitle;
			val3.Textures = ((IEnumerable<OrdinaryEnemyTextureProfile>)appearance.Textures).Select((Func<OrdinaryEnemyTextureProfile, Texture>)((OrdinaryEnemyTextureProfile texture) => new Texture
			{
				Place = texture.Place,
				Id = texture.Id,
				Unknown = texture.Unknown
			})).ToArray();
			val3.Meshes = ((IEnumerable<OrdinaryEnemyMeshProfile>)appearance.Meshes).Select((Func<OrdinaryEnemyMeshProfile, Mesh>)((OrdinaryEnemyMeshProfile mesh) => new Mesh
			{
				Position = (byte)mesh.Position,
				Id = mesh.Id,
				OverrideTextureId = mesh.OverrideTextureId,
				Layer = (byte)mesh.Layer
			})).ToArray();
			val3.Waypoints = (Vector3[])(object)new Vector3[0];
		}
		return val3;
	}

	public static SimpleCharFullUpdateMessage ConstructMessage(IZoneClient client)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Expected O, but got Unknown
		return SimpleCharFullUpdate.ConstructMessage((Character)client.Controller.Character);
	}

	public static void SendToOne(ICharacter character, IZoneClient receiver)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		SimpleCharFullUpdateMessage val = SimpleCharFullUpdate.ConstructMessage((Character)character);
		((IDynel)receiver.Controller.Character).Send((MessageBody)(object)val, false);
	}

	public static void SendToPlayfield(IZoneClient client)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		SimpleCharFullUpdateMessage val = ConstructMessage(client);
		PlayfieldLifecycleTrace.Record("same-playfield-visibility", "joining-character-simple-char-full-update-broadcast", "SimpleCharFullUpdate", ((IEntity)client.Controller.Character).Identity);
		((IInstancedEntity)client.Controller.Character).Playfield.Announce((MessageBody)(object)val);
	}
}
