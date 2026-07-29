using AORebirth.Core.Entities;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Arete.Dialogue;

public static class AreteRexDialogueRouter
{
	public const string EnableEnvironmentVariableName = "AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING";

	public static bool IsEnabled => ContentDrivenNpcDialogueRouter.IsRexLarssonRoutingEnabled;

	public static bool TryStartDialogue(ICharacter npc, Identity sourceIdentity)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		return ContentDrivenNpcDialogueRouter.TryStartDialogue(npc, sourceIdentity);
	}

	public static bool TryStartDialogueForTarget(ICharacter source, Identity targetIdentity)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		return ContentDrivenNpcDialogueRouter.TryStartDialogueForTarget(source, targetIdentity);
	}

	public static bool ShouldSuppressCombat(ICharacter target)
	{
		return ContentDrivenNpcDialogueRouter.ShouldSuppressCombat(target);
	}

	public static bool TryHandleAnswer(ICharacter source, Identity targetIdentity, int answerIndex)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		return ContentDrivenNpcDialogueRouter.TryHandleAnswer(source, targetIdentity, answerIndex);
	}

	public static bool TryHandleClose(ICharacter source, Identity targetIdentity)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		return ContentDrivenNpcDialogueRouter.TryHandleClose(source, targetIdentity);
	}
}
