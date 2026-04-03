namespace OpenAiChat.Models;

public class ChatRequest
{
    public string ConversationId { get; set; } = string.Empty;
    public string UserInput { get; set; } = string.Empty;
}