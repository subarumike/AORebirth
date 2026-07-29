using System;
using System.Collections.Generic;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Nanos;
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
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.Core;

internal static class PetCommandService
{
	private sealed class PetHealCommandState
	{
		public Identity FocusTarget { get; set; }

		public DateTime NextCastUtc { get; set; }

		public bool AnnouncedStart { get; set; }
	}

	private static readonly Dictionary<int, PetHealCommandState> ActiveHealCommands = new Dictionary<int, PetHealCommandState>();

	private static readonly Dictionary<int, Identity> OwnerHealFocusSelection = new Dictionary<int, Identity>();

	public const int CommandFollow = 1;

	public const int CommandBehind = 2;

	public const int CommandWait = 4;

	public const int CommandGuard = 6;

	public const int CommandAttack = 7;

	public const int CommandTerminate = 10;

	public const int CommandHeal = 12;

	public const int CommandReport = 14;

	public static void HandleChatPetCommand(IZoneClient client, string[] cmdArgs)
	{
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		ICharacter val = ((client != null && client.Controller != null) ? client.Controller.Character : null);
		if (val == null || cmdArgs == null || cmdArgs.Length < 2)
		{
			return;
		}
		int num = 1;
		if (cmdArgs.Length >= 3 && cmdArgs[1].StartsWith("\"", StringComparison.Ordinal))
		{
			for (num = 2; num < cmdArgs.Length && !cmdArgs[num].EndsWith("\"", StringComparison.Ordinal); num++)
			{
			}
			num++;
		}
		if (num < cmdArgs.Length && TryResolveCommandId(cmdArgs[num], out var commandId))
		{
			ExecuteForAllOwnedPets(val, client, commandId, Identity.None);
		}
	}

	public static void HandlePetCommandMessage(IZoneClient client, ICharacter owner, int commandId, bool applyToAllPets, Identity petIdentity, Identity commandTarget)
	{
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		if (owner == null || ((IInstancedEntity)owner).Playfield == null || commandId <= 0)
		{
			return;
		}
		if (applyToAllPets || ((Identity)(ref petIdentity)).Instance == 0)
		{
			ExecuteForAllOwnedPets(owner, client, commandId, commandTarget);
			return;
		}
		ICharacter val = ResolveOwnedPet(owner, petIdentity);
		if (val != null)
		{
			ExecuteForPet(owner, client, val, commandId, commandTarget);
		}
	}

