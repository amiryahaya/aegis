<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  MagnifyingGlassIcon,
  FunnelIcon,
  ClockIcon,
  DocumentTextIcon,
  ChatBubbleLeftRightIcon,
  FolderIcon,
  ChatBubbleOvalLeftIcon,
  XMarkIcon,
  ArrowPathIcon
} from '@heroicons/vue/24/outline'
import { Listbox, ListboxButton, ListboxOptions, ListboxOption } from '@headlessui/vue'
import { useSearchStore } from '@/stores/search'
import { useWorkspaceStore } from '@/stores/workspace'
import type { SearchResultType } from '@/types/search'

const route = useRoute()
const router = useRouter()
const searchStore = useSearchStore()
const workspaceStore = useWorkspaceStore()

const searchInput = ref('')
const showFilters = ref(false)

const typeOptions: { value: SearchResultType; label: string; icon: typeof DocumentTextIcon }[] = [
  { value: 'session', label: 'Sessions', icon: ChatBubbleLeftRightIcon },
  { value: 'document', label: 'Documents', icon: DocumentTextIcon },
  { value: 'workspace', label: 'Workspaces', icon: FolderIcon },
  { value: 'message', label: 'Messages', icon: ChatBubbleOvalLeftIcon }
]

const datePresets = [
  { value: 'today', label: 'Today' },
  { value: 'week', label: 'Past Week' },
  { value: 'month', label: 'Past Month' },
  { value: 'year', label: 'Past Year' }
]

const selectedDatePreset = ref<string | null>(null)

const resultTypeIcon = computed(() => (type: SearchResultType) => {
  switch (type) {
    case 'session': return ChatBubbleLeftRightIcon
    case 'document': return DocumentTextIcon
    case 'workspace': return FolderIcon
    case 'message': return ChatBubbleOvalLeftIcon
    default: return DocumentTextIcon
  }
})

const resultTypeLabel = computed(() => (type: SearchResultType) => {
  switch (type) {
    case 'session': return 'Session'
    case 'document': return 'Document'
    case 'workspace': return 'Workspace'
    case 'message': return 'Message'
    default: return type
  }
})

// Initialize from URL query params
onMounted(async () => {
  await workspaceStore.fetchWorkspaces()

  const query = route.query.q as string
  if (query) {
    searchInput.value = query
    await performSearch()
  }
})

// Watch for URL changes
watch(() => route.query.q, async (newQuery) => {
  if (newQuery && newQuery !== searchInput.value) {
    searchInput.value = newQuery as string
    await performSearch()
  }
})

async function performSearch() {
  if (!searchInput.value.trim()) return

  // Update URL
  router.replace({ query: { q: searchInput.value } })

  await searchStore.search(searchInput.value)
}

function handleSearchSubmit() {
  performSearch()
}

function selectRecentSearch(query: string) {
  searchInput.value = query
  performSearch()
}

function toggleTypeFilter(type: SearchResultType) {
  const current = [...searchStore.filters.types]
  const index = current.indexOf(type)
  if (index === -1) {
    current.push(type)
  } else {
    current.splice(index, 1)
  }
  searchStore.setTypeFilter(current)
  if (searchInput.value) {
    performSearch()
  }
}

function selectDatePreset(preset: string) {
  selectedDatePreset.value = preset
  const now = new Date()
  let from: string | null = null

  switch (preset) {
    case 'today':
      from = new Date(now.setHours(0, 0, 0, 0)).toISOString()
      break
    case 'week':
      from = new Date(now.setDate(now.getDate() - 7)).toISOString()
      break
    case 'month':
      from = new Date(now.setMonth(now.getMonth() - 1)).toISOString()
      break
    case 'year':
      from = new Date(now.setFullYear(now.getFullYear() - 1)).toISOString()
      break
  }

  searchStore.setDateRangeFilter(from, null)
  if (searchInput.value) {
    performSearch()
  }
}

function clearAllFilters() {
  searchStore.clearFilters()
  selectedDatePreset.value = null
  if (searchInput.value) {
    performSearch()
  }
}

