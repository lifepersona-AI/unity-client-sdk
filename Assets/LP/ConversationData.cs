using System;
using System.Collections.Generic;

namespace LP
{
    [Serializable]
    public class ConversationData
    {
        private readonly List<TextEntry> _entries = new List<TextEntry>();

        public IReadOnlyList<TextEntry> Entries => _entries;

        public void AddEntry(TextEntry entry)
        {
            _entries.Add(entry);
        }

        public void Clear()
        {
            _entries.Clear();
        }
    }
}