	private static void ExecuteForAllOwnedPets(ICharacter owner, IZoneClient client, int commandId, Identity commandTarget)
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		foreach (int activePetStrain in PetRuntimeService.Default.GetActivePetStrains(owner))
		{
			ICharacter activePetInStrain = PetRuntimeService.Default.GetActivePetInStrain(owner, activePetStrain);
			if (activePetInStrain != null)
			{
				ExecuteForPet(owner, client, activePetInStrain, commandId, commandTarget);
			}
		}
	}

	private static void ExecuteForPet(ICharacter owner, IZoneClient client, ICharacter pet, int commandId, Identity commandTarget)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		//IL_0190: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Unknown result type (might be due to invalid IL or missing references)
		//IL_022c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0231: Unknown result type (might be due to invalid IL or missing references)
		//IL_0247: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
		LogUtil.Debug((DebugInfoDetail)256, $"PetCommandExecute owner={((IEntity)owner).Identity} pet={((IEntity)pet).Identity} commandId={commandId} target={commandTarget}");
		if (client != null)
		{
			((IClient)client).Server.Info((IClient)(object)client, "PetCommand pet={0} commandId={1} target={2}", new object[3]
			{
				((IEntity)pet).Identity,
				commandId,
				commandTarget
			});
		}
		if (!(((IDynel)pet).Controller is NPCController nPCController) || !(((IInstancedEntity)owner).Playfield is Playfield playfield))
		{
			return;
		}
		Identity identity;
		switch (commandId)
		{
		case 1:
		case 6:
		{
			Dictionary<int, PetHealCommandState> activeHealCommands3 = ActiveHealCommands;
			identity = ((IEntity)pet).Identity;
			activeHealCommands3.Remove(((Identity)(ref identity)).Instance);
			nPCController.Follow(((IEntity)owner).Identity, 2.0);
			break;
		}
		case 2:
		{
			Dictionary<int, PetHealCommandState> activeHealCommands4 = ActiveHealCommands;
			identity = ((IEntity)pet).Identity;
			activeHealCommands4.Remove(((Identity)(ref identity)).Instance);
			nPCController.Follow(((IEntity)owner).Identity, 4.0);
			break;
		}
		case 4:
			ExecuteWait(owner, pet, nPCController, playfield);
			break;
		case 7:
		{
			Dictionary<int, PetHealCommandState> activeHealCommands2 = ActiveHealCommands;
			identity = ((IEntity)pet).Identity;
			activeHealCommands2.Remove(((Identity)(ref identity)).Instance);
			if (PetCombatRules.IsPlayerOwnedMeleeCombatPet(pet))
			{
				ExecuteAttack(owner, pet, nPCController, playfield, commandTarget);
			}
			break;
		}
		case 12:
			ExecuteHeal(owner, pet, nPCController, playfield, commandTarget);
			break;
		case 14:
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(owner, $"{((INamedEntity)pet).Name}: HP {((IStats)pet).Stats[(StatIds)27].Value}/{((IStats)pet).Stats[(StatIds)1].Value}", 0, 0);
			break;
		case 10:
		{
			Dictionary<int, PetHealCommandState> activeHealCommands = ActiveHealCommands;
			identity = ((IEntity)pet).Identity;
			activeHealCommands.Remove(((Identity)(ref identity)).Instance);
			PetRuntimeService.Default.TerminatePetByIdentity(owner, ((IEntity)pet).Identity);
			break;
		}
		case 3:
		case 5:
		case 8:
		case 9:
		case 11:
		case 13:
			break;
		}
	}

	private static void ExecuteAttack(ICharacter owner, ICharacter pet, NPCController petController, Playfield playfield, Identity commandTarget)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		Identity val = commandTarget;
		if (((Identity)(ref val)).Instance == 0)
		{
			val = ((ITargetingEntity)owner).SelectedTarget;
		}
		if (((Identity)(ref val)).Instance == 0)
		{
			val = ((ITargetingEntity)owner).FightingTarget;
		}
		if (((Identity)(ref val)).Instance == 0)
		{
			return;
		}
		int instance = ((Identity)(ref val)).Instance;
		Identity identity = ((IEntity)pet).Identity;
		if (instance != ((Identity)(ref identity)).Instance)
		{
			ICharacter val2 = ((IInstancedEntity)owner).Playfield.FindByIdentity<ICharacter>(val);
			if (val2 != null && PlayerVersusPlayerCombatRules.CanEngagePlayerVersusPlayerCombat(pet, val2))
			{
				petController.StopFollow();
				((ITargetingEntity)pet).SetTarget(val);
				((ITargetingEntity)pet).SetFightingTarget(val);
				playfield.SuspendNpcRegen(val2);
				playfield.ResetCombatTick(((IEntity)pet).Identity);
				playfield.AcquireNpcAggro(pet, val2);
			}
		}
	}

	private static void ExecuteWait(ICharacter owner, ICharacter pet, NPCController petController, Playfield playfield)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Expected O, but got Unknown
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		Dictionary<int, PetHealCommandState> activeHealCommands = ActiveHealCommands;
		Identity identity = ((IEntity)pet).Identity;
		activeHealCommands.Remove(((Identity)(ref identity)).Instance);
		((ITargetingEntity)pet).SetFightingTarget(Identity.None);
		((ITargetingEntity)pet).SetTarget(Identity.None);
		playfield.Announce((MessageBody)new StopFightMessage
		{
			Identity = ((IEntity)pet).Identity,
			Unknown1 = 1
		});
		playfield.ClearCombatTracking(((IEntity)pet).Identity);
		petController.StopFollow();
		BaseMessageHandler<FollowTargetMessage, FollowTargetMessageHandler>.Default.Send(pet, ((IDynel)pet).RawCoordinates);
	}

	internal static void ReturnPetToOwner(ICharacter pet)
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		if (pet != null && PetCombatRules.IsPlayerOwnedPet(pet))
		{
			ICharacter val = PetCombatRules.ResolvePetOwner(pet);
			if (val != null && ((IDynel)pet).Controller is NPCController nPCController)
			{
				((ITargetingEntity)pet).SetFightingTarget(Identity.None);
				((ITargetingEntity)pet).SetTarget(Identity.None);
				nPCController.Follow(((IEntity)val).Identity, 2.0);
			}
		}
	}

	internal static bool OnOwnerLookAtTarget(ICharacter owner, Identity lookTarget)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		if (owner == null || ((Identity)(ref lookTarget)).Instance == 0)
		{
			return false;
		}
		if (!(((IInstancedEntity)owner).Playfield is Playfield playfield))
		{
			return false;
		}
		Identity val = ResolveFriendlyHealTargetByInstance(owner, lookTarget, playfield);
		if (((Identity)(ref val)).Instance == 0)
		{
			return false;
		}
		SetOwnerHealFocusSelection(owner, val);
		((ITargetingEntity)owner).SetTarget(val);
		if (HasActiveHealCommand(owner))
		{
			ApplyHealFocusToActivePets(owner, playfield, val, triggerImmediateHeal: true);
		}
		return true;
	}

	internal static ICharacter ResolveOwnedPet(ICharacter owner, Identity petIdentity)
	{
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		if (owner == null || ((Identity)(ref petIdentity)).Instance == 0)
		{
			return null;
		}
		Identity identity;
		foreach (int activePetStrain in PetRuntimeService.Default.GetActivePetStrains(owner))
		{
			ICharacter activePetInStrain = PetRuntimeService.Default.GetActivePetInStrain(owner, activePetStrain);
			if (activePetInStrain != null)
			{
				identity = ((IEntity)activePetInStrain).Identity;
				if (((Identity)(ref identity)).Instance == ((Identity)(ref petIdentity)).Instance)
				{
					return activePetInStrain;
				}
			}
		}
		if (((IInstancedEntity)owner).Playfield == null)
		{
			return null;
		}
		ICharacter val = ((IInstancedEntity)owner).Playfield.FindByIdentity<ICharacter>(petIdentity);
		if (val != null)
		{
			int value = ((IStats)val).Stats[(StatIds)196].Value;
			identity = ((IEntity)owner).Identity;
			if (value == ((Identity)(ref identity)).Instance)
			{
				return val;
			}
		}
		if (!(((IInstancedEntity)owner).Playfield is Playfield playfield))
		{
			return null;
		}
		return FindCharacterByInstance(playfield, ((Identity)(ref petIdentity)).Instance, petIdentity);
	}

	internal static Identity ResolveHealCommandTarget(ICharacter owner, Identity healPetIdentity, Identity packetTarget)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		if (owner == null || ((IInstancedEntity)owner).Playfield == null)
		{
			return Identity.None;
		}
		if (!(((IInstancedEntity)owner).Playfield is Playfield playfield))
		{
			return Identity.None;
		}
		bool flag = ((Identity)(ref packetTarget)).Instance != 0 && ((Identity)(ref healPetIdentity)).Instance != 0 && ((Identity)(ref packetTarget)).Instance == ((Identity)(ref healPetIdentity)).Instance;
		if (flag)
		{
			packetTarget = Identity.None;
		}
		if (((Identity)(ref packetTarget)).Instance != 0)
		{
			Identity val = ResolveFriendlyHealTargetByInstance(owner, packetTarget, playfield);
			if (((Identity)(ref val)).Instance != 0)
			{
				SetOwnerHealFocusSelection(owner, val);
				return val;
			}
		}
		if (flag)
		{
			Identity ownerHealFocusSelection = GetOwnerHealFocusSelection(owner, playfield);
			if (((Identity)(ref ownerHealFocusSelection)).Instance != 0)
			{
				return ownerHealFocusSelection;
			}
		}
		else
		{
			Identity ownerHealFocusSelection2 = GetOwnerHealFocusSelection(owner, playfield);
			if (((Identity)(ref ownerHealFocusSelection2)).Instance != 0)
			{
				return ownerHealFocusSelection2;
			}
		}
		SetOwnerHealFocusSelection(owner, ((IEntity)owner).Identity);
		return ((IEntity)owner).Identity;
	}

	private static Identity GetOwnerHealFocusSelection(ICharacter owner, Playfield playfield)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		if (owner == null || playfield == null)
		{
			return Identity.None;
		}
		Dictionary<int, Identity> ownerHealFocusSelection = OwnerHealFocusSelection;
		Identity identity = ((IEntity)owner).Identity;
		if (!ownerHealFocusSelection.TryGetValue(((Identity)(ref identity)).Instance, out var value) || ((Identity)(ref value)).Instance == 0)
		{
			return Identity.None;
		}
		return ResolveFriendlyHealTargetByInstance(owner, value, playfield);
	}

	private static Identity ResolveFriendlyHealTargetByInstance(ICharacter owner, Identity target, Playfield playfield)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		if (owner == null || playfield == null || ((Identity)(ref target)).Instance == 0)
		{
			return Identity.None;
		}
		int instance = ((Identity)(ref target)).Instance;
		Identity identity = ((IEntity)owner).Identity;
		if (instance == ((Identity)(ref identity)).Instance)
		{
			return ((IEntity)owner).Identity;
		}
		foreach (int activePetStrain in PetRuntimeService.Default.GetActivePetStrains(owner))
		{
			ICharacter activePetInStrain = PetRuntimeService.Default.GetActivePetInStrain(owner, activePetStrain);
			if (activePetInStrain != null)
			{
				identity = ((IEntity)activePetInStrain).Identity;
				if (((Identity)(ref identity)).Instance == ((Identity)(ref target)).Instance && ((IStats)activePetInStrain).Stats[(StatIds)27].Value > 0)
				{
					return ((IEntity)activePetInStrain).Identity;
				}
			}
		}
		return NormalizeFriendlyHealIdentity(owner, target, playfield);
	}

	internal static void CommitHealTargetFromPacket(ICharacter owner, Identity healPetIdentity, Identity packetTarget)
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		if (owner != null && ((IInstancedEntity)owner).Playfield != null && ((Identity)(ref packetTarget)).Instance != 0 && (((Identity)(ref healPetIdentity)).Instance == 0 || ((Identity)(ref packetTarget)).Instance != ((Identity)(ref healPetIdentity)).Instance))
		{
			ResolveFriendlyHealTargetForSelection(owner, packetTarget);
		}
	}

	internal static Identity ResolveFriendlyHealTargetForSelection(ICharacter owner, Identity target)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		if (owner == null || ((IInstancedEntity)owner).Playfield == null || ((Identity)(ref target)).Instance == 0)
		{
			return Identity.None;
		}
		if (!(((IInstancedEntity)owner).Playfield is Playfield playfield))
		{
			return Identity.None;
		}
		Identity val = ResolveFriendlyHealTargetByInstance(owner, target, playfield);
		if (((Identity)(ref val)).Instance != 0)
		{
			SetOwnerHealFocusSelection(owner, val);
		}
		return val;
	}

	private static void SetOwnerHealFocusSelection(ICharacter owner, Identity focus)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		if (owner != null && ((Identity)(ref focus)).Instance != 0)
		{
			Dictionary<int, Identity> ownerHealFocusSelection = OwnerHealFocusSelection;
			Identity identity = ((IEntity)owner).Identity;
			ownerHealFocusSelection[((Identity)(ref identity)).Instance] = focus;
		}
	}

	internal static bool HasActiveHealCommand(ICharacter owner)
	{
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		if (owner == null || ((IInstancedEntity)owner).Playfield == null)
		{
			return false;
		}
		if (!(((IInstancedEntity)owner).Playfield is Playfield playfield))
		{
			return false;
		}
		foreach (int key in ActiveHealCommands.Keys)
		{
			ICharacter val = FindCharacterByInstance(playfield, key, Identity.None);
			if (val == null)
			{
				continue;
			}
			ICharacter val2 = PetCombatRules.ResolvePetOwner(val);
			if (val2 != null)
			{
				Identity identity = ((IEntity)val2).Identity;
				int instance = ((Identity)(ref identity)).Instance;
				identity = ((IEntity)owner).Identity;
				if (instance == ((Identity)(ref identity)).Instance)
				{
					return true;
				}
			}
		}
		return false;
	}

	internal static void ProcessPetHealTick(ICharacter pet)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_014d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		if (!PetCombatRules.IsPlayerOwnedHealingPet(pet) || ((IInstancedEntity)pet).Playfield == null)
		{
			return;
		}
		Dictionary<int, PetHealCommandState> activeHealCommands = ActiveHealCommands;
		Identity val = ((IEntity)pet).Identity;
		if (!activeHealCommands.TryGetValue(((Identity)(ref val)).Instance, out var value))
		{
			return;
		}
		ICharacter val2 = PetCombatRules.ResolvePetOwner(pet);
		if (val2 == null)
		{
			Dictionary<int, PetHealCommandState> activeHealCommands2 = ActiveHealCommands;
			val = ((IEntity)pet).Identity;
			activeHealCommands2.Remove(((Identity)(ref val)).Instance);
		}
		else
		{
			if (!(((IInstancedEntity)val2).Playfield is Playfield playfield) || DateTime.UtcNow < value.NextCastUtc)
			{
				return;
			}
			Identity ownerHealFocusSelection = GetOwnerHealFocusSelection(val2, playfield);
			if (((Identity)(ref ownerHealFocusSelection)).Instance != 0)
			{
				int instance = ((Identity)(ref ownerHealFocusSelection)).Instance;
				val = value.FocusTarget;
				if (instance != ((Identity)(ref val)).Instance)
				{
					value.FocusTarget = ownerHealFocusSelection;
					value.NextCastUtc = DateTime.UtcNow;
				}
			}
			if (((IDynel)pet).Controller is NPCController petController)
			{
				val = value.FocusTarget;
				if (((Identity)(ref val)).Instance != 0)
				{
					SyncHealPetFollow(pet, petController, value.FocusTarget);
				}
			}
			ProcessHealCycle(val2, pet, playfield, ref value);
			Dictionary<int, PetHealCommandState> activeHealCommands3 = ActiveHealCommands;
			val = ((IEntity)pet).Identity;
			activeHealCommands3[((Identity)(ref val)).Instance] = value;
		}
	}

	internal static void SyncOwnerHealSelectedTarget(ICharacter owner, Identity commandTarget)
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		if (owner != null && ((IInstancedEntity)owner).Playfield != null && ((IInstancedEntity)owner).Playfield is Playfield playfield)
		{
			Identity val = ResolveHealCommandTarget(owner, playfield, commandTarget);
			if (((Identity)(ref val)).Instance != 0)
			{
				((ITargetingEntity)owner).SetTarget(val);
				ApplyHealFocusToActivePets(owner, playfield, val, triggerImmediateHeal: false);
			}
		}
	}

	private static Identity ResolveHealCommandTarget(ICharacter owner, Playfield playfield, Identity commandTarget)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		ICharacter val = null;
		foreach (int key in ActiveHealCommands.Keys)
		{
			ICharacter val2 = FindCharacterByInstance(playfield, key, Identity.None);
			if (val2 == null || !PetCombatRules.IsPlayerOwnedHealingPet(val2))
			{
				continue;
			}
			ICharacter val3 = PetCombatRules.ResolvePetOwner(val2);
			if (val3 != null)
			{
				Identity identity = ((IEntity)val3).Identity;
				int instance = ((Identity)(ref identity)).Instance;
				identity = ((IEntity)owner).Identity;
				if (instance == ((Identity)(ref identity)).Instance)
				{
					val = val2;
					break;
				}
			}
		}
		Identity healPetIdentity = ((val != null) ? ((IEntity)val).Identity : Identity.None);
		return ResolveHealCommandTarget(owner, healPetIdentity, commandTarget);
	}

	private static void ApplyHealFocusToActivePets(ICharacter owner, Playfield playfield, Identity focus, bool triggerImmediateHeal)
	{
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		List<int> list = new List<int>(ActiveHealCommands.Keys);
		foreach (int item in list)
		{
			if (!ActiveHealCommands.TryGetValue(item, out var value))
			{
				continue;
			}
			ICharacter val = FindCharacterByInstance(playfield, item, Identity.None);
			if (val == null || !PetCombatRules.IsPlayerOwnedHealingPet(val))
			{
				continue;
			}
			ICharacter val2 = PetCombatRules.ResolvePetOwner(val);
			if (val2 == null)
			{
				continue;
			}
			Identity identity = ((IEntity)val2).Identity;
			int instance = ((Identity)(ref identity)).Instance;
			identity = ((IEntity)owner).Identity;
			if (instance == ((Identity)(ref identity)).Instance)
			{
				value.FocusTarget = focus;
				value.NextCastUtc = DateTime.UtcNow;
				ActiveHealCommands[item] = value;
				if (((IDynel)val).Controller is NPCController petController)
				{
					SyncHealPetFollow(val, petController, focus);
				}
				if (triggerImmediateHeal)
				{
					ProcessHealCycle(owner, val, playfield, ref value);
					ActiveHealCommands[item] = value;
				}
			}
		}
	}

	private static void SyncHealPetFollow(ICharacter healPet, NPCController petController, Identity focus)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		if (healPet != null && petController != null && ((Identity)(ref focus)).Instance != 0)
		{
			petController.Follow(focus, 2.0);
		}
	}

	internal static void OnOwnerSelectedTargetChanged(ICharacter owner)
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		if (owner != null && ((IInstancedEntity)owner).Playfield != null && ((IInstancedEntity)owner).Playfield is Playfield playfield)
		{
			Identity val = ResolveFriendlyHealTargetByInstance(owner, ((ITargetingEntity)owner).SelectedTarget, playfield);
			if (((Identity)(ref val)).Instance != 0)
			{
				SetOwnerHealFocusSelection(owner, val);
				((ITargetingEntity)owner).SetTarget(val);
				ApplyHealFocusToActivePets(owner, playfield, val, triggerImmediateHeal: true);
			}
		}
	}

	private static Identity NormalizeFriendlyHealIdentity(ICharacter owner, Identity target, Playfield playfield)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		if (owner == null || playfield == null || ((Identity)(ref target)).Instance == 0)
		{
			return Identity.None;
		}
		int instance = ((Identity)(ref target)).Instance;
		Identity identity = ((IEntity)owner).Identity;
		if (instance == ((Identity)(ref identity)).Instance)
		{
			return ((IEntity)owner).Identity;
		}
		ICharacter val = FindCharacterByInstance(playfield, ((Identity)(ref target)).Instance, target);
		if (val != null && ((IStats)val).Stats[(StatIds)27].Value > 0 && PetCombatRules.IsPlayerOwnedPet(val))
		{
			int value = ((IStats)val).Stats[(StatIds)196].Value;
			identity = ((IEntity)owner).Identity;
			if (value == ((Identity)(ref identity)).Instance)
			{
				return ((IEntity)val).Identity;
			}
		}
		return Identity.None;
	}

	private static ICharacter FindCharacterByInstance(Playfield playfield, int instance, Identity hint)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Invalid comparison between Unknown and I4
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		if (playfield == null || instance == 0)
		{
			return null;
		}
		if (((Identity)(ref hint)).Instance == instance && (int)((Identity)(ref hint)).Type > 0)
		{
			ICharacter val = playfield.FindByIdentity<ICharacter>(hint);
			if (val != null)
			{
				return val;
			}
		}
		Identity identity = default(Identity);
		((Identity)(ref identity)).Type = (IdentityType)50000;
		((Identity)(ref identity)).Instance = instance;
		ICharacter val2 = playfield.FindByIdentity<ICharacter>(identity);
		if (val2 != null)
		{
			return val2;
		}
		return ResolveCharacterFromPool(playfield, instance, hint);
	}

	private static ICharacter ResolveCharacterFromPool(Playfield playfield, int instance, Identity hint)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Invalid comparison between Unknown and I4
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		if (playfield == null || instance == 0)
		{
			return null;
		}
		Identity identity = ((PooledObject)playfield).Identity;
		if (((Identity)(ref hint)).Instance == instance && (int)((Identity)(ref hint)).Type > 0)
		{
			IEntity @object = Pool.Instance.GetObject(identity, hint);
			ICharacter val = (ICharacter)(object)((@object is ICharacter) ? @object : null);
			if (val != null)
			{
				return val;
			}
		}
		Pool instance2 = Pool.Instance;
		Identity val2 = default(Identity);
		((Identity)(ref val2)).Type = (IdentityType)50000;
		((Identity)(ref val2)).Instance = instance;
		IEntity object2 = instance2.GetObject(identity, val2);
		ICharacter val3 = (ICharacter)(object)((object2 is ICharacter) ? object2 : null);
		if (val3 != null)
		{
			return val3;
		}
		Pool instance3 = Pool.Instance;
		val2 = default(Identity);
		((Identity)(ref val2)).Type = (IdentityType)50000;
		((Identity)(ref val2)).Instance = instance;
		return instance3.GetObject<ICharacter>(val2);
	}

	private static ICharacter ResolveHealTargetCharacter(ICharacter owner, Playfield playfield, Identity target)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		Identity hint = ResolveFriendlyHealTargetByInstance(owner, target, playfield);
		if (((Identity)(ref hint)).Instance == 0)
		{
			return null;
		}
		if (owner != null)
		{
			int instance = ((Identity)(ref hint)).Instance;
			Identity identity = ((IEntity)owner).Identity;
			if (instance == ((Identity)(ref identity)).Instance)
			{
				return owner;
			}
		}
		return FindCharacterByInstance(playfield, ((Identity)(ref hint)).Instance, hint);
	}

	private static void ExecuteHeal(ICharacter owner, ICharacter pet, NPCController petController, Playfield playfield, Identity commandTarget)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Expected O, but got Unknown
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		if (!PetCombatRules.IsPlayerOwnedHealingPet(pet))
		{
			Identity target = ResolveFollowTarget(owner, commandTarget);
			petController.Follow(target, 2.0);
			return;
		}
		((ITargetingEntity)pet).SetFightingTarget(Identity.None);
		((ITargetingEntity)pet).SetTarget(Identity.None);
		playfield.Announce((MessageBody)new StopFightMessage
		{
			Identity = ((IEntity)pet).Identity,
			Unknown1 = 1
		});
		SyncOwnerHealSelectedTarget(owner, commandTarget);
		Identity val = ResolveHealCommandTarget(owner, ((IEntity)pet).Identity, commandTarget);
		if (((Identity)(ref val)).Instance != 0)
		{
			SyncHealPetFollow(pet, petController, val);
		}
		else
		{
			petController.Follow(((IEntity)owner).Identity, 2.0);
		}
		Dictionary<int, PetHealCommandState> activeHealCommands = ActiveHealCommands;
		Identity identity = ((IEntity)pet).Identity;
		if (!activeHealCommands.TryGetValue(((Identity)(ref identity)).Instance, out var value))
		{
			value = new PetHealCommandState();
		}
		value.NextCastUtc = DateTime.UtcNow;
		value.FocusTarget = val;
		Dictionary<int, PetHealCommandState> activeHealCommands2 = ActiveHealCommands;
		identity = ((IEntity)pet).Identity;
		activeHealCommands2[((Identity)(ref identity)).Instance] = value;
		if (((Identity)(ref val)).Instance != 0)
		{
			ApplyHealFocusToActivePets(owner, playfield, val, triggerImmediateHeal: true);
		}
		if (!value.AnnouncedStart)
		{
			string arg = ((INamedEntity)owner).Name ?? "Your";
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(owner, $"{arg}'s pet, {((INamedEntity)pet).Name}: Commencing the healing process now, master.", 0, 0);
			value.AnnouncedStart = true;
			Dictionary<int, PetHealCommandState> activeHealCommands3 = ActiveHealCommands;
			identity = ((IEntity)pet).Identity;
			activeHealCommands3[((Identity)(ref identity)).Instance] = value;
		}
		ProcessHealCycle(owner, pet, playfield, ref value);
		Dictionary<int, PetHealCommandState> activeHealCommands4 = ActiveHealCommands;
		identity = ((IEntity)pet).Identity;
		activeHealCommands4[((Identity)(ref identity)).Instance] = value;
	}

	private static Identity ResolveFollowTarget(ICharacter owner, Identity preferredTarget)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		if (((Identity)(ref preferredTarget)).Instance != 0 && ((IInstancedEntity)owner).Playfield != null && ((IInstancedEntity)owner).Playfield.FindByIdentity<ICharacter>(preferredTarget) != null)
		{
			return preferredTarget;
		}
		return ((IEntity)owner).Identity;
	}

	private static void ProcessHealCycle(ICharacter owner, ICharacter pet, Playfield playfield, ref PetHealCommandState healState)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		Identity val = healState.FocusTarget;
		if (((Identity)(ref val)).Instance == 0)
		{
			val = GetOwnerHealFocusSelection(owner, playfield);
			if (((Identity)(ref val)).Instance != 0)
			{
				healState.FocusTarget = val;
			}
		}
		if (((Identity)(ref val)).Instance == 0)
		{
			healState.NextCastUtc = DateTime.UtcNow.AddSeconds(2.5);
			return;
		}
		ICharacter val2 = ResolveHealTargetCharacter(owner, playfield, val);
		if (val2 == null || ((IStats)val2).Stats[(StatIds)27].Value <= 0)
		{
			healState.FocusTarget = Identity.None;
			ReturnPetToOwner(pet);
			healState.NextCastUtc = DateTime.UtcNow.AddSeconds(2.5);
			return;
		}
		if (((IDynel)pet).Controller is NPCController petController)
		{
			SyncHealPetFollow(pet, petController, val);
		}
		if (!IsHealCandidateReady(pet, val2) || !TryCastPetHeal(owner, pet, val2, playfield, ref healState))
		{
			healState.NextCastUtc = DateTime.UtcNow.AddSeconds(2.5);
		}
	}

	private static bool IsHealCandidateReady(ICharacter healPet, ICharacter candidate)
	{
		if (candidate == null || ((IStats)candidate).Stats[(StatIds)27].Value <= 0 || !NeedsHealing(candidate) || Playfield.GetCombatDistance(healPet, candidate) > 20.0)
		{
			return false;
		}
		return true;
	}

	private static bool NeedsHealing(ICharacter character)
	{
		return ((IStats)character).Stats[(StatIds)27].Value < ((IStats)character).Stats[(StatIds)1].Value;
	}

	private static bool TryCastPetHeal(ICharacter owner, ICharacter pet, ICharacter healTarget, Playfield playfield, ref PetHealCommandState healState)
	{
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		//IL_0198: Unknown result type (might be due to invalid IL or missing references)
		//IL_019a: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e1: Expected O, but got Unknown
		//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0205: Unknown result type (might be due to invalid IL or missing references)
		//IL_0222: Unknown result type (might be due to invalid IL or missing references)
		//IL_022f: Expected O, but got Unknown
		//IL_0286: Unknown result type (might be due to invalid IL or missing references)
		//IL_0294: Unknown result type (might be due to invalid IL or missing references)
		if (!PetRuntimeService.Default.TryGetHealNanoId(owner, pet, out var healNanoId))
		{
			return false;
		}
		if (!NanoLoader.NanoList.TryGetValue(healNanoId, out var value))
		{
			return false;
		}
		int nanoCastCost = PetHealNanoCatalog.GetNanoCastCost(value);
		if (nanoCastCost > 0 && ((IStats)pet).Stats[(StatIds)214].Value < nanoCastCost)
		{
			return false;
		}
		if (!PetHealNanoCatalog.TryRollHealAmount(value, healTarget, out var healRoll, out var healApplied))
		{
			return false;
		}
		if (healApplied <= 0)
		{
			return false;
		}
		if (nanoCastCost > 0)
		{
			IStat obj = ((IStats)pet).Stats[(StatIds)214];
			obj.Value -= nanoCastCost;
			BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(pet, 214, (uint)((IStats)pet).Stats[(StatIds)214].Value);
		}
		BaseMessageHandler<CastNanoSpellMessage, CastNanoSpellMessageHandler>.Default.SendPetCast(pet, healNanoId, ((IEntity)healTarget).Identity);
		BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.FinishNanoCasting(pet, (CharacterActionType)107, Identity.None, 1, healNanoId);
		BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.SendPetNanoExecutedWithinOwnerNcu(owner, pet, healRoll);
		int value2 = ((IStats)healTarget).Stats[(StatIds)27].Value;
		IStat obj2 = ((IStats)healTarget).Stats[(StatIds)27];
		obj2.Value += healApplied;
		int value3 = ((IStats)healTarget).Stats[(StatIds)27].Value;
		int num = value3 - value2;
		if (num <= 0)
		{
			return false;
		}
		playfield.Announce((MessageBody)new HealthDamageMessage
		{
			Identity = ((IEntity)healTarget).Identity,
			Unknown1 = value3,
			Unknown2 = num,
			Unknown3 = 0,
			Unknown4 = 0,
			Target = ((IEntity)pet).Identity,
			Unknown5 = 0
		});
		playfield.Announce((MessageBody)new FormatFeedbackMessage
		{
			Identity = ((IEntity)owner).Identity,
			Unknown = 1,
			Unknown1 = 0,
			FormattedMessage = $"~&!!!\":$Dt11s\n{((INamedEntity)pet).Name}\u0015{PetHealNanoCatalog.GetHealNanoDisplayName(healNanoId)}",
			Unknown2 = 0
		});
		BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(owner, $"{((INamedEntity)pet).Name} executes {PetHealNanoCatalog.GetHealNanoDisplayName(healNanoId)} on {((INamedEntity)healTarget).Name}.", 0, 0);
		BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendChanged(healTarget);
		BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendChanged(pet);
		LogUtil.Debug((DebugInfoDetail)256, $"PetHealCast pet={((IEntity)pet).Identity} target={((IEntity)healTarget).Identity} nano={healNanoId} roll={healRoll} applied={num} cost={nanoCastCost}");
		healState.NextCastUtc = DateTime.UtcNow.AddSeconds(PetHealNanoCatalog.GetHealRechargeSeconds(healNanoId));
		return true;
	}

	private static bool TryResolveCommandId(string command, out int commandId)
	{
		commandId = 0;
		if (string.IsNullOrWhiteSpace(command))
		{
			return false;
		}
		string text = command.Trim();
		if (text.Equals("follow", StringComparison.OrdinalIgnoreCase) || text.Equals("follow me", StringComparison.OrdinalIgnoreCase))
		{
			commandId = 1;
			return true;
		}
		if (text.Equals("behind", StringComparison.OrdinalIgnoreCase))
		{
			commandId = 2;
			return true;
		}
		if (text.Equals("wait", StringComparison.OrdinalIgnoreCase) || text.Equals("stop", StringComparison.OrdinalIgnoreCase) || text.Equals("stay", StringComparison.OrdinalIgnoreCase))
		{
			commandId = 4;
			return true;
		}
		if (text.Equals("guard", StringComparison.OrdinalIgnoreCase))
		{
			commandId = 6;
			return true;
		}
		if (text.Equals("attack", StringComparison.OrdinalIgnoreCase) || text.Equals("hunt", StringComparison.OrdinalIgnoreCase))
		{
			commandId = 7;
			return true;
		}
		if (text.Equals("heal", StringComparison.OrdinalIgnoreCase) || text.Equals("cast", StringComparison.OrdinalIgnoreCase))
		{
			commandId = 12;
			return true;
		}
		if (text.Equals("report", StringComparison.OrdinalIgnoreCase))
		{
			commandId = 14;
			return true;
		}
		if (text.Equals("terminate", StringComparison.OrdinalIgnoreCase) || text.Equals("dismiss", StringComparison.OrdinalIgnoreCase) || text.Equals("release", StringComparison.OrdinalIgnoreCase))
		{
			commandId = 10;
			return true;
		}
		if (int.TryParse(text, out var result) && result > 0)
		{
			commandId = result;
			return true;
		}
		return false;
	}
}
