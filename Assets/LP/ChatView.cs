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

        private ConversationController _conversationController;
        private TextEntryFactory _factory;

        public void Initialize(ConversationController conversationController)
        {
            _conversationController = conversationController;
            _factory = new TextEntryFactory(textEntryPrefab, contentContainer);

            clearLogsButton.onClick.AddListener(ClearLogs);
            sendButton.onClick.AddListener(SendMessage);
            disconnectButton.onClick.AddListener(Disconnect);

            _conversationController.OnTextAdded += HandleTextAdded;

            foreach (var entry in _conversationController.Model.Entries)
            {
                DisplayTextEntry(entry);
            }
        }

        private void OnDisable()
        {
            if (_conversationController != null)
            {
                _conversationController.OnTextAdded -= HandleTextAdded;
            }
        }

        private void OnDestroy()
        {
            clearLogsButton.onClick.RemoveListener(ClearLogs);
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

            _conversationController.Clear();
        }

        private void SendMessage()
        {
            string message = messageInputField.text;

            if (string.IsNullOrWhiteSpace(message))
            {
                Debug.LogWarning("Cannot send empty message");
                return;
            }

            _conversationController.SendMessage(message).Forget();
            messageInputField.text = string.Empty;
            messageInputField.ActivateInputField();
        }

        private void Disconnect()
        {
            _conversationController.Disconnect().Forget();
        }
    }
}
