using OpenAI.Chat;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;

public class OpenAiService : IOpenAiService
{
    private readonly ChatClient _chatClient;

    public OpenAiService(IConfiguration configuration)
    {
        var apiKey = configuration["OpenAI:ApiKey"];
        var client = new OpenAI.OpenAIClient(apiKey);
        _chatClient = client.GetChatClient("gpt-4o-mini");
    }

    public async Task<OpenAiResponse> GetResponseAsync(string conversationId, string userInput, string summary)
    {
        var prompt = $"Based on this conversation summary: \"{summary}\". Answer the user's question: \"{userInput}\". Return ONLY a JSON object with 'answer' and 'summary'. Update 'summary' only if the context has significantly changed, otherwise set 'summary' to empty string \"\".";
        var response = await _chatClient.CompleteChatAsync(prompt);
        var content = response.Value.Content.ToString();
        var result = JsonSerializer.Deserialize<OpenAiResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return result ?? new OpenAiResponse { Answer = "Error parsing response", Summary = "" };
    }
}