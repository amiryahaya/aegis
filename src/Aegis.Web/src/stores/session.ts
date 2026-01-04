import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { Session, SessionTurn, SessionStats } from '@/types'
import { FeedbackRating } from '@/types'
import sessionService from '@/services/session.service'
import type {
  CreateSessionRequest,
  UpdateSessionRequest,
  AddTurnRequest,
  CompleteTurnRequest,
  TurnFeedbackRequest,
  ShareSessionRequest,
  SessionFilter,
  ExportFormat
} from '@/services/session.service'

// Re-export for convenience
export type {
  CreateSessionRequest,
  UpdateSessionRequest,
  AddTurnRequest,
  CompleteTurnRequest,
  TurnFeedbackRequest,
  ShareSessionRequest,
  SessionFilter,
  ExportFormat
}

export const useSessionStore = defineStore('session', () => {
  // State
  const sessions = ref<Session[]>([])
  const currentSession = ref<Session | null>(null)
  const currentTurns = ref<SessionTurn[]>([])
  const stats = ref<SessionStats | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const totalCount = ref(0)
  const currentPage = ref(1)
  const pageSize = ref(20)
  const totalPages = ref(0)

  // Getters
  const activeSessions = computed(() =>
    sessions.value.filter(s => s.status === 'Active')
  )
  const hasMore = computed(() => currentPage.value < totalPages.value)
  const hasPrevious = computed(() => currentPage.value > 1)

  // Actions
  async function fetchSessions(filter?: SessionFilter): Promise<void> {
    loading.value = true
    error.value = null

    try {
      const userId = filter?.userId || ''
      const response = await sessionService.getForUser(userId, {
        ...filter,
        page: filter?.page || currentPage.value,
        pageSize: filter?.pageSize || pageSize.value
      })

      sessions.value = response.items.map(sessionService.mapToSession)
      totalCount.value = response.totalCount
      currentPage.value = response.pageNumber
      totalPages.value = response.totalPages
    } catch (err) {
      error.value = (err as { detail?: string }).detail || 'Failed to fetch sessions'
    } finally {
      loading.value = false
    }
  }

  async function fetchActiveSessions(userId: string): Promise<Session[]> {
    try {
      const response = await sessionService.getActive(userId)
      return response.map(sessionService.mapToSession)
    } catch (err) {
      error.value = (err as { detail?: string }).detail || 'Failed to fetch active sessions'
      return []
    }
  }

  async function fetchSession(sessionId: string): Promise<Session | null> {
    loading.value = true
    error.value = null

    try {
      const response = await sessionService.getById(sessionId)
      const session = sessionService.mapToSession(response)
      currentSession.value = session
      return session
    } catch (err) {
      error.value = (err as { detail?: string }).detail || 'Failed to fetch session'
      return null
    } finally {
      loading.value = false
    }
  }

  async function fetchTurns(sessionId: string, lastN?: number): Promise<SessionTurn[]> {
    try {
      const response = await sessionService.getConversation(sessionId, lastN)
      const turns = response.map(sessionService.mapToTurn)
      currentTurns.value = turns
      return turns
    } catch (err) {
      error.value = (err as { detail?: string }).detail || 'Failed to fetch conversation'
      return []
    }
  }

  async function createSession(request: CreateSessionRequest): Promise<Session | null> {
    loading.value = true
    error.value = null

    try {
      const response = await sessionService.create(request)
      const session = sessionService.mapToSession(response)
      sessions.value.unshift(session)
      currentSession.value = session
      currentTurns.value = []
      return session
    } catch (err) {
      error.value = (err as { detail?: string }).detail || 'Failed to create session'
      return null
    } finally {
      loading.value = false
    }
  }

  async function updateSession(sessionId: string, request: UpdateSessionRequest): Promise<Session | null> {
    loading.value = true
    error.value = null

    try {
      const response = await sessionService.update(sessionId, request)
      const session = sessionService.mapToSession(response)

      // Update in sessions list
      const index = sessions.value.findIndex(s => s.id === sessionId)
      if (index !== -1) {
        sessions.value[index] = session
      }

      // Update current session if it's the same
      if (currentSession.value?.id === sessionId) {
        currentSession.value = session
      }

      return session
    } catch (err) {
      error.value = (err as { detail?: string }).detail || 'Failed to update session'
      return null
    } finally {
      loading.value = false
    }
  }

  async function addTurn(sessionId: string, request: AddTurnRequest): Promise<SessionTurn | null> {
    try {
      const response = await sessionService.addTurn(sessionId, request)
      const turn = sessionService.mapToTurn(response)
      currentTurns.value.push(turn)

      // Update session turn count
      if (currentSession.value?.id === sessionId) {
        currentSession.value.turnCount++
        currentSession.value.lastActivityAt = new Date().toISOString()
      }

      return turn
    } catch (err) {
      error.value = (err as { detail?: string }).detail || 'Failed to add turn'
      return null
    }
  }

  async function completeTurn(
    sessionId: string,
    turnId: string,
    responseOrRequest: string | CompleteTurnRequest,
    sources?: unknown[],
    followUps?: string[]
  ): Promise<SessionTurn | null> {
    try {
      // Handle both old signature (response, sources, followUps) and new signature (CompleteTurnRequest)
      const request: CompleteTurnRequest = typeof responseOrRequest === 'string'
        ? {
            response: responseOrRequest,
            sources: sources as CompleteTurnRequest['sources'],
            followUps
          }
        : responseOrRequest

      const response = await sessionService.completeTurn(sessionId, turnId, request)
      const turn = sessionService.mapToTurn(response)

      // Update the turn in the list
      const index = currentTurns.value.findIndex(t => t.id === turnId)
      if (index !== -1) {
        currentTurns.value[index] = turn
      }

      return turn
    } catch (err) {
      error.value = (err as { detail?: string }).detail || 'Failed to complete turn'
      return null
    }
  }

  async function addTurnFeedback(
    sessionId: string,
    turnId: string,
    feedback: TurnFeedbackRequest
  ): Promise<boolean> {
    try {
      await sessionService.addTurnFeedback(sessionId, turnId, feedback)

      // Update the turn in the list
      const turn = currentTurns.value.find(t => t.id === turnId)
      if (turn) {
        turn.feedback = {
          rating: feedback.rating as FeedbackRating,
          comment: feedback.comment,
          providedAt: new Date().toISOString()
        }
      }

      return true
    } catch (err) {
      error.value = (err as { detail?: string }).detail || 'Failed to add feedback'
      return false
    }
  }

  async function updateTitle(sessionId: string, title: string): Promise<boolean> {
    try {
      await sessionService.updateTitle(sessionId, title)

      if (currentSession.value?.id === sessionId) {
        currentSession.value.title = title
      }

      const session = sessions.value.find(s => s.id === sessionId)
      if (session) {
        session.title = title
      }

      return true
    } catch (err) {
      error.value = (err as { detail?: string }).detail || 'Failed to update title'
      return false
    }
  }

  async function endSession(sessionId: string): Promise<boolean> {
    try {
      await sessionService.endSession(sessionId)

      if (currentSession.value?.id === sessionId) {
        currentSession.value.status = 'Ended' as Session['status']
        currentSession.value.endedAt = new Date().toISOString()
      }

      const session = sessions.value.find(s => s.id === sessionId)
      if (session) {
        session.status = 'Ended' as Session['status']
        session.endedAt = new Date().toISOString()
      }

      return true
    } catch (err) {
      error.value = (err as { detail?: string }).detail || 'Failed to end session'
      return false
    }
  }

  async function deleteSession(sessionId: string): Promise<boolean> {
    try {
      await sessionService.delete(sessionId)

      sessions.value = sessions.value.filter(s => s.id !== sessionId)

      if (currentSession.value?.id === sessionId) {
        currentSession.value = null
        currentTurns.value = []
      }

      return true
    } catch (err) {
      error.value = (err as { detail?: string }).detail || 'Failed to delete session'
      return false
    }
  }

  async function archiveSession(sessionId: string): Promise<boolean> {
    try {
      const response = await sessionService.archive(sessionId)
      const session = sessionService.mapToSession(response)

      // Update in sessions list
      const index = sessions.value.findIndex(s => s.id === sessionId)
      if (index !== -1) {
        sessions.value[index] = session
      }

      // Update current session if it's the same
      if (currentSession.value?.id === sessionId) {
        currentSession.value = session
      }

      return true
    } catch (err) {
      error.value = (err as { detail?: string }).detail || 'Failed to archive session'
      return false
    }
  }

  async function unarchiveSession(sessionId: string): Promise<boolean> {
    try {
      const response = await sessionService.unarchive(sessionId)
      const session = sessionService.mapToSession(response)

      // Update in sessions list
      const index = sessions.value.findIndex(s => s.id === sessionId)
      if (index !== -1) {
        sessions.value[index] = session
      }

      // Update current session if it's the same
      if (currentSession.value?.id === sessionId) {
        currentSession.value = session
      }

      return true
    } catch (err) {
      error.value = (err as { detail?: string }).detail || 'Failed to unarchive session'
      return false
    }
  }

  async function exportSession(sessionId: string, format: ExportFormat | string): Promise<{ content: string; fileName: string; contentType: string } | null> {
    try {
      // Convert string to lowercase for API compatibility
      const apiFormat = (typeof format === 'string' ? format.toLowerCase() : format) as ExportFormat
      const response = await sessionService.export(sessionId, apiFormat)

      // Determine content type based on format
      const contentTypeMap: Record<string, string> = {
        json: 'application/json',
        markdown: 'text/markdown',
        html: 'text/html',
        text: 'text/plain'
      }
      const formatKey = apiFormat.toLowerCase()
      const contentType = contentTypeMap[formatKey] || 'text/plain'

      return {
        content: response.content,
        fileName: response.fileName || `session-${sessionId}.${formatKey === 'markdown' ? 'md' : formatKey}`,
        contentType
      }
    } catch (err) {
      error.value = (err as { detail?: string }).detail || 'Failed to export session'
      return null
    }
  }

  async function shareSession(sessionId: string, request: ShareSessionRequest): Promise<boolean> {
    try {
      await sessionService.share(sessionId, request)
      return true
    } catch (err) {
      error.value = (err as { detail?: string }).detail || 'Failed to share session'
      return false
    }
  }

  async function fetchSharedSessions(userId: string): Promise<Session[]> {
    try {
      const response = await sessionService.getShared(userId)
      return response.map(sessionService.mapToSession)
    } catch (err) {
      error.value = (err as { detail?: string }).detail || 'Failed to fetch shared sessions'
      return []
    }
  }

  async function searchSessions(query: string, userId?: string): Promise<Session[]> {
    loading.value = true
    error.value = null

    try {
      const response = await sessionService.search(query, userId)
      return response.items.map(sessionService.mapToSession)
    } catch (err) {
      error.value = (err as { detail?: string }).detail || 'Failed to search sessions'
      return []
    } finally {
      loading.value = false
    }
  }

  async function fetchStats(userId?: string): Promise<void> {
    try {
      const response = await sessionService.getStats(userId || '')
      stats.value = sessionService.mapToStats(response)
    } catch (err) {
      error.value = (err as { detail?: string }).detail || 'Failed to fetch stats'
    }
  }

  function setPage(page: number) {
    currentPage.value = page
  }

  function setPageSize(size: number) {
    pageSize.value = size
    currentPage.value = 1 // Reset to first page when changing page size
  }

  function clearCurrent() {
    currentSession.value = null
    currentTurns.value = []
  }

  function clearError() {
    error.value = null
  }

  return {
    // State
    sessions,
    currentSession,
    currentTurns,
    stats,
    loading,
    error,
    totalCount,
    currentPage,
    pageSize,
    totalPages,

    // Getters
    activeSessions,
    hasMore,
    hasPrevious,

    // Actions
    fetchSessions,
    fetchActiveSessions,
    fetchSession,
    fetchTurns,
    createSession,
    updateSession,
    addTurn,
    completeTurn,
    addTurnFeedback,
    updateTitle,
    endSession,
    deleteSession,
    archiveSession,
    unarchiveSession,
    exportSession,
    shareSession,
    fetchSharedSessions,
    searchSessions,
    fetchStats,
    setPage,
    setPageSize,
    clearCurrent,
    clearError
  }
})
