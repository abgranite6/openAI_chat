# Conversation Flow with Token Tracking - Implementation Guide

## Overview
Complete conversation flow implementation with automatic token usage tracking per conversation.

---

## 🎯 Key Features

✅ **Automatic Summary Management** - Gets and updates summary from Redis  
✅ **Token Tracking** - Accumulates total tokens used per conversation  
✅ **Single API Call** - Only calls OpenAI once per request  
✅ **Smart Summary Updates** - Updates summary only when AI provides new one  
✅ **Message Persistence** - Saves both user and AI messages  
✅ **Async Throughout** - All operations are fully async  

---

## 🔄 Complete Flow

### Step-by-Step Execution

```
1. User sends message
   ↓
2. Service gets summary from Redis
   ↓
3. Service calls OpenAI API
   • Uses system prompt
   • Uses knowledge base (if provided)
   • Uses conversation summary
   • Uses user input
   ↓
4. OpenAI returns JSON:
   {
     "answer": "...",
     "summary": "..."
   }
   ↓
5. Service saves USER message to Redis
   ↓
6. Service saves AI RESPONSE to Redis
   ↓
7. Service updates summary (ONLY if new summary != "")
   ↓
8. Service adds token usage to conversation total
   ↓
9. Service returns response with token info
```

---

## 📊 Data Models

### ConversationHistory (Updated)
```csharp
public class ConversationHistory
{
    public string ConversationId { get; set; }
    public List<Message> Messages { get; set; }
    public string Summary { get; set; }
    public int TotalTokensUsed { get; set; }  // NEW: Accumulated tokens
}
```

### TokenUsage (New)
```csharp
public class TokenUsage
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }
}
```

---

## 🔌 Redis Service Updates

### New Methods

#### 1. GetConversationHistoryAsync
```csharp
Task<ConversationHistory> GetConversationHistoryAsync(string conversationId);
```
**Purpose**: Get complete conversation including messages, summary, and token count

#### 2. SaveConversationAsync
```csharp
Task SaveConversationAsync(ConversationHistory conversation);
```
**Purpose**: Save entire conversation state to Redis

#### 3. GetTotalTokensUsedAsync
```csharp
Task<int> GetTotalTokensUsedAsync(string conversationId);
```
**Purpose**: Retrieve total tokens used for a conversation

#### 4. AddTokenUsageAsync
```csharp
Task<int> AddTokenUsageAsync(string conversationId, int tokensUsed);
```
**Purpose**: Add tokens to conversation total (accumulates)

---

## 💡 OpenAI Service Flow

### Updated GetResponseAsync

```csharp
public async Task<OpenAiResponse> GetResponseAsync(
    string conversationId, 
    string userInput, 
    string? summary,        // Can be null - will auto-fetch from Redis
    string? knowledgeBase = null)
{
    // 1. Auto-fetch summary if not provided
    if (string.IsNullOrWhiteSpace(summary))
    {
        summary = await _redisService.GetSummaryAsync(conversationId);
    }

    // 2. Call OpenAI (single call)
    var (response, tokenUsage) = await CallOpenAiApiAsync(...);

    // 3. Save user message
    await _redisService.AppendMessageAsync(conversationId, userMessage);

    // 4. Save AI response
    await _redisService.AppendMessageAsync(conversationId, aiMessage);

    // 5. Update summary ONLY if new summary exists
    if (!string.IsNullOrWhiteSpace(response.Summary))
    {
        await _redisService.SetSummaryAsync(conversationId, response.Summary);
    }

    // 6. Accumulate token usage
    await _redisService.AddTokenUsageAsync(conversationId, tokenUsage.TotalTokens);

    // 7. Get conversation total
    var totalTokens = await _redisService.GetTotalTokensUsedAsync(conversationId);

    // 8. Append token info to response
    response.Answer += $"\n\n[Tokens - Input: {tokenUsage.InputTokens}, " +
                       $"Output: {tokenUsage.OutputTokens}, " +
                       $"Total: {tokenUsage.TotalTokens}] " +
                       $"[Conversation Total: {totalTokens}]";

    return response;
}
```

---

## 📝 Usage Examples

### Example 1: First Message in Conversation
```csharp
var response = await _openAiService.GetResponseAsync(
    conversationId: "conv-001",
    userInput: "My name is Alice",
    summary: null  // No summary yet
);

// Response:
// "Nice to meet you, Alice!"
// [Tokens - Input: 120, Output: 50, Total: 170]
// [Conversation Total: 170]

// Redis stores:
// - Messages: [user: "My name is Alice", ai: "Nice to meet you..."]
// - Summary: "User's name is Alice"
// - TotalTokensUsed: 170
```

### Example 2: Follow-up Message
```csharp
var response = await _openAiService.GetResponseAsync(
    conversationId: "conv-001",
    userInput: "What's my name?",
    summary: null  // Will auto-fetch "User's name is Alice"
);

// Response:
// "Your name is Alice."
// [Tokens - Input: 130, Output: 40, Total: 170]
// [Conversation Total: 340]  ← 170 + 170

// Redis updates:
// - Messages: [... previous ..., user: "What's my name?", ai: "Your name is Alice."]
// - Summary: "User's name is Alice" (unchanged - new summary was "")
// - TotalTokensUsed: 340 ← Accumulated
```

