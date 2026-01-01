<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useNotificationStore } from '@/stores/notification'
import { BellIcon, CheckIcon } from '@heroicons/vue/24/outline'
import { BellAlertIcon } from '@heroicons/vue/24/solid'
import {
  Popover,
  PopoverButton,
  PopoverPanel
} from '@headlessui/vue'

const router = useRouter()
const notificationStore = useNotificationStore()

const isOpen = ref(false)

const recentNotifications = computed(() =>
  notificationStore.sortedNotifications.slice(0, 5)
)

onMounted(async () => {
  await notificationStore.connect()
})

onUnmounted(() => {
  // Don't disconnect here as other components may need it
})

function formatDate(dateString: string) {
  const date = new Date(dateString)
  const now = new Date()
  const diff = now.getTime() - date.getTime()

  if (diff < 60000) return 'Just now'
  if (diff < 3600000) return `${Math.floor(diff / 60000)}m ago`
  if (diff < 86400000) return `${Math.floor(diff / 3600000)}h ago`
  return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
}

function viewAll() {
  isOpen.value = false
  router.push('/notifications')
}
</script>

<template>
  <Popover class="relative">
    <PopoverButton
      class="relative rounded-lg p-2 text-gray-500 hover:bg-gray-100 focus:outline-none focus:ring-2 focus:ring-aegis-500 dark:text-gray-400 dark:hover:bg-gray-700"
    >
      <component
        :is="notificationStore.hasUnread ? BellAlertIcon : BellIcon"
        class="h-6 w-6"
        :class="notificationStore.hasUnread ? 'text-aegis-600 dark:text-aegis-400' : ''"
      />
      <span
        v-if="notificationStore.unreadCount > 0"
        class="absolute -right-1 -top-1 flex h-5 w-5 items-center justify-center rounded-full bg-red-500 text-xs font-medium text-white"
      >
        {{ notificationStore.unreadCount > 9 ? '9+' : notificationStore.unreadCount }}
      </span>
    </PopoverButton>

    <transition
      enter-active-class="transition ease-out duration-200"
      enter-from-class="opacity-0 translate-y-1"
      enter-to-class="opacity-100 translate-y-0"
      leave-active-class="transition ease-in duration-150"
      leave-from-class="opacity-100 translate-y-0"
      leave-to-class="opacity-0 translate-y-1"
    >
      <PopoverPanel
        class="absolute right-0 z-50 mt-2 w-80 origin-top-right rounded-lg bg-white shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none dark:bg-gray-800 dark:ring-gray-700"
      >
        <div class="p-4">
          <div class="flex items-center justify-between">
            <h3 class="font-semibold text-gray-900 dark:text-white">
              Notifications
            </h3>
            <button
              v-if="notificationStore.unreadCount > 0"
              class="text-xs text-aegis-600 hover:text-aegis-500 dark:text-aegis-400"
              @click="notificationStore.markAllAsRead()"
            >
              Mark all read
            </button>
          </div>

          <div class="mt-4 space-y-3">
            <div
              v-if="recentNotifications.length === 0"
              class="py-4 text-center text-sm text-gray-500 dark:text-gray-400"
            >
              No notifications
            </div>

            <div
              v-for="notification in recentNotifications"
              :key="notification.id"
              class="flex gap-3 rounded-lg p-2 transition-colors"
              :class="notification.isRead
                ? 'hover:bg-gray-50 dark:hover:bg-gray-700'
                : 'bg-aegis-50 dark:bg-aegis-900/20'"
            >
              <div
                class="h-2 w-2 mt-2 flex-shrink-0 rounded-full"
                :class="notification.isRead ? 'bg-transparent' : 'bg-aegis-600'"
              />
              <div class="flex-1 min-w-0">
                <p class="text-sm font-medium text-gray-900 dark:text-white truncate">
                  {{ notification.title }}
                </p>
                <p class="text-xs text-gray-500 dark:text-gray-400 truncate">
                  {{ notification.message }}
                </p>
                <p class="mt-1 text-xs text-gray-400 dark:text-gray-500">
                  {{ formatDate(notification.createdAt) }}
                </p>
              </div>
              <button
                v-if="!notification.isRead"
                class="flex-shrink-0 p-1 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300"
                @click.stop="notificationStore.markAsRead(notification.id)"
              >
                <CheckIcon class="h-4 w-4" />
              </button>
            </div>
          </div>

          <div class="mt-4 pt-4 border-t border-gray-200 dark:border-gray-700">
            <button
              class="w-full text-center text-sm font-medium text-aegis-600 hover:text-aegis-500 dark:text-aegis-400"
              @click="viewAll"
            >
              View all notifications
            </button>
          </div>
        </div>
      </PopoverPanel>
    </transition>
  </Popover>

  <!-- Toast notification -->
  <Teleport to="body">
    <transition
      enter-active-class="transform ease-out duration-300 transition"
      enter-from-class="translate-y-2 opacity-0 sm:translate-y-0 sm:translate-x-2"
      enter-to-class="translate-y-0 opacity-100 sm:translate-x-0"
      leave-active-class="transition ease-in duration-100"
      leave-from-class="opacity-100"
      leave-to-class="opacity-0"
    >
      <div
        v-if="notificationStore.toastNotification"
        class="pointer-events-auto fixed bottom-4 right-4 z-50 w-full max-w-sm rounded-lg bg-white shadow-lg ring-1 ring-black ring-opacity-5 dark:bg-gray-800 dark:ring-gray-700"
      >
        <div class="p-4">
          <div class="flex items-start">
            <div class="flex-shrink-0">
              <BellIcon class="h-6 w-6 text-aegis-600 dark:text-aegis-400" />
            </div>
            <div class="ml-3 w-0 flex-1">
              <p class="text-sm font-medium text-gray-900 dark:text-white">
                {{ notificationStore.toastNotification.title }}
              </p>
              <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
                {{ notificationStore.toastNotification.message }}
              </p>
            </div>
            <div class="ml-4 flex flex-shrink-0">
              <button
                type="button"
                class="inline-flex rounded-md text-gray-400 hover:text-gray-500 focus:outline-none"
                @click="notificationStore.dismissToast()"
              >
                <span class="sr-only">Close</span>
                <svg class="h-5 w-5" viewBox="0 0 20 20" fill="currentColor">
                  <path fill-rule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clip-rule="evenodd" />
                </svg>
              </button>
            </div>
          </div>
        </div>
      </div>
    </transition>
  </Teleport>
</template>
