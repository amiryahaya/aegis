import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import api from '@/services/api'
import signalRService, { type NotificationPayload } from '@/services/signalr.service'
import type {
  Notification,
  NotificationFilter,
  NotificationStats,
  NotificationPreferences,
  PagedResponse
} from '@/types'

export const useNotificationStore = defineStore('notification', () => {
  // State
  const notifications = ref<Notification[]>([])
  const unreadCount = ref(0)
  const stats = ref<NotificationStats | null>(null)
  const preferences = ref<NotificationPreferences | null>(null)
  const isLoading = ref(false)
  const error = ref<string | null>(null)
  const isConnected = ref(false)
  const totalCount = ref(0)
  const currentPage = ref(1)
  const pageSize = ref(20)

  // Toast/popup notification state
  const toastNotification = ref<NotificationPayload | null>(null)

  // Getters
  const hasUnread = computed(() => unreadCount.value > 0)
  const sortedNotifications = computed(() =>
    [...notifications.value].sort((a, b) =>
      new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
    )
  )
  const unreadNotifications = computed(() =>
    notifications.value.filter(n => !n.isRead)
  )

  // Actions - Connection
  async function connect(): Promise<void> {
    try {
      signalRService.setCallbacks({
        onNotification: handleNewNotification,
        onUnreadCount: (count) => {
          unreadCount.value = count
        },
        onNotificationRead: (notificationId) => {
          const notification = notifications.value.find(n => n.id === notificationId)
          if (notification && !notification.isRead) {
            notification.isRead = true
            notification.readAt = new Date().toISOString()
            unreadCount.value = Math.max(0, unreadCount.value - 1)
          }
        },
        onAllNotificationsRead: () => {
          notifications.value.forEach(n => {
            if (!n.isRead) {
              n.isRead = true
              n.readAt = new Date().toISOString()
            }
          })
          unreadCount.value = 0
        },
        onRecentNotifications: (recentNotifications) => {
          // Convert SignalR payloads to Notification type
          const converted = recentNotifications.map(convertPayloadToNotification)
          notifications.value = converted
        },
        onStateChange: (state) => {
          isConnected.value = state === 'connected'
        }
      })

      await signalRService.connectNotificationHub()
      isConnected.value = true
    } catch (err) {
      console.error('Failed to connect to notification hub:', err)
      isConnected.value = false
    }
  }

  async function disconnect(): Promise<void> {
    await signalRService.disconnectNotificationHub()
    isConnected.value = false
  }

  function handleNewNotification(payload: NotificationPayload): void {
    const notification = convertPayloadToNotification(payload)
    notifications.value.unshift(notification)

    if (!notification.isRead) {
      unreadCount.value++
    }

    // Show toast notification
    toastNotification.value = payload

    // Auto-dismiss toast after 5 seconds
    setTimeout(() => {
      if (toastNotification.value?.id === payload.id) {
        toastNotification.value = null
      }
    }, 5000)
  }

  function convertPayloadToNotification(payload: NotificationPayload): Notification {
    return {
      id: payload.id,
      userId: '', // Not provided in payload
      type: payload.type as Notification['type'],
      title: payload.title,
      message: payload.message,
      priority: payload.priority as Notification['priority'],
      channels: ['InApp', 'RealTime'],
      isRead: payload.isRead,
      createdAt: payload.createdAt,
      data: payload.data
    }
  }

  function dismissToast(): void {
    toastNotification.value = null
  }

  // Actions - API
  async function fetchNotifications(filter?: NotificationFilter): Promise<void> {
    isLoading.value = true
    error.value = null
    try {
      const response = await api.get<PagedResponse<Notification>>('/notifications', {
        ...filter,
        pageNumber: filter?.pageNumber || currentPage.value,
        pageSize: filter?.pageSize || pageSize.value
      })
      notifications.value = response.items
      totalCount.value = response.totalCount
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch notifications'
    } finally {
      isLoading.value = false
    }
  }

  async function fetchStats(): Promise<void> {
    isLoading.value = true
    error.value = null
    try {
      const response = await api.get<NotificationStats>('/notifications/stats')
      stats.value = response
      unreadCount.value = response.unreadCount
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch notification stats'
    } finally {
      isLoading.value = false
    }
  }

  async function markAsRead(notificationId: string): Promise<boolean> {
    try {
      await signalRService.markAsRead(notificationId)

      // Optimistically update local state
      const notification = notifications.value.find(n => n.id === notificationId)
      if (notification && !notification.isRead) {
        notification.isRead = true
        notification.readAt = new Date().toISOString()
        unreadCount.value = Math.max(0, unreadCount.value - 1)
      }

      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to mark as read'
      return false
    }
  }

  async function markAllAsRead(): Promise<boolean> {
    try {
      await signalRService.markAllAsRead()

      // Optimistically update local state
      notifications.value.forEach(n => {
        if (!n.isRead) {
          n.isRead = true
          n.readAt = new Date().toISOString()
        }
      })
      unreadCount.value = 0

      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to mark all as read'
      return false
    }
  }

  async function deleteNotification(notificationId: string): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await api.delete(`/notifications/${notificationId}`)
      const notification = notifications.value.find(n => n.id === notificationId)
      if (notification && !notification.isRead) {
        unreadCount.value = Math.max(0, unreadCount.value - 1)
      }
      notifications.value = notifications.value.filter(n => n.id !== notificationId)
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to delete notification'
      return false
    } finally {
      isLoading.value = false
    }
  }

  // Actions - Preferences
  async function fetchPreferences(): Promise<void> {
    isLoading.value = true
    error.value = null
    try {
      const response = await api.get<NotificationPreferences>('/notifications/preferences')
      preferences.value = response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch preferences'
    } finally {
      isLoading.value = false
    }
  }

  async function updatePreferences(newPreferences: Partial<NotificationPreferences>): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      const response = await api.put<NotificationPreferences>('/notifications/preferences', newPreferences)
      preferences.value = response
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to update preferences'
      return false
    } finally {
      isLoading.value = false
    }
  }

  // Actions - Team/Workspace subscriptions
  async function joinTeam(teamId: string): Promise<void> {
    await signalRService.joinTeam(teamId)
  }

  async function leaveTeam(teamId: string): Promise<void> {
    await signalRService.leaveTeam(teamId)
  }

  async function joinWorkspace(workspaceId: string): Promise<void> {
    await signalRService.joinWorkspace(workspaceId)
  }

  async function leaveWorkspace(workspaceId: string): Promise<void> {
    await signalRService.leaveWorkspace(workspaceId)
  }

  // Cleanup
  function clearError(): void {
    error.value = null
  }

  function reset(): void {
    notifications.value = []
    unreadCount.value = 0
    stats.value = null
    toastNotification.value = null
  }

  return {
    // State
    notifications,
    unreadCount,
    stats,
    preferences,
    isLoading,
    error,
    isConnected,
    totalCount,
    currentPage,
    pageSize,
    toastNotification,

    // Getters
    hasUnread,
    sortedNotifications,
    unreadNotifications,

    // Actions - Connection
    connect,
    disconnect,
    dismissToast,

    // Actions - API
    fetchNotifications,
    fetchStats,
    markAsRead,
    markAllAsRead,
    deleteNotification,

    // Actions - Preferences
    fetchPreferences,
    updatePreferences,

    // Actions - Subscriptions
    joinTeam,
    leaveTeam,
    joinWorkspace,
    leaveWorkspace,

    // Cleanup
    clearError,
    reset
  }
})
