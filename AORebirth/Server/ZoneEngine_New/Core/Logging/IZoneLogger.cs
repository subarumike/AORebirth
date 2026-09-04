namespace ZoneEngine_New.Core.Logging
{
    using System;

    public interface IZoneLogger
    {
        void Debug(string message);

        void Info(string message);

        void Warn(string message);

        void Error(string message);

        void Error(Exception exception, string message);

        IZoneLogger CreateForPlayfield(int playfieldId);
    }
}
