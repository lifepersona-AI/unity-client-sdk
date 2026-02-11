using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace LP
{
    public class HttpService
    {
        private string ApiKey { get; set; } = "pk_live_lPcjElxhOL3wHuV15WK5eIuTPUIzaZ0v";
        private int TimeoutSeconds { get; set; } = 30;

        public event Action<string> OnError;

        public async Task<BootResponse> BootAsync(string userId, string url)
        {
            string jsonBody = $"{{\"userId\": \"{userId}\"}}";
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();

                request.SetRequestHeader("x-access-token", ApiKey);
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = TimeoutSeconds;

                await request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request.downloadHandler.text;
                    Debug.Log($"Boot success: {responseText}");

                    try
                    {
                        var response = JsonUtility.FromJson<BootResponse>(responseText);
                        return response;
                    }
                    catch (Exception ex)
                    {
                        string error = $"Failed to parse boot response: {ex.Message}";
                        Debug.LogError(error);
                        OnError?.Invoke(error);
                        throw;
                    }
                }
                else
                {
                    string error = $"Boot request failed: {request.error}";
                    Debug.LogError(error);
                    OnError?.Invoke(error);
                    throw new Exception(error);
                }
            }
        }

        public async Task<StartConversationResponse> StartConversationAsync(string userId, string url)
        {
            string jsonBody = $"{{\"userId\": \"{userId}\"}}";
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();

                request.SetRequestHeader("x-access-token", ApiKey);
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = TimeoutSeconds;

                await request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request.downloadHandler.text;
                    Debug.Log($"Start conversation success: {responseText}");

                    try
                    {
                        var response = JsonUtility.FromJson<StartConversationResponse>(responseText);
                        return response;
                    }
                    catch (Exception ex)
                    {
                        string error = $"Failed to parse start conversation response: {ex.Message}";
                        Debug.LogError(error);
                        OnError?.Invoke(error);
                        throw;
                    }
                }
                else
                {
                    string error = $"Start conversation request failed: {request.error}";
                    Debug.LogError(error);
                    OnError?.Invoke(error);
                    throw new Exception(error);
                }
            }
        }

        [Serializable]
        public class BootResponse
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
        public class StartConversationResponse
        {
            public string id;
            public string conversationId;
            public string status;
        }
    }
}
