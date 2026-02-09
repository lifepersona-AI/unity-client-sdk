using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace LP
{
    public class ClientService
    {
        private readonly ClientConfig _config;
        private readonly MonoBehaviour _coroutineRunner;

        public event Action<string> OnMessageReceived;
        public event Action<string> OnError;

        public ClientService(ClientConfig config, MonoBehaviour coroutineRunner)
        {
            _config = config;
            _coroutineRunner = coroutineRunner;
        }

        /// <summary>
        /// Sends a GET request to fetch a text message from the server.
        /// TODO: Replace endpoint URL when backend API is ready.
        /// Expected response format: plain text or JSON with "message" field.
        /// </summary>
        public void FetchMessage(string endpoint)
        {
            _coroutineRunner.StartCoroutine(FetchMessageCoroutine(endpoint));
        }

        /// <summary>
        /// Sends a POST request with message data to the server.
        /// TODO: Update request body format to match your backend API.
        /// Current format: { "message": "text", "timestamp": "ISO8601" }
        /// </summary>
        public void SendMessage(string message, string endpoint)
        {
            _coroutineRunner.StartCoroutine(SendMessageCoroutine(message, endpoint));
        }

        private IEnumerator FetchMessageCoroutine(string endpoint)
        {
            string url = $"{_config.BaseUrl}{endpoint}";

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                // Add headers
                request.SetRequestHeader("x-access-token", _config.ApiKey);
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = _config.TimeoutSeconds;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request.downloadHandler.text;

                    // TODO: Parse JSON if needed. For now, assuming plain text response.
                    // Example for JSON: var json = JsonUtility.FromJson<MessageResponse>(responseText);

                    OnMessageReceived?.Invoke(responseText);
                }
                else
                {
                    string error = $"FetchMessage failed: {request.error}";
                    Debug.LogError(error);
                    OnError?.Invoke(error);
                }
            }
        }

        private IEnumerator SendMessageCoroutine(string messageBody, string endpoint)
        {
            string url = $"{_config.BaseUrl}{endpoint}";
            string jsonBody = JsonUtility.ToJson(messageBody);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();

                // Add headers
                request.SetRequestHeader("x-access-token", _config.ApiKey);
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = _config.TimeoutSeconds;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request.downloadHandler.text;

                    // TODO: Handle response if needed
                    Debug.Log($"SendMessage success: {responseText}");
                    OnMessageReceived?.Invoke(responseText);
                }
                else
                {
                    string error = $"SendMessage failed: {request.error}";
                    Debug.LogError(error);
                    OnError?.Invoke(error);
                }
            }
        }

        // TODO: Create response class if your API returns structured JSON
        // [Serializable]
        // private class MessageResponse
        // {
        //     public string message;
        //     public string userId;
        // }
    }
}
