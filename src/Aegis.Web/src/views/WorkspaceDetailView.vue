<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useWorkspaceStore } from '@/stores/workspace'
import { useFileUpload } from '@/composables/useFileUpload'
import { useConnection } from '@/composables/useConnection'
import LivePresence from '@/components/connection/LivePresence.vue'
import {
  DocumentTextIcon,
  CloudArrowUpIcon,
  ArrowPathIcon,
  TrashIcon,
  PlusIcon,
  FolderOpenIcon,
  LinkIcon,
  Cog6ToothIcon
} from '@heroicons/vue/24/outline'
import {
  Dialog,
  DialogPanel,
  DialogTitle,
  TransitionRoot,
  TransitionChild,
  TabGroup,
  TabList,
  Tab,
  TabPanels,
  TabPanel
} from '@headlessui/vue'
import type { DataSourceType } from '@/types/workspace'
import DragDropZone from '@/components/upload/DragDropZone.vue'
import FileUploadProgress from '@/components/upload/FileUploadProgress.vue'

const route = useRoute()
const router = useRouter()
const workspaceStore = useWorkspaceStore()
const { joinResource, leaveResource } = useConnection()

const workspaceId = computed(() => route.params.workspaceId as string)
const workspace = computed(() => workspaceStore.currentWorkspace)

const isUploadDialogOpen = ref(false)
const isAddDataSourceDialogOpen = ref(false)
const uploadError = ref<string | null>(null)

// File upload composable
const fileUpload = useFileUpload({
  url: `/api/workspaces/${workspaceId.value}/documents/upload`,
  autoUpload: false
})

const newDataSourceName = ref('')
const newDataSourceType = ref<DataSourceType>('WebCrawler')
const newDataSourceUrl = ref('')

const dataSourceTypes: { type: DataSourceType; label: string; icon: string }[] = [
  { type: 'WebCrawler', label: 'Web Crawler', icon: 'globe' },
  { type: 'SharePoint', label: 'SharePoint', icon: 'microsoft' },
  { type: 'GoogleDrive', label: 'Google Drive', icon: 'google' },
  { type: 'Confluence', label: 'Confluence', icon: 'atlassian' },
  { type: 'Notion', label: 'Notion', icon: 'notion' },
  { type: 'S3', label: 'Amazon S3', icon: 'aws' },
  { type: 'AzureBlob', label: 'Azure Blob', icon: 'azure' }
]

onMounted(async () => {
  await loadWorkspace()
  if (workspaceId.value) {
    joinResource('workspace', workspaceId.value)
  }
})

onUnmounted(() => {
  if (workspaceId.value) {
    leaveResource('workspace', workspaceId.value)
  }
})

watch(workspaceId, async (newId, oldId) => {
  if (oldId) {
    leaveResource('workspace', oldId)
  }
  await loadWorkspace()
  if (newId) {
    joinResource('workspace', newId)
  }
})

async function loadWorkspace() {
  if (workspaceId.value) {
    await Promise.all([
      workspaceStore.fetchWorkspace(workspaceId.value),
      workspaceStore.fetchDataSources(workspaceId.value),
      workspaceStore.fetchDocuments(workspaceId.value)
    ])
  }
}

function handleFilesSelected(files: File[]) {
  uploadError.value = null
  fileUpload.addFiles(files)
}

function handleUploadError(message: string) {
  uploadError.value = message
}

async function startUpload() {
  if (fileUpload.files.value.length === 0) return

  fileUpload.startAllUploads()
}

function closeUploadDialog() {
  isUploadDialogOpen.value = false
  fileUpload.clearAll()
  uploadError.value = null
}

// Watch for all uploads completed
watch(() => fileUpload.completedCount.value, (completed) => {
  if (completed > 0 && completed === fileUpload.files.value.length && !fileUpload.isUploading.value) {
    // Refresh documents list after all uploads complete
    workspaceStore.fetchDocuments(workspaceId.value)
  }
})

