<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useWorkspaceStore } from '@/stores/workspace'
import { useDocuments } from '@/composables/useDocuments'
import { useConnection } from '@/composables/useConnection'
import { useToast } from '@/composables/useToast'
import LivePresence from '@/components/connection/LivePresence.vue'
import DocumentManagerPanel from '@/components/documents/DocumentManagerPanel.vue'
import DocumentDetailsDrawer from '@/components/documents/DocumentDetailsDrawer.vue'
import DocumentUploadDialog from '@/components/documents/DocumentUploadDialog.vue'
import DataSourceConfigDialog from '@/components/datasources/DataSourceConfigDialog.vue'
import DataSourceDetailsDrawer from '@/components/datasources/DataSourceDetailsDrawer.vue'
import type { DocumentResponse } from '@/services/document.service'
import type { DataSource, DataSourceType, DataSourceConfig } from '@/types/workspace'
import {
  ArrowPathIcon,
  TrashIcon,
  PlusIcon,
  LinkIcon,
  Cog6ToothIcon,
  GlobeAltIcon,
  CircleStackIcon,
  CloudIcon,
  DocumentTextIcon,
  ServerIcon
} from '@heroicons/vue/24/outline'
import {
  TabGroup,
  TabList,
  Tab,
  TabPanels,
  TabPanel
} from '@headlessui/vue'

const route = useRoute()
const router = useRouter()
const workspaceStore = useWorkspaceStore()
const { joinResource, leaveResource } = useConnection()
const toast = useToast()

const workspaceId = computed(() => route.params.workspaceId as string)
const workspace = computed(() => workspaceStore.currentWorkspace)

const isUploadDialogOpen = ref(false)
const isDataSourceConfigOpen = ref(false)
const isDataSourceDetailsOpen = ref(false)
const isDetailsDrawerOpen = ref(false)
const selectedDocument = ref<DocumentResponse | null>(null)
const selectedDataSource = ref<DataSource | null>(null)
const editingDataSource = ref<DataSource | null>(null)

// Use documents composable for document operations
const documentsComposable = computed(() => {
  if (workspaceId.value) {
    return useDocuments(workspaceId.value, {
      onUploadComplete: () => {
        // Refresh after upload
      },
      onUploadError: (fileName, error) => {
        toast.error('Upload failed', `Failed to upload ${fileName}: ${error}`)
      }
    })
  }
  return null
})

// Helper to get data source type icon
function getDataSourceIcon(type: DataSourceType) {
  switch (type) {
    case 'WebCrawler': return GlobeAltIcon
    case 'Database': return CircleStackIcon
    case 'SharePoint':
    case 'GoogleDrive':
    case 'AzureBlob': return CloudIcon
    case 'Confluence':
    case 'Notion': return DocumentTextIcon
    case 'S3': return ServerIcon
    default: return LinkIcon
  }
}

// Computed states from documents composable
const documents = computed(() => [...(documentsComposable.value?.documents.value ?? [])])

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
      documentsComposable.value?.fetchDocuments()
    ])
  }
}

// Document management handlers
function handleViewDetails(doc: DocumentResponse) {
  selectedDocument.value = doc
  isDetailsDrawerOpen.value = true
}

async function handleUpload(files: File[]) {
  if (!documentsComposable.value) return
  await documentsComposable.value.uploadMultiple(files)
  await documentsComposable.value.fetchDocuments()
}

async function handleReindex(_documentId: string) {
  toast.success('Reindexing started', 'The document is being reprocessed')
  // TODO: Call reindex API when available
}

async function handleDownload(_documentId: string) {
  toast.success('Download started', 'Your download will begin shortly')
  // TODO: Call download API when available
}

// Data source management handlers
function handleAddDataSource() {
  editingDataSource.value = null
  isDataSourceConfigOpen.value = true
}

function handleViewDataSource(ds: DataSource) {
  selectedDataSource.value = ds
  isDataSourceDetailsOpen.value = true
}

