using System;
using System.Collections.Generic;

namespace LP
{
    public class LogManager
    {
        private static LogManager _instance;
        public static LogManager Instance => _instance ??= new LogManager();

        private readonly List<LogEntry> _logs = new List<LogEntry>();

        public event Action<LogEntry> OnLogAdded;

        public IReadOnlyList<LogEntry> Logs => _logs;

        private LogManager() { }

        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            var entry = new LogEntry(message, level);
            _logs.Add(entry);
            OnLogAdded?.Invoke(entry);
        }

        public void Clear()
        {
            _logs.Clear();
        }
    }
}