async function addDataSource() {
  if (!newDataSourceName.value.trim()) return

  await workspaceStore.createDataSource({
    workspaceId: workspaceId.value,
    name: newDataSourceName.value.trim(),
    type: newDataSourceType.value,
    config: {
      url: newDataSourceUrl.value.trim() || undefined
    }
  })

  isAddDataSourceDialogOpen.value = false
  newDataSourceName.value = ''
  newDataSourceType.value = 'WebCrawler'
  newDataSourceUrl.value = ''
}

async function syncDataSource(dataSourceId: string) {
  await workspaceStore.syncDataSource(workspaceId.value, dataSourceId)
}

async function deleteDataSource(dataSourceId: string) {
  if (confirm('Are you sure you want to delete this data source?')) {
    await workspaceStore.deleteDataSource(workspaceId.value, dataSourceId)
  }
}

async function deleteDocument(documentId: string) {
  if (confirm('Are you sure you want to delete this document?')) {
    await workspaceStore.deleteDocument(workspaceId.value, documentId)
  }
}

function getStatusColor(status: string) {
  switch (status) {
    case 'Active':
    case 'Indexed':
      return 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400'
    case 'Syncing':
    case 'Processing':
      return 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400'
    case 'Error':
    case 'Failed':
      return 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400'
    case 'Pending':
      return 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-400'
    default:
      return 'bg-gray-100 text-gray-800 dark:bg-gray-900/30 dark:text-gray-400'
  }
}

