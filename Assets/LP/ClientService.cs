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
                    Debug.Log($"SendMessage success: {responseText}");

                    // Parse WebSocket URL from response
                    // TODO: Update JSON parsing to match your backend response structure
                    // Expected format: { "wsUrl": "ws://..." } or { "websocketUrl": "ws://..." }
                    try
                    {
                        var response = JsonUtility.FromJson<PostResponse>(responseText);
                        if (!string.IsNullOrEmpty(response.wsUrl))
                        {
                            _webSocketUrl = response.wsUrl;
                            Debug.Log($"Received WebSocket URL: {_webSocketUrl}");
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

                _webSocket.OnOpen += () =>
                {
                    Debug.Log("WebSocket connected!");
                };

                _webSocket.OnMessage += (bytes) =>
                {
                    string message = Encoding.UTF8.GetString(bytes);
                    Debug.Log($"WebSocket message received: {message}");
                    OnMessageReceived?.Invoke(message);
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
            }
            catch (Exception ex)
            {
                string error = $"WebSocket connection failed: {ex.Message}";
                Debug.LogError(error);
                OnError?.Invoke(error);
            }
        }

        /// <summary>
        /// Sends a message through the WebSocket connection.
        /// Make sure to call ConnectWebSocketAsync before sending messages.
        /// </summary>
        public async Awaitable SendMessageAsync(string message)
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
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                await _webSocket.Send(messageBytes);
                Debug.Log($"WebSocket message sent: {message}");
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
            public string wsUrl;
        }
    }
}
