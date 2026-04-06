using OpenAiChat.Models;

namespace OpenAiChat.Interfaces;

public interface IRedisService
{
    Task<Conversation?> GetConversation(string conversationId);
    Task SetConversation(Conversation conversation);
}