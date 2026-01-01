// Presence Types

export interface UserPresence {
  userId: string
  displayName: string
  avatarUrl?: string
  email?: string
  resourceId?: string
  resourceType?: PresenceResourceType
  workspaceId?: string
  connectionId?: string
  status: PresenceStatus
  statusMessage?: string
  activity: PresenceActivity
  joinedAt: string
  lastActiveAt: string
  cursorPosition?: CursorPosition
  color?: string
  metadata: Record<string, unknown>
}

export type PresenceResourceType =
  | 'Session'
  | 'Document'
  | 'Query'
  | 'Report'
  | 'Dashboard'
  | 'Workspace'
  | 'Collection'

export type PresenceStatus = 'Online' | 'Away' | 'Busy' | 'DoNotDisturb' | 'Offline'

export type PresenceActivity = 'Viewing' | 'Editing' | 'Commenting' | 'Typing' | 'Idle'

export interface JoinResourceRequest {
  resourceId: string
  resourceType: PresenceResourceType
  workspaceId?: string
  initialActivity?: PresenceActivity
}

export interface UpdatePresenceRequest {
  resourceId?: string
  status?: PresenceStatus
  statusMessage?: string
  activity?: PresenceActivity
}

export interface CursorPosition {
  line?: number
  column?: number
  startOffset?: number
  endOffset?: number
  selectionText?: string
  elementId?: string
  x?: number
  y?: number
}

export interface UserCursor {
  userId: string
  displayName: string
  avatarUrl?: string
  color: string
  position: CursorPosition
  updatedAt: string
}

export interface PresenceStats {
  totalOnlineUsers: number
  totalActiveResources: number
  usersByStatus: Record<PresenceStatus, number>
  usersByActivity: Record<PresenceActivity, number>
  usersByResourceType: Record<PresenceResourceType, number>
  peakConcurrentUsers: number
  peakTime: string
  generatedAt: string
}

// Helper function to get status color
export function getStatusColor(status: PresenceStatus): string {
  const colors: Record<PresenceStatus, string> = {
    Online: 'bg-green-500',
    Away: 'bg-yellow-500',
    Busy: 'bg-red-500',
    DoNotDisturb: 'bg-red-600',
    Offline: 'bg-gray-400'
  }
  return colors[status]
}

// Helper function to get status text
export function getStatusText(status: PresenceStatus): string {
  const texts: Record<PresenceStatus, string> = {
    Online: 'Online',
    Away: 'Away',
    Busy: 'Busy',
    DoNotDisturb: 'Do Not Disturb',
    Offline: 'Offline'
  }
  return texts[status]
}

// Helper function to get activity text
export function getActivityText(activity: PresenceActivity): string {
  const texts: Record<PresenceActivity, string> = {
    Viewing: 'Viewing',
    Editing: 'Editing',
    Commenting: 'Commenting',
    Typing: 'Typing',
    Idle: 'Idle'
  }
  return texts[activity]
}

// Generate a consistent color based on user ID
export function getUserColor(userId: string): string {
  const colors = [
    '#ef4444', // red
    '#f97316', // orange
    '#eab308', // yellow
    '#22c55e', // green
    '#14b8a6', // teal
    '#3b82f6', // blue
    '#8b5cf6', // violet
    '#ec4899', // pink
    '#06b6d4', // cyan
    '#6366f1'  // indigo
  ]

  let hash = 0
  for (let i = 0; i < userId.length; i++) {
    hash = userId.charCodeAt(i) + ((hash << 5) - hash)
  }

  return colors[Math.abs(hash) % colors.length]
}
