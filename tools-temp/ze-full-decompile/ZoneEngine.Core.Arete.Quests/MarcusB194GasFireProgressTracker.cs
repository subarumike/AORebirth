using System;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Inventory;
using AORebirth.Core.Items;
using AORebirth.Core.Network;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.MessageHandlers;
using ZoneEngine.Core.Missions;

namespace ZoneEngine.Core.Arete.Quests;

public static class MarcusB194GasFireProgressTracker
{
	private const int AreteLandingPlayfieldId = 6553;

	private const int GasFireTemplateId = 295883;

	private const int CompactFireSuppressantItemId = 296780;

	private const string MissionId = "Mission:5514B194";

	private const string ObjectiveId = "mission_5514b194_objective_questfullupdate";

	private const string ExtinguishFeedback = "~&!!!\":!!!)<s\u001dYou extinguish the Gas Fire.";

	public static bool TryHandleUseItemOnItem(IZoneClient client, GenericCmdMessage message)
	{
		//IL_045c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Invalid comparison between Unknown and I4
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Unknown result type (might be due to invalid IL or missing references)
		//IL_022f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0234: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_039a: Unknown result type (might be due to invalid IL or missing references)
		//IL_039f: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d5: Expected O, but got Unknown
		if (client == null || message == null || message.Target == null || message.Target.Length < 2)
		{
			return false;
		}
		if (UseItemOnItemInteractionRules.ResolveRouteMode(message.Action) != UseItemOnItemInteractionRouteMode.UseItemOnItem)
		{
			return false;
		}
		ICharacter val = ((client.Controller != null) ? client.Controller.Character : null);
		if (val != null && ((IInstancedEntity)val).Playfield != null)
		{
			Identity identity = ((IEntity)((IInstancedEntity)val).Playfield).Identity;
			if (((Identity)(ref identity)).Instance == 6553 && ((IDynel)val).Controller is PlayerController)
			{
				Identity itemIdentity = message.Target[0];
				Identity val2 = message.Target[1];
				if ((int)((Identity)(ref val2)).Type != 51005)
				{
					return false;
				}
				StaticDynel @object = Pool.Instance.GetObject<StaticDynel>(((IEntity)((IInstancedEntity)val).Playfield).Identity, val2);
				if (@object == null || !IsGasFire(@object))
				{
					return false;
				}
				IItem val3 = ResolveInventoryItem(val, itemIdentity);
				if (val3 == null || !IsSuppressant(val3))
				{
					return false;
				}
				if (!MissionRuntime.IsInitialized)
				{
					return false;
				}
				PersistentMissionService service = MissionRuntime.Service;
				identity = ((IEntity)val).Identity;
				ZoneEngine.Core.Missions.MissionStateRecord mission = service.GetMission(((Identity)(ref identity)).Instance, "Mission:5514B194");
				PersistentMissionService service2 = MissionRuntime.Service;
				identity = ((IEntity)val).Identity;
				ZoneEngine.Core.Missions.MissionStateRecord mission2 = service2.GetMission(((Identity)(ref identity)).Instance, "Mission:5514B196");
				bool flag = mission != null && (mission.State == MissionLifecycleState.Active || mission.State == MissionLifecycleState.Completed || mission.State == MissionLifecycleState.Offered);
				bool flag2 = mission2 != null && (mission2.State == MissionLifecycleState.Active || mission2.State == MissionLifecycleState.Completed || mission2.State == MissionLifecycleState.Offered);
				if (!flag && !flag2 && InventoryContainerRuntimeService.Default.CountCharacterItemInCarriedInventory(val, 296780) <= 0)
				{
					return false;
				}
				BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.Acknowledge(val, message);
				try
				{
					identity = ((IEntity)val).Identity;
					int instance = ((Identity)(ref identity)).Instance;
					if (mission != null && mission.State == MissionLifecycleState.Offered)
					{
						MissionRuntime.Service.AcceptMission(instance, "Mission:5514B194");
						mission = MissionRuntime.Service.GetMission(instance, "Mission:5514B194");
					}
					if (mission != null && mission.State == MissionLifecycleState.Active)
					{
						PersistentMissionService service3 = MissionRuntime.Service;
						MissionObjectiveObservation obj = new MissionObjectiveObservation
						{
							CharacterId = instance,
							QuestId = "Mission:5514B194",
							ObjectiveId = "mission_5514b194_objective_questfullupdate",
							ObservationKey = "gas-fire:" + ((Identity)(ref val2)).ToString(true),
							Amount = 1,
							EventType = "GenericCmd:UseItemOnItem"
						};
						identity = ((IEntity)val).Identity;
						obj.SourceIdentity = ((Identity)(ref identity)).ToString(true);
						obj.TargetIdentity = ((Identity)(ref val2)).ToString(true);
						service3.ObserveObjective(obj);
					}
					MissionOperationResult missionOperationResult = MissionRuntime.Service.CompleteAndActivateNextMission(instance, "Mission:5514B194", "Mission:5514B196");
					if (missionOperationResult.Status != MissionOperationStatus.Applied && missionOperationResult.Status != MissionOperationStatus.AlreadyApplied)
					{
						MissionRuntime.Service.CompleteMission(instance, "Mission:5514B194");
						MissionRuntime.Service.OfferMission(instance, "Mission:5514B196");
						MissionRuntime.Service.AcceptMission(instance, "Mission:5514B196");
					}
					try
					{
						((IDynel)val).Controller.Client.SendCompressed((MessageBody)new FormatFeedbackMessage
						{
							Identity = ((IEntity)val).Identity,
							Unknown = 1,
							Unknown1 = 0,
							FormattedMessage = "~&!!!\":!!!)<s\u001dYou extinguish the Gas Fire.",
							Unknown2 = 0
						});
					}
					catch (Exception ex)
					{
						LogUtil.Debug((DebugInfoDetail)512, "ARETE_MARCUS_B194 extinguish feedback failed: " + ex.Message);
					}
					RexQuestPreviewEmissionResult rexQuestPreviewEmissionResult = SafeQuestFullUpdateSender.TrySendB194ToB196Handoff(val);
					if (rexQuestPreviewEmissionResult == null || !rexQuestPreviewEmissionResult.Emitted)
					{
						LogUtil.Debug((DebugInfoDetail)512, "ARETE_MARCUS_B194 B194→B196 handoff failed: " + ((rexQuestPreviewEmissionResult == null) ? "null" : rexQuestPreviewEmissionResult.Message));
						SafeQuestFullUpdateSender.TrySendB194QuestDelete(val);
						SafeQuestFullUpdateSender.TrySendB196Preview(val);
					}
				}
				finally
				{
					try
					{
						((IInstancedEntity)val).Playfield.Despawn(val2);
					}
					catch (Exception ex2)
					{
						LogUtil.Debug((DebugInfoDetail)512, "ARETE_MARCUS_B194 gas fire despawn failed: " + ex2.Message);
					}
				}
				string[] obj2 = new string[6] { "ARETE_MARCUS_B194 gas fire extinguished character=", null, null, null, null, null };
				identity = ((IEntity)val).Identity;
				obj2[1] = ((Identity)(ref identity)).ToString(true);
				obj2[2] = " fire=";
				obj2[3] = ((Identity)(ref val2)).ToString(true);
				obj2[4] = " item=";
				obj2[5] = 296780.ToString();
				LogUtil.Debug((DebugInfoDetail)128, string.Concat(obj2));
				return true;
			}
		}
		return false;
	}

