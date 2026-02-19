using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace LP
{
    public class HttpService
    {
        private readonly string _apiKey;
        private const int TimeoutSeconds = 30;

        public HttpService(string apiKey)
        {
            _apiKey = apiKey;
        }

        // ===== Core Request Helpers =====

        private async Task<string> SendRawAsync(string url, string method, string jsonBody = null)
        {
            using (UnityWebRequest request = new UnityWebRequest(url, method))
            {
                if (jsonBody != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                    request.SetRequestHeader("Content-Type", "application/json");
                }

                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("x-access-token", _apiKey);
                request.timeout = TimeoutSeconds;

                await request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception($"{method} {url} failed: {request.error}");
                }

                return request.downloadHandler.text;
            }
        }

        private async Task<T> SendAsync<T>(string url, string method, string jsonBody = null)
        {
            string responseText = await SendRawAsync(url, method, jsonBody);
            return JsonUtility.FromJson<T>(responseText);
        }

        // ===== API Methods =====

        public Task<BootResponse> BootAsync(string userId, string url)
        {
            string jsonBody = JsonUtility.ToJson(new UserIdBody { userId = userId });
            return SendAsync<BootResponse>(url, "POST", jsonBody);
        }

        public Task<StartConversationResponse> StartConversationAsync(string userId, string url)
        {
            string jsonBody = JsonUtility.ToJson(new UserIdBody { userId = userId });
            return SendAsync<StartConversationResponse>(url, "POST", jsonBody);
        }

        public async Task<ActiveTriggerResponse[]> GetActiveTriggersAsync(string userId, string url)
        {
            string requestUrl = $"{url}?userId={Uri.EscapeDataString(userId)}";
            // JsonUtility can't parse root-level arrays, so wrap it
            string responseText = await SendRawAsync(requestUrl, "GET");
            string wrapped = $"{{\"items\":{responseText}}}";
            var wrapper = JsonUtility.FromJson<ActiveTriggersArrayWrapper>(wrapped);
            return wrapper.items ?? Array.Empty<ActiveTriggerResponse>();
        }

        public Task<ActiveTriggerResponse> GetActiveTriggerAsync(string id, string userId, string url)
        {
            string requestUrl = $"{url}/{id}?userId={Uri.EscapeDataString(userId)}";
            return SendAsync<ActiveTriggerResponse>(requestUrl, "GET");
        }

        public Task<ActiveTriggerResponse> MarkActiveTriggerSentAsync(string id, string userId, string url)
        {
            string requestUrl = $"{url}/{id}/sent?userId={Uri.EscapeDataString(userId)}";
            return SendAsync<ActiveTriggerResponse>(requestUrl, "PATCH");
        }

        // ===== Response / Request DTOs =====

        [Serializable]
        private class UserIdBody
        {
            public string userId;
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

        [Serializable]
        public class ActiveTriggerResponse
        {
            public string id;
            public string playerId;
            public string status;
            public string eventSource;
            public string triggerId;
            public string name;
            public string objective;
            public string playbook;
            public string firstMessage;
            public string eligibleOfferPolicy;
            public string createdAt;
            public string updatedAt;
        }

        [Serializable]
        private class ActiveTriggersArrayWrapper
        {
            public ActiveTriggerResponse[] items;
        }
    }
}
