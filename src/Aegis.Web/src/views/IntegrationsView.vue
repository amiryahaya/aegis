<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { TabGroup, TabList, Tab, TabPanels, TabPanel } from '@headlessui/vue'
import {
  GlobeAltIcon,
  KeyIcon,
  PlusIcon,
  MagnifyingGlassIcon,
  FunnelIcon,
  ArrowPathIcon,
  CheckCircleIcon,
  ExclamationTriangleIcon
} from '@heroicons/vue/24/outline'
import { useWebhookStore } from '@/stores/webhook'
import { useToast } from '@/composables/useToast'
import WebhookCard from '@/components/webhook/WebhookCard.vue'
import DeliveryHistoryTable from '@/components/webhook/DeliveryHistoryTable.vue'
import WebhookFormDialog from '@/components/webhook/WebhookFormDialog.vue'
import WebhookTestDialog from '@/components/webhook/WebhookTestDialog.vue'
import type { WebhookSubscription, CreateWebhookRequest, UpdateWebhookRequest } from '@/types'

const webhookStore = useWebhookStore()
const toast = useToast()

// State
const selectedTab = ref(0)
const searchQuery = ref('')
const showActiveOnly = ref<boolean | undefined>(undefined)
const isFormDialogOpen = ref(false)
const isTestDialogOpen = ref(false)
const editingWebhook = ref<WebhookSubscription | null>(null)
const testingWebhook = ref<WebhookSubscription | null>(null)
const selectedWebhookId = ref<string | null>(null)
const testDialogRef = ref<InstanceType<typeof WebhookTestDialog> | null>(null)

// Computed
const filteredWebhooks = computed(() => {
  let result = webhookStore.filteredWebhooks

  if (searchQuery.value) {
    const query = searchQuery.value.toLowerCase()
    result = result.filter(
      w =>
        w.name.toLowerCase().includes(query) ||
        w.url.toLowerCase().includes(query) ||
        w.description?.toLowerCase().includes(query)
    )
  }

  if (showActiveOnly.value !== undefined) {
    result = result.filter(w => w.isActive === showActiveOnly.value)
  }

  return result
})

const stats = computed(() => webhookStore.stats)

// Actions
async function loadWebhooks() {
  try {
    await webhookStore.fetchWebhooks()
  } catch {
    toast.error('Failed to load webhooks')
  }
}

function openCreateDialog() {
  editingWebhook.value = null
  isFormDialogOpen.value = true
}

function openEditDialog(webhook: WebhookSubscription) {
  editingWebhook.value = webhook
  isFormDialogOpen.value = true
}

function openTestDialog(webhookId: string) {
  const webhook = webhookStore.webhooks.find(w => w.id === webhookId)
  if (webhook) {
    testingWebhook.value = webhook
    isTestDialogOpen.value = true
  }
}

async function handleCreate(request: CreateWebhookRequest) {
  try {
    await webhookStore.createWebhook(request)
    isFormDialogOpen.value = false
    toast.success('Webhook created successfully')
  } catch {
    toast.error('Failed to create webhook')
  }
}

async function handleUpdate(id: string, request: UpdateWebhookRequest) {
  try {
    await webhookStore.updateWebhook(id, request)
    isFormDialogOpen.value = false
    toast.success('Webhook updated successfully')
  } catch {
    toast.error('Failed to update webhook')
  }
}

async function handleDelete(id: string) {
  if (!confirm('Are you sure you want to delete this webhook?')) return

  try {
    await webhookStore.deleteWebhook(id)
    toast.success('Webhook deleted')
  } catch {
    toast.error('Failed to delete webhook')
  }
}

async function handleToggle(id: string) {
  try {
    await webhookStore.toggleWebhookActive(id)
    toast.success('Webhook status updated')
  } catch {
    toast.error('Failed to update webhook status')
  }
}

async function handleTest(webhookId: string) {
  try {
    const result = await webhookStore.testWebhook(webhookId)
    testDialogRef.value?.handleTestResult(result)

    if (result.success) {
      toast.success('Test webhook delivered successfully')
    } else {
      toast.error('Test webhook delivery failed')
    }
  } catch {
    toast.error('Failed to test webhook')
  }
}

function viewWebhookDetails(id: string) {
  selectedWebhookId.value = id
  webhookStore.fetchDeliveryHistory(id)
  selectedTab.value = 1 // Switch to delivery history tab
}

async function handleRetryDelivery(deliveryId: string) {
  try {
    await webhookStore.retryDelivery(deliveryId)
    toast.success('Delivery retry initiated')
  } catch {
    toast.error('Failed to retry delivery')
  }
}

// Lifecycle
onMounted(() => {
  loadWebhooks()
})
</script>

<template>
  <div class="mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-8">
    <!-- Header -->
    <div class="mb-6">
      <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Integrations</h1>
      <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
        Manage webhooks and API integrations
      </p>
    </div>

    <!-- Stats -->
    <div class="mb-6 grid grid-cols-2 gap-4 sm:grid-cols-4">
      <div class="rounded-lg border bg-white p-4 dark:border-gray-700 dark:bg-gray-800">
        <div class="flex items-center gap-3">
          <div class="rounded-lg bg-aegis-100 p-2 dark:bg-aegis-900/30">
            <GlobeAltIcon class="h-5 w-5 text-aegis-600 dark:text-aegis-400" />
          </div>
          <div>
            <p class="text-2xl font-bold text-gray-900 dark:text-white">
              {{ stats.totalWebhooks }}
            </p>
            <p class="text-sm text-gray-500 dark:text-gray-400">Total Webhooks</p>
          </div>
        </div>
      </div>

      <div class="rounded-lg border bg-white p-4 dark:border-gray-700 dark:bg-gray-800">
        <div class="flex items-center gap-3">
          <div class="rounded-lg bg-green-100 p-2 dark:bg-green-900/30">
            <CheckCircleIcon class="h-5 w-5 text-green-600 dark:text-green-400" />
          </div>
          <div>
            <p class="text-2xl font-bold text-gray-900 dark:text-white">
              {{ stats.activeWebhooks }}
            </p>
            <p class="text-sm text-gray-500 dark:text-gray-400">Active</p>
          </div>
        </div>
      </div>

      <div class="rounded-lg border bg-white p-4 dark:border-gray-700 dark:bg-gray-800">
        <div class="flex items-center gap-3">
          <div class="rounded-lg bg-yellow-100 p-2 dark:bg-yellow-900/30">
            <ExclamationTriangleIcon class="h-5 w-5 text-yellow-600 dark:text-yellow-400" />
          </div>
          <div>
            <p class="text-2xl font-bold text-gray-900 dark:text-white">
              {{ stats.failedDeliveries }}
            </p>
            <p class="text-sm text-gray-500 dark:text-gray-400">Failed Deliveries</p>
          </div>
        </div>
      </div>

      <div class="rounded-lg border bg-white p-4 dark:border-gray-700 dark:bg-gray-800">
        <div class="flex items-center gap-3">
          <div class="rounded-lg bg-blue-100 p-2 dark:bg-blue-900/30">
            <ArrowPathIcon class="h-5 w-5 text-blue-600 dark:text-blue-400" />
          </div>
          <div>
            <p class="text-2xl font-bold text-gray-900 dark:text-white">
              {{ stats.averageSuccessRate.toFixed(1) }}%
            </p>
            <p class="text-sm text-gray-500 dark:text-gray-400">Success Rate</p>
          </div>
        </div>
      </div>
    </div>

    <!-- Tabs -->
    <TabGroup :selected-index="selectedTab" @change="selectedTab = $event">
      <div class="flex items-center justify-between border-b dark:border-gray-700">
        <TabList class="flex gap-4">
          <Tab v-slot="{ selected }" as="template">
            <button
              class="flex items-center gap-2 border-b-2 px-1 py-3 text-sm font-medium transition-colors"
              :class="[
                selected
                  ? 'border-aegis-500 text-aegis-600 dark:text-aegis-400'
                  : 'border-transparent text-gray-500 hover:border-gray-300 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-300'
              ]"
            >
              <GlobeAltIcon class="h-5 w-5" />
              Webhooks
            </button>
          </Tab>
          <Tab v-slot="{ selected }" as="template">
            <button
              class="flex items-center gap-2 border-b-2 px-1 py-3 text-sm font-medium transition-colors"
              :class="[
                selected
                  ? 'border-aegis-500 text-aegis-600 dark:text-aegis-400'
                  : 'border-transparent text-gray-500 hover:border-gray-300 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-300'
              ]"
            >
              <ArrowPathIcon class="h-5 w-5" />
              Delivery History
            </button>
          </Tab>
          <Tab v-slot="{ selected }" as="template">
            <button
              class="flex items-center gap-2 border-b-2 px-1 py-3 text-sm font-medium transition-colors"
              :class="[
                selected
                  ? 'border-aegis-500 text-aegis-600 dark:text-aegis-400'
                  : 'border-transparent text-gray-500 hover:border-gray-300 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-300'
              ]"
            >
              <KeyIcon class="h-5 w-5" />
              API Keys
            </button>
          </Tab>
        </TabList>

        <button class="btn-primary gap-2" @click="openCreateDialog">
          <PlusIcon class="h-4 w-4" />
          New Webhook
        </button>
      </div>

      <TabPanels class="mt-6">
        <!-- Webhooks Tab -->
        <TabPanel>
          <!-- Filters -->
          <div class="mb-4 flex flex-wrap items-center gap-4">
            <div class="relative flex-1">
              <MagnifyingGlassIcon
                class="absolute left-3 top-1/2 h-5 w-5 -translate-y-1/2 text-gray-400"
              />
              <input
                v-model="searchQuery"
                type="text"
                placeholder="Search webhooks..."
                class="w-full rounded-lg border-gray-300 pl-10 focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
              />
            </div>
            <div class="flex items-center gap-2">
              <FunnelIcon class="h-5 w-5 text-gray-400" />
              <select
                v-model="showActiveOnly"
                class="rounded-lg border-gray-300 focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
              >
                <option :value="undefined">All Status</option>
                <option :value="true">Active Only</option>
                <option :value="false">Inactive Only</option>
              </select>
            </div>
            <button
              class="rounded-lg p-2 text-gray-400 hover:bg-gray-100 hover:text-gray-600 dark:hover:bg-gray-700"
              title="Refresh"
              @click="loadWebhooks"
            >
              <ArrowPathIcon
                class="h-5 w-5"
                :class="{ 'animate-spin': webhookStore.isLoading }"
              />
            </button>
          </div>

          <!-- Webhook List -->
          <div v-if="webhookStore.isLoading" class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <div
              v-for="i in 6"
              :key="i"
              class="h-48 animate-pulse rounded-lg bg-gray-200 dark:bg-gray-700"
            />
          </div>

          <div
            v-else-if="filteredWebhooks.length === 0"
            class="flex flex-col items-center justify-center py-12"
          >
            <GlobeAltIcon class="h-12 w-12 text-gray-300 dark:text-gray-600" />
            <h3 class="mt-4 text-lg font-medium text-gray-900 dark:text-white">
              No webhooks found
            </h3>
            <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
              {{ searchQuery ? 'Try adjusting your search' : 'Create your first webhook to get started' }}
            </p>
            <button v-if="!searchQuery" class="btn-primary mt-4 gap-2" @click="openCreateDialog">
              <PlusIcon class="h-4 w-4" />
              Create Webhook
            </button>
          </div>

          <div v-else class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <WebhookCard
              v-for="webhook in filteredWebhooks"
              :key="webhook.id"
              :webhook="webhook"
              @test="openTestDialog"
              @edit="openEditDialog"
              @delete="handleDelete"
              @toggle="handleToggle"
              @view-details="viewWebhookDetails"
            />
          </div>
        </TabPanel>

        <!-- Delivery History Tab -->
        <TabPanel>
          <div v-if="!selectedWebhookId" class="py-12 text-center">
            <ArrowPathIcon class="mx-auto h-12 w-12 text-gray-300 dark:text-gray-600" />
            <h3 class="mt-4 text-lg font-medium text-gray-900 dark:text-white">
              Select a webhook
            </h3>
            <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
              Click on a webhook to view its delivery history
            </p>
          </div>

          <div v-else>
            <div class="mb-4 flex items-center justify-between">
              <div>
                <h3 class="font-medium text-gray-900 dark:text-white">
                  {{ webhookStore.selectedWebhook?.name || 'Delivery History' }}
                </h3>
                <p class="text-sm text-gray-500 dark:text-gray-400">
                  Recent webhook delivery attempts
                </p>
              </div>
              <button
                class="text-sm text-aegis-600 hover:text-aegis-700 dark:text-aegis-400"
                @click="selectedWebhookId = null"
              >
                ← Back to webhooks
              </button>
            </div>

            <DeliveryHistoryTable
              :deliveries="webhookStore.deliveryHistory"
              :is-loading="webhookStore.isLoadingDeliveries"
              @retry="handleRetryDelivery"
            />
          </div>
        </TabPanel>

        <!-- API Keys Tab -->
        <TabPanel>
          <div class="py-12 text-center">
            <KeyIcon class="mx-auto h-12 w-12 text-gray-300 dark:text-gray-600" />
            <h3 class="mt-4 text-lg font-medium text-gray-900 dark:text-white">
              API Key Management
            </h3>
            <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
              Manage your API keys in the Profile settings
            </p>
            <router-link to="/profile" class="btn-primary mt-4 inline-flex gap-2">
              <KeyIcon class="h-4 w-4" />
              Go to Profile
            </router-link>
          </div>
        </TabPanel>
      </TabPanels>
    </TabGroup>

    <!-- Dialogs -->
    <WebhookFormDialog
      :is-open="isFormDialogOpen"
      :webhook="editingWebhook"
      :is-loading="webhookStore.isLoading"
      @close="isFormDialogOpen = false"
      @create="handleCreate"
      @update="handleUpdate"
    />

    <WebhookTestDialog
      ref="testDialogRef"
      :is-open="isTestDialogOpen"
      :webhook="testingWebhook"
      @close="isTestDialogOpen = false"
      @test="handleTest"
    />
  </div>
</template>
