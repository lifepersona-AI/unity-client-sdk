using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace LP
{
    public class ClientService
    {
        //
        private string ApiKey { get; set; } = "pk_live_lPcjElxhOL3wHuV15WK5eIuTPUIzaZ0v";
        private int TimeoutSeconds { get; set; } = 30;
        //
        
        public event Action<string> OnMessageReceived;
        public event Action<string> OnError;

        private ClientWebSocket _webSocket;
        private string _webSocketUrl;
        private CancellationTokenSource _receiveCts;
        
        public async Awaitable PostRequestAsync(string messageBody, string url, CancellationToken cancellationToken = default)
        {
            string jsonBody = messageBody;
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();

                // Add headers
                request.SetRequestHeader("x-access-token", ApiKey);
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = TimeoutSeconds;

                await request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request.downloadHandler.text;
                    Debug.Log($"Boot success: {responseText}");

                    // Parse WebSocket URL from response
                    try
                    {
                        var response = JsonUtility.FromJson<PostResponse>(responseText);
                        if (!string.IsNullOrEmpty(response.signedUrl))
                        {
                            _webSocketUrl = response.signedUrl;
                            Debug.Log($"Received WebSocket URL: {_webSocketUrl}");
                            Debug.Log($"Session ID: {response.sessionId}, Agent ID: {response.agentId}");
                            Debug.Log("----- Calling ConnectWebSocketAsync ----");
                            await ConnectWebSocketAsync();

                            // After connecting, send initialization message to ElevenLabs
                            Debug.Log("----- Sending ElevenLabs initialization message ----");
                            await SendElevenLabsInitMessage(response.userId, response.sessionId);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Failed to parse WebSocket URL from response: {ex.Message}");
                    }
                }
                else
                {
                    string error = $"SendMessage failed: {request.error}";
                    Debug.LogError(error);
                    OnError?.Invoke(error);
                }
            }
        }

        /// <summary>
        /// Connects to the WebSocket server using the URL received from PostRequestAsync.
        /// Call PostRequestAsync first to get the WebSocket URL.
        /// </summary>
        public async Awaitable ConnectWebSocketAsync()
        {
            if (string.IsNullOrEmpty(_webSocketUrl))
            {
                string error = "WebSocket URL not set. Call PostRequestAsync first to get the WebSocket URL.";
                Debug.LogError(error);
                OnError?.Invoke(error);
                return;
            }

            try
            {
                _webSocket = new ClientWebSocket();
                _receiveCts = new CancellationTokenSource();

                Debug.Log($"Connecting to WebSocket: {_webSocketUrl}");

                // Connect to the WebSocket
                await _webSocket.ConnectAsync(new Uri(_webSocketUrl), CancellationToken.None);

                Debug.Log("WebSocket connected! Starting receive loop...");

                // Start receiving messages in the background
                _ = ReceiveMessagesAsync();
            }
            catch (Exception ex)
            {
                string error = $"WebSocket connection failed: {ex.Message}";
                Debug.LogError(error);
                OnError?.Invoke(error);
            }
        }

        /// <summary>
        /// Continuously receives messages from the WebSocket.
        /// </summary>
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

                        // Try to parse as ElevenLabs event
                        try
                        {
                            var elevenlabsEvent = JsonUtility.FromJson<ElevenLabsEvent>(message);
                            if (elevenlabsEvent.type == "agent_response")
                            {
                                Debug.Log($"Agent response: {elevenlabsEvent.agent_response}");
                                OnMessageReceived?.Invoke(elevenlabsEvent.agent_response);
                            }
                            else if (elevenlabsEvent.type == "transcript")
                            {
                                Debug.Log($"Transcript: {elevenlabsEvent.transcript}");
                            }
                            else
                            {
                                Debug.Log($"ElevenLabs event type: {elevenlabsEvent.type}");
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
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
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

        /// <summary>
        /// Sends the ElevenLabs initialization message to configure the conversation.
        /// </summary>
        private async Awaitable SendElevenLabsInitMessage(string userId, string conversationId)
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

        /// <summary>
        /// Sends a text message through the WebSocket connection to ElevenLabs.
        /// Make sure to call ConnectWebSocketAsync before sending messages.
        /// </summary>
        public async Awaitable SendTextMessageAsync(string message)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            {
                string error = "WebSocket is not connected. Call ConnectWebSocketAsync first.";
                Debug.LogError(error);
                OnError?.Invoke(error);
                return;
            }

            try
            {
                // ElevenLabs expects text messages in a specific JSON format
                var textMessage = new ElevenLabsTextMessage
                {
                    type = "text",
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
            }
        }

        /// <summary>
        /// Sends raw JSON string through the WebSocket.
        /// </summary>
        private async Awaitable SendRawJsonAsync(string json)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            {
                throw new Exception("WebSocket is not connected");
            }

            byte[] messageBytes = Encoding.UTF8.GetBytes(json);
            var segment = new ArraySegment<byte>(messageBytes);
            await _webSocket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
            Debug.Log($"WebSocket raw JSON sent: {json}");
        }

        /// <summary>
        /// No longer needed with ClientWebSocket (it doesn't have a message queue).
        /// Can be removed or left empty for compatibility.
        /// </summary>
        public void Update()
        {
            // ClientWebSocket handles messages asynchronously, no manual dispatch needed
        }

        /// <summary>
        /// Closes the WebSocket connection and cleans up resources.
        /// </summary>
        public async Awaitable DisconnectAsync()
        {
            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                _receiveCts?.Cancel();
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                _webSocket.Dispose();
                _webSocket = null;
                Debug.Log("WebSocket disconnected");
            }
        }

        [Serializable]
        private class PostResponse
        {
            public string signedUrl;
            public string timestamp;
            public string userId;
            public string agentId;
            public string sessionId;
            public string projectId;
            public string offersUrl;
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
            public string agent_response;
            public string transcript;
        }
    }
}
