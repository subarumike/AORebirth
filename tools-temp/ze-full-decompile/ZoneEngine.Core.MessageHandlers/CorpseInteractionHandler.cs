using System.Threading;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

public sealed class CorpseInteractionHandler
{
	public static readonly CorpseInteractionHandler Default = new CorpseInteractionHandler();

	private CorpseInteractionHandler()
	{
	}

	public bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Invalid comparison between Unknown and I4
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		if (CorpseInteractionRules.IsDirectCorpseTarget(target))
		{
			bool flag = ((IInstancedEntity)client.Controller.Character).Playfield.TryUseCorpse(client.Controller.Character, target);
			((IClient)client).Server.Info((IClient)(object)client, "CorpseUse direct target={0} used={1}", new object[2] { target, flag });
			if (flag)
			{
				AcknowledgeCorpseUseDelayed(client.Controller.Character, message, target);
			}
			return true;
		}
		if ((int)((Identity)(ref target)).Type == 50000 && TryRouteDeadNpcCorpseUse(client, target, out var routedCorpseIdentity))
		{
			AcknowledgeCorpseUseDelayed(client.Controller.Character, message, routedCorpseIdentity);
			return true;
		}
		return false;
	}

	private bool TryRouteDeadNpcCorpseUse(IZoneClient client, Identity target, out Identity routedCorpseIdentity)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		bool flag = ((IInstancedEntity)client.Controller.Character).Playfield.TryUseDeadNpcCorpse(client.Controller.Character, target, ref routedCorpseIdentity);
		((IClient)client).Server.Info((IClient)(object)client, "CorpseUse deadNpc target={0} routed={1} corpse={2}", new object[3] { target, flag, routedCorpseIdentity });
		return flag;
	}

	private static void AcknowledgeCorpseUseDelayed(ICharacter character, GenericCmdMessage message, Identity corpse, bool announceToPlayfield = false)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		ThreadPool.QueueUserWorkItem(delegate
		{
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			Thread.Sleep(550);
			if (character != null && ((IDynel)character).Controller != null && ((IDynel)character).Controller.Client != null)
			{
				BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.AcknowledgeCorpseUse(character, message, corpse, announceToPlayfield);
			}
		});
	}
}