function navigateToResult(result: { type: SearchResultType; id: string; metadata: { sessionId?: string; workspaceId?: string } }) {
  switch (result.type) {
    case 'session':
      router.push(`/chat/${result.id}`)
      break
    case 'document':
      if (result.metadata.workspaceId) {
        router.push(`/workspaces/${result.metadata.workspaceId}`)
      }
      break
    case 'workspace':
      router.push(`/workspaces/${result.id}`)
      break
    case 'message':
      if (result.metadata.sessionId) {
        router.push(`/chat/${result.metadata.sessionId}`)
      }
      break
  }
}

function formatDate(dateString: string): string {
  return new Date(dateString).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric'
  })
}

function highlightText(text: string, highlights: string[]): string {
  if (!highlights.length) return text
  let result = text
  highlights.forEach(highlight => {
    const regex = new RegExp(`(${highlight})`, 'gi')
    result = result.replace(regex, '<mark class="bg-yellow-200 dark:bg-yellow-800 px-0.5 rounded">$1</mark>')
  })
  return result
}
</script>

<template>
  <div class="min-h-screen bg-gray-50 dark:bg-gray-900">
    <!-- Search Header -->
    <div class="bg-white dark:bg-gray-800 border-b border-gray-200 dark:border-gray-700 sticky top-0 z-10">
      <div class="max-w-5xl mx-auto px-4 py-6">
        <form @submit.prevent="handleSearchSubmit" class="relative">
          <MagnifyingGlassIcon class="absolute left-4 top-1/2 -translate-y-1/2 h-5 w-5 text-gray-400" />
          <input
            v-model="searchInput"
            type="text"
            placeholder="Search sessions, documents, workspaces..."
            class="w-full pl-12 pr-4 py-3 text-lg border border-gray-300 dark:border-gray-600 rounded-xl bg-white dark:bg-gray-700 text-gray-900 dark:text-gray-100 placeholder-gray-500 dark:placeholder-gray-400 focus:ring-2 focus:ring-aegis-500 focus:border-transparent"
          />
        </form>

        <!-- Filter Toggle -->
        <div class="flex items-center justify-between mt-4">
          <button
            @click="showFilters = !showFilters"
            class="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-gray-100"
          >
            <FunnelIcon class="h-4 w-4" />
            Filters
            <span v-if="searchStore.activeFilterCount > 0" class="px-1.5 py-0.5 bg-aegis-100 dark:bg-aegis-900 text-aegis-700 dark:text-aegis-300 rounded-full text-xs">
              {{ searchStore.activeFilterCount }}
            </span>
          </button>

          <button
            v-if="searchStore.activeFilterCount > 0"
            @click="clearAllFilters"
            class="text-sm text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200"
          >
            Clear all filters
          </button>
        </div>

        <!-- Filters Panel -->
        <div v-if="showFilters" class="mt-4 p-4 bg-gray-50 dark:bg-gray-700/50 rounded-lg space-y-4">
          <!-- Type Filters -->
          <div>
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">Type</label>
            <div class="flex flex-wrap gap-2">
              <button
                v-for="type in typeOptions"
                :key="type.value"
                @click="toggleTypeFilter(type.value)"
                :class="[
                  'flex items-center gap-1.5 px-3 py-1.5 rounded-full text-sm transition-colors',
                  searchStore.filters.types.includes(type.value)
                    ? 'bg-aegis-100 dark:bg-aegis-900 text-aegis-700 dark:text-aegis-300 border border-aegis-300 dark:border-aegis-700'
                    : 'bg-white dark:bg-gray-600 text-gray-700 dark:text-gray-300 border border-gray-300 dark:border-gray-500 hover:bg-gray-50 dark:hover:bg-gray-500'
                ]"
              >
                <component :is="type.icon" class="h-4 w-4" />
                {{ type.label }}
              </button>
            </div>
          </div>

          <!-- Date Filters -->
          <div>
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">Date Range</label>
            <div class="flex flex-wrap gap-2">
              <button
                v-for="preset in datePresets"
                :key="preset.value"
                @click="selectDatePreset(preset.value)"
                :class="[
                  'px-3 py-1.5 rounded-full text-sm transition-colors',
                  selectedDatePreset === preset.value
                    ? 'bg-aegis-100 dark:bg-aegis-900 text-aegis-700 dark:text-aegis-300 border border-aegis-300 dark:border-aegis-700'
                    : 'bg-white dark:bg-gray-600 text-gray-700 dark:text-gray-300 border border-gray-300 dark:border-gray-500 hover:bg-gray-50 dark:hover:bg-gray-500'
                ]"
              >
                {{ preset.label }}
              </button>
            </div>
          </div>

          <!-- Workspace Filter -->
          <div v-if="workspaceStore.workspaces.length > 0">
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">Workspace</label>
            <Listbox
              :model-value="searchStore.filters.workspaceIds"
              @update:model-value="searchStore.setWorkspaceFilter($event); searchInput && performSearch()"
              multiple
            >
              <div class="relative">
                <ListboxButton class="relative w-full py-2 pl-3 pr-10 text-left bg-white dark:bg-gray-600 border border-gray-300 dark:border-gray-500 rounded-lg cursor-pointer focus:outline-none focus:ring-2 focus:ring-aegis-500">
                  <span class="block truncate text-gray-700 dark:text-gray-300">
                    {{ searchStore.filters.workspaceIds.length > 0
                      ? `${searchStore.filters.workspaceIds.length} workspace(s) selected`
                      : 'All workspaces' }}
                  </span>
                </ListboxButton>
                <ListboxOptions class="absolute z-10 w-full mt-1 max-h-60 overflow-auto bg-white dark:bg-gray-700 border border-gray-200 dark:border-gray-600 rounded-lg shadow-lg focus:outline-none">
                  <ListboxOption
                    v-for="workspace in workspaceStore.workspaces"
                    :key="workspace.id"
                    :value="workspace.id"
                    v-slot="{ active, selected }"
                    as="template"
                  >
                    <li
                      :class="[
                        'cursor-pointer select-none relative py-2 pl-10 pr-4',
                        active ? 'bg-aegis-100 dark:bg-aegis-900 text-aegis-900 dark:text-aegis-100' : 'text-gray-900 dark:text-gray-100'
                      ]"
                    >
                      <span :class="['block truncate', selected ? 'font-medium' : 'font-normal']">
                        {{ workspace.name }}
                      </span>
                      <span
                        v-if="selected"
                        class="absolute inset-y-0 left-0 flex items-center pl-3 text-aegis-600 dark:text-aegis-400"
                      >
                        <svg class="h-5 w-5" viewBox="0 0 20 20" fill="currentColor">
                          <path fill-rule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clip-rule="evenodd" />
                        </svg>
                      </span>
                    </li>
                  </ListboxOption>
                </ListboxOptions>
              </div>
            </Listbox>
          </div>
        </div>
      </div>
    </div>

    <div class="max-w-5xl mx-auto px-4 py-6">
      <!-- Recent Searches (when no query) -->
      <div v-if="!searchStore.currentQuery && searchStore.recentSearches.length > 0" class="mb-8">
        <div class="flex items-center justify-between mb-4">
          <h2 class="text-lg font-medium text-gray-900 dark:text-gray-100 flex items-center gap-2">
            <ClockIcon class="h-5 w-5 text-gray-400" />
            Recent Searches
          </h2>
          <button
            @click="searchStore.clearRecentSearches()"
            class="text-sm text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200"
          >
            Clear
          </button>
        </div>
        <div class="space-y-2">
          <button
            v-for="recent in searchStore.recentSearches"
            :key="recent.query"
            @click="selectRecentSearch(recent.query)"
            class="w-full flex items-center justify-between p-3 bg-white dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-700/50 transition-colors"
          >
            <span class="text-gray-900 dark:text-gray-100">{{ recent.query }}</span>
            <span class="text-sm text-gray-500 dark:text-gray-400">{{ recent.resultCount }} results</span>
          </button>
        </div>
      </div>

      <!-- Loading State -->
      <div v-if="searchStore.isLoading" class="flex items-center justify-center py-12">
        <ArrowPathIcon class="h-8 w-8 text-aegis-600 animate-spin" />
      </div>

      <!-- Results -->
      <div v-else-if="searchStore.hasResults">
        <div class="flex items-center justify-between mb-4">
          <p class="text-sm text-gray-600 dark:text-gray-400">
            {{ searchStore.totalCount.toLocaleString() }} results for "{{ searchStore.currentQuery }}"
            <span class="text-gray-400 dark:text-gray-500">({{ searchStore.searchTimeMs }}ms)</span>
          </p>
        </div>

        <div class="space-y-4">
          <div
            v-for="result in searchStore.results"
            :key="result.id"
            @click="navigateToResult(result)"
            class="bg-white dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700 p-4 hover:shadow-md transition-shadow cursor-pointer"
          >
            <div class="flex items-start gap-3">
              <div class="flex-shrink-0 p-2 bg-gray-100 dark:bg-gray-700 rounded-lg">
                <component :is="resultTypeIcon(result.type)" class="h-5 w-5 text-gray-500 dark:text-gray-400" />
              </div>
              <div class="flex-1 min-w-0">
                <div class="flex items-center gap-2 mb-1">
                  <span class="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">
                    {{ resultTypeLabel(result.type) }}
                  </span>
                  <span v-if="result.metadata.workspaceName" class="text-xs text-gray-400 dark:text-gray-500">
                    in {{ result.metadata.workspaceName }}
                  </span>
                </div>
                <h3 class="text-lg font-medium text-gray-900 dark:text-gray-100 mb-1">
                  {{ result.title }}
                </h3>
                <p
                  class="text-sm text-gray-600 dark:text-gray-400 line-clamp-2"
                  v-html="highlightText(result.excerpt, result.highlights)"
                />
                <div class="flex items-center gap-4 mt-2 text-xs text-gray-500 dark:text-gray-400">
                  <span>{{ formatDate(result.createdAt) }}</span>
                  <span v-if="result.score" class="flex items-center gap-1">
                    Relevance: {{ Math.round(result.score * 100) }}%
                  </span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Load More -->
        <div v-if="searchStore.hasMorePages" class="mt-6 text-center">
          <button
            @click="searchStore.loadMoreResults()"
            :disabled="searchStore.isLoading"
            class="btn-secondary"
          >
            <ArrowPathIcon v-if="searchStore.isLoading" class="h-4 w-4 animate-spin" />
            <span v-else>Load More</span>
          </button>
        </div>
      </div>

      <!-- No Results -->
      <div v-else-if="searchStore.currentQuery && !searchStore.isLoading" class="text-center py-12">
        <MagnifyingGlassIcon class="h-12 w-12 text-gray-400 mx-auto mb-4" />
        <h3 class="text-lg font-medium text-gray-900 dark:text-gray-100 mb-2">No results found</h3>
        <p class="text-gray-600 dark:text-gray-400">
          Try adjusting your search or filters to find what you're looking for.
        </p>
      </div>

      <!-- Initial State -->
      <div v-else-if="!searchStore.currentQuery && searchStore.recentSearches.length === 0" class="text-center py-12">
        <MagnifyingGlassIcon class="h-12 w-12 text-gray-400 mx-auto mb-4" />
        <h3 class="text-lg font-medium text-gray-900 dark:text-gray-100 mb-2">Search AEGIS</h3>
        <p class="text-gray-600 dark:text-gray-400">
          Search across your sessions, documents, and workspaces.
        </p>
      </div>

      <!-- Error State -->
      <div v-if="searchStore.error" class="mt-4 p-4 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg">
        <div class="flex items-center gap-2">
          <XMarkIcon class="h-5 w-5 text-red-500" />
          <span class="text-red-700 dark:text-red-300">{{ searchStore.error }}</span>
        </div>
      </div>
    </div>
  </div>
</template>
