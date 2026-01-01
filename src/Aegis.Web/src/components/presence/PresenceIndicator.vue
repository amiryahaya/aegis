<script setup lang="ts">
import { computed } from 'vue'
import { Popover, PopoverButton, PopoverPanel } from '@headlessui/vue'
import { UserCircleIcon, UsersIcon } from '@heroicons/vue/24/outline'
import type { PresenceResourceType } from '@/types/presence'
import { getStatusColor, getStatusText, getActivityText } from '@/types/presence'
import { usePresence } from '@/composables/usePresence'

const props = withDefaults(defineProps<{
  resourceId: string
  resourceType: PresenceResourceType
  showCount?: boolean
  showAvatars?: boolean
  maxAvatars?: number
}>(), {
  showCount: true,
  showAvatars: true,
  maxAvatars: 4
})

const { presenceList, onlineUsers, userCount, isLoading } = usePresence(props.resourceId, props.resourceType)

const displayedUsers = computed(() => onlineUsers.value.slice(0, props.maxAvatars))
const remainingCount = computed(() => Math.max(0, onlineUsers.value.length - props.maxAvatars))

function formatLastActive(dateString: string): string {
  const date = new Date(dateString)
  const now = new Date()
  const diffInSeconds = Math.floor((now.getTime() - date.getTime()) / 1000)

  if (diffInSeconds < 60) return 'just now'
  if (diffInSeconds < 3600) return `${Math.floor(diffInSeconds / 60)}m ago`
  return `${Math.floor(diffInSeconds / 3600)}h ago`
}
</script>

<template>
  <Popover v-if="!isLoading && userCount > 0" class="relative">
    <PopoverButton
      class="flex items-center gap-2 rounded-lg px-2 py-1 hover:bg-gray-100 dark:hover:bg-gray-700 focus:outline-none focus:ring-2 focus:ring-aegis-500"
    >
      <!-- Avatar Stack -->
      <div v-if="showAvatars" class="flex -space-x-2">
        <div
          v-for="user in displayedUsers"
          :key="user.userId"
          class="relative"
        >
          <img
            v-if="user.avatarUrl"
            :src="user.avatarUrl"
            :alt="user.displayName"
            class="h-6 w-6 rounded-full ring-2 ring-white dark:ring-gray-800"
          />
          <div
            v-else
            class="h-6 w-6 rounded-full ring-2 ring-white dark:ring-gray-800 flex items-center justify-center text-xs font-medium text-white"
            :style="{ backgroundColor: user.color }"
          >
            {{ user.displayName.charAt(0).toUpperCase() }}
          </div>
          <!-- Status Dot -->
          <span
            class="absolute bottom-0 right-0 block h-2 w-2 rounded-full ring-1 ring-white dark:ring-gray-800"
            :class="getStatusColor(user.status)"
          />
        </div>

        <!-- Remaining Count -->
        <div
          v-if="remainingCount > 0"
          class="h-6 w-6 rounded-full ring-2 ring-white dark:ring-gray-800 bg-gray-200 dark:bg-gray-600 flex items-center justify-center text-xs font-medium text-gray-600 dark:text-gray-300"
        >
          +{{ remainingCount }}
        </div>
      </div>

      <!-- Count Badge -->
      <span
        v-if="showCount"
        class="inline-flex items-center gap-1 text-xs text-gray-600 dark:text-gray-400"
      >
        <UsersIcon class="h-3.5 w-3.5" />
        {{ userCount }} {{ userCount === 1 ? 'viewer' : 'viewers' }}
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
        class="absolute right-0 z-10 mt-2 w-72 origin-top-right rounded-lg bg-white dark:bg-gray-800 shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none"
      >
        <div class="p-3">
          <div class="flex items-center justify-between mb-3">
            <h3 class="text-sm font-medium text-gray-900 dark:text-white">
              Who's viewing
            </h3>
            <span class="text-xs text-gray-500 dark:text-gray-400">
              {{ userCount }} {{ userCount === 1 ? 'person' : 'people' }}
            </span>
          </div>

          <div class="space-y-2 max-h-64 overflow-y-auto">
            <div
              v-for="user in presenceList"
              :key="user.userId"
              class="flex items-center gap-3 p-2 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700/50"
            >
              <!-- Avatar -->
              <div class="relative flex-shrink-0">
                <img
                  v-if="user.avatarUrl"
                  :src="user.avatarUrl"
                  :alt="user.displayName"
                  class="h-8 w-8 rounded-full"
                />
                <div
                  v-else
                  class="h-8 w-8 rounded-full flex items-center justify-center text-sm font-medium text-white"
                  :style="{ backgroundColor: user.color }"
                >
                  {{ user.displayName.charAt(0).toUpperCase() }}
                </div>
                <!-- Status Dot -->
                <span
                  class="absolute bottom-0 right-0 block h-2.5 w-2.5 rounded-full ring-2 ring-white dark:ring-gray-800"
                  :class="getStatusColor(user.status)"
                />
              </div>

              <!-- Info -->
              <div class="flex-1 min-w-0">
                <div class="flex items-center gap-2">
                  <p class="text-sm font-medium text-gray-900 dark:text-white truncate">
                    {{ user.displayName }}
                  </p>
                </div>
                <div class="flex items-center gap-2 text-xs text-gray-500 dark:text-gray-400">
                  <span>{{ getStatusText(user.status) }}</span>
                  <span class="text-gray-300 dark:text-gray-600">|</span>
                  <span>{{ getActivityText(user.activity) }}</span>
                </div>
                <p v-if="user.statusMessage" class="text-xs text-gray-400 dark:text-gray-500 truncate mt-0.5">
                  {{ user.statusMessage }}
                </p>
              </div>

              <!-- Last Active -->
              <div class="flex-shrink-0 text-xs text-gray-400 dark:text-gray-500">
                {{ formatLastActive(user.lastActiveAt) }}
              </div>
            </div>
          </div>
        </div>
      </PopoverPanel>
    </transition>
  </Popover>

  <!-- Empty State -->
  <div
    v-else-if="!isLoading && userCount === 0 && showCount"
    class="flex items-center gap-1 text-xs text-gray-400 dark:text-gray-500"
  >
    <UserCircleIcon class="h-4 w-4" />
    <span>Only you</span>
  </div>
</template>
