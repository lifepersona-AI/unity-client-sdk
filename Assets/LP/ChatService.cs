using System;

namespace LP
{
    public class ChatService
    {
        private readonly ChatModel _model;

        public event Action<TextEntry> OnTextAdded;

        public ChatModel Model => _model;

        public ChatService(ChatModel model)
        {
            _model = model;
        }

        public void AddText(string message, string label = "Chat")
        {
            var entry = new TextEntry(message, label);
            _model.AddEntry(entry);
            OnTextAdded?.Invoke(entry);
        }

        public void Clear()
        {
            _model.Clear();
        }
    }
}