### Example 3: With Knowledge Base
```csharp
var kb = "Product X: $99, Warranty: 2 years";

var response = await _openAiService.GetResponseAsync(
    conversationId: "conv-002",
    userInput: "What's the warranty?",
    summary: null,
    knowledgeBase: kb
);

// Response:
// "The warranty for Product X is 2 years."
// [Tokens - Input: 250, Output: 60, Total: 310]
// [Conversation Total: 310]
```

---

## 🧠 Smart Summary Logic

### When Summary Updates

**Scenario 1: AI provides new summary**
```json
{
  "answer": "Your name is Bob.",
  "summary": "User's name is Bob. Prefers email communication."
}
```
✅ Redis summary = "User's name is Bob. Prefers email communication."

**Scenario 2: AI returns empty summary**
```json
{
  "answer": "2+2 equals 4.",
  "summary": ""
}
```
✅ Redis summary = (unchanged - keeps previous summary)

**Scenario 3: Simple clarification**
```json
{
  "answer": "Yes, that's correct.",
  "summary": ""
}
```
✅ Redis summary = (unchanged - no new context to remember)

---

## 📊 Token Usage Tracking

### Token Information in Response

Every response includes:
```
[Tokens - Input: X, Output: Y, Total: Z] [Conversation Total: T]
```

**Breakdown:**
- **Input**: System prompt + knowledge base + summary + user message
- **Output**: AI-generated response
- **Total**: Input + Output (this request)
- **Conversation Total**: Accumulated total across all requests

### Example Progression
```
Request 1: [Total: 200] [Conversation Total: 200]
Request 2: [Total: 150] [Conversation Total: 350]  ← 200 + 150
Request 3: [Total: 180] [Conversation Total: 530]  ← 350 + 180
```

---

## 🎯 Optimization Features

### 1. Single API Call
- ✅ OpenAI called only ONCE per request
- ✅ No redundant API calls
- ✅ Minimizes latency

### 2. Auto Summary Fetch
- ✅ Automatically retrieves summary from Redis if not provided
- ✅ No manual summary management needed
- ✅ Always uses latest summary

### 3. Conditional Summary Update
- ✅ Only updates summary when AI provides new information
- ✅ Preserves existing summary if new one is empty
- ✅ Reduces unnecessary writes

### 4. Token Accumulation
- ✅ Tracks total tokens per conversation
- ✅ Useful for cost monitoring
- ✅ Helps identify expensive conversations

---

## 🔧 Integration Example

### Complete Controller Implementation
```csharp
[HttpPost]
public async Task<IActionResult> Chat([FromBody] ChatRequest request)
{
    // Just pass to service - it handles everything!
    var response = await _openAiService.GetResponseAsync(
        request.ConversationId,
        request.UserInput,
        null,  // Service auto-fetches summary
        request.KnowledgeBase
    );

    return Ok(response);
}
```

**That's it!** The service handles:
- ✅ Fetching summary
- ✅ Calling OpenAI
- ✅ Saving messages
- ✅ Updating summary
- ✅ Tracking tokens

---

## 📂 Files Modified

| File | Status | Changes |
|------|--------|---------|
| `ConversationHistory.cs` | ✏️ Modified | Added `TotalTokensUsed` property |
| `TokenUsage.cs` | ➕ Created | New model for token tracking |
| `IRedisService.cs` | ✏️ Modified | Added 4 new methods |
| `RedisService.cs` | ✏️ Modified | Implemented new methods |
| `OpenAiService.cs` | ✏️ Modified | Implemented full conversation flow |

**Files NOT Modified:**
- ❌ Controllers - No changes
- ❌ Other models - No changes
- ❌ Configuration - No changes

---

## ✅ Requirements Met

✅ **Get summary from Redis** - Automatic in GetResponseAsync  
✅ **Call OpenAI** - Single call, returns answer + summary  
✅ **Save user message** - AppendMessageAsync  
✅ **Save AI message** - AppendMessageAsync  
✅ **Update summary conditionally** - Only if summary != ""  
✅ **Track tokens** - Accumulates per conversation  
✅ **Future requests work** - Uses stored summary  
✅ **Do NOT call OpenAI twice** - Single API call  
✅ **Optimize token usage** - Smart summary updates  
✅ **Keep code clean** - Async throughout  
✅ **Use current models** - ConversationHistory, not old Conversation  
✅ **Modify only specified files** - OpenAiService, RedisService, models  

---

## 🚀 Ready to Use!

The conversation flow is fully implemented and production-ready. Every conversation:
- ✅ Maintains context via summaries
- ✅ Tracks token usage
- ✅ Preserves message history
- ✅ Optimizes API calls
- ✅ Works seamlessly with knowledge base

**Status**: 🟢 **PRODUCTION READY**  
**Version**: 2.0  
**Updated**: 2026-04-03
