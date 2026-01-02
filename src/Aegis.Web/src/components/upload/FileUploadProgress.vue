<script setup lang="ts">
import { computed } from 'vue'
import {
  DocumentIcon,
  DocumentTextIcon,
  PhotoIcon,
  FilmIcon,
  MusicalNoteIcon,
  ArchiveBoxIcon,
  CheckCircleIcon,
  ExclamationCircleIcon,
  XMarkIcon,
  ArrowPathIcon
} from '@heroicons/vue/24/outline'
import type { UploadFile, UploadStatus } from '@/types/upload'

interface Props {
  files: UploadFile[]
  showClear?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  showClear: true
})

const emit = defineEmits<{
  cancel: [fileId: string]
  retry: [fileId: string]
  remove: [fileId: string]
  clearCompleted: []
}>()

const hasCompletedFiles = computed(() =>
  props.files.some(f => f.status === 'completed' || f.status === 'error')
)

const overallProgress = computed(() => {
  if (props.files.length === 0) return 0
  const total = props.files.reduce((sum, f) => sum + f.progress, 0)
  return Math.round(total / props.files.length)
})

const uploadingCount = computed(() =>
  props.files.filter(f => f.status === 'uploading').length
)

const completedCount = computed(() =>
  props.files.filter(f => f.status === 'completed').length
)

const errorCount = computed(() =>
  props.files.filter(f => f.status === 'error').length
)

function getFileIcon(mimeType: string) {
  if (mimeType.startsWith('image/')) return PhotoIcon
  if (mimeType.startsWith('video/')) return FilmIcon
  if (mimeType.startsWith('audio/')) return MusicalNoteIcon
  if (mimeType.includes('pdf') || mimeType.includes('text')) return DocumentTextIcon
  if (mimeType.includes('zip') || mimeType.includes('archive') || mimeType.includes('compressed')) return ArchiveBoxIcon
  return DocumentIcon
}

function getStatusColor(status: UploadStatus): string {
  switch (status) {
    case 'pending':
      return 'text-gray-400'
    case 'uploading':
      return 'text-aegis-500'
    case 'processing':
      return 'text-yellow-500'
    case 'completed':
      return 'text-green-500'
    case 'error':
      return 'text-red-500'
    default:
      return 'text-gray-400'
  }
}

