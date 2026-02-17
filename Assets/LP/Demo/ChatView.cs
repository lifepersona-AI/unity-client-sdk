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
        [SerializeField] private Button disconnectButton;

        [Header("Input")]
        [SerializeField] private Button sendButton;
        [SerializeField] private TMP_InputField messageInputField;

        private TextEntryFactory _factory;

        private void Start()
        {
            _factory = new TextEntryFactory(textEntryPrefab, contentContainer);

            // Subscribe to button events
            clearLogsButton.onClick.AddListener(ClearLogs);
            bootButton.onClick.AddListener(Connect);
            sendButton.onClick.AddListener(SendMessage);
            disconnectButton.onClick.AddListener(Disconnect);

            // Subscribe to SDK UnityEvents
            if (LifePersonaSDK.Instance != null)
            {
                LifePersonaSDK.Instance.OnConversationStartedEvent.AddListener(OnConversationStarted);
                LifePersonaSDK.Instance.OnConversationDisconnectedEvent.AddListener(OnConversationDisconnected);
                LifePersonaSDK.Instance.OnAgentTranscript.AddListener(OnAgentMessage);
                LifePersonaSDK.Instance.OnUserTranscript.AddListener(OnUserMessage);
            }

            // Set initial UI state
            SetConnectionState(false);

            SetMobileConfig();
        }

        private void SetMobileConfig()
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            Screen.orientation = ScreenOrientation.Portrait;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;

            Application.targetFrameRate = 60;

            QualitySettings.vSyncCount = 0;

            Application.runInBackground = true;

            Input.multiTouchEnabled = false;
        }

        private void OnDisable()
        {
            if (LifePersonaSDK.Instance != null)
            {
                LifePersonaSDK.Instance.OnConversationStartedEvent.RemoveListener(OnConversationStarted);
                LifePersonaSDK.Instance.OnConversationDisconnectedEvent.RemoveListener(OnConversationDisconnected);
                LifePersonaSDK.Instance.OnAgentTranscript.RemoveListener(OnAgentMessage);
                LifePersonaSDK.Instance.OnUserTranscript.RemoveListener(OnUserMessage);
            }
        }

        private void OnDestroy()
        {
            clearLogsButton.onClick.RemoveListener(ClearLogs);
            bootButton.onClick.RemoveListener(Connect);
            sendButton.onClick.RemoveListener(SendMessage);
            disconnectButton.onClick.RemoveListener(Disconnect);
        }

        // Event Handlers
        private void OnConversationStarted()
        {
            DisplaySystemMessage("Conversation started");
            SetConnectionState(true);
        }

        private void OnConversationDisconnected()
        {
            DisplaySystemMessage("Conversation disconnected");
            SetConnectionState(false);
        }

        private void OnAgentMessage(string message)
        {
            DisplayTextEntry(new TextEntry(message, "Agent"));
            ScrollToBottom();
        }

        private void OnUserMessage(string message)
        {
            DisplayTextEntry(new TextEntry(message, "User"));
            ScrollToBottom();
        }

        // UI Methods
        private void DisplayTextEntry(TextEntry entry)
        {
            _factory.Create(entry);
        }

        private void DisplaySystemMessage(string message)
        {
            DisplayTextEntry(new TextEntry(message, "System"));
            ScrollToBottom();
        }

        private void ScrollToBottom()
        {
            if (autoScrollToBottom)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private void SetConnectionState(bool connected)
        {
            bootButton.interactable = !connected;
            sendButton.interactable = connected;
            disconnectButton.interactable = connected;
            messageInputField.interactable = connected;
        }

        // Button Callbacks
        private void Connect()
        {
            if (LifePersonaSDK.Instance == null)
            {
                Debug.LogError("LifePersonaSDK instance not found!");
                return;
            }

            DisplaySystemMessage("Booting SDK...");
            LifePersonaSDK.Instance.Init();
        }

        private void ClearLogs()
        {
            foreach (Transform child in contentContainer)
            {
                _factory.Return(child.gameObject);
            }
        }

        private void SendMessage()
        {
            if (LifePersonaSDK.Instance == null)
            {
                Debug.LogError("LifePersonaSDK instance not found!");
                return;
            }

            string message = messageInputField.text;

            if (string.IsNullOrWhiteSpace(message))
            {
                Debug.LogWarning("Cannot send empty message");
                return;
            }

            LifePersonaSDK.Instance.SendText(message);
            messageInputField.text = string.Empty;
            messageInputField.ActivateInputField();
        }

        private void Disconnect()
        {
            if (LifePersonaSDK.Instance == null)
            {
                Debug.LogError("LifePersonaSDK instance not found!");
                return;
            }

            LifePersonaSDK.Instance.Disconnect();
        }
    }
}
