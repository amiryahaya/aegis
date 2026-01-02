import { ref, computed, onMounted, onUnmounted } from 'vue'
import { HubConnectionBuilder, HubConnection, HubConnectionState, LogLevel } from '@microsoft/signalr'
import type {
  ConnectionState,
  ConnectionStatus,
  LiveUser,
  PresenceUpdate,
  TypingIndicator
} from '@/types/connection'
import { useAuthStore } from '@/stores/auth'
import { useToast } from '@/composables/useToast'

// Singleton connection instance
let hubConnection: HubConnection | null = null
let connectionPromise: Promise<void> | null = null

// Global state
const connectionStatus = ref<ConnectionStatus>({
  state: 'disconnected',
  reconnectAttempts: 0
})

const liveUsers = ref<Map<string, LiveUser>>(new Map())
const typingUsers = ref<Map<string, TypingIndicator>>(new Map())

// Configuration
const MAX_RECONNECT_ATTEMPTS = 10
const RECONNECT_DELAYS = [0, 1000, 2000, 5000, 10000, 30000]
const TYPING_TIMEOUT = 3000

export function useConnection() {
  const authStore = useAuthStore()
  const toast = useToast()

  // Computed properties
  const state = computed(() => connectionStatus.value.state)
  const isConnected = computed(() => connectionStatus.value.state === 'connected')
  const isConnecting = computed(() =>
    connectionStatus.value.state === 'connecting' ||
    connectionStatus.value.state === 'reconnecting'
  )
  const hasError = computed(() => connectionStatus.value.state === 'error')
  const reconnectAttempts = computed(() => connectionStatus.value.reconnectAttempts)
  const lastError = computed(() => connectionStatus.value.error)

  // Get all live users as array
  const activeLiveUsers = computed(() => Array.from(liveUsers.value.values()))

  // Get users in a specific resource
  function getUsersInResource(resourceType: string, resourceId: string): LiveUser[] {
    return activeLiveUsers.value.filter(
      user => user.currentResource?.type === resourceType &&
              user.currentResource?.id === resourceId
    )
  }

  // Get typing users for a session
  function getTypingUsers(sessionId: string): TypingIndicator[] {
    return Array.from(typingUsers.value.values()).filter(
      indicator => indicator.sessionId === sessionId && indicator.isTyping
    )
  }

  // Update connection state
  function updateState(newState: ConnectionState, error?: string): void {
    connectionStatus.value = {
      ...connectionStatus.value,
      state: newState,
      error,
      ...(newState === 'connected' ? { lastConnected: new Date(), reconnectAttempts: 0 } : {}),
      ...(newState === 'disconnected' ? { lastDisconnected: new Date() } : {})
    }
  }

  // Build connection
  function buildConnection(): HubConnection {
    const baseUrl = import.meta.env.VITE_API_URL || 'http://localhost:5000'

    return new HubConnectionBuilder()
      .withUrl(`${baseUrl}/hubs/presence`, {
        accessTokenFactory: () => authStore.token || ''
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          connectionStatus.value.reconnectAttempts = retryContext.previousRetryCount + 1

          if (retryContext.previousRetryCount >= MAX_RECONNECT_ATTEMPTS) {
            return null // Stop reconnecting
          }

          const delayIndex = Math.min(retryContext.previousRetryCount, RECONNECT_DELAYS.length - 1)
          return RECONNECT_DELAYS[delayIndex]
        }
      })
      .configureLogging(LogLevel.Warning)
      .build()
  }

  // Register event handlers
  function registerHandlers(connection: HubConnection): void {
    // Connection lifecycle events
    connection.onreconnecting(() => {
      updateState('reconnecting')
      toast.warning('Connection lost. Reconnecting...')
    })

    connection.onreconnected(() => {
      updateState('connected')
      toast.success('Connection restored')
      // Re-announce presence
      announcePresence()
    })

    connection.onclose((error) => {
      if (error) {
        updateState('error', error.message)
        toast.error('Connection failed: ' + error.message)
      } else {
        updateState('disconnected')
      }
    })

    // Presence events
    connection.on('UserJoined', (user: LiveUser) => {
      liveUsers.value.set(user.id, user)
    })

    connection.on('UserLeft', (userId: string) => {
      liveUsers.value.delete(userId)
      typingUsers.value.delete(userId)
    })

    connection.on('UserStatusChanged', (userId: string, status: LiveUser['status']) => {
      const user = liveUsers.value.get(userId)
      if (user) {
        liveUsers.value.set(userId, { ...user, status, lastSeen: new Date() })
      }
    })

    connection.on('PresenceUpdate', (update: PresenceUpdate) => {
      const user = liveUsers.value.get(update.userId)
      if (user) {
        if (update.action === 'left') {
          user.currentResource = undefined
        } else {
          user.currentResource = {
            type: update.resourceType,
            id: update.resourceId
          }
        }
        user.lastSeen = new Date()
        liveUsers.value.set(update.userId, user)
      }
    })

    connection.on('TypingIndicator', (indicator: TypingIndicator) => {
      if (indicator.isTyping) {
        typingUsers.value.set(indicator.userId, {
          ...indicator,
          timestamp: new Date()
        })

        // Auto-remove after timeout
        setTimeout(() => {
          const current = typingUsers.value.get(indicator.userId)
          if (current && current.sessionId === indicator.sessionId) {
            typingUsers.value.delete(indicator.userId)
          }
        }, TYPING_TIMEOUT)
      } else {
        typingUsers.value.delete(indicator.userId)
      }
    })

    // Receive list of online users
    connection.on('OnlineUsers', (users: LiveUser[]) => {
      liveUsers.value.clear()
      users.forEach(user => liveUsers.value.set(user.id, user))
    })
  }

  // Connect to hub
  async function connect(): Promise<void> {
    if (!authStore.isAuthenticated) {
      console.warn('Cannot connect: user not authenticated')
      return
    }

    // Return existing connection promise if connecting
    if (connectionPromise) {
      return connectionPromise
    }

    // Already connected
    if (hubConnection?.state === HubConnectionState.Connected) {
      return
    }

    // Build new connection if needed
    if (!hubConnection) {
      hubConnection = buildConnection()
      registerHandlers(hubConnection)
    }

    updateState('connecting')

    connectionPromise = hubConnection.start()
      .then(() => {
        updateState('connected')
        announcePresence()
      })
      .catch((error) => {
        updateState('error', error.message)
        toast.error('Failed to connect: ' + error.message)
        throw error
      })
      .finally(() => {
        connectionPromise = null
      })

    return connectionPromise
  }

  // Disconnect from hub
  async function disconnect(): Promise<void> {
    if (hubConnection) {
      try {
        await hubConnection.stop()
      } catch (error) {
        console.error('Error disconnecting:', error)
      }
      updateState('disconnected')
    }
  }

  // Announce presence to server
  async function announcePresence(): Promise<void> {
    if (!hubConnection || hubConnection.state !== HubConnectionState.Connected) return
    if (!authStore.user) return

    try {
      await hubConnection.invoke('AnnouncePresence', {
        userId: authStore.user.id,
        name: authStore.user.name || authStore.user.email,
        status: 'active'
      })
    } catch (error) {
      console.error('Error announcing presence:', error)
    }
  }

  // Join a resource (workspace, session, document)
  async function joinResource(resourceType: 'workspace' | 'session' | 'document', resourceId: string): Promise<void> {
    if (!hubConnection || hubConnection.state !== HubConnectionState.Connected) return

    try {
      await hubConnection.invoke('JoinResource', resourceType, resourceId)
    } catch (error) {
      console.error('Error joining resource:', error)
    }
  }

  // Leave a resource
  async function leaveResource(resourceType: 'workspace' | 'session' | 'document', resourceId: string): Promise<void> {
    if (!hubConnection || hubConnection.state !== HubConnectionState.Connected) return

    try {
      await hubConnection.invoke('LeaveResource', resourceType, resourceId)
    } catch (error) {
      console.error('Error leaving resource:', error)
    }
  }

  // Send typing indicator
  async function sendTypingIndicator(sessionId: string, isTyping: boolean): Promise<void> {
    if (!hubConnection || hubConnection.state !== HubConnectionState.Connected) return
    if (!authStore.user) return

    try {
      await hubConnection.invoke('SendTypingIndicator', {
        userId: authStore.user.id,
        userName: authStore.user.name || authStore.user.email,
        sessionId,
        isTyping,
        timestamp: new Date()
      })
    } catch (error) {
      console.error('Error sending typing indicator:', error)
    }
  }

  // Update user status
  async function updateStatus(status: 'active' | 'idle' | 'away'): Promise<void> {
    if (!hubConnection || hubConnection.state !== HubConnectionState.Connected) return

    try {
      await hubConnection.invoke('UpdateStatus', status)
    } catch (error) {
      console.error('Error updating status:', error)
    }
  }

  // Retry connection manually
  async function retry(): Promise<void> {
    connectionStatus.value.reconnectAttempts = 0
    hubConnection = null
    await connect()
  }

  // Auto-connect on mount if authenticated
  onMounted(() => {
    if (authStore.isAuthenticated) {
      connect()
    }
  })

  // Cleanup on unmount (but keep connection alive for other components)
  onUnmounted(() => {
    // Don't disconnect - connection is shared across components
  })

  return {
    // State
    connectionStatus: computed(() => connectionStatus.value),
    state,
    isConnected,
    isConnecting,
    hasError,
    reconnectAttempts,
    lastError,

    // Live users
    liveUsers: activeLiveUsers,
    getUsersInResource,
    getTypingUsers,

    // Actions
    connect,
    disconnect,
    retry,
    joinResource,
    leaveResource,
    sendTypingIndicator,
    updateStatus
  }
}

// Export for global disconnect (e.g., on logout)
export async function disconnectGlobal(): Promise<void> {
  if (hubConnection) {
    await hubConnection.stop()
    hubConnection = null
    connectionStatus.value = {
      state: 'disconnected',
      reconnectAttempts: 0,
      lastDisconnected: new Date()
    }
    liveUsers.value.clear()
    typingUsers.value.clear()
  }
}
