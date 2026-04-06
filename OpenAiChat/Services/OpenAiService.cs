using OpenAI.Chat;
using System.Text.Json;
using System.Text.RegularExpressions;
using OpenAiChat.Interfaces;
using OpenAiChat.Models;
using OpenAI;

namespace OpenAiChat.Services;

public class OpenAiService : IOpenAiService
{
    private const string SystemPrompt = @"You are a helpful AI assistant that MUST respond ONLY in valid JSON format.

CRITICAL: Your entire response must be ONLY a valid JSON object. No markdown code blocks, no explanations, no text before or after the JSON.

REQUIRED JSON STRUCTURE:
{
  ""answer"": ""string"",
  ""summary"": ""string""
}

FIELD RULES:

1. ""answer"" field:
   - Contains your complete response to the user's question
   - Be clear, concise, and correct
   - If you don't know something, respond with: ""I don't know"" or ""I'm not certain about that""
   - Never make up information or hallucinate facts
   - Prioritize accuracy over completeness

2. ""summary"" field:
   - Update ONLY when new information meaningfully changes the conversation context
   - Contains a brief summary of the conversation state and important context for future turns
   - Include key facts, decisions, preferences, or context that should be remembered
   - Return empty string """" if no context update is needed (e.g., simple questions, clarifications)
   - Keep summaries concise but informative

CONVERSATION CONTINUITY:
- If a summary is provided to you, USE IT to maintain context
- Never ask users to repeat information that's in the summary
- Build upon previous conversation naturally
- Reference past context when relevant

BEHAVIOR GUIDELINES:
- Be consistent in your responses across the conversation
- Prioritize correctness over verbosity
- Stay focused on the user's actual question
- Don't add unnecessary explanations unless asked

EXAMPLES:

User: ""What is 2+2?""
Response:
{
  ""answer"": ""2+2 equals 4."",
  ""summary"": """"
}

User: ""My name is John and I prefer Python.""
Response:
{
  ""answer"": ""Nice to meet you, John! I'll keep in mind that you prefer Python for any coding examples or discussions."",
  ""summary"": ""User's name is John. Prefers Python programming language.""
}

User: ""What's my name?"" (with summary: ""User's name is John. Prefers Python."")
Response:
{
  ""answer"": ""Your name is John."",
  ""summary"": """"
}

User: ""What is the capital of Atlantis?""
Response:
{
  ""answer"": ""I don't know. Atlantis is a legendary fictional island, so it doesn't have a real capital city."",
  ""summary"": """"
}