function handleEditDataSource(dataSourceId: string) {
  const ds = workspaceStore.dataSources.find(d => d.id === dataSourceId)
  if (ds) {
    editingDataSource.value = ds
    isDataSourceDetailsOpen.value = false
    isDataSourceConfigOpen.value = true
  }
}

async function handleSaveDataSource(data: { name: string; type: DataSourceType; config: DataSourceConfig }) {
  try {
    if (editingDataSource.value) {
      // Update existing data source
      await workspaceStore.updateDataSource(workspaceId.value, editingDataSource.value.id, {
        name: data.name,
        config: data.config
      })
      toast.success('Data source updated', `${data.name} has been updated`)
    } else {
      // Create new data source
      await workspaceStore.createDataSource({
        workspaceId: workspaceId.value,
        name: data.name,
        type: data.type,
        config: data.config
      })
      toast.success('Data source created', `${data.name} has been added`)
    }
    isDataSourceConfigOpen.value = false
    editingDataSource.value = null
  } catch (error) {
    toast.error('Error', 'Failed to save data source')
  }
}

async function syncDataSource(dataSourceId: string) {
  await workspaceStore.syncDataSource(workspaceId.value, dataSourceId)
}

async function deleteDataSource(dataSourceId: string) {
  if (confirm('Are you sure you want to delete this data source? All synced documents will remain in the workspace.')) {
    try {
      await workspaceStore.deleteDataSource(workspaceId.value, dataSourceId)
      toast.success('Data source deleted', 'The data source has been removed')
      // Close drawer if the deleted data source was being viewed
      if (selectedDataSource.value?.id === dataSourceId) {
        isDataSourceDetailsOpen.value = false
        selectedDataSource.value = null
      }
    } catch (error) {
      toast.error('Error', 'Failed to delete data source')
    }
  }
}

async function toggleDataSource(_dataSourceId: string, _enabled: boolean) {
  // TODO: Implement toggle when API supports it
  toast.info('Coming soon', 'Data source enable/disable will be available soon')
}

async function deleteDocument(documentId: string) {
  if (confirm('Are you sure you want to delete this document?')) {
    await documentsComposable.value?.deleteDocument(documentId)
    toast.success('Document deleted', 'The document has been removed')
    // Close drawer if the deleted document was being viewed
    if (selectedDocument.value?.id === documentId) {
      isDetailsDrawerOpen.value = false
      selectedDocument.value = null
    }
  }
}

