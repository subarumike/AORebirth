using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Script;

namespace ZoneEngine.Scripts;

public class PerkResetService : IAOScript
{
	public void Main(string[] args)
	{
	}

	public void InitPerkResetService(ICharacter character)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		PerkResetServiceKnu knuBot = new PerkResetServiceKnu(((IEntity)character).Identity);
		if (((IDynel)character).Controller is NPCController nPCController)
		{
			nPCController.SetKnuBot(knuBot);
			Identity identity = ((IEntity)character).Identity;
			LogUtil.Debug((DebugInfoDetail)128, "Initialized PerkResetService with npc " + ((Identity)(ref identity)).ToString(true));
		}
	}
}
