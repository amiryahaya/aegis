import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import api from '@/services/api'
import type {
  SearchRequest,
  SearchResponse,
  SearchResult,
  SearchFilter,
  SearchResultType,
  RecentSearch,
  DocumentPreview
} from '@/types/search'

const MAX_RECENT_SEARCHES = 10

export const useSearchStore = defineStore('search', () => {
  // State
  const results = ref<SearchResult[]>([])
  const totalCount = ref(0)
  const currentPage = ref(1)
  const pageSize = ref(20)
  const totalPages = ref(0)
  const searchTimeMs = ref(0)
  const currentQuery = ref('')
  const isLoading = ref(false)
  const error = ref<string | null>(null)

  const filters = ref<SearchFilter>({
    types: [],
    workspaceIds: [],
    dateRange: null
  })

  const recentSearches = ref<RecentSearch[]>(loadRecentSearches())
  const documentPreview = ref<DocumentPreview | null>(null)
  const isPreviewLoading = ref(false)

  // Computed
  const hasResults = computed(() => results.value.length > 0)
  const hasMorePages = computed(() => currentPage.value < totalPages.value)
  const activeFilterCount = computed(() => {
    let count = 0
    if (filters.value.types.length > 0) count++
    if (filters.value.workspaceIds.length > 0) count++
    if (filters.value.dateRange) count++
    return count
  })

  // Load recent searches from localStorage
  function loadRecentSearches(): RecentSearch[] {
    const stored = localStorage.getItem('recentSearches')
    if (stored) {
      try {
        return JSON.parse(stored)
      } catch {
        return []
      }
    }
    return []
  }

  // Save recent searches to localStorage
  function saveRecentSearches() {
    localStorage.setItem('recentSearches', JSON.stringify(recentSearches.value))
  }

  // Add to recent searches
  function addRecentSearch(query: string, resultCount: number) {
    // Remove duplicate if exists
    recentSearches.value = recentSearches.value.filter(s => s.query !== query)

    // Add new search at the beginning
    recentSearches.value.unshift({
      query,
      timestamp: new Date().toISOString(),
      resultCount
    })

    // Keep only max items
    if (recentSearches.value.length > MAX_RECENT_SEARCHES) {
      recentSearches.value = recentSearches.value.slice(0, MAX_RECENT_SEARCHES)
    }

    saveRecentSearches()
  }

  // Actions
  async function search(query: string, page = 1): Promise<SearchResponse | null> {
    if (!query.trim()) {
      clearResults()
      return null
    }

    isLoading.value = true
    error.value = null
    currentQuery.value = query
    currentPage.value = page

    try {
      const request: SearchRequest = {
        query,
        types: filters.value.types.length > 0 ? filters.value.types : undefined,
        workspaceIds: filters.value.workspaceIds.length > 0 ? filters.value.workspaceIds : undefined,
        dateFrom: filters.value.dateRange?.from || undefined,
        dateTo: filters.value.dateRange?.to || undefined,
        pageNumber: page,
        pageSize: pageSize.value,
        sortBy: 'relevance'
      }

      const response = await api.post<SearchResponse>('/search', request)

      results.value = response.results
      totalCount.value = response.totalCount
      totalPages.value = response.totalPages
      searchTimeMs.value = response.searchTimeMs

      // Add to recent searches
      addRecentSearch(query, response.totalCount)

      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Search failed'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function loadMoreResults(): Promise<void> {
    if (!hasMorePages.value || isLoading.value) return

    isLoading.value = true
    error.value = null

    try {
      const request: SearchRequest = {
        query: currentQuery.value,
        types: filters.value.types.length > 0 ? filters.value.types : undefined,
        workspaceIds: filters.value.workspaceIds.length > 0 ? filters.value.workspaceIds : undefined,
        dateFrom: filters.value.dateRange?.from || undefined,
        dateTo: filters.value.dateRange?.to || undefined,
        pageNumber: currentPage.value + 1,
        pageSize: pageSize.value,
        sortBy: 'relevance'
      }

      const response = await api.post<SearchResponse>('/search', request)

      results.value = [...results.value, ...response.results]
      currentPage.value = response.pageNumber
      totalPages.value = response.totalPages
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to load more results'
    } finally {
      isLoading.value = false
    }
  }

  async function fetchDocumentPreview(documentId: string): Promise<DocumentPreview | null> {
    isPreviewLoading.value = true
    error.value = null

    try {
      const response = await api.get<DocumentPreview>(`/documents/${documentId}/preview`)
      documentPreview.value = response
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to load document preview'
      return null
    } finally {
      isPreviewLoading.value = false
    }
  }

  function setTypeFilter(types: SearchResultType[]) {
    filters.value.types = types
  }

  function setWorkspaceFilter(workspaceIds: string[]) {
    filters.value.workspaceIds = workspaceIds
  }

  function setDateRangeFilter(from: string | null, to: string | null) {
    if (from || to) {
      filters.value.dateRange = { from, to }
    } else {
      filters.value.dateRange = null
    }
  }

  function clearFilters() {
    filters.value = {
      types: [],
      workspaceIds: [],
      dateRange: null
    }
  }

  function clearResults() {
    results.value = []
    totalCount.value = 0
    currentPage.value = 1
    totalPages.value = 0
    searchTimeMs.value = 0
    currentQuery.value = ''
  }

  function clearRecentSearches() {
    recentSearches.value = []
    saveRecentSearches()
  }

  function closeDocumentPreview() {
    documentPreview.value = null
  }

  function clearError() {
    error.value = null
  }

  return {
    // State
    results,
    totalCount,
    currentPage,
    pageSize,
    totalPages,
    searchTimeMs,
    currentQuery,
    isLoading,
    error,
    filters,
    recentSearches,
    documentPreview,
    isPreviewLoading,

    // Computed
    hasResults,
    hasMorePages,
    activeFilterCount,

    // Actions
    search,
    loadMoreResults,
    fetchDocumentPreview,
    setTypeFilter,
    setWorkspaceFilter,
    setDateRangeFilter,
    clearFilters,
    clearResults,
    clearRecentSearches,
    closeDocumentPreview,
    clearError
  }
})
