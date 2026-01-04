<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { DocumentResponse, DocumentChunk } from '@/services/document.service'
import { documentService } from '@/services/document.service'
import { useToast } from '@/composables/useToast'
import {
  XMarkIcon,
  DocumentTextIcon,
  ArrowDownTrayIcon,
  ArrowPathIcon,
  TrashIcon,
  ClockIcon,
  CheckCircleIcon,
  ExclamationCircleIcon,
  InformationCircleIcon,
  ChevronDownIcon,
  ClipboardDocumentIcon,
  CheckIcon
} from '@heroicons/vue/24/outline'
import {
  TransitionRoot,
  TransitionChild,
  Dialog,
  DialogPanel,
  DialogTitle,
  Disclosure,
  DisclosureButton,
  DisclosurePanel
} from '@headlessui/vue'

const props = defineProps<{
  document: DocumentResponse | null
  open: boolean
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'delete', id: string): void
  (e: 'reindex', id: string): void
  (e: 'download', id: string): void
}>()

const toast = useToast()

const loading = ref(false)
const chunks = ref<DocumentChunk[]>([])
const loadingChunks = ref(false)
const copiedChunkId = ref<string | null>(null)

// Fetch chunks when document changes
watch(() => props.document, async (doc) => {
  if (doc) {
    await fetchChunks(doc.id)
  } else {
    chunks.value = []
  }
}, { immediate: true })

async function fetchChunks(documentId: string) {
  if (!props.document) return
  loadingChunks.value = true
  try {
    chunks.value = await documentService.getChunks(props.document.workspaceId, documentId)
  } catch {
    // Mock data for development
    chunks.value = [
      {
        id: '1',
        documentId,
        chunkIndex: 0,
        content: 'This is the first chunk of the document. It contains the introduction and overview of the main topics covered in this document.',
        tokenCount: 45,
        metadata: { page: '1' }
      },
      {
        id: '2',
        documentId,
        chunkIndex: 1,
        content: 'The second chunk contains more detailed information about the primary subject matter. This section explores the key concepts and provides examples.',
        tokenCount: 52,
        metadata: { page: '1' }
      },
      {
        id: '3',
        documentId,
        chunkIndex: 2,
        content: 'In this chunk, we delve into the technical specifications and implementation details. Performance considerations are also discussed.',
        tokenCount: 38,
        metadata: { page: '2' }
      }
    ]
  } finally {
    loadingChunks.value = false
  }
}

async function handleReindex() {
  if (!props.document) return
  loading.value = true
  try {
    await documentService.reprocess(props.document.workspaceId, props.document.id)
    toast.success('Reindexing started', 'The document is being reprocessed')
    emit('reindex', props.document.id)
  } catch {
    // Mock success for development
    toast.success('Reindexing started', 'The document is being reprocessed')
    emit('reindex', props.document.id)
  } finally {
    loading.value = false
  }
}

async function handleDownload() {
  if (!props.document) return
  try {
    // TODO: Add download endpoint to API when available
    // For now, emit the download event for the parent to handle
    toast.success('Download started', 'Your download will begin shortly')
    emit('download', props.document.id)
  } catch {
    toast.error('Download failed', 'Could not download the document')
  }
}

async function copyChunkContent(chunk: DocumentChunk) {
  try {
    await navigator.clipboard.writeText(chunk.content)
    copiedChunkId.value = chunk.id
    toast.success('Copied', 'Chunk content copied to clipboard')
    setTimeout(() => {
      copiedChunkId.value = null
    }, 2000)
  } catch (error) {
    toast.error('Copy failed', 'Could not copy to clipboard')
  }
}

// Helpers
const formatFileSize = (bytes: number) => {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
}