REMEMBER: Output ONLY the JSON object. Nothing else.";

    private readonly ChatClient _chatClient;
    private readonly OpenAiSettings _settings;
    private readonly ILogger<OpenAiService> _logger;
    private readonly IRedisService _redisService;
    private readonly JsonSerializerOptions _jsonOptions;

    public OpenAiService(IConfiguration configuration, ILogger<OpenAiService> logger, IRedisService redisService)
    {
        _logger = logger;
        _redisService = redisService;

        // Load settings from configuration
        _settings = new OpenAiSettings
        {
            ApiKey = configuration["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI API key not configured"),
            Model = configuration["OpenAI:Model"] ?? "gpt-4o",
            Temperature = double.TryParse(configuration["OpenAI:Temperature"], out var temp) ? temp : 0.25,
            MaxTokens = int.TryParse(configuration["OpenAI:MaxTokens"], out var tokens) ? tokens : 1500,
            MaxRetryAttempts = int.TryParse(configuration["OpenAI:MaxRetryAttempts"], out var retries) ? retries : 2
        };

        // Initialize OpenAI client
        var openAiClient = new OpenAIClient(_settings.ApiKey);
        _chatClient = openAiClient.GetChatClient(_settings.Model);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        _logger.LogInformation("OpenAI Service initialized with model: {Model}, temperature: {Temperature}, max tokens: {MaxTokens}",
            _settings.Model, _settings.Temperature, _settings.MaxTokens);
    }

    public async Task<OpenAiResponse> GetResponseAsync(string conversationId, string userInput, string? knowledgeBase = null)
    {
        if (string.IsNullOrWhiteSpace(userInput))
        {
            return new OpenAiResponse
            {
                Answer = "Please provide a valid input.",
                Summary = string.Empty
            };
        }

        // Step 1: Get Conversation from redis
        Conversation conversation = await _redisService.GetConversation(conversationId)
            ?? new Conversation { ConversationId = conversationId, Messages = [], Summary = "", TotalTokensUsed = 0 };

        int retryCount = 0;
        Exception? lastException = null;

        while (retryCount <= _settings.MaxRetryAttempts)
        {
            try
            {
                _logger.LogInformation("Processing request for conversation: {ConversationId}, Attempt: {Attempt}",
                    conversationId, retryCount + 1);

                // Step 2: Call OpenAI API (returns answer + summary + token usage)
                //var (response, tokenUsage) = await CallOpenAiApiAsync(userInput, summary, knowledgeBase);
                var inputToken = new Random().Next(0,10000);
                var outputToken = new Random().Next(0,10000);
                var (response, tokenUsage) = (
                    new OpenAiResponse { Answer = "I have given my answer", Summary = "This is the summary"},
                    new TokenUsage { InputTokens = inputToken, OutputTokens = outputToken, TotalTokens = inputToken + outputToken }
                );

                var messageCount = conversation.Messages?.Count ?? 0;
                // Step 3: Save user message
                ++messageCount;
                conversation?.Messages?.Add(new Message { UserType = MessageUserType.User, Content = userInput, MessageOrder = messageCount });

                // Step 4: Save AI response
                ++messageCount;
                conversation?.Messages?.Add(new Message { UserType = MessageUserType.Agent, Content = response.Answer, MessageOrder = messageCount });

                // Step 5: Update summary only if new summary is not empty
                if (!string.IsNullOrWhiteSpace(response.Summary))
                {
                    conversation?.Summary = response.Summary;
                }

                // Step 6: Add token usage to conversation total
                conversation?.TotalTokensUsed = (conversation?.TotalTokensUsed ?? 0) + tokenUsage.TotalTokens;

                // Step 7: Set the new conversation to redis
                await _redisService.SetConversation(conversation!);
                _logger.LogInformation("Successfully processed request for conversation: {ConversationId}", conversationId);

                return response;
            }
            catch (JsonException jsonEx)
            {
                _logger.LogWarning(jsonEx, "JSON parsing failed on attempt {Attempt} for conversation: {ConversationId}",
                    retryCount + 1, conversationId);
                lastException = jsonEx;
                retryCount++;

                if (retryCount <= _settings.MaxRetryAttempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount - 1))); // Exponential backoff
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OpenAI API for conversation: {ConversationId}", conversationId);
                lastException = ex;
                break; // Don't retry on non-JSON errors
            }
        }

        // Fallback response
        _logger.LogError("All retry attempts exhausted for conversation: {ConversationId}. Last error: {Error}",
            conversationId, lastException?.Message);

        return new OpenAiResponse
        {
            Answer = "I apologize, but I encountered an error processing your request. Please try again.",
            Summary = conversation.Summary ?? string.Empty
        };
    }

    private async Task<(OpenAiResponse response, TokenUsage tokenUsage)> CallOpenAiApiAsync(string userInput, string? summary, string? knowledgeBase)
    {
        // Build message list
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(SystemPrompt)
        };

        // Add knowledge base context if provided
        if (!string.IsNullOrWhiteSpace(knowledgeBase))
        {
            messages.Add(new SystemChatMessage(
                $"KNOWLEDGE BASE - You MUST use the following information as the authoritative source when answering the user's question. " +
                $"Do NOT use external knowledge if the answer can be found here. If the user's question is related to this content, " +
                $"answer ONLY using information from this knowledge base. If the information is not in the knowledge base, clearly state that.\n\n" +
                $"{knowledgeBase}"));
        }

        // Add context if summary exists
        if (!string.IsNullOrWhiteSpace(summary))
        {
            messages.Add(new SystemChatMessage($"Previous conversation context: {summary}"));
        }

        // Add user input
        messages.Add(new UserChatMessage(userInput));

        // Configure chat completion options
        var options = new ChatCompletionOptions
        {
            Temperature = (float)_settings.Temperature,
            MaxOutputTokenCount = _settings.MaxTokens,
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };

        // Call OpenAI API
        var completion = await _chatClient.CompleteChatAsync(messages, options);

        // Extract token usage
        var usage = completion.Value.Usage;
        var tokenUsage = new TokenUsage
        {
            InputTokens = usage.InputTokenCount,
            OutputTokens = usage.OutputTokenCount,
            TotalTokens = usage.TotalTokenCount
        };

        _logger.LogInformation("OpenAI API call completed. [Tokens - Input: {Input}, Output: {Output}, Total: {Total}]", 
            tokenUsage.InputTokens, tokenUsage.OutputTokens, tokenUsage.TotalTokens);

        // Get response content
        var responseContent = completion.Value.Content[0].Text;

        // Parse JSON response with fallback strategies
        var parsedResponse = ParseJsonResponse(responseContent);

        return (parsedResponse, tokenUsage);
    }

    private OpenAiResponse ParseJsonResponse(string content)
    {
        // Strategy 1: Direct JSON deserialization
        try
        {
            var response = JsonSerializer.Deserialize<OpenAiResponse>(content, _jsonOptions);
            if (response != null && !string.IsNullOrEmpty(response.Answer))
            {
                _logger.LogDebug("Successfully parsed JSON response using direct deserialization");
                return response;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Direct JSON parsing failed, trying fallback strategies");
        }

        // Strategy 2: Extract JSON from markdown code blocks
        var jsonMatch = Regex.Match(content, @"```json\s*(\{.*?\})\s*```", RegexOptions.Singleline);
        if (jsonMatch.Success)
        {
            try
            {
                var response = JsonSerializer.Deserialize<OpenAiResponse>(jsonMatch.Groups[1].Value, _jsonOptions);
                if (response != null && !string.IsNullOrEmpty(response.Answer))
                {
                    _logger.LogDebug("Successfully parsed JSON response from markdown code block");
                    return response;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogDebug(ex, "Markdown code block JSON parsing failed");
            }
        }

        // Strategy 3: Extract any JSON object from text
        var objectMatch = Regex.Match(content, @"\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\}", RegexOptions.Singleline);
        if (objectMatch.Success)
        {
            try
            {
                var response = JsonSerializer.Deserialize<OpenAiResponse>(objectMatch.Value, _jsonOptions);
                if (response != null && !string.IsNullOrEmpty(response.Answer))
                {
                    _logger.LogDebug("Successfully parsed JSON response using regex extraction");
                    return response;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogDebug(ex, "Regex extraction JSON parsing failed");
            }
        }

        // Strategy 4: Fallback - create response from raw content
        _logger.LogWarning("All JSON parsing strategies failed. Using raw content as answer");
        return new OpenAiResponse
        {
            Answer = content,
            Summary = string.Empty
        };
    }
}