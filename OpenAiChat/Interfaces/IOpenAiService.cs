namespace OpenAiChat.Interfaces;

public interface IOpenAiService
{
    Task<Models.OpenAiResponse> GetResponseAsync(
        string conversationId,
        string userInput,
        string? knowledgeBase = null);
}