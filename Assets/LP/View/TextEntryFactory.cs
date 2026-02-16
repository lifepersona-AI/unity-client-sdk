using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LP
{
    public class TextEntryFactory
    {
        private readonly GameObject _prefab;
        private readonly Transform _parent;
        private readonly Queue<GameObject> _pool = new Queue<GameObject>();

        public TextEntryFactory(GameObject prefab, Transform parent)
        {
            _prefab = prefab;
            _parent = parent;
        }

        public GameObject Create(TextEntry entry)
        {
            GameObject entryObj = _pool.Count > 0
                ? _pool.Dequeue()
                : Object.Instantiate(_prefab, _parent);

            entryObj.SetActive(true);

            var textComponent = entryObj.GetComponentInChildren<TextMeshProUGUI>();
            if (textComponent != null)
            {
                textComponent.text = entry.ToString();
                textComponent.color = GetColorForLabel(entry.Label);
            }
            else
            {
                var text = entryObj.GetComponentInChildren<Text>();
                if (text != null)
                {
                    text.text = entry.ToString();
                    text.color = GetColorForLabel(entry.Label);
                }
            }

            return entryObj;
        }

        public void Return(GameObject entryObj)
        {
            entryObj.SetActive(false);
            _pool.Enqueue(entryObj);
        }

        private Color GetColorForLabel(string label)
        {
            return label switch
            {
                "User" => new Color(0.95f, 0.95f, 0.95f),   // Bright white
                "Agent" => new Color(0.75f, 0.75f, 0.75f),  // Dimmer white
                "System" => new Color(0.6f, 0.8f, 1f),      // Light blue (for connection messages)
                "Error" => new Color(1f, 0.3f, 0.3f),       // Red (for errors)
                _ => Color.white                             // White (fallback)
            };
        }
    }
}
