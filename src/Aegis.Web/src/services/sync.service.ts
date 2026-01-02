/**
 * Sync Service for data reconciliation between local IndexedDB and remote API
 * Implements optimistic updates with conflict resolution
 */

import { indexedDBService, STORES, type PendingOperation } from './indexeddb.service'
import api from './api'
import type { Session } from '@/types'
import type { Workspace } from '@/types'

// Cache staleness thresholds (in milliseconds)
export const CACHE_MAX_AGE = {
  SESSIONS: 5 * 60 * 1000, // 5 minutes
  WORKSPACES: 10 * 60 * 1000, // 10 minutes
  DOCUMENTS: 15 * 60 * 1000 // 15 minutes
}

export interface SyncResult<T> {
  data: T[]
  fromCache: boolean
  stale: boolean
}

export interface ConflictResolution {
  strategy: 'local' | 'remote' | 'merge'
  resolver?: <T>(local: T, remote: T) => T
}

class SyncService {
  /**
   * Fetch sessions with offline-first strategy
   */
  async fetchSessions(
    userId: string,
    forceRefresh = false
  ): Promise<SyncResult<Session>> {
    const isOnline = navigator.onLine

    // Check cache first
    const cachedSessions = await indexedDBService.getByIndex<Session>(
      STORES.SESSIONS,
      'userId',
      userId
    )
    const isStale = await indexedDBService.isCacheStale(
      STORES.SESSIONS,
      CACHE_MAX_AGE.SESSIONS
    )

    // Return cache if offline or cache is fresh and not forcing refresh
    if (!isOnline || (!forceRefresh && !isStale && cachedSessions.length > 0)) {
      return {
        data: cachedSessions,
        fromCache: true,
        stale: isStale
      }
    }

    // Fetch from API
    try {
      const sessions = await api.get<Session[]>('/api/sessions', {
        userId
      })

      // Update cache
      await indexedDBService.putMany(STORES.SESSIONS, sessions)
      await indexedDBService.updateCacheMetadata(STORES.SESSIONS)

      return {
        data: sessions,
        fromCache: false,
        stale: false
      }
    } catch (error) {
      // Fall back to cache on error
      if (cachedSessions.length > 0) {
        console.warn('Failed to fetch sessions, using cache:', error)
        return {
          data: cachedSessions,
          fromCache: true,
          stale: true
        }
      }
      throw error
    }
  }

  /**
   * Fetch a single session with its turns
   */
  async fetchSession(sessionId: string, forceRefresh = false): Promise<Session | null> {
    const isOnline = navigator.onLine

    // Check cache first
    const cachedSession = await indexedDBService.get<Session>(STORES.SESSIONS, sessionId)

    if (!isOnline) {
      return cachedSession || null
    }

    if (!forceRefresh && cachedSession) {
      return cachedSession
    }

    // Fetch from API
    try {
      const session = await api.get<Session>(`/api/sessions/${sessionId}`)

      // Update cache
      await indexedDBService.put(STORES.SESSIONS, session)

      return session
    } catch (error) {
      if (cachedSession) {
        console.warn('Failed to fetch session, using cache:', error)
        return cachedSession
      }
      throw error
    }
  }

  /**
   * Create a session with optimistic update
   */
  async createSession(session: Partial<Session>): Promise<Session> {
    // Generate temporary ID for optimistic update
    const tempId = `temp_${crypto.randomUUID()}`
    const optimisticSession: Session = {
      id: tempId,
      title: session.title || 'New Session',
      userId: session.userId || '',
      type: session.type || 'QuickQuery',
      status: 'Active',
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      turnCount: 0,
      ...session
    } as Session

    // Save optimistically to local cache
    await indexedDBService.put(STORES.SESSIONS, optimisticSession)

    if (!navigator.onLine) {
      // Queue for later sync
      await indexedDBService.addPendingOperation({
        type: 'create',
        store: STORES.SESSIONS,
        data: { ...session, tempId }
      })
      return optimisticSession
    }

    // Try to sync immediately
    try {
      const createdSession = await api.post<Session>('/api/sessions', session)

      // Replace temp session with real one
      await indexedDBService.delete(STORES.SESSIONS, tempId)
      await indexedDBService.put(STORES.SESSIONS, createdSession)

      return createdSession
    } catch (error) {
      // Queue for later sync on error
      await indexedDBService.addPendingOperation({
        type: 'create',
        store: STORES.SESSIONS,
        data: { ...session, tempId }
      })
      return optimisticSession
    }
  }

  /**
   * Update a session with optimistic update
   */
  async updateSession(sessionId: string, updates: Partial<Session>): Promise<Session | null> {
    // Get current session
    const currentSession = await indexedDBService.get<Session>(STORES.SESSIONS, sessionId)
    if (!currentSession) return null

    // Apply optimistic update
    const updatedSession: Session = {
      ...currentSession,
      ...updates,
      lastActivityAt: new Date().toISOString()
    }
    await indexedDBService.put(STORES.SESSIONS, updatedSession)

    if (!navigator.onLine) {
      await indexedDBService.addPendingOperation({
        type: 'update',
        store: STORES.SESSIONS,
        data: { id: sessionId, ...updates }
      })
      return updatedSession
    }

    try {
      const updated = await api.patch<Session>(`/api/sessions/${sessionId}`, updates)
      await indexedDBService.put(STORES.SESSIONS, updated)
      return updated
    } catch (error) {
      await indexedDBService.addPendingOperation({
        type: 'update',
        store: STORES.SESSIONS,
        data: { id: sessionId, ...updates }
      })
      return updatedSession
    }
  }

