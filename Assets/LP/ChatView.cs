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

        [Header("input Buttons")]
        [SerializeField] private Button generateChatButton;
        [SerializeField] private Button clearLogsButton;
        [SerializeField] private Button bootButton;

        private ChatService _chatService;
        private ClientService _clientService;
        private TextEntryFactory _factory;
        private Coroutine _generateCoroutine;

        public void Initialize(ChatService chatService, ClientService clientService)
        {
            _chatService = chatService;
            _clientService = clientService;
            _factory = new TextEntryFactory(textEntryPrefab, contentContainer);

            generateChatButton.onClick.AddListener(GenerateChat);
            clearLogsButton.onClick.AddListener(ClearLogs);
            bootButton.onClick.AddListener(BootLp);

            _chatService.OnTextAdded += HandleTextAdded;

            foreach (var entry in _chatService.Model.Entries)
            {
                DisplayTextEntry(entry);
            }
        }

        private void OnDisable()
        {
            _chatService.OnTextAdded -= HandleTextAdded;
        }

        private void OnDestroy()
        {
            generateChatButton.onClick.RemoveListener(GenerateChat);
            clearLogsButton.onClick.RemoveListener(ClearLogs);
            bootButton.onClick.RemoveListener(BootLp);
        }

        private void HandleTextAdded(TextEntry entry)
        {
            DisplayTextEntry(entry);

            if (autoScrollToBottom)
            {
                // TODO - is this call below a good one?
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

            _chatService.Clear();
        }

        private void GenerateChat()
        {
            if (_generateCoroutine != null)
            {
                StopCoroutine(_generateCoroutine);
            }

            _generateCoroutine = StartCoroutine(GenerateChatCoroutine());
        }

        private IEnumerator GenerateChatCoroutine()
        {
            string[] messages = _chatService.GetFunnyMessages();

            for (int i = 0; i < 20 && i < messages.Length; i++)
            {
                _chatService.AddText(messages[i], "Generated");
                yield return new WaitForSeconds(messageInterval);
            }

            _generateCoroutine = null;
        }

        private async void BootLp()
        {
            try
            {
                string bodyMessage = "{\"userId\": \"john doe\"}";
                string url = "https://api.lifepersona.ai/api/client/boot";

                await _clientService.PostRequestAsync(bodyMessage, url);
            }
            catch (Exception e)
            {
                throw; // TODO handle exception
            }
        }
    }
}
