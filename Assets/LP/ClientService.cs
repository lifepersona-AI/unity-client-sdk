using System;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using NativeWebSocket;

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

        private WebSocket _webSocket;
        private string _webSocketUrl;
        
        /// <summary>
        /// Sends a POST request with message data to the server.
        /// TODO: Update request body format to match your backend API.
        /// Current format: { "message": "text", "timestamp": "ISO8601" }
        /// </summary>
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

                            // After connecting, send initial message to start conversation
                            Debug.Log("qqq----- Sending initial message ----");
                            await SendTextMessageAsync("{\n    \"type\": \"conversation_initiation_client_data\",\n    \"conversation_config_override\": {\n        \"agent\": {\n            \"language\": \"en\"\n        },\n        \"tts\": {},\n        \"conversation\": {\n            \"text_only\": true\n        }\n    },\n    \"dynamic_variables\": {\n        \"conversationId\": \"136c9b13-09e4-46bf-807a-1b9ed65dd424\"\n    },\n    \"user_id\": \"john doe\"\n}");
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
                _webSocket = new WebSocket(_webSocketUrl);

                bool isConnected = false;

                _webSocket.OnOpen += () =>
                {
                    Debug.Log("WebSocket connected!");
                    isConnected = true;
                };

                _webSocket.OnMessage += (bytes) =>
                {
                    // ElevenLabs sends both text (JSON events) and binary (audio) messages
                    // For text-only mode, we only handle JSON text events
                    string message = Encoding.UTF8.GetString(bytes);
                    Debug.Log($"WebSocket message received: {message}");

                    // Try to parse as ElevenLabs event
                    try
                    {
                        var elevenlabsEvent = JsonUtility.FromJson<ElevenLabsEvent>(message);
                        if (elevenlabsEvent.type == "agent_response")
                        {
                            // Agent's text response
                            Debug.Log($"Agent response: {elevenlabsEvent.agent_response}");
                            OnMessageReceived?.Invoke(elevenlabsEvent.agent_response);
                        }
                        else if (elevenlabsEvent.type == "transcript")
                        {
                            // User's transcribed speech (if using voice input)
                            Debug.Log($"Transcript: {elevenlabsEvent.transcript}");
                        }
                        else
                        {
                            Debug.Log($"ElevenLabs event type: {elevenlabsEvent.type}");
                        }
                    }
                    catch
                    {
                        // If not a JSON event, just pass through the raw message
                        OnMessageReceived?.Invoke(message);
                    }
                };

                _webSocket.OnError += (errorMsg) =>
                {
                    Debug.LogError($"WebSocket error: {errorMsg}");
                    OnError?.Invoke(errorMsg);
                };

                _webSocket.OnClose += (closeCode) =>
                {
                    Debug.Log($"WebSocket closed with code: {closeCode}");
                };

                await _webSocket.Connect();

                Debug.Log("qqq----- WebSocket Connect() called, waiting for OnOpen ----");

                // Wait for the connection to actually open
                float timeout = 10f;
                float elapsed = 0f;
                while (!isConnected && elapsed < timeout)
                {
                    await Awaitable.WaitForSecondsAsync(0.1f);
                    elapsed += 0.1f;
                }

                if (!isConnected)
                {
                    throw new Exception("WebSocket connection timeout - OnOpen never fired");
                }

                Debug.Log("qqq----- WebSocket OnOpen fired, connection ready ----");
            }
            catch (Exception ex)
            {
                string error = $"WebSocket connection failed: {ex.Message}";
                Debug.LogError(error);
                OnError?.Invoke(error);
            }
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
                byte[] messageBytes = Encoding.UTF8.GetBytes(jsonMessage);
                await _webSocket.Send(messageBytes);
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
        /// Dispatches WebSocket events. Call this in MonoBehaviour's Update method.
        /// </summary>
        /// TODO make it slow update
        public void Update()
        {
            _webSocket?.DispatchMessageQueue();
        }

        /// <summary>
        /// Closes the WebSocket connection and cleans up resources.
        /// </summary>
        public async Awaitable DisconnectAsync()
        {
            if (_webSocket != null)
            {
                await _webSocket.Close();
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
