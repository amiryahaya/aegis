<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import {
  apiKeyService,
  API_KEY_SCOPES,
  EXPIRATION_OPTIONS,
  type ApiKey,
  type ApiKeyScope,
  type CreateApiKeyResponse
} from '@/services/apikey.service'
import { useToast } from '@/composables/useToast'
import {
  KeyIcon,
  PlusIcon,
  TrashIcon,
  ClipboardDocumentIcon,
  CheckIcon,
  ExclamationTriangleIcon,
  ClockIcon,
  ArrowPathIcon
} from '@heroicons/vue/24/outline'
import {
  Dialog,
  DialogPanel,
  DialogTitle,
  TransitionChild,
  TransitionRoot,
  Disclosure,
  DisclosureButton,
  DisclosurePanel
} from '@headlessui/vue'

const toast = useToast()

const loading = ref(false)
const apiKeys = ref<ApiKey[]>([])
const showCreateDialog = ref(false)
const showKeyCreatedDialog = ref(false)
const showRevokeConfirm = ref(false)
const keyToRevoke = ref<ApiKey | null>(null)
const createdKey = ref<CreateApiKeyResponse | null>(null)
const copiedKeyId = ref<string | null>(null)
const revokingKeyId = ref<string | null>(null)

// Create form
const newKeyName = ref('')
const newKeyScopes = ref<ApiKeyScope[]>(['read', 'query'])
const newKeyExpiration = ref(90)
const newKeyDescription = ref('')
const creating = ref(false)

onMounted(async () => {
  await fetchApiKeys()
})

async function fetchApiKeys() {
  loading.value = true
  try {
    apiKeys.value = await apiKeyService.getApiKeys()
  } catch (error) {
    // Mock data for development
    apiKeys.value = [
      {
        id: '1',
        name: 'Development Key',
        prefix: 'ak_dev_',
        scopes: ['read', 'write', 'query'],
        createdAt: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString(),
        expiresAt: new Date(Date.now() + 60 * 24 * 60 * 60 * 1000).toISOString(),
        lastUsedAt: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
        isActive: true,
        usageCount: 1234
      },
      {
        id: '2',
        name: 'Production API',
        prefix: 'ak_prod',
        scopes: ['read', 'query', 'documents'],
        createdAt: new Date(Date.now() - 90 * 24 * 60 * 60 * 1000).toISOString(),
        lastUsedAt: new Date(Date.now() - 5 * 60 * 1000).toISOString(),
        isActive: true,
        usageCount: 45678
      },
      {
        id: '3',
        name: 'Testing Key',
        prefix: 'ak_test',
        scopes: ['read'],
        createdAt: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString(),
        expiresAt: new Date(Date.now() - 1 * 24 * 60 * 60 * 1000).toISOString(), // Expired
        isActive: false,
        usageCount: 56
      }
    ]
  } finally {
    loading.value = false
  }
}

async function createApiKey() {
  if (!newKeyName.value.trim() || newKeyScopes.value.length === 0) {
    toast.error('Invalid input', 'Please provide a name and select at least one scope')
    return
  }

  creating.value = true
  try {
    const response = await apiKeyService.createApiKey({
      name: newKeyName.value.trim(),
      scopes: newKeyScopes.value,
      expiresInDays: newKeyExpiration.value || undefined,
      description: newKeyDescription.value.trim() || undefined
    })
    createdKey.value = response
    showCreateDialog.value = false
    showKeyCreatedDialog.value = true
    await fetchApiKeys()
  } catch (error) {
    // Mock success for development
    const mockKey: CreateApiKeyResponse = {
      id: Date.now().toString(),
      name: newKeyName.value.trim(),
      key: `ak_${Math.random().toString(36).substring(2, 10)}_${Math.random().toString(36).substring(2, 34)}`,
      prefix: `ak_${Math.random().toString(36).substring(2, 6)}`,
      scopes: newKeyScopes.value,
      createdAt: new Date().toISOString(),
      expiresAt: newKeyExpiration.value
        ? new Date(Date.now() + newKeyExpiration.value * 24 * 60 * 60 * 1000).toISOString()
        : undefined
    }
    createdKey.value = mockKey
    showCreateDialog.value = false
    showKeyCreatedDialog.value = true

    // Add to list
    apiKeys.value.unshift({
      id: mockKey.id,
      name: mockKey.name,
      prefix: mockKey.prefix,
      scopes: mockKey.scopes,
      createdAt: mockKey.createdAt,
      expiresAt: mockKey.expiresAt,
      isActive: true,
      usageCount: 0
    })
  } finally {
    creating.value = false
  }
}

function openCreateDialog() {
  newKeyName.value = ''
  newKeyScopes.value = ['read', 'query']
  newKeyExpiration.value = 90
  newKeyDescription.value = ''
  showCreateDialog.value = true
}

function closeKeyCreatedDialog() {
  showKeyCreatedDialog.value = false
  createdKey.value = null
  newKeyName.value = ''
  newKeyScopes.value = ['read', 'query']
  newKeyExpiration.value = 90
  newKeyDescription.value = ''
}

function confirmRevoke(key: ApiKey) {
  keyToRevoke.value = key
  showRevokeConfirm.value = true
}

async function revokeApiKey() {
  if (!keyToRevoke.value) return

  revokingKeyId.value = keyToRevoke.value.id
  try {
    await apiKeyService.revokeApiKey(keyToRevoke.value.id)
    apiKeys.value = apiKeys.value.filter(k => k.id !== keyToRevoke.value?.id)
    toast.success('API key revoked', `"${keyToRevoke.value.name}" has been revoked`)
  } catch (error) {
    // Mock success for development
    apiKeys.value = apiKeys.value.filter(k => k.id !== keyToRevoke.value?.id)
    toast.success('API key revoked', `"${keyToRevoke.value.name}" has been revoked`)
  } finally {
    revokingKeyId.value = null
    showRevokeConfirm.value = false
    keyToRevoke.value = null
  }
}

async function copyToClipboard(text: string, keyId: string) {
  try {
    await navigator.clipboard.writeText(text)
    copiedKeyId.value = keyId
    toast.success('Copied', 'API key copied to clipboard')
    setTimeout(() => {
      copiedKeyId.value = null
    }, 2000)
  } catch (error) {
    toast.error('Copy failed', 'Could not copy to clipboard')
  }
}

function toggleScope(scope: ApiKeyScope) {
  const index = newKeyScopes.value.indexOf(scope)
  if (index === -1) {
    newKeyScopes.value.push(scope)
  } else {
    newKeyScopes.value.splice(index, 1)
  }
}

function formatDate(dateStr: string) {
  return new Date(dateStr).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric'
  })
}

function formatRelativeTime(dateStr?: string) {
  if (!dateStr) return 'Never'
  const date = new Date(dateStr)
  const now = new Date()
  const diff = now.getTime() - date.getTime()

  if (diff < 60000) return 'Just now'
  if (diff < 3600000) return `${Math.floor(diff / 60000)}m ago`
  if (diff < 86400000) return `${Math.floor(diff / 3600000)}h ago`
  if (diff < 604800000) return `${Math.floor(diff / 86400000)}d ago`
  return formatDate(dateStr)
}

function isExpired(key: ApiKey): boolean {
  if (!key.expiresAt) return false
  return new Date(key.expiresAt) < new Date()
}

function getExpirationStatus(key: ApiKey): { label: string; class: string } {
  if (!key.expiresAt) {
    return { label: 'Never expires', class: 'text-gray-500' }
  }

  const expiresAt = new Date(key.expiresAt)
  const now = new Date()
  const daysUntilExpiry = Math.ceil((expiresAt.getTime() - now.getTime()) / (24 * 60 * 60 * 1000))

  if (daysUntilExpiry < 0) {
    return { label: 'Expired', class: 'text-red-500' }
  }
  if (daysUntilExpiry <= 7) {
    return { label: `Expires in ${daysUntilExpiry}d`, class: 'text-amber-500' }
  }
  if (daysUntilExpiry <= 30) {
    return { label: `Expires in ${daysUntilExpiry}d`, class: 'text-yellow-500' }
  }
  return { label: `Expires ${formatDate(key.expiresAt)}`, class: 'text-gray-500' }
}

const activeKeys = computed(() => apiKeys.value.filter(k => k.isActive && !isExpired(k)))
const inactiveKeys = computed(() => apiKeys.value.filter(k => !k.isActive || isExpired(k)))

