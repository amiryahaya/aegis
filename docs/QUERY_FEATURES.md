# Query Features Guide

## Overview

AEGIS provides advanced query capabilities including history tracking, user feedback collection, and cross-encoder reranking to continuously improve RAG response quality.

---

## Query History

Track all RAG queries with metadata, performance metrics, and full-text search capabilities.

### Features

- **Automatic Tracking**: All queries are logged automatically
- **Full-Text Search**: Search query and response content
- **Performance Metrics**: Token usage, response time, chunks retrieved
- **Conversation Grouping**: Link queries to conversations
- **Analytics**: Aggregate statistics by workspace or user

### API Endpoints

#### Get Workspace Query History

```bash
GET /api/query-history/workspace/{workspaceId}?limit=50

Response:
{
  "items": [
    {
      "id": "uuid",
      "workspaceId": "uuid",
      "userId": "uuid",
      "conversationId": "uuid",
      "query": "What are the key findings about entity X?",
      "responsePreview": "Based on the documents...",
      "tokensUsed": 1250,
      "responseTimeMs": 850,
      "chunksRetrieved": 8,
      "retrievalMethod": "reranked",
      "createdAt": "2025-01-15T10:30:00Z"
    }
  ]
}
```

#### Get User Query History

```bash
GET /api/query-history/user/{userId}?limit=50
```

#### Get Conversation History

```bash
GET /api/query-history/conversation/{conversationId}
```

Returns all queries in chronological order for a specific conversation.

#### Search Query History

```bash
GET /api/query-history/search?workspaceId={uuid}&searchTerm=entity&limit=50
```

Uses PostgreSQL full-text search on both query and response content.

#### Get Query Statistics

```bash
GET /api/query-history/stats/{workspaceId}?from=2025-01-01&to=2025-01-31

Response:
{
  "totalQueries": 1250,
  "totalTokensUsed": 1850000,
  "averageResponseTimeMs": 750,
  "averageChunksRetrieved": 7.5,
  "from": "2025-01-01T00:00:00Z",
  "to": "2025-01-31T23:59:59Z"
}
```

#### Get Single Query

```bash
GET /api/query-history/{id}

Response:
{
  "id": "uuid",
  "workspaceId": "uuid",
  "userId": "uuid",
  "conversationId": "uuid",
  "query": "Full query text",
  "response": "Full response text",
  "tokensUsed": 1250,
  "responseTimeMs": 850,
  "chunksRetrieved": 8,
  "retrievalMethod": "reranked",
  "metadata": {
    "model": "gpt-4o-mini",
    "intent": "information_seeking"
  },
  "createdAt": "2025-01-15T10:30:00Z"
}
```

---

## Feedback Collection

Collect user feedback on RAG responses to improve quality over time.

### Feedback Types

- **Positive (1)**: Response was helpful and accurate
- **Negative (2)**: Response was unhelpful or inaccurate
- **Neutral (3)**: Response was acceptable but could be better

### Features

- **One Feedback Per Query**: Users can submit/update feedback once per query
- **Optional Comments**: Detailed feedback with free-text comments
- **Statistics**: Track positive rate and feedback trends
- **Full-Text Search**: Search feedback comments

### API Endpoints

#### Submit Feedback

```bash
POST /api/feedback
{
  "queryHistoryId": "uuid",
  "userId": "uuid",
  "workspaceId": "uuid",
  "type": 1,  // 1=Positive, 2=Negative, 3=Neutral
  "comment": "Very helpful, answered my question completely"
}

Response:
{
  "id": "uuid",
  "queryHistoryId": "uuid",
  "userId": "uuid",
  "workspaceId": "uuid",
  "type": "Positive",
  "comment": "Very helpful, answered my question completely",
  "createdAt": "2025-01-15T10:35:00Z"
}
```

#### Update Feedback

```bash
PUT /api/feedback/{id}
{
  "type": 2,  // Changed to Negative
  "comment": "Actually, this missed important context"
}
```

#### Get Workspace Feedback

