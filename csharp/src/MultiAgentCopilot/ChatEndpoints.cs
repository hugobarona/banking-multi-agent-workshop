﻿using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using MultiAgentCopilot.Services;


namespace MultiAgentCopilot
{
    public class ChatEndpoints
    {
        private readonly ChatService _chatService;

        public ChatEndpoints(ChatService chatService)
        {
            _chatService = chatService;
        }

        public void Map(WebApplication app)
        {

            app.MapGet("/tenant/{tenantId}/user/{userId}/sessions/", async (string tenantId, string userId) =>
                await _chatService.GetAllChatSessionsAsync(tenantId, userId))
                .WithName("GetAllChatSessions");

            app.MapGet("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}/messages",
                    async (string tenantId, string userId, string sessionId) =>
                    await _chatService.GetChatSessionMessagesAsync(tenantId, userId, sessionId))
                .WithName("GetChatSessionMessages");

            app.MapPost("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}/message/{messageId}/rate",
                    async (string tenantId, string userId, string messageId, string sessionId, bool? rating) =>
                    await _chatService.RateChatCompletionAsync(tenantId, userId, messageId, sessionId, rating))
                .WithName("RateMessage");

            app.MapGet("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}/completiondetails/{debuglogId}",
                    async (string tenantId, string userId, string sessionId, string debuglogId) =>
                    await _chatService.GetChatCompletionDebugLogAsync(tenantId, userId, sessionId, debuglogId))
                .WithName("GetChatCompletionDetails");

            app.MapPost("/tenant/{tenantId}/user/{userId}/sessions/", async (string tenantId, string userId) =>
                await _chatService.CreateNewChatSessionAsync(tenantId, userId))
                .WithName("CreateNewChatSession");

            app.MapPost("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}/rename", async (string tenantId, string userId, string sessionId, string newChatSessionName) =>
                    await _chatService.RenameChatSessionAsync(tenantId, userId, sessionId, newChatSessionName))
                .WithName("RenameChatSession");

            app.MapDelete("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}", async (string tenantId, string userId, string sessionId) =>
                    await _chatService.DeleteChatSessionAsync(tenantId, userId, sessionId))
                .WithName("DeleteChatSession");

            app.MapPost("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}/completion", async (string tenantId, string userId, string sessionId, [FromBody] string userPrompt) =>
                    await _chatService.GetChatCompletionAsync(tenantId, userId, sessionId, userPrompt))
                .WithName("GetChatCompletionDebugLogAsync");

            app.MapPost("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}/summarize-name", async (string tenantId, string userId, string sessionId, [FromBody] string prompt) =>
                    await _chatService.SummarizeChatSessionNameAsync(tenantId, userId, sessionId, prompt))
                .WithName("SummarizeChatSessionName");


            app.MapPut("/offerdata", async ([FromBody] JsonElement document) =>
                    await _chatService.AddDocument("OfferData", document))
                .WithName("AddOfferData");

            app.MapPut("/accountdata", async ([FromBody] JsonElement document) =>
                    await _chatService.AddDocument("AccountData", document))
                .WithName("AddAccountData");

            app.MapPut("/userdata", async ([FromBody] JsonElement document) =>
                    await _chatService.AddDocument("UserData", document))
                .WithName("AddUserData");


        }
    }
}
