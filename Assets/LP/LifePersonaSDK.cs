using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace LP
{
    public enum SdkState
    {
        Uninitialized,
        Initialized,
        Connecting,
        Connected,
        Disconnected
    }

    public class LifePersonaSDK : MonoBehaviour
    {
        [Header("Setup")]
        [SerializeField] private string appKey;

        [Space(10)]
        [SerializeField] private bool textOnlyMode = true;
        [SerializeField] private bool verboseLogging = false;

        [Header("Audio Components (optional - for voice mode)")]
        [SerializeField] private PcmAudioPlayer audioPlayer;
        [SerializeField] private MicrophoneStreamer microphoneStreamer;

        [Header("Events")]
        public UnityEvent OnConversationStartedEvent;
        public UnityEvent OnConversationDisconnectedEvent;
        public UnityEvent<string> OnAgentTranscript;
        public UnityEvent<string> OnUserTranscript;
        public UnityEvent<string> OnError;

        private HttpService _httpService;
        private WebSocketService _webSocketService;
        private ConversationController _conversationController;
        private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();
        private SdkState _state = SdkState.Uninitialized;

        public static LifePersonaSDK Instance { get; private set; }
        public SdkState State => _state;
        public static bool VerboseLogging => Instance != null && Instance.verboseLogging;
        private static string baseUrl = "https://api.lifepersona.ai/api/client/";

        private string userId;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Update()
        {
            while (_mainThreadQueue.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LifePersonaSDK] Main thread action failed: {ex}");
                }
            }
        }

        internal void EnqueueOnMainThread(Action action)
        {
            _mainThreadQueue.Enqueue(action);
        }

        private void RequireState(SdkState minimumState, string operation)
        {
            if (_state < minimumState)
            {
                var msg = $"SDK state is {_state}, requires at least {minimumState}";
                EmitError(operation, msg);
                throw new InvalidOperationException($"Cannot {operation}: {msg}");
            }
        }

        private void EmitError(string operation, string reason)
        {
            string message = $"{operation}: {reason}";
            Debug.LogError($"[LifePersonaSDK] {message}");
            OnError?.Invoke(message);
        }

        private void EmitError(string operation, Exception ex) => EmitError(operation, ex.Message);

        // ===== Lifecycle =====

        public void Initialize(string userId)
        {
            if (_state != SdkState.Uninitialized && _state != SdkState.Disconnected)
            {
                Debug.LogWarning($"[LifePersonaSDK] Initialize called in state {_state}. Ignoring.");
                return;
            }

            _httpService = new HttpService(appKey);
            _webSocketService = new WebSocketService();
            this.userId = userId;

            _state = SdkState.Initialized;
        }

        public async Task StartConversation()
        {
            RequireState(SdkState.Initialized, "StartConversation");
            if (_state == SdkState.Connecting || _state == SdkState.Connected)
            {
                throw new InvalidOperationException("Already connecting/connected");
            }

            _state = SdkState.Connecting;
            try
            {
                CreateConversationController();
                await _conversationController.StartConversation(this.userId, baseUrl);
                _state = SdkState.Connected;
                OnConversationStartedEvent?.Invoke();
                Debug.Log("Conversation started successfully");
            }
            catch (Exception ex)
            {
                CleanupConversationController();
                _state = SdkState.Initialized;
                EmitError("StartConversation", ex);
                throw;
            }
        }

        public async Task SendText(string message)
        {
            RequireState(SdkState.Connected, "SendText");

            try
            {
                await _conversationController.SendText(message);
            }
            catch (Exception ex)
            {
                EmitError("SendText", ex);
                throw;
            }
        }

        public void EndConversation()
        {
            if (_state != SdkState.Connected && _state != SdkState.Connecting)
            {
                Debug.LogWarning($"[LifePersonaSDK] EndConversation called in state {_state}. Ignoring.");
                return;
            }

            CleanupConversationController();

            _webSocketService?.DisconnectAsync().ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Debug.LogError($"[LifePersonaSDK] WebSocket disconnect error: {t.Exception}");
            });

            audioPlayer?.StopImmediately();

            _state = SdkState.Initialized;
            OnConversationDisconnectedEvent?.Invoke();
        }

        // ===== Trigger API =====

        public async Task<HttpService.ActiveTriggerResponse[]> GetActiveTriggers()
        {
            RequireState(SdkState.Initialized, "GetActiveTriggers");

            try
            {
                return await _httpService.GetActiveTriggersAsync(this.userId, baseUrl + "active-triggers");
            }
            catch (Exception ex)
            {
                EmitError("GetActiveTriggers", ex);
                throw;
            }
        }

        public async Task ActivateTrigger(string activeTriggerIdParam)
        {
            RequireState(SdkState.Initialized, "ActivateTrigger");
            if (_state == SdkState.Connecting || _state == SdkState.Connected)
            {
                throw new InvalidOperationException("Already connecting/connected");
            }

            _state = SdkState.Connecting;
            try
            {
                CreateConversationController();
                await _conversationController.StartConversationWithTrigger(
                    this.userId, baseUrl, activeTriggerIdParam);
                _state = SdkState.Connected;
                OnConversationStartedEvent?.Invoke();
                Debug.Log("Trigger conversation started successfully");
            }
            catch (Exception ex)
            {
                CleanupConversationController();
                _state = SdkState.Initialized;
                EmitError("ActivateTrigger", ex);
                throw;
            }
        }

        public void Disconnect()
        {
            if (_state == SdkState.Connected || _state == SdkState.Connecting)
            {
                EndConversation();
            }

            _httpService = null;
            _webSocketService = null;

            _state = SdkState.Disconnected;
        }

        private void CreateConversationController()
        {
            _conversationController = new ConversationController(
                _httpService,
                _webSocketService,
                audioPlayer,
                microphoneStreamer,
                textOnlyMode,
                action => EnqueueOnMainThread(action));

            _conversationController.OnAgentTranscript += OnAgentTranscriptReceived;
            _conversationController.OnUserTranscript += OnUserTranscriptReceived;
        }

        private void CleanupConversationController()
        {
            if (_conversationController != null)
            {
                _conversationController.OnAgentTranscript -= OnAgentTranscriptReceived;
                _conversationController.OnUserTranscript -= OnUserTranscriptReceived;
                _conversationController.Dispose();
                _conversationController = null;
            }
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

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            Disconnect();
        }
    }
}
