namespace ZoneEngine_New.Core.Logging
{
    using System;
    using System.Globalization;

    using NLog;

    public sealed class NLogZoneLogger : IZoneLogger
    {
        private readonly Logger _logger;

        public NLogZoneLogger()
            : this(LogManager.GetLogger("ZoneEngine_New"))
        {
        }

        public NLogZoneLogger(Logger logger)
        {
            ArgumentNullException.ThrowIfNull(logger);

            _logger = logger;
        }

        public void Debug(string message) => _logger.Debug(message);

        public void Info(string message) => _logger.Info(message);

        public void Warn(string message) => _logger.Warn(message);

        public void Error(string message) => _logger.Error(message);

        public void Error(Exception exception, string message) => _logger.Error(exception, message);

        public IZoneLogger CreateForPlayfield(int playfieldId)
        {
            string name = string.Format(CultureInfo.InvariantCulture, "Playfield.{0}", playfieldId);
            return new PlayfieldZoneLogger(LogManager.GetLogger(name), playfieldId);
        }
    }
}
