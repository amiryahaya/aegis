import { ref, onMounted, onUnmounted } from 'vue'
import signalRService, {
  type ConnectionState,
  type QueryCitation,
  type NotificationPayload
} from '@/services/signalr.service'

export interface StreamingState {
  isStreaming: boolean
  tokens: string[]
  fullResponse: string
  citations: QueryCitation[]
  error: string | null
}

export function useQueryStream() {
  const connectionState = ref<ConnectionState>('disconnected')
  const streamState = ref<StreamingState>({
    isStreaming: false,
    tokens: [],
    fullResponse: '',
    citations: [],
    error: null
  })

  function resetStream() {
    streamState.value = {
      isStreaming: false,
      tokens: [],
      fullResponse: '',
      citations: [],
      error: null
    }
  }

  async function connect() {
    signalRService.setCallbacks({
      onStateChange: (state) => {
        connectionState.value = state
      },
      onStreamToken: (token) => {
        streamState.value.tokens.push(token)
        streamState.value.fullResponse += token
      },
      onCitations: (citations) => {
        streamState.value.citations = citations
      },
      onStreamComplete: () => {
        streamState.value.isStreaming = false
      },
      onError: (error) => {
        streamState.value.error = error
        streamState.value.isStreaming = false
      }
    })

    await signalRService.connectQueryHub()
  }

  async function disconnect() {
    await signalRService.disconnectQueryHub()
  }

  async function streamQuery(teamId: string, query: string, maxResults = 5) {
    resetStream()
    streamState.value.isStreaming = true

    try {
      await signalRService.streamQuery(teamId, query, maxResults)
    } catch (error) {
      streamState.value.error = error instanceof Error ? error.message : 'Failed to stream query'
      streamState.value.isStreaming = false
    }
  }

  onMounted(() => {
    connect()
  })

  onUnmounted(() => {
    disconnect()
  })

  return {
    connectionState,
    streamState,
    connect,
    disconnect,
    streamQuery,
    resetStream
  }
}

export function useNotifications() {
  const connectionState = ref<ConnectionState>('disconnected')
  const unreadCount = ref(0)
  const notifications = ref<NotificationPayload[]>([])
  const latestNotification = ref<NotificationPayload | null>(null)

  async function connect() {
    signalRService.setCallbacks({
      onStateChange: (state) => {
        connectionState.value = state
      },
      onNotification: (notification) => {
        notifications.value.unshift(notification)
        latestNotification.value = notification
        if (!notification.isRead) {
          unreadCount.value++
        }
      },
      onUnreadCount: (count) => {
        unreadCount.value = count
      },
      onNotificationRead: (notificationId) => {
        const notification = notifications.value.find(n => n.id === notificationId)
        if (notification && !notification.isRead) {
          notification.isRead = true
        }
      },
      onAllNotificationsRead: () => {
        notifications.value.forEach(n => n.isRead = true)
        unreadCount.value = 0
      },
      onRecentNotifications: (recentNotifications) => {
        notifications.value = recentNotifications
      },
      onError: (error) => {
        console.error('Notification error:', error)
      }
    })

    await signalRService.connectNotificationHub()
  }

  async function disconnect() {
    await signalRService.disconnectNotificationHub()
  }

  async function joinTeam(teamId: string) {
    await signalRService.joinTeam(teamId)
  }

  async function leaveTeam(teamId: string) {
    await signalRService.leaveTeam(teamId)
  }

  async function joinWorkspace(workspaceId: string) {
    await signalRService.joinWorkspace(workspaceId)
  }

  async function leaveWorkspace(workspaceId: string) {
    await signalRService.leaveWorkspace(workspaceId)
  }

  async function markAsRead(notificationId: string) {
    await signalRService.markAsRead(notificationId)
  }

  async function markAllAsRead() {
    await signalRService.markAllAsRead()
  }

  async function fetchRecent(count = 10) {
    await signalRService.getRecentNotifications(count)
  }

  function clearLatest() {
    latestNotification.value = null
  }

  onMounted(() => {
    connect()
  })

  onUnmounted(() => {
    disconnect()
  })

  return {
    connectionState,
    unreadCount,
    notifications,
    latestNotification,
    connect,
    disconnect,
    joinTeam,
    leaveTeam,
    joinWorkspace,
    leaveWorkspace,
    markAsRead,
    markAllAsRead,
    fetchRecent,
    clearLatest
  }
}
