using System;

namespace LP
{
    [Serializable]
    public class ClientConfig
    {
        public string BaseUrl = "https://api.example.com";
        public string ApiKey = "";
        public int TimeoutSeconds = 30;

        // TODO: Add endpoint paths here when backend API is ready
        // Example: public string SendMessageEndpoint = "/api/messages";
    }
}
