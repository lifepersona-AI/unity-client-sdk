using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LP
{
    public class WebSocketService
    {
        public event Action<string> OnMessageReceived;
        public event Action OnConnected;
        public event Action OnDisconnected;

        private ClientWebSocket _webSocket;
        private CancellationTokenSource _receiveCts;
        private bool _isConnected;

        public bool IsConnected => _isConnected && _webSocket?.State == WebSocketState.Open;

        public async Task ConnectAsync(string webSocketUrl)
        {
            if (string.IsNullOrEmpty(webSocketUrl))
            {
                string error = "WebSocket URL is null or empty";
                Debug.LogError(error);
                throw new ArgumentException(error);
            }

            try
            {
                // Clean up previous connection if exists
                _receiveCts?.Cancel();
                _receiveCts?.Dispose();
                _webSocket?.Dispose();

                _webSocket = new ClientWebSocket();
                _receiveCts = new CancellationTokenSource();
                _isConnected = false;

                Debug.Log($"Connecting to WebSocket: {webSocketUrl}");

                await _webSocket.ConnectAsync(new Uri(webSocketUrl), CancellationToken.None);

                _isConnected = true;
                Debug.Log("WebSocket connected! Starting receive loop...");

                OnConnected?.Invoke();

                // Start receive loop without blocking - fire and forget with inline error handling
                _ = ReceiveMessagesAsync();
            }
            catch (Exception ex)
            {
                string error = $"WebSocket connection failed: {ex.Message}";
                Debug.LogError(error);

                // Clean up on failure
                _isConnected = false;
                _receiveCts?.Dispose();
                _receiveCts = null;
                _webSocket?.Dispose();
                _webSocket = null;

                throw;
            }
        }

        public async Task SendInitMessageAsync(string userId, string conversationId, bool textOnlyMode = true)
        {
            var initMessage = new InitMessage
            {
                type = "conversation_initiation_client_data",
                conversation_config_override = new ConversationConfigOverride
                {
                    agent = new AgentConfig { language = "en" },
                    tts = new TtsConfig { model_id = "eleven_flash_v2_5" },  // Low latency model (75ms)
                    conversation = new ConversationConfig { text_only = textOnlyMode }
                },
                dynamic_variables = new DynamicVariables { conversationId = conversationId },
                user_id = userId
            };

            string json = JsonUtility.ToJson(initMessage);
            await SendRawJsonAsync(json);
        }

        public async Task SendTextMessageAsync(string message)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            {
                string error = "WebSocket is not connected";
                Debug.LogError(error);
                throw new InvalidOperationException(error);
            }

            try
            {
                var textMessage = new ElevenLabsTextMessage
                {
                    type = "user_message",
                    text = message
                };
                string jsonMessage = JsonUtility.ToJson(textMessage);
                await SendRawJsonAsync(jsonMessage);
                Debug.Log($"WebSocket text message sent: {message}");
            }
            catch (Exception ex)
            {
                string error = $"Failed to send WebSocket message: {ex.Message}";
                Debug.LogError(error);
                throw;
            }
        }

        public async Task DisconnectAsync()
        {
            if (!_isConnected)
            {
                Debug.Log("WebSocket already disconnected");
                return;
            }

            // Cancel the receive loop first
            _receiveCts?.Cancel();

            // Try to close gracefully if in a valid state
            if (_webSocket != null)
            {
                try
                {
                    var state = _webSocket.State;
                    if (state == WebSocketState.Open ||
                        state == WebSocketState.CloseReceived ||
                        state == WebSocketState.CloseSent)
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Error during WebSocket close: {ex.Message}");
                }
            }

            // Clean up resources regardless of close status
            _isConnected = false;
            _receiveCts?.Dispose();
            _receiveCts = null;
            _webSocket?.Dispose();
            _webSocket = null;

            Debug.Log("WebSocket disconnected");
            OnDisconnected?.Invoke();
        }

        public async Task SendRawJsonAsync(string json)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            {
                throw new InvalidOperationException("WebSocket is not connected");
            }

            byte[] messageBytes = Encoding.UTF8.GetBytes(json);
            var segment = new ArraySegment<byte>(messageBytes);
            await _webSocket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
            Debug.Log($"WebSocket raw JSON sent: {json}");
        }

        private async Task ReceiveMessagesAsync()
        {
            var buffer = new byte[1024 * 8]; // Increased buffer size for audio chunks
            var messageBuilder = new StringBuilder();

            try
            {
                while (_webSocket.State == WebSocketState.Open && !_receiveCts.Token.IsCancellationRequested)
                {
                    messageBuilder.Clear();
                    WebSocketReceiveResult result;

                    // Keep receiving until we get the complete message
                    do
                    {
                        result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _receiveCts.Token);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            Debug.Log($"WebSocket close received: {result.CloseStatus}");
                            _isConnected = false;

                            try
                            {
                                var state = _webSocket.State;
                                if (state == WebSocketState.Open ||
                                    state == WebSocketState.CloseReceived ||
                                    state == WebSocketState.CloseSent)
                                {
                                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"Error closing WebSocket on server close: {ex.Message}");
                            }

                            OnDisconnected?.Invoke();
                            return;
                        }

                        if (result.MessageType == WebSocketMessageType.Text)
                        {
                            messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                        }
                    }
                    while (!result.EndOfMessage);

                    // Only process if we received a text message
                    if (result.MessageType == WebSocketMessageType.Text && messageBuilder.Length > 0)
                    {
                        string message = messageBuilder.ToString();
                        Debug.Log($"WebSocket message received: {message}");

                        // Forward the complete message to subscribers
                        OnMessageReceived?.Invoke(message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("WebSocket receive cancelled");
            }
            catch (Exception ex)
            {
                Debug.LogError($"WebSocket receive error: {ex.Message}");
                _isConnected = false;
                OnDisconnected?.Invoke();
            }
        }

        #region Message Data Classes

        [Serializable]
        private class ElevenLabsTextMessage
        {
            public string type;
            public string text;
        }

        [Serializable]
        private class InitMessage
        {
            public string type;
            public ConversationConfigOverride conversation_config_override;
            public DynamicVariables dynamic_variables;
            public string user_id;
        }

        [Serializable]
        private class ConversationConfigOverride
        {
            public AgentConfig agent;
            public TtsConfig tts;
            public ConversationConfig conversation;
        }

        [Serializable]
        private class AgentConfig
        {
            public string language;
        }

        [Serializable]
        private class TtsConfig
        {
            public string model_id;  // "eleven_flash_v2_5" for lowest latency (75ms)
        }

        [Serializable]
        private class ConversationConfig
        {
            public bool text_only;
        }

        [Serializable]
        private class DynamicVariables
        {
            public string conversationId;
        }

        #endregion
    }
}

