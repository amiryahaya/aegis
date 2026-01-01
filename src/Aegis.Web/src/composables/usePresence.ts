import { ref, onMounted, onUnmounted, computed } from 'vue'
import api from '@/services/api'
import type {
  UserPresence,
  PresenceResourceType,
  PresenceStatus,
  CursorPosition,
  UserCursor,
  JoinResourceRequest,
  UpdatePresenceRequest
} from '@/types/presence'
import { getUserColor } from '@/types/presence'

export function usePresence(resourceId?: string, resourceType?: PresenceResourceType) {
  const presenceList = ref<UserPresence[]>([])
  const cursors = ref<UserCursor[]>([])
  const myPresence = ref<UserPresence | null>(null)
  const isLoading = ref(false)
  const error = ref<string | null>(null)

  const onlineUsers = computed(() =>
    presenceList.value.filter(p => p.status === 'Online' || p.status === 'Away')
  )

  const userCount = computed(() => presenceList.value.length)

  async function joinResource(request: JoinResourceRequest): Promise<UserPresence | null> {
    isLoading.value = true
    error.value = null
    try {
      const response = await api.post<UserPresence>('/presence/join', request)
      myPresence.value = response
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to join resource'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function leaveResource(): Promise<boolean> {
    if (!resourceId) return false

    try {
      await api.post('/presence/leave', { resourceId })
      myPresence.value = null
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to leave resource'
      return false
    }
  }

  async function updatePresence(request: UpdatePresenceRequest): Promise<UserPresence | null> {
    try {
      const response = await api.put<UserPresence>('/presence', request)
      myPresence.value = response
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to update presence'
      return null
    }
  }

  async function setStatus(status: PresenceStatus, statusMessage?: string): Promise<UserPresence | null> {
    try {
      const response = await api.post<UserPresence>('/presence/status', {
        status,
        statusMessage
      })
      myPresence.value = response
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to set status'
      return null
    }
  }

  async function updateCursor(position: CursorPosition): Promise<boolean> {
    if (!resourceId) return false

    try {
      await api.post('/presence/cursor', {
        resourceId,
        position
      })
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to update cursor'
      return false
    }
  }

  async function fetchResourcePresence(): Promise<UserPresence[]> {
    if (!resourceId) return []

    isLoading.value = true
    error.value = null
    try {
      const response = await api.get<UserPresence[]>(`/presence/resource/${resourceId}`)
      presenceList.value = response.map(p => ({
        ...p,
        color: p.color || getUserColor(p.userId)
      }))
      return presenceList.value
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch presence'
      return []
    } finally {
      isLoading.value = false
    }
  }

  async function fetchWorkspacePresence(workspaceId: string): Promise<UserPresence[]> {
    isLoading.value = true
    error.value = null
    try {
      const response = await api.get<UserPresence[]>(`/presence/workspace/${workspaceId}`)
      presenceList.value = response.map(p => ({
        ...p,
        color: p.color || getUserColor(p.userId)
      }))
      return presenceList.value
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch workspace presence'
      return []
    } finally {
      isLoading.value = false
    }
  }

  async function fetchCursors(): Promise<UserCursor[]> {
    if (!resourceId) return []

    try {
      const response = await api.get<UserCursor[]>(`/presence/resource/${resourceId}/cursors`)
      cursors.value = response.map(c => ({
        ...c,
        color: c.color || getUserColor(c.userId)
      }))
      return cursors.value
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch cursors'
      return []
    }
  }

  // Event handlers for real-time updates (can be called from SignalR or polling)
  function handleUserJoined(presence: UserPresence) {
    const existingIndex = presenceList.value.findIndex(p => p.userId === presence.userId)
    const enrichedPresence = {
      ...presence,
      color: presence.color || getUserColor(presence.userId)
    }

    if (existingIndex >= 0) {
      presenceList.value[existingIndex] = enrichedPresence
    } else {
      presenceList.value.push(enrichedPresence)
    }
  }

  function handleUserLeft(userId: string) {
    presenceList.value = presenceList.value.filter(p => p.userId !== userId)
    cursors.value = cursors.value.filter(c => c.userId !== userId)
  }

  function handlePresenceUpdated(presence: UserPresence) {
    const index = presenceList.value.findIndex(p => p.userId === presence.userId)
    if (index >= 0) {
      presenceList.value[index] = {
        ...presence,
        color: presence.color || getUserColor(presence.userId)
      }
    }
  }

  function handleCursorUpdated(cursor: UserCursor) {
    const index = cursors.value.findIndex(c => c.userId === cursor.userId)
    const enrichedCursor = {
      ...cursor,
      color: cursor.color || getUserColor(cursor.userId)
    }

    if (index >= 0) {
      cursors.value[index] = enrichedCursor
    } else {
      cursors.value.push(enrichedCursor)
    }
  }

  // Lifecycle
  onMounted(async () => {
    if (resourceId && resourceType) {
      await fetchResourcePresence()

      // Auto-join the resource
      await joinResource({
        resourceId,
        resourceType
      })
    }
  })

  onUnmounted(async () => {
    if (resourceId) {
      await leaveResource()
    }
  })

  return {
    // State
    presenceList,
    cursors,
    myPresence,
    isLoading,
    error,

    // Computed
    onlineUsers,
    userCount,

    // Actions
    joinResource,
    leaveResource,
    updatePresence,
    setStatus,
    updateCursor,
    fetchResourcePresence,
    fetchWorkspacePresence,
    fetchCursors,

    // Event handlers (for external SignalR integration)
    handleUserJoined,
    handleUserLeft,
    handlePresenceUpdated,
    handleCursorUpdated
  }
}