```bash
GET /api/feedback/workspace/{workspaceId}?limit=100

Response:
{
  "items": [
    {
      "id": "uuid",
      "queryHistoryId": "uuid",
      "userId": "uuid",
      "workspaceId": "uuid",
      "type": "Positive",
      "comment": "Great response!",
      "createdAt": "2025-01-15T10:35:00Z",
      "updatedAt": null
    }
  ]
}
```

#### Get Feedback Statistics

```bash
GET /api/feedback/stats/{workspaceId}?from=2025-01-01&to=2025-01-31

Response:
{
  "totalFeedback": 500,
  "positiveFeedback": 380,
  "negativeFeedback": 85,
  "neutralFeedback": 35,
  "positiveRate": 76.0,  // Percentage
  "totalWithComments": 250,
  "from": "2025-01-01T00:00:00Z",
  "to": "2025-01-31T23:59:59Z"
}
```

#### Delete Feedback

```bash
DELETE /api/feedback/{id}
```

---

## Cross-Encoder Reranking

Improve retrieval quality by reranking chunks using cross-encoder models.

### How It Works

1. **Hybrid Retrieval**: BM25 + Vector search returns top-N chunks
2. **Reranking**: Cross-encoder scores query-chunk relevance
3. **Selection**: Top-K most relevant chunks are used for generation

### Configuration

#### Cohere Reranker (Production)

Uses Cohere's rerank API for state-of-the-art relevance scoring.

**Setup:**

```bash
# Set environment variable
export COHERE_API_KEY=your_cohere_api_key

# Or in appsettings.json
{
  "Cohere": {
    "ApiKey": "your_cohere_api_key",
    "Model": "rerank-english-v3.0"  // Default
  }
}
```

**Models:**
- `rerank-english-v3.0` - Best for English (default)
- `rerank-multilingual-v3.0` - Supports 100+ languages
- `rerank-english-v2.0` - Legacy model

**Usage:**

Reranking is automatically enabled when Cohere API key is configured. The system will:
- Retrieve top 20 chunks from hybrid search
- Rerank using Cohere API
- Return top 10 most relevant chunks

#### Simple Reranker (Fallback)

When Cohere API key is not configured, AEGIS uses a simple term-overlap reranker.

**How It Works:**
- Calculates Jaccard similarity between query and chunk terms
- Fast but less accurate than cross-encoder models
- No external dependencies or API calls

### Monitoring Reranking

Query history tracks the retrieval method:
- `hybrid` - Standard BM25 + vector search
- `reranked` - Chunks were reranked

```bash
GET /api/query-history/stats/{workspaceId}

# Check retrievalMethod distribution
```

### Performance Impact

**Latency:**
- Cohere Reranker: +200-500ms per query
- Simple Reranker: +10-50ms per query

**Accuracy:**
- Cohere typically improves relevance by 15-25%
- Simple reranker provides 5-10% improvement

**Cost:**
- Cohere: ~$2 per 1,000 rerank requests (check current pricing)
- Simple: Free (compute only)

---

## Integration Examples

### Complete Query Flow with Feedback

```typescript
// 1. Execute query
const queryResponse = await fetch('/api/workspaces/{workspaceId}/query', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    query: 'What are the sanctions against entity X?'
  })
});

const result = await queryResponse.json();
// {
//   query: "...",
//   response: "...",
//   sources: [...],
//   queryAnalysis: {...},
//   conversationId: "uuid"
// }

// 2. Display response to user
displayResponse(result.response, result.sources);

// 3. Collect feedback
const feedbackResponse = await fetch('/api/feedback', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    queryHistoryId: result.conversationId,  // Links to query
    userId: currentUserId,
    workspaceId: workspaceId,
    type: userRating,  // 1, 2, or 3
    comment: userComment || null
  })
});
```

### Streaming Query with History

```typescript
const eventSource = new EventSource(
  `/api/workspaces/{workspaceId}/query/stream?query=${encodeURIComponent(query)}`
);

let fullResponse = '';

eventSource.addEventListener('message', (event) => {
  const data = JSON.parse(event.data);

  if (data.type === 'content') {
    fullResponse += data.content;
    displayChunk(data.content);
  } else if (data.type === 'metadata') {
    displaySources(data.sources);
    displayAnalysis(data.queryAnalysis);
  } else if (event.data === '[DONE]') {
    eventSource.close();
  }
});

// Query is automatically saved to history
```

