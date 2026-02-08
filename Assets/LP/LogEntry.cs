using System;

namespace LP
{
    public class LogEntry
    {
        public string Message { get; }
        public DateTime Timestamp { get; }
        public LogLevel Level { get; }

        public LogEntry(string message, LogLevel level = LogLevel.Info)
        {
            Message = message;
            Timestamp = DateTime.Now;
            Level = level;
        }

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss}] [{Level}] {Message}";
        }
    }

    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }
}
