<script setup lang="ts">
import { ref, computed } from 'vue'
import { Switch } from '@headlessui/vue'
import {
  FlagIcon,
  PlusIcon,
  PencilIcon,
  TrashIcon,
  MagnifyingGlassIcon
} from '@heroicons/vue/24/outline'
import type { FeatureFlag, CreateFeatureFlagRequest, UpdateFeatureFlagRequest } from '@/types'

const props = defineProps<{
  featureFlags: FeatureFlag[]
  isLoading?: boolean
  isSaving?: boolean
}>()

const emit = defineEmits<{
  create: [request: CreateFeatureFlagRequest]
  update: [id: string, request: UpdateFeatureFlagRequest]
  toggle: [id: string]
  delete: [id: string]
}>()

const searchQuery = ref('')
const showCreateDialog = ref(false)
const editingFlag = ref<FeatureFlag | null>(null)

// Form state
const formData = ref<CreateFeatureFlagRequest>({
  name: '',
  key: '',
  description: '',
  enabled: false,
  enabledForRoles: [],
  rolloutPercentage: 0
})

const availableRoles = ['Viewer', 'Contributor', 'Analyst', 'Admin', 'SystemAdmin']

const filteredFlags = computed(() => {
  if (!searchQuery.value.trim()) return props.featureFlags
  const query = searchQuery.value.toLowerCase()
  return props.featureFlags.filter(
    f => f.name.toLowerCase().includes(query) ||
         f.key.toLowerCase().includes(query) ||
         f.description.toLowerCase().includes(query)
  )
})

const enabledCount = computed(() => props.featureFlags.filter(f => f.enabled).length)

function openCreateDialog() {
  formData.value = {
    name: '',
    key: '',
    description: '',
    enabled: false,
    enabledForRoles: [],
    rolloutPercentage: 0
  }
  editingFlag.value = null
  showCreateDialog.value = true
}

function openEditDialog(flag: FeatureFlag) {
  formData.value = {
    name: flag.name,
    key: flag.key,
    description: flag.description,
    enabled: flag.enabled,
    enabledForRoles: [...flag.enabledForRoles],
    rolloutPercentage: flag.rolloutPercentage
  }
  editingFlag.value = flag
  showCreateDialog.value = true
}

function handleSubmit() {
  if (editingFlag.value) {
    emit('update', editingFlag.value.id, {
      name: formData.value.name,
      description: formData.value.description,
      enabled: formData.value.enabled,
      enabledForRoles: formData.value.enabledForRoles,
      rolloutPercentage: formData.value.rolloutPercentage
    })
  } else {
    emit('create', formData.value)
  }
  showCreateDialog.value = false
}

function handleDelete(flag: FeatureFlag) {
  if (confirm(`Are you sure you want to delete "${flag.name}"?`)) {
    emit('delete', flag.id)
  }
}

function generateKey(name: string): string {
  return name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '')
}

function toggleRole(role: string) {
  const index = formData.value.enabledForRoles?.indexOf(role) ?? -1
  if (index === -1) {
    formData.value.enabledForRoles = [...(formData.value.enabledForRoles || []), role]
  } else {
    formData.value.enabledForRoles = formData.value.enabledForRoles?.filter(r => r !== role)
  }
}

