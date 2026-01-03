<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useNotificationStore } from '@/stores/notification'
import { useToast } from '@/composables/useToast'
import {
  BellIcon,
  CheckIcon,
  CheckCircleIcon,
  TrashIcon,
  FunnelIcon,
  ExclamationTriangleIcon,
  InformationCircleIcon,
  XCircleIcon
} from '@heroicons/vue/24/outline'
import {
  Menu,
  MenuButton,
  MenuItem,
  MenuItems
} from '@headlessui/vue'
import type { NotificationType, NotificationPriority } from '@/types'

const notificationStore = useNotificationStore()
const toast = useToast()

const filterType = ref<NotificationType | 'all'>('all')
const filterPriority = ref<NotificationPriority | 'all'>('all')
const showUnreadOnly = ref(false)

const filteredNotifications = computed(() => {
  let notifications = notificationStore.sortedNotifications

  if (showUnreadOnly.value) {
    notifications = notifications.filter(n => !n.isRead)
  }

  if (filterType.value !== 'all') {
    notifications = notifications.filter(n => n.type === filterType.value)
  }

  if (filterPriority.value !== 'all') {
    notifications = notifications.filter(n => n.priority === filterPriority.value)
  }

  return notifications
})

const typeOptions: { value: NotificationType | 'all'; label: string }[] = [
  { value: 'all', label: 'All Types' },
  { value: 'QueryCompleted', label: 'Query Completed' },
  { value: 'DocumentProcessed', label: 'Document Processed' },
  { value: 'SyncCompleted', label: 'Sync Completed' },
  { value: 'SystemAlert', label: 'System Alert' },
  { value: 'Welcome', label: 'Welcome' }
]

const priorityOptions: { value: NotificationPriority | 'all'; label: string }[] = [
  { value: 'all', label: 'All Priorities' },
  { value: 'Urgent', label: 'Urgent' },
  { value: 'High', label: 'High' },
  { value: 'Normal', label: 'Normal' },
  { value: 'Low', label: 'Low' }
]

onMounted(async () => {
  try {
    await notificationStore.connect()
    await notificationStore.fetchNotifications()
    await notificationStore.fetchStats()
  } catch (error) {
    toast.error('Failed to load notifications', 'Please try refreshing the page')
  }
})

onUnmounted(() => {
  notificationStore.disconnect()
})

async function handleMarkAllAsRead() {
  try {
    await notificationStore.markAllAsRead()
    toast.success('All caught up!', 'All notifications marked as read')
  } catch (error) {
    toast.apiError(error, 'Failed to mark notifications as read')
  }
}

async function handleMarkAsRead(id: string) {
  try {
    await notificationStore.markAsRead(id)
  } catch (error) {
    toast.apiError(error, 'Failed to mark as read')
  }
}

async function handleDelete(id: string) {
  try {
    await notificationStore.deleteNotification(id)
    toast.success('Deleted', 'Notification removed')
  } catch (error) {
    toast.apiError(error, 'Failed to delete notification')
  }
}

function getPriorityIcon(priority: NotificationPriority) {
  switch (priority) {
    case 'Urgent':
      return XCircleIcon
    case 'High':
      return ExclamationTriangleIcon
    case 'Normal':
      return InformationCircleIcon
    case 'Low':
      return CheckCircleIcon
    default:
      return InformationCircleIcon
  }
}

function getPriorityColor(priority: NotificationPriority) {
  switch (priority) {
    case 'Urgent':
      return 'text-red-500'
    case 'High':
      return 'text-orange-500'
    case 'Normal':
      return 'text-blue-500'
    case 'Low':
      return 'text-gray-400'
    default:
      return 'text-gray-400'
  }
}

function formatDate(dateString: string) {
  const date = new Date(dateString)
  const now = new Date()
  const diff = now.getTime() - date.getTime()

  // Less than 1 minute
  if (diff < 60000) {
    return 'Just now'
  }

  // Less than 1 hour
  if (diff < 3600000) {
    const minutes = Math.floor(diff / 60000)
    return `${minutes}m ago`
  }

  // Less than 24 hours
  if (diff < 86400000) {
    const hours = Math.floor(diff / 3600000)
    return `${hours}h ago`
  }

  // Less than 7 days
  if (diff < 604800000) {
    const days = Math.floor(diff / 86400000)
    return `${days}d ago`
  }

  // Default to date
  return date.toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric'
  })
}
</script>

