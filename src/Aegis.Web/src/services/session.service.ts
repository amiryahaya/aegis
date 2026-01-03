import api from './api'
import type { Session, SessionTurn, SessionStats } from '@/types'
import { FeedbackRating, SessionType as FrontendSessionType, SessionStatus as FrontendSessionStatus } from '@/types'

// =============================================================================
// Request/Response Types (matching backend DTOs)
// =============================================================================

export interface CreateSessionRequest {
  userId: string
  teamId?: string
  workspaceId?: string
  title?: string
  description?: string
  type?: FrontendSessionType
  tags?: string[]
  settings?: SessionSettings
  metadata?: Record<string, unknown>
}

export interface UpdateSessionRequest {
  title?: string
  description?: string
  tags?: string[]
  settings?: SessionSettings
  metadata?: Record<string, unknown>
}

export interface SessionSettings {
  maxTurns?: number
  contextWindowSize?: number
  temperature?: number
  systemPrompt?: string
}

// Re-export types from main types for convenience
export { SessionType, SessionStatus } from '@/types'
export type { SessionType as SessionTypeEnum, SessionStatus as SessionStatusEnum } from '@/types'

export interface SessionResponse {
  id: string
  userId: string
  teamId?: string
  workspaceId?: string
  title: string
  description?: string
  status: string
  type: string
  turnCount: number
  tags: string[]
  createdAt: string
  lastActivityAt?: string
  endedAt?: string
  durationMinutes?: number
}

export interface SessionPageResponse {
  items: SessionResponse[]
  totalCount: number
  pageNumber: number
  pageSize: number
  totalPages: number
  hasNextPage: boolean
  hasPreviousPage: boolean
}

export interface AddTurnRequest {
  query: string
  context?: Record<string, unknown>
}

export interface CompleteTurnRequest {
  response: string
  sources?: SourceReference[]
  followUps?: string[]
  metrics?: TurnMetrics
}

export interface SourceReference {
  documentId: string
  documentName: string
  excerpt?: string
  relevanceScore: number
  pageNumber?: number
}

export interface TurnMetrics {
  retrievalTime: number
  generationTime: number
  totalTime: number
  sourcesRetrieved: number
  sourcesUsed: number
  tokensUsed: number
  cacheHit?: boolean
}

export interface SessionTurnResponse {
  id: string
  sessionId: string
  turnNumber: number
  userQuery: string
  systemResponse?: string
  status: string
  sources: SourceReferenceResponse[]
  followUpQuestions: string[]
  metrics?: TurnMetricsResponse
  feedback?: TurnFeedbackResponse
  createdAt: string
  completedAt?: string
  processingTimeMs?: number
}

export interface SourceReferenceResponse {
  documentId: string
  documentName: string
  excerpt?: string
  relevanceScore: number
  pageNumber?: number
}

export interface TurnMetricsResponse {
  retrievalTimeMs: number
  generationTimeMs: number
  totalTimeMs: number
  sourcesRetrieved: number
  sourcesUsed: number
  tokensUsed: number
  cacheHit?: boolean
}

export interface TurnFeedbackResponse {
  rating: string
  comment?: string
  providedAt: string
}

export interface TurnFeedbackRequest {
  rating: FeedbackRating
  comment?: string
  issues?: string[]
}

export interface ShareSessionRequest {
  shareWithUserId?: string
  shareWithTeamId?: string
  isPublic?: boolean
  permission?: 'ReadOnly' | 'Comment' | 'Edit'
  expiresAt?: string
}

export interface SessionShareResponse {
  id: string
  sessionId: string
  sharedWithUserId?: string
  sharedWithTeamId?: string
  isPublic: boolean
  permission: string
  shareLink?: string
  createdAt: string
  expiresAt?: string
}

export interface SessionExportResponse {
  sessionId: string
  format: string
  content: string
  fileName?: string
  contentType?: string
  exportedAt: string
}

