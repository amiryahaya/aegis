import { ref, computed, readonly, onMounted, onUnmounted } from 'vue'
import notificationService from '@/services/notification.service'
import signalRService from '@/services/signalr.service'
import type {
  NotificationResponse,
  NotificationType,
  NotificationPriority,
  NotificationStatus
} from '@/services/notification.service'

export interface FormattedNotification {
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
}

export interface UseNotificationsOptions {
  userId?: string
  autoConnect?: boolean
  onNewNotification?: (notification: NotificationResponse) => void
  onUnreadCountChange?: (count: number) => void
}

export function useNotifications(options: UseNotificationsOptions = {}) {
  // State
  const notifications = ref<NotificationResponse[]>([])
  const unreadCount = ref(0)
  const isLoading = ref(false)
  const error = ref<string | null>(null)
  const isConnected = ref(false)
  const totalCount = ref(0)
  const currentPage = ref(1)
  const pageSize = ref(20)

  // Toast state for popup notifications
  const toastNotification = ref<NotificationResponse | null>(null)
  const toastTimeout = ref<ReturnType<typeof setTimeout> | null>(null)

  // Computed
  const hasUnread = computed(() => unreadCount.value > 0)
  const hasMore = computed(() => notifications.value.length < totalCount.value)

  const sortedNotifications = computed(() =>
    [...notifications.value].sort((a, b) =>
      new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
    )
  )

  const unreadNotifications = computed(() =>
    notifications.value.filter(n => n.status === 'Unread')
  )

  const formattedNotifications = computed<FormattedNotification[]>(() =>
    sortedNotifications.value.map(n => notificationService.formatNotification(n))
  )

  const groupedNotifications = computed(() =>
    notificationService.groupByDate(sortedNotifications.value)
  )

  // Actions - Connection
  async function connect(): Promise<void> {
    try {
      signalRService.setCallbacks({
        onNotification: handleNewNotification,
        onUnreadCount: (count) => {
          unreadCount.value = count
          options.onUnreadCountChange?.(count)
        },
        onNotificationRead: (notificationId) => {
          const notification = notifications.value.find(n => n.id === notificationId)
          if (notification && notification.status === 'Unread') {
            notification.status = 'Read'
            notification.readAt = new Date().toISOString()
            unreadCount.value = Math.max(0, unreadCount.value - 1)
          }
        },
        onAllNotificationsRead: () => {
          notifications.value.forEach(n => {
            if (n.status === 'Unread') {
              n.status = 'Read'
              n.readAt = new Date().toISOString()
            }
          })
          unreadCount.value = 0
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

  function handleNewNotification(payload: import('@/services/signalr.service').NotificationPayload): void {
    const notification: NotificationResponse = {
      id: payload.id,
      userId: options.userId || '',
      type: payload.type as NotificationType,
      priority: payload.priority as NotificationPriority,
      title: payload.title,
      message: payload.message,
      status: payload.isRead ? 'Read' : 'Unread',
      createdAt: payload.createdAt,
      data: payload.data
    }

    notifications.value.unshift(notification)

    if (!payload.isRead) {
      unreadCount.value++
    }

    // Show toast
    showToast(notification)

    options.onNewNotification?.(notification)
  }

  // Actions - Toast
  function showToast(notification: NotificationResponse, duration = 5000): void {
    // Clear existing timeout
    if (toastTimeout.value) {
      clearTimeout(toastTimeout.value)
    }

    toastNotification.value = notification

    toastTimeout.value = setTimeout(() => {
      if (toastNotification.value?.id === notification.id) {
        toastNotification.value = null
      }
    }, duration)
  }

  function dismissToast(): void {
    if (toastTimeout.value) {
      clearTimeout(toastTimeout.value)
    }
    toastNotification.value = null
  }

  // Actions - Fetch
  async function fetchNotifications(page = 1): Promise<void> {
    isLoading.value = true
    error.value = null
    try {
      const response = await notificationService.getNotifications(options.userId || '', {
        page,
        pageSize: pageSize.value
      })
      if (page === 1) {
        notifications.value = response.items
      } else {
        notifications.value = [...notifications.value, ...response.items]
      }
      totalCount.value = response.totalCount
      currentPage.value = response.pageNumber
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch notifications'
    } finally {
      isLoading.value = false
    }
  }

  async function fetchUnreadCount(): Promise<void> {
    try {
      unreadCount.value = await notificationService.getUnreadCount(options.userId || '')
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch unread count'
    }
  }

  async function loadMore(): Promise<void> {
    if (!hasMore.value || isLoading.value) return
    await fetchNotifications(currentPage.value + 1)
  }

  // Actions - Mark as read
  async function markAsRead(notificationId: string): Promise<boolean> {
    try {
      await notificationService.markAsRead(notificationId)

      const notification = notifications.value.find(n => n.id === notificationId)
      if (notification && notification.status === 'Unread') {
        notification.status = 'Read'
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
      await notificationService.markAllAsRead(options.userId || '')

      notifications.value.forEach(n => {
        if (n.status === 'Unread') {
          n.status = 'Read'
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

  // Actions - Archive/Delete
  async function archive(notificationId: string): Promise<boolean> {
    try {
      await notificationService.archive(notificationId)

      const notification = notifications.value.find(n => n.id === notificationId)
      if (notification) {
        const wasUnread = notification.status === 'Unread'
        notification.status = 'Archived'
        notification.archivedAt = new Date().toISOString()
        if (wasUnread) {
          unreadCount.value = Math.max(0, unreadCount.value - 1)
        }
      }

      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to archive notification'
      return false
    }
  }

  async function deleteNotification(notificationId: string): Promise<boolean> {
    try {
      await notificationService.delete(notificationId)

      const notification = notifications.value.find(n => n.id === notificationId)
      if (notification && notification.status === 'Unread') {
        unreadCount.value = Math.max(0, unreadCount.value - 1)
      }

      notifications.value = notifications.value.filter(n => n.id !== notificationId)

      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to delete notification'
      return false
    }
  }

  // Actions - Filter
  async function filterByType(type: NotificationType): Promise<void> {
    isLoading.value = true
    error.value = null
    try {
      const response = await notificationService.getNotifications(options.userId || '', {
        type,
        page: 1,
        pageSize: pageSize.value
      })
      notifications.value = response.items
      totalCount.value = response.totalCount
      currentPage.value = 1
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to filter notifications'
    } finally {
      isLoading.value = false
    }
  }

  async function filterByStatus(status: NotificationStatus): Promise<void> {
    isLoading.value = true
    error.value = null
    try {
      const response = await notificationService.getNotifications(options.userId || '', {
        status,
        page: 1,
        pageSize: pageSize.value
      })
      notifications.value = response.items
      totalCount.value = response.totalCount
      currentPage.value = 1
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to filter notifications'
    } finally {
      isLoading.value = false
    }
  }

  // Utilities
  function getCategory(type: NotificationType): string {
    return notificationService.getCategory(type)
  }

  function getPriorityBadge(priority: NotificationPriority): { label: string; color: string } {
    return notificationService.getPriorityBadge(priority)
  }

  function getTypeIcon(type: NotificationType): string {
    return notificationService.getTypeIcon(type)
  }

  // Reset
  function reset(): void {
    notifications.value = []
    unreadCount.value = 0
    error.value = null
    currentPage.value = 1
    totalCount.value = 0
    dismissToast()
  }

  // Lifecycle
  if (options.autoConnect) {
    onMounted(async () => {
      await connect()
      await fetchNotifications()
      await fetchUnreadCount()
    })

    onUnmounted(() => {
      disconnect()
      dismissToast()
    })
  }

  return {
    // State (readonly)
    notifications: readonly(notifications),
    unreadCount: readonly(unreadCount),
    isLoading: readonly(isLoading),
    error: readonly(error),
    isConnected: readonly(isConnected),
    totalCount: readonly(totalCount),
    currentPage: readonly(currentPage),
    toastNotification: readonly(toastNotification),

    // Computed
    hasUnread,
    hasMore,
    sortedNotifications,
    unreadNotifications,
    formattedNotifications,
    groupedNotifications,

    // Actions - Connection
    connect,
    disconnect,

    // Actions - Toast
    showToast,
    dismissToast,

    // Actions - Fetch
    fetchNotifications,
    fetchUnreadCount,
    loadMore,

    // Actions - Mark as read
    markAsRead,
    markAllAsRead,

    // Actions - Archive/Delete
    archive,
    deleteNotification,

    // Actions - Filter
    filterByType,
    filterByStatus,

    // Utilities
    getCategory,
    getPriorityBadge,
    getTypeIcon,

    // Reset
    reset
  }
}

export type NotificationsComposable = ReturnType<typeof useNotifications>
