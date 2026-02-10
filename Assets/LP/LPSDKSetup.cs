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
        private HttpService _httpService;
        private WebSocketService _webSocketService;
        private ChatController _chatController;

        private void Awake()
        {
            // Initialize model
            _chatModel = new ChatModel();

            // Initialize services
            _httpService = new HttpService();
            _webSocketService = new WebSocketService();

            // Initialize controller (orchestrator)
            _chatController = new ChatController(_chatModel, _httpService, _webSocketService);

            // Initialize view with controller
            chatView.Initialize(_chatController);

            // Hook up Unity log capture
            if (captureUnityLogs)
            {
                Application.logMessageReceived += HandleUnityLog;
            }
        }

        private void OnDestroy()
        {
            if (captureUnityLogs)
            {
                Application.logMessageReceived -= HandleUnityLog;
            }

            _chatController?.Dispose();
        }

        private void HandleUnityLog(string message, string stackTrace, LogType type)
        {
            _chatController.AddText(message, "UnityLog");
        }
    }
}
