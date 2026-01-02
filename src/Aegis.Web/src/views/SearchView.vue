<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  MagnifyingGlassIcon,
  ClockIcon,
  DocumentTextIcon,
  ChatBubbleLeftRightIcon,
  FolderIcon,
  ChatBubbleOvalLeftIcon,
  XMarkIcon,
  ArrowPathIcon
} from '@heroicons/vue/24/outline'
import { useSearchStore } from '@/stores/search'
import { useWorkspaceStore } from '@/stores/workspace'
import type { SearchResultType, SearchSuggestion } from '@/types/search'
import AdvancedFilters from '@/components/search/AdvancedFilters.vue'
import SearchSuggestions from '@/components/search/SearchSuggestions.vue'
import SavedSearches from '@/components/search/SavedSearches.vue'

const route = useRoute()
const router = useRouter()
const searchStore = useSearchStore()
const workspaceStore = useWorkspaceStore()

const searchInput = ref('')
const showSuggestions = ref(false)
const searchInputFocused = ref(false)

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
  showSuggestions.value = false
  performSearch()
}

function selectRecentSearch(query: string) {
  searchInput.value = query
  showSuggestions.value = false
  performSearch()
}

function handleSuggestionSelect(suggestion: SearchSuggestion) {
  searchInput.value = suggestion.text
  showSuggestions.value = false
  performSearch()
}

function handleSearchInputFocus() {
  searchInputFocused.value = true
  if (searchInput.value) {
    showSuggestions.value = true
  }
}

function handleSearchInputBlur() {
  // Delay to allow suggestion click
  setTimeout(() => {
    searchInputFocused.value = false
    showSuggestions.value = false
  }, 200)
}

function handleFiltersApply() {
  if (searchInput.value) {
    performSearch()
  }
}

function handleFiltersClear() {
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
            @focus="handleSearchInputFocus"
            @blur="handleSearchInputBlur"
            @input="showSuggestions = searchInput.length > 0"
          />

          <!-- Search Suggestions Dropdown -->
          <SearchSuggestions
            :query="searchInput"
            :show="showSuggestions && searchInputFocused"
            @select="handleSuggestionSelect"
            @close="showSuggestions = false"
          />
        </form>

        <!-- Advanced Filters -->
        <div class="mt-4">
          <AdvancedFilters
            @apply="handleFiltersApply"
            @clear="handleFiltersClear"
          />
        </div>
      </div>
    </div>

    <div class="max-w-5xl mx-auto px-4 py-6">
      <!-- No Query State: Show Saved Searches and Recent Searches -->
      <div v-if="!searchStore.currentQuery" class="grid md:grid-cols-2 gap-6 mb-8">
        <!-- Saved Searches -->
        <div class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 p-4">
          <SavedSearches />
        </div>

        <!-- Recent Searches -->
        <div class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 p-4">
          <div class="flex items-center justify-between mb-4">
            <h3 class="text-sm font-medium text-gray-900 dark:text-gray-100 flex items-center gap-2">
              <ClockIcon class="h-4 w-4" />
              Recent Searches
            </h3>
            <button
              v-if="searchStore.recentSearches.length > 0"
              @click="searchStore.clearRecentSearches()"
              class="text-xs text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200"
            >
              Clear
            </button>
          </div>
          <div v-if="searchStore.recentSearches.length > 0" class="space-y-2">
            <button
              v-for="recent in searchStore.recentSearches"
              :key="recent.query"
              @click="selectRecentSearch(recent.query)"
              class="w-full flex items-center justify-between p-3 bg-gray-50 dark:bg-gray-800/50 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-700/50 transition-colors text-left"
            >
              <span class="text-sm text-gray-900 dark:text-gray-100 truncate">{{ recent.query }}</span>
              <span class="text-xs text-gray-500 dark:text-gray-400 flex-shrink-0 ml-2">{{ recent.resultCount }} results</span>
            </button>
          </div>
          <div v-else class="text-center py-6 text-gray-500 dark:text-gray-400">
            <ClockIcon class="h-8 w-8 mx-auto mb-2 opacity-50" />
            <p class="text-sm">No recent searches</p>
          </div>
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
