namespace ZoneEngine.Core.Arete.Dialogue
{
    #region Usings ...

    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    public static class AreteRexDialogueRouter
    {
        public const string EnableEnvironmentVariableName =
            ContentDrivenNpcDialogueRouter.RexLarssonGateEnvironmentVariableName;

        public static bool IsEnabled
        {
            get
            {
                return ContentDrivenNpcDialogueRouter.IsRexLarssonRoutingEnabled;
            }
        }

        public static bool TryStartDialogue(ICharacter npc, Identity sourceIdentity)
        {
            return ContentDrivenNpcDialogueRouter.TryStartDialogue(npc, sourceIdentity);
        }

        public static bool TryStartDialogueForTarget(ICharacter source, Identity targetIdentity)
        {
            return ContentDrivenNpcDialogueRouter.TryStartDialogueForTarget(source, targetIdentity);
        }

        public static bool ShouldSuppressCombat(ICharacter target)
        {
            return ContentDrivenNpcDialogueRouter.ShouldSuppressCombat(target);
        }

        public static bool TryHandleAnswer(ICharacter source, Identity targetIdentity, int answerIndex)
        {
            return ContentDrivenNpcDialogueRouter.TryHandleAnswer(source, targetIdentity, answerIndex);
        }

        public static bool TryHandleClose(ICharacter source, Identity targetIdentity)
        {
            return ContentDrivenNpcDialogueRouter.TryHandleClose(source, targetIdentity);
        }
    }
}