function formatDate(dateString: string): string {
  return new Date(dateString).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric'
  })
}
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <div class="flex items-center justify-between">
      <div>
        <h3 class="text-lg font-medium text-gray-900 dark:text-white">Feature Flags</h3>
        <p class="text-sm text-gray-500 dark:text-gray-400">
          {{ enabledCount }} of {{ featureFlags.length }} flags enabled
        </p>
      </div>
      <button
        type="button"
        class="inline-flex items-center gap-2 rounded-lg bg-aegis-600 px-4 py-2 text-sm font-medium text-white hover:bg-aegis-700"
        @click="openCreateDialog"
      >
        <PlusIcon class="h-4 w-4" />
        Add Flag
      </button>
    </div>

    <!-- Search -->
    <div class="relative">
      <MagnifyingGlassIcon class="absolute left-3 top-1/2 h-5 w-5 -translate-y-1/2 text-gray-400" />
      <input
        v-model="searchQuery"
        type="text"
        placeholder="Search feature flags..."
        class="w-full rounded-lg border-gray-300 pl-10 pr-4 py-2 text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white dark:placeholder-gray-400"
      />
    </div>

    <!-- Loading state -->
    <div v-if="isLoading" class="animate-pulse space-y-4">
      <div v-for="i in 4" :key="i" class="h-24 rounded-lg bg-gray-200 dark:bg-gray-700" />
    </div>

    <!-- Flags list -->
    <div v-else class="space-y-4">
      <div
        v-for="flag in filteredFlags"
        :key="flag.id"
        class="rounded-lg border bg-white p-4 dark:border-gray-700 dark:bg-gray-800"
      >
        <div class="flex items-start gap-4">
          <div class="flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-lg" :class="flag.enabled ? 'bg-green-100 dark:bg-green-900/30' : 'bg-gray-100 dark:bg-gray-700'">
            <FlagIcon class="h-5 w-5" :class="flag.enabled ? 'text-green-600 dark:text-green-400' : 'text-gray-400'" />
          </div>

          <div class="min-w-0 flex-1">
            <div class="flex items-center gap-2">
              <h4 class="font-medium text-gray-900 dark:text-white">{{ flag.name }}</h4>
              <code class="rounded bg-gray-100 px-1.5 py-0.5 text-xs text-gray-600 dark:bg-gray-700 dark:text-gray-300">
                {{ flag.key }}
              </code>
            </div>
            <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">{{ flag.description }}</p>

            <div class="mt-3 flex flex-wrap items-center gap-4 text-sm">
              <!-- Rollout percentage -->
              <div class="flex items-center gap-2">
                <span class="text-gray-500 dark:text-gray-400">Rollout:</span>
                <div class="flex items-center gap-1">
                  <div class="h-2 w-20 overflow-hidden rounded-full bg-gray-200 dark:bg-gray-700">
                    <div
                      class="h-full bg-aegis-500"
                      :style="{ width: `${flag.rolloutPercentage}%` }"
                    />
                  </div>
                  <span class="text-gray-700 dark:text-gray-300">{{ flag.rolloutPercentage }}%</span>
                </div>
              </div>

              <!-- Roles -->
              <div v-if="flag.enabledForRoles.length > 0" class="flex items-center gap-1">
                <span class="text-gray-500 dark:text-gray-400">Roles:</span>
                <span
                  v-for="role in flag.enabledForRoles"
                  :key="role"
                  class="rounded bg-blue-100 px-1.5 py-0.5 text-xs text-blue-700 dark:bg-blue-900/30 dark:text-blue-300"
                >
                  {{ role }}
                </span>
              </div>

              <!-- Updated date -->
              <span class="text-gray-400 dark:text-gray-500">
                Updated {{ formatDate(flag.updatedAt) }}
              </span>
            </div>
          </div>

          <div class="flex items-center gap-2">
            <button
              type="button"
              class="rounded p-1.5 text-gray-400 hover:bg-gray-100 hover:text-gray-600 dark:hover:bg-gray-700"
              @click="openEditDialog(flag)"
            >
              <PencilIcon class="h-4 w-4" />
            </button>
            <button
              type="button"
              class="rounded p-1.5 text-gray-400 hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-900/20"
              @click="handleDelete(flag)"
            >
              <TrashIcon class="h-4 w-4" />
            </button>
            <Switch
              :model-value="flag.enabled"
              :class="[
                flag.enabled ? 'bg-green-500' : 'bg-gray-200 dark:bg-gray-600',
                'relative inline-flex h-6 w-11 flex-shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-aegis-500 focus:ring-offset-2 dark:focus:ring-offset-gray-800'
              ]"
              @update:model-value="emit('toggle', flag.id)"
            >
              <span
                :class="[
                  flag.enabled ? 'translate-x-5' : 'translate-x-0',
                  'pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out'
                ]"
              />
            </Switch>
          </div>
        </div>
      </div>

      <div
        v-if="filteredFlags.length === 0"
        class="py-12 text-center"
      >
        <FlagIcon class="mx-auto h-12 w-12 text-gray-400" />
        <h3 class="mt-2 text-sm font-medium text-gray-900 dark:text-white">No feature flags</h3>
        <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
          {{ searchQuery ? 'No flags match your search.' : 'Get started by creating a new feature flag.' }}
        </p>
      </div>
    </div>

    <!-- Create/Edit Dialog -->
    <Teleport to="body">
      <div
        v-if="showCreateDialog"
        class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
        @click.self="showCreateDialog = false"
      >
        <div class="w-full max-w-lg rounded-lg bg-white p-6 shadow-xl dark:bg-gray-800">
          <h3 class="text-lg font-medium text-gray-900 dark:text-white">
            {{ editingFlag ? 'Edit Feature Flag' : 'Create Feature Flag' }}
          </h3>

          <form class="mt-4 space-y-4" @submit.prevent="handleSubmit">
            <div>
              <label class="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Name</label>
              <input
                v-model="formData.name"
                type="text"
                required
                class="w-full rounded-lg border-gray-300 text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                placeholder="New Feature Name"
                @input="!editingFlag && (formData.key = generateKey(formData.name))"
              />
            </div>

            <div>
              <label class="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Key</label>
              <input
                v-model="formData.key"
                type="text"
                required
                :disabled="!!editingFlag"
                class="w-full rounded-lg border-gray-300 text-sm focus:border-aegis-500 focus:ring-aegis-500 disabled:bg-gray-100 dark:border-gray-600 dark:bg-gray-700 dark:text-white dark:disabled:bg-gray-600"
                placeholder="new-feature-name"
              />
              <p class="mt-1 text-xs text-gray-500">Unique identifier used in code</p>
            </div>

            <div>
              <label class="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Description</label>
              <textarea
                v-model="formData.description"
                rows="2"
                class="w-full rounded-lg border-gray-300 text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                placeholder="Describe what this feature flag controls..."
              />
            </div>

            <div>
              <label class="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
                Rollout Percentage: {{ formData.rolloutPercentage }}%
              </label>
              <input
                v-model.number="formData.rolloutPercentage"
                type="range"
                min="0"
                max="100"
                step="5"
                class="w-full"
              />
              <div class="flex justify-between text-xs text-gray-500">
                <span>0%</span>
                <span>50%</span>
                <span>100%</span>
              </div>
            </div>

            <div>
              <label class="mb-2 block text-sm font-medium text-gray-700 dark:text-gray-300">
                Enabled for Roles
              </label>
              <div class="flex flex-wrap gap-2">
                <button
                  v-for="role in availableRoles"
                  :key="role"
                  type="button"
                  :class="[
                    'rounded-lg px-3 py-1.5 text-sm font-medium transition-colors',
                    formData.enabledForRoles?.includes(role)
                      ? 'bg-aegis-100 text-aegis-700 dark:bg-aegis-900/30 dark:text-aegis-300'
                      : 'bg-gray-100 text-gray-600 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600'
                  ]"
                  @click="toggleRole(role)"
                >
                  {{ role }}
                </button>
              </div>
              <p class="mt-1 text-xs text-gray-500">Leave empty to apply to all users based on rollout percentage</p>
            </div>

            <div class="flex items-center gap-3">
              <Switch
                v-model="formData.enabled"
                :class="[
                  formData.enabled ? 'bg-green-500' : 'bg-gray-200 dark:bg-gray-600',
                  'relative inline-flex h-6 w-11 flex-shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-aegis-500 focus:ring-offset-2'
                ]"
              >
                <span
                  :class="[
                    formData.enabled ? 'translate-x-5' : 'translate-x-0',
                    'pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out'
                  ]"
                />
              </Switch>
              <span class="text-sm text-gray-700 dark:text-gray-300">
                {{ formData.enabled ? 'Enabled' : 'Disabled' }}
              </span>
            </div>

            <div class="flex justify-end gap-3 pt-4">
              <button
                type="button"
                class="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-600 dark:text-gray-300 dark:hover:bg-gray-700"
                @click="showCreateDialog = false"
              >
                Cancel
              </button>
              <button
                type="submit"
                class="rounded-lg bg-aegis-600 px-4 py-2 text-sm font-medium text-white hover:bg-aegis-700 disabled:opacity-50"
                :disabled="isSaving"
              >
                {{ isSaving ? 'Saving...' : (editingFlag ? 'Update' : 'Create') }}
              </button>
            </div>
          </form>
        </div>
      </div>
    </Teleport>
  </div>
</template>
