using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.VisualBasic;
using OpenAiChat.Interfaces;
using OpenAiChat.Models;
using StackExchange.Redis;
using System.Text.Json;

namespace OpenAiChat.Services;

public class RedisService : IRedisService
{
    private readonly IDatabase _database;
    private readonly TimeSpan _expiryTime = TimeSpan.FromHours(2);

    public RedisService(IConfiguration configuration)
    {
        var connectionString = configuration["Redis:ConnectionString"]
            ?? throw new InvalidOperationException("Redis connection string not configured");

        var redis = ConnectionMultiplexer.Connect(connectionString);
        _database = redis.GetDatabase();
    }

    public async Task<Conversation?> GetConversation(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return null;
        }
        var key = GetConversationKey(conversationId);
        var hashFields = await _database.HashGetAllAsync(key);
        if (hashFields.Length == 0)
        {
            return null;
        }

        var messagesKey = GetMessagesKey(conversationId);
        var messageJsons = await _database.ListRangeAsync(messagesKey, 0, -1);
        var messages = messageJsons.Select(json => JsonSerializer.Deserialize<Message>(json.ToString())).OfType<Message>().ToList();

        return new Conversation
        {
            ConversationId = hashFields.FirstOrDefault(f => f.Name == nameof(Conversation.ConversationId)).Value.ToString(),
            Messages = messages,
            Summary = hashFields.FirstOrDefault(f => f.Name == nameof(Conversation.Summary)).Value.ToString(),
            TotalTokensUsed = int.Parse(hashFields.FirstOrDefault(f => f.Name == nameof(Conversation.TotalTokensUsed)).Value.ToString())
        };
    }

    public async Task SetConversation(Conversation conversation)
    {
        var key = GetConversationKey(conversation.ConversationId);
        var hashEntries = new HashEntry[]
        {
            new(nameof(Conversation.ConversationId), conversation.ConversationId),
            new(nameof(Conversation.Summary), conversation.Summary),
            new(nameof(Conversation.TotalTokensUsed), conversation.TotalTokensUsed.ToString())
        };
        await _database.HashSetAsync(key, hashEntries);

        var messagesKey = GetMessagesKey(conversation.ConversationId);
        await _database.KeyDeleteAsync(messagesKey); // Clear existing messages
        var messageJsons = conversation.Messages.Select(m => (RedisValue)JsonSerializer.Serialize(m)).ToArray();
        if (messageJsons.Length > 0)
        {
            await _database.ListRightPushAsync(messagesKey, messageJsons);
        }

        await _database.KeyExpireAsync(key, _expiryTime);
    }

    public async Task SetConversationId(string conversationId)
    {
        var conversationKey = GetConversationKey(conversationId);
        await _database.HashSetAsync(conversationKey, nameof(Conversation.ConversationId), conversationId);
        await _database.KeyExpireAsync(conversationKey, _expiryTime);
    }

    public async Task AddMessageAsync(string conversationId, Message message)
    {
        var messagesKey = GetMessagesKey(conversationId);
        var messageJson = JsonSerializer.Serialize(message);
        await _database.ListRightPushAsync(messagesKey, messageJson);
        await _database.KeyExpireAsync(messagesKey, _expiryTime);

        // Optionally refresh expiry on the main key
        var key = GetConversationKey(conversationId);
        await _database.KeyExpireAsync(key, _expiryTime);
    }

    public async Task UpdateSummaryAsync(string conversationId, string summary)
    {
        var key = GetConversationKey(conversationId);
        await _database.HashSetAsync(key, nameof(Conversation.Summary), summary);

        // Optionally refresh expiry
        await _database.KeyExpireAsync(key, _expiryTime);
    }

    public async Task UpdateTotalTokensUsedAsync(string conversationId, int totalTokens)
    {
        var key = GetConversationKey(conversationId);
        //await _database.HashSetAsync(key, "TotalTokensUsed", totalTokens.ToString());
        await _database.HashIncrementAsync(key, nameof(Conversation.TotalTokensUsed), totalTokens);

        // Optionally refresh expiry
        await _database.KeyExpireAsync(key, _expiryTime);
    }

    public async Task<int> GetMessageCountAsync(string conversationId)
    {
        var messagesKey = GetMessagesKey(conversationId);
        var length = await _database.ListLengthAsync(messagesKey);
        return (int)length;
    }

    private static string GetConversationKey(string conversationId)
        => $"conversation:{conversationId}";

    private static string GetMessagesKey(string conversationId)
        => $"conversation:{conversationId}:messages";
}