<script setup lang="ts">
import { computed } from 'vue'
import {
  DocumentArrowDownIcon,
  TrashIcon,
  ClockIcon,
  CheckCircleIcon,
  ExclamationCircleIcon,
  ArrowPathIcon
} from '@heroicons/vue/24/outline'
import type { ExportHistoryItem } from '@/types/export'
import { formatFileSize, getExportFormatByValue } from '@/types/export'

const props = defineProps<{
  history: ExportHistoryItem[]
  maxItems?: number
}>()

const emit = defineEmits<{
  download: [item: ExportHistoryItem]
  delete: [id: string]
  clear: []
}>()

const displayedHistory = computed(() => {
  if (props.maxItems) {
    return props.history.slice(0, props.maxItems)
  }
  return props.history
})

function getStatusIcon(status: string) {
  switch (status) {
    case 'completed':
      return CheckCircleIcon
    case 'failed':
      return ExclamationCircleIcon
    case 'processing':
      return ArrowPathIcon
    default:
      return ClockIcon
  }
}

function getStatusColor(status: string) {
  switch (status) {
    case 'completed':
      return 'text-green-500'
    case 'failed':
      return 'text-red-500'
    case 'processing':
      return 'text-yellow-500 animate-spin'
    default:
      return 'text-gray-400'
  }
}

function formatDate(dateStr: string) {
  const date = new Date(dateStr)
  return date.toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

function isExpired(expiresAt: string) {
  return new Date(expiresAt) < new Date()
}
</script>

<template>
  <div class="space-y-4">
    <!-- Header -->
    <div class="flex items-center justify-between">
      <h3 class="text-sm font-medium text-gray-900 dark:text-white">
        Export History
      </h3>
      <button
        v-if="history.length > 0"
        type="button"
        class="text-sm text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200"
        @click="emit('clear')"
      >
        Clear All
      </button>
    </div>

    <!-- Empty state -->
    <div
      v-if="history.length === 0"
      class="rounded-lg border-2 border-dashed border-gray-200 p-6 text-center dark:border-gray-700"
    >
      <DocumentArrowDownIcon class="mx-auto h-10 w-10 text-gray-400" />
      <p class="mt-2 text-sm text-gray-500 dark:text-gray-400">
        No exports yet
      </p>
    </div>

    <!-- History list -->
    <ul v-else class="divide-y divide-gray-100 dark:divide-gray-700">
      <li
        v-for="item in displayedHistory"
        :key="item.id"
        class="flex items-center gap-3 py-3"
      >
        <!-- Status icon -->
        <component
          :is="getStatusIcon(item.status)"
          class="h-5 w-5 shrink-0"
          :class="getStatusColor(item.status)"
        />

        <!-- Info -->
        <div class="flex-1 min-w-0">
          <div class="flex items-center gap-2">
            <span class="truncate text-sm font-medium text-gray-900 dark:text-white">
              {{ item.fileName }}
            </span>
            <span
              class="inline-flex rounded-full px-2 py-0.5 text-xs font-medium"
              :class="[
                item.status === 'completed'
                  ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400'
                  : item.status === 'failed'
                    ? 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400'
                    : 'bg-gray-100 text-gray-700 dark:bg-gray-700 dark:text-gray-300'
              ]"
            >
              {{ getExportFormatByValue(item.format)?.label }}
            </span>
          </div>
          <div class="flex items-center gap-2 text-xs text-gray-500 dark:text-gray-400">
            <span>{{ formatDate(item.createdAt) }}</span>
            <span v-if="item.fileSize">&middot;</span>
            <span v-if="item.fileSize">{{ formatFileSize(item.fileSize) }}</span>
            <span v-if="item.downloadCount > 0">&middot;</span>
            <span v-if="item.downloadCount > 0">
              {{ item.downloadCount }} download{{ item.downloadCount !== 1 ? 's' : '' }}
            </span>
          </div>
        </div>

        <!-- Actions -->
        <div class="flex items-center gap-1">
          <button
            v-if="item.status === 'completed' && item.downloadUrl && !isExpired(item.expiresAt)"
            type="button"
            class="rounded-lg p-1.5 text-gray-400 hover:bg-gray-100 hover:text-aegis-600 dark:hover:bg-gray-700 dark:hover:text-aegis-400"
            title="Download"
            @click="emit('download', item)"
          >
            <DocumentArrowDownIcon class="h-4 w-4" />
          </button>
          <span
            v-else-if="item.status === 'completed' && isExpired(item.expiresAt)"
            class="text-xs text-gray-400"
          >
            Expired
          </span>
          <button
            type="button"
            class="rounded-lg p-1.5 text-gray-400 hover:bg-gray-100 hover:text-red-600 dark:hover:bg-gray-700 dark:hover:text-red-400"
            title="Delete"
            @click="emit('delete', item.id)"
          >
            <TrashIcon class="h-4 w-4" />
          </button>
        </div>
      </li>
    </ul>

    <!-- Show more -->
    <div
      v-if="maxItems && history.length > maxItems"
      class="text-center"
    >
      <span class="text-sm text-gray-500 dark:text-gray-400">
        +{{ history.length - maxItems }} more exports
      </span>
    </div>
  </div>
</template>
