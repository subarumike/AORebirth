namespace ZoneEngine.Core.Functions.GameFunctions
{
    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    using Utility;

    /// <summary>
    /// FunctionType 53168 (OpenSystemDialog) — Market/GMI browser open from terminal OnUse.
    /// Client typically opens on GenericCmd Use ACK; this registers the function so Event.Perform
    /// does not log "not found" if the server-side OnUse also calls it.
    /// </summary>
    internal class opensystemdialog : FunctionPrototype
    {
        public override FunctionType FunctionId
        {
            get
            {
                // Enum member spelling in AORebirth.Enums is unreliable across builds; use wire id.
                return (FunctionType)53168;
            }
        }

        public override bool Execute(
            INamedEntity self,
            IEntity caller,
            IInstancedEntity target,
            MessagePackObject[] arguments)
        {
            string detail = "(no args)";
            if (arguments != null && arguments.Length > 0)
            {
                try
                {
                    detail = arguments[0].AsString();
                }
                catch
                {
                    detail = arguments[0].ToString();
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.GameFunctions,
                "OpenSystemDialog self="
                + (self != null ? self.Identity.ToString() : "null")
                + " " + detail);

            // Browser open is client-driven after Use ACK for Market terminals.
            return true;
        }
    }
}
