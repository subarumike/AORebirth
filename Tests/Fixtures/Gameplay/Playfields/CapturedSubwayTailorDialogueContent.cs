namespace ZoneEngine.Core.Playfields
{
    // Capture-backed Tailor dialogue contract from AOSharpLiveCapture/20260719-021611.
    internal static class CapturedSubwayTailorDialogueContent
    {
        internal const int SourceNpcInstance = unchecked((int)0x79135F51);
        internal const string SourceNpcIdentity = "SimpleChar:79135F51";
        internal const string RootNodeId = "tailor_root";
        internal const string ReopenRootNodeId = "tailor_root_reopen";
        internal const string MeasurementNodeId = "tailor_parts";

        private static readonly int[] MeasurementItemIds =
        {
            256415,
            256416,
            256417,
            256418,
            256419,
            256420,
            256421,
            256422
        };

        internal static bool TryGetMeasurementItemId(int answerIndex, out int itemId)
        {
            if (answerIndex < 0 || answerIndex >= MeasurementItemIds.Length)
            {
                itemId = 0;
                return false;
            }

            itemId = MeasurementItemIds[answerIndex];
            return true;
        }

        internal static string ResolveRootNodeId(bool hasPriorOpen)
        {
            return hasPriorOpen ? ReopenRootNodeId : RootNodeId;
        }
    }
}
