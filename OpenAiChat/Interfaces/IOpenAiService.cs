public interface IOpenAiService
{
    Task<OpenAiResponse> GetResponseAsync(string conversationId, string userInput, string summary);
}