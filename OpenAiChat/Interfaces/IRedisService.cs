public interface IRedisService
{
    Task<ConversationHistory> GetConversationHistoryAsync(string conversationId);
    Task SaveConversationHistoryAsync(ConversationHistory history);
}