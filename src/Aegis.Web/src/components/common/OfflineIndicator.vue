<script setup lang="ts">
import { computed } from 'vue'
import {
  SignalSlashIcon,
  ArrowPathIcon,
  CloudArrowUpIcon,
  ExclamationTriangleIcon
} from '@heroicons/vue/24/outline'
import { useOfflineQueue } from '@/composables/useOfflineQueue'

const {
  isOnline,
  isSyncing,
  pendingCount,
  hasPendingChanges,
  syncError,
  syncPendingOperations
} = useOfflineQueue()

const statusMessage = computed(() => {
  if (!isOnline.value) {
    return 'You are offline'
  }
  if (isSyncing.value) {
    return 'Syncing changes...'
  }
  if (syncError.value) {
    return 'Sync failed'
  }
  if (hasPendingChanges.value) {
    return `${pendingCount.value} pending change${pendingCount.value > 1 ? 's' : ''}`
  }
  return null
})

const statusClass = computed(() => {
  if (!isOnline.value) {
    return 'bg-gray-700 text-white'
  }
  if (syncError.value) {
    return 'bg-red-600 text-white'
  }
  if (isSyncing.value || hasPendingChanges.value) {
    return 'bg-yellow-500 text-yellow-900'
  }
  return ''
})

function handleRetry() {
  syncPendingOperations()
}
</script>

<template>
  <transition
    enter-active-class="transition-all duration-300 ease-out"
    enter-from-class="opacity-0 -translate-y-full"
    enter-to-class="opacity-100 translate-y-0"
    leave-active-class="transition-all duration-200 ease-in"
    leave-from-class="opacity-100 translate-y-0"
    leave-to-class="opacity-0 -translate-y-full"
  >
    <div
      v-if="statusMessage"
      class="fixed top-0 left-0 right-0 z-[100] flex items-center justify-center gap-2 px-4 py-2 text-sm font-medium"
      :class="statusClass"
    >
      <!-- Icon -->
      <SignalSlashIcon v-if="!isOnline" class="h-4 w-4" />
      <ArrowPathIcon v-else-if="isSyncing" class="h-4 w-4 animate-spin" />
      <ExclamationTriangleIcon v-else-if="syncError" class="h-4 w-4" />
      <CloudArrowUpIcon v-else class="h-4 w-4" />

      <!-- Message -->
      <span>{{ statusMessage }}</span>

      <!-- Retry button -->
      <button
        v-if="syncError || (isOnline && hasPendingChanges && !isSyncing)"
        class="ml-2 rounded px-2 py-0.5 text-xs font-semibold hover:bg-white/20"
        @click="handleRetry"
      >
        Retry
      </button>
    </div>
  </transition>
</template>