async function handleBulkDelete(ids: string[]) {
  if (confirm(`Are you sure you want to delete ${ids.length} documents?`)) {
    for (const id of ids) {
      await documentsComposable.value?.deleteDocument(id)
    }
    toast.success('Documents deleted', `${ids.length} documents have been removed`)
    await documentsComposable.value?.fetchDocuments()
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
          <TabPanel class="h-full">
            <DocumentManagerPanel
              :documents="documents"
              :loading="workspaceStore.isLoading"
              @upload="isUploadDialogOpen = true"
              @delete="deleteDocument"
              @bulk-delete="handleBulkDelete"
              @reindex="handleReindex"
              @download="handleDownload"
              @view-details="handleViewDetails"
            />
          </TabPanel>

          <!-- Data Sources Tab -->
          <TabPanel class="p-6">
            <div class="mb-4 flex items-center justify-between">
              <h2 class="text-lg font-semibold text-gray-900 dark:text-white">
                Data Sources ({{ workspaceStore.dataSources.length }})
              </h2>
              <button
                class="btn-primary inline-flex items-center gap-2"
                @click="handleAddDataSource"
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
                @click="handleAddDataSource"
              >
                Add Data Source
              </button>
            </div>

            <!-- Data sources grid -->
            <div v-else class="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
              <div
                v-for="ds in workspaceStore.dataSources"
                :key="ds.id"
                class="group relative rounded-lg border border-gray-200 bg-white p-4 hover:border-aegis-300 hover:shadow-md transition-all cursor-pointer dark:border-gray-700 dark:bg-gray-800 dark:hover:border-aegis-600"
                @click="handleViewDataSource(ds)"
              >
                <!-- Status indicator -->
                <div class="absolute top-3 right-3">
                  <span
                    class="inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium"
                    :class="getStatusColor(ds.status)"
                  >
                    {{ ds.status }}
                  </span>
                </div>

                <!-- Icon and type -->
                <div class="flex items-center gap-3 mb-3">
                  <div class="rounded-lg bg-gray-100 p-2 dark:bg-gray-700">
                    <component :is="getDataSourceIcon(ds.type)" class="h-6 w-6 text-gray-600 dark:text-gray-400" />
                  </div>
                  <span class="text-sm text-gray-500 dark:text-gray-400">{{ ds.type }}</span>
                </div>

                <!-- Name and stats -->
                <h4 class="font-medium text-gray-900 dark:text-white mb-2">
                  {{ ds.name }}
                </h4>
                <div class="text-sm text-gray-500 dark:text-gray-400 space-y-1">
                  <p>{{ ds.documentCount }} documents</p>
                  <p v-if="ds.lastSyncAt">Last sync: {{ formatDate(ds.lastSyncAt) }}</p>
                  <p v-else class="text-yellow-600 dark:text-yellow-400">Never synced</p>
                </div>

                <!-- Quick actions -->
                <div class="mt-4 pt-3 border-t border-gray-100 dark:border-gray-700 flex items-center gap-2">
                  <button
                    class="btn-ghost p-2 flex-1 justify-center text-sm"
                    :disabled="ds.status === 'Syncing'"
                    @click.stop="syncDataSource(ds.id)"
                  >
                    <ArrowPathIcon
                      class="h-4 w-4 mr-1"
                      :class="ds.status === 'Syncing' ? 'animate-spin' : ''"
                    />
                    {{ ds.status === 'Syncing' ? 'Syncing...' : 'Sync' }}
                  </button>
                  <button
                    class="btn-ghost p-2"
                    @click.stop="handleEditDataSource(ds.id)"
                  >
                    <Cog6ToothIcon class="h-4 w-4" />
                  </button>
                  <button
                    class="btn-ghost p-2 text-red-600 hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-900/30"
                    @click.stop="deleteDataSource(ds.id)"
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
    <DocumentUploadDialog
      :open="isUploadDialogOpen"
      :max-files="20"
      :max-file-size="50 * 1024 * 1024"
      @close="isUploadDialogOpen = false"
      @upload="handleUpload"
    />

    <!-- Document Details Drawer -->
    <DocumentDetailsDrawer
      :document="selectedDocument"
      :open="isDetailsDrawerOpen"
      @close="isDetailsDrawerOpen = false"
      @delete="deleteDocument"
      @reindex="handleReindex"
      @download="handleDownload"
    />

    <!-- Data Source Config Dialog -->
    <DataSourceConfigDialog
      :open="isDataSourceConfigOpen"
      :workspace-id="workspaceId"
      :edit-mode="!!editingDataSource"
      :data-source="editingDataSource ? {
        id: editingDataSource.id,
        name: editingDataSource.name,
        type: editingDataSource.type,
        config: editingDataSource.config
      } : undefined"
      @close="isDataSourceConfigOpen = false; editingDataSource = null"
      @save="handleSaveDataSource"
    />

    <!-- Data Source Details Drawer -->
    <DataSourceDetailsDrawer
      :data-source="selectedDataSource"
      :open="isDataSourceDetailsOpen"
      @close="isDataSourceDetailsOpen = false; selectedDataSource = null"
      @sync="syncDataSource"
      @edit="handleEditDataSource"
      @delete="deleteDataSource"
      @toggle="toggleDataSource"
    />
  </div>
</template>
