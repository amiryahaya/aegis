import api from './api'

// =============================================================================
// Request/Response Types (matching backend DTOs)
// =============================================================================

export interface QueryRequest {
  query: string
}

export interface QueryResponse {
  query: string
  response: string
  sources: QuerySource[]
  queryAnalysis: QueryAnalysis
  tokensUsed: number
  processingTime: number
  conversationId?: string
}

export interface QuerySource {
  documentId: string
  documentName: string
  content: string
  relevance: number
  chunkIndex: number
}

export interface QueryAnalysis {
  intent: QueryIntent
  complexity: QueryComplexity
  keywords: string[]
  extractedEntities: string[]
}

export type QueryIntent =
  | 'Factual'
  | 'Analytical'
  | 'Comparative'
  | 'Procedural'
  | 'Exploratory'
  | 'Clarification'

export type QueryComplexity =
  | 'Simple'
  | 'Moderate'
  | 'Complex'

// Streaming types
export interface StreamChunk {
  type: 'content' | 'metadata' | 'error'
  content?: string
  sources?: QuerySource[]
  queryAnalysis?: QueryAnalysis
  error?: string
}

export interface StreamingCallbacks {
  onToken: (token: string) => void
  onSources: (sources: QuerySource[]) => void
  onAnalysis: (analysis: QueryAnalysis) => void
  onComplete: () => void
  onError: (error: string) => void
}

// =============================================================================
// Query Service
// =============================================================================

class QueryService {
  /**
   * Execute a standard (non-streaming) RAG query
   */
  async query(
    workspaceId: string,
    query: string,
    conversationId?: string
  ): Promise<QueryResponse> {
    const params: Record<string, unknown> = {}
    if (conversationId) {
      params.conversationId = conversationId
    }

    return api.post<QueryResponse>(
      `/workspaces/${workspaceId}/query`,
      { query } as QueryRequest,
    )
  }

  /**
   * Execute a streaming RAG query using Server-Sent Events (SSE)
   * Returns an AbortController to allow cancellation
   */
  streamQuery(
    workspaceId: string,
    query: string,
    callbacks: StreamingCallbacks,
    conversationId?: string
  ): AbortController {
    const controller = new AbortController()

    this.executeStreamQuery(workspaceId, query, callbacks, controller.signal, conversationId)
      .catch((error) => {
        if (error.name !== 'AbortError') {
          callbacks.onError(error.message || 'Stream query failed')
        }
      })

    return controller
  }

  private async executeStreamQuery(
    workspaceId: string,
    query: string,
    callbacks: StreamingCallbacks,
    signal: AbortSignal,
    conversationId?: string
  ): Promise<void> {
    const baseUrl = import.meta.env.VITE_API_URL || '/api'
    const token = localStorage.getItem('aegis_token')

    let url = `${baseUrl}/workspaces/${workspaceId}/query/stream`
    if (conversationId) {
      url += `?conversationId=${conversationId}`
    }

    const response = await fetch(url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': token ? `Bearer ${token}` : ''
      },
      body: JSON.stringify({ query }),
      signal
    })

    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: 'Request failed' }))
      throw new Error(error.error || `HTTP ${response.status}`)
    }

    const reader = response.body?.getReader()
    if (!reader) {
      throw new Error('Response body is not readable')
    }

    const decoder = new TextDecoder()
    let buffer = ''

    try {
      while (true) {
        const { done, value } = await reader.read()

        if (done) {
          callbacks.onComplete()
          break
        }

        buffer += decoder.decode(value, { stream: true })

        // Process complete SSE messages
        const lines = buffer.split('\n')
        buffer = lines.pop() || '' // Keep incomplete line in buffer

        for (const line of lines) {
          if (line.startsWith('data: ')) {
            const data = line.slice(6).trim()

            if (data === '[DONE]') {
              callbacks.onComplete()
              return
            }

            try {
              const chunk = JSON.parse(data) as StreamChunk
              this.processChunk(chunk, callbacks)
            } catch {
              // Ignore JSON parse errors for partial data
            }
          }
        }
      }
    } finally {
      reader.releaseLock()
    }
  }

  private processChunk(chunk: StreamChunk, callbacks: StreamingCallbacks): void {
    switch (chunk.type) {
      case 'content':
        if (chunk.content) {
          callbacks.onToken(chunk.content)
        }
        break

      case 'metadata':
        if (chunk.sources) {
          callbacks.onSources(chunk.sources)
        }
        if (chunk.queryAnalysis) {
          callbacks.onAnalysis(chunk.queryAnalysis)
        }
        break

      case 'error':
        if (chunk.error) {
          callbacks.onError(chunk.error)
        }
        break
    }
  }

  /**
   * Build a query prompt with context from previous conversation
   */
  buildContextualQuery(query: string, previousTurns: Array<{ query: string; response: string }>): string {
    if (previousTurns.length === 0) {
      return query
    }

    // Include last 3 turns for context
    const recentTurns = previousTurns.slice(-3)
    const context = recentTurns
      .map(turn => `User: ${turn.query}\nAssistant: ${turn.response}`)
      .join('\n\n')

    return `Previous conversation:\n${context}\n\nCurrent question: ${query}`
  }

  /**
   * Extract follow-up questions from a response
   */
  extractFollowUpQuestions(response: string): string[] {
    // Simple extraction based on question patterns
    const questionPatterns = [
      /Would you like (?:me )?to ([^?]+\?)/gi,
      /Do you want (?:me )?to ([^?]+\?)/gi,
      /Should I ([^?]+\?)/gi,
      /Can I ([^?]+\?)/gi
    ]

    const followUps: string[] = []

    for (const pattern of questionPatterns) {
      const matches = response.matchAll(pattern)
      for (const match of matches) {
        if (match[0]) {
          followUps.push(match[0])
        }
      }
    }

    // Generate default follow-ups if none found
    if (followUps.length === 0) {
      return [
        'Can you provide more details?',
        'What are the key takeaways?',
        'Are there related topics I should explore?'
      ]
    }

    return followUps.slice(0, 3)
  }

  /**
   * Format sources for display
   */
  formatSources(sources: QuerySource[]): Array<{
    id: string
    name: string
    excerpt: string
    relevance: number
    page?: number
  }> {
    return sources.map(source => ({
      id: source.documentId,
      name: source.documentName,
      excerpt: source.content.slice(0, 200) + (source.content.length > 200 ? '...' : ''),
      relevance: Math.round(source.relevance * 100),
      page: source.chunkIndex + 1 // Convert 0-indexed chunk to 1-indexed page
    }))
  }
}

export const queryService = new QueryService()
export default queryService
