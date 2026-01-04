<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { DataSource, SyncHistory, SyncStats, DataSourceType } from '@/types/workspace'
import { useToast } from '@/composables/useToast'
import {
  XMarkIcon,
  ArrowPathIcon,
  TrashIcon,
  Cog6ToothIcon,
  CheckCircleIcon,
  ExclamationCircleIcon,
  ClockIcon,
  DocumentPlusIcon,
  DocumentMinusIcon,
  DocumentArrowUpIcon,
  GlobeAltIcon,
  CircleStackIcon,
  CloudIcon,
  DocumentTextIcon,
  ServerIcon
} from '@heroicons/vue/24/outline'
import {
  TransitionRoot,
  TransitionChild,
  Dialog,
  DialogPanel,
  DialogTitle,
  TabGroup,
  TabList,
  Tab,
  TabPanels,
  TabPanel,
  Switch
} from '@headlessui/vue'
import { workspaceService } from '@/services/workspace.service'

const props = defineProps<{
  dataSource: DataSource | null
  open: boolean
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'sync', id: string): void
  (e: 'edit', id: string): void
  (e: 'delete', id: string): void
  (e: 'toggle', id: string, enabled: boolean): void
}>()

const toast = useToast()

const loading = ref(false)
const syncHistory = ref<SyncHistory[]>([])
const syncStats = ref<SyncStats | null>(null)

// Mock sync history for development
watch(() => props.dataSource, async (ds) => {
  if (ds) {
    await fetchSyncHistory(ds.id)
  } else {
    syncHistory.value = []
    syncStats.value = null
  }
}, { immediate: true })

async function fetchSyncHistory(_dataSourceId: string) {
  loading.value = true
  try {
    // Mock data for development
    syncHistory.value = [
      {
        id: '1',
        dataSourceId: _dataSourceId,
        status: 'Completed',
        startedAt: new Date(Date.now() - 3600000).toISOString(),
        completedAt: new Date(Date.now() - 3500000).toISOString(),
        documentsAdded: 15,
        documentsUpdated: 3,
        documentsDeleted: 0,
        documentsSkipped: 2,
        errorCount: 0,
        durationMs: 100000,
        triggeredBy: 'Manual'
      },
      {
        id: '2',
        dataSourceId: _dataSourceId,
        status: 'Completed',
        startedAt: new Date(Date.now() - 86400000).toISOString(),
        completedAt: new Date(Date.now() - 86300000).toISOString(),
        documentsAdded: 42,
        documentsUpdated: 8,
        documentsDeleted: 1,
        documentsSkipped: 5,
        errorCount: 0,
        durationMs: 100000,
        triggeredBy: 'Scheduled'
      },
      {
        id: '3',
        dataSourceId: _dataSourceId,
        status: 'Failed',
        startedAt: new Date(Date.now() - 172800000).toISOString(),
        completedAt: new Date(Date.now() - 172700000).toISOString(),
        documentsAdded: 0,
        documentsUpdated: 0,
        documentsDeleted: 0,
        documentsSkipped: 0,
        errorCount: 1,
        errorMessage: 'Connection timeout after 30 seconds',
        durationMs: 30000,
        triggeredBy: 'Scheduled'
      }
    ]
    syncStats.value = {
      totalSyncs: 25,
      successfulSyncs: 23,
      failedSyncs: 2,
      averageDurationMs: 95000,
      lastSuccessfulSync: new Date(Date.now() - 3600000).toISOString(),
      documentsProcessed: 450
    }
  } finally {
    loading.value = false
  }
}

async function handleSync() {
  if (!props.dataSource) return
  loading.value = true
  try {
    emit('sync', props.dataSource.id)
    toast.success('Sync started', 'The data source is now syncing')
  } finally {
    loading.value = false
  }
}

const isEnabled = ref(true)

// Update isEnabled when dataSource changes
watch(() => props.dataSource, (ds) => {
  if (ds) {
    isEnabled.value = ds.status !== 'Disabled'
  }
}, { immediate: true })

