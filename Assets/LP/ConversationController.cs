using System;
using System.Threading.Tasks;
using UnityEngine;

namespace LP
{
    public class ConversationController
    {
        public event Action<string> OnAgentTranscript;
        public event Action<string> OnUserTranscript;

        private readonly HttpService _httpService;
        private readonly WebSocketService _webSocketService;
        private readonly PcmAudioPlayer _audioPlayer;
        private readonly MicrophoneStreamer _micStreamer;
        private readonly bool _textOnlyMode;
        private readonly Action<Action> _dispatchToMainThread;
        private volatile bool _conversationReady;

        public ConversationController(
            HttpService httpService,
            WebSocketService webSocketService,
            PcmAudioPlayer audioPlayer = null,
            MicrophoneStreamer micStreamer = null,
            bool textOnlyMode = true,
            Action<Action> dispatchToMainThread = null)
        {
            _httpService = httpService;
            _webSocketService = webSocketService;
            _audioPlayer = audioPlayer;
            _micStreamer = micStreamer;
            _textOnlyMode = textOnlyMode;
            _dispatchToMainThread = dispatchToMainThread ?? (a => a());

            // Subscribe to incoming WebSocket messages
            _webSocketService.OnMessageReceived += HandleMessageReceivedFromBackground;

            // Subscribe to microphone audio chunks if available
            if (_micStreamer != null && !_textOnlyMode)
            {
                _micStreamer.OnAudioChunk += OnMicAudioChunk;
            }

            // Subscribe to audio player events for echo cancellation (mic muting)
            if (_audioPlayer != null && _micStreamer != null && !_textOnlyMode)
            {
                _audioPlayer.OnAgentStartedSpeaking += OnAgentStartedSpeaking;
                _audioPlayer.OnAgentStoppedSpeaking += OnAgentStoppedSpeaking;
            }
        }

        private void HandleMessageReceivedFromBackground(string message)
        {
            _dispatchToMainThread(() => HandleMessageReceived(message));
        }

        private void HandleMessageReceived(string message)
        {
            try
            {
                var eventPayload = JsonUtility.FromJson<ElevenLabsEvent>(message);

                switch (eventPayload.type)
                {
                    case "ping":
                        HandlePingEvent(message).ContinueWith(t =>
                        {
                            if (t.IsFaulted)
                                Debug.LogError($"[ConversationController] Pong failed: {t.Exception}");
                        }, TaskScheduler.Default);
                        break;
                    case "audio":
                        HandleAudioEvent(message);
                        break;
                    case "user_transcript":
                        HandleUserTranscriptEvent(message);
                        break;
                    case "agent_response":
                        HandleAgentResponseEvent(message);
                        break;
                    case "interruption":
                        _audioPlayer?.StopImmediately();
                        break;
                    default:
                        Debug.Log($"Unhandled event type: {eventPayload.type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to parse WebSocket message: {ex.Message}\nMessage: {message.Substring(0, Mathf.Min(200, message.Length))}...");
            }
        }

        private async Task HandlePingEvent(string message)
        {
            var pingEvent = JsonUtility.FromJson<PingEvent>(message);
            var pongMessage = new PongMessage
            {
                type = "pong",
                event_id = pingEvent.event_id
            };
            string pongJson = JsonUtility.ToJson(pongMessage);
            await _webSocketService.SendRawJsonAsync(pongJson);
        }

        private void HandleAudioEvent(string message)
        {
            if (_textOnlyMode)
            {
                Debug.LogWarning("[ConversationController] Audio event received but text-only mode is enabled");
                return;
            }

            if (_audioPlayer == null)
            {
                Debug.LogError("[ConversationController] Audio event received but PcmAudioPlayer is null!");
                return;
            }

            var audioEvent = JsonUtility.FromJson<AudioEvent>(message);
            if (!string.IsNullOrEmpty(audioEvent.audio_event?.audio_base_64))
            {
                if (LifePersonaSDK.VerboseLogging)
                    Debug.Log($"[ConversationController] Processing audio event, base64 length: {audioEvent.audio_event.audio_base_64.Length}");
                _audioPlayer.EnqueueBase64Audio(audioEvent.audio_event.audio_base_64);
            }
            else
            {
                Debug.LogWarning("[ConversationController] Audio event received but audio_base_64 is empty");
            }
        }

        private void HandleUserTranscriptEvent(string message)
        {
            var userTranscriptEvent = JsonUtility.FromJson<UserTranscriptEvent>(message);
            var transcript = userTranscriptEvent.user_transcription_event?.user_transcript;
            if (!string.IsNullOrEmpty(transcript))
            {
                OnUserTranscript?.Invoke(transcript);
            }
        }

        private void HandleAgentResponseEvent(string message)
        {
            var agentResponseEvent = JsonUtility.FromJson<AgentResponseEvent>(message);
            var response = agentResponseEvent.agent_response_event?.agent_response;
            if (!string.IsNullOrEmpty(response))
            {
                OnAgentTranscript?.Invoke(response);
            }
        }

        private void OnMicAudioChunk(string base64Chunk)
        {
            if (!_conversationReady || !_webSocketService.IsConnected) return;

            try
            {
                var audioMessage = new MicAudioMessage
                {
                    user_audio_chunk = base64Chunk
                };
                string json = JsonUtility.ToJson(audioMessage);
                _webSocketService.SendRawJsonAsync(json).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        Debug.LogWarning($"[ConversationController] Mic chunk send failed (likely disconnecting): {t.Exception?.InnerException?.Message}");
                }, TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ConversationController] Mic chunk send failed: {ex.Message}");
            }
        }

