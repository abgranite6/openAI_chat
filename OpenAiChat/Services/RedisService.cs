using OpenAiChat.Interfaces;
using OpenAiChat.Models;
using StackExchange.Redis;
using System.Text.Json;

namespace OpenAiChat.Services;

public class RedisService : IRedisService
{
    private readonly IDatabase _database;

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
        var data = await _database.StringGetAsync(key);
        if (data.IsNullOrEmpty)
        {
            return null;
        }
        return JsonSerializer.Deserialize<Conversation>(data.ToString());
    }

    public async Task SetConversation(Conversation conversation)
    {
        var key = GetConversationKey(conversation.ConversationId);
        var json = JsonSerializer.Serialize(conversation);
        await _database.StringSetAsync(key, json, TimeSpan.FromHours(1));
    }

    private static string GetConversationKey(string conversationId)
        => $"conversation:{conversationId}";
}