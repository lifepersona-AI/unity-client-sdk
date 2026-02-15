using UnityEngine;
using UnityEngine.Events;

namespace LP
{
    public class LPSDKSetup : MonoBehaviour
    {
        //TODO - expose LP SDK Key and user id here
        
        [Header("Setup")]
        [SerializeField] private string appKey;
        [SerializeField] private string userId = "Yoav the true king";
        
        private static string baseUrl = "https://api.lifepersona.ai/api/client/";

        [Space(10)]
        [SerializeField] private bool bootOnAwake = true;

        [Header("Events")]
        public UnityEvent OnConversationStartedEvent; 
        
        [Header("View")]
        [SerializeField] private ChatView chatView;
        
        private ConversationData _conversationData;
        private HttpService _httpService;
        private WebSocketService _webSocketService;
        private ConversationController _conversationController;
        
        public static LPSDKSetup Instance { get; private set; }
        
        public void Init()
        {
            BootSDK();
        }
        
        private void Awake()
        {
            if (!bootOnAwake)
                return;
            
            BootSDK();
        }

        private void BootSDK()
        {
            // Initialize data holder - make in optional
            _conversationData = new ConversationData();

            // Initialize services
            _httpService = new HttpService();
            _webSocketService = new WebSocketService();

            // Initialize controller (orchestrator)
            _conversationController = new ConversationController(_conversationData, _httpService, _webSocketService);
            _conversationController.StartConversation(userId, baseUrl, OnConversationStarted);
            
            // Initialize view with controller
            chatView.Initialize(_conversationController);
        }

        private void OnConversationStarted(bool success)
        {
            if (!success)
            {
                Debug.LogError("Failed to start conversation");
                return;
            }
            
            OnConversationStartedEvent.Invoke();
            Debug.Log("Conversation started successfully");
        }

        private void OnDestroy()
        {
            _conversationController?.Dispose();
        }

        private void HandleUnityLog(string message, string stackTrace, LogType type)
        {
            _conversationController.AddText(message, "UnityLog");
        }
    }
}
