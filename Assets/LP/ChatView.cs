using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LP
{
    public class ChatView : MonoBehaviour
    {
        [SerializeField] private Transform contentContainer;
        [SerializeField] private GameObject textEntryPrefab;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private bool autoScrollToBottom = true;

        [Header("Buttons")]
        [SerializeField] private Button clearLogsButton;
        [SerializeField] private Button bootButton;
        [SerializeField] private Button sendButton;
        [SerializeField] private Button disconnectButton;

        [Header("Input")]
        [SerializeField] private TMP_InputField messageInputField;

        private ChatController _chatController;
        private TextEntryFactory _factory;

        public void Initialize(ChatController chatController)
        {
            _chatController = chatController;
            _factory = new TextEntryFactory(textEntryPrefab, contentContainer);

            clearLogsButton.onClick.AddListener(ClearLogs);
            bootButton.onClick.AddListener(BootLp);
            sendButton.onClick.AddListener(SendMessage);
            disconnectButton.onClick.AddListener(Disconnect);

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
            sendButton.onClick.RemoveListener(SendMessage);
            disconnectButton.onClick.RemoveListener(Disconnect);
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

            _chatController.StartConversation(userId, url).Forget();
        }

        private void SendMessage()
        {
            string message = messageInputField.text;

            if (string.IsNullOrWhiteSpace(message))
            {
                Debug.LogWarning("Cannot send empty message");
                return;
            }

            _chatController.SendMessage(message).Forget();
            messageInputField.text = string.Empty;
            messageInputField.ActivateInputField();
        }

        private void Disconnect()
        {
            _chatController.Disconnect().Forget();
        }
    }
}
