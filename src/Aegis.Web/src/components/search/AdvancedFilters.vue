<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useSearchStore } from '@/stores/search'
import { useWorkspaceStore } from '@/stores/workspace'
import {
  FunnelIcon,
  XMarkIcon,
  ChevronDownIcon,
  CalendarIcon,
  FolderIcon,
  DocumentTextIcon,
  ChatBubbleLeftRightIcon,
  BuildingOfficeIcon
} from '@heroicons/vue/24/outline'
import { Disclosure, DisclosureButton, DisclosurePanel, Listbox, ListboxButton, ListboxOption, ListboxOptions } from '@headlessui/vue'
import type { SearchResultType } from '@/types/search'

interface Props {
  expanded?: boolean
}

withDefaults(defineProps<Props>(), {
  expanded: false
})

const emit = defineEmits<{
  apply: []
  clear: []
}>()

const searchStore = useSearchStore()
const workspaceStore = useWorkspaceStore()

// Type filter options
const typeOptions: { value: SearchResultType; label: string; icon: typeof DocumentTextIcon }[] = [
  { value: 'session', label: 'Sessions', icon: ChatBubbleLeftRightIcon },
  { value: 'document', label: 'Documents', icon: DocumentTextIcon },
  { value: 'workspace', label: 'Workspaces', icon: BuildingOfficeIcon },
  { value: 'message', label: 'Messages', icon: ChatBubbleLeftRightIcon }
]

// Date range presets
const datePresets = [
  { value: 'today', label: 'Today' },
  { value: 'week', label: 'Past Week' },
  { value: 'month', label: 'Past Month' },
  { value: 'quarter', label: 'Past 3 Months' },
  { value: 'year', label: 'Past Year' },
  { value: 'custom', label: 'Custom Range' }
] as const

type DatePreset = typeof datePresets[number]['value']

const selectedDatePreset = ref<DatePreset | null>(null)
const customDateFrom = ref('')
const customDateTo = ref('')

// Local filter state
const selectedTypes = ref<SearchResultType[]>([...searchStore.filters.types])
const selectedWorkspaces = ref<string[]>([...searchStore.filters.workspaceIds])

// Computed
const hasActiveFilters = computed(() => searchStore.activeFilterCount > 0)

const workspaceOptions = computed(() =>
  workspaceStore.workspaces.map(ws => ({
    value: ws.id,
    label: ws.name
  }))
)

// Watch for external filter changes
watch(() => searchStore.filters, (newFilters) => {
  selectedTypes.value = [...newFilters.types]
  selectedWorkspaces.value = [...newFilters.workspaceIds]
}, { deep: true })

// Toggle type selection
function toggleType(type: SearchResultType) {
  const index = selectedTypes.value.indexOf(type)
  if (index === -1) {
    selectedTypes.value.push(type)
  } else {
    selectedTypes.value.splice(index, 1)
  }
}

// Apply date preset
function applyDatePreset(preset: DatePreset) {
  selectedDatePreset.value = preset

  if (preset === 'custom') {
    return
  }

  const now = new Date()
  let from: Date

  switch (preset) {
    case 'today':
      from = new Date(now.getFullYear(), now.getMonth(), now.getDate())
      break
    case 'week':
      from = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000)
      break
    case 'month':
      from = new Date(now.getFullYear(), now.getMonth() - 1, now.getDate())
      break
    case 'quarter':
      from = new Date(now.getFullYear(), now.getMonth() - 3, now.getDate())
      break
    case 'year':
      from = new Date(now.getFullYear() - 1, now.getMonth(), now.getDate())
      break
    default:
      return
  }

  customDateFrom.value = from.toISOString().split('T')[0]
  customDateTo.value = now.toISOString().split('T')[0]
}

// Apply all filters
function applyFilters() {
  searchStore.setTypeFilter(selectedTypes.value)
  searchStore.setWorkspaceFilter(selectedWorkspaces.value)

  if (customDateFrom.value || customDateTo.value) {
    searchStore.setDateRangeFilter(customDateFrom.value || null, customDateTo.value || null)
  } else {
    searchStore.setDateRangeFilter(null, null)
  }

  emit('apply')
}

// Clear all filters
function clearFilters() {
  selectedTypes.value = []
  selectedWorkspaces.value = []
  selectedDatePreset.value = null
  customDateFrom.value = ''
  customDateTo.value = ''
  searchStore.clearFilters()
  emit('clear')
}

// Get selected workspace names for display
function getSelectedWorkspaceNames(): string {
  if (selectedWorkspaces.value.length === 0) return 'All Workspaces'
  if (selectedWorkspaces.value.length === 1) {
    const ws = workspaceStore.workspaces.find(w => w.id === selectedWorkspaces.value[0])
    return ws?.name || 'Unknown'
  }
  return `${selectedWorkspaces.value.length} workspaces`
}
</script>