export interface SessionStatsResponse {
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

export interface SessionFilter {
  userId?: string
  status?: FrontendSessionStatus
  type?: FrontendSessionType
  page?: number
  pageSize?: number
}

export type ExportFormat = 'json' | 'markdown' | 'html' | 'text'

// =============================================================================
// Session Service
// =============================================================================

class SessionService {
  private readonly basePath = '/sessions'

  /**
   * Create a new session
   */
  async create(request: CreateSessionRequest): Promise<SessionResponse> {
    return api.post<SessionResponse>(this.basePath, request)
  }

  /**
   * Get session by ID
   */
  async getById(id: string): Promise<SessionResponse> {
    return api.get<SessionResponse>(`${this.basePath}/${id}`)
  }

  /**
   * Get sessions for a user
   */
  async getForUser(userId: string, filter?: SessionFilter): Promise<SessionPageResponse> {
    const params: Record<string, unknown> = { userId }
    if (filter?.status) params.status = filter.status
    if (filter?.type) params.type = filter.type
    if (filter?.page) params.page = filter.page
    if (filter?.pageSize) params.pageSize = filter.pageSize
    return api.get<SessionPageResponse>(this.basePath, params)
  }

  /**
   * Get active sessions for a user
   */
  async getActive(userId: string): Promise<SessionResponse[]> {
    return api.get<SessionResponse[]>(`${this.basePath}/active`, { userId })
  }

  /**
   * Update session
   */
  async update(id: string, request: UpdateSessionRequest): Promise<SessionResponse> {
    return api.put<SessionResponse>(`${this.basePath}/${id}`, request)
  }

  /**
   * Update session title
   */
  async updateTitle(id: string, title: string): Promise<void> {
    return api.put<void>(`${this.basePath}/${id}/title`, { title })
  }

  /**
   * Add a conversation turn
   */
  async addTurn(sessionId: string, request: AddTurnRequest): Promise<SessionTurnResponse> {
    return api.post<SessionTurnResponse>(`${this.basePath}/${sessionId}/turns`, request)
  }

  /**
   * Complete a turn with response
   */
  async completeTurn(
    sessionId: string,
    turnId: string,
    request: CompleteTurnRequest
  ): Promise<SessionTurnResponse> {
    return api.put<SessionTurnResponse>(
      `${this.basePath}/${sessionId}/turns/${turnId}/complete`,
      request
    )
  }

  /**
   * Get conversation history
   */
  async getConversation(sessionId: string, lastN?: number): Promise<SessionTurnResponse[]> {
    const params = lastN ? { lastN } : undefined
    return api.get<SessionTurnResponse[]>(`${this.basePath}/${sessionId}/conversation`, params)
  }

  /**
   * Add feedback to a turn
   */
  async addTurnFeedback(
    sessionId: string,
    turnId: string,
    feedback: TurnFeedbackRequest
  ): Promise<void> {
    return api.post<void>(`${this.basePath}/${sessionId}/turns/${turnId}/feedback`, feedback)
  }

  /**
   * End/close a session
   */
  async endSession(id: string): Promise<void> {
    return api.put<void>(`${this.basePath}/${id}/end`)
  }

  /**
   * Delete a session
   */
  async delete(id: string): Promise<void> {
    return api.delete<void>(`${this.basePath}/${id}`)
  }

  /**
   * Get session statistics
   */
  async getStats(userId: string): Promise<SessionStatsResponse> {
    return api.get<SessionStatsResponse>(`${this.basePath}/stats`, { userId })
  }

  /**
   * Share a session
   */
  async share(id: string, request: ShareSessionRequest): Promise<SessionShareResponse> {
    return api.post<SessionShareResponse>(`${this.basePath}/${id}/share`, request)
  }

  /**
   * Get sessions shared with user
   */
  async getShared(userId: string): Promise<SessionResponse[]> {
    return api.get<SessionResponse[]>(`${this.basePath}/shared`, { userId })
  }

