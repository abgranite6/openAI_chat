using OpenAiChat.Models;

namespace OpenAiChat.Interfaces;

public interface IRedisService
{
    Task<Conversation?> GetConversation(string conversationId);
    Task SetConversation(Conversation conversation);
    Task SetConversationId(string conversationId);
    Task<int> GetMessageCountAsync(string conversationId);
    Task AddMessageAsync(string conversationId, Message message);
    Task UpdateSummaryAsync(string conversationId, string summary);
    Task UpdateTotalTokensUsedAsync(string conversationId, int totalTokens);
}