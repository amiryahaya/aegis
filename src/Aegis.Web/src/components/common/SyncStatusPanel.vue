<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import {
  CloudArrowUpIcon,
  SignalSlashIcon,
  ArrowPathIcon,
  TrashIcon,
  CheckCircleIcon,
  ExclamationCircleIcon
} from '@heroicons/vue/24/outline'
import { useOfflineQueue } from '@/composables/useOfflineQueue'
import { syncService } from '@/services/sync.service'

const {
  isOnline,
  isSyncing,
  pendingCount,
  lastSyncAt,
  syncError,
  syncPendingOperations,
  clearPendingOperations
} = useOfflineQueue()

const cacheStats = ref({
  sessions: 0,
  workspaces: 0,
  documents: 0,
  pendingOperations: 0
})
const isClearing = ref(false)
const showConfirmClear = ref(false)

const lastSyncFormatted = computed(() => {
  if (!lastSyncAt.value) return 'Never'

  const date = new Date(lastSyncAt.value)
  const now = new Date()
  const diffMs = now.getTime() - date.getTime()
  const diffMinutes = Math.floor(diffMs / 60000)

  if (diffMinutes < 1) return 'Just now'
  if (diffMinutes < 60) return `${diffMinutes} minute${diffMinutes > 1 ? 's' : ''} ago`

  const diffHours = Math.floor(diffMinutes / 60)
  if (diffHours < 24) return `${diffHours} hour${diffHours > 1 ? 's' : ''} ago`

  return date.toLocaleDateString()
})

const statusIcon = computed(() => {
  if (!isOnline.value) return SignalSlashIcon
  if (isSyncing.value) return ArrowPathIcon
  if (syncError.value) return ExclamationCircleIcon
  if (pendingCount.value > 0) return CloudArrowUpIcon
  return CheckCircleIcon
})

const statusColor = computed(() => {
  if (!isOnline.value) return 'text-gray-500'
  if (syncError.value) return 'text-red-500'
  if (isSyncing.value || pendingCount.value > 0) return 'text-yellow-500'
  return 'text-green-500'
})

const statusText = computed(() => {
  if (!isOnline.value) return 'Offline'
  if (isSyncing.value) return 'Syncing...'
  if (syncError.value) return 'Sync Error'
  if (pendingCount.value > 0) return 'Pending Changes'
  return 'Synced'
})

async function loadCacheStats() {
  cacheStats.value = await syncService.getCacheStats()
}

async function handleClearCache() {
  isClearing.value = true
  try {
    await syncService.clearAllCaches()
    await loadCacheStats()
    showConfirmClear.value = false
  } finally {
    isClearing.value = false
  }
}

async function handleClearPending() {
  await clearPendingOperations()
  await loadCacheStats()
}

onMounted(() => {
  loadCacheStats()
})
</script>

<template>
  <div class="rounded-lg border border-gray-200 bg-white p-4 dark:border-gray-700 dark:bg-gray-800">
    <h3 class="text-sm font-semibold text-gray-900 dark:text-white">
      Offline & Sync Status
    </h3>

    <!-- Status indicator -->
    <div class="mt-4 flex items-center gap-3">
      <div
        class="flex h-10 w-10 items-center justify-center rounded-full"
        :class="isOnline ? 'bg-green-100 dark:bg-green-900/30' : 'bg-gray-100 dark:bg-gray-700'"
      >
        <component
          :is="statusIcon"
          class="h-5 w-5"
          :class="[statusColor, { 'animate-spin': isSyncing }]"
        />
      </div>
      <div>
        <p class="font-medium text-gray-900 dark:text-white">
          {{ statusText }}
        </p>
        <p class="text-sm text-gray-500 dark:text-gray-400">
          Last sync: {{ lastSyncFormatted }}
        </p>
      </div>
    </div>

    <!-- Error message -->
    <div
      v-if="syncError"
      class="mt-3 rounded-lg bg-red-50 p-3 text-sm text-red-700 dark:bg-red-900/30 dark:text-red-300"
    >
      {{ syncError }}
    </div>

    <!-- Pending changes -->
    <div v-if="pendingCount > 0" class="mt-4">
      <div class="flex items-center justify-between">
        <span class="text-sm text-gray-600 dark:text-gray-400">
          {{ pendingCount }} pending change{{ pendingCount > 1 ? 's' : '' }}
        </span>
        <div class="flex gap-2">
          <button
            class="text-sm text-red-600 hover:text-red-700 dark:text-red-400"
            @click="handleClearPending"
          >
            Discard
          </button>
          <button
            v-if="isOnline"
            class="text-sm text-aegis-600 hover:text-aegis-700 dark:text-aegis-400"
            :disabled="isSyncing"
            @click="syncPendingOperations"
          >
            Sync Now
          </button>
        </div>
      </div>
    </div>

    <!-- Cache statistics -->
    <div class="mt-4 border-t border-gray-200 pt-4 dark:border-gray-700">
      <p class="text-sm font-medium text-gray-700 dark:text-gray-300">
        Cached Data
      </p>
      <div class="mt-2 grid grid-cols-2 gap-2 text-sm">
        <div class="flex items-center justify-between rounded-lg bg-gray-50 px-3 py-2 dark:bg-gray-700">
          <span class="text-gray-600 dark:text-gray-400">Sessions</span>
          <span class="font-medium text-gray-900 dark:text-white">
            {{ cacheStats.sessions }}
          </span>
        </div>
        <div class="flex items-center justify-between rounded-lg bg-gray-50 px-3 py-2 dark:bg-gray-700">
          <span class="text-gray-600 dark:text-gray-400">Workspaces</span>
          <span class="font-medium text-gray-900 dark:text-white">
            {{ cacheStats.workspaces }}
          </span>
        </div>
        <div class="flex items-center justify-between rounded-lg bg-gray-50 px-3 py-2 dark:bg-gray-700">
          <span class="text-gray-600 dark:text-gray-400">Documents</span>
          <span class="font-medium text-gray-900 dark:text-white">
            {{ cacheStats.documents }}
          </span>
        </div>
        <div class="flex items-center justify-between rounded-lg bg-gray-50 px-3 py-2 dark:bg-gray-700">
          <span class="text-gray-600 dark:text-gray-400">Pending</span>
          <span class="font-medium text-gray-900 dark:text-white">
            {{ cacheStats.pendingOperations }}
          </span>
        </div>
      </div>
    </div>

    <!-- Clear cache button -->
    <div class="mt-4">
      <template v-if="!showConfirmClear">
        <button
          class="flex w-full items-center justify-center gap-2 rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-600 dark:text-gray-300 dark:hover:bg-gray-700"
          @click="showConfirmClear = true"
        >
          <TrashIcon class="h-4 w-4" />
          Clear Cache
        </button>
      </template>
      <template v-else>
        <div class="rounded-lg bg-yellow-50 p-3 dark:bg-yellow-900/30">
          <p class="text-sm text-yellow-800 dark:text-yellow-200">
            This will clear all cached data. You'll need to be online to reload data.
          </p>
          <div class="mt-3 flex gap-2">
            <button
              class="flex-1 rounded-lg bg-yellow-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-yellow-700"
              :disabled="isClearing"
              @click="handleClearCache"
            >
              {{ isClearing ? 'Clearing...' : 'Confirm Clear' }}
            </button>
            <button
              class="flex-1 rounded-lg border border-gray-300 px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-600 dark:text-gray-300"
              @click="showConfirmClear = false"
            >
              Cancel
            </button>
          </div>
        </div>
      </template>
    </div>
  </div>
</template>
