using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Nanos;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using MsgPack;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class castnano : FunctionPrototype
{
	public override FunctionType FunctionId => (FunctionType)53051;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		Character val = (Character)(object)((self is Character) ? self : null);
		if (val == null || arguments == null || arguments.Length < 1)
		{
			return false;
		}
		int nanoId = ((MessagePackObject)(ref arguments[0])).AsInt32();
		return ApplyInstantNano(val, target, nanoId);
	}

	internal static bool ApplyInstantNano(Character character, IInstancedEntity preferredTarget, int nanoId)
	{
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01af: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || nanoId <= 0)
		{
			return false;
		}
		if (!NanoLoader.NanoList.TryGetValue(nanoId, out var value))
		{
			LogUtil.Debug((DebugInfoDetail)256, "CastNano missing nanoId=" + nanoId);
			return false;
		}
		Character val = (Character)(object)((preferredTarget is Character) ? preferredTarget : null);
		Identity val2;
		if (val == null)
		{
			val = character;
			val2 = character.SelectedTarget;
			if (((Identity)(ref val2)).Instance != 0)
			{
				val2 = character.SelectedTarget;
				int instance = ((Identity)(ref val2)).Instance;
				val2 = ((PooledObject)character).Identity;
				if (instance != ((Identity)(ref val2)).Instance && ((Dynel)character).Playfield != null)
				{
					Character val3 = ((Dynel)character).Playfield.FindByIdentity<Character>(character.SelectedTarget);
					if (val3 != null)
					{
						val = val3;
					}
				}
			}
		}
		Identity identity = ((PooledObject)val).Identity;
		BaseMessageHandler<CastNanoSpellMessage, CastNanoSpellMessageHandler>.Default.Send((ICharacter)(object)character, nanoId, identity);
		BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.FinishNanoCasting((ICharacter)(object)character, (CharacterActionType)107, Identity.None, 1, nanoId);
		bool flag = value.Events != null && value.Events.Count > 0;
		if (flag)
		{
			if (val != null)
			{
				val2 = ((PooledObject)val).Identity;
				if (((Identity)(ref val2)).Instance != 0)
				{
					val2 = ((PooledObject)val).Identity;
					int instance2 = ((Identity)(ref val2)).Instance;
					val2 = ((PooledObject)character).Identity;
					if (instance2 != ((Identity)(ref val2)).Instance)
					{
						character.SetTarget(((PooledObject)val).Identity);
					}
				}
			}
			NanoEventRuntimeService.Default.ExecuteOnUseEvents((ICharacter)(object)character, value);
		}
		int itemAttribute = value.getItemAttribute(8);
		if (itemAttribute > 0 && flag && !NanoEventRuntimeService.Default.HasOffensiveHitOnUse(value))
		{
			BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.SetNanoDuration((ICharacter)(object)character, identity, nanoId, itemAttribute);
		}
		LogUtil.Debug((DebugInfoDetail)128, $"CastNano instant caster={((PooledObject)character).Identity} nano={nanoId} recipient={identity} duration={itemAttribute} scripted={flag}");
		return true;
	}
}
