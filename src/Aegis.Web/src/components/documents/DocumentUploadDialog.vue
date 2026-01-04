<script setup lang="ts">
import { ref, computed } from 'vue'
import { useToast } from '@/composables/useToast'
import {
  XMarkIcon,
  CloudArrowUpIcon,
  DocumentTextIcon,
  TrashIcon,
  CheckCircleIcon,
  ExclamationCircleIcon,
  ArrowPathIcon
} from '@heroicons/vue/24/outline'
import {
  Dialog,
  DialogPanel,
  DialogTitle,
  TransitionChild,
  TransitionRoot
} from '@headlessui/vue'

interface UploadFile {
  id: string
  file: File
  progress: number
  status: 'pending' | 'uploading' | 'processing' | 'complete' | 'error'
  error?: string
}

const props = defineProps<{
  open: boolean
  maxFiles?: number
  maxFileSize?: number // in bytes
  acceptedTypes?: string[]
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'upload', files: File[]): void
}>()

const toast = useToast()

const isDragging = ref(false)
const files = ref<UploadFile[]>([])
const uploading = ref(false)

const maxFileSizeMB = computed(() => (props.maxFileSize || 50 * 1024 * 1024) / (1024 * 1024))

const acceptedTypesString = computed(() => {
  if (!props.acceptedTypes || props.acceptedTypes.length === 0) {
    return '.pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.md,.html,.json,.csv'
  }
  return props.acceptedTypes.join(',')
})

const acceptedTypesDisplay = computed(() => {
  return 'PDF, Word, Excel, PowerPoint, Text, Markdown, HTML, JSON, CSV'
})

// Drag and drop handlers
const handleDragEnter = (e: DragEvent) => {
  e.preventDefault()
  isDragging.value = true
}

const handleDragLeave = (e: DragEvent) => {
  e.preventDefault()
  isDragging.value = false
}

const handleDragOver = (e: DragEvent) => {
  e.preventDefault()
}

const handleDrop = (e: DragEvent) => {
  e.preventDefault()
  isDragging.value = false

  const droppedFiles = e.dataTransfer?.files
  if (droppedFiles) {
    addFiles(Array.from(droppedFiles))
  }
}

// File input handler
const handleFileInput = (e: Event) => {
  const input = e.target as HTMLInputElement
  if (input.files) {
    addFiles(Array.from(input.files))
    input.value = '' // Reset input
  }
}

