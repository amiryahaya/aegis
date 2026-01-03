import api from './api'

// =============================================================================
// Request/Response Types (matching backend DTOs)
// =============================================================================

export interface NotificationResponse {
  id: string
  userId: string
  type: NotificationType
  priority: NotificationPriority
  title: string
  message: string
  actionUrl?: string
  actionLabel?: string
  status: NotificationStatus
  data?: Record<string, unknown>
  relatedEntityId?: string
  relatedEntityType?: string
  teamId?: string
  workspaceId?: string
  createdAt: string
  readAt?: string
  archivedAt?: string
  expiresAt?: string
}

export type NotificationType =
  | 'QueryCompleted'
  | 'QueryFailed'
  | 'DocumentUploaded'
  | 'DocumentProcessed'
  | 'DocumentProcessingFailed'
  | 'DocumentDeleted'
  | 'SyncStarted'
  | 'SyncCompleted'
  | 'SyncFailed'
  | 'SystemAlert'
  | 'MaintenanceScheduled'
  | 'ServiceDegraded'
  | 'WelcomeMessage'
  | 'TeamInvitation'
  | 'TeamRemoval'
  | 'RoleChanged'
  | 'RateLimitWarning'
  | 'ApiKeyExpiring'
  | 'ApiKeyRevoked'
  | 'SuspiciousActivity'
  | 'WebhookDeliveryFailed'
  | 'WebhookDisabled'
  | 'FeatureFlagChanged'
  | 'ConfigurationChanged'
  | 'FeedbackReceived'
  | 'FeedbackResolved'
  | 'Custom'

export type NotificationPriority = 'Low' | 'Normal' | 'High' | 'Urgent'

export type NotificationStatus = 'Unread' | 'Read' | 'Archived' | 'Expired'

export type NotificationChannel = 'InApp' | 'Email' | 'Push' | 'RealTime'

export interface NotificationFilter {
  type?: NotificationType
  status?: NotificationStatus
  priority?: NotificationPriority
  page?: number
  pageSize?: number
  includeArchived?: boolean
}

export interface NotificationPageResponse {
  items: NotificationResponse[]
  totalCount: number
  pageNumber: number
  pageSize: number
  totalPages: number
  hasNextPage: boolean
  hasPreviousPage: boolean
}

export interface NotificationStats {
  userId: string
  totalCount: number
  unreadCount: number
  readCount: number
  archivedCount: number
  countByType: Record<string, number>
  countByPriority: Record<string, number>
  lastNotificationAt?: string
  lastReadAt?: string
}

export interface NotificationTypeInfo {
  name: string
  description: string
  category: string
}

export interface SendNotificationRequest {
  userId: string
  type: NotificationType
  title: string
  message: string
  priority?: NotificationPriority
  channels?: NotificationChannel[]
  actionUrl?: string
  actionLabel?: string
  data?: Record<string, unknown>
  relatedEntityId?: string
  relatedEntityType?: string
  teamId?: string
  workspaceId?: string
  expiresAt?: string
}

// =============================================================================
// Notification Service
// =============================================================================

class NotificationService {
  private readonly basePath = '/notifications'

  /**
   * Get notifications for a user
   */
  async getNotifications(
    userId: string,
    filter?: NotificationFilter
  ): Promise<NotificationPageResponse> {
    const params: Record<string, unknown> = { userId }

    if (filter?.type) params.type = filter.type
    if (filter?.status) params.status = filter.status
    if (filter?.priority) params.priority = filter.priority
    if (filter?.page) params.page = filter.page
    if (filter?.pageSize) params.pageSize = filter.pageSize
    if (filter?.includeArchived !== undefined) params.includeArchived = filter.includeArchived

    return api.get<NotificationPageResponse>(this.basePath, params)
  }

  /**
   * Get a single notification by ID
   */
  async getById(id: string): Promise<NotificationResponse> {
    return api.get<NotificationResponse>(`${this.basePath}/${id}`)
  }

