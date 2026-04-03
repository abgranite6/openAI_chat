using StackExchange.Redis;
using System.Text.Json;

public class RedisService : IRedisService
{
    private readonly IDatabase _database;

    public RedisService(IConfiguration configuration)
    {
        var connectionString = configuration["Redis:ConnectionString"];
        var redis = ConnectionMultiplexer.Connect(connectionString);
        _database = redis.GetDatabase();
    }

    public async Task<ConversationHistory> GetConversationHistoryAsync(string conversationId)
    {
        var json = await _database.StringGetAsync(conversationId);
        if (json.IsNullOrEmpty)
        {
            return new ConversationHistory { ConversationId = conversationId, Summary = "" };
        }
        return JsonSerializer.Deserialize<ConversationHistory>(json.ToString()) ?? new ConversationHistory { ConversationId = conversationId, Summary = "" };
    }

    public async Task SaveConversationHistoryAsync(ConversationHistory history)
    {
        var json = JsonSerializer.Serialize(history);
        await _database.StringSetAsync(history.ConversationId, json);
    }
}