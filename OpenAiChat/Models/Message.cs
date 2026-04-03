namespace OpenAiChat.Models;

public class Message
{
    public string ConversationId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int MessageOrder { get; set; }
}