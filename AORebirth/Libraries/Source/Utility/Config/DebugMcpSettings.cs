namespace Utility.Config
{
    /// <summary>
    /// Localhost Model Context Protocol server for inspecting live ZoneEngine state.
    /// Defaults to off. ListenIP must be a loopback address when Enabled is true.
    /// </summary>
    public class DebugMcpSettings
    {
        public bool Enabled { get; set; }

        public string ListenIP { get; set; }

        public int Port { get; set; }
    }
}