  /**
   * Delete a session with optimistic update
   */
  async deleteSession(sessionId: string): Promise<boolean> {
    // Optimistically delete from cache
    await indexedDBService.delete(STORES.SESSIONS, sessionId)

    if (!navigator.onLine) {
      await indexedDBService.addPendingOperation({
        type: 'delete',
        store: STORES.SESSIONS,
        data: { id: sessionId }
      })
      return true
    }

    try {
      await api.delete(`/api/sessions/${sessionId}`)
      return true
    } catch (error) {
      await indexedDBService.addPendingOperation({
        type: 'delete',
        store: STORES.SESSIONS,
        data: { id: sessionId }
      })
      return true
    }
  }

  /**
   * Fetch workspaces with offline-first strategy
   */
  async fetchWorkspaces(
    teamId?: string,
    forceRefresh = false
  ): Promise<SyncResult<Workspace>> {
    const isOnline = navigator.onLine

    let cachedWorkspaces: Workspace[]
    if (teamId) {
      cachedWorkspaces = await indexedDBService.getByIndex<Workspace>(
        STORES.WORKSPACES,
        'teamId',
        teamId
      )
    } else {
      cachedWorkspaces = await indexedDBService.getAll<Workspace>(STORES.WORKSPACES)
    }

    const isStale = await indexedDBService.isCacheStale(
      STORES.WORKSPACES,
      CACHE_MAX_AGE.WORKSPACES
    )

    if (!isOnline || (!forceRefresh && !isStale && cachedWorkspaces.length > 0)) {
      return {
        data: cachedWorkspaces,
        fromCache: true,
        stale: isStale
      }
    }

    try {
      const workspaces = await api.get<Workspace[]>(
        '/api/workspaces',
        teamId ? { teamId } : undefined
      )

      await indexedDBService.putMany(STORES.WORKSPACES, workspaces)
      await indexedDBService.updateCacheMetadata(STORES.WORKSPACES)

      return {
        data: workspaces,
        fromCache: false,
        stale: false
      }
    } catch (error) {
      if (cachedWorkspaces.length > 0) {
        console.warn('Failed to fetch workspaces, using cache:', error)
        return {
          data: cachedWorkspaces,
          fromCache: true,
          stale: true
        }
      }
      throw error
    }
  }

  /**
   * Sync a pending operation
   */
  async syncOperation(operation: PendingOperation): Promise<void> {
    const { type, store, data } = operation
    const typedData = data as Record<string, unknown>

    switch (store) {
      case STORES.SESSIONS:
        await this.syncSessionOperation(type, typedData)
        break
      case STORES.WORKSPACES:
        await this.syncWorkspaceOperation(type, typedData)
        break
      default:
        console.warn(`Unknown store for sync: ${store}`)
    }
  }

  /**
   * Sync a session operation
   */
  private async syncSessionOperation(
    type: 'create' | 'update' | 'delete',
    data: Record<string, unknown>
  ): Promise<void> {
    switch (type) {
      case 'create': {
        const { tempId, ...sessionData } = data
        const created = await api.post<Session>('/api/sessions', sessionData)

        // Replace temp session with real one
        if (tempId) {
          await indexedDBService.delete(STORES.SESSIONS, tempId as string)
        }
        await indexedDBService.put(STORES.SESSIONS, created)
        break
      }
      case 'update': {
        const { id, ...updates } = data
        const updated = await api.patch<Session>(`/api/sessions/${id}`, updates)
        await indexedDBService.put(STORES.SESSIONS, updated)
        break
      }
      case 'delete': {
        const { id } = data
        await api.delete(`/api/sessions/${id}`)
        break
      }
    }
  }

  /**
   * Sync a workspace operation
   */
  private async syncWorkspaceOperation(
    type: 'create' | 'update' | 'delete',
    data: Record<string, unknown>
  ): Promise<void> {
    switch (type) {
      case 'create': {
        const { tempId, ...workspaceData } = data
        const created = await api.post<Workspace>('/api/workspaces', workspaceData)

        if (tempId) {
          await indexedDBService.delete(STORES.WORKSPACES, tempId as string)
        }
        await indexedDBService.put(STORES.WORKSPACES, created)
        break
      }
      case 'update': {
        const { id, ...updates } = data
        const updated = await api.patch<Workspace>(`/api/workspaces/${id}`, updates)
        await indexedDBService.put(STORES.WORKSPACES, updated)
        break
      }
      case 'delete': {
        const { id } = data
        await api.delete(`/api/workspaces/${id}`)
        break
      }
    }
  }

  /**
   * Clear all cached data (for logout)
   */
  async clearAllCaches(): Promise<void> {
    await Promise.all([
      indexedDBService.clear(STORES.SESSIONS),
      indexedDBService.clear(STORES.WORKSPACES),
      indexedDBService.clear(STORES.DOCUMENTS),
      indexedDBService.clear(STORES.CACHE_METADATA),
      indexedDBService.clear(STORES.USER_DATA)
    ])
  }

  /**
   * Get cache statistics
   */
  async getCacheStats(): Promise<{
    sessions: number
    workspaces: number
    documents: number
    pendingOperations: number
  }> {
    const [sessions, workspaces, documents, pendingOperations] = await Promise.all([
      indexedDBService.count(STORES.SESSIONS),
      indexedDBService.count(STORES.WORKSPACES),
      indexedDBService.count(STORES.DOCUMENTS),
      indexedDBService.count(STORES.PENDING_OPERATIONS)
    ])

    return {
      sessions,
      workspaces,
      documents,
      pendingOperations
    }
  }
}

export const syncService = new SyncService()
export default syncService