function formatBytes(bytes: number) {
  if (bytes === 0) return '0 Bytes'
  const k = 1024
  const sizes = ['Bytes', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
}

function formatDate(dateString: string) {
  return new Date(dateString).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}
</script>

<template>
  <div class="h-full flex flex-col">
    <!-- Loading -->
    <div v-if="workspaceStore.isLoading && !workspace" class="flex-1 flex items-center justify-center">
      <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-aegis-600"></div>
    </div>

    <template v-else-if="workspace">
      <!-- Header -->
      <div class="border-b border-gray-200 bg-white px-6 py-4 dark:border-gray-700 dark:bg-gray-800">
        <div class="flex items-center justify-between">
          <div>
            <div class="flex items-center gap-2">
              <button
                class="text-sm text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200"
                @click="router.push('/workspaces')"
              >
                Workspaces
              </button>
              <span class="text-gray-400">/</span>
              <h1 class="text-xl font-bold text-gray-900 dark:text-white">
                {{ workspace.name }}
              </h1>
            </div>
            <p v-if="workspace.description" class="mt-1 text-sm text-gray-500 dark:text-gray-400">
              {{ workspace.description }}
            </p>
          </div>

          <div class="flex items-center gap-3">
            <!-- Live presence for this workspace -->
            <LivePresence
              resource-type="workspace"
              :resource-id="workspaceId"
              :max-avatars="4"
            />

            <button
              class="btn-ghost inline-flex items-center gap-2"
              @click="router.push(`/workspaces/${workspaceId}/settings`)"
            >
              <Cog6ToothIcon class="h-5 w-5" />
              Settings
            </button>
            <button
              class="btn-primary inline-flex items-center gap-2"
              @click="router.push(`/chat?workspace=${workspaceId}`)"
            >
              Query Workspace
            </button>
          </div>
        </div>

        <!-- Stats -->
        <div class="mt-4 flex gap-6 text-sm">
          <div>
            <span class="text-gray-500 dark:text-gray-400">Documents:</span>
            <span class="ml-1 font-medium text-gray-900 dark:text-white">
              {{ workspace.stats?.documentCount || 0 }}
            </span>
          </div>
          <div>
            <span class="text-gray-500 dark:text-gray-400">Queries:</span>
            <span class="ml-1 font-medium text-gray-900 dark:text-white">
              {{ workspace.stats?.queryCount || 0 }}
            </span>
          </div>
          <div>
            <span class="text-gray-500 dark:text-gray-400">Tokens Used:</span>
            <span class="ml-1 font-medium text-gray-900 dark:text-white">
              {{ (workspace.stats?.totalTokensUsed || 0).toLocaleString() }}
            </span>
          </div>
        </div>
      </div>

      <!-- Tabs -->
      <TabGroup class="flex-1 flex flex-col overflow-hidden">
        <TabList class="flex border-b border-gray-200 bg-white px-6 dark:border-gray-700 dark:bg-gray-800">
          <Tab
            v-slot="{ selected }"
            as="template"
          >
            <button
              class="px-4 py-3 text-sm font-medium border-b-2 -mb-px transition-colors"
              :class="selected
                ? 'border-aegis-600 text-aegis-600 dark:text-aegis-400'
                : 'border-transparent text-gray-500 hover:text-gray-700 dark:text-gray-400'"
            >
              Documents
            </button>
          </Tab>
          <Tab
            v-slot="{ selected }"
            as="template"
          >
            <button
              class="px-4 py-3 text-sm font-medium border-b-2 -mb-px transition-colors"
              :class="selected
                ? 'border-aegis-600 text-aegis-600 dark:text-aegis-400'
                : 'border-transparent text-gray-500 hover:text-gray-700 dark:text-gray-400'"
            >
              Data Sources
            </button>
          </Tab>
        </TabList>

        <TabPanels class="flex-1 overflow-auto">
          <!-- Documents Tab -->
          <TabPanel class="p-6">
            <div class="mb-4 flex items-center justify-between">
              <h2 class="text-lg font-semibold text-gray-900 dark:text-white">
                Documents ({{ workspaceStore.documents.length }})
              </h2>
              <button
                class="btn-primary inline-flex items-center gap-2"
                @click="isUploadDialogOpen = true"
              >
                <CloudArrowUpIcon class="h-5 w-5" />
                Upload Documents
              </button>
            </div>

            <!-- Empty state -->
            <div
              v-if="workspaceStore.documents.length === 0"
              class="text-center py-12 border-2 border-dashed border-gray-300 rounded-lg dark:border-gray-600"
            >
              <FolderOpenIcon class="mx-auto h-12 w-12 text-gray-400" />
              <h3 class="mt-4 text-lg font-medium text-gray-900 dark:text-white">
                No documents yet
              </h3>
              <p class="mt-2 text-sm text-gray-500 dark:text-gray-400">
                Upload documents or add a data source to get started
              </p>
              <button
                class="btn-primary mt-4"
                @click="isUploadDialogOpen = true"
              >
                Upload Documents
              </button>
            </div>

            <!-- Documents list -->
            <div v-else class="space-y-2">
              <div
                v-for="doc in workspaceStore.documents"
                :key="doc.id"
                class="flex items-center justify-between rounded-lg border border-gray-200 bg-white p-4 dark:border-gray-700 dark:bg-gray-800"
              >
                <div class="flex items-center gap-3">
                  <DocumentTextIcon class="h-8 w-8 text-gray-400" />
                  <div>
                    <h4 class="font-medium text-gray-900 dark:text-white">
                      {{ doc.name }}
                    </h4>
                    <p class="text-sm text-gray-500 dark:text-gray-400">
                      {{ formatBytes(doc.size) }} · {{ doc.chunkCount }} chunks · {{ formatDate(doc.createdAt) }}
                    </p>
                  </div>
                </div>

                <div class="flex items-center gap-3">
                  <span
                    class="inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium"
                    :class="getStatusColor(doc.status)"
                  >
                    {{ doc.status }}
                  </span>
                  <button
                    class="btn-ghost p-2 text-red-600 hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-900/30"
                    @click="deleteDocument(doc.id)"
                  >
                    <TrashIcon class="h-4 w-4" />
                  </button>
                </div>
              </div>
            </div>
          </TabPanel>

          <!-- Data Sources Tab -->
          <TabPanel class="p-6">
            <div class="mb-4 flex items-center justify-between">
              <h2 class="text-lg font-semibold text-gray-900 dark:text-white">
                Data Sources ({{ workspaceStore.dataSources.length }})
              </h2>
              <button
                class="btn-primary inline-flex items-center gap-2"
                @click="isAddDataSourceDialogOpen = true"
              >
                <PlusIcon class="h-5 w-5" />
                Add Data Source
              </button>
            </div>

            <!-- Empty state -->
            <div
              v-if="workspaceStore.dataSources.length === 0"
              class="text-center py-12 border-2 border-dashed border-gray-300 rounded-lg dark:border-gray-600"
            >
              <LinkIcon class="mx-auto h-12 w-12 text-gray-400" />
              <h3 class="mt-4 text-lg font-medium text-gray-900 dark:text-white">
                No data sources
              </h3>
              <p class="mt-2 text-sm text-gray-500 dark:text-gray-400">
                Connect external sources to automatically sync documents
              </p>
              <button
                class="btn-primary mt-4"
                @click="isAddDataSourceDialogOpen = true"
              >
                Add Data Source
              </button>
            </div>

            <!-- Data sources list -->
            <div v-else class="space-y-2">
              <div
                v-for="ds in workspaceStore.dataSources"
                :key="ds.id"
                class="flex items-center justify-between rounded-lg border border-gray-200 bg-white p-4 dark:border-gray-700 dark:bg-gray-800"
              >
                <div class="flex items-center gap-3">
                  <div class="rounded-lg bg-gray-100 p-2 dark:bg-gray-700">
                    <LinkIcon class="h-6 w-6 text-gray-600 dark:text-gray-400" />
                  </div>
                  <div>
                    <h4 class="font-medium text-gray-900 dark:text-white">
                      {{ ds.name }}
                    </h4>
                    <p class="text-sm text-gray-500 dark:text-gray-400">
                      {{ ds.type }} · {{ ds.documentCount }} documents
                      <span v-if="ds.lastSyncAt"> · Last sync {{ formatDate(ds.lastSyncAt) }}</span>
                    </p>
                  </div>
                </div>

                <div class="flex items-center gap-2">
                  <span
                    class="inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium"
                    :class="getStatusColor(ds.status)"
                  >
                    {{ ds.status }}
                  </span>
                  <button
                    class="btn-ghost p-2"
                    :disabled="ds.status === 'Syncing'"
                    @click="syncDataSource(ds.id)"
                  >
                    <ArrowPathIcon
                      class="h-4 w-4"
                      :class="ds.status === 'Syncing' ? 'animate-spin' : ''"
                    />
                  </button>
                  <button
                    class="btn-ghost p-2 text-red-600 hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-900/30"
                    @click="deleteDataSource(ds.id)"
                  >
                    <TrashIcon class="h-4 w-4" />
                  </button>
                </div>
              </div>
            </div>
          </TabPanel>
        </TabPanels>
      </TabGroup>
    </template>

    <!-- Upload Dialog -->
    <TransitionRoot appear :show="isUploadDialogOpen" as="template">
      <Dialog as="div" class="relative z-50" @close="closeUploadDialog">
        <TransitionChild
          as="template"
          enter="ease-out duration-300"
          enter-from="opacity-0"
          enter-to="opacity-100"
          leave="ease-in duration-200"
          leave-from="opacity-100"
          leave-to="opacity-0"
        >
          <div class="fixed inset-0 bg-black bg-opacity-25 dark:bg-opacity-50" />
        </TransitionChild>

        <div class="fixed inset-0 overflow-y-auto">
          <div class="flex min-h-full items-center justify-center p-4">
            <TransitionChild
              as="template"
              enter="ease-out duration-300"
              enter-from="opacity-0 scale-95"
              enter-to="opacity-100 scale-100"
              leave="ease-in duration-200"
              leave-from="opacity-100 scale-100"
              leave-to="opacity-0 scale-95"
            >
              <DialogPanel class="w-full max-w-lg transform overflow-hidden rounded-2xl bg-white p-6 shadow-xl transition-all dark:bg-gray-800">
                <DialogTitle class="text-lg font-medium text-gray-900 dark:text-white">
                  Upload Documents
                </DialogTitle>

                <div class="mt-4 space-y-4">
                  <!-- Drag and Drop Zone -->
                  <DragDropZone
                    accept=".pdf,.docx,.doc,.txt,.md,.html,.json,.csv"
                    :max-size="50"
                    :max-files="20"
                    :disabled="fileUpload.isUploading.value"
                    @files-selected="handleFilesSelected"
                    @error="handleUploadError"
                  />

                  <!-- Error message -->
                  <div
                    v-if="uploadError"
                    class="p-3 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg text-sm text-red-700 dark:text-red-300"
                  >
                    {{ uploadError }}
                  </div>

                  <!-- File Upload Progress -->
                  <FileUploadProgress
                    :files="fileUpload.files.value"
                    @cancel="fileUpload.cancelUpload($event)"
                    @retry="fileUpload.retryUpload($event)"
                    @remove="fileUpload.removeFile($event)"
                    @clear-completed="fileUpload.clearCompleted()"
                  />
                </div>

                <div class="mt-6 flex justify-end gap-3">
                  <button
                    class="btn-ghost"
                    :disabled="fileUpload.isUploading.value"
                    @click="closeUploadDialog"
                  >
                    {{ fileUpload.completedCount.value > 0 ? 'Done' : 'Cancel' }}
                  </button>
                  <button
                    v-if="fileUpload.pendingCount.value > 0"
                    class="btn-primary"
                    :disabled="fileUpload.files.value.length === 0 || fileUpload.isUploading.value"
                    @click="startUpload"
                  >
                    {{ fileUpload.isUploading.value ? 'Uploading...' : `Upload ${fileUpload.pendingCount.value} file${fileUpload.pendingCount.value !== 1 ? 's' : ''}` }}
                  </button>
                </div>
              </DialogPanel>
            </TransitionChild>
          </div>
        </div>
      </Dialog>
    </TransitionRoot>

    <!-- Add Data Source Dialog -->
    <TransitionRoot appear :show="isAddDataSourceDialogOpen" as="template">
      <Dialog as="div" class="relative z-50" @close="isAddDataSourceDialogOpen = false">
        <TransitionChild
          as="template"
          enter="ease-out duration-300"
          enter-from="opacity-0"
          enter-to="opacity-100"
          leave="ease-in duration-200"
          leave-from="opacity-100"
          leave-to="opacity-0"
        >
          <div class="fixed inset-0 bg-black bg-opacity-25 dark:bg-opacity-50" />
        </TransitionChild>

        <div class="fixed inset-0 overflow-y-auto">
          <div class="flex min-h-full items-center justify-center p-4">
            <TransitionChild
              as="template"
              enter="ease-out duration-300"
              enter-from="opacity-0 scale-95"
              enter-to="opacity-100 scale-100"
              leave="ease-in duration-200"
              leave-from="opacity-100 scale-100"
              leave-to="opacity-0 scale-95"
            >
              <DialogPanel class="w-full max-w-md transform overflow-hidden rounded-2xl bg-white p-6 shadow-xl transition-all dark:bg-gray-800">
                <DialogTitle class="text-lg font-medium text-gray-900 dark:text-white">
                  Add Data Source
                </DialogTitle>

                <div class="mt-4 space-y-4">
                  <div>
                    <label class="label">Name</label>
                    <input
                      v-model="newDataSourceName"
                      type="text"
                      class="input w-full"
                      placeholder="My Data Source"
                    />
                  </div>

                  <div>
                    <label class="label">Type</label>
                    <select
                      v-model="newDataSourceType"
                      class="input w-full"
                    >
                      <option
                        v-for="dsType in dataSourceTypes"
                        :key="dsType.type"
                        :value="dsType.type"
                      >
                        {{ dsType.label }}
                      </option>
                    </select>
                  </div>

                  <div v-if="newDataSourceType === 'WebCrawler'">
                    <label class="label">URL</label>
                    <input
                      v-model="newDataSourceUrl"
                      type="url"
                      class="input w-full"
                      placeholder="https://example.com"
                    />
                  </div>
                </div>

                <div class="mt-6 flex justify-end gap-3">
                  <button
                    class="btn-ghost"
                    @click="isAddDataSourceDialogOpen = false"
                  >
                    Cancel
                  </button>
                  <button
                    class="btn-primary"
                    :disabled="!newDataSourceName.trim()"
                    @click="addDataSource"
                  >
                    Add
                  </button>
                </div>
              </DialogPanel>
            </TransitionChild>
          </div>
        </div>
      </Dialog>
    </TransitionRoot>
  </div>
</template>
