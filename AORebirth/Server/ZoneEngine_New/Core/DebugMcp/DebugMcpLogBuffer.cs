namespace ZoneEngine_New.Core.DebugMcp
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    using NLog;
    using NLog.Config;
    using NLog.Targets;

    /// <summary>Fixed ring of recent zone log lines for the debug MCP server.</summary>
    public sealed class DebugMcpLogBuffer : TargetWithLayout
    {
        public const int Capacity = 256;
        public const int MaxLines = 64;
        const int MaxLineLength = 500;

        readonly Lock _sync = new();
        readonly string[] _lines = new string[Capacity];
        int _start;
        int _count;
        bool _installed;

        public static DebugMcpLogBuffer Shared { get; } = new();

        public DebugMcpLogBuffer()
        {
            Name = "debugMcp";
            Layout = "${longdate}|${level}|${logger}|${message}${onexception:inner=|${exception:format=message}}";
        }

        public void Install()
        {
            lock (_sync)
            {
                if (_installed)
                    return;
                _installed = true;
            }

            LoggingConfiguration config = LogManager.Configuration ?? new LoggingConfiguration();
            if (config.FindTargetByName(Name) == null)
            {
                config.AddTarget(this);
                config.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, this));
                LogManager.Configuration = config;
            }
        }

        public IReadOnlyList<string> Recent(int count)
        {
            if (count < 1)
            {
                count = 1;
            }

            if (count > MaxLines)
            {
                count = MaxLines;
            }

            lock (_sync)
            {
                int take = Math.Min(count, _count);
                var lines = new string[take];
                int index = _start + _count - take;
                for (int i = 0; i < take; i++)
                    lines[i] = _lines[(index + i) % Capacity];
                return lines;
            }
        }

        protected override void Write(LogEventInfo logEvent)
        {
            string rendered;
            try
            {
                rendered = Layout.Render(logEvent);
            }
            catch (Exception)
            {
                return;
            }

            if (rendered.Length > MaxLineLength)
            {
                rendered = rendered.Substring(0, MaxLineLength);
            }

            lock (_sync)
            {
                int slot = (_start + _count) % Capacity;
                _lines[slot] = rendered;
                if (_count == Capacity)
                {
                    _start = (_start + 1) % Capacity;
                }
                else
                {
                    _count++;
                }
            }
        }
    }
}
