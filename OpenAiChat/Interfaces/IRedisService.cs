using OpenAiChat.Models;

namespace OpenAiChat.Interfaces;

public interface IRedisService
{
    Task<List<Message>> GetMessagesAsync(string conversationId);
    Task AppendMessageAsync(string conversationId, Message message);
    Task<string?> GetSummaryAsync(string conversationId);
    Task SetSummaryAsync(string conversationId, string summary);
}