using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LP
{
    public class ChatView : MonoBehaviour
    {
        [SerializeField] private string userId;
        [Space(10)]
        [SerializeField] private Transform contentContainer;
        [SerializeField] private GameObject textEntryPrefab;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private bool autoScrollToBottom = true;

        [Header("Buttons")]
        [SerializeField] private Button activateTriggerButton;
        [SerializeField] private Button startConversationButton;
        [SerializeField] private Button endConversationButton;

        [Header("Input")]
        [SerializeField] private Button sendButton;
        [SerializeField] private TMP_InputField messageInputField;

        private TextEntryFactory _factory;

        public static ChatView Instance { get; private set; }

        private void Start()
        {
            LifePersonaSDK.Instance.Initialize(userId);

            _factory = new TextEntryFactory(textEntryPrefab, contentContainer);

            // Subscribe to button events
            activateTriggerButton.onClick.AddListener(ActivateTrigger);
            startConversationButton.onClick.AddListener(StartConversation);
            sendButton.onClick.AddListener(SendMessage);
            endConversationButton.onClick.AddListener(EndConversation);

            // Subscribe to SDK UnityEvents
            if (LifePersonaSDK.Instance != null)
            {
                LifePersonaSDK.Instance.OnConversationStartedEvent.AddListener(OnConversationStarted);
                LifePersonaSDK.Instance.OnConversationDisconnectedEvent.AddListener(OnConversationDisconnected);
                LifePersonaSDK.Instance.OnAgentTranscript.AddListener(OnAgentMessage);
                LifePersonaSDK.Instance.OnUserTranscript.AddListener(OnUserMessage);
                LifePersonaSDK.Instance.OnError.AddListener(OnError);
            }

            // Set initial UI state
            SetConnectionState(false);

            SetMobileConfig();

            DisplaySystemMessage("SDK Initialized. Ready to start conversations!");

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
                LifePersonaSDK.Instance.OnError.RemoveListener(OnError);
            }
        }

        private void OnDestroy()
        {
            activateTriggerButton.onClick.RemoveListener(ActivateTrigger);
            startConversationButton.onClick.RemoveListener(StartConversation);
            sendButton.onClick.RemoveListener(SendMessage);
            endConversationButton.onClick.RemoveListener(EndConversation);
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

        private void OnError(string errorMessage)
        {
            DisplayTextEntry(new TextEntry(errorMessage, "Error"));
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
            startConversationButton.interactable = !connected;
            sendButton.interactable = connected;
            endConversationButton.interactable = connected;
            messageInputField.interactable = connected;
        }

        // Button Callbacks
        private async void StartConversation()
        {
            if (LifePersonaSDK.Instance == null)
            {
                Debug.LogError("LifePersonaSDK instance not found!");
                return;
            }

            ClearLogs();
            DisplaySystemMessage("Connecting...");
            try
            {
                await LifePersonaSDK.Instance.StartConversation();
            }
            catch (Exception ex)
            {
                Debug.LogError($"StartConversation failed: {ex.Message}");
            }
        }

        private void ClearLogs()
        {
            foreach (Transform child in contentContainer)
            {
                _factory.Return(child.gameObject);
            }
        }

        private async void SendMessage()
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

            messageInputField.text = string.Empty;
            messageInputField.ActivateInputField();

            try
            {
                await LifePersonaSDK.Instance.SendText(message);
            }
            catch (Exception ex)
            {
                Debug.LogError($"SendText failed: {ex.Message}");
            }
        }

        private void EndConversation()
        {
            if (LifePersonaSDK.Instance == null)
            {
                Debug.LogError("LifePersonaSDK instance not found!");
                return;
            }

            LifePersonaSDK.Instance.EndConversation();
        }

        private async void ActivateTrigger()
        {
            if (LifePersonaSDK.Instance == null)
            {
                Debug.LogError("LifePersonaSDK instance not found!");
                return;
            }

            try
            {
                var triggers = await LifePersonaSDK.Instance.GetActiveTriggers();

                if (triggers.Length > 0)
                {
                    Debug.Log($"Found {triggers.Length} active triggers. Activating first: {triggers[0].name}");
                    ClearLogs();
                    DisplaySystemMessage("Connecting...");
                    await LifePersonaSDK.Instance.ActivateTrigger(triggers[0].id);
                    Debug.Log($"Trigger '{triggers[0].name}' activated successfully");
                }
                else
                {
                    DisplaySystemMessage("No active triggers found");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"ActivateTrigger failed: {ex.Message}");
            }
        }
    }
}
