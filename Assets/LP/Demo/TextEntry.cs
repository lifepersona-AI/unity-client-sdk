using System;

namespace LP
{
    [Serializable]
    public class TextEntry
    {
        public string Message { get; }
        public DateTime Timestamp { get; }
        public string Label { get; }

        public TextEntry(string message, string label = "Chat")
        {
            Message = TextUtility.RemoveEmojis(message);
            Timestamp = DateTime.Now;
            Label = label;
        }

        public override string ToString()
        {
            return $"[{Label}] {Message}";
        }
    }
}