<template>
  <Disclosure v-slot="{ open }" :default-open="expanded">
    <div class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 overflow-hidden">
      <!-- Header -->
      <DisclosureButton class="w-full px-4 py-3 flex items-center justify-between text-left hover:bg-gray-50 dark:hover:bg-gray-700/50 transition-colors">
        <div class="flex items-center gap-2">
          <FunnelIcon class="h-5 w-5 text-gray-500" />
          <span class="font-medium text-gray-900 dark:text-gray-100">Advanced Filters</span>
          <span
            v-if="hasActiveFilters"
            class="px-2 py-0.5 text-xs bg-aegis-100 text-aegis-700 dark:bg-aegis-900/50 dark:text-aegis-300 rounded-full"
          >
            {{ searchStore.activeFilterCount }} active
          </span>
        </div>
        <ChevronDownIcon
          class="h-5 w-5 text-gray-400 transition-transform"
          :class="{ 'rotate-180': open }"
        />
      </DisclosureButton>

      <!-- Filter content -->
      <DisclosurePanel class="px-4 pb-4 space-y-4">
        <!-- Type filters -->
        <div>
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
            Content Type
          </label>
          <div class="flex flex-wrap gap-2">
            <button
              v-for="type in typeOptions"
              :key="type.value"
              @click="toggleType(type.value)"
              class="flex items-center gap-2 px-3 py-1.5 rounded-full text-sm transition-colors"
              :class="[
                selectedTypes.includes(type.value)
                  ? 'bg-aegis-100 text-aegis-700 dark:bg-aegis-900/50 dark:text-aegis-300 ring-1 ring-aegis-300 dark:ring-aegis-700'
                  : 'bg-gray-100 text-gray-700 dark:bg-gray-700 dark:text-gray-300 hover:bg-gray-200 dark:hover:bg-gray-600'
              ]"
            >
              <component :is="type.icon" class="h-4 w-4" />
              {{ type.label }}
            </button>
          </div>
        </div>

        <!-- Workspace filter -->
        <div>
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
            Workspace
          </label>
          <Listbox v-model="selectedWorkspaces" multiple>
            <div class="relative">
              <ListboxButton class="input w-full text-left flex items-center justify-between">
                <span class="flex items-center gap-2">
                  <FolderIcon class="h-4 w-4 text-gray-400" />
                  {{ getSelectedWorkspaceNames() }}
                </span>
                <ChevronDownIcon class="h-4 w-4 text-gray-400" />
              </ListboxButton>
              <ListboxOptions class="absolute z-10 mt-1 w-full bg-white dark:bg-gray-800 rounded-lg shadow-lg border border-gray-200 dark:border-gray-700 max-h-48 overflow-auto">
                <ListboxOption
                  v-for="workspace in workspaceOptions"
                  :key="workspace.value"
                  :value="workspace.value"
                  v-slot="{ selected, active }"
                >
                  <li
                    class="px-3 py-2 cursor-pointer flex items-center gap-2"
                    :class="[
                      active ? 'bg-aegis-50 dark:bg-aegis-900/50' : '',
                      selected ? 'bg-aegis-100 dark:bg-aegis-900' : ''
                    ]"
                  >
                    <input
                      type="checkbox"
                      :checked="selected"
                      class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500"
                    />
                    <span class="text-sm text-gray-900 dark:text-gray-100">{{ workspace.label }}</span>
                  </li>
                </ListboxOption>
              </ListboxOptions>
            </div>
          </Listbox>
        </div>

        <!-- Date range -->
        <div>
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
            Date Range
          </label>
          <div class="flex flex-wrap gap-2 mb-3">
            <button
              v-for="preset in datePresets"
              :key="preset.value"
              @click="applyDatePreset(preset.value)"
              class="px-3 py-1 rounded-full text-sm transition-colors"
              :class="[
                selectedDatePreset === preset.value
                  ? 'bg-aegis-100 text-aegis-700 dark:bg-aegis-900/50 dark:text-aegis-300'
                  : 'bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-400 hover:bg-gray-200 dark:hover:bg-gray-600'
              ]"
            >
              {{ preset.label }}
            </button>
          </div>

          <div v-if="selectedDatePreset === 'custom'" class="grid grid-cols-2 gap-3">
            <div>
              <label class="block text-xs text-gray-500 mb-1">From</label>
              <div class="relative">
                <CalendarIcon class="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400" />
                <input
                  v-model="customDateFrom"
                  type="date"
                  class="input pl-9 w-full"
                />
              </div>
            </div>
            <div>
              <label class="block text-xs text-gray-500 mb-1">To</label>
              <div class="relative">
                <CalendarIcon class="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400" />
                <input
                  v-model="customDateTo"
                  type="date"
                  class="input pl-9 w-full"
                />
              </div>
            </div>
          </div>
        </div>

        <!-- Actions -->
        <div class="flex justify-end gap-3 pt-2 border-t border-gray-100 dark:border-gray-700">
          <button
            v-if="hasActiveFilters"
            @click="clearFilters"
            class="btn-ghost text-sm flex items-center gap-1"
          >
            <XMarkIcon class="h-4 w-4" />
            Clear All
          </button>
          <button
            @click="applyFilters"
            class="btn-primary text-sm"
          >
            Apply Filters
          </button>
        </div>
      </DisclosurePanel>
    </div>
  </Disclosure>
</template>