// Add files to queue
const addFiles = (newFiles: File[]) => {
  const maxFiles = props.maxFiles || 20
  const maxFileSize = props.maxFileSize || 50 * 1024 * 1024

  for (const file of newFiles) {
    // Check max files limit
    if (files.value.length >= maxFiles) {
      toast.warning('Maximum files reached', `You can upload up to ${maxFiles} files at once`)
      break
    }

    // Check file size
    if (file.size > maxFileSize) {
      toast.error('File too large', `${file.name} exceeds the ${maxFileSizeMB.value}MB limit`)
      continue
    }

    // Check for duplicates
    if (files.value.some(f => f.file.name === file.name && f.file.size === file.size)) {
      toast.warning('Duplicate file', `${file.name} is already in the queue`)
      continue
    }

    files.value.push({
      id: `${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
      file,
      progress: 0,
      status: 'pending'
    })
  }
}

// Remove file from queue
const removeFile = (id: string) => {
  const index = files.value.findIndex(f => f.id === id)
  if (index !== -1) {
    files.value.splice(index, 1)
  }
}

// Clear all files
const clearFiles = () => {
  files.value = []
}

// Start upload
const startUpload = async () => {
  if (files.value.length === 0) return

  uploading.value = true
  const filesToUpload = files.value.filter(f => f.status === 'pending').map(f => f.file)

  // Mark all as uploading
  files.value.forEach(f => {
    if (f.status === 'pending') {
      f.status = 'uploading'
    }
  })

  // Simulate upload progress
  for (const uploadFile of files.value) {
    if (uploadFile.status !== 'uploading') continue

    // Simulate progress
    for (let i = 0; i <= 100; i += 10) {
      await new Promise(resolve => setTimeout(resolve, 100))
      uploadFile.progress = i
      if (i === 50) {
        uploadFile.status = 'processing'
      }
    }
    uploadFile.status = 'complete'
  }

  emit('upload', filesToUpload)
  uploading.value = false

  // Show success and close after delay
  toast.success('Upload complete', `${filesToUpload.length} file${filesToUpload.length !== 1 ? 's' : ''} uploaded successfully`)
  setTimeout(() => {
    files.value = []
    emit('close')
  }, 1500)
}

// Retry failed upload
const retryUpload = async (id: string) => {
  const file = files.value.find(f => f.id === id)
  if (!file || file.status !== 'error') return

  file.status = 'uploading'
  file.progress = 0
  file.error = undefined

  // Simulate retry
  for (let i = 0; i <= 100; i += 10) {
    await new Promise(resolve => setTimeout(resolve, 100))
    file.progress = i
    if (i === 50) {
      file.status = 'processing'
    }
  }
  file.status = 'complete'
}

// Helpers
const formatFileSize = (bytes: number) => {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i]
}

const getFileIcon = (_file: File) => {
  // Could be extended to return different icons based on file type
  return DocumentTextIcon
}

const getStatusIcon = (status: UploadFile['status']) => {
  switch (status) {
    case 'complete':
      return CheckCircleIcon
    case 'error':
      return ExclamationCircleIcon
    case 'uploading':
    case 'processing':
      return ArrowPathIcon
    default:
      return DocumentTextIcon
  }
}

const getStatusColor = (status: UploadFile['status']) => {
  switch (status) {
    case 'complete':
      return 'text-green-500'
    case 'error':
      return 'text-red-500'
    case 'uploading':
    case 'processing':
      return 'text-blue-500'
    default:
      return 'text-gray-400'
  }
}

const pendingCount = computed(() => files.value.filter(f => f.status === 'pending').length)
const completedCount = computed(() => files.value.filter(f => f.status === 'complete').length)
const errorCount = computed(() => files.value.filter(f => f.status === 'error').length)
</script>

<template>
  <TransitionRoot appear :show="open" as="template">
    <Dialog as="div" class="relative z-50" @close="!uploading && emit('close')">
      <TransitionChild
        enter="duration-300 ease-out"
        enter-from="opacity-0"
        enter-to="opacity-100"
        leave="duration-200 ease-in"
        leave-from="opacity-100"
        leave-to="opacity-0"
      >
        <div class="fixed inset-0 bg-black/30 backdrop-blur-sm" />
      </TransitionChild>

      <div class="fixed inset-0 overflow-y-auto">
        <div class="flex min-h-full items-center justify-center p-4">
          <TransitionChild
            enter="duration-300 ease-out"
            enter-from="opacity-0 scale-95"
            enter-to="opacity-100 scale-100"
            leave="duration-200 ease-in"
            leave-from="opacity-100 scale-100"
            leave-to="opacity-0 scale-95"
          >
            <DialogPanel class="w-full max-w-2xl transform rounded-xl bg-white p-6 shadow-xl transition-all dark:bg-gray-800">
              <div class="flex items-center justify-between mb-4">
                <DialogTitle class="text-lg font-semibold text-gray-900 dark:text-white">
                  Upload Documents
                </DialogTitle>
                <button
                  v-if="!uploading"
                  class="rounded-lg p-1 text-gray-400 hover:text-gray-500 dark:hover:text-gray-300"
                  @click="emit('close')"
                >
                  <XMarkIcon class="h-6 w-6" />
                </button>
              </div>

              <!-- Drop Zone -->
              <div
                class="relative rounded-lg border-2 border-dashed transition-colors"
                :class="isDragging
                  ? 'border-aegis-500 bg-aegis-50 dark:bg-aegis-900/20'
                  : 'border-gray-300 dark:border-gray-600'"
                @dragenter="handleDragEnter"
                @dragleave="handleDragLeave"
                @dragover="handleDragOver"
                @drop="handleDrop"
              >
                <input
                  type="file"
                  class="absolute inset-0 z-10 cursor-pointer opacity-0"
                  multiple
                  :accept="acceptedTypesString"
                  :disabled="uploading"
                  @change="handleFileInput"
                />
                <div class="p-8 text-center">
                  <CloudArrowUpIcon
                    class="mx-auto h-12 w-12 transition-colors"
                    :class="isDragging ? 'text-aegis-500' : 'text-gray-400'"
                  />
                  <p class="mt-4 text-sm font-medium text-gray-900 dark:text-white">
                    <span v-if="isDragging">Drop files here</span>
                    <span v-else>Drag and drop files, or click to browse</span>
                  </p>
                  <p class="mt-2 text-xs text-gray-500 dark:text-gray-400">
                    {{ acceptedTypesDisplay }}
                  </p>
                  <p class="mt-1 text-xs text-gray-500 dark:text-gray-400">
                    Maximum {{ maxFileSizeMB }}MB per file
                  </p>
                </div>
              </div>

              <!-- File Queue -->
              <div v-if="files.length > 0" class="mt-4">
                <div class="flex items-center justify-between mb-2">
                  <p class="text-sm font-medium text-gray-900 dark:text-white">
                    {{ files.length }} file{{ files.length !== 1 ? 's' : '' }} selected
                    <span v-if="completedCount > 0" class="text-green-500"> · {{ completedCount }} complete</span>
                    <span v-if="errorCount > 0" class="text-red-500"> · {{ errorCount }} failed</span>
                  </p>
                  <button
                    v-if="!uploading"
                    class="text-sm text-red-600 hover:text-red-700 dark:text-red-400"
                    @click="clearFiles"
                  >
                    Clear all
                  </button>
                </div>

                <div class="max-h-60 space-y-2 overflow-y-auto">
                  <div
                    v-for="uploadFile in files"
                    :key="uploadFile.id"
                    class="flex items-center gap-3 rounded-lg border border-gray-200 p-3 dark:border-gray-700"
                  >
                    <!-- File Icon -->
                    <div class="shrink-0">
                      <component
                        :is="getFileIcon(uploadFile.file)"
                        class="h-8 w-8 text-gray-400"
                      />
                    </div>

                    <!-- File Info -->
                    <div class="flex-1 min-w-0">
                      <p class="text-sm font-medium text-gray-900 dark:text-white truncate">
                        {{ uploadFile.file.name }}
                      </p>
                      <p class="text-xs text-gray-500 dark:text-gray-400">
                        {{ formatFileSize(uploadFile.file.size) }}
                      </p>

                      <!-- Progress Bar -->
                      <div
                        v-if="uploadFile.status === 'uploading' || uploadFile.status === 'processing'"
                        class="mt-2"
                      >
                        <div class="flex items-center justify-between mb-1">
                          <span class="text-xs text-gray-500 dark:text-gray-400">
                            {{ uploadFile.status === 'processing' ? 'Processing...' : 'Uploading...' }}
                          </span>
                          <span class="text-xs text-gray-500 dark:text-gray-400">
                            {{ uploadFile.progress }}%
                          </span>
                        </div>
                        <div class="h-1.5 bg-gray-200 rounded-full dark:bg-gray-700">
                          <div
                            class="h-1.5 bg-aegis-600 rounded-full transition-all duration-300"
                            :style="{ width: `${uploadFile.progress}%` }"
                          ></div>
                        </div>
                      </div>

                      <!-- Error Message -->
                      <p
                        v-if="uploadFile.error"
                        class="mt-1 text-xs text-red-500"
                      >
                        {{ uploadFile.error }}
                      </p>
                    </div>

                    <!-- Status / Actions -->
                    <div class="shrink-0 flex items-center gap-2">
                      <component
                        :is="getStatusIcon(uploadFile.status)"
                        class="h-5 w-5"
                        :class="[
                          getStatusColor(uploadFile.status),
                          { 'animate-spin': uploadFile.status === 'uploading' || uploadFile.status === 'processing' }
                        ]"
                      />

                      <button
                        v-if="uploadFile.status === 'error'"
                        class="p-1 text-gray-400 hover:text-aegis-600"
                        title="Retry"
                        @click="retryUpload(uploadFile.id)"
                      >
                        <ArrowPathIcon class="h-4 w-4" />
                      </button>

                      <button
                        v-if="uploadFile.status === 'pending' && !uploading"
                        class="p-1 text-gray-400 hover:text-red-600"
                        title="Remove"
                        @click="removeFile(uploadFile.id)"
                      >
                        <TrashIcon class="h-4 w-4" />
                      </button>
                    </div>
                  </div>
                </div>
              </div>

              <!-- Actions -->
              <div class="mt-6 flex justify-end gap-3">
                <button
                  v-if="!uploading"
                  class="btn-ghost"
                  @click="emit('close')"
                >
                  Cancel
                </button>
                <button
                  class="btn-primary"
                  :disabled="pendingCount === 0 || uploading"
                  @click="startUpload"
                >
                  <template v-if="uploading">
                    <ArrowPathIcon class="h-4 w-4 mr-2 animate-spin" />
                    Uploading...
                  </template>
                  <template v-else>
                    <CloudArrowUpIcon class="h-4 w-4 mr-2" />
                    Upload {{ pendingCount }} File{{ pendingCount !== 1 ? 's' : '' }}
                  </template>
                </button>
              </div>
            </DialogPanel>
          </TransitionChild>
        </div>
      </div>
    </Dialog>
  </TransitionRoot>
</template>