	private static bool IsGasFire(StaticDynel fire)
	{
		if (fire == null)
		{
			return false;
		}
		if (fire.Template != null && fire.Template.ID == 295883)
		{
			return true;
		}
		if (fire.Stats != null && (fire.Stats.TryGetValue(702, out var value) || fire.Stats.TryGetValue(23, out value)))
		{
			return value == 295883;
		}
		return false;
	}

	private static bool IsSuppressant(IItem item)
	{
		return item != null && (item.HighID == 296780 || item.LowID == 296780);
	}

	private static IItem ResolveInventoryItem(ICharacter character, Identity itemIdentity)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Expected I4, but got Unknown
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Expected I4, but got Unknown
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		if (((IItemContainer)character).BaseInventory != null && ((IItemContainer)character).BaseInventory.Pages.TryGetValue((int)((Identity)(ref itemIdentity)).Type, out var value) && value != null)
		{
			return value[((Identity)(ref itemIdentity)).Instance];
		}
		Pool instance = Pool.Instance;
		Identity val = default(Identity);
		Identity identity = ((IEntity)character).Identity;
		((Identity)(ref val)).Type = (IdentityType)((Identity)(ref identity)).Instance;
		((Identity)(ref val)).Instance = (int)((Identity)(ref itemIdentity)).Type;
		value = instance.GetObject<IInventoryPage>(val);
		return (value == null) ? null : value[((Identity)(ref itemIdentity)).Instance];
	}
}
