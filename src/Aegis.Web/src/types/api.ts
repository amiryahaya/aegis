// Query types
export interface QueryRequest {
  query: string
}

export interface QueryResponse {
  query: string
  response: string
  sources: QuerySource[]
  queryAnalysis?: QueryAnalysis
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
  intent: string
  complexity: string
  keywords: string[]
  extractedEntities: string[]
}

// Streaming types
export interface StreamChunk {
  type: 'content' | 'metadata' | 'sources' | 'done' | 'error'
  content?: string
  sources?: QuerySource[]
  queryAnalysis?: QueryAnalysis
  error?: string
}

// Workspace types
export interface Workspace {
  id: string
  name: string
  description?: string
  customInstructions?: string
  teamId?: string
  createdBy: string
  status: WorkspaceStatus
  createdAt: string
  updatedAt?: string
  stats?: {
    documentCount: number
    queryCount: number
    totalTokensUsed: number
    averageResponseTime: number
    lastActivityAt?: string
  }
  settings?: {
    defaultSearchMode?: 'Semantic' | 'Keyword' | 'Hybrid'
    maxResults?: number
    enableCaching?: boolean
    enableFollowUps?: boolean
    llmModel?: string
    temperature?: number
    systemPrompt?: string
  }
}

export enum WorkspaceStatus {
  Active = 'Active',
  Archived = 'Archived',
  Deleted = 'Deleted'
}

// Conversation context types
export interface ConversationContext {
  sessionId: string
  messages: ContextMessage[]
  entities: TrackedEntity[]
  currentTopic?: Topic
  estimatedTokens: number
}

export interface ContextMessage {
  role: 'user' | 'assistant'
  content: string
  turnNumber: number
  timestamp: string
  estimatedTokens: number
}

export interface TrackedEntity {
  name: string
  type: string
  mentionCount: number
  salience: number
}

export interface Topic {
  name: string
  keywords: string[]
  confidence: number
}

export interface RewrittenQuery {
  originalQuery: string
  rewrittenText: string
  resolvedReferences: string[]
  expandedTerms: string[]
  confidenceScore: number
  explanation?: string
}

export interface ConversationSummary {
  sessionId: string
  summary: string
  keyPoints: string[]
  questionsAsked: string[]
  topicsDiscussed: string[]
  turnsCovered: number
}
