// Notification Types

export interface Notification {
  id: string
  userId: string
  teamId?: string
  workspaceId?: string
  type: NotificationType
  title: string
  message: string
  priority: NotificationPriority
  channels: NotificationChannel[]
  isRead: boolean
  readAt?: string
  createdAt: string
  expiresAt?: string
  data?: Record<string, unknown>
  actions?: NotificationAction[]
}

export type NotificationType =
  // Query notifications
  | 'QueryCompleted'
  | 'QueryFailed'
  | 'QueryTimeout'
  // Document notifications
  | 'DocumentUploaded'
  | 'DocumentProcessed'
  | 'DocumentFailed'
  | 'DocumentDeleted'
  // Sync notifications
  | 'SyncStarted'
  | 'SyncCompleted'
  | 'SyncFailed'
  // System notifications
  | 'SystemAlert'
  | 'MaintenanceScheduled'
  | 'SystemUpdate'
  // User notifications
  | 'Welcome'
  | 'PasswordChanged'
  | 'ApiKeyCreated'
  | 'ApiKeyExpiring'
  // Security notifications
  | 'LoginAttempt'
  | 'SuspiciousActivity'
  | 'RateLimitExceeded'
  // Webhook notifications
  | 'WebhookDelivered'
  | 'WebhookFailed'
  // Configuration notifications
  | 'ConfigurationChanged'
  | 'FeatureFlagToggled'
  // Feedback notifications
  | 'FeedbackReceived'
  | 'FeedbackResolved'

export type NotificationPriority =
  | 'Low'
  | 'Normal'
  | 'High'
  | 'Urgent'

export type NotificationChannel =
  | 'InApp'
  | 'Email'
  | 'RealTime'
  | 'Webhook'

export interface NotificationAction {
  label: string
  url?: string
  action?: string
  data?: Record<string, unknown>
}

export interface NotificationFilter {
  type?: NotificationType
  priority?: NotificationPriority
  isRead?: boolean
  fromDate?: string
  toDate?: string
  pageNumber?: number
  pageSize?: number
}

export interface NotificationStats {
  totalCount: number
  unreadCount: number
  byType: Record<string, number>
  byPriority: Record<string, number>
}

export interface NotificationPreferences {
  userId: string
  enabledTypes: NotificationType[]
  enabledChannels: NotificationChannel[]
  quietHoursStart?: string
  quietHoursEnd?: string
  emailDigest: EmailDigestFrequency
}

export type EmailDigestFrequency =
  | 'Immediate'
  | 'Hourly'
  | 'Daily'
  | 'Weekly'
  | 'Never'
