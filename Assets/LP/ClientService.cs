using System;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace LP
{
    public class ClientService
    {
        public string ApiKey { get; set; } = "pk_live_lPcjElxhOL3wHuV15WK5eIuTPUIzaZ0v";
        public int TimeoutSeconds { get; set; } = 30;

        public event Action<string> OnMessageReceived;
        public event Action<string> OnError;
        
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
                }
                else
                {
                    string error = $"SendMessage failed: {request.error}";
                    Debug.LogError(error);
                }
            }
        }
    }
}
