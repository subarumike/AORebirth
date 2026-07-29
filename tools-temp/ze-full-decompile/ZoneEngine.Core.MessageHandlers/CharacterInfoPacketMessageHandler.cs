using System;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Database.Dao;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.PacketHandlers;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class CharacterInfoPacketMessageHandler : BaseMessageHandler<InfoPacketMessage, CharacterInfoPacketMessageHandler>
{
	public void Send(ICharacter character, ICharacter infoTarget)
	{
		((AbstractMessageHandler<InfoPacketMessage>)(object)this).Send(character, CharacterInfoPacket(character, infoTarget), false);
	}

	private static MessageDataFiller<InfoPacketMessage> CharacterInfoPacket(ICharacter character, ICharacter tPlayer)
	{
		return delegate(InfoPacketMessage x)
		{
			//IL_0153: Unknown result type (might be due to invalid IL or missing references)
			//IL_018d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0177: Unknown result type (might be due to invalid IL or missing references)
			//IL_034d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0352: Unknown result type (might be due to invalid IL or missing references)
			//IL_035a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0378: Unknown result type (might be due to invalid IL or missing references)
			//IL_038a: Unknown result type (might be due to invalid IL or missing references)
			//IL_03a9: Unknown result type (might be due to invalid IL or missing references)
			//IL_03ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_03d2: Unknown result type (might be due to invalid IL or missing references)
			//IL_03f0: Unknown result type (might be due to invalid IL or missing references)
			//IL_040d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0415: Unknown result type (might be due to invalid IL or missing references)
			//IL_041d: Unknown result type (might be due to invalid IL or missing references)
			//IL_042f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0441: Unknown result type (might be due to invalid IL or missing references)
			//IL_0449: Unknown result type (might be due to invalid IL or missing references)
			//IL_0451: Unknown result type (might be due to invalid IL or missing references)
			//IL_045a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0462: Unknown result type (might be due to invalid IL or missing references)
			//IL_046b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0473: Unknown result type (might be due to invalid IL or missing references)
			//IL_0494: Unknown result type (might be due to invalid IL or missing references)
			//IL_04b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_04d6: Unknown result type (might be due to invalid IL or missing references)
			//IL_04f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_0518: Unknown result type (might be due to invalid IL or missing references)
			//IL_0539: Unknown result type (might be due to invalid IL or missing references)
			//IL_055a: Unknown result type (might be due to invalid IL or missing references)
			//IL_057b: Unknown result type (might be due to invalid IL or missing references)
			//IL_059c: Unknown result type (might be due to invalid IL or missing references)
			//IL_05bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_05e3: Expected O, but got Unknown
			//IL_023e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0249: Unknown result type (might be due to invalid IL or missing references)
			//IL_024e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0256: Unknown result type (might be due to invalid IL or missing references)
			//IL_025e: Unknown result type (might be due to invalid IL or missing references)
			//IL_027c: Unknown result type (might be due to invalid IL or missing references)
			//IL_028e: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ab: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d2: Unknown result type (might be due to invalid IL or missing references)
			//IL_02f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0313: Unknown result type (might be due to invalid IL or missing references)
			//IL_031f: Unknown result type (might be due to invalid IL or missing references)
			//IL_032b: Unknown result type (might be due to invalid IL or missing references)
			//IL_033c: Expected O, but got Unknown
			//IL_05e6: Unknown result type (might be due to invalid IL or missing references)
			//IL_05f5: Unknown result type (might be due to invalid IL or missing references)
			uint baseValue = ((IStats)tPlayer).Stats[(StatIds)333].BaseValue;
			string text = null;
			text = ((baseValue < 1400) ? string.Empty : ((baseValue < 1500) ? "Freshman" : ((baseValue < 1600) ? "Rookie" : ((baseValue < 1700) ? "Apprentice" : ((baseValue < 1800) ? "Novice" : ((baseValue < 1900) ? "Neophyte" : ((baseValue < 2000) ? "Experienced" : ((baseValue < 2100) ? "Expert" : ((baseValue < 2300) ? "Master" : ((baseValue >= 2500) ? "Grand Master" : "Champion"))))))))));
			int governingForm = 0;
			try
			{
				governingForm = Dao<DBOrganization, OrganizationDao>.Instance.GetGovernmentForm(((IStats)character).Stats[(StatIds)5].Value);
			}
			catch (Exception)
			{
			}
			InfoPacketType val = (InfoPacketType)64;
			int? num;
			string organizationRank;
			if (((IStats)tPlayer).Stats[(StatIds)5].BaseValue == 0)
			{
				val = (InfoPacketType)64;
				num = null;
				organizationRank = null;
			}
			else
			{
				val = (InfoPacketType)65;
				num = (int)((IStats)tPlayer).Stats[(StatIds)5].BaseValue;
				organizationRank = ((((IStats)character).Stats[(StatIds)5].BaseValue != ((IStats)tPlayer).Stats[(StatIds)5].BaseValue) ? string.Empty : OrgClient.GetRank(governingForm, ((IStats)tPlayer).Stats[(StatIds)48].BaseValue));
			}
			int organizationCityPlayfieldId = GetOrganizationCityPlayfieldId(num);
			if (((IStats)tPlayer).Stats[(StatIds)455].Value != 0)
			{
				val = (InfoPacketType)80;
				((N3Message)x).Unknown = 1;
				x.Info = (InfoPacket)new MonsterInfoPacket
				{
					Unknown1 = 1,
					Unknown2 = 0,
					CurrentHealth = ((IStats)tPlayer).Stats[(StatIds)27].Value,
					Level = ResolveInfoLevel(tPlayer),
					MaxHealth = ((IStats)tPlayer).Stats[(StatIds)1].Value,
					OrganizationId = 0,
					Profession = (byte)((IStats)tPlayer).Stats[(StatIds)60].Value,
					TitleLevel = (byte)((IStats)tPlayer).Stats[(StatIds)37].Value,
					VisualProfession = (byte)((IStats)tPlayer).Stats[(StatIds)368].Value,
					Unknown8 = 1234567890,
					Unknown9 = 1234567890,
					Unknown10 = 1234567890
				};
			}
			else
			{
				((N3Message)x).Unknown = 0;
				x.Info = (InfoPacket)new CharacterInfoPacket
				{
					Unknown1 = 1,
					Profession = (Profession)((IStats)tPlayer).Stats[(StatIds)60].Value,
					Level = ResolveInfoLevel(tPlayer),
					TitleLevel = (byte)((IStats)tPlayer).Stats[(StatIds)37].Value,
					VisualProfession = (Profession)((IStats)tPlayer).Stats[(StatIds)368].Value,
					SideXp = 0,
					Health = ((IStats)tPlayer).Stats[(StatIds)27].Value,
					MaxHealth = ((IStats)tPlayer).Stats[(StatIds)1].Value,
					BreedHostility = 0,
					OrganizationId = num,
					FirstName = tPlayer.FirstName,
					LastName = tPlayer.LastName,
					LegacyTitle = text,
					Unknown2 = 0,
					OrganizationRank = organizationRank,
					TowerFields = null,
					CityPlayfieldId = organizationCityPlayfieldId,
					Towers = null,
					InvadersKilled = ((IStats)tPlayer).Stats[(StatIds)615].Value,
					KilledByInvaders = ((IStats)tPlayer).Stats[(StatIds)616].Value,
					AiLevel = ((IStats)tPlayer).Stats[(StatIds)169].Value,
					PvpDuelWins = ((IStats)tPlayer).Stats[(StatIds)674].Value,
					PvpDuelLoses = ((IStats)tPlayer).Stats[(StatIds)675].Value,
					PvpProfessionDuelLoses = ((IStats)tPlayer).Stats[(StatIds)677].Value,
					PvpSoloKills = ((IStats)tPlayer).Stats[(StatIds)678].Value,
					PvpTeamKills = ((IStats)tPlayer).Stats[(StatIds)680].Value,
					PvpSoloScore = ((IStats)tPlayer).Stats[(StatIds)682].Value,
					PvpTeamScore = ((IStats)tPlayer).Stats[(StatIds)683].Value,
					PvpDuelScore = ((IStats)tPlayer).Stats[(StatIds)684].Value
				};
			}
			x.Type = val;
			((N3Message)x).Identity = ((IEntity)tPlayer).Identity;
		};
	}

	private static int GetOrganizationCityPlayfieldId(int? orgId)
	{
		if (!orgId.HasValue || orgId.Value <= 0)
		{
			return 0;
		}
		try
		{
			DBOrganization val = ((Dao<DBOrganization, OrganizationDao>)(object)Dao<DBOrganization, OrganizationDao>.Instance).Get(orgId.Value);
			return (val != null) ? val.CityId : 0;
		}
		catch (Exception)
		{
			return 0;
		}
	}

	private static byte ResolveInfoLevel(ICharacter character)
	{
		uint baseValue = ((IStats)character).Stats[(StatIds)54].BaseValue;
		if (baseValue == 0 || baseValue == 1234567890 || baseValue > 200)
		{
			return 1;
		}
		return (byte)baseValue;
	}

	internal void Send(ICharacter character, Identity identity)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		IEntity @object = Pool.Instance.GetObject(((IEntity)((IInstancedEntity)character).Playfield).Identity, identity);
		ICharacter val = (ICharacter)(object)((@object is ICharacter) ? @object : null);
		if (val != null && ((IStats)val).Stats[(StatIds)455].Value == 0)
		{
			Send(character, val);
		}
	}
}
