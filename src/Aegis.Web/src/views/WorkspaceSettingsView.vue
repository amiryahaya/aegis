<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useWorkspaceStore } from '@/stores/workspace'
import { useToast } from '@/composables/useToast'
import UserAccessManager from '@/components/workspace/UserAccessManager.vue'
import ShareableLinkGenerator from '@/components/workspace/ShareableLinkGenerator.vue'
import type { WorkspaceSettings, SearchMode } from '@/types/workspace'
import {
  ArrowLeftIcon,
  Cog6ToothIcon,
  MagnifyingGlassIcon,
  CpuChipIcon,
  UserGroupIcon,
  ExclamationTriangleIcon,
  CheckIcon
} from '@heroicons/vue/24/outline'
import {
  TabGroup,
  TabList,
  Tab,
  TabPanels,
  TabPanel,
  Listbox,
  ListboxButton,
  ListboxOptions,
  ListboxOption,
  Switch
} from '@headlessui/vue'

const route = useRoute()
const router = useRouter()
const workspaceStore = useWorkspaceStore()
const toast = useToast()

const workspaceId = computed(() => route.params.workspaceId as string)
const workspace = computed(() => workspaceStore.currentWorkspace)

const saving = ref(false)
const deleting = ref(false)
const deleteConfirmation = ref('')

// Form state
const form = ref({
  name: '',
  description: '',
  settings: {
    defaultSearchMode: 'Hybrid' as SearchMode,
    maxResults: 10,
    enableCaching: true,
    enableFollowUps: true,
    llmModel: 'gpt-4',
    temperature: 0.7,
    systemPrompt: ''
  } as WorkspaceSettings
})

const searchModes: { value: SearchMode; label: string; description: string }[] = [
  { value: 'Semantic', label: 'Semantic', description: 'Vector similarity search using embeddings' },
  { value: 'Keyword', label: 'Keyword', description: 'Traditional keyword-based BM25 search' },
  { value: 'Hybrid', label: 'Hybrid', description: 'Combines semantic and keyword search for best results' }
]

const llmModels = [
  { value: 'gpt-4', label: 'GPT-4', description: 'Most capable, best for complex analysis' },
  { value: 'gpt-4-turbo', label: 'GPT-4 Turbo', description: 'Faster, good balance of speed and quality' },
  { value: 'gpt-3.5-turbo', label: 'GPT-3.5 Turbo', description: 'Fast and cost-effective' },
  { value: 'claude-3-opus', label: 'Claude 3 Opus', description: 'Advanced reasoning capabilities' },
  { value: 'claude-3-sonnet', label: 'Claude 3 Sonnet', description: 'Balanced performance' },
  { value: 'ollama-llama3', label: 'Llama 3 (Local)', description: 'Run locally with Ollama' }
]

const maxResultsOptions = [5, 10, 15, 20, 25, 50]

onMounted(async () => {
  if (workspaceId.value) {
    await workspaceStore.fetchWorkspace(workspaceId.value)
    if (workspace.value) {
      form.value.name = workspace.value.name
      form.value.description = workspace.value.description || ''
      if (workspace.value.settings) {
        form.value.settings = { ...form.value.settings, ...workspace.value.settings }
      }
    }
  }
})

watch(workspace, (ws) => {
  if (ws) {
    form.value.name = ws.name
    form.value.description = ws.description || ''
    if (ws.settings) {
      form.value.settings = { ...form.value.settings, ...ws.settings }
    }
  }
})

async function saveGeneralSettings() {
  saving.value = true
  try {
    await workspaceStore.updateWorkspace(workspaceId.value, {
      name: form.value.name,
      description: form.value.description
    })
    toast.success('Settings saved', 'General settings have been updated')
  } catch (error) {
    toast.error('Error', 'Failed to save settings')
  } finally {
    saving.value = false
  }
}

async function saveSearchSettings() {
  saving.value = true
  try {
    await workspaceStore.updateWorkspace(workspaceId.value, {
      settings: {
        defaultSearchMode: form.value.settings.defaultSearchMode,
        maxResults: form.value.settings.maxResults,
        enableCaching: form.value.settings.enableCaching,
        enableFollowUps: form.value.settings.enableFollowUps
      }
    })
    toast.success('Settings saved', 'Search settings have been updated')
  } catch (error) {
    toast.error('Error', 'Failed to save search settings')
  } finally {
    saving.value = false
  }
}

async function saveLLMSettings() {
  saving.value = true
  try {
    await workspaceStore.updateWorkspace(workspaceId.value, {
      settings: {
        llmModel: form.value.settings.llmModel,
        temperature: form.value.settings.temperature,
        systemPrompt: form.value.settings.systemPrompt
      }
    })
    toast.success('Settings saved', 'LLM settings have been updated')
  } catch (error) {
    toast.error('Error', 'Failed to save LLM settings')
  } finally {
    saving.value = false
  }
}

async function archiveWorkspace() {
  if (!confirm('Are you sure you want to archive this workspace? It can be restored later.')) return

  try {
    await workspaceStore.archiveWorkspace(workspaceId.value)
    toast.success('Workspace archived', 'The workspace has been archived')
    router.push('/workspaces')
  } catch (error) {
    toast.error('Error', 'Failed to archive workspace')
  }
}

async function deleteWorkspace() {
  if (deleteConfirmation.value !== workspace.value?.name) {
    toast.error('Confirmation required', 'Please type the workspace name to confirm deletion')
    return
  }

  deleting.value = true
  try {
    await workspaceStore.deleteWorkspace(workspaceId.value)
    toast.success('Workspace deleted', 'The workspace has been permanently deleted')
    router.push('/workspaces')
  } catch (error) {
    toast.error('Error', 'Failed to delete workspace')
  } finally {
    deleting.value = false
  }
}

