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

        public string[] GetFunnyMessages()
        {
            return new[]
            {
                "Why did the Unity developer break up? Too many null references!",
                "I tried to catch some fog earlier. I mist.",
                "Debugging is like being a detective in a crime movie where you're also the murderer.",
                "99 little bugs in the code, 99 little bugs. Take one down, patch it around, 127 little bugs in the code.",
                "My code doesn't work, I have no idea why. My code works, I have no idea why.",
                "I would love to change the world, but they won't give me the source code.",
                "A SQL query walks into a bar, walks up to two tables and asks... 'Can I join you?'",
                "There are 10 types of people: those who understand binary and those who don't.",
                "Programmer: A machine that turns coffee into code.",
                "Why do programmers prefer dark mode? Because light attracts bugs!",
                "How many programmers does it take to change a light bulb? None, that's a hardware problem.",
                "I'm not lazy, I'm just on energy-saving mode.",
                "Why did the developer go broke? Because he used up all his cache.",
                "Life would be much easier if I had the source code.",
                "Programming is 10% writing code and 90% figuring out why it doesn't work.",
                "To err is human, to really mess things up requires a computer.",
                "I speak fluent sarcasm and broken code.",
                "Coffee: the official sponsor of 'just one more compile'.",
                "My code is compiling... time to check social media!",
                "Documentation? That's future me's problem."
            };
        }
    }
}
