namespace ZoneEngine_New.Core.Logging
{
    using System;
    using System.Globalization;

    using NLog;

    /// <summary>
    /// Playfield-scoped logger: same NLog pipeline, named Playfield.{id}, open for extra config later.
    /// </summary>
    public sealed class PlayfieldZoneLogger : IZoneLogger
    {
        private readonly Logger _logger;

        public PlayfieldZoneLogger(Logger logger, int playfieldId)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(playfieldId);

            _logger = logger;
            PlayfieldId = playfieldId;
        }

        public int PlayfieldId { get; }

        public void Debug(string message) => _logger.Debug(Prefix(message));

        public void Info(string message) => _logger.Info(Prefix(message));

        public void Warn(string message) => _logger.Warn(Prefix(message));

        public void Error(string message) => _logger.Error(Prefix(message));

        public void Error(Exception exception, string message) => _logger.Error(exception, Prefix(message));

        public IZoneLogger CreateForPlayfield(int id)
        {
            string name = string.Format(CultureInfo.InvariantCulture, "Playfield.{0}", id);
            return new PlayfieldZoneLogger(LogManager.GetLogger(name), id);
        }

        private string Prefix(string? message) =>
            string.Format(
                CultureInfo.InvariantCulture,
                "[pf={0}] {1}",
                PlayfieldId,
                message ?? string.Empty);
    }
}
