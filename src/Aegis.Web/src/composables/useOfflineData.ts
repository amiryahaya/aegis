import { ref, computed, onMounted } from 'vue'
import { syncService } from '@/services/sync.service'
import { indexedDBService, STORES } from '@/services/indexeddb.service'
import { useOfflineQueue } from './useOfflineQueue'
import type { Session, Workspace } from '@/types'

/**
 * Composable for offline-first data access
 * Provides reactive data with automatic caching and sync
 */
export function useOfflineSessions(userId: string) {
  const sessions = ref<Session[]>([])
  const isLoading = ref(false)
  const error = ref<string | null>(null)
  const isFromCache = ref(false)
  const isStale = ref(false)

  const { isOnline, registerSyncHandler } = useOfflineQueue()

  async function fetchSessions(forceRefresh = false) {
    isLoading.value = true
    error.value = null

    try {
      const result = await syncService.fetchSessions(userId, forceRefresh)
      sessions.value = result.data
      isFromCache.value = result.fromCache
      isStale.value = result.stale
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to load sessions'
    } finally {
      isLoading.value = false
    }
  }

  async function createSession(session: Partial<Session>): Promise<Session | null> {
    try {
      const created = await syncService.createSession(session)
      sessions.value = [created, ...sessions.value]
      return created
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to create session'
      return null
    }
  }

  async function updateSession(sessionId: string, updates: Partial<Session>): Promise<boolean> {
    try {
      const updated = await syncService.updateSession(sessionId, updates)
      if (updated) {
        const index = sessions.value.findIndex(s => s.id === sessionId)
        if (index !== -1) {
          sessions.value[index] = updated
        }
      }
      return !!updated
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to update session'
      return false
    }
  }

  async function deleteSession(sessionId: string): Promise<boolean> {
    try {
      await syncService.deleteSession(sessionId)
      sessions.value = sessions.value.filter(s => s.id !== sessionId)
      return true
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to delete session'
      return false
    }
  }

  // Register sync handler for sessions
  onMounted(() => {
    registerSyncHandler(STORES.SESSIONS, async (operation) => {
      await syncService.syncOperation(operation)
      // Refresh after sync
      await fetchSessions(true)
    })

    fetchSessions()
  })

  return {
    sessions: computed(() => sessions.value),
    isLoading: computed(() => isLoading.value),
    error: computed(() => error.value),
    isFromCache: computed(() => isFromCache.value),
    isStale: computed(() => isStale.value),
    isOnline,
    fetchSessions,
    createSession,
    updateSession,
    deleteSession,
    refresh: () => fetchSessions(true)
  }
}

/**
 * Composable for offline-first workspace data
 */
export function useOfflineWorkspaces(teamId?: string) {
  const workspaces = ref<Workspace[]>([])
  const isLoading = ref(false)
  const error = ref<string | null>(null)
  const isFromCache = ref(false)
  const isStale = ref(false)

  const { isOnline, registerSyncHandler } = useOfflineQueue()

  async function fetchWorkspaces(forceRefresh = false) {
    isLoading.value = true
    error.value = null

    try {
      const result = await syncService.fetchWorkspaces(teamId, forceRefresh)
      workspaces.value = result.data
      isFromCache.value = result.fromCache
      isStale.value = result.stale
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to load workspaces'
    } finally {
      isLoading.value = false
    }
  }

  onMounted(() => {
    registerSyncHandler(STORES.WORKSPACES, async (operation) => {
      await syncService.syncOperation(operation)
      await fetchWorkspaces(true)
    })

    fetchWorkspaces()
  })

  return {
    workspaces: computed(() => workspaces.value),
    isLoading: computed(() => isLoading.value),
    error: computed(() => error.value),
    isFromCache: computed(() => isFromCache.value),
    isStale: computed(() => isStale.value),
    isOnline,
    fetchWorkspaces,
    refresh: () => fetchWorkspaces(true)
  }
}

/**
 * Composable for caching individual items
 */
export function useOfflineCache<T extends { id: string }>(storeName: string) {
  async function get(id: string): Promise<T | undefined> {
    return indexedDBService.get<T>(storeName as any, id)
  }

  async function getAll(): Promise<T[]> {
    return indexedDBService.getAll<T>(storeName as any)
  }

  async function set(item: T): Promise<void> {
    return indexedDBService.put(storeName as any, item)
  }

  async function setMany(items: T[]): Promise<void> {
    return indexedDBService.putMany(storeName as any, items)
  }

  async function remove(id: string): Promise<void> {
    return indexedDBService.delete(storeName as any, id)
  }

  async function clear(): Promise<void> {
    return indexedDBService.clear(storeName as any)
  }

  return {
    get,
    getAll,
    set,
    setMany,
    remove,
    clear
  }
}

export default useOfflineSessions
