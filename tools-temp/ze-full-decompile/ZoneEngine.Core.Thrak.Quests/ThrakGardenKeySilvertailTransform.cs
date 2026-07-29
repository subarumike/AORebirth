using System;
using AORebirth.Core.Entities;
using AORebirth.Core.NPCHandler;
using AORebirth.Core.Playfields;
using AORebirth.Core.Vector;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core.Controllers;

namespace ZoneEngine.Core.Thrak.Quests;

internal static class ThrakGardenKeySilvertailTransform
{
	private const string TemplateHash = "BART";

	private const int CursedLevel = 8;

	private const int CursedHealth = 720;

	private const int CursedMonsterData = 208922;

	private const int CursedScale = 141;

	private const int CursedVisualFlags = 31;

	internal static bool TryCurseAndAggro(ICharacter source, Identity silvertailIdentity)
	{
		//IL_019c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || ((IInstancedEntity)source).Playfield == null || silvertailIdentity == Identity.None)
		{
			return false;
		}
		if (!(((IInstancedEntity)source).Playfield is Playfield playfield))
		{
			return false;
		}
		ICharacter @object = Pool.Instance.GetObject<ICharacter>(((IEntity)((IInstancedEntity)source).Playfield).Identity, silvertailIdentity);
		if (@object == null || !string.Equals(((INamedEntity)@object).Name, "Dreaming Silvertail", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		Coordinate position = ((IDynel)@object).Coordinates();
		Quaternion heading = (Quaternion)(((object)((IDynel)@object).Heading) ?? ((object)new Quaternion(0.0, 0.0, 0.0, 1.0)));
		Character val = SpawnCursed(playfield, ((IEntity)((IInstancedEntity)source).Playfield).Identity, position, heading);
		if (val == null)
		{
			LogUtil.Debug((DebugInfoDetail)512, "ThrakGardenKeySilvertailTransform cursed spawn failed at dreaming=" + ((Identity)(ref silvertailIdentity)).ToString(true));
			return false;
		}
		try
		{
			playfield.DespawnNpcImmediately(@object);
		}
		catch (Exception ex)
		{
			LogUtil.Debug((DebugInfoDetail)512, "ThrakGardenKeySilvertailTransform dreaming despawn failed: " + ex.Message);
		}
		try
		{
			val.SetFightingTarget(((IEntity)source).Identity);
			((ITargetingEntity)source).SetFightingTarget(((PooledObject)val).Identity);
			playfield.AcquireNpcAggro(source, (ICharacter)(object)val);
		}
		catch (Exception ex2)
		{
			LogUtil.Debug((DebugInfoDetail)512, "ThrakGardenKeySilvertailTransform aggro failed: " + ex2.Message);
		}
		string[] obj = new string[6] { "ThrakGardenKeySilvertailTransform cursed npc=", null, null, null, null, null };
		Identity identity = ((PooledObject)val).Identity;
		obj[1] = ((Identity)(ref identity)).ToString(true);
		obj[2] = " from dreaming=";
		obj[3] = ((Identity)(ref silvertailIdentity)).ToString(true);
		obj[4] = " by=";
		identity = ((IEntity)source).Identity;
		obj[5] = ((Identity)(ref identity)).ToString(true);
		LogUtil.Debug((DebugInfoDetail)128, string.Concat(obj));
		return true;
	}

	internal static void TryObserveCursedDeath(ICharacter attacker, ICharacter target)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		if (attacker != null && target != null && string.Equals(((INamedEntity)target).Name, "Cursed Silvertail", StringComparison.OrdinalIgnoreCase))
		{
			Identity identity = ((IEntity)attacker).Identity;
			LogUtil.Debug((DebugInfoDetail)128, "ThrakGardenKey cursed silvertail killed by=" + ((Identity)(ref identity)).ToString(true) + " (soul count already advanced on trade)");
		}
	}

	private static Character SpawnCursed(Playfield playfield, Identity playfieldIdentity, Coordinate position, Quaternion heading)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		if (playfield == null || position == null)
		{
			return null;
		}
		NPCController nPCController = new NPCController();
		Character val = NonPlayerCharacterHandler.SpawnMobFromTemplate("BART", playfieldIdentity, position, (Quaternion)(((object)heading) ?? ((object)new Quaternion(0.0, 0.0, 0.0, 1.0))), (IController)(object)nPCController, 8);
		if (val == null)
		{
			return null;
		}
		((Dynel)val).Name = "Cursed Silvertail";
		((Dynel)val).Playfield = (IPlayfield)(object)playfield;
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(359, 208922u);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(1, 720u);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(27, 720u);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(54, 8u);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(673, 31u);
		((Dynel)val).Stats.SetBaseValueWithoutTriggering(360, 141u);
		((Dynel)val).Coordinates(position);
		((Dynel)val).DoNotDoTimers = false;
		playfield.ActivateNpc((ICharacter)(object)val);
		playfield.AnnounceSpawnedCharacterVisibility((ICharacter)(object)val, Identity.None);
		return val;
	}
}
