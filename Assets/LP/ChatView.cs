using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LP
{
    public class ChatView : MonoBehaviour
    {
        [SerializeField] private Transform contentContainer;
        [SerializeField] private GameObject textEntryPrefab;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private bool autoScrollToBottom = true;
        [SerializeField] private float messageInterval = 2.5f;

        [Header("Buttons")]
        [SerializeField] private Button clearLogsButton;
        [SerializeField] private Button bootButton;

        private ChatController _chatController;
        private TextEntryFactory _factory;

        public void Initialize(ChatController chatController)
        {
            _chatController = chatController;
            _factory = new TextEntryFactory(textEntryPrefab, contentContainer);

            clearLogsButton.onClick.AddListener(ClearLogs);
            bootButton.onClick.AddListener(BootLp);

            _chatController.OnTextAdded += HandleTextAdded;

            foreach (var entry in _chatController.Model.Entries)
            {
                DisplayTextEntry(entry);
            }
        }

        private void OnDisable()
        {
            if (_chatController != null)
            {
                _chatController.OnTextAdded -= HandleTextAdded;
            }
        }

        private void OnDestroy()
        {
            clearLogsButton.onClick.RemoveListener(ClearLogs);
            bootButton.onClick.RemoveListener(BootLp);
        }

        private void HandleTextAdded(TextEntry entry)
        {
            DisplayTextEntry(entry);

            if (autoScrollToBottom)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private void DisplayTextEntry(TextEntry entry)
        {
            _factory.Create(entry);
        }

        private void ClearLogs()
        {
            foreach (Transform child in contentContainer)
            {
                _factory.Return(child.gameObject);
            }

            _chatController.Clear();
        }

        private void BootLp()
        {
            string userId = "Yoav the king";
            string url = "https://api.lifepersona.ai/api/client/boot";

            _chatController.BootConversation(userId, url).Forget();
        }
    }
}
