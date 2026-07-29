using System;
using System.Data;
using System.Linq;
using System.Text;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
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
using SmokeLounge.AOtomation.Messaging.Messages.SystemMessages;
using Utility;
using ZoneEngine.Core.Arete.Quests;
using ZoneEngine.Core.GMI;
using ZoneEngine.Core.InternalMessages;
using ZoneEngine.Core.Mail;
using ZoneEngine.Core.MessageHandlers;
using ZoneEngine.Core.Missions;
using ZoneEngine.Core.Packets;
using ZoneEngine.Core.Perks;
using ZoneEngine.Core.Playfields;
using ZoneEngine.Core.Thrak.Quests;
using ZoneEngine.Script;

namespace ZoneEngine.Core.PacketHandlers;

public class ClientConnected
{
	public static byte[] StrToByteArray(string str)
	{
		ASCIIEncoding aSCIIEncoding = new ASCIIEncoding();
		return aSCIIEncoding.GetBytes(str);
	}

	public void Read(int charID, ZoneClient client)
	{
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0202: Unknown result type (might be due to invalid IL or missing references)
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		//IL_0209: Unknown result type (might be due to invalid IL or missing references)
		//IL_0214: Unknown result type (might be due to invalid IL or missing references)
		//IL_0220: Unknown result type (might be due to invalid IL or missing references)
		//IL_022c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0239: Expected O, but got Unknown
		//IL_0292: Unknown result type (might be due to invalid IL or missing references)
		//IL_0299: Expected O, but got Unknown
		//IL_029c: Unknown result type (might be due to invalid IL or missing references)
		//IL_03db: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0401: Unknown result type (might be due to invalid IL or missing references)
		//IL_040e: Expected O, but got Unknown
		//IL_0410: Unknown result type (might be due to invalid IL or missing references)
		//IL_0415: Unknown result type (might be due to invalid IL or missing references)
		//IL_0421: Unknown result type (might be due to invalid IL or missing references)
		//IL_042d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0439: Unknown result type (might be due to invalid IL or missing references)
		//IL_0446: Expected O, but got Unknown
		//IL_0448: Unknown result type (might be due to invalid IL or missing references)
		//IL_044d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0459: Unknown result type (might be due to invalid IL or missing references)
		//IL_0465: Unknown result type (might be due to invalid IL or missing references)
		//IL_0471: Unknown result type (might be due to invalid IL or missing references)
		//IL_047e: Expected O, but got Unknown
		//IL_047f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0484: Unknown result type (might be due to invalid IL or missing references)
		//IL_0486: Unknown result type (might be due to invalid IL or missing references)
		//IL_0491: Unknown result type (might be due to invalid IL or missing references)
		//IL_049a: Expected O, but got Unknown
		client.PacketSequencing.BeginSessionReadyBlock(client.SessionLifecycle.EnterReadyBlockForSessionInit);
		ServerBase server = ((ClientBase)client).Server;
		ZoneClient zoneClient = client;
		object[] array = new object[3];
		Identity val = ((IEntity)client.Controller.Character).Identity;
		array[0] = ((Identity)(ref val)).Instance;
		array[1] = ((ClientBase)client).ClientAddress;
		array[2] = ((INamedEntity)client.Controller.Character).Name;
		server.Info((IClient)(object)zoneClient, "Client connected. ID: {0} IP: {1} Character name: {2}", array);
		ActiveNanoRuntimeService.Default.PrepareCharacterForLogin(client.Controller.Character);
		MailRuntimeService.SyncUnreadMailEnvelope(client.Controller.Character);
		GridZoneInDiagnostics.BeginGridZoneIn(client);
		BaseMessageHandler<ChatServerInfoMessage, ChatServerInfoMessageHandler>.Default.Send(client.Controller.Character);
		WorldEntrySummary.Begin(client, "zone_login");
		BaseMessageHandler<PlayfieldAnarchyFMessage, PlayfieldAnarchyFMessageHandler>.Default.Send(client.Controller.Character);
		MissionInstanceDoorReplay.SendForCharacter((IZoneClient)(object)client, client.Controller.Character);
		foreach (Vendor item in Pool.Instance.GetAll<Vendor>(((IEntity)((IInstancedEntity)client.Controller.Character).Playfield).Identity, 51035))
		{
			BaseMessageHandler<VendingMachineFullUpdateMessage, VendingMachineFullUpdateMessageHandler>.Default.Send(client.Controller.Character, item);
		}
		((IStats)client.Controller.Character).Stats[(StatIds)521].BaseValue = 4u;
		val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)50000;
		((Identity)(ref val)).Instance = charID;
		Identity identity = val;
		GameTimeMessage messageBody = new GameTimeMessage
		{
			Identity = identity,
			Unknown1 = 30024f,
			Unknown3 = 185408,
			Unknown4 = 80183.31f
		};
		client.SendCompressed((MessageBody)(object)messageBody);
		client.LastGameTimeSyncUtc = DateTime.UtcNow;
		InitializeActionableState(client);
		SendActionableState(client);
		BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.SendSkillAvailable(client.Controller.Character, 124);
		ZoneClient zoneClient2 = client;
		StatMessage val2 = new StatMessage();
		((N3Message)val2).Identity = identity;
		val2.Stats = new GameTuple<CharacterStat, uint>[1]
		{
			new GameTuple<CharacterStat, uint>
			{
				Value1 = (CharacterStat)521,
				Value2 = (uint)((IStats)client.Controller.Character).Stats[(StatIds)521].Value
			}
		};
		zoneClient2.SendCompressed((MessageBody)(object)val2);
		Playfield currentPlayfield = null;
		client.Controller.Character.CalculateSkills();
		SyncVitalStats(client.Controller.Character);
		client.PacketSequencing.RunSessionReadyFullCharacterSequence(delegate
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			PlayfieldLifecycleTrace.Record("private-city-ready-init", "private-city-ready-block-begin", "PrivateCityReadyBlockBegin", identity);
		}, delegate
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			PlayfieldLifecycleTrace.Record("private-city-ready-init", "private-city-simple-char-full-update-broadcast", "SimpleCharFullUpdate", identity);
		}, delegate
		{
			SimpleCharFullUpdate.SendToPlayfield((IZoneClient)(object)client);
		}, delegate
		{
			GuestKeyGeneratorInteractionHandler.ProcessCityAccessCardLifetimes(client.Controller.Character);
			WeaponItemFullUpdate.SendWeaponDefinitions(client.Controller.Character);
			currentPlayfield = ((IInstancedEntity)client.Controller.Character).Playfield as Playfield;
		}, delegate
		{
			if (currentPlayfield != null)
			{
				currentPlayfield.SendPrivateCityPreFullCharacterReadyBlock(client, client.Controller.Character);
			}
		}, delegate
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			PlayfieldLifecycleTrace.Record("private-city-ready-init", "private-city-full-character", "FullCharacter", identity);
		}, client.SessionLifecycle.EnterFullCharacterBoundaryForSessionInit, delegate
		{
			CombatXpRuntimeService.LogXpWireSnapshot(client.Controller.Character, "ClientConnected", "zone-login-before-prepare");
			CombatXpRuntimeService.PrepareXpStatsForLogin(client.Controller.Character);
			CombatXpRuntimeService.LogXpWireSnapshot(client.Controller.Character, "ClientConnected", "zone-login-after-prepare-before-fullchar");
			BaseMessageHandler<FullCharacterMessage, FullCharacterMessageHandler>.Default.Send(client.Controller.Character);
			CombatXpRuntimeService.SyncXpBarStatsOnLogin(client.Controller.Character);
			CombatXpRuntimeService.LogXpWireSnapshot(client.Controller.Character, "ClientConnected", "zone-login-after-fullchar");
			ICharacter character = client.Controller.Character;
			Character val3 = (Character)(object)((character is Character) ? character : null);
			if (val3 != null)
			{
				PerkRuntimeService.Default.ResendPerkActions(val3);
			}
			MissionAcceptService.TryResendForLogin(client.Controller.Character);
			ThrakGardenKeyQuestRuntime.TryResendActiveMissionsForLogin(client.Controller.Character);
			RexMarcusChainCoordinator.TryResendActiveTipsForLogin(client.Controller.Character);
			ThrakGardenKeyQuestRuntime.TryRestoreGardenKeyIfMissing(client.Controller.Character);
			GmiRuntimeService.ProcessPendingWithdrawals(client.Controller.Character);
			MailRuntimeService.SyncUnreadMailEnvelope(client.Controller.Character);
		}, delegate
		{
			if (currentPlayfield != null)
			{
				currentPlayfield.SendPrivateCityPlayfieldReadyBlock(client, client.Controller.Character);
			}
		}, delegate
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			PlayfieldLifecycleTrace.Record("private-city-ready-init", "private-city-ready-block-end", "PrivateCityReadyBlockEnd", identity);
		});
		ActiveNanoRuntimeService.Default.SchedulePostLoginNanoRestore((IZoneClient)(object)client);
		SpecialAttack[] specials = (SpecialAttack[])(object)new SpecialAttack[3]
		{
			new SpecialAttack
			{
				Unknown1 = 43712,
				Unknown2 = 144745,
				Unknown3 = 100,
				Unknown4 = "MAAT"
			},
			new SpecialAttack
			{
				Unknown1 = 42033,
				Unknown2 = 42032,
				Unknown3 = 144,
				Unknown4 = "DIIT"
			},
			new SpecialAttack
			{
				Unknown1 = 70292,
				Unknown2 = 70293,
				Unknown3 = 142,
				Unknown4 = "BRAW"
			}
		};
		SpecialAttackWeaponMessage messageBody2 = new SpecialAttackWeaponMessage
		{
			Identity = identity,
			Specials = specials
		};
		client.SendCompressed((MessageBody)(object)messageBody2);
		WorldEntrySummary.Complete(client);
		client.Controller.Character.CalculateSkills();
		SyncVitalStats(client.Controller.Character);
		InventoryContainerRuntimeService.Default.EnsureWeaponVisualMeshes(client.Controller.Character, announceAppearanceUpdate: false);
		if (currentPlayfield != null)
		{
			client.PacketSequencing.RunVisibilityInitializationSequence(delegate
			{
				//IL_001f: Unknown result type (might be due to invalid IL or missing references)
				PlayfieldLifecycleTrace.Record("same-playfield-visibility", "visibility-joiner-ready", "ClientConnected", ((IEntity)client.Controller.Character).Identity);
			}, client.SessionLifecycle.EnterCharInPlayForVisibilityEntry, delegate
			{
				currentPlayfield.AnnouncePlayerVisibility(client.Controller.Character);
			}, delegate
			{
				currentPlayfield.SendSCFUsToClient(new IMSendPlayerSCFUs
				{
					toClient = (IZoneClient)(object)client
				});
				currentPlayfield.SendStaticDynelsToClient(client.Controller.Character);
			});
		}
		BaseMessageHandler<AppearanceUpdateMessage, AppearanceUpdateMessageHandler>.Default.Send(client.Controller.Character);
		CompleteDeathRespawnCharInPlay(client);
		SendAliveDeadTimerBaseline(client);
		ScriptCompiler.Instance.CallMethod("OnConnect", client.Controller.Character);
		client.PacketSequencing.CompleteSessionInitialization(client.SessionLifecycle.CompleteInPlayForSessionInit);
		((IInstancedEntity)client.Controller.Character).DoNotDoTimers = false;
	}

	private static void CompleteDeathRespawnCharInPlay(ZoneClient client)
	{
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Expected O, but got Unknown
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		ICharacter character = client.Controller.Character;
		if (((IStats)character).Stats[(StatIds)27].Value > 0 && ((IStats)character).Stats[(StatIds)34].Value == 75)
		{
			((IInstancedEntity)character).Starting = false;
			client.SendCompressed((MessageBody)new CharInPlayMessage
			{
				Identity = ((IEntity)character).Identity,
				Unknown = 0
			});
			LogUtil.Debug((DebugInfoDetail)4, $"Death respawn CharInPlay completion sent target={((IEntity)character).Identity} unknown=0 hp={((IStats)character).Stats[(StatIds)27].Value}/{((IStats)character).Stats[(StatIds)1].Value} deadTimer={((IStats)character).Stats[(StatIds)34].Value}");
		}
	}

	private static void InitializeActionableState(ZoneClient client)
	{
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0210: Unknown result type (might be due to invalid IL or missing references)
		//IL_0215: Unknown result type (might be due to invalid IL or missing references)
		//IL_0235: Unknown result type (might be due to invalid IL or missing references)
		//IL_023a: Unknown result type (might be due to invalid IL or missing references)
		bool preserveLogoutSitOnConnect = client.PreserveLogoutSitOnConnect;
		SetStat(client, (StatIds)7, 0);
		Identity identity;
		if (preserveLogoutSitOnConnect)
		{
			client.Controller.Character.EnterLogoutSitPosture();
			client.PreserveLogoutSitOnConnect = false;
		}
		else
		{
			ICharacter character2 = client.Controller.Character;
			Character val = (Character)(object)((character2 is Character) ? character2 : null);
			if (val != null)
			{
				val.UpdateMoveType((byte)25);
			}
			SetStat(client, (StatIds)173, 3);
			SetStat(client, (StatIds)174, 3);
			identity = ((IEntity)client.Controller.Character).Identity;
			UpsertCharacterStat(((Identity)(ref identity)).Instance, (StatIds)173, 3);
			identity = ((IEntity)client.Controller.Character).Identity;
			UpsertCharacterStat(((Identity)(ref identity)).Instance, (StatIds)174, 3);
		}
		ICharacter character = client.Controller.Character;
		DBCharacter val2 = ((Dao<DBCharacter, CharacterDao>)(object)Dao<DBCharacter, CharacterDao>.Instance).GetAll((object)new { }).FirstOrDefault(delegate(DBCharacter c)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			int id = c.Id;
			Identity identity2 = ((IEntity)character).Identity;
			return id == ((Identity)(ref identity2)).Instance;
		});
		if (val2 == null)
		{
			identity = ((IEntity)character).Identity;
			Console.WriteLine($"[GM/EXP DEBUG] Character NOT FOUND ID={((Identity)(ref identity)).Instance}");
			return;
		}
		DBLoginData byUsername = Dao<DBLoginData, LoginDataDao>.Instance.GetByUsername(val2.Username);
		if (byUsername == null)
		{
			Console.WriteLine("[GM/EXP DEBUG] LOGIN NOT FOUND for " + val2.Username);
			return;
		}
		identity = ((IEntity)character).Identity;
		Console.WriteLine($"[GM/EXP DEBUG] CharacterID = {((Identity)(ref identity)).Instance}");
		Console.WriteLine("[GM/EXP DEBUG] Username = " + val2.Username);
		Console.WriteLine($"[GM/EXP DEBUG] GM = {byUsername.GM}");
		Console.WriteLine($"[GM/EXP DEBUG] EXP = {byUsername.Expansions}");
		SetStat(client, (StatIds)215, byUsername.GM);
		SetStat(client, (StatIds)389, byUsername.Expansions | 2);
		identity = ((IEntity)character).Identity;
		UpsertCharacterStat(((Identity)(ref identity)).Instance, (StatIds)215, byUsername.GM);
		identity = ((IEntity)character).Identity;
		UpsertCharacterStat(((Identity)(ref identity)).Instance, (StatIds)389, byUsername.Expansions | 2);
		ApplyGmNametagFlags(client, byUsername.GM);
		ApplyGmMaxHp(client, byUsername.GM);
		client.Controller.SendChangedStats();
		SetStat(client, (StatIds)423, 0);
		SetStat(client, (StatIds)430, 0);
		SetStat(client, (StatIds)521, 4);
		SetStat(client, (StatIds)348, 3);
		SetStat(client, (StatIds)588, 0);
	}

	private static void SyncVitalStats(ICharacter character)
	{
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_0179: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		int num = Math.Max(1, ((IStats)character).Stats[(StatIds)1].Value);
		int num2 = Math.Max(0, ((IStats)character).Stats[(StatIds)221].Value);
		Identity identity;
		if (((IInstancedEntity)character).Starting)
		{
			((IStats)character).Stats[(StatIds)27].Value = num;
			((IStats)character).Stats[(StatIds)27].BaseValue = (uint)num;
			((IStats)character).Stats[(StatIds)214].Value = num2;
			((IStats)character).Stats[(StatIds)214].BaseValue = (uint)num2;
			identity = ((IEntity)character).Identity;
			UpsertCharacterStat(((Identity)(ref identity)).Instance, (StatIds)27, num);
			identity = ((IEntity)character).Identity;
			UpsertCharacterStat(((Identity)(ref identity)).Instance, (StatIds)214, num2);
			return;
		}
		if (((IStats)character).Stats[(StatIds)27].Value > num)
		{
			((IStats)character).Stats[(StatIds)27].Value = num;
			((IStats)character).Stats[(StatIds)27].BaseValue = (uint)num;
			identity = ((IEntity)character).Identity;
			UpsertCharacterStat(((Identity)(ref identity)).Instance, (StatIds)27, num);
		}
		if (((IStats)character).Stats[(StatIds)214].Value > num2)
		{
			((IStats)character).Stats[(StatIds)214].Value = num2;
			((IStats)character).Stats[(StatIds)214].BaseValue = (uint)num2;
			identity = ((IEntity)character).Identity;
			UpsertCharacterStat(((Identity)(ref identity)).Instance, (StatIds)214, num2);
		}
	}

	private static void SetStat(ZoneClient client, StatIds stat, int value)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		((IStats)client.Controller.Character).Stats[stat].Value = value;
		((IStats)client.Controller.Character).Stats[stat].BaseValue = (uint)value;
	}

	private static void ApplyGmNametagFlags(ZoneClient client, int gmLevel)
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		if (client != null && client.Controller != null && client.Controller.Character != null && gmLevel > 0)
		{
			ICharacter character = client.Controller.Character;
			int value = ((IStats)character).Stats[(StatIds)0].Value;
			int num = (value & -268435457) | 0x800000;
			if (num != value)
			{
				SetStat(client, (StatIds)0, num);
				Identity identity = ((IEntity)character).Identity;
				UpsertCharacterStat(((Identity)(ref identity)).Instance, (StatIds)0, num);
			}
		}
	}

	private static void ApplyGmMaxHp(ZoneClient client, int gmLevel)
	{
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		if (client != null && client.Controller != null && client.Controller.Character != null && gmLevel > 0)
		{
			ICharacter character = client.Controller.Character;
			SetStat(client, (StatIds)27, 2000000000);
			Identity identity = ((IEntity)character).Identity;
			UpsertCharacterStat(((Identity)(ref identity)).Instance, (StatIds)27, 2000000000);
		}
	}

	private static void UpsertCharacterStat(int characterId, StatIds statId, int value)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Expected I4, but got Unknown
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Expected I4, but got Unknown
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Expected O, but got Unknown
		DBStats val = ((Dao<DBStats, StatDao>)(object)Dao<DBStats, StatDao>.Instance).GetAll((object)new
		{
			Type = 50000,
			Instance = characterId,
			StatId = (int)statId
		}).FirstOrDefault();
		if (val == null)
		{
			((Dao<DBStats, StatDao>)(object)Dao<DBStats, StatDao>.Instance).Add(new DBStats
			{
				Type = 50000,
				Instance = characterId,
				StatId = (int)statId,
				StatValue = value
			}, (IDbConnection)null, (IDbTransaction)null, true);
		}
		else
		{
			val.StatValue = value;
			((Dao<DBStats, StatDao>)(object)Dao<DBStats, StatDao>.Instance).Save(val, (object)null, (IDbConnection)null, (IDbTransaction)null);
		}
	}

	private static void SendAliveDeadTimerBaseline(ZoneClient client)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Expected O, but got Unknown
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		ICharacter character = client.Controller.Character;
		if (((IStats)character).Stats[(StatIds)27].Value > 0)
		{
			SetStat(client, (StatIds)34, 75);
			StatMessage val = new StatMessage();
			((N3Message)val).Identity = ((IEntity)character).Identity;
			((N3Message)val).Unknown = 1;
			val.Stats = new GameTuple<CharacterStat, uint>[1]
			{
				new GameTuple<CharacterStat, uint>
				{
					Value1 = (CharacterStat)34,
					Value2 = (uint)((IStats)character).Stats[(StatIds)34].Value
				}
			};
			client.SendCompressed((MessageBody)(object)val);
		}
	}

	private static void SendActionableState(ZoneClient client)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected O, but got Unknown
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		ICharacter character = client.Controller.Character;
		StatMessage val = new StatMessage();
		((N3Message)val).Identity = ((IEntity)character).Identity;
		((N3Message)val).Unknown = 1;
		val.Stats = new GameTuple<CharacterStat, uint>[1]
		{
			new GameTuple<CharacterStat, uint>
			{
				Value1 = (CharacterStat)7,
				Value2 = (uint)((IStats)character).Stats[(StatIds)7].Value
			}
		};
		client.SendCompressed((MessageBody)(object)val);
	}
}