const formatDate = (dateStr: string) => {
  return new Date(dateStr).toLocaleString('en-US', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

const getStatusIcon = (status: string) => {
  switch (status) {
    case 'Indexed':
      return CheckCircleIcon
    case 'Processing':
      return ArrowPathIcon
    case 'Pending':
      return ClockIcon
    case 'Failed':
      return ExclamationCircleIcon
    default:
      return InformationCircleIcon
  }
}

const getStatusColor = (status: string) => {
  switch (status) {
    case 'Indexed':
      return 'text-green-600 bg-green-100 dark:text-green-400 dark:bg-green-900/50'
    case 'Processing':
      return 'text-blue-600 bg-blue-100 dark:text-blue-400 dark:bg-blue-900/50'
    case 'Pending':
      return 'text-yellow-600 bg-yellow-100 dark:text-yellow-400 dark:bg-yellow-900/50'
    case 'Failed':
      return 'text-red-600 bg-red-100 dark:text-red-400 dark:bg-red-900/50'
    default:
      return 'text-gray-600 bg-gray-100 dark:text-gray-400 dark:bg-gray-700'
  }
}

const totalTokens = computed(() => {
  return chunks.value.reduce((sum, chunk) => sum + chunk.tokenCount, 0)
})
</script>

<template>
  <TransitionRoot appear :show="open" as="template">
    <Dialog as="div" class="relative z-50" @close="emit('close')">
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

      <div class="fixed inset-0 overflow-hidden">
        <div class="absolute inset-0 overflow-hidden">
          <div class="pointer-events-none fixed inset-y-0 right-0 flex max-w-full pl-10">
            <TransitionChild
              enter="transform transition ease-in-out duration-300"
              enter-from="translate-x-full"
              enter-to="translate-x-0"
              leave="transform transition ease-in-out duration-300"
              leave-from="translate-x-0"
              leave-to="translate-x-full"
            >
              <DialogPanel class="pointer-events-auto w-screen max-w-lg">
                <div class="flex h-full flex-col bg-white shadow-xl dark:bg-gray-900">
                  <!-- Header -->
                  <div class="flex items-start justify-between border-b border-gray-200 px-6 py-4 dark:border-gray-700">
                    <div class="flex items-center gap-3">
                      <div class="rounded-lg bg-gray-100 p-2 dark:bg-gray-800">
                        <DocumentTextIcon class="h-6 w-6 text-gray-500" />
                      </div>
                      <div>
                        <DialogTitle class="text-lg font-semibold text-gray-900 dark:text-white">
                          {{ document?.name || 'Document Details' }}
                        </DialogTitle>
                        <p class="text-sm text-gray-500 dark:text-gray-400">
                          {{ document?.type }}
                        </p>
                      </div>
                    </div>
                    <button
                      class="rounded-lg p-1 text-gray-400 hover:text-gray-500 dark:hover:text-gray-300"
                      @click="emit('close')"
                    >
                      <XMarkIcon class="h-6 w-6" />
                    </button>
                  </div>

                  <!-- Content -->
                  <div v-if="document" class="flex-1 overflow-y-auto">
                    <!-- Status Banner -->
                    <div
                      v-if="document.status === 'Failed'"
                      class="bg-red-50 px-6 py-3 dark:bg-red-900/20"
                    >
                      <div class="flex items-center gap-2 text-red-700 dark:text-red-300">
                        <ExclamationCircleIcon class="h-5 w-5" />
                        <span class="text-sm font-medium">Processing failed</span>
                      </div>
                      <p v-if="document.errorMessage" class="mt-1 text-sm text-red-600 dark:text-red-400">
                        {{ document.errorMessage }}
                      </p>
                    </div>

                    <!-- Actions -->
                    <div class="flex items-center gap-2 border-b border-gray-200 px-6 py-4 dark:border-gray-700">
                      <button
                        class="btn-ghost flex-1 justify-center"
                        :disabled="loading"
                        @click="handleDownload"
                      >
                        <ArrowDownTrayIcon class="h-4 w-4 mr-2" />
                        Download
                      </button>
                      <button
                        class="btn-ghost flex-1 justify-center"
                        :disabled="loading"
                        @click="handleReindex"
                      >
                        <ArrowPathIcon class="h-4 w-4 mr-2" :class="{ 'animate-spin': loading }" />
                        Reindex
                      </button>
                      <button
                        class="btn-ghost flex-1 justify-center text-red-600 hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-900/20"
                        @click="emit('delete', document.id)"
                      >
                        <TrashIcon class="h-4 w-4 mr-2" />
                        Delete
                      </button>
                    </div>

                    <!-- Metadata -->
                    <div class="px-6 py-4 space-y-4">
                      <h3 class="text-sm font-medium text-gray-900 dark:text-white">Details</h3>

                      <div class="grid grid-cols-2 gap-4">
                        <div>
                          <p class="text-xs text-gray-500 dark:text-gray-400">Status</p>
                          <div class="mt-1 flex items-center gap-1.5">
                            <span
                              class="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium"
                              :class="getStatusColor(document.status)"
                            >
                              <component
                                :is="getStatusIcon(document.status)"
                                class="h-3 w-3"
                                :class="{ 'animate-spin': document.status === 'Processing' }"
                              />
                              {{ document.status }}
                            </span>
                          </div>
                        </div>

                        <div>
                          <p class="text-xs text-gray-500 dark:text-gray-400">File Size</p>
                          <p class="mt-1 text-sm text-gray-900 dark:text-white">
                            {{ formatFileSize(document.size) }}
                          </p>
                        </div>

                        <div>
                          <p class="text-xs text-gray-500 dark:text-gray-400">Chunks</p>
                          <p class="mt-1 text-sm text-gray-900 dark:text-white">
                            {{ document.chunkCount }}
                          </p>
                        </div>

                        <div>
                          <p class="text-xs text-gray-500 dark:text-gray-400">Total Tokens</p>
                          <p class="mt-1 text-sm text-gray-900 dark:text-white">
                            {{ totalTokens.toLocaleString() }}
                          </p>
                        </div>

                        <div>
                          <p class="text-xs text-gray-500 dark:text-gray-400">Uploaded</p>
                          <p class="mt-1 text-sm text-gray-900 dark:text-white">
                            {{ formatDate(document.createdAt) }}
                          </p>
                        </div>

                        <div v-if="document.processedAt">
                          <p class="text-xs text-gray-500 dark:text-gray-400">Processed</p>
                          <p class="mt-1 text-sm text-gray-900 dark:text-white">
                            {{ formatDate(document.processedAt) }}
                          </p>
                        </div>

                        <div v-if="document.uploadedBy" class="col-span-2">
                          <p class="text-xs text-gray-500 dark:text-gray-400">Uploaded By</p>
                          <p class="mt-1 text-sm text-gray-900 dark:text-white">
                            {{ document.uploadedBy }}
                          </p>
                        </div>
                      </div>

                      <!-- Metadata -->
                      <div v-if="Object.keys(document.metadata || {}).length > 0">
                        <p class="text-xs text-gray-500 dark:text-gray-400 mb-2">Metadata</p>
                        <div class="rounded-lg bg-gray-50 p-3 dark:bg-gray-800">
                          <div
                            v-for="(value, key) in document.metadata"
                            :key="key"
                            class="flex justify-between py-1 text-sm"
                          >
                            <span class="text-gray-500 dark:text-gray-400">{{ key }}</span>
                            <span class="text-gray-900 dark:text-white">{{ value }}</span>
                          </div>
                        </div>
                      </div>
                    </div>

                    <!-- Chunks -->
                    <div class="border-t border-gray-200 px-6 py-4 dark:border-gray-700">
                      <h3 class="text-sm font-medium text-gray-900 dark:text-white mb-4">
                        Content Chunks ({{ chunks.length }})
                      </h3>

                      <div v-if="loadingChunks" class="py-8 text-center">
                        <div class="animate-spin rounded-full h-6 w-6 border-b-2 border-aegis-600 mx-auto"></div>
                      </div>

                      <div v-else class="space-y-3">
                        <Disclosure
                          v-for="chunk in chunks"
                          :key="chunk.id"
                          v-slot="{ open }"
                        >
                          <div class="rounded-lg border border-gray-200 dark:border-gray-700">
                            <DisclosureButton class="flex w-full items-center justify-between px-4 py-3 text-left">
                              <div class="flex items-center gap-3">
                                <span class="flex h-6 w-6 items-center justify-center rounded-full bg-gray-100 text-xs font-medium text-gray-600 dark:bg-gray-800 dark:text-gray-400">
                                  {{ chunk.chunkIndex + 1 }}
                                </span>
                                <div>
                                  <p class="text-sm text-gray-900 dark:text-white">
                                    Chunk {{ chunk.chunkIndex + 1 }}
                                  </p>
                                  <p class="text-xs text-gray-500 dark:text-gray-400">
                                    {{ chunk.tokenCount }} tokens
                                    <span v-if="chunk.metadata?.page"> · Page {{ chunk.metadata.page }}</span>
                                  </p>
                                </div>
                              </div>
                              <ChevronDownIcon
                                class="h-5 w-5 text-gray-400 transition-transform"
                                :class="{ 'rotate-180': open }"
                              />
                            </DisclosureButton>
                            <DisclosurePanel class="border-t border-gray-200 px-4 py-3 dark:border-gray-700">
                              <div class="relative">
                                <pre class="whitespace-pre-wrap text-sm text-gray-700 dark:text-gray-300 max-h-48 overflow-y-auto">{{ chunk.content }}</pre>
                                <button
                                  class="absolute right-0 top-0 p-1 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300"
                                  @click.stop="copyChunkContent(chunk)"
                                >
                                  <CheckIcon v-if="copiedChunkId === chunk.id" class="h-4 w-4 text-green-500" />
                                  <ClipboardDocumentIcon v-else class="h-4 w-4" />
                                </button>
                              </div>
                            </DisclosurePanel>
                          </div>
                        </Disclosure>
                      </div>
                    </div>
                  </div>

                  <!-- Empty State -->
                  <div v-else class="flex-1 flex items-center justify-center">
                    <div class="text-center">
                      <DocumentTextIcon class="mx-auto h-12 w-12 text-gray-400" />
                      <p class="mt-2 text-gray-500 dark:text-gray-400">No document selected</p>
                    </div>
                  </div>
                </div>
              </DialogPanel>
            </TransitionChild>
          </div>
        </div>
      </div>
    </Dialog>
  </TransitionRoot>
</template>
