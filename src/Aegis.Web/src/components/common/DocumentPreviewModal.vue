<script setup lang="ts">
import { computed } from 'vue'
import { Dialog, DialogPanel, DialogTitle, TransitionChild, TransitionRoot } from '@headlessui/vue'
import {
  XMarkIcon,
  DocumentTextIcon,
  ArrowDownTrayIcon,
  ClockIcon,
  UserIcon,
  FolderIcon
} from '@heroicons/vue/24/outline'
import type { DocumentPreview } from '@/types/search'

const props = defineProps<{
  isOpen: boolean
  document: DocumentPreview | null
  isLoading?: boolean
}>()

const emit = defineEmits<{
  close: []
  download: [documentId: string]
}>()

const mimeTypeIcon = computed(() => {
  if (!props.document) return DocumentTextIcon
  const mimeType = props.document.mimeType
  // Could extend with more specific icons for different file types
  if (mimeType.includes('pdf')) return DocumentTextIcon
  if (mimeType.includes('word') || mimeType.includes('document')) return DocumentTextIcon
  if (mimeType.includes('spreadsheet') || mimeType.includes('excel')) return DocumentTextIcon
  return DocumentTextIcon
})

const mimeTypeLabel = computed(() => {
  if (!props.document) return 'Document'
  const mimeType = props.document.mimeType
  if (mimeType.includes('pdf')) return 'PDF Document'
  if (mimeType.includes('word') || mimeType.includes('msword')) return 'Word Document'
  if (mimeType.includes('spreadsheet') || mimeType.includes('excel')) return 'Spreadsheet'
  if (mimeType.includes('text/plain')) return 'Text File'
  if (mimeType.includes('markdown')) return 'Markdown'
  return 'Document'
})

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