function getProgressBarColor(status: UploadStatus): string {
  switch (status) {
    case 'uploading':
      return 'bg-aegis-500'
    case 'processing':
      return 'bg-yellow-500'
    case 'completed':
      return 'bg-green-500'
    case 'error':
      return 'bg-red-500'
    default:
      return 'bg-gray-400'
  }
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

function getStatusLabel(status: UploadStatus): string {
  switch (status) {
    case 'pending':
      return 'Pending'
    case 'uploading':
      return 'Uploading'
    case 'processing':
      return 'Processing'
    case 'completed':
      return 'Completed'
    case 'error':
      return 'Failed'
    default:
      return status
  }
}
</script>

<template>
  <div v-if="files.length > 0" class="space-y-4">
    <!-- Overall progress header -->
    <div class="flex items-center justify-between">
      <div class="flex items-center gap-4 text-sm">
        <span v-if="uploadingCount > 0" class="text-aegis-600 dark:text-aegis-400">
          Uploading {{ uploadingCount }} file{{ uploadingCount > 1 ? 's' : '' }}...
        </span>
        <span v-if="completedCount > 0" class="text-green-600 dark:text-green-400">
          {{ completedCount }} completed
        </span>
        <span v-if="errorCount > 0" class="text-red-600 dark:text-red-400">
          {{ errorCount }} failed
        </span>
      </div>
      <button
        v-if="showClear && hasCompletedFiles"
        @click="emit('clearCompleted')"
        class="text-sm text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200"
      >
        Clear completed
      </button>
    </div>

    <!-- Overall progress bar -->
    <div v-if="uploadingCount > 0" class="relative">
      <div class="h-2 bg-gray-200 dark:bg-gray-700 rounded-full overflow-hidden">
        <div
          class="h-full bg-aegis-500 transition-all duration-300"
          :style="{ width: `${overallProgress}%` }"
        />
      </div>
      <span class="absolute right-0 -top-5 text-xs text-gray-500 dark:text-gray-400">
        {{ overallProgress }}%
      </span>
    </div>

    <!-- File list -->
    <div class="space-y-2 max-h-64 overflow-y-auto">
      <div
        v-for="file in files"
        :key="file.id"
        class="flex items-center gap-3 p-3 bg-gray-50 dark:bg-gray-800/50 rounded-lg"
      >
        <!-- File icon -->
        <div
          class="flex-shrink-0 p-2 rounded-lg"
          :class="[
            file.status === 'completed' ? 'bg-green-100 dark:bg-green-900/30' :
            file.status === 'error' ? 'bg-red-100 dark:bg-red-900/30' :
            'bg-gray-100 dark:bg-gray-700'
          ]"
        >
          <component
            :is="getFileIcon(file.type)"
            class="h-5 w-5"
            :class="getStatusColor(file.status)"
          />
        </div>

        <!-- File info -->
        <div class="flex-1 min-w-0">
          <div class="flex items-center gap-2">
            <p class="text-sm font-medium text-gray-900 dark:text-gray-100 truncate">
              {{ file.name }}
            </p>
            <span class="text-xs text-gray-500 dark:text-gray-400">
              {{ formatFileSize(file.size) }}
            </span>
          </div>

          <!-- Progress bar -->
          <div v-if="file.status === 'uploading' || file.status === 'processing'" class="mt-1">
            <div class="h-1.5 bg-gray-200 dark:bg-gray-600 rounded-full overflow-hidden">
              <div
                class="h-full transition-all duration-300"
                :class="getProgressBarColor(file.status)"
                :style="{ width: `${file.progress}%` }"
              />
            </div>
          </div>

          <!-- Status text -->
          <div class="flex items-center gap-2 mt-0.5">
            <span
              class="text-xs"
              :class="getStatusColor(file.status)"
            >
              {{ getStatusLabel(file.status) }}
              <span v-if="file.status === 'uploading'">{{ file.progress }}%</span>
            </span>
            <span v-if="file.error" class="text-xs text-red-500 truncate">
              - {{ file.error }}
            </span>
          </div>
        </div>

        <!-- Status icon / actions -->
        <div class="flex items-center gap-1">
          <!-- Completed -->
          <CheckCircleIcon
            v-if="file.status === 'completed'"
            class="h-5 w-5 text-green-500"
          />

          <!-- Error with retry -->
          <template v-else-if="file.status === 'error'">
            <ExclamationCircleIcon class="h-5 w-5 text-red-500" />
            <button
              @click="emit('retry', file.id)"
              class="p-1 rounded-md hover:bg-gray-200 dark:hover:bg-gray-600 text-gray-500 hover:text-gray-700 dark:text-gray-400"
              title="Retry"
            >
              <ArrowPathIcon class="h-4 w-4" />
            </button>
          </template>

          <!-- Uploading - can cancel -->
          <button
            v-else-if="file.status === 'uploading'"
            @click="emit('cancel', file.id)"
            class="p-1 rounded-md hover:bg-gray-200 dark:hover:bg-gray-600 text-gray-500 hover:text-red-500"
            title="Cancel"
          >
            <XMarkIcon class="h-4 w-4" />
          </button>

          <!-- Processing spinner -->
          <ArrowPathIcon
            v-else-if="file.status === 'processing'"
            class="h-5 w-5 text-yellow-500 animate-spin"
          />

          <!-- Remove button (always available except during upload) -->
          <button
            v-if="file.status !== 'uploading'"
            @click="emit('remove', file.id)"
            class="p-1 rounded-md hover:bg-gray-200 dark:hover:bg-gray-600 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300"
            title="Remove"
          >
            <XMarkIcon class="h-4 w-4" />
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