### Analytics Dashboard

```typescript
// Fetch workspace statistics
const stats = await fetch(`/api/query-history/stats/${workspaceId}?from=${startDate}&to=${endDate}`)
  .then(r => r.json());

const feedbackStats = await fetch(`/api/feedback/stats/${workspaceId}?from=${startDate}&to=${endDate}`)
  .then(r => r.json());

// Display metrics
displayMetric('Total Queries', stats.totalQueries);
displayMetric('Avg Response Time', `${stats.averageResponseTimeMs}ms`);
displayMetric('Positive Feedback Rate', `${feedbackStats.positiveRate}%`);
displayMetric('Avg Chunks Retrieved', stats.averageChunksRetrieved);
```

---

## Database Schema

### query_history Table

```sql
CREATE TABLE query_history (
    id UUID PRIMARY KEY,
    workspace_id UUID NOT NULL,
    user_id UUID NOT NULL,
    conversation_id UUID,
    query TEXT NOT NULL,
    response TEXT NOT NULL,
    tokens_used INTEGER,
    response_time_ms BIGINT,
    chunks_retrieved INTEGER,
    retrieval_method VARCHAR(50),
    metadata JSONB,
    created_at TIMESTAMPTZ,
    updated_at TIMESTAMPTZ
);

-- Full-text search index
CREATE INDEX idx_query_history_search
ON query_history USING gin(to_tsvector('english', query || ' ' || response));
```

### feedback Table

```sql
CREATE TABLE feedback (
    id UUID PRIMARY KEY,
    query_history_id UUID NOT NULL REFERENCES query_history(id),
    user_id UUID NOT NULL,
    workspace_id UUID NOT NULL,
    type INTEGER NOT NULL,  -- 1=Positive, 2=Negative, 3=Neutral
    comment TEXT,
    metadata JSONB,
    created_at TIMESTAMPTZ,
    updated_at TIMESTAMPTZ,
    UNIQUE(query_history_id, user_id)  -- One feedback per query per user
);

-- Full-text search on comments
CREATE INDEX idx_feedback_comment_search
ON feedback USING gin(to_tsvector('english', COALESCE(comment, '')));
```

---

## Best Practices

### Query History
- **Privacy**: Respect user data retention policies
- **Cleanup**: Archive or delete old queries (e.g., > 1 year)
- **Indexing**: Full-text search indexes can grow large, monitor size

### Feedback
- **Encourage Feedback**: Make it easy to submit (thumbs up/down UI)
- **Act on Feedback**: Review negative feedback regularly
- **Trends**: Track feedback trends over time to measure improvements

### Reranking
- **Cost vs Accuracy**: Use Cohere for production, Simple for development
- **Caching**: Consider caching rerank results for repeated queries
- **Monitoring**: Track latency impact on user experience

---

## Troubleshooting

### Query History Not Saving

Check that `IQueryHistoryRepository` is registered and `userId` is provided:

```csharp
// In QueryModule.cs
var result = await queryService.QueryAsync(
    request.Query,
    workspaceId,
    userId: GetCurrentUserId(),  // Must provide userId
    conversationId);
```

### Cohere Reranking Failing

Check API key configuration and error logs:

```bash
# Verify environment variable
echo $COHERE_API_KEY

# Check logs for API errors
docker logs aegis-api | grep -i cohere
```

Falls back to Simple Reranker automatically on failure.

### Feedback Statistics Not Updating

Ensure feedback type is 1, 2, or 3:

```typescript
// Correct
type: 1  // Positive

// Incorrect
type: "positive"  // Will fail validation
```

---

## Next Steps

- [Data Connectors Guide](./DATA_CONNECTORS.md)
- [API Reference](./API_REFERENCE.md)
- [Monitoring and Observability](./MONITORING.md)