        private void OnAgentStartedSpeaking()
        {
            // Stop microphone streaming to prevent echo
            _micStreamer?.StopStreaming();
        }

        private void OnAgentStoppedSpeaking()
        {
            if (!_conversationReady) return;

            // Resume microphone streaming after agent finishes
            _micStreamer?.StartStreaming();
        }

        // ===== Commands =====

        private async Task StartConversationCore(
            string userId, string baseUrl,
            Func<HttpService.BootResponse, HttpService.StartConversationResponse, Task> sendInitMessage)
        {
            _conversationReady = false;

            var bootResponse = await _httpService.BootAsync(userId, baseUrl + "boot");
            Debug.Log($"Boot successful - Session: {bootResponse.sessionId}");

            var startResponse = await _httpService.StartConversationAsync(userId, baseUrl + "start-conversation");
            Debug.Log($"Conversation started - ID: {startResponse.conversationId}");

            await _webSocketService.ConnectAsync(bootResponse.signedUrl);

            await sendInitMessage(bootResponse, startResponse);
            _conversationReady = true;

            if (_micStreamer != null && !_textOnlyMode)
            {
                _micStreamer.StartStreaming();
            }
        }

        public Task StartConversation(string userId, string baseUrl)
        {
            return StartConversationCore(userId, baseUrl,
                async (boot, start) =>
                {
                    await _webSocketService.SendInitMessageAsync(boot.userId, start.conversationId, _textOnlyMode);
                });
        }

        public async Task StartConversationWithTrigger(
            string userId, string baseUrl, string activeTriggerIdParam)
        {
            var trigger = await _httpService.GetActiveTriggerAsync(
                activeTriggerIdParam, userId, baseUrl + "active-triggers");
            Debug.Log($"Trigger fetched - Name: {trigger.name}");

            await StartConversationCore(userId, baseUrl,
                async (boot, start) =>
                {
                    await _webSocketService.SendTriggerInitMessageAsync(
                        boot.userId, start.conversationId,
                        activeTriggerIdParam, trigger.objective, trigger.playbook,
                        trigger.firstMessage, trigger.eligibleOfferPolicy, _textOnlyMode);

                    await _httpService.MarkActiveTriggerSentAsync(
                        activeTriggerIdParam, userId, baseUrl + "active-triggers");
                    Debug.Log($"Trigger {activeTriggerIdParam} marked as sent");
                });
        }

        public async Task SendText(string message)
        {
            if (_webSocketService == null || !_webSocketService.IsConnected)
            {
                throw new InvalidOperationException("Cannot send text: WebSocket is not connected");
            }

            await _webSocketService.SendTextMessageAsync(message);

            // Emit user transcript for text sends (voice transcripts arrive via WS events)
            OnUserTranscript?.Invoke(message);
        }

        public void Dispose()
        {
            _conversationReady = false;
            _webSocketService.OnMessageReceived -= HandleMessageReceivedFromBackground;

            // Unsubscribe from audio player events
            if (_audioPlayer != null && _micStreamer != null && !_textOnlyMode)
            {
                _audioPlayer.OnAgentStartedSpeaking -= OnAgentStartedSpeaking;
                _audioPlayer.OnAgentStoppedSpeaking -= OnAgentStoppedSpeaking;
            }

            // Stop microphone streaming if active
            if (_micStreamer != null && !_textOnlyMode)
            {
                _micStreamer.OnAudioChunk -= OnMicAudioChunk;
                _micStreamer.StopStreaming();
            }
        }

        #region Event Data Classes

        [Serializable]
        private class ElevenLabsEvent
        {
            public string type;
        }

        [Serializable]
        private class PingEvent
        {
            public string type;
            public int event_id;
        }

        [Serializable]
        private class PongMessage
        {
            public string type;
            public int event_id;
        }

        [Serializable]
        private class AudioEvent
        {
            public string type;
            public AudioEventData audio_event;
        }

        [Serializable]
        private class AudioEventData
        {
            public string audio_base_64;  // Fixed: underscore between base and 64
            public int event_id;
        }

        [Serializable]
        private class UserTranscriptEvent
        {
            public string type;
            public UserTranscriptEventData user_transcription_event;
        }

        [Serializable]
        private class UserTranscriptEventData
        {
            public string user_transcript;
            public int event_id;
        }

        [Serializable]
        private class AgentResponseEvent
        {
            public string type;
            public AgentResponseEventData agent_response_event;
        }

        [Serializable]
        private class AgentResponseEventData
        {
            public string agent_response;
            public int event_id;
        }

        [Serializable]
        private class MicAudioMessage
        {
            public string user_audio_chunk;
        }

        #endregion
    }
}