const canDelete = computed(() => deleteConfirmation.value === workspace.value?.name)
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
        <div class="flex items-center gap-4">
          <button
            class="p-2 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-700"
            @click="router.push(`/workspaces/${workspaceId}`)"
          >
            <ArrowLeftIcon class="h-5 w-5 text-gray-500" />
          </button>
          <div>
            <h1 class="text-xl font-bold text-gray-900 dark:text-white">
              Workspace Settings
            </h1>
            <p class="text-sm text-gray-500 dark:text-gray-400">
              {{ workspace.name }}
            </p>
          </div>
        </div>
      </div>

      <!-- Settings Tabs -->
      <div class="flex-1 overflow-auto">
        <div class="max-w-4xl mx-auto px-6 py-6">
          <TabGroup>
            <TabList class="flex space-x-1 rounded-xl bg-gray-100 p-1 dark:bg-gray-800">
              <Tab v-slot="{ selected }" as="template">
                <button
                  class="flex items-center gap-2 w-full rounded-lg py-2.5 px-4 text-sm font-medium leading-5 transition-colors"
                  :class="selected
                    ? 'bg-white text-aegis-700 shadow dark:bg-gray-700 dark:text-aegis-400'
                    : 'text-gray-600 hover:bg-white/50 hover:text-gray-900 dark:text-gray-400 dark:hover:bg-gray-700/50'"
                >
                  <Cog6ToothIcon class="h-4 w-4" />
                  General
                </button>
              </Tab>
              <Tab v-slot="{ selected }" as="template">
                <button
                  class="flex items-center gap-2 w-full rounded-lg py-2.5 px-4 text-sm font-medium leading-5 transition-colors"
                  :class="selected
                    ? 'bg-white text-aegis-700 shadow dark:bg-gray-700 dark:text-aegis-400'
                    : 'text-gray-600 hover:bg-white/50 hover:text-gray-900 dark:text-gray-400 dark:hover:bg-gray-700/50'"
                >
                  <MagnifyingGlassIcon class="h-4 w-4" />
                  Search
                </button>
              </Tab>
              <Tab v-slot="{ selected }" as="template">
                <button
                  class="flex items-center gap-2 w-full rounded-lg py-2.5 px-4 text-sm font-medium leading-5 transition-colors"
                  :class="selected
                    ? 'bg-white text-aegis-700 shadow dark:bg-gray-700 dark:text-aegis-400'
                    : 'text-gray-600 hover:bg-white/50 hover:text-gray-900 dark:text-gray-400 dark:hover:bg-gray-700/50'"
                >
                  <CpuChipIcon class="h-4 w-4" />
                  LLM
                </button>
              </Tab>
              <Tab v-slot="{ selected }" as="template">
                <button
                  class="flex items-center gap-2 w-full rounded-lg py-2.5 px-4 text-sm font-medium leading-5 transition-colors"
                  :class="selected
                    ? 'bg-white text-aegis-700 shadow dark:bg-gray-700 dark:text-aegis-400'
                    : 'text-gray-600 hover:bg-white/50 hover:text-gray-900 dark:text-gray-400 dark:hover:bg-gray-700/50'"
                >
                  <UserGroupIcon class="h-4 w-4" />
                  Access
                </button>
              </Tab>
              <Tab v-slot="{ selected }" as="template">
                <button
                  class="flex items-center gap-2 w-full rounded-lg py-2.5 px-4 text-sm font-medium leading-5 transition-colors"
                  :class="selected
                    ? 'bg-white text-red-700 shadow dark:bg-gray-700 dark:text-red-400'
                    : 'text-gray-600 hover:bg-white/50 hover:text-gray-900 dark:text-gray-400 dark:hover:bg-gray-700/50'"
                >
                  <ExclamationTriangleIcon class="h-4 w-4" />
                  Danger
                </button>
              </Tab>
            </TabList>

            <TabPanels class="mt-6">
              <!-- General Settings -->
              <TabPanel>
                <div class="bg-white rounded-lg border border-gray-200 p-6 dark:bg-gray-800 dark:border-gray-700">
                  <h2 class="text-lg font-semibold text-gray-900 dark:text-white mb-4">
                    General Settings
                  </h2>
                  <div class="space-y-4">
                    <div>
                      <label class="label">Workspace Name</label>
                      <input
                        v-model="form.name"
                        type="text"
                        class="input w-full"
                        placeholder="My Workspace"
                      />
                    </div>
                    <div>
                      <label class="label">Description</label>
                      <textarea
                        v-model="form.description"
                        rows="3"
                        class="input w-full"
                        placeholder="Describe the purpose of this workspace..."
                      />
                    </div>
                    <div class="pt-4">
                      <button
                        class="btn-primary"
                        :disabled="saving || !form.name.trim()"
                        @click="saveGeneralSettings"
                      >
                        <span v-if="saving">Saving...</span>
                        <span v-else>Save Changes</span>
                      </button>
                    </div>
                  </div>
                </div>
              </TabPanel>

              <!-- Search Settings -->
              <TabPanel>
                <div class="bg-white rounded-lg border border-gray-200 p-6 dark:bg-gray-800 dark:border-gray-700">
                  <h2 class="text-lg font-semibold text-gray-900 dark:text-white mb-4">
                    Search Settings
                  </h2>
                  <div class="space-y-6">
                    <!-- Search Mode -->
                    <div>
                      <label class="label">Default Search Mode</label>
                      <Listbox v-model="form.settings.defaultSearchMode">
                        <div class="relative mt-1">
                          <ListboxButton class="input w-full text-left flex items-center justify-between">
                            <span>{{ searchModes.find(m => m.value === form.settings.defaultSearchMode)?.label }}</span>
                            <CheckIcon class="h-4 w-4 text-gray-400" />
                          </ListboxButton>
                          <ListboxOptions class="absolute z-10 mt-1 w-full bg-white rounded-md shadow-lg border border-gray-200 dark:bg-gray-800 dark:border-gray-700">
                            <ListboxOption
                              v-for="mode in searchModes"
                              :key="mode.value"
                              :value="mode.value"
                              v-slot="{ selected, active }"
                            >
                              <div
                                class="cursor-pointer select-none px-4 py-3"
                                :class="active ? 'bg-gray-100 dark:bg-gray-700' : ''"
                              >
                                <div class="flex items-center justify-between">
                                  <span class="font-medium text-gray-900 dark:text-white">{{ mode.label }}</span>
                                  <CheckIcon v-if="selected" class="h-4 w-4 text-aegis-600" />
                                </div>
                                <p class="text-sm text-gray-500 dark:text-gray-400">{{ mode.description }}</p>
                              </div>
                            </ListboxOption>
                          </ListboxOptions>
                        </div>
                      </Listbox>
                    </div>

                    <!-- Max Results -->
                    <div>
                      <label class="label">Maximum Results</label>
                      <select v-model.number="form.settings.maxResults" class="input w-full">
                        <option v-for="n in maxResultsOptions" :key="n" :value="n">{{ n }} results</option>
                      </select>
                      <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
                        Maximum number of documents to retrieve per query
                      </p>
                    </div>

                    <!-- Toggles -->
                    <div class="space-y-4">
                      <div class="flex items-center justify-between">
                        <div>
                          <p class="font-medium text-gray-900 dark:text-white">Enable Caching</p>
                          <p class="text-sm text-gray-500 dark:text-gray-400">Cache query results for faster responses</p>
                        </div>
                        <Switch
                          v-model="form.settings.enableCaching"
                          :class="form.settings.enableCaching ? 'bg-aegis-600' : 'bg-gray-200 dark:bg-gray-700'"
                          class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors"
                        >
                          <span
                            :class="form.settings.enableCaching ? 'translate-x-6' : 'translate-x-1'"
                            class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform"
                          />
                        </Switch>
                      </div>

                      <div class="flex items-center justify-between">
                        <div>
                          <p class="font-medium text-gray-900 dark:text-white">Enable Follow-up Questions</p>
                          <p class="text-sm text-gray-500 dark:text-gray-400">Generate suggested follow-up questions</p>
                        </div>
                        <Switch
                          v-model="form.settings.enableFollowUps"
                          :class="form.settings.enableFollowUps ? 'bg-aegis-600' : 'bg-gray-200 dark:bg-gray-700'"
                          class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors"
                        >
                          <span
                            :class="form.settings.enableFollowUps ? 'translate-x-6' : 'translate-x-1'"
                            class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform"
                          />
                        </Switch>
                      </div>
                    </div>

                    <div class="pt-4">
                      <button
                        class="btn-primary"
                        :disabled="saving"
                        @click="saveSearchSettings"
                      >
                        <span v-if="saving">Saving...</span>
                        <span v-else>Save Changes</span>
                      </button>
                    </div>
                  </div>
                </div>
              </TabPanel>

              <!-- LLM Settings -->
              <TabPanel>
                <div class="bg-white rounded-lg border border-gray-200 p-6 dark:bg-gray-800 dark:border-gray-700">
                  <h2 class="text-lg font-semibold text-gray-900 dark:text-white mb-4">
                    LLM Settings
                  </h2>
                  <div class="space-y-6">
                    <!-- Model Selection -->
                    <div>
                      <label class="label">Language Model</label>
                      <Listbox v-model="form.settings.llmModel">
                        <div class="relative mt-1">
                          <ListboxButton class="input w-full text-left flex items-center justify-between">
                            <span>{{ llmModels.find(m => m.value === form.settings.llmModel)?.label || form.settings.llmModel }}</span>
                            <CheckIcon class="h-4 w-4 text-gray-400" />
                          </ListboxButton>
                          <ListboxOptions class="absolute z-10 mt-1 w-full bg-white rounded-md shadow-lg border border-gray-200 dark:bg-gray-800 dark:border-gray-700 max-h-60 overflow-auto">
                            <ListboxOption
                              v-for="model in llmModels"
                              :key="model.value"
                              :value="model.value"
                              v-slot="{ selected, active }"
                            >
                              <div
                                class="cursor-pointer select-none px-4 py-3"
                                :class="active ? 'bg-gray-100 dark:bg-gray-700' : ''"
                              >
                                <div class="flex items-center justify-between">
                                  <span class="font-medium text-gray-900 dark:text-white">{{ model.label }}</span>
                                  <CheckIcon v-if="selected" class="h-4 w-4 text-aegis-600" />
                                </div>
                                <p class="text-sm text-gray-500 dark:text-gray-400">{{ model.description }}</p>
                              </div>
                            </ListboxOption>
                          </ListboxOptions>
                        </div>
                      </Listbox>
                    </div>

                    <!-- Temperature -->
                    <div>
                      <label class="label">Temperature: {{ form.settings.temperature }}</label>
                      <input
                        v-model.number="form.settings.temperature"
                        type="range"
                        min="0"
                        max="2"
                        step="0.1"
                        class="w-full h-2 bg-gray-200 rounded-lg appearance-none cursor-pointer dark:bg-gray-700"
                      />
                      <div class="flex justify-between text-xs text-gray-500 dark:text-gray-400 mt-1">
                        <span>Precise (0)</span>
                        <span>Balanced (1)</span>
                        <span>Creative (2)</span>
                      </div>
                    </div>

                    <!-- System Prompt -->
                    <div>
                      <label class="label">System Prompt (Optional)</label>
                      <textarea
                        v-model="form.settings.systemPrompt"
                        rows="4"
                        class="input w-full font-mono text-sm"
                        placeholder="You are a helpful assistant specialized in..."
                      />
                      <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
                        Custom instructions for the LLM when querying this workspace
                      </p>
                    </div>

                    <div class="pt-4">
                      <button
                        class="btn-primary"
                        :disabled="saving"
                        @click="saveLLMSettings"
                      >
                        <span v-if="saving">Saving...</span>
                        <span v-else>Save Changes</span>
                      </button>
                    </div>
                  </div>
                </div>
              </TabPanel>

              <!-- Access & Sharing -->
              <TabPanel>
                <div class="space-y-6">
                  <!-- Members -->
                  <UserAccessManager :workspace-id="workspaceId" />

                  <!-- Shareable Links -->
                  <ShareableLinkGenerator :workspace-id="workspaceId" />
                </div>
              </TabPanel>

              <!-- Danger Zone -->
              <TabPanel>
                <div class="space-y-6">
                  <!-- Archive -->
                  <div class="bg-white rounded-lg border border-yellow-200 p-6 dark:bg-gray-800 dark:border-yellow-900/50">
                    <h3 class="text-lg font-semibold text-yellow-800 dark:text-yellow-400 mb-2">
                      Archive Workspace
                    </h3>
                    <p class="text-sm text-gray-600 dark:text-gray-400 mb-4">
                      Archiving this workspace will hide it from your workspace list. You can restore it later from the archived workspaces section.
                    </p>
                    <button
                      class="btn-ghost border border-yellow-300 text-yellow-700 hover:bg-yellow-50 dark:border-yellow-700 dark:text-yellow-400 dark:hover:bg-yellow-900/20"
                      @click="archiveWorkspace"
                    >
                      Archive Workspace
                    </button>
                  </div>

                  <!-- Delete -->
                  <div class="bg-white rounded-lg border border-red-200 p-6 dark:bg-gray-800 dark:border-red-900/50">
                    <h3 class="text-lg font-semibold text-red-700 dark:text-red-400 mb-2">
                      Delete Workspace
                    </h3>
                    <p class="text-sm text-gray-600 dark:text-gray-400 mb-4">
                      Once you delete a workspace, there is no going back. This will permanently delete the workspace, all documents, data sources, and query history.
                    </p>
                    <div class="mb-4">
                      <label class="label text-red-700 dark:text-red-400">
                        Type "{{ workspace.name }}" to confirm
                      </label>
                      <input
                        v-model="deleteConfirmation"
                        type="text"
                        class="input w-full border-red-300 focus:ring-red-500 focus:border-red-500"
                        :placeholder="workspace.name"
                      />
                    </div>
                    <button
                      class="btn-ghost border border-red-300 text-red-700 hover:bg-red-50 dark:border-red-700 dark:text-red-400 dark:hover:bg-red-900/20"
                      :disabled="!canDelete || deleting"
                      @click="deleteWorkspace"
                    >
                      <span v-if="deleting">Deleting...</span>
                      <span v-else>Delete Workspace Permanently</span>
                    </button>
                  </div>
                </div>
              </TabPanel>
            </TabPanels>
          </TabGroup>
        </div>
      </div>
    </template>
  </div>
</template>
