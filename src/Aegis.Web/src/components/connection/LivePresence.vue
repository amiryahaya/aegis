<script setup lang="ts">
import { computed, onMounted, onUnmounted } from 'vue'
import { Popover, PopoverButton, PopoverPanel } from '@headlessui/vue'
import { UsersIcon } from '@heroicons/vue/24/outline'
import { useConnection } from '@/composables/useConnection'
import type { LiveUser } from '@/types/connection'

interface Props {
  resourceType?: 'workspace' | 'session' | 'document'
  resourceId?: string
  maxAvatars?: number
  showCount?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  maxAvatars: 3,
  showCount: true
})

const {
  liveUsers,
  getUsersInResource,
  isConnected,
  joinResource,
  leaveResource
} = useConnection()

// Get users to display based on resource filter
const displayUsers = computed<LiveUser[]>(() => {
  if (props.resourceType && props.resourceId) {
    return getUsersInResource(props.resourceType, props.resourceId)
  }
  return liveUsers.value
})

// Users to show as avatars
const visibleUsers = computed(() =>
  displayUsers.value.slice(0, props.maxAvatars)
)

// Overflow count
const overflowCount = computed(() =>
  Math.max(0, displayUsers.value.length - props.maxAvatars)
)

// Status colors
const statusColors: Record<LiveUser['status'], string> = {
  active: 'bg-green-500',
  idle: 'bg-yellow-500',
  away: 'bg-gray-400'
}

// Get initials from name
function getInitials(name: string): string {
  return name
    .split(' ')
    .map(part => part[0])
    .slice(0, 2)
    .join('')
    .toUpperCase()
}

// Get time ago string
function getTimeAgo(date: Date): string {
  const now = new Date()
  const seconds = Math.floor((now.getTime() - new Date(date).getTime()) / 1000)

  if (seconds < 60) return 'Just now'
  if (seconds < 3600) return `${Math.floor(seconds / 60)}m ago`
  if (seconds < 86400) return `${Math.floor(seconds / 3600)}h ago`
  return `${Math.floor(seconds / 86400)}d ago`
}

// Join/leave resource on mount/unmount
onMounted(() => {
  if (props.resourceType && props.resourceId && isConnected.value) {
    joinResource(props.resourceType, props.resourceId)
  }
})

onUnmounted(() => {
  if (props.resourceType && props.resourceId && isConnected.value) {
    leaveResource(props.resourceType, props.resourceId)
  }
})
</script>

<template>
  <Popover v-if="displayUsers.length > 0" class="relative">
    <PopoverButton
      class="flex items-center gap-1 rounded-lg p-1 hover:bg-gray-100 focus:outline-none focus:ring-2 focus:ring-aegis-500 dark:hover:bg-gray-700"
    >
      <!-- Avatar stack -->
      <div class="flex -space-x-2">
        <div
          v-for="user in visibleUsers"
          :key="user.id"
          class="relative"
        >
          <!-- Avatar -->
          <div
            class="h-7 w-7 rounded-full border-2 border-white bg-gray-200 dark:border-gray-800 dark:bg-gray-600"
          >
            <img
              v-if="user.avatar"
              :src="user.avatar"
              :alt="user.name"
              class="h-full w-full rounded-full object-cover"
            />
            <span
              v-else
              class="flex h-full w-full items-center justify-center text-xs font-medium text-gray-600 dark:text-gray-300"
            >
              {{ getInitials(user.name) }}
            </span>
          </div>

          <!-- Status dot -->
          <span
            class="absolute -bottom-0.5 -right-0.5 h-2.5 w-2.5 rounded-full border-2 border-white dark:border-gray-800"
            :class="statusColors[user.status]"
          />
        </div>

        <!-- Overflow indicator -->
        <div
          v-if="overflowCount > 0"
          class="flex h-7 w-7 items-center justify-center rounded-full border-2 border-white bg-gray-100 text-xs font-medium text-gray-600 dark:border-gray-800 dark:bg-gray-700 dark:text-gray-300"
        >
          +{{ overflowCount }}
        </div>
      </div>

      <!-- Count label -->
      <span
        v-if="showCount && displayUsers.length > 0"
        class="text-xs text-gray-500 dark:text-gray-400"
      >
        {{ displayUsers.length }} online
      </span>
    </PopoverButton>

    <transition
      enter-active-class="transition duration-200 ease-out"
      enter-from-class="translate-y-1 opacity-0"
      enter-to-class="translate-y-0 opacity-100"
      leave-active-class="transition duration-150 ease-in"
      leave-from-class="translate-y-0 opacity-100"
      leave-to-class="translate-y-1 opacity-0"
    >
      <PopoverPanel
        class="absolute right-0 z-10 mt-2 w-72 rounded-lg bg-white shadow-lg ring-1 ring-black ring-opacity-5 dark:bg-gray-800 dark:ring-gray-700"
      >
        <div class="p-3">
          <div class="mb-2 flex items-center gap-2">
            <UsersIcon class="h-4 w-4 text-gray-500 dark:text-gray-400" />
            <h3 class="text-sm font-medium text-gray-900 dark:text-white">
              {{ displayUsers.length }} {{ displayUsers.length === 1 ? 'person' : 'people' }} online
            </h3>
          </div>

          <div class="max-h-64 space-y-1 overflow-y-auto">
            <div
              v-for="user in displayUsers"
              :key="user.id"
              class="flex items-center gap-3 rounded-lg p-2 hover:bg-gray-50 dark:hover:bg-gray-700/50"
            >
              <!-- Avatar -->
              <div class="relative flex-shrink-0">
                <div
                  class="h-9 w-9 rounded-full bg-gray-200 dark:bg-gray-600"
                >
                  <img
                    v-if="user.avatar"
                    :src="user.avatar"
                    :alt="user.name"
                    class="h-full w-full rounded-full object-cover"
                  />
                  <span
                    v-else
                    class="flex h-full w-full items-center justify-center text-sm font-medium text-gray-600 dark:text-gray-300"
                  >
                    {{ getInitials(user.name) }}
                  </span>
                </div>
                <span
                  class="absolute -bottom-0.5 -right-0.5 h-3 w-3 rounded-full border-2 border-white dark:border-gray-800"
                  :class="statusColors[user.status]"
                />
              </div>

              <!-- User info -->
              <div class="min-w-0 flex-1">
                <p class="truncate text-sm font-medium text-gray-900 dark:text-white">
                  {{ user.name }}
                </p>
                <div class="flex items-center gap-2 text-xs text-gray-500 dark:text-gray-400">
                  <span class="capitalize">{{ user.status }}</span>
                  <span>&middot;</span>
                  <span>{{ getTimeAgo(user.lastSeen) }}</span>
                </div>
              </div>

              <!-- Current location -->
              <div
                v-if="user.currentResource"
                class="flex-shrink-0 rounded-md bg-gray-100 px-2 py-0.5 text-xs text-gray-600 dark:bg-gray-700 dark:text-gray-400"
              >
                {{ user.currentResource.name || user.currentResource.type }}
              </div>
            </div>
          </div>
        </div>
      </PopoverPanel>
    </transition>
  </Popover>

  <!-- Empty state when no users -->
  <div
    v-else-if="isConnected"
    class="flex items-center gap-1 text-xs text-gray-400 dark:text-gray-500"
  >
    <UsersIcon class="h-4 w-4" />
    <span>No one else online</span>
  </div>
</template>
