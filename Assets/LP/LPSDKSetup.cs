using UnityEngine;

namespace LP
{
    public class LPSDKSetup : MonoBehaviour
    {
        [Header("View")]
        [SerializeField] private ChatView chatView;
        

        [Header("Settings")]
        [SerializeField] private bool captureUnityLogs = true;

        private ChatModel _chatModel;
        private ChatService _chatService;
        private ClientService _clientService;

        public ChatService ChatService => _chatService;
        public ClientService ClientService => _clientService;

        private void Awake()
        {
            // Initialize model and services
            _chatModel = new ChatModel();
            _chatService = new ChatService(_chatModel);
            _clientService = new ClientService();

            // Wire ClientService events to ChatService (Composition Root Pattern)
            _clientService.OnMessageReceived += HandleMessageReceived;

            // Initialize view with services
            chatView.Initialize(_chatService, _clientService);

            // Hook up Unity log capture
            if (captureUnityLogs)
            {
                Application.logMessageReceived += HandleUnityLog;
            }
        }

        private void Update()
        {
            // Dispatch WebSocket events
            _clientService?.Update();
        }

        private void OnDestroy()
        {
            if (captureUnityLogs)
            {
                Application.logMessageReceived -= HandleUnityLog;
            }

            if (_clientService != null)
            {
                _clientService.OnMessageReceived -= HandleMessageReceived;
            }
        }

        private void HandleUnityLog(string message, string stackTrace, LogType type)
        {
            _chatService.AddText(message, "UnityLog");
        }

        private void HandleMessageReceived(string message)
        {
            _chatService.AddText(message, "Server");
        }
    }
}