async function handleToggle(enabled: boolean) {
  if (!props.dataSource) return
  loading.value = true
  try {
    await workspaceService.toggleDataSource(
      props.dataSource.workspaceId,
      props.dataSource.id,
      enabled
    )
    isEnabled.value = enabled
    emit('toggle', props.dataSource.id, enabled)
    toast.success(
      enabled ? 'Data source enabled' : 'Data source disabled',
      enabled ? 'Syncing will resume on schedule' : 'Syncing has been paused'
    )
  } catch (e) {
    toast.error('Failed to toggle data source', e instanceof Error ? e.message : 'Unknown error')
    // Revert the toggle
    isEnabled.value = !enabled
  } finally {
    loading.value = false
  }
}

// Helpers
const getTypeIcon = (type: DataSourceType) => {
  switch (type) {
    case 'WebCrawler': return GlobeAltIcon
    case 'Database': return CircleStackIcon
    case 'SharePoint':
    case 'GoogleDrive':
    case 'AzureBlob': return CloudIcon
    case 'Confluence':
    case 'Notion': return DocumentTextIcon
    case 'S3': return ServerIcon
    default: return DocumentTextIcon
  }
}

const getStatusColor = (status: string) => {
  switch (status) {
    case 'Active':
    case 'Completed':
      return 'text-green-600 bg-green-100 dark:text-green-400 dark:bg-green-900/50'
    case 'Syncing':
    case 'InProgress':
      return 'text-blue-600 bg-blue-100 dark:text-blue-400 dark:bg-blue-900/50'
    case 'Pending':
      return 'text-yellow-600 bg-yellow-100 dark:text-yellow-400 dark:bg-yellow-900/50'
    case 'Error':
    case 'Failed':
      return 'text-red-600 bg-red-100 dark:text-red-400 dark:bg-red-900/50'
    case 'Disabled':
      return 'text-gray-600 bg-gray-100 dark:text-gray-400 dark:bg-gray-700'
    case 'PartialSuccess':
      return 'text-orange-600 bg-orange-100 dark:text-orange-400 dark:bg-orange-900/50'
    default:
      return 'text-gray-600 bg-gray-100 dark:text-gray-400 dark:bg-gray-700'
  }
}

const getSyncStatusIcon = (status: string) => {
  switch (status) {
    case 'Completed': return CheckCircleIcon
    case 'Failed': return ExclamationCircleIcon
    case 'InProgress': return ArrowPathIcon
    case 'Pending': return ClockIcon
    default: return ClockIcon
  }
}

const formatDate = (dateStr: string) => {
  return new Date(dateStr).toLocaleString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

const formatRelativeTime = (dateStr: string) => {
  const date = new Date(dateStr)
  const now = new Date()
  const diff = now.getTime() - date.getTime()
  const minutes = Math.floor(diff / 60000)
  const hours = Math.floor(diff / 3600000)
  const days = Math.floor(diff / 86400000)

  if (minutes < 60) return `${minutes}m ago`
  if (hours < 24) return `${hours}h ago`
  return `${days}d ago`
}

const formatDuration = (ms: number) => {
  const seconds = Math.floor(ms / 1000)
  const minutes = Math.floor(seconds / 60)
  const hours = Math.floor(minutes / 60)

  if (hours > 0) return `${hours}h ${minutes % 60}m`
  if (minutes > 0) return `${minutes}m ${seconds % 60}s`
  return `${seconds}s`
}

const successRate = computed(() => {
  if (!syncStats.value || syncStats.value.totalSyncs === 0) return 0
  return Math.round((syncStats.value.successfulSyncs / syncStats.value.totalSyncs) * 100)
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
                    <div v-if="dataSource" class="flex items-center gap-3">
                      <div class="rounded-lg bg-gray-100 p-2 dark:bg-gray-800">
                        <component :is="getTypeIcon(dataSource.type)" class="h-6 w-6 text-gray-500" />
                      </div>
                      <div>
                        <DialogTitle class="text-lg font-semibold text-gray-900 dark:text-white">
                          {{ dataSource.name }}
                        </DialogTitle>
                        <p class="text-sm text-gray-500 dark:text-gray-400">
                          {{ dataSource.type }}
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
                  <div v-if="dataSource" class="flex-1 overflow-y-auto">
                    <!-- Status Banner -->
                    <div
                      v-if="dataSource.status === 'Error'"
                      class="bg-red-50 px-6 py-3 dark:bg-red-900/20"
                    >
                      <div class="flex items-center gap-2 text-red-700 dark:text-red-300">
                        <ExclamationCircleIcon class="h-5 w-5" />
                        <span class="text-sm font-medium">Sync failed</span>
                      </div>
                      <p class="mt-1 text-sm text-red-600 dark:text-red-400">
                        Last sync encountered an error. Check the sync history for details.
                      </p>
                    </div>

                    <!-- Actions -->
                    <div class="border-b border-gray-200 px-6 py-4 dark:border-gray-700 space-y-4">
                      <!-- Enable/Disable Toggle -->
                      <div class="flex items-center justify-between">
                        <div>
                          <p class="text-sm font-medium text-gray-900 dark:text-white">Enable Data Source</p>
                          <p class="text-xs text-gray-500 dark:text-gray-400">
                            {{ isEnabled ? 'Syncing is active' : 'Syncing is paused' }}
                          </p>
                        </div>
                        <Switch
                          :model-value="isEnabled"
                          :disabled="loading"
                          class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors"
                          :class="isEnabled ? 'bg-aegis-600' : 'bg-gray-300 dark:bg-gray-600'"
                          @update:model-value="handleToggle"
                        >
                          <span
                            class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform"
                            :class="isEnabled ? 'translate-x-6' : 'translate-x-1'"
                          />
                        </Switch>
                      </div>

                      <!-- Action Buttons -->
                      <div class="flex items-center gap-2">
                        <button
                          class="btn-primary flex-1 justify-center"
                          :disabled="loading || dataSource.status === 'Syncing' || !isEnabled"
                          @click="handleSync"
                        >
                          <ArrowPathIcon class="h-4 w-4 mr-2" :class="{ 'animate-spin': dataSource.status === 'Syncing' }" />
                          {{ dataSource.status === 'Syncing' ? 'Syncing...' : 'Sync Now' }}
                        </button>
                        <button
                          class="btn-ghost flex-1 justify-center"
                          @click="emit('edit', dataSource.id)"
                        >
                          <Cog6ToothIcon class="h-4 w-4 mr-2" />
                          Configure
                        </button>
                        <button
                          class="btn-ghost p-2 text-red-600 hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-900/20"
                          @click="emit('delete', dataSource.id)"
                        >
                          <TrashIcon class="h-5 w-5" />
                        </button>
                      </div>
                    </div>

                    <!-- Tabs -->
                    <TabGroup>
                      <TabList class="flex border-b border-gray-200 px-6 dark:border-gray-700">
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
                            Overview
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
                            Sync History
                          </button>
                        </Tab>
                      </TabList>

                      <TabPanels>
                        <!-- Overview Tab -->
                        <TabPanel class="px-6 py-4 space-y-6">
                          <!-- Status -->
                          <div class="grid grid-cols-2 gap-4">
                            <div>
                              <p class="text-xs text-gray-500 dark:text-gray-400">Status</p>
                              <span
                                class="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium mt-1"
                                :class="getStatusColor(dataSource.status)"
                              >
                                {{ dataSource.status }}
                              </span>
                            </div>
                            <div>
                              <p class="text-xs text-gray-500 dark:text-gray-400">Documents</p>
                              <p class="mt-1 text-sm font-medium text-gray-900 dark:text-white">
                                {{ dataSource.documentCount.toLocaleString() }}
                              </p>
                            </div>
                            <div>
                              <p class="text-xs text-gray-500 dark:text-gray-400">Last Sync</p>
                              <p class="mt-1 text-sm text-gray-900 dark:text-white">
                                {{ dataSource.lastSyncAt ? formatRelativeTime(dataSource.lastSyncAt) : 'Never' }}
                              </p>
                            </div>
                            <div>
                              <p class="text-xs text-gray-500 dark:text-gray-400">Next Sync</p>
                              <p class="mt-1 text-sm text-gray-900 dark:text-white">
                                {{ dataSource.nextSyncAt ? formatDate(dataSource.nextSyncAt) : 'Not scheduled' }}
                              </p>
                            </div>
                          </div>

                          <!-- Sync Stats -->
                          <div v-if="syncStats" class="border-t border-gray-200 pt-4 dark:border-gray-700">
                            <h3 class="text-sm font-medium text-gray-900 dark:text-white mb-4">Sync Statistics</h3>
                            <div class="grid grid-cols-2 gap-4">
                              <div class="rounded-lg bg-gray-50 p-3 dark:bg-gray-800">
                                <p class="text-xs text-gray-500 dark:text-gray-400">Success Rate</p>
                                <p class="mt-1 text-lg font-semibold" :class="successRate >= 90 ? 'text-green-600' : successRate >= 70 ? 'text-yellow-600' : 'text-red-600'">
                                  {{ successRate }}%
                                </p>
                              </div>
                              <div class="rounded-lg bg-gray-50 p-3 dark:bg-gray-800">
                                <p class="text-xs text-gray-500 dark:text-gray-400">Total Syncs</p>
                                <p class="mt-1 text-lg font-semibold text-gray-900 dark:text-white">
                                  {{ syncStats.totalSyncs }}
                                </p>
                              </div>
                              <div class="rounded-lg bg-gray-50 p-3 dark:bg-gray-800">
                                <p class="text-xs text-gray-500 dark:text-gray-400">Avg Duration</p>
                                <p class="mt-1 text-lg font-semibold text-gray-900 dark:text-white">
                                  {{ formatDuration(syncStats.averageDurationMs) }}
                                </p>
                              </div>
                              <div class="rounded-lg bg-gray-50 p-3 dark:bg-gray-800">
                                <p class="text-xs text-gray-500 dark:text-gray-400">Docs Processed</p>
                                <p class="mt-1 text-lg font-semibold text-gray-900 dark:text-white">
                                  {{ syncStats.documentsProcessed.toLocaleString() }}
                                </p>
                              </div>
                            </div>
                          </div>

                          <!-- Configuration Summary -->
                          <div class="border-t border-gray-200 pt-4 dark:border-gray-700">
                            <h3 class="text-sm font-medium text-gray-900 dark:text-white mb-4">Configuration</h3>
                            <div class="rounded-lg bg-gray-50 p-3 dark:bg-gray-800">
                              <div v-if="dataSource.config.url" class="flex justify-between py-1 text-sm">
                                <span class="text-gray-500 dark:text-gray-400">URL</span>
                                <span class="text-gray-900 dark:text-white truncate max-w-48">{{ dataSource.config.url }}</span>
                              </div>
                              <div v-if="dataSource.config.syncSchedule" class="flex justify-between py-1 text-sm">
                                <span class="text-gray-500 dark:text-gray-400">Schedule</span>
                                <span class="text-gray-900 dark:text-white">{{ dataSource.config.syncSchedule }}</span>
                              </div>
                              <div v-if="dataSource.config.maxDocuments" class="flex justify-between py-1 text-sm">
                                <span class="text-gray-500 dark:text-gray-400">Max Documents</span>
                                <span class="text-gray-900 dark:text-white">{{ dataSource.config.maxDocuments.toLocaleString() }}</span>
                              </div>
                              <div class="flex justify-between py-1 text-sm">
                                <span class="text-gray-500 dark:text-gray-400">Created</span>
                                <span class="text-gray-900 dark:text-white">{{ formatDate(dataSource.createdAt) }}</span>
                              </div>
                            </div>
                          </div>
                        </TabPanel>

                        <!-- Sync History Tab -->
                        <TabPanel class="px-6 py-4">
                          <div v-if="loading" class="py-8 text-center">
                            <div class="animate-spin rounded-full h-6 w-6 border-b-2 border-aegis-600 mx-auto"></div>
                          </div>

                          <div v-else-if="syncHistory.length === 0" class="py-8 text-center">
                            <ClockIcon class="mx-auto h-12 w-12 text-gray-400" />
                            <p class="mt-2 text-gray-500 dark:text-gray-400">No sync history yet</p>
                          </div>

                          <div v-else class="space-y-3">
                            <div
                              v-for="sync in syncHistory"
                              :key="sync.id"
                              class="rounded-lg border border-gray-200 p-4 dark:border-gray-700"
                            >
                              <div class="flex items-start justify-between">
                                <div class="flex items-center gap-2">
                                  <component
                                    :is="getSyncStatusIcon(sync.status)"
                                    class="h-5 w-5"
                                    :class="{
                                      'text-green-500': sync.status === 'Completed',
                                      'text-red-500': sync.status === 'Failed',
                                      'text-blue-500 animate-spin': sync.status === 'InProgress',
                                      'text-yellow-500': sync.status === 'Pending',
                                      'text-orange-500': sync.status === 'PartialSuccess'
                                    }"
                                  />
                                  <span
                                    class="text-sm font-medium"
                                    :class="{
                                      'text-green-700 dark:text-green-400': sync.status === 'Completed',
                                      'text-red-700 dark:text-red-400': sync.status === 'Failed',
                                      'text-blue-700 dark:text-blue-400': sync.status === 'InProgress',
                                      'text-gray-700 dark:text-gray-300': sync.status === 'Pending'
                                    }"
                                  >
                                    {{ sync.status }}
                                  </span>
                                </div>
                                <span class="text-xs text-gray-500 dark:text-gray-400">
                                  {{ formatRelativeTime(sync.startedAt) }}
                                </span>
                              </div>

                              <div class="mt-3 flex items-center gap-4 text-sm text-gray-600 dark:text-gray-400">
                                <div class="flex items-center gap-1" title="Added">
                                  <DocumentPlusIcon class="h-4 w-4 text-green-500" />
                                  <span>{{ sync.documentsAdded }}</span>
                                </div>
                                <div class="flex items-center gap-1" title="Updated">
                                  <DocumentArrowUpIcon class="h-4 w-4 text-blue-500" />
                                  <span>{{ sync.documentsUpdated }}</span>
                                </div>
                                <div class="flex items-center gap-1" title="Deleted">
                                  <DocumentMinusIcon class="h-4 w-4 text-red-500" />
                                  <span>{{ sync.documentsDeleted }}</span>
                                </div>
                                <div v-if="sync.durationMs" class="flex items-center gap-1">
                                  <ClockIcon class="h-4 w-4" />
                                  <span>{{ formatDuration(sync.durationMs) }}</span>
                                </div>
                              </div>

                              <div v-if="sync.errorMessage" class="mt-2 p-2 bg-red-50 rounded text-sm text-red-600 dark:bg-red-900/20 dark:text-red-400">
                                {{ sync.errorMessage }}
                              </div>

                              <div class="mt-2 flex items-center gap-2 text-xs text-gray-500 dark:text-gray-400">
                                <span class="inline-flex items-center gap-1 rounded-full bg-gray-100 px-2 py-0.5 dark:bg-gray-700">
                                  {{ sync.triggeredBy }}
                                </span>
                                <span>{{ formatDate(sync.startedAt) }}</span>
                              </div>
                            </div>
                          </div>
                        </TabPanel>
                      </TabPanels>
                    </TabGroup>
                  </div>

                  <!-- Empty State -->
                  <div v-else class="flex-1 flex items-center justify-center">
                    <div class="text-center">
                      <GlobeAltIcon class="mx-auto h-12 w-12 text-gray-400" />
                      <p class="mt-2 text-gray-500 dark:text-gray-400">No data source selected</p>
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
