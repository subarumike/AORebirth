using System;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Playfields;
using AORebirth.Database.Dao;
using AORebirth.Database.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages.OrgServerMessages;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.Core.PacketHandlers;

public static class OrgClient
{
	private const int CapturedOrgInfoOrganizationInstance = 1970177;

	private const string CapturedOrgInfoOrganizationName = "Est. 2024";

	private const string CapturedOrgInfoLeaderName = "Celcius2024";

	public static bool TryHandleCapturedOrgInfo(OrgClientMessage message, ZoneClient client)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Invalid comparison between Unknown and I4
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Expected O, but got Unknown
		//IL_0197: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dd: Unknown result type (might be due to invalid IL or missing references)
		if (message == null || client == null || (int)message.Command != 5)
		{
			return false;
		}
		Character val = (Character)((client.Controller == null) ? null : /*isinst with value type is only supported in some contexts*/);
		if (val == null)
		{
			return true;
		}
		IInstancedEntity val2 = ((client.Playfield == null) ? null : client.Playfield.FindByIdentity(message.Target));
		Identity organization;
		if (val2 == null)
		{
			organization = message.Target;
			IdentityType type = ((Identity)(ref organization)).Type;
			organization = ((PooledObject)val).Identity;
			if (type == ((Identity)(ref organization)).Type)
			{
				organization = message.Target;
				int instance = ((Identity)(ref organization)).Instance;
				organization = ((PooledObject)val).Identity;
				if (instance == ((Identity)(ref organization)).Instance)
				{
					val2 = (IInstancedEntity)(object)val;
				}
			}
		}
		if (val2 == null)
		{
			((ClientBase)client).Server.Info((IClient)(object)client, "OrgClient Info ignored target={0} unknown1={1} reason=target_not_found evidence=live_capture_20260623-084448", new object[2] { message.Target, message.Unknown1 });
			return true;
		}
		int num = ResolveCharacterStatValue(val2, (StatIds)5);
		if (num <= 0)
		{
			((ClientBase)client).Server.Info((IClient)(object)client, "OrgClient Info ignored target={0} unknown1={1} reason=no_organization evidence=live_capture_20260623-084448", new object[2] { message.Target, message.Unknown1 });
			return true;
		}
		DBOrganization val3 = ResolveOrganization(num);
		int governingForm = ((val3 != null) ? val3.GovernmentForm : 0);
		int leaderId = ((val3 != null) ? val3.LeaderId : 0);
		string leaderName = ResolveLeaderName(leaderId, (Character)(object)((val2 is Character) ? val2 : null), num);
		OrgInfoMessage val4 = new OrgInfoMessage();
		((N3Message)val4).Identity = ((IEntity)val2).Identity;
		((N3Message)val4).Unknown = 0;
		((OrgServerMessage)val4).Unknown1 = 0;
		((OrgServerMessage)val4).Unknown2 = 0;
		organization = default(Identity);
		((Identity)(ref organization)).Type = (IdentityType)57002;
		((Identity)(ref organization)).Instance = num;
		((OrgServerMessage)val4).Organization = organization;
		((OrgServerMessage)val4).OrganizationName = ResolveOrganizationName(val3, (Character)(object)((val2 is Character) ? val2 : null), num);
		val4.Description = ((val3 == null) ? string.Empty : (val3.Description ?? string.Empty));
		val4.Objective = ((val3 == null) ? string.Empty : (val3.Objective ?? string.Empty));
		val4.History = ((val3 == null) ? string.Empty : (val3.History ?? string.Empty));
		val4.GoverningForm = ResolveGoverningFormText(governingForm);
		val4.LeaderName = leaderName;
		val4.Rank = GetRank(governingForm, (uint)ResolveCharacterStatValue(val2, (StatIds)48));
		val4.Unknown3 = new object[0];
		OrgInfoMessage messageBody = val4;
		client.SendCompressed((MessageBody)(object)messageBody);
		return true;
	}

	public static bool TryHandleCapturedCityControllerBankAdd(OrgClientMessage message, ZoneClient client)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Invalid comparison between Unknown and I4
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		if (message != null && client != null && (int)message.Command == 19)
		{
			Identity target = message.Target;
			if ((int)((Identity)(ref target)).Type == 50200)
			{
				Identity val = ((client.Controller == null || client.Controller.Character == null) ? Identity.None : ((IEntity)client.Controller.Character).Identity);
				((ClientBase)client).Server.Info((IClient)(object)client, "OrgClient BankAdd routed to CityController character={0} target={1} unknown1={2} args={3} evidence=live_capture_20260622-073015 no_state_change=1", new object[4]
				{
					val,
					message.Target,
					message.Unknown1,
					message.CommandArgs ?? string.Empty
				});
				return true;
			}
		}
		return false;
	}

	public static void Read(OrgClientMessage message, ZoneClient client)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Expected I4, but got Unknown
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_037a: Unknown result type (might be due to invalid IL or missing references)
		//IL_06fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0bac: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c8b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c90: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a5a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a77: Unknown result type (might be due to invalid IL or missing references)
		//IL_0be0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0be5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0be8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0bf3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0bfb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c03: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c0b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c0e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c44: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c4c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c63: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c6d: Expected O, but got Unknown
		//IL_0b1f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b24: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c9: Unknown result type (might be due to invalid IL or missing references)
		Identity val;
		switch ((byte)(int)message.Command)
		{
		case 1:
		{
			OrganizationDao instance = Dao<DBOrganization, OrganizationDao>.Instance;
			string commandArgs = message.CommandArgs;
			DateTime utcNow = DateTime.UtcNow;
			val = ((IEntity)client.Controller.Character).Identity;
			if (instance.CreateOrganization(commandArgs, utcNow, ((Identity)(ref val)).Instance))
			{
				((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(client.Controller.Character, "You have created the guild: " + message.CommandArgs, 0, 0));
				int organizationId = Dao<DBOrganization, OrganizationDao>.Instance.GetOrganizationId(message.CommandArgs);
				((IStats)client.Controller.Character).Stats[(StatIds)48].Value = 0;
				((IStats)client.Controller.Character).Stats[(StatIds)5].Value = organizationId;
			}
			else
			{
				((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(client.Controller.Character, "This guild already <font color=#DC143C>exists</font>", 0, 0));
			}
			break;
		}
		case 2:
		{
			if (((IStats)client.Controller.Character).Stats[(StatIds)5].BaseValue == 0)
			{
				((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(client.Controller.Character, "You're not in an organization!", 0, 0));
				break;
			}
			int governmentForm3 = Dao<DBOrganization, OrganizationDao>.Instance.GetGovernmentForm((int)((IStats)client.Controller.Character).Stats[(StatIds)5].BaseValue);
			((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(client.Controller.Character, "Current Rank Structure: " + GetRankList(governmentForm3), 0, 0));
			break;
		}
		case 3:
			break;
		case 4:
			Console.WriteLine("Case 4 Started");
			break;
		case 5:
			TryHandleCapturedOrgInfo(message, client);
			break;
		case 6:
		{
			DBOrganization val2 = ((Dao<DBOrganization, OrganizationDao>)(object)Dao<DBOrganization, OrganizationDao>.Instance).Get((int)((IStats)client.Controller.Character).Stats[(StatIds)5].BaseValue);
			if (val2 != null && val2.Bank != 0)
			{
				ulong num = ((IStats)client.Controller.Character).Stats[(StatIds)61].BaseValue + val2.Bank;
				uint num2 = (uint)((num > int.MaxValue) ? 2147483647u : num);
				((IStats)client.Controller.Character).Stats[(StatIds)61].Set(num2, false);
				client.Controller.SendChangedStats();
				((IDatabaseObject)((IStats)client.Controller.Character).Stats).Write();
			}
			StatDao.DisbandOrganization((int)((IStats)client.Controller.Character).Stats[(StatIds)5].BaseValue);
			break;
		}
		case 7:
			break;
		case 8:
			break;
		case 9:
			break;
		case 10:
		{
			Character val3 = null;
			int num3 = -1;
			val3 = client.Playfield.FindByIdentity<Character>(message.Target);
			if (val3 == null)
			{
				break;
			}
			if (((Dynel)val3).Stats[(StatIds)5].BaseValue != ((IStats)client.Controller.Character).Stats[(StatIds)5].BaseValue)
			{
				((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(client.Controller.Character, "Target is not in your organization!", 0, 0));
			}
			else if (((IStats)client.Controller.Character).Stats[(StatIds)48].Value == ((Dynel)val3).Stats[(StatIds)48].Value - 2 || ((IStats)client.Controller.Character).Stats[(StatIds)48].Value == 0)
			{
				DBOrganization val4 = ((Dao<DBOrganization, OrganizationDao>)(object)Dao<DBOrganization, OrganizationDao>.Instance).Get((int)((IStats)client.Controller.Character).Stats[(StatIds)5].BaseValue);
				int governingForm = -1;
				string empty = string.Empty;
				if (val4 != null)
				{
					if (num3 - 1 == 0)
					{
						OrganizationDao instance2 = Dao<DBOrganization, OrganizationDao>.Instance;
						int id = val4.Id;
						val = ((PooledObject)val3).Identity;
						instance2.SetNewPrez(id, ((Identity)(ref val)).Instance);
						((Dynel)val3).Stats[(StatIds)48].Value = 0;
						((IStats)client.Controller.Character).Stats[(StatIds)48].Value = 1;
						((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(client.Controller.Character, "You've passed leadership of the organization to: " + ((Dynel)val3).Name, 0, 0));
						((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM((ICharacter)(object)val3, "You've been promoted to the rank of " + empty + " by " + ((INamedEntity)client.Controller.Character).Name, 0, 0));
					}
					else
					{
						num3 = ((Dynel)val3).Stats[(StatIds)48].Value;
						int num4 = num3 - 1;
						empty = GetRank(governingForm, (uint)num4);
						((Dynel)val3).Stats[(StatIds)48].Value = num4;
						((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(client.Controller.Character, "You've promoted " + ((Dynel)val3).Name + " to " + empty, 0, 0));
						((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM((ICharacter)(object)val3, "You've been promoted to the rank of " + empty + " by " + ((INamedEntity)client.Controller.Character).Name, 0, 0));
					}
				}
				else
				{
					((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(client.Controller.Character, "Organization does not exist?", 0, 0));
				}
			}
			else
			{
				((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(client.Controller.Character, "Your Rank is not high enough to promote " + ((Dynel)val3).Name, 0, 0));
			}
			break;
		}
		case 11:
		{
			Character val8 = null;
			int num5 = -1;
			int num6 = -1;
			val8 = ((IInstancedEntity)client.Controller.Character).Playfield.FindByIdentity<Character>(message.Target);
			if (val8 == null)
			{
				break;
			}
			if (((Dynel)val8).Stats[(StatIds)5].BaseValue != ((IStats)client.Controller.Character).Stats[(StatIds)5].BaseValue)
			{
				((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(client.Controller.Character, "Target is not in your organization!", 0, 0));
			}
			else if (((IStats)client.Controller.Character).Stats[(StatIds)48].Value <= ((Dynel)val8).Stats[(StatIds)48].Value - 2 || ((IStats)client.Controller.Character).Stats[(StatIds)48].Value == 0)
			{
				DBOrganization val9 = ((Dao<DBOrganization, OrganizationDao>)(object)Dao<DBOrganization, OrganizationDao>.Instance).Get((int)((IStats)client.Controller.Character).Stats[(StatIds)5].BaseValue);
				int governingForm2 = -1;
				string empty2 = string.Empty;
				if (val9 == null)
				{
					((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(client.Controller.Character, "Organization does not exist?", 0, 0));
					break;
				}
				if (num5 + 1 > GetLowestRank(val9.GovernmentForm))
				{
					((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(client.Controller.Character, "You can't demote character any lower!", 0, 0));
					break;
				}
				num5 = ((Dynel)val8).Stats[(StatIds)48].Value;
				num6 = num5 + 1;
				empty2 = GetRank(governingForm2, (uint)num6);
				((Dynel)val8).Stats[(StatIds)48].Value = num6;
				((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(client.Controller.Character, "You've demoted " + ((Dynel)val8).Name + " to " + empty2, 0, 0));
				((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM((ICharacter)(object)val8, "You've been demoted to the rank of " + empty2 + " by " + ((INamedEntity)client.Controller.Character).Name, 0, 0));
			}
			else
			{
				((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(client.Controller.Character, "Your Rank is not high enough to demote " + ((Dynel)val8).Name, 0, 0));
			}
			break;
		}
		case 12:
			Console.WriteLine("Case 12 Started");
			break;
		case 13:
		{
			uint baseValue = ((IStats)client.Controller.Character).Stats[(StatIds)5].BaseValue;
			DBCharacter byCharName = Dao<DBCharacter, CharacterDao>.Instance.GetByCharName(message.CommandArgs);
			if (byCharName == null)
			{
				((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(client.Controller.Character, "No character with name " + message.CommandArgs + " exists.", 0, 0));
				break;
			}
			int id2 = byCharName.Id;
			IPlayfield playfield = client.Playfield;
			val = default(Identity);
			((Identity)(ref val)).Type = (IdentityType)50000;
			((Identity)(ref val)).Instance = id2;
			Character val10 = playfield.FindByIdentity<Character>(val);
			if (val10 == null)
			{
				break;
			}
			uint baseValue2 = ((Dynel)val10).Stats[(StatIds)5].BaseValue;
			if (baseValue2 != ((IStats)client.Controller.Character).Stats[(StatIds)5].BaseValue)
			{
				((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(client.Controller.Character, message.CommandArgs + "is not a member of your organization!", 0, 0));
				break;
			}
			CharacterDao instance4 = Dao<DBCharacter, CharacterDao>.Instance;
			val = ((IEntity)client.Controller.Character).Identity;
			if (instance4.IsOnline(((Identity)(ref val)).Instance) != 0)
			{
				string organizationName = ((Dynel)val10).OrganizationName;
				((Dynel)val10)[(StatIds)48].Value = 0;
				((Dynel)val10)[(StatIds)5].Value = 0;
				((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM((ICharacter)(object)val10, "You've been kicked from the organization " + organizationName, 0, 0));
			}
			break;
		}
		case 14:
		{
			Character val5 = client.Playfield.FindByIdentity<Character>(message.Target);
			if (val5 != null && ((Dynel)val5).Controller.Client != null)
			{
				OrgInviteMessage val6 = new OrgInviteMessage
				{
					Identity = ((PooledObject)val5).Identity,
					Unknown = 0,
					Unknown1 = 0,
					Unknown2 = 0
				};
				val = default(Identity);
				((Identity)(ref val)).Type = (IdentityType)57002;
				((Identity)(ref val)).Instance = ((IStats)client.Controller.Character).Stats[5].Value;
				((OrgServerMessage)val6).Organization = val;
				((OrgServerMessage)val6).OrganizationName = ((IDynel)client.Controller.Character).OrganizationName;
				val6.Unknown3 = 0;
				OrgInviteMessage val7 = val6;
				((Dynel)val5).Controller.Client.SendCompressed((MessageBody)(object)val7);
			}
			break;
		}
		case 15:
		{
			val = message.Target;
			int instance3 = ((Identity)(ref val)).Instance;
			int governmentForm2 = Dao<DBOrganization, OrganizationDao>.Instance.GetGovernmentForm(instance3);
			((IStats)client.Controller.Character).Stats[(StatIds)48].Value = GetLowestRank(governmentForm2);
			((IStats)client.Controller.Character).Stats[(StatIds)5].Value = instance3;
			break;
		}
		case 16:
		{
			int governmentForm = Dao<DBOrganization, OrganizationDao>.Instance.GetGovernmentForm(((IStats)client.Controller.Character).Stats[(StatIds)5].Value);
			if (((IStats)client.Controller.Character).Stats[(StatIds)48].Value == 0 && governmentForm != 4)
			{
				((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(client.Controller.Character, "Organization Leader cannot leave organization without Disbanding or Passing Leadership!", 0, 0));
				break;
			}
			int value = ((IStats)client.Controller.Character).Stats[(StatIds)5].Value;
			string name = ((Dao<DBOrganization, OrganizationDao>)(object)Dao<DBOrganization, OrganizationDao>.Instance).Get(value).Name;
			((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(client.Controller.Character, "You left the organization " + name + ".", 0, 0));
			break;
		}
		case 17:
			if (message.CommandArgs == null)
			{
				((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(client.Controller.Character, "The current organization tax rate is: ", 0, 0));
			}
			break;
		case 18:
			break;
		case 19:
			break;
		case 20:
			break;
		case 21:
			break;
		case 22:
			break;
		case 23:
			break;
		case 24:
			break;
		case 25:
			break;
		case 26:
			break;
		case 27:
			break;
		case 28:
			break;
		}
	}

	internal static int GetLowestRank(int GoverningForm)
	{
		return GoverningForm switch
		{
			0 => 6, 
			1 => 4, 
			2 => 4, 
			3 => 2, 
			4 => 0, 
			5 => 3, 
			_ => 0, 
		};
	}

	private static string ResolveGoverningFormText(int governingForm)
	{
		return governingForm switch
		{
			0 => "Department", 
			1 => "Faction", 
			2 => "Republic", 
			3 => "Monarchy", 
			4 => "Anarchism", 
			5 => "Feudalism", 
			_ => "Department", 
		};
	}

	private static string ResolveLeaderName(int leaderId, Character targetCharacter, int organizationInstance)
	{
		if (leaderId > 0)
		{
			try
			{
				string characterNameById = Dao<DBCharacter, CharacterDao>.Instance.GetCharacterNameById(leaderId);
				if (!string.IsNullOrEmpty(characterNameById))
				{
					return characterNameById;
				}
			}
			catch
			{
			}
		}
		if (targetCharacter != null && !string.IsNullOrEmpty(((Dynel)targetCharacter).Name))
		{
			return ((Dynel)targetCharacter).Name;
		}
		return (organizationInstance == 1970177) ? "Celcius2024" : string.Empty;
	}

	private static DBOrganization ResolveOrganization(int organizationInstance)
	{
		try
		{
			return ((Dao<DBOrganization, OrganizationDao>)(object)Dao<DBOrganization, OrganizationDao>.Instance).Get(organizationInstance);
		}
		catch
		{
			return null;
		}
	}

	private static string ResolveOrganizationName(DBOrganization organization, Character targetCharacter, int organizationInstance)
	{
		if (organization != null && !string.IsNullOrEmpty(organization.Name))
		{
			return organization.Name;
		}
		if (targetCharacter != null && !string.IsNullOrEmpty(((Dynel)targetCharacter).OrganizationName))
		{
			return ((Dynel)targetCharacter).OrganizationName;
		}
		return (organizationInstance == 1970177) ? "Est. 2024" : string.Empty;
	}

	private static int ResolveCharacterStatValue(IInstancedEntity entity, StatIds statId)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		if (entity == null)
		{
			return 0;
		}
		uint baseValue = ((IStats)entity).Stats[statId].BaseValue;
		if (baseValue != 0)
		{
			return (int)baseValue;
		}
		return ((IStats)entity).Stats[statId].Value;
	}

	internal static string GetRank(int GoverningForm, uint Rank)
	{
		string[] array = new string[7] { "President", "General", "Squad Commander", "Unit Commander", "Unit Leader", "Unit Member", "Applicant" };
		string[] array2 = new string[5] { "Director", "Board Member", "Executive", "Member", "Applicant" };
		string[] array3 = new string[5] { "President", "Advisor", "Veteran", "Member", "Applicant" };
		string[] array4 = new string[3] { "Monarch", "Council", "Follower" };
		string[] array5 = new string[1] { "Anarchist" };
		string[] array6 = new string[4] { "Lord", "Knight", "Vassal", "Peasant" };
		switch (GoverningForm)
		{
		case 0:
			if (Rank > 6)
			{
				return string.Empty;
			}
			return array[Rank];
		case 1:
			if (Rank > 4)
			{
				return string.Empty;
			}
			return array2[Rank];
		case 2:
			if (Rank > 4)
			{
				return string.Empty;
			}
			return array3[Rank];
		case 3:
			if (Rank > 2)
			{
				return string.Empty;
			}
			return array4[Rank];
		case 4:
			if (Rank != 0)
			{
				return string.Empty;
			}
			return array5[Rank];
		case 5:
			if (Rank > 3)
			{
				return string.Empty;
			}
			return array6[Rank];
		default:
			return string.Empty;
		}
	}

	internal static string GetRankList(int GoverningForm)
	{
		string text = "President, General, Squad Commander, Unit Commander, Unit Leader, Unit Member, Applicant";
		string text2 = "Director, Board Member, Executive, Member, Applicant";
		string text3 = "President, Advisor, Veteran, Member, Applicant";
		string text4 = "Monarch, Council, Follower";
		string text5 = "Anarchist";
		string text6 = "Lord, Knight, Vassal, Peasant";
		return GoverningForm switch
		{
			0 => text, 
			1 => text2, 
			2 => text3, 
			3 => text4, 
			4 => text5, 
			5 => text6, 
			_ => string.Empty, 
		};
	}
}
