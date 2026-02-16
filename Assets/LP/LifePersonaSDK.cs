using System;
using UnityEngine;
using UnityEngine.Events;

namespace LP
{
    public class LifePersonaSDK : MonoBehaviour
    {
        [Header("Setup")]
        [SerializeField] private string appKey;
        [SerializeField] private string userId;
        
        [Space(10)]
        [SerializeField] private bool bootOnAwake = true;
        [SerializeField] private bool textOnlyMode = true;

        [Header("Audio Components (optional - for voice mode)")]
        [SerializeField] private PcmAudioPlayer audioPlayer;
        [SerializeField] private MicrophoneStreamer microphoneStreamer;

        [Header("Events")]
        public UnityEvent OnConversationStartedEvent;
        public UnityEvent OnConversationDisconnectedEvent;
        public UnityEvent<string> OnAgentTranscript;
        public UnityEvent<string> OnUserTranscript;
        
        private HttpService _httpService;
        private WebSocketService _webSocketService;
        private ConversationController _conversationController;
        
        public static LifePersonaSDK Instance { get; private set; }
        private static string baseUrl = "https://api.lifepersona.ai/api/client/";
        
        private void Awake()
        {
            if (!bootOnAwake)
                return;
            
            BootSDK();
        }
        
        public void Init() => BootSDK();
        
        private void BootSDK()
        {
            // Initialize services
            _httpService = new HttpService();
            _webSocketService = new WebSocketService();

            // Initialize controller
            _conversationController = new ConversationController(
                _httpService,
                _webSocketService,
                audioPlayer,
                microphoneStreamer,
                textOnlyMode);

            // Subscribe to controller events
            _conversationController.OnAgentTranscript += OnAgentTranscriptReceived;
            _conversationController.OnUserTranscript += OnUserTranscriptReceived;

            _conversationController.StartConversation(userId, baseUrl, OnConversationStarted).Forget();
        }
        
        public void SendMassage(string message)
        {
            try
            {
                _conversationController.SendMessage(message).Forget();
            }
            catch (Exception e)
            {
                Debug.Log(e);
                throw;
            }
            
            OnUserTranscript.Invoke(message);
        }

        public void Disconnect()
        {
            // Unsubscribe from controller events
            if (_conversationController != null)
            {
                _conversationController.OnAgentTranscript -= OnAgentTranscriptReceived;
                _conversationController.OnUserTranscript -= OnUserTranscriptReceived;
                _conversationController.Dispose();
            }

            _webSocketService?.DisconnectAsync().Forget();
            OnConversationDisconnectedEvent?.Invoke();
        }
        
        private void OnAgentTranscriptReceived(string transcript)
        {
            Debug.Log($"Agent: {transcript}");
            OnAgentTranscript?.Invoke(transcript);
        }

        private void OnUserTranscriptReceived(string transcript)
        {
            Debug.Log($"User: {transcript}");
            OnUserTranscript?.Invoke(transcript);
        }

        private void OnConversationStarted(bool success)
        {
            if (!success)
            {
                Debug.LogError("Failed to start conversation");
                return;
            }
            
            OnConversationStartedEvent?.Invoke();
            Debug.Log("Conversation started successfully");
        }

        private void OnDestroy()
        {
            Disconnect();
        }
    }
}
