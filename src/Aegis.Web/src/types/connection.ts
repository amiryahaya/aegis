export type ConnectionState =
  | 'connected'
  | 'connecting'
  | 'reconnecting'
  | 'disconnected'
  | 'error'

export interface ConnectionStatus {
  state: ConnectionState
  lastConnected?: Date
  lastDisconnected?: Date
  reconnectAttempts: number
  error?: string
}

export interface LiveUser {
  id: string
  name: string
  avatar?: string
  status: 'active' | 'idle' | 'away'
  lastSeen: Date
  currentResource?: {
    type: 'workspace' | 'session' | 'document'
    id: string
    name?: string
  }
}

export interface PresenceUpdate {
  userId: string
  resourceType: 'workspace' | 'session' | 'document'
  resourceId: string
  action: 'joined' | 'left' | 'typing' | 'idle'
  timestamp: Date
}

export interface TypingIndicator {
  userId: string
  userName: string
  sessionId: string
  isTyping: boolean
  timestamp: Date
}

export const CONNECTION_STATE_LABELS: Record<ConnectionState, string> = {
  connected: 'Connected',
  connecting: 'Connecting...',
  reconnecting: 'Reconnecting...',
  disconnected: 'Disconnected',
  error: 'Connection Error'
}

export const CONNECTION_STATE_COLORS: Record<ConnectionState, { bg: string; text: string; dot: string }> = {
  connected: {
    bg: 'bg-green-100 dark:bg-green-900/30',
    text: 'text-green-700 dark:text-green-400',
    dot: 'bg-green-500'
  },
  connecting: {
    bg: 'bg-yellow-100 dark:bg-yellow-900/30',
    text: 'text-yellow-700 dark:text-yellow-400',
    dot: 'bg-yellow-500'
  },
  reconnecting: {
    bg: 'bg-yellow-100 dark:bg-yellow-900/30',
    text: 'text-yellow-700 dark:text-yellow-400',
    dot: 'bg-yellow-500'
  },
  disconnected: {
    bg: 'bg-gray-100 dark:bg-gray-700',
    text: 'text-gray-600 dark:text-gray-400',
    dot: 'bg-gray-400'
  },
  error: {
    bg: 'bg-red-100 dark:bg-red-900/30',
    text: 'text-red-700 dark:text-red-400',
    dot: 'bg-red-500'
  }
}