  /**
   * Export session
   */
  async export(id: string, format: ExportFormat): Promise<SessionExportResponse> {
    return api.get<SessionExportResponse>(`${this.basePath}/${id}/export`, { format })
  }

  /**
   * Search sessions
   */
  async search(
    query: string,
    userId?: string,
    page?: number,
    pageSize?: number
  ): Promise<SessionPageResponse> {
    const params: Record<string, unknown> = { query }
    if (userId) params.userId = userId
    if (page) params.page = page
    if (pageSize) params.pageSize = pageSize
    return api.get<SessionPageResponse>(`${this.basePath}/search`, params)
  }

  /**
   * Get recent sessions (admin)
   */
  async getRecent(page?: number, pageSize?: number): Promise<SessionPageResponse> {
    const params: Record<string, unknown> = {}
    if (page) params.page = page
    if (pageSize) params.pageSize = pageSize
    return api.get<SessionPageResponse>(`${this.basePath}/recent`, params)
  }

  /**
   * Get session templates
   */
  async getTemplates(): Promise<SessionTemplateInfo[]> {
    return api.get<SessionTemplateInfo[]>(`${this.basePath}/templates`)
  }

  /**
   * Map backend response to frontend Session type
   */
  mapToSession(response: SessionResponse): Session {
    return {
      id: response.id,
      userId: response.userId,
      teamId: response.teamId,
      workspaceId: response.workspaceId,
      title: response.title,
      description: response.description,
      status: response.status as Session['status'],
      type: response.type as Session['type'],
      turnCount: response.turnCount,
      tags: response.tags,
      createdAt: response.createdAt,
      lastActivityAt: response.lastActivityAt,
      endedAt: response.endedAt,
      durationMinutes: response.durationMinutes
    }
  }

  /**
   * Map backend turn response to frontend SessionTurn type
   */
  mapToTurn(response: SessionTurnResponse): SessionTurn {
    return {
      id: response.id,
      sessionId: response.sessionId,
      turnNumber: response.turnNumber,
      userQuery: response.userQuery,
      systemResponse: response.systemResponse,
      status: response.status as SessionTurn['status'],
      sources: response.sources,
      followUpQuestions: response.followUpQuestions,
      metrics: response.metrics ? {
        retrievalTimeMs: response.metrics.retrievalTimeMs,
        generationTimeMs: response.metrics.generationTimeMs,
        totalTimeMs: response.metrics.totalTimeMs,
        sourcesRetrieved: response.metrics.sourcesRetrieved,
        sourcesUsed: response.metrics.sourcesUsed,
        tokensUsed: response.metrics.tokensUsed,
        cacheHit: response.metrics.cacheHit
      } : undefined,
      feedback: response.feedback ? {
        rating: response.feedback.rating as FeedbackRating,
        comment: response.feedback.comment,
        providedAt: response.feedback.providedAt
      } : undefined,
      createdAt: response.createdAt,
      completedAt: response.completedAt,
      processingTimeMs: response.processingTimeMs
    }
  }

  /**
   * Map backend stats to frontend SessionStats type
   */
  mapToStats(response: SessionStatsResponse): SessionStats {
    return {
      userId: response.userId,
      totalSessions: response.totalSessions,
      activeSessions: response.activeSessions,
      totalTurns: response.totalTurns,
      totalQueriesThisWeek: response.totalQueriesThisWeek,
      totalQueriesThisMonth: response.totalQueriesThisMonth,
      averageSessionDuration: response.averageSessionDuration,
      averageTurnsPerSession: response.averageTurnsPerSession,
      sessionsByType: response.sessionsByType,
      feedbackByRating: response.feedbackByRating,
      lastSessionAt: response.lastSessionAt,
      topTags: response.topTags
    }
  }
}

export interface SessionTemplateInfo {
  key: string
  name: string
  description: string
}

export const sessionService = new SessionService()
export default sessionService
