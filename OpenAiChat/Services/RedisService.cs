using OpenAiChat.Interfaces;
using OpenAiChat.Models;
using StackExchange.Redis;
using System.Text.Json;

namespace OpenAiChat.Services;

public class RedisService : IRedisService
{
    private readonly IDatabase _database;
    private readonly JsonSerializerOptions _jsonOptions;

    public RedisService(IConfiguration configuration)
    {
        var connectionString = configuration["Redis:ConnectionString"]
            ?? throw new InvalidOperationException("Redis connection string not configured");

        var redis = ConnectionMultiplexer.Connect(connectionString);
        _database = redis.GetDatabase();

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task<List<Message>> GetMessagesAsync(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return new List<Message>();
        }

        //var key = GetMessagesKey(conversationId);
        var key = GetConversationKey(conversationId);
        var data = await _database.StringGetAsync(key);

        if (data.IsNullOrEmpty)
        {
            return new List<Message>();
        }

        var conversation = JsonSerializer.Deserialize<Conversation>(data.ToString(), _jsonOptions);
        return conversation?.Messages?.OrderBy(m => m.MessageOrder)?.ToList() ?? new List<Message>();
    }

    public async Task AppendMessageAsync(string conversationId, Message message)
    {
        if (string.IsNullOrWhiteSpace(conversationId) || message == null)
        {
            return;
        }

        var existingMessages = await GetMessagesAsync(conversationId);

        // Set the message order to preserve order
        message.MessageOrder = existingMessages.Count + 1;
        message.ConversationId = conversationId;

        existingMessages.Add(message);

        await SetConversation(
            conversationId,
            existingMessages,
            await GetSummaryAsync(conversationId) ?? string.Empty
        );
    }

    public async Task<string?> GetSummaryAsync(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return null;
        }

        //var key = GetSummaryKey(conversationId);
        var key = GetConversationKey(conversationId);
        var data = await _database.StringGetAsync(key);
        if (data.IsNullOrEmpty)
        {
            return "";
        }

        var conversation = JsonSerializer.Deserialize<Conversation>(data.ToString(), _jsonOptions);

        return conversation?.Summary ?? "";
    }

    public async Task SetSummaryAsync(string conversationId, string summary)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return;
        }

        var existingMessages = await GetMessagesAsync(conversationId);

        await SetConversation(
            conversationId,
            existingMessages,
            summary
        );
    }

    public async Task<int> GetTotalTokensUsedAsync(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return 0;
        }

        var conversation = await GetConversation(conversationId);
        return conversation?.TotalTokensUsed ?? 0;
    }

    public async Task AddTokenUsageAsync(string conversationId, int tokensUsed)
    {
        if (string.IsNullOrWhiteSpace(conversationId) || tokensUsed <= 0)
        {
            return;
        }

        var conversation = await GetConversation(conversationId);
        var totalTokenUsed = conversation?.TotalTokensUsed ?? 0;
        totalTokenUsed += tokensUsed;
        conversation?.TotalTokensUsed = totalTokenUsed;
        await SetConversation(
            conversationId,
            conversation?.Messages ?? new List<Message>(),
            conversation?.Summary ?? string.Empty
        );
    }

    private async Task<Conversation?> GetConversation(string conversationId)
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
        return JsonSerializer.Deserialize<Conversation>(data.ToString(), _jsonOptions);
    }

    private async Task SetConversation(string conversationId, List<Message> messages, string summary)
    {
        var key = GetConversationKey(conversationId);

        var history = new Conversation
        {
            ConversationId = conversationId,
            Messages = messages,
            Summary = summary
        };

        var json = JsonSerializer.Serialize(history, _jsonOptions);
        await _database.StringSetAsync(key, json);
    }

    private static string GetConversationKey(string conversationId)
        => $"conversation:{conversationId}";
}