import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type {
  Session,
  SessionTurn,
  SessionStats,
  SessionFilter,
  CreateSessionRequest,
  AddTurnRequest,
  SessionExport,
  ExportFormat,
  PagedResponse
} from '@/types'
import api from '@/services/api'

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

  // Getters
  const activeSessions = computed(() =>
    sessions.value.filter(s => s.status === 'Active')
  )
  const hasMore = computed(() =>
    currentPage.value * pageSize.value < totalCount.value
  )

  // Actions
  async function fetchSessions(filter?: SessionFilter): Promise<void> {
    loading.value = true
    error.value = null

    try {
      const params = {
        page: filter?.page || currentPage.value,
        pageSize: filter?.pageSize || pageSize.value,
        ...filter
      }
      const response = await api.get<PagedResponse<Session>>('/sessions', params)

      sessions.value = response.items
      totalCount.value = response.totalCount
      currentPage.value = response.pageNumber
    } catch (err) {
      error.value = (err as { detail?: string }).detail || 'Failed to fetch sessions'
    } finally {
      loading.value = false
    }
  }

  async function fetchSession(sessionId: string): Promise<Session | null> {
    loading.value = true
    error.value = null

    try {
      const session = await api.get<Session>(`/sessions/${sessionId}`)
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
      const params = lastN ? { lastN } : {}
      const turns = await api.get<SessionTurn[]>(`/sessions/${sessionId}/conversation`, params)
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
      const session = await api.post<Session>('/sessions', request)
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

  async function addTurn(sessionId: string, request: AddTurnRequest): Promise<SessionTurn | null> {
    try {
      const turn = await api.post<SessionTurn>(`/sessions/${sessionId}/turns`, request)
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
    response: string,
    sources?: unknown[],
    followUps?: string[]
  ): Promise<SessionTurn | null> {
    try {
      const turn = await api.put<SessionTurn>(`/sessions/${sessionId}/turns/${turnId}/complete`, {
        response,
        sources,
        followUps
      })

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

  async function updateTitle(sessionId: string, title: string): Promise<boolean> {
    try {
      await api.put(`/sessions/${sessionId}/title`, { title })

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
      await api.put(`/sessions/${sessionId}/end`)

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
      await api.delete(`/sessions/${sessionId}`)

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

  async function exportSession(sessionId: string, format: ExportFormat): Promise<SessionExport | null> {
    try {
      const exportData = await api.get<SessionExport>(`/sessions/${sessionId}/export`, { format })
      return exportData
    } catch (err) {
      error.value = (err as { detail?: string }).detail || 'Failed to export session'
      return null
    }
  }

  async function fetchStats(userId?: string): Promise<void> {
    try {
      const params = userId ? { userId } : {}
      stats.value = await api.get<SessionStats>('/sessions/stats', params)
    } catch (err) {
      error.value = (err as { detail?: string }).detail || 'Failed to fetch stats'
    }
  }

  function clearCurrent() {
    currentSession.value = null
    currentTurns.value = []
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
    // Getters
    activeSessions,
    hasMore,
    // Actions
    fetchSessions,
    fetchSession,
    fetchTurns,
    createSession,
    addTurn,
    completeTurn,
    updateTitle,
    endSession,
    deleteSession,
    exportSession,
    fetchStats,
    clearCurrent
  }
})
