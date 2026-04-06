namespace OpenAiChat.Interfaces;

public interface IOpenAiService
{
    Task<string> GetResponseAsync(string conversationId, string userInput, string? knowledgeBase = null);
}