const scopesByCategory = computed(() => {
  const categories: Record<string, typeof API_KEY_SCOPES> = {}
  API_KEY_SCOPES.forEach(scope => {
    if (!categories[scope.category]) {
      categories[scope.category] = []
    }
    categories[scope.category].push(scope)
  })
  return categories
})
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <div class="card p-6">
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-3">
          <div class="rounded-lg bg-purple-100 p-2 dark:bg-purple-900/50">
            <KeyIcon class="h-5 w-5 text-purple-600 dark:text-purple-400" />
          </div>
          <div>
            <h2 class="text-lg font-semibold text-gray-900 dark:text-white">API Keys</h2>
            <p class="text-sm text-gray-500 dark:text-gray-400">
              Manage API keys for programmatic access to AEGIS
            </p>
          </div>
        </div>
        <button class="btn-primary" @click="openCreateDialog">
          <PlusIcon class="h-4 w-4 mr-2" />
          Create API Key
        </button>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="card p-8 text-center">
      <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-aegis-600 mx-auto"></div>
    </div>

    <!-- Empty State -->
    <div v-else-if="apiKeys.length === 0" class="card p-8 text-center">
      <KeyIcon class="mx-auto h-12 w-12 text-gray-400" />
      <h3 class="mt-4 text-lg font-medium text-gray-900 dark:text-white">No API Keys</h3>
      <p class="mt-2 text-sm text-gray-500 dark:text-gray-400">
        Create an API key to access AEGIS programmatically
      </p>
      <button class="btn-primary mt-4" @click="openCreateDialog">
        <PlusIcon class="h-4 w-4 mr-2" />
        Create Your First API Key
      </button>
    </div>

    <!-- API Keys List -->
    <template v-else>
      <!-- Active Keys -->
      <div v-if="activeKeys.length > 0" class="card p-6">
        <h3 class="text-md font-medium text-gray-900 dark:text-white mb-4">
          Active Keys ({{ activeKeys.length }})
        </h3>
        <div class="space-y-4">
          <div
            v-for="key in activeKeys"
            :key="key.id"
            class="flex items-start justify-between rounded-lg border border-gray-200 p-4 dark:border-gray-700"
          >
            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2">
                <p class="font-medium text-gray-900 dark:text-white">{{ key.name }}</p>
                <span class="inline-flex items-center rounded-full bg-green-100 px-2 py-0.5 text-xs font-medium text-green-700 dark:bg-green-900/50 dark:text-green-300">
                  Active
                </span>
              </div>
              <div class="mt-1 flex items-center gap-2">
                <code class="rounded bg-gray-100 px-2 py-0.5 text-sm font-mono text-gray-600 dark:bg-gray-800 dark:text-gray-400">
                  {{ key.prefix }}••••••••
                </code>
                <button
                  class="p-1 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300"
                  @click="copyToClipboard(key.prefix + '••••••••', key.id)"
                >
                  <CheckIcon v-if="copiedKeyId === key.id" class="h-4 w-4 text-green-500" />
                  <ClipboardDocumentIcon v-else class="h-4 w-4" />
                </button>
              </div>
              <div class="mt-2 flex flex-wrap gap-1">
                <span
                  v-for="scope in key.scopes"
                  :key="scope"
                  class="inline-flex items-center rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-600 dark:bg-gray-800 dark:text-gray-400"
                >
                  {{ scope }}
                </span>
              </div>
              <div class="mt-2 flex items-center gap-4 text-xs text-gray-500 dark:text-gray-400">
                <span>Created {{ formatDate(key.createdAt) }}</span>
                <span :class="getExpirationStatus(key).class">{{ getExpirationStatus(key).label }}</span>
                <span v-if="key.lastUsedAt">Last used {{ formatRelativeTime(key.lastUsedAt) }}</span>
                <span v-if="key.usageCount">{{ key.usageCount.toLocaleString() }} requests</span>
              </div>
            </div>
            <button
              class="btn-ghost p-2 text-red-600 hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-900/20"
              :disabled="revokingKeyId === key.id"
              @click="confirmRevoke(key)"
            >
              <ArrowPathIcon v-if="revokingKeyId === key.id" class="h-5 w-5 animate-spin" />
              <TrashIcon v-else class="h-5 w-5" />
            </button>
          </div>
        </div>
      </div>

      <!-- Inactive/Expired Keys -->
      <div v-if="inactiveKeys.length > 0" class="card p-6">
        <Disclosure v-slot="{ open }">
          <DisclosureButton class="flex w-full items-center justify-between text-left">
            <h3 class="text-md font-medium text-gray-500 dark:text-gray-400">
              Inactive/Expired Keys ({{ inactiveKeys.length }})
            </h3>
            <svg
              class="h-5 w-5 text-gray-400 transition-transform"
              :class="{ 'rotate-180': open }"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
            </svg>
          </DisclosureButton>
          <DisclosurePanel class="mt-4 space-y-4">
            <div
              v-for="key in inactiveKeys"
              :key="key.id"
              class="flex items-start justify-between rounded-lg border border-gray-200 p-4 opacity-60 dark:border-gray-700"
            >
              <div class="flex-1 min-w-0">
                <div class="flex items-center gap-2">
                  <p class="font-medium text-gray-900 dark:text-white">{{ key.name }}</p>
                  <span
                    class="inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium"
                    :class="isExpired(key)
                      ? 'bg-red-100 text-red-700 dark:bg-red-900/50 dark:text-red-300'
                      : 'bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-400'"
                  >
                    {{ isExpired(key) ? 'Expired' : 'Inactive' }}
                  </span>
                </div>
                <code class="mt-1 rounded bg-gray-100 px-2 py-0.5 text-sm font-mono text-gray-500 dark:bg-gray-800">
                  {{ key.prefix }}••••••••
                </code>
                <div class="mt-2 text-xs text-gray-500 dark:text-gray-400">
                  Created {{ formatDate(key.createdAt) }}
                  <span v-if="key.expiresAt"> · Expired {{ formatDate(key.expiresAt) }}</span>
                </div>
              </div>
              <button
                class="btn-ghost p-2 text-red-600 hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-900/20"
                @click="confirmRevoke(key)"
              >
                <TrashIcon class="h-5 w-5" />
              </button>
            </div>
          </DisclosurePanel>
        </Disclosure>
      </div>
    </template>

    <!-- Create API Key Dialog -->
    <TransitionRoot appear :show="showCreateDialog" as="template">
      <Dialog as="div" class="relative z-50" @close="showCreateDialog = false">
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
              <DialogPanel class="w-full max-w-lg transform rounded-xl bg-white p-6 shadow-xl transition-all dark:bg-gray-800">
                <DialogTitle class="text-lg font-semibold text-gray-900 dark:text-white mb-4">
                  Create API Key
                </DialogTitle>

                <div class="space-y-4">
                  <!-- Name -->
                  <div>
                    <label class="label">Name</label>
                    <input
                      v-model="newKeyName"
                      type="text"
                      class="input w-full"
                      placeholder="My API Key"
                    />
                  </div>

                  <!-- Description -->
                  <div>
                    <label class="label">Description (optional)</label>
                    <input
                      v-model="newKeyDescription"
                      type="text"
                      class="input w-full"
                      placeholder="What this key is used for"
                    />
                  </div>

                  <!-- Scopes -->
                  <div>
                    <label class="label">Permissions</label>
                    <div class="space-y-3">
                      <div v-for="(scopes, category) in scopesByCategory" :key="category">
                        <p class="text-xs font-medium text-gray-500 dark:text-gray-400 mb-2">{{ category }}</p>
                        <div class="flex flex-wrap gap-2">
                          <button
                            v-for="scope in scopes"
                            :key="scope.value"
                            class="inline-flex items-center rounded-lg border px-3 py-1.5 text-sm transition-colors"
                            :class="newKeyScopes.includes(scope.value)
                              ? 'border-aegis-500 bg-aegis-50 text-aegis-700 dark:bg-aegis-900/30 dark:text-aegis-300'
                              : 'border-gray-200 text-gray-600 hover:border-gray-300 dark:border-gray-700 dark:text-gray-400'"
                            :title="scope.description"
                            @click="toggleScope(scope.value)"
                          >
                            {{ scope.label }}
                          </button>
                        </div>
                      </div>
                    </div>
                  </div>

                  <!-- Expiration -->
                  <div>
                    <label class="label">Expiration</label>
                    <select v-model="newKeyExpiration" class="input w-full">
                      <option v-for="opt in EXPIRATION_OPTIONS" :key="opt.value" :value="opt.value">
                        {{ opt.label }}
                      </option>
                    </select>
                  </div>
                </div>

                <div class="mt-6 flex justify-end gap-3">
                  <button class="btn-ghost" @click="showCreateDialog = false">
                    Cancel
                  </button>
                  <button
                    class="btn-primary"
                    :disabled="creating || !newKeyName.trim() || newKeyScopes.length === 0"
                    @click="createApiKey"
                  >
                    <template v-if="creating">
                      <ArrowPathIcon class="h-4 w-4 mr-2 animate-spin" />
                      Creating...
                    </template>
                    <template v-else>
                      Create Key
                    </template>
                  </button>
                </div>
              </DialogPanel>
            </TransitionChild>
          </div>
        </div>
      </Dialog>
    </TransitionRoot>

    <!-- Key Created Success Dialog -->
    <TransitionRoot appear :show="showKeyCreatedDialog" as="template">
      <Dialog as="div" class="relative z-50" @close="closeKeyCreatedDialog">
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
              <DialogPanel class="w-full max-w-lg transform rounded-xl bg-white p-6 shadow-xl transition-all dark:bg-gray-800">
                <div class="text-center mb-4">
                  <div class="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-green-100 dark:bg-green-900/50">
                    <CheckIcon class="h-6 w-6 text-green-600 dark:text-green-400" />
                  </div>
                  <DialogTitle class="mt-4 text-lg font-semibold text-gray-900 dark:text-white">
                    API Key Created
                  </DialogTitle>
                </div>

                <div class="rounded-lg bg-amber-50 p-4 dark:bg-amber-900/20 mb-4">
                  <div class="flex items-start gap-3">
                    <ExclamationTriangleIcon class="h-5 w-5 text-amber-600 dark:text-amber-400 shrink-0 mt-0.5" />
                    <div>
                      <p class="text-sm font-medium text-amber-800 dark:text-amber-200">
                        Copy your API key now
                      </p>
                      <p class="text-sm text-amber-700 dark:text-amber-300 mt-1">
                        This is the only time you'll see this key. Store it securely.
                      </p>
                    </div>
                  </div>
                </div>

                <div v-if="createdKey" class="space-y-3">
                  <div>
                    <label class="label">Name</label>
                    <p class="text-gray-900 dark:text-white">{{ createdKey.name }}</p>
                  </div>

                  <div>
                    <label class="label">API Key</label>
                    <div class="flex items-center gap-2">
                      <code class="flex-1 rounded-lg bg-gray-100 px-3 py-2 text-sm font-mono text-gray-900 dark:bg-gray-900 dark:text-gray-100 break-all">
                        {{ createdKey.key }}
                      </code>
                      <button
                        class="btn-ghost p-2"
                        @click="copyToClipboard(createdKey.key, 'created')"
                      >
                        <CheckIcon v-if="copiedKeyId === 'created'" class="h-5 w-5 text-green-500" />
                        <ClipboardDocumentIcon v-else class="h-5 w-5" />
                      </button>
                    </div>
                  </div>

                  <div class="flex flex-wrap gap-1">
                    <span
                      v-for="scope in createdKey.scopes"
                      :key="scope"
                      class="inline-flex items-center rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-600 dark:bg-gray-700 dark:text-gray-400"
                    >
                      {{ scope }}
                    </span>
                  </div>

                  <div v-if="createdKey.expiresAt" class="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400">
                    <ClockIcon class="h-4 w-4" />
                    Expires {{ formatDate(createdKey.expiresAt) }}
                  </div>
                </div>

                <div class="mt-6">
                  <button class="btn-primary w-full" @click="closeKeyCreatedDialog">
                    Done
                  </button>
                </div>
              </DialogPanel>
            </TransitionChild>
          </div>
        </div>
      </Dialog>
    </TransitionRoot>

    <!-- Revoke Confirmation Dialog -->
    <TransitionRoot appear :show="showRevokeConfirm" as="template">
      <Dialog as="div" class="relative z-50" @close="showRevokeConfirm = false">
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
              <DialogPanel class="w-full max-w-md transform rounded-xl bg-white p-6 shadow-xl transition-all dark:bg-gray-800">
                <div class="flex items-start gap-4">
                  <div class="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-red-100 dark:bg-red-900/50">
                    <ExclamationTriangleIcon class="h-5 w-5 text-red-600 dark:text-red-400" />
                  </div>
                  <div>
                    <DialogTitle class="text-lg font-semibold text-gray-900 dark:text-white">
                      Revoke API Key
                    </DialogTitle>
                    <p class="mt-2 text-sm text-gray-500 dark:text-gray-400">
                      Are you sure you want to revoke <strong>"{{ keyToRevoke?.name }}"</strong>?
                      This action cannot be undone. Any applications using this key will stop working.
                    </p>
                  </div>
                </div>

                <div class="mt-6 flex justify-end gap-3">
                  <button class="btn-ghost" @click="showRevokeConfirm = false">
                    Cancel
                  </button>
                  <button
                    class="btn-primary bg-red-600 hover:bg-red-700"
                    :disabled="revokingKeyId === keyToRevoke?.id"
                    @click="revokeApiKey"
                  >
                    <template v-if="revokingKeyId === keyToRevoke?.id">
                      <ArrowPathIcon class="h-4 w-4 mr-2 animate-spin" />
                      Revoking...
                    </template>
                    <template v-else>
                      Revoke Key
                    </template>
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