  /**
   * Get unread notification count
   */
  async getUnreadCount(userId: string): Promise<number> {
    return api.get<number>(`${this.basePath}/unread-count`, { userId })
  }

  /**
   * Get notification statistics
   */
  async getStats(userId: string): Promise<NotificationStats> {
    return api.get<NotificationStats>(`${this.basePath}/stats`, { userId })
  }

  /**
   * Mark a notification as read
   */
  async markAsRead(id: string): Promise<void> {
    return api.put<void>(`${this.basePath}/${id}/read`)
  }

  /**
   * Mark all notifications as read
   */
  async markAllAsRead(userId: string): Promise<{ count: number }> {
    return api.put<{ count: number }>(`${this.basePath}/read-all?userId=${encodeURIComponent(userId)}`)
  }

  /**
   * Archive a notification
   */
  async archive(id: string): Promise<void> {
    return api.put<void>(`${this.basePath}/${id}/archive`)
  }

  /**
   * Delete a notification
   */
  async delete(id: string): Promise<void> {
    return api.delete<void>(`${this.basePath}/${id}`)
  }

  /**
   * Send a notification (admin only)
   */
  async send(request: SendNotificationRequest): Promise<NotificationResponse> {
    return api.post<NotificationResponse>(`${this.basePath}/send`, request)
  }

  /**
   * Send a notification to all team members (admin only)
   */
  async sendToTeam(
    teamId: string,
    request: Omit<SendNotificationRequest, 'userId'>
  ): Promise<{ recipientCount: number }> {
    return api.post<{ recipientCount: number }>(
      `${this.basePath}/send-to-team/${teamId}`,
      request
    )
  }

  /**
   * Get available notification types
   */
  async getTypes(): Promise<NotificationTypeInfo[]> {
    return api.get<NotificationTypeInfo[]>(`${this.basePath}/types`)
  }

  // =============================================================================
  // Utility Methods
  // =============================================================================

  /**
   * Get notification category for a type
   */
  getCategory(type: NotificationType): string {
    const categoryMap: Record<string, NotificationType[]> = {
      Query: ['QueryCompleted', 'QueryFailed'],
      Document: ['DocumentUploaded', 'DocumentProcessed', 'DocumentProcessingFailed', 'DocumentDeleted'],
      DataSource: ['SyncStarted', 'SyncCompleted', 'SyncFailed'],
      System: ['SystemAlert', 'MaintenanceScheduled', 'ServiceDegraded'],
      User: ['WelcomeMessage', 'TeamInvitation', 'TeamRemoval', 'RoleChanged'],
      Security: ['RateLimitWarning', 'ApiKeyExpiring', 'ApiKeyRevoked', 'SuspiciousActivity'],
      Webhook: ['WebhookDeliveryFailed', 'WebhookDisabled'],
      Configuration: ['FeatureFlagChanged', 'ConfigurationChanged'],
      Feedback: ['FeedbackReceived', 'FeedbackResolved']
    }

    for (const [category, types] of Object.entries(categoryMap)) {
      if (types.includes(type)) {
        return category
      }
    }

    return 'Other'
  }

  /**
   * Get priority badge info for display
   */
  getPriorityBadge(priority: NotificationPriority): {
    label: string
    color: 'gray' | 'blue' | 'yellow' | 'red'
  } {
    switch (priority) {
      case 'Low':
        return { label: 'Low', color: 'gray' }
      case 'Normal':
        return { label: 'Normal', color: 'blue' }
      case 'High':
        return { label: 'High', color: 'yellow' }
      case 'Urgent':
        return { label: 'Urgent', color: 'red' }
      default:
        return { label: priority, color: 'gray' }
    }
  }

  /**
   * Get status badge info for display
   */
  getStatusBadge(status: NotificationStatus): {
    label: string
    color: 'blue' | 'green' | 'gray' | 'yellow'
  } {
    switch (status) {
      case 'Unread':
        return { label: 'Unread', color: 'blue' }
      case 'Read':
        return { label: 'Read', color: 'green' }
      case 'Archived':
        return { label: 'Archived', color: 'gray' }
      case 'Expired':
        return { label: 'Expired', color: 'yellow' }
      default:
        return { label: status, color: 'gray' }
    }
  }

