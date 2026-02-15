using System;
using System.Threading.Tasks;
using UnityEngine;

namespace LP
{
    public class ConversationController
    {
        private readonly ConversationData _model;
        private readonly HttpService _httpService;
        private readonly WebSocketService _webSocketService;

        public event Action<TextEntry> OnTextAdded;

        public ConversationData Model => _model;

        public ConversationController(ConversationData model, HttpService httpService, WebSocketService webSocketService)
        {
            _model = model;
            _httpService = httpService;
            _webSocketService = webSocketService;

            // Subscribe to incoming WebSocket messages
            _webSocketService.OnMessageReceived += HandleMessageReceived;
        }

        // ===== Commands =====

        public async Task StartConversation(string userId, string baseUrl, Action<bool> onConversationStarted)
        {
            // 1. Boot via HTTP to get WebSocket URL
            try
            {
                var bootResponse = await _httpService.BootAsync(userId, baseUrl + "boot");
                Debug.Log($"Boot successful - Session: {bootResponse.sessionId}");

                // 2. Start conversation to get conversationId
                var startConversationResponse =
                    await _httpService.StartConversationAsync(userId, baseUrl + "start-conversation");
                Debug.Log($"Conversation started - ID: {startConversationResponse.conversationId}");

                // 3. Connect to WebSocket
                await _webSocketService.ConnectAsync(bootResponse.signedUrl);

                // 4. Send initialization message with conversationId
                await _webSocketService.SendInitMessageAsync(bootResponse.userId,
                    startConversationResponse.conversationId);
            }
            catch(Exception ex)
            {
                Debug.LogError($"Failed to start conversation: {ex.Message}");
                onConversationStarted(false);
            }
            
            onConversationStarted(true);
        }

        public async Task SendMessage(string message)
        {
            await _webSocketService.SendTextMessageAsync(message);
            AddText(message, "User");
        }

        public async Task Disconnect()
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
            AddText(message, "Agent");
        }
        
        public void Dispose()
        {
            _webSocketService.OnMessageReceived -= HandleMessageReceived;
            _webSocketService.DisconnectAsync().Forget();
        }
    }
}
