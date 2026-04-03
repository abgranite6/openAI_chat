# Knowledge Base Feature - Quick Reference

## Overview
The OpenAI service now supports passing a knowledge base (context/reference material) for RAG (Retrieval-Augmented Generation) scenarios.

---

## 🎯 Interface Signature

```csharp
Task<OpenAiResponse> GetResponseAsync(
    string conversationId, 
    string userInput, 
    string? summary, 
    string? knowledgeBase = null  // Optional: Reference material
);
```

---

## 📝 Usage Examples

### Example 1: Without Knowledge Base (Regular Chat)
```csharp
var response = await _openAiService.GetResponseAsync(
    conversationId: "abc123",
    userInput: "What is 2+2?",
    summary: null
);
```

### Example 2: With Knowledge Base (RAG Pattern)
```csharp
var knowledgeBase = @"
Product: SuperWidget Pro
Price: $299
Features:
- Advanced AI processing
- 24/7 support
- Cloud integration
Warranty: 2 years
";

var response = await _openAiService.GetResponseAsync(
    conversationId: "abc123",
    userInput: "What is the price and warranty?",
    summary: null,
    knowledgeBase: knowledgeBase
);
```

### Example 3: With Both Summary and Knowledge Base
```csharp
var response = await _openAiService.GetResponseAsync(
    conversationId: "abc123",
    userInput: "What about the warranty?",
    summary: "User asked about SuperWidget Pro pricing.",
    knowledgeBase: productDocumentation
);
```

---

## 🔄 Message Flow (Updated)

```
[System Prompt]
    ↓
[Knowledge Base] (if provided) ← NEW
    ↓
[Conversation Summary] (if provided)
    ↓
[User Input]
```

---

## 💪 Knowledge Base Prompt (Strong & Precise)

When knowledge base is provided, the AI receives:

```
KNOWLEDGE BASE - You MUST use the following information as the 
authoritative source when answering the user's question. Do NOT 
use external knowledge if the answer can be found here. If the 
user's question is related to this content, answer ONLY using 
information from this knowledge base. If the information is not 
in the knowledge base, clearly state that.

[Your knowledge base content here]
```

**Key Instructions:**
- ✅ MUST use as authoritative source
- ✅ Do NOT use external knowledge if answer exists
- ✅ Answer ONLY from knowledge base for related questions
- ✅ Clearly state if information not found

---

## 🎯 Use Cases

### 1. Document Q&A
```csharp
// Vector database returns relevant chunks
var chunks = await vectorDb.SearchAsync(userQuery);
var knowledgeBase = string.Join("\n\n", chunks);

var response = await _openAiService.GetResponseAsync(
    conversationId, 
    userQuery, 
    summary, 
    knowledgeBase
);
```

### 2. Customer Support (FAQ)
```csharp
// Load relevant FAQ sections
var faqContent = await faqService.GetRelevantSectionsAsync(userQuery);

var response = await _openAiService.GetResponseAsync(
    conversationId, 
    userQuery, 
    summary, 
    faqContent
);
```

### 3. Product Recommendations
```csharp
// Product catalog data
var productInfo = await catalogService.GetProductDetailsAsync(productId);

var response = await _openAiService.GetResponseAsync(
    conversationId, 
    "Tell me about this product", 
    summary, 
    productInfo
);
```

### 4. Code Documentation
```csharp
// API reference
var apiDocs = await docService.GetApiDocsAsync(apiName);

var response = await _openAiService.GetResponseAsync(
    conversationId, 
    "How do I use this API?", 
    summary, 
    apiDocs
);
```

---

## 📊 Token Usage Impact

Knowledge base content is included in **Input Tokens**:

**Without Knowledge Base:**
```
[Tokens - Input: 250, Output: 120, Total: 370]
```

**With Knowledge Base (1000 chars):**
```
[Tokens - Input: 850, Output: 120, Total: 970]
                 ↑ includes knowledge base
```

**Formula:** Input = System Prompt + Knowledge Base + Summary + User Message

---

## ⚙️ Integration Patterns

### Pattern 1: Direct String
```csharp
var kb = "Direct content here...";
var response = await service.GetResponseAsync(id, input, summary, kb);
```

### Pattern 2: From File
```csharp
var kb = await File.ReadAllTextAsync("knowledge.txt");
var response = await service.GetResponseAsync(id, input, summary, kb);
```

### Pattern 3: From Database
```csharp
var kb = await dbContext.KnowledgeBases
    .Where(x => x.Topic == topic)
    .Select(x => x.Content)
    .FirstOrDefaultAsync();
var response = await service.GetResponseAsync(id, input, summary, kb);
```

### Pattern 4: From Vector Search
```csharp
var embeddings = await embeddingService.GetEmbeddingsAsync(userInput);
var chunks = await vectorDb.SearchSimilarAsync(embeddings, topK: 5);
var kb = string.Join("\n\n---\n\n", chunks.Select(c => c.Text));
var response = await service.GetResponseAsync(id, input, summary, kb);
```

---

## ✅ Advantages

✅ **Optional** - Backward compatible, works with or without  
✅ **Flexible** - Any string content (docs, FAQs, data, code)  
✅ **Clear Separation** - Knowledge base ≠ conversation history  
✅ **Strong Instructions** - AI prioritizes knowledge base over training data  
✅ **RAG Ready** - Perfect for vector database integrations  
✅ **No Validation** - Accepts any content without size/format checks  

---

## 🚫 What's NOT Changed

❌ Controller - Not modified (implement later)  
❌ ChatRequest model - Not modified  
❌ Validation - No size/format validation added  
❌ Logging - No knowledge base logging added  
❌ Token warnings - No limit warnings added  

---

## 📂 Files Modified

| File | Change |
|------|--------|
| `IOpenAiService.cs` | Added `knowledgeBase` parameter |
| `OpenAiService.cs` | Implemented knowledge base handling |

---

**Status**: ✅ **Ready to Use**  
**Version**: 1.1  
**Updated**: 2026-04-03