  /**
   * Get icon name for notification type
   */
  getTypeIcon(type: NotificationType): string {
    const iconMap: Record<string, string> = {
      QueryCompleted: 'check-circle',
      QueryFailed: 'x-circle',
      DocumentUploaded: 'document-arrow-up',
      DocumentProcessed: 'document-check',
      DocumentProcessingFailed: 'document-minus',
      DocumentDeleted: 'trash',
      SyncStarted: 'arrow-path',
      SyncCompleted: 'check',
      SyncFailed: 'exclamation-triangle',
      SystemAlert: 'bell-alert',
      MaintenanceScheduled: 'wrench',
      ServiceDegraded: 'exclamation-circle',
      WelcomeMessage: 'hand-raised',
      TeamInvitation: 'user-plus',
      TeamRemoval: 'user-minus',
      RoleChanged: 'shield-check',
      RateLimitWarning: 'clock',
      ApiKeyExpiring: 'key',
      ApiKeyRevoked: 'lock-closed',
      SuspiciousActivity: 'shield-exclamation',
      WebhookDeliveryFailed: 'link',
      WebhookDisabled: 'link-slash',
      FeatureFlagChanged: 'flag',
      ConfigurationChanged: 'cog',
      FeedbackReceived: 'chat-bubble-left',
      FeedbackResolved: 'chat-bubble-left-right',
      Custom: 'bell'
    }

    return iconMap[type] || 'bell'
  }

  /**
   * Format notification for display
   */
  formatNotification(notification: NotificationResponse): {
    id: string
    title: string
    message: string
    time: string
    isRead: boolean
    priority: NotificationPriority
    category: string
    icon: string
    actionUrl?: string
    actionLabel?: string
  } {
    const createdAt = new Date(notification.createdAt)
    const now = new Date()
    const diffMs = now.getTime() - createdAt.getTime()
    const diffMins = Math.floor(diffMs / 60000)
    const diffHours = Math.floor(diffMins / 60)
    const diffDays = Math.floor(diffHours / 24)

    let time: string
    if (diffMins < 1) {
      time = 'Just now'
    } else if (diffMins < 60) {
      time = `${diffMins}m ago`
    } else if (diffHours < 24) {
      time = `${diffHours}h ago`
    } else if (diffDays < 7) {
      time = `${diffDays}d ago`
    } else {
      time = createdAt.toLocaleDateString()
    }

    return {
      id: notification.id,
      title: notification.title,
      message: notification.message,
      time,
      isRead: notification.status === 'Read' || notification.status === 'Archived',
      priority: notification.priority,
      category: this.getCategory(notification.type),
      icon: this.getTypeIcon(notification.type),
      actionUrl: notification.actionUrl,
      actionLabel: notification.actionLabel
    }
  }

  /**
   * Group notifications by date
   */
  groupByDate(notifications: NotificationResponse[]): Map<string, NotificationResponse[]> {
    const groups = new Map<string, NotificationResponse[]>()
    const today = new Date()
    const yesterday = new Date(today)
    yesterday.setDate(yesterday.getDate() - 1)

    for (const notification of notifications) {
      const date = new Date(notification.createdAt)
      let key: string

      if (date.toDateString() === today.toDateString()) {
        key = 'Today'
      } else if (date.toDateString() === yesterday.toDateString()) {
        key = 'Yesterday'
      } else if (date > new Date(today.getTime() - 7 * 24 * 60 * 60 * 1000)) {
        key = 'This Week'
      } else {
        key = date.toLocaleDateString('en-US', { month: 'long', year: 'numeric' })
      }

      if (!groups.has(key)) {
        groups.set(key, [])
      }
      groups.get(key)!.push(notification)
    }

    return groups
  }
}

export const notificationService = new NotificationService()
export default notificationService