<template>
  <div class="h-full flex flex-col">
    <!-- Header -->
    <div class="border-b border-gray-200 bg-white px-6 py-4 dark:border-gray-700 dark:bg-gray-800">
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-3">
          <BellIcon class="h-6 w-6 text-gray-500" />
          <div>
            <h1 class="text-xl font-bold text-gray-900 dark:text-white">
              Notifications
            </h1>
            <p class="text-sm text-gray-500 dark:text-gray-400">
              {{ notificationStore.unreadCount }} unread
            </p>
          </div>
        </div>

        <div class="flex items-center gap-2">
          <button
            v-if="notificationStore.unreadCount > 0"
            class="btn-ghost inline-flex items-center gap-2"
            @click="handleMarkAllAsRead"
          >
            <CheckIcon class="h-4 w-4" />
            Mark all as read
          </button>
        </div>
      </div>

      <!-- Filters -->
      <div class="mt-4 flex flex-wrap items-center gap-4">
        <label class="inline-flex items-center">
          <input
            v-model="showUnreadOnly"
            type="checkbox"
            class="rounded border-gray-300 text-aegis-600 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700"
          />
          <span class="ml-2 text-sm text-gray-600 dark:text-gray-400">Unread only</span>
        </label>

        <Menu as="div" class="relative">
          <MenuButton class="btn-ghost inline-flex items-center gap-2">
            <FunnelIcon class="h-4 w-4" />
            {{ typeOptions.find(t => t.value === filterType)?.label }}
          </MenuButton>
          <MenuItems
            class="absolute left-0 z-10 mt-2 w-48 origin-top-left rounded-lg bg-white py-1 shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none dark:bg-gray-800 dark:ring-gray-700"
          >
            <MenuItem
              v-for="option in typeOptions"
              :key="option.value"
              v-slot="{ active }"
            >
              <button
                class="flex w-full items-center px-4 py-2 text-sm"
                :class="[
                  active ? 'bg-gray-100 dark:bg-gray-700' : '',
                  filterType === option.value ? 'text-aegis-600 dark:text-aegis-400' : ''
                ]"
                @click="filterType = option.value"
              >
                {{ option.label }}
              </button>
            </MenuItem>
          </MenuItems>
        </Menu>

        <Menu as="div" class="relative">
          <MenuButton class="btn-ghost inline-flex items-center gap-2">
            <FunnelIcon class="h-4 w-4" />
            {{ priorityOptions.find(p => p.value === filterPriority)?.label }}
          </MenuButton>
          <MenuItems
            class="absolute left-0 z-10 mt-2 w-40 origin-top-left rounded-lg bg-white py-1 shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none dark:bg-gray-800 dark:ring-gray-700"
          >
            <MenuItem
              v-for="option in priorityOptions"
              :key="option.value"
              v-slot="{ active }"
            >
              <button
                class="flex w-full items-center px-4 py-2 text-sm"
                :class="[
                  active ? 'bg-gray-100 dark:bg-gray-700' : '',
                  filterPriority === option.value ? 'text-aegis-600 dark:text-aegis-400' : ''
                ]"
                @click="filterPriority = option.value"
              >
                {{ option.label }}
              </button>
            </MenuItem>
          </MenuItems>
        </Menu>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="notificationStore.isLoading" class="flex-1 flex items-center justify-center">
      <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-aegis-600"></div>
    </div>

    <!-- Empty state -->
    <div
      v-else-if="filteredNotifications.length === 0"
      class="flex-1 flex flex-col items-center justify-center py-12"
    >
      <BellIcon class="h-12 w-12 text-gray-400" />
      <h3 class="mt-4 text-lg font-medium text-gray-900 dark:text-white">
        No notifications
      </h3>
      <p class="mt-2 text-sm text-gray-500 dark:text-gray-400">
        {{ showUnreadOnly ? 'All caught up!' : 'You don\'t have any notifications yet' }}
      </p>
    </div>

    <!-- Notifications list -->
    <div v-else class="flex-1 overflow-auto">
      <div class="divide-y divide-gray-200 dark:divide-gray-700">
        <div
          v-for="notification in filteredNotifications"
          :key="notification.id"
          class="flex gap-4 px-6 py-4 transition-colors"
          :class="notification.isRead
            ? 'bg-white dark:bg-gray-800'
            : 'bg-aegis-50 dark:bg-aegis-900/20'"
        >
          <div class="flex-shrink-0">
            <component
              :is="getPriorityIcon(notification.priority)"
              class="h-6 w-6"
              :class="getPriorityColor(notification.priority)"
            />
          </div>

          <div class="flex-1 min-w-0">
            <div class="flex items-start justify-between gap-4">
              <div>
                <p class="font-medium text-gray-900 dark:text-white">
                  {{ notification.title }}
                </p>
                <p class="mt-1 text-sm text-gray-600 dark:text-gray-400">
                  {{ notification.message }}
                </p>
                <p class="mt-1 text-xs text-gray-500 dark:text-gray-500">
                  {{ formatDate(notification.createdAt) }}
                </p>
              </div>

              <div class="flex items-center gap-2 flex-shrink-0">
                <button
                  v-if="!notification.isRead"
                  class="btn-ghost p-1"
                  title="Mark as read"
                  @click="handleMarkAsRead(notification.id)"
                >
                  <CheckIcon class="h-4 w-4" />
                </button>
                <button
                  class="btn-ghost p-1 text-red-600 hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-900/30"
                  title="Delete"
                  @click="handleDelete(notification.id)"
                >
                  <TrashIcon class="h-4 w-4" />
                </button>
              </div>
            </div>

            <!-- Actions -->
            <div v-if="notification.actions?.length" class="mt-2 flex gap-2">
              <a
                v-for="action in notification.actions"
                :key="action.label"
                :href="action.url"
                class="text-sm font-medium text-aegis-600 hover:text-aegis-500 dark:text-aegis-400"
              >
                {{ action.label }}
              </a>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Stats footer -->
    <div
      v-if="notificationStore.stats"
      class="border-t border-gray-200 bg-gray-50 px-6 py-3 dark:border-gray-700 dark:bg-gray-900"
    >
      <div class="flex items-center justify-between text-sm text-gray-500 dark:text-gray-400">
        <span>Total: {{ notificationStore.stats.totalCount }}</span>
        <span>Unread: {{ notificationStore.stats.unreadCount }}</span>
      </div>
    </div>
  </div>
</template>
