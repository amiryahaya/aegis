export interface Session {
  id: string
  userId: string
  teamId?: string
  workspaceId?: string
  title: string
  description?: string
  status: SessionStatus
  type: SessionType
  turnCount: number
  tags: string[]
  createdAt: string
  lastActivityAt?: string
  endedAt?: string
  durationMinutes?: number
}

export enum SessionStatus {
  Active = 'Active',
  Ended = 'Ended',
  Archived = 'Archived'
}

export enum SessionType {
  QuickQuery = 'QuickQuery',
  Research = 'Research',
  Analysis = 'Analysis',
  Document = 'Document',
  Exploration = 'Exploration',
  Comparison = 'Comparison'
}

export interface SessionTurn {
  id: string
  sessionId: string
  turnNumber: number
  userQuery: string
  systemResponse?: string
  status: TurnStatus
  sources: SourceReference[]
  followUpQuestions: string[]
  metrics?: TurnMetrics
  feedback?: TurnFeedback
  createdAt: string
  completedAt?: string
  processingTimeMs?: number
}

export enum TurnStatus {
  Pending = 'Pending',
  Processing = 'Processing',
  Completed = 'Completed',
  Failed = 'Failed'
}

export interface SourceReference {
  documentId: string
  documentName: string
  excerpt?: string
  relevanceScore: number
  pageNumber?: number
}

export interface TurnMetrics {
  retrievalTimeMs: number
  generationTimeMs: number
  totalTimeMs: number
  sourcesRetrieved: number
  sourcesUsed: number
  tokensUsed: number
  cacheHit?: boolean
}

export interface TurnFeedback {
  rating: FeedbackRating
  comment?: string
  providedAt: string
}

export enum FeedbackRating {
  Positive = 'Positive',
  Negative = 'Negative',
  Neutral = 'Neutral'
}

export interface SessionStats {
  userId: string
  totalSessions: number
  activeSessions: number
  totalTurns: number
  totalQueriesThisWeek: number
  totalQueriesThisMonth: number
  averageSessionDuration: number
  averageTurnsPerSession: number
  sessionsByType: Record<string, number>
  feedbackByRating: Record<string, number>
  lastSessionAt?: string
  topTags: string[]
}

export interface CreateSessionRequest {
  userId: string
  teamId?: string
  workspaceId?: string
  title?: string
  description?: string
  type?: SessionType
  tags?: string[]
}

export interface AddTurnRequest {
  query: string
  context?: Record<string, unknown>
}

export interface SessionExport {
  sessionId: string
  format: ExportFormat
  content: string
  fileName: string
  contentType: string
  exportedAt: string
}

export enum ExportFormat {
  Json = 'Json',
  Markdown = 'Markdown',
  Html = 'Html',
  Text = 'Text'
}

export interface SessionFilter {
  userId?: string
  status?: SessionStatus
  type?: SessionType
  page?: number
  pageSize?: number
}
