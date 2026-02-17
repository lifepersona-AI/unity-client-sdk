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

        public ConversationController(
            HttpService httpService,
            WebSocketService webSocketService,
            PcmAudioPlayer audioPlayer = null,
            MicrophoneStreamer micStreamer = null,
            bool textOnlyMode = true)
        {
            _httpService = httpService;
            _webSocketService = webSocketService;
            _audioPlayer = audioPlayer;
            _micStreamer = micStreamer;
            _textOnlyMode = textOnlyMode;

            // Subscribe to incoming WebSocket messages
            _webSocketService.OnMessageReceived += HandleMessageReceived;

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

        private void HandleMessageReceived(string message)
        {
            try
            {
                var eventPayload = JsonUtility.FromJson<ElevenLabsEvent>(message);

                switch (eventPayload.type)
                {
                    case "ping":
                        _ = HandlePingEvent(message);
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
            var audioMessage = new MicAudioMessage
            {
                user_audio_chunk = base64Chunk
            };
            string json = JsonUtility.ToJson(audioMessage);
            _ = _webSocketService.SendRawJsonAsync(json);
        }

        private void OnAgentStartedSpeaking()
        {
            // Stop microphone streaming to prevent echo
            _micStreamer?.StopStreaming();
        }

        private void OnAgentStoppedSpeaking()
        {
            // Resume microphone streaming after agent finishes
            _micStreamer?.StartStreaming();
        }

        // ===== Commands =====

        public async Task StartConversation(string apiKey, string userId, string baseUrl, Action<bool> onConversationStarted)
        {
            try
            {
                // 1. Boot via HTTP to get WebSocket URL
                var bootResponse = await _httpService.BootAsync(apiKey, userId, baseUrl + "boot");
                Debug.Log($"Boot successful - Session: {bootResponse.sessionId}");

                // 2. Start conversation to get conversationId
                var startConversationResponse = await _httpService.StartConversationAsync(userId, baseUrl + "start-conversation");
                Debug.Log($"Conversation started - ID: {startConversationResponse.conversationId}");

                // 3. Connect to WebSocket
                await _webSocketService.ConnectAsync(bootResponse.signedUrl);

                // 4. Send initialization message with conversationId
                await _webSocketService.SendInitMessageAsync(bootResponse.userId, startConversationResponse.conversationId, _textOnlyMode);

                // 5. Start microphone streaming if not in text-only mode
                if (_micStreamer != null && !_textOnlyMode)
                {
                    _micStreamer.StartStreaming();
                }

                onConversationStarted(true);
            }
            catch(Exception ex)
            {
                Debug.LogError($"Failed to start conversation: {ex.Message}");
                onConversationStarted(false);
            }
        }

        public async Task SendText(string message)
        {
            if (_webSocketService == null || !_webSocketService.IsConnected)
            {
                throw new InvalidOperationException("Cannot send text: WebSocket is not connected");
            }

            await _webSocketService.SendTextMessageAsync(message);
        }

        public void Dispose()
        {
            _webSocketService.OnMessageReceived -= HandleMessageReceived;

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
