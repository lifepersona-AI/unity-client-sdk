using System;
using UnityEngine;

namespace LP
{
    public class ChatController
    {
        private readonly ChatModel _model;
        private readonly HttpService _httpService;
        private readonly WebSocketService _webSocketService;

        public event Action<TextEntry> OnTextAdded;

        public ChatModel Model => _model;

        public ChatController(ChatModel model, HttpService httpService, WebSocketService webSocketService)
        {
            _model = model;
            _httpService = httpService;
            _webSocketService = webSocketService;

            // Subscribe to incoming WebSocket messages
            _webSocketService.OnMessageReceived += HandleMessageReceived;
        }

        // ===== Commands =====

        public async Awaitable StartConversation(string userId, string bootUrl)
        {
            // 1. Boot via HTTP to get WebSocket URL
            var bootResponse = await _httpService.BootAsync(userId, bootUrl);
            AddText($"Boot successful - Session: {bootResponse.sessionId}", "System");

            // 2. Start conversation to get conversationId
            var startConversationResponse = await _httpService.StartConversationAsync(userId, "https://api.lifepersona.ai/api/client/start-conversation");
            AddText($"Conversation started - ID: {startConversationResponse.conversationId}", "System");

           // 3. Connect to WebSocket
            await _webSocketService.ConnectAsync(bootResponse.signedUrl);
            AddText("WebSocket connected", "System");

            // 4. Send initialization message with conversationId
            await _webSocketService.SendInitMessageAsync(bootResponse.userId, startConversationResponse.conversationId);
            AddText("Initialization message sent", "System");
        }

        public async Awaitable SendMessage(string message)
        {
            await _webSocketService.SendTextMessageAsync(message);
            AddText(message, "User");
        }

        public async Awaitable Disconnect()
        {
            await _webSocketService.DisconnectAsync();
            AddText("Disconnected", "System");
        }

        // ===== Model Operations =====

        public void AddText(string message, string label = "Chat")
        {
            var entry = new TextEntry(message, label);
            _model.AddEntry(entry);
            OnTextAdded?.Invoke(entry);
        }

        public void Clear()
        {
            _model.Clear();
        }

        // ===== Event Handlers =====

        private void HandleMessageReceived(string message)
        {
            AddText(message, "Server");
        }
        
        public void Dispose()
        {
            _webSocketService.OnMessageReceived -= HandleMessageReceived;
            _webSocketService.DisconnectAsync().Forget();
        }
    }
}