function formatDate(dateString: string): string {
  return new Date(dateString).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

function handleDownload() {
  if (props.document) {
    emit('download', props.document.id)
  }
}
</script>

<template>
  <TransitionRoot as="template" :show="isOpen">
    <Dialog as="div" class="relative z-50" @close="emit('close')">
      <TransitionChild
        as="template"
        enter="ease-out duration-300"
        enter-from="opacity-0"
        enter-to="opacity-100"
        leave="ease-in duration-200"
        leave-from="opacity-100"
        leave-to="opacity-0"
      >
        <div class="fixed inset-0 bg-gray-500/75 dark:bg-gray-900/80 transition-opacity" />
      </TransitionChild>

      <div class="fixed inset-0 z-10 overflow-y-auto">
        <div class="flex min-h-full items-end justify-center p-4 text-center sm:items-center sm:p-0">
          <TransitionChild
            as="template"
            enter="ease-out duration-300"
            enter-from="opacity-0 translate-y-4 sm:translate-y-0 sm:scale-95"
            enter-to="opacity-100 translate-y-0 sm:scale-100"
            leave="ease-in duration-200"
            leave-from="opacity-100 translate-y-0 sm:scale-100"
            leave-to="opacity-0 translate-y-4 sm:translate-y-0 sm:scale-95"
          >
            <DialogPanel class="relative transform overflow-hidden rounded-lg bg-white dark:bg-gray-800 text-left shadow-xl transition-all sm:my-8 sm:w-full sm:max-w-4xl">
              <!-- Header -->
              <div class="flex items-center justify-between px-6 py-4 border-b border-gray-200 dark:border-gray-700">
                <div class="flex items-center gap-3">
                  <div class="p-2 bg-gray-100 dark:bg-gray-700 rounded-lg">
                    <component :is="mimeTypeIcon" class="h-6 w-6 text-gray-500 dark:text-gray-400" />
                  </div>
                  <div>
                    <DialogTitle as="h3" class="text-lg font-semibold text-gray-900 dark:text-gray-100">
                      {{ document?.name || 'Document Preview' }}
                    </DialogTitle>
                    <p class="text-sm text-gray-500 dark:text-gray-400">{{ mimeTypeLabel }}</p>
                  </div>
                </div>
                <div class="flex items-center gap-2">
                  <button
                    v-if="document"
                    @click="handleDownload"
                    class="btn-secondary flex items-center gap-2"
                  >
                    <ArrowDownTrayIcon class="h-4 w-4" />
                    Download
                  </button>
                  <button
                    @click="emit('close')"
                    class="btn-ghost p-2"
                  >
                    <XMarkIcon class="h-5 w-5" />
                  </button>
                </div>
              </div>

              <!-- Loading State -->
              <div v-if="isLoading" class="flex items-center justify-center py-12">
                <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-aegis-600"></div>
              </div>

              <!-- Content -->
              <div v-else-if="document" class="flex flex-col lg:flex-row">
                <!-- Main Content -->
                <div class="flex-1 p-6 overflow-auto max-h-[60vh]">
                  <div class="prose dark:prose-invert max-w-none">
                    <div v-if="document.chunks.length > 0">
                      <div
                        v-for="chunk in document.chunks"
                        :key="chunk.index"
                        class="mb-6 p-4 bg-gray-50 dark:bg-gray-700/50 rounded-lg"
                      >
                        <div v-if="chunk.pageNumber" class="text-xs text-gray-500 dark:text-gray-400 mb-2">
                          Page {{ chunk.pageNumber }}
                        </div>
                        <p class="text-gray-700 dark:text-gray-300 whitespace-pre-wrap">{{ chunk.content }}</p>
                      </div>
                    </div>
                    <div v-else-if="document.content">
                      <p class="text-gray-700 dark:text-gray-300 whitespace-pre-wrap">{{ document.content }}</p>
                    </div>
                    <div v-else class="text-center py-8">
                      <DocumentTextIcon class="h-12 w-12 text-gray-400 mx-auto mb-4" />
                      <p class="text-gray-500 dark:text-gray-400">No preview available for this document.</p>
                    </div>
                  </div>
                </div>

                <!-- Metadata Sidebar -->
                <div class="lg:w-64 flex-shrink-0 border-t lg:border-t-0 lg:border-l border-gray-200 dark:border-gray-700 p-6 bg-gray-50 dark:bg-gray-700/30">
                  <h4 class="text-sm font-semibold text-gray-900 dark:text-gray-100 mb-4">Document Info</h4>
                  <dl class="space-y-4">
                    <div>
                      <dt class="flex items-center gap-2 text-xs text-gray-500 dark:text-gray-400 mb-1">
                        <FolderIcon class="h-4 w-4" />
                        Workspace
                      </dt>
                      <dd class="text-sm text-gray-900 dark:text-gray-100">{{ document.metadata.workspaceName }}</dd>
                    </div>
                    <div>
                      <dt class="flex items-center gap-2 text-xs text-gray-500 dark:text-gray-400 mb-1">
                        <UserIcon class="h-4 w-4" />
                        Uploaded by
                      </dt>
                      <dd class="text-sm text-gray-900 dark:text-gray-100">{{ document.metadata.uploadedBy }}</dd>
                    </div>
                    <div>
                      <dt class="flex items-center gap-2 text-xs text-gray-500 dark:text-gray-400 mb-1">
                        <ClockIcon class="h-4 w-4" />
                        Uploaded
                      </dt>
                      <dd class="text-sm text-gray-900 dark:text-gray-100">{{ formatDate(document.metadata.uploadedAt) }}</dd>
                    </div>
                    <div>
                      <dt class="text-xs text-gray-500 dark:text-gray-400 mb-1">File Size</dt>
                      <dd class="text-sm text-gray-900 dark:text-gray-100">{{ formatFileSize(document.metadata.fileSize) }}</dd>
                    </div>
                    <div v-if="document.metadata.pageCount">
                      <dt class="text-xs text-gray-500 dark:text-gray-400 mb-1">Pages</dt>
                      <dd class="text-sm text-gray-900 dark:text-gray-100">{{ document.metadata.pageCount }}</dd>
                    </div>
                    <div v-if="document.metadata.wordCount">
                      <dt class="text-xs text-gray-500 dark:text-gray-400 mb-1">Words</dt>
                      <dd class="text-sm text-gray-900 dark:text-gray-100">{{ document.metadata.wordCount.toLocaleString() }}</dd>
                    </div>
                    <div v-if="document.metadata.language">
                      <dt class="text-xs text-gray-500 dark:text-gray-400 mb-1">Language</dt>
                      <dd class="text-sm text-gray-900 dark:text-gray-100">{{ document.metadata.language }}</dd>
                    </div>
                    <div>
                      <dt class="text-xs text-gray-500 dark:text-gray-400 mb-1">Chunks</dt>
                      <dd class="text-sm text-gray-900 dark:text-gray-100">{{ document.chunks.length }}</dd>
                    </div>
                  </dl>
                </div>
              </div>

              <!-- No Document -->
              <div v-else class="flex items-center justify-center py-12">
                <div class="text-center">
                  <DocumentTextIcon class="h-12 w-12 text-gray-400 mx-auto mb-4" />
                  <p class="text-gray-500 dark:text-gray-400">No document selected</p>
                </div>
              </div>
            </DialogPanel>
          </TransitionChild>
        </div>
      </div>
    </Dialog>
  </TransitionRoot>
</template>
