namespace ZoneEngine.Core.MessageHandlers
{
    public enum GridTerminalInteractionRouteMode
    {
        None,
        CapturedGridTerminal,
        GridEnterTerminal
    }

    public static class GridTerminalInteractionRules
    {
        public const int CapturedGridPlayfieldId = 152;

        public const int GridEnterTerminalTemplateId = 95350;

        public const int GridExitTerminalTemplateId = 95351;

        public const float GridDestinationTerminalClearance = 2.5f;

        public const int CapturedBorealisGridTerminalInstance = unchecked((int)0xC0040320);

        public static GridTerminalInteractionRouteMode ResolveRouteMode(
            bool capturedGridTerminalRouteMatched,
            bool gridEnterTerminalMatched)
        {
            if (capturedGridTerminalRouteMatched)
            {
                return GridTerminalInteractionRouteMode.CapturedGridTerminal;
            }

            return gridEnterTerminalMatched
                       ? GridTerminalInteractionRouteMode.GridEnterTerminal
                       : GridTerminalInteractionRouteMode.None;
        }
    }
}
