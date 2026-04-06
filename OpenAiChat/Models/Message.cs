namespace OpenAiChat.Models;

public class Message
{
    public MessageUserType UserType { get; set; }
    public string Content { get; set; } = string.Empty;
    public int MessageOrder { get; set; }
}

public enum MessageUserType
{
    User = 1,
    Agent = 2
}