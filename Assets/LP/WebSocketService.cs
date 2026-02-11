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
        public event Action<string> OnError;

        private ClientWebSocket _webSocket;
        private CancellationTokenSource _receiveCts;

        public async Task ConnectAsync(string webSocketUrl)
        {
            if (string.IsNullOrEmpty(webSocketUrl))
            {
                string error = "WebSocket URL is null or empty";
                Debug.LogError(error);
                OnError?.Invoke(error);
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

                Debug.Log($"Connecting to WebSocket: {webSocketUrl}");

                await _webSocket.ConnectAsync(new Uri(webSocketUrl), CancellationToken.None);

                Debug.Log("WebSocket connected! Starting receive loop...");

                ReceiveMessagesAsync().Forget();
            }
            catch (Exception ex)
            {
                string error = $"WebSocket connection failed: {ex.Message}";
                Debug.LogError(error);
                OnError?.Invoke(error);

                // Clean up on failure
                _receiveCts?.Dispose();
                _receiveCts = null;
                _webSocket?.Dispose();
                _webSocket = null;

                throw;
            }
        }

        public async Task SendInitMessageAsync(string userId, string conversationId)
        {
            string initMessage = $@"{{
                                ""type"": ""conversation_initiation_client_data"",
                                ""conversation_config_override"": {{
                                    ""agent"": {{
                                        ""language"": ""en""
                                    }},
                                    ""tts"": {{}},
                                    ""conversation"": {{
                                        ""text_only"": true
                                    }}
                                }},
                                ""dynamic_variables"": {{
                                    ""conversationId"": ""{conversationId}""
                                }},
                                ""user_id"": ""{userId}""
                            }}";
            await SendRawJsonAsync(initMessage);
        }

        public async Task SendTextMessageAsync(string message)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            {
                string error = "WebSocket is not connected";
                Debug.LogError(error);
                OnError?.Invoke(error);
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
                OnError?.Invoke(error);
                throw;
            }
        }

        public async Task DisconnectAsync()
        {
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
            _receiveCts?.Dispose();
            _receiveCts = null;
            _webSocket?.Dispose();
            _webSocket = null;

            Debug.Log("WebSocket disconnected");
        }

        private async Task SendRawJsonAsync(string json)
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
            var buffer = new byte[1024 * 4];

            try
            {
                while (_webSocket.State == WebSocketState.Open && !_receiveCts.Token.IsCancellationRequested)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _receiveCts.Token);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Debug.Log($"WebSocket message received: {message}");

                        try
                        {
                            var elevenlabsEvent = JsonUtility.FromJson<ElevenLabsEvent>(message);
                            if (elevenlabsEvent.type == "agent_response" && elevenlabsEvent.agent_response_event != null)
                            {
                                string agentResponse = elevenlabsEvent.agent_response_event.agent_response;
                                Debug.Log($"Agent response: {agentResponse}");
                                OnMessageReceived?.Invoke(agentResponse);
                            }
                        }
                        catch
                        {
                            OnMessageReceived?.Invoke(message);
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Debug.Log($"WebSocket close received: {result.CloseStatus}");
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
                        break;
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
                OnError?.Invoke(ex.Message);
            }
        }

        [Serializable]
        private class ElevenLabsTextMessage
        {
            public string type;
            public string text;
        }

        [Serializable]
        private class ElevenLabsEvent
        {
            public string type;
            public AgentResponseEvent agent_response_event;
            public string transcript;
        }

        [Serializable]
        private class AgentResponseEvent
        {
            public string agent_response;
            public int event_id;
        }
    }
}
