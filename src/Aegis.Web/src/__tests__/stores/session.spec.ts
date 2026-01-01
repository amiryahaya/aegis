import { describe, it, expect, vi, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useSessionStore } from '@/stores/session'
import type { Session, SessionTurn } from '@/types'
import api from '@/services/api'

// Mock the API
vi.mock('@/services/api', () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}))

// Create a working localStorage mock for these tests
const localStorageData: Record<string, string> = {}
const localStorageMock = {
  getItem: vi.fn((key: string) => localStorageData[key] || null),
  setItem: vi.fn((key: string, value: string) => { localStorageData[key] = value }),
  removeItem: vi.fn((key: string) => { delete localStorageData[key] }),
  clear: vi.fn(() => { Object.keys(localStorageData).forEach(key => delete localStorageData[key]) }),
}

// Set up localStorage mock before tests
Object.defineProperty(window, 'localStorage', { value: localStorageMock, writable: true })

describe('Session Store', () => {
  const mockSession: Session = {
    id: 'session-1',
    userId: 'user-1',
    title: 'Test Session',
    type: 'QuickQuery',
    status: 'Active',
    turnCount: 0,
    createdAt: new Date().toISOString(),
    lastActivityAt: new Date().toISOString(),
  }

  const mockTurn: SessionTurn = {
    id: 'turn-1',
    sessionId: 'session-1',
    userQuery: 'What is machine learning?',
    systemResponse: 'Machine learning is...',
    sources: [],
    createdAt: new Date().toISOString(),
  }

  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  describe('initial state', () => {
    it('has empty sessions array', () => {
      const store = useSessionStore()
      expect(store.sessions).toEqual([])
    })

    it('has null currentSession', () => {
      const store = useSessionStore()
      expect(store.currentSession).toBeNull()
    })

    it('has empty currentTurns array', () => {
      const store = useSessionStore()
      expect(store.currentTurns).toEqual([])
    })

    it('is not loading initially', () => {
      const store = useSessionStore()
      expect(store.loading).toBe(false)
    })
  })

  describe('fetchSessions', () => {
    it('fetches sessions from API', async () => {
      const mockResponse = {
        items: [mockSession],
        totalCount: 1,
        pageNumber: 1,
        pageSize: 20,
      }

      vi.mocked(api.get).mockResolvedValueOnce(mockResponse)

      const store = useSessionStore()
      await store.fetchSessions()

      expect(api.get).toHaveBeenCalledWith('/sessions', expect.any(Object))
      expect(store.sessions).toEqual([mockSession])
      expect(store.totalCount).toBe(1)
    })

    it('sets loading state during fetch', async () => {
      vi.mocked(api.get).mockImplementationOnce(() =>
        new Promise(resolve =>
          setTimeout(() => resolve({ items: [], totalCount: 0, pageNumber: 1 }), 100)
        )
      )

      const store = useSessionStore()
      const fetchPromise = store.fetchSessions()

      expect(store.loading).toBe(true)
      await fetchPromise
      expect(store.loading).toBe(false)
    })

    it('handles fetch error', async () => {
      vi.mocked(api.get).mockRejectedValueOnce({
        detail: 'Failed to fetch sessions',
      })

      const store = useSessionStore()
      await store.fetchSessions()

      expect(store.error).toBe('Failed to fetch sessions')
    })

    it('applies filters', async () => {
      vi.mocked(api.get).mockResolvedValueOnce({
        items: [],
        totalCount: 0,
        pageNumber: 1,
      })

      const store = useSessionStore()
      await store.fetchSessions({ type: 'Research', status: 'Active' })

      expect(api.get).toHaveBeenCalledWith('/sessions', expect.objectContaining({
        type: 'Research',
        status: 'Active',
      }))
    })
  })

  describe('fetchSession', () => {
    it('fetches single session by ID', async () => {
      vi.mocked(api.get).mockResolvedValueOnce(mockSession)

      const store = useSessionStore()
      const result = await store.fetchSession('session-1')

      expect(api.get).toHaveBeenCalledWith('/sessions/session-1')
      expect(result).toEqual(mockSession)
      expect(store.currentSession).toEqual(mockSession)
    })

    it('returns null on error', async () => {
      vi.mocked(api.get).mockRejectedValueOnce({
        detail: 'Session not found',
      })

      const store = useSessionStore()
      const result = await store.fetchSession('invalid-id')

      expect(result).toBeNull()
      expect(store.error).toBe('Session not found')
    })
  })

  describe('fetchTurns', () => {
    it('fetches conversation turns', async () => {
      vi.mocked(api.get).mockResolvedValueOnce([mockTurn])

      const store = useSessionStore()
      const result = await store.fetchTurns('session-1')

      expect(api.get).toHaveBeenCalledWith('/sessions/session-1/conversation', {})
      expect(result).toEqual([mockTurn])
      expect(store.currentTurns).toEqual([mockTurn])
    })

    it('applies lastN parameter', async () => {
      vi.mocked(api.get).mockResolvedValueOnce([mockTurn])

      const store = useSessionStore()
      await store.fetchTurns('session-1', 5)

      expect(api.get).toHaveBeenCalledWith('/sessions/session-1/conversation', { lastN: 5 })
    })
  })

  describe('createSession', () => {
    it('creates new session', async () => {
      vi.mocked(api.post).mockResolvedValueOnce(mockSession)

      const store = useSessionStore()
      const result = await store.createSession({
        title: 'New Session',
        type: 'QuickQuery',
        workspaceId: 'workspace-1',
      })

      expect(api.post).toHaveBeenCalledWith('/sessions', expect.any(Object))
      expect(result).toEqual(mockSession)
      expect(store.currentSession).toEqual(mockSession)
      expect(store.sessions).toHaveLength(1)
      expect(store.sessions[0]).toEqual(mockSession)
    })

    it('adds new session to beginning of list', async () => {
      const existingSession = { ...mockSession, id: 'existing-1' }
      const newSession = { ...mockSession, id: 'new-1' }

      vi.mocked(api.get).mockResolvedValueOnce({
        items: [existingSession],
        totalCount: 1,
        pageNumber: 1,
      })
      vi.mocked(api.post).mockResolvedValueOnce(newSession)

      const store = useSessionStore()
      await store.fetchSessions()
      await store.createSession({ title: 'New', type: 'QuickQuery' })

      expect(store.sessions[0]).toEqual(newSession)
    })

    it('handles creation error', async () => {
      vi.mocked(api.post).mockRejectedValueOnce({
        detail: 'Failed to create session',
      })

      const store = useSessionStore()
      const result = await store.createSession({
        title: 'New Session',
        type: 'QuickQuery',
      })

      expect(result).toBeNull()
      expect(store.error).toBe('Failed to create session')
    })
  })

  describe('addTurn', () => {
    it('adds turn to current session', async () => {
      vi.mocked(api.post).mockResolvedValueOnce(mockTurn)

      const store = useSessionStore()
      store.currentSession = mockSession
      store.currentTurns = []

      const result = await store.addTurn('session-1', {
        userQuery: 'What is ML?',
        workspaceId: 'workspace-1',
      })

      expect(result).toEqual(mockTurn)
      expect(store.currentTurns).toHaveLength(1)
      expect(store.currentTurns[0]).toEqual(mockTurn)
    })

    it('updates session turn count', async () => {
      vi.mocked(api.post).mockResolvedValueOnce(mockTurn)

      const store = useSessionStore()
      store.currentSession = { ...mockSession, turnCount: 0 }

      await store.addTurn('session-1', {
        userQuery: 'Test',
        workspaceId: 'workspace-1',
      })

      expect(store.currentSession?.turnCount).toBe(1)
    })
  })

  describe('updateTitle', () => {
    it('updates session title', async () => {
      vi.mocked(api.put).mockResolvedValueOnce({})

      const store = useSessionStore()
      store.currentSession = { ...mockSession }
      store.sessions = [{ ...mockSession }]

      const result = await store.updateTitle('session-1', 'New Title')

      expect(result).toBe(true)
      expect(store.currentSession?.title).toBe('New Title')
      expect(store.sessions[0].title).toBe('New Title')
    })
  })

  describe('endSession', () => {
    it('ends the session', async () => {
      vi.mocked(api.put).mockResolvedValueOnce({})

      const store = useSessionStore()
      store.currentSession = { ...mockSession }

      const result = await store.endSession('session-1')

      expect(result).toBe(true)
      expect(store.currentSession?.status).toBe('Ended')
      expect(store.currentSession?.endedAt).toBeDefined()
    })
  })

  describe('deleteSession', () => {
    it('deletes session from list', async () => {
      vi.mocked(api.delete).mockResolvedValueOnce({})

      const store = useSessionStore()
      store.sessions = [mockSession]
      store.currentSession = mockSession
      store.currentTurns = [mockTurn]

      const result = await store.deleteSession('session-1')

      expect(result).toBe(true)
      expect(store.sessions).not.toContain(mockSession)
      expect(store.currentSession).toBeNull()
      expect(store.currentTurns).toEqual([])
    })

    it('handles delete error', async () => {
      vi.mocked(api.delete).mockRejectedValueOnce({
        detail: 'Cannot delete session',
      })

      const store = useSessionStore()
      store.sessions = [mockSession]

      const result = await store.deleteSession('session-1')

      expect(result).toBe(false)
      expect(store.error).toBe('Cannot delete session')
    })
  })

  describe('exportSession', () => {
    it('exports session in specified format', async () => {
      const mockExport = {
        sessionId: 'session-1',
        format: 'Markdown' as const,
        content: '# Session Export\n...',
        exportedAt: new Date().toISOString(),
      }

      vi.mocked(api.get).mockResolvedValueOnce(mockExport)

      const store = useSessionStore()
      const result = await store.exportSession('session-1', 'Markdown')

      expect(api.get).toHaveBeenCalledWith('/sessions/session-1/export', { format: 'Markdown' })
      expect(result).toEqual(mockExport)
    })
  })

  describe('computed properties', () => {
    it('activeSessions filters only Active sessions', async () => {
      const activeSessions = [
        { ...mockSession, id: 's1', status: 'Active' as const },
        { ...mockSession, id: 's2', status: 'Active' as const },
      ]
      const endedSession = { ...mockSession, id: 's3', status: 'Ended' as const }

      vi.mocked(api.get).mockResolvedValueOnce({
        items: [...activeSessions, endedSession],
        totalCount: 3,
        pageNumber: 1,
      })

      const store = useSessionStore()
      await store.fetchSessions()

      expect(store.activeSessions).toHaveLength(2)
      expect(store.activeSessions.every(s => s.status === 'Active')).toBe(true)
    })

    it('hasMore correctly indicates pagination', () => {
      const store = useSessionStore()
      store.totalCount = 50
      store.currentPage = 1
      store.pageSize = 20

      expect(store.hasMore).toBe(true)

      store.currentPage = 3
      expect(store.hasMore).toBe(false)
    })
  })

  describe('clearCurrent', () => {
    it('clears current session and turns', () => {
      const store = useSessionStore()
      store.currentSession = mockSession
      store.currentTurns = [mockTurn]

      store.clearCurrent()

      expect(store.currentSession).toBeNull()
      expect(store.currentTurns).toEqual([])
    })
  })
})
