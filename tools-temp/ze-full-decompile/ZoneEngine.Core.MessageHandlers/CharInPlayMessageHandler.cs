using System.Collections.Generic;
using System.Threading;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Core.Playfields;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.Arete.Quests;
using ZoneEngine.Core.GMI;
using ZoneEngine.Core.Mail;
using ZoneEngine.Core.Missions;
using ZoneEngine.Core.Packets;
using ZoneEngine.Core.Perks;
using ZoneEngine.Core.Playfields;
using ZoneEngine.Core.Thrak.Quests;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class CharInPlayMessageHandler : BaseMessageHandler<CharInPlayMessage, CharInPlayMessageHandler>
{
	public CharInPlayMessageHandler()
	{
		base.UpdateCharacterStatsOnReceive = true;
	}

	protected override void Read(CharInPlayMessage message, IZoneClient client)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Expected O, but got Unknown
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0300: Unknown result type (might be due to invalid IL or missing references)
		//IL_033d: Unknown result type (might be due to invalid IL or missing references)
		//IL_03aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0508: Unknown result type (might be due to invalid IL or missing references)
		//IL_050d: Unknown result type (might be due to invalid IL or missing references)
		//IL_057d: Unknown result type (might be due to invalid IL or missing references)
		PlayfieldLifecycleTrace.Record("same-playfield-visibility", "char-in-play-received", "CharInPlay", ((IEntity)client.Controller.Character).Identity);
		LogUtil.Debug((DebugInfoDetail)16, $"Client CharInPlay received character={((IEntity)client.Controller.Character).Identity} unknown={((N3Message)message).Unknown}");
		((IInstancedEntity)client.Controller.Character).DoNotDoTimers = true;
		Thread.Sleep(1000);
		CharInPlayMessage val = new CharInPlayMessage
		{
			Identity = ((IEntity)client.Controller.Character).Identity,
			Unknown = 0
		};
		PlayfieldLifecycleTrace.Record("same-playfield-visibility", "char-in-play-announce", "CharInPlay", ((IEntity)client.Controller.Character).Identity);
		((IInstancedEntity)client.Controller.Character).Playfield.Announce((MessageBody)(object)val);
		((IInstancedEntity)client.Controller.Character).Starting = false;
		PlayfieldLifecycleTrace.Record("same-playfield-visibility", "char-in-play-ready", "CharacterReady", ((IEntity)client.Controller.Character).Identity);
		((IStats)client.Controller.Character).Stats.ClearChangedFlags();
		((IStats)client.Controller.Character).Stats[(StatIds)215].Value = ((IStats)client.Controller.Character).Stats[(StatIds)215].Value;
		((IStats)client.Controller.Character).Stats[(StatIds)389].Value = ((IStats)client.Controller.Character).Stats[(StatIds)389].Value;
		((IStats)client.Controller.Character).Stats[(StatIds)342].Value = 0;
		((IStats)client.Controller.Character).Stats[(StatIds)343].Value = 0;
		((IStats)client.Controller.Character).Stats[(StatIds)363].Value = 0;
		((IStats)client.Controller.Character).Stats[(StatIds)364].Value = 0;
		((IStats)client.Controller.Character).Stats[(StatIds)53].Value = 0;
		MailRuntimeService.SyncUnreadMailEnvelope(client.Controller.Character);
		client.Controller.SendChangedStats();
		foreach (WeatherEntry weather in WeatherSettings.Instance.WeatherList)
		{
			BaseMessageHandler<WeatherControlMessage, WeatherControlMessageHandler>.Default.Send(client.Controller.Character, weather);
		}
		List<StaticDynel> list = new List<StaticDynel>(Pool.Instance.GetAll<StaticDynel>(((IEntity)((IInstancedEntity)client.Controller.Character).Playfield).Identity));
		ServerBase server = ((IClient)client).Server;
		object[] array = new object[2];
		Identity identity = ((IEntity)((IInstancedEntity)client.Controller.Character).Playfield).Identity;
		array[0] = ((Identity)(ref identity)).Instance;
		array[1] = list.Count;
		server.Info((IClient)(object)client, "StaticDynelSnapshot pf={0} count={1}", array);
		PlayfieldLifecycleTrace.Record("same-playfield-visibility", "static-dynel-snapshot", "StaticDynelSnapshot", ((IEntity)client.Controller.Character).Identity);
		foreach (StaticDynel item in list)
		{
			BaseMessageHandler<SimpleItemFullUpdateMessage, SimpleItemFullUpdateMessageHandler>.Default.Send(client.Controller.Character, item);
		}
		PlayfieldLifecycleTrace.Record("same-playfield-visibility", "weapon-definitions", "WeaponItemFullUpdate", ((IEntity)client.Controller.Character).Identity);
		WeaponItemFullUpdate.SendWeaponDefinitions(client.Controller.Character);
		InventoryContainerRuntimeService.Default.PublishMailBlockedContainerLinks(client.Controller.Character);
		Playfield.ArmPostZoneCollisionGrace(client.Controller.Character);
		MailRuntimeService.SyncUnreadMailEnvelope(client.Controller.Character);
		GmiRuntimeService.ProcessPendingWithdrawals(client.Controller.Character);
		ICharacter character = client.Controller.Character;
		Character val2 = (Character)(object)((character is Character) ? character : null);
		if (val2 != null)
		{
			val2.ReloadTrainedPerksFromDatabase();
			PerkRuntimeService.Default.ResendPerkActions(val2);
		}
		bool flag = MissionAcceptService.TryResendForLogin(client.Controller.Character);
		bool flag2 = ThrakGardenKeyQuestRuntime.TryResendActiveMissionsForLogin(client.Controller.Character);
		bool flag3 = RexMarcusChainCoordinator.TryResendActiveTipsForLogin(client.Controller.Character);
		ThrakGardenKeyQuestRuntime.TryRestoreGardenKeyIfMissing(client.Controller.Character);
		int num;
		if (((IInstancedEntity)client.Controller.Character).Playfield == null)
		{
			num = 0;
		}
		else
		{
			identity = ((IEntity)((IInstancedEntity)client.Controller.Character).Playfield).Identity;
			num = ((Identity)(ref identity)).Instance;
		}
		int num2 = num;
		((IClient)client).Server.Info((IClient)(object)client, "CharInPlay mission-window resync resent={0} thrak={1} areteTips={2}", new object[3] { flag, flag2, flag3 });
		object[] array2 = new object[4];
		identity = ((IEntity)client.Controller.Character).Identity;
		array2[0] = ((Identity)(ref identity)).Instance;
		array2[1] = num2;
		array2[2] = flag;
		array2[3] = flag2;
		MissionDiagnostics.Log("CHARINPLAY char={0} pf={1} windowResent={2} thrakResent={3}", array2);
		MissionInstanceDoorReplay.SendForCharacter(client, client.Controller.Character);
		((IInstancedEntity)client.Controller.Character).DoNotDoTimers = false;
		PlayfieldLifecycleTrace.Record("same-playfield-visibility", "timers-enabled", "TimersEnabled", ((IEntity)client.Controller.Character).Identity);
	}
}
