# OpenAI Service Implementation Documentation

## Overview
Production-grade OpenAI service implementation with robust error handling, retry logic, and token usage tracking.

---

## 🎯 Key Features

### ✅ Core Functionality
- **Async Operations**: All methods are fully async
- **Context Preservation**: Uses conversation summaries for follow-up questions
- **JSON Enforcement**: Strict JSON-only responses from OpenAI
- **Token Tracking**: Displays input/output/total tokens in each response

### ✅ Error Handling
- **Retry Logic**: Automatic retry with exponential backoff (up to 2 attempts)
- **Multiple Parsing Strategies**: 4-layer JSON parsing fallback
- **Graceful Degradation**: Returns user-friendly error messages
- **Comprehensive Logging**: Debug, Info, Warning, and Error logs

### ✅ Configuration
- **Flexible Settings**: Model, temperature, max tokens configurable
- **Safe Defaults**: Sensible defaults if configuration missing
- **Type Safety**: Strongly-typed configuration model

---

## 📋 Configuration

### Required Settings (appsettings.json)
```json
{
  "OpenAI": {
    "ApiKey": "your-openai-api-key-here",
    "Model": "gpt-3.5-turbo",
    "Temperature": "0.25",
    "MaxTokens": "1500",
    "MaxRetryAttempts": "2"
  }
}
```

### Configuration Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `ApiKey` | *Required* | OpenAI API key |
| `Model` | `gpt-3.5-turbo` | OpenAI model to use (gpt-3.5-turbo, gpt-4, etc.) |
| `Temperature` | `0.25` | Response randomness (0.0 = deterministic, 1.0 = creative) |
| `MaxTokens` | `1500` | Maximum tokens in response |
| `MaxRetryAttempts` | `2` | Number of retry attempts on failure |

---

## 🔄 Request Flow

### 1. Input Processing
```
User Input + Optional Summary → Service
```

### 2. Prompt Construction
```
[System Prompt] - Strict JSON rules
     ↓
[Context Message] - Previous conversation summary (if exists)
     ↓
[User Message] - Current user input
```

### 3. API Call
```
OpenAI API Request
  - Model: Configured model
  - Temperature: 0.25
  - Max Tokens: 1500
  - Response Format: JSON
```

### 4. Response Processing
```
Raw Response → JSON Parsing (4 strategies) → Add Token Info → Return
```

---

## 🛡️ Error Handling

### Retry Strategy
1. **Network/API Errors**: Retry with exponential backoff (1s, 2s)
2. **JSON Parsing Errors**: Retry with new API call
3. **Max Retries Exceeded**: Return fallback response
4. **Critical Errors**: Immediate fallback (no retry)

### JSON Parsing Strategies (Sequential)

#### Strategy 1: Direct Deserialization ⚡
```csharp
JsonSerializer.Deserialize<OpenAiResponse>(content)
```
- **Use Case**: Well-formed JSON response
- **Performance**: Fastest

#### Strategy 2: Markdown Code Block Extraction 📝
```regex
```json\s*(\{.*?\})\s*```
```
- **Use Case**: Response wrapped in markdown
- **Example**: ` ```json { "answer": "..." } ``` `

#### Strategy 3: Regex JSON Extraction 🔍
```regex
\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\}
```
- **Use Case**: JSON embedded in text
- **Example**: "Here's your answer: { ... } Hope this helps!"

#### Strategy 4: Fallback Response 🛟
```csharp
return new OpenAiResponse { Answer = rawContent, Summary = "" }
```
- **Use Case**: All parsing strategies failed
- **Behavior**: Returns raw content as answer

---

## 📊 Token Usage Tracking

### Output Format
Every response includes token usage information:

```
[Response content]

[Tokens - Input: 250, Output: 120, Total: 370]
```

### Token Details
- **Input Tokens**: System prompt + context + user message
- **Output Tokens**: AI-generated response
- **Total Tokens**: Input + Output (used for billing)

---

## 🔐 Production-Grade Features

### 1. Logging
```csharp
_logger.LogInformation("Processing request for conversation: {ConversationId}");
_logger.LogWarning("JSON parsing failed on attempt {Attempt}");
_logger.LogError("All retry attempts exhausted");
```

### 2. Dependency Injection
```csharp
public OpenAiService(IConfiguration configuration, ILogger<OpenAiService> logger)
```

### 3. Exception Handling
```csharp
try { /* API call */ }
catch (JsonException) { /* Retry logic */ }
catch (Exception) { /* Error logging + fallback */ }
```

### 4. Exponential Backoff
```csharp
await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount - 1)));
// Retry 1: 1 second
// Retry 2: 2 seconds
```

---

## 🧪 Testing Recommendations

### Unit Tests
- ✅ Test JSON parsing strategies independently
- ✅ Test retry logic with mock failures
- ✅ Test configuration loading
- ✅ Test token usage extraction

### Integration Tests
- ✅ Test with real OpenAI API (use test key)
- ✅ Test conversation continuity with summaries
- ✅ Test error scenarios (invalid API key, network timeout)
- ✅ Test malformed JSON responses

### Load Tests
- ✅ Test concurrent requests
- ✅ Test rate limiting handling
- ✅ Test token quota management

---

## 📝 Usage Example

```csharp
// Inject the service
public class ChatController : ControllerBase
{
    private readonly IOpenAiService _openAiService;
    
    public ChatController(IOpenAiService openAiService)
    {
        _openAiService = openAiService;
    }
    
    // Use the service
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        var response = await _openAiService.GetResponseAsync(
            conversationId: request.ConversationId,
            userInput: request.UserInput,
            summary: previousSummary // From Redis/database
        );
        
        return Ok(response);
    }
}
```

---

## 🚨 Important Notes

### 1. API Key Security
- **Never commit API keys to source control**
- Use environment variables or Azure Key Vault in production
- Rotate keys regularly

### 2. Rate Limiting
- OpenAI has rate limits (requests per minute, tokens per minute)
- Implement rate limiting middleware if needed
- Monitor usage in OpenAI dashboard

### 3. Cost Management
- Set `MaxTokens` appropriately to control costs
- Monitor token usage via logs
- Consider caching for repeated queries

### 4. Model Selection
- **gpt-3.5-turbo**: Fast, cost-effective, good for most cases
- **gpt-4**: More capable but slower and more expensive
- **gpt-4-turbo**: Balance of speed and capability

---

## 🔧 Troubleshooting

### Issue: "OpenAI API key not configured"
**Solution**: Add API key to appsettings.json or environment variables

### Issue: "All retry attempts exhausted"
**Solution**: Check OpenAI API status, network connectivity, API key validity

### Issue: Token usage not showing
**Solution**: Verify OpenAI API response includes usage information

### Issue: JSON parsing always fails
**Solution**: Check system prompt, ensure model supports JSON response format

---

## 📚 References

- [OpenAI .NET SDK Documentation](https://github.com/openai/openai-dotnet)
- [OpenAI API Reference](https://platform.openai.com/docs/api-reference)
- [Best Practices for Production](https://platform.openai.com/docs/guides/production-best-practices)

---

## ✅ Implementation Checklist

- [x] Core service implementation
- [x] Configuration management
- [x] Error handling & retry logic
- [x] JSON parsing strategies
- [x] Token usage tracking
- [x] Logging integration
- [x] Conversation context handling
- [x] Build verification
- [x] Startup verification
- [x] Documentation

---

**Status**: ✅ Production Ready
**Last Updated**: 2026-04-03
