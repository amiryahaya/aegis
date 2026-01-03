import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import searchService from '@/services/search.service'
import type {
  SearchResponse,
  SearchResult,
  SearchFilter,
  SearchResultType,
  RecentSearch,
  DocumentPreview,
  SavedSearch,
  CreateSavedSearchRequest,
  UpdateSavedSearchRequest,
  SearchSuggestion
} from '@/types/search'

const MAX_RECENT_SEARCHES = 10
const MAX_SAVED_SEARCHES = 20
const SAVED_SEARCHES_KEY = 'savedSearches'

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

  // Saved searches state
  const savedSearches = ref<SavedSearch[]>(loadSavedSearches())
  const suggestions = ref<SearchSuggestion[]>([])
  const isSuggestionsLoading = ref(false)
  const showSuggestions = ref(false)

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

  // Load saved searches from localStorage
  function loadSavedSearches(): SavedSearch[] {
    const stored = localStorage.getItem(SAVED_SEARCHES_KEY)
    if (stored) {
      try {
        return JSON.parse(stored)
      } catch {
        return []
      }
    }
    return []
  }

  // Save saved searches to localStorage
  function persistSavedSearches() {
    localStorage.setItem(SAVED_SEARCHES_KEY, JSON.stringify(savedSearches.value))
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
      const response = await searchService.searchPaged(query, page, pageSize.value, {
        types: filters.value.types.length > 0 ? filters.value.types : undefined,
        workspaceIds: filters.value.workspaceIds.length > 0 ? filters.value.workspaceIds : undefined,
        dateFrom: filters.value.dateRange?.from || undefined,
        dateTo: filters.value.dateRange?.to || undefined,
        sortBy: 'relevance'
      })

      // Map service response to store types
      results.value = response.results.map(r => ({
        id: r.id,
        type: r.type as SearchResultType,
        title: r.title,
        excerpt: r.excerpt,
        highlights: r.highlights,
        score: r.relevanceScore,
        metadata: {
          workspaceId: r.metadata.workspaceId,
          workspaceName: r.metadata.workspaceName,
          sessionId: r.metadata.sessionId,
          documentId: r.metadata.documentId
        },
        createdAt: r.createdAt,
        updatedAt: r.updatedAt
      }))
      totalCount.value = response.totalCount
      totalPages.value = response.totalPages
      searchTimeMs.value = response.searchTimeMs

      // Add to recent searches
      addRecentSearch(query, response.totalCount)

      return response as unknown as SearchResponse
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
      const response = await searchService.searchPaged(
        currentQuery.value,
        currentPage.value + 1,
        pageSize.value,
        {
          types: filters.value.types.length > 0 ? filters.value.types : undefined,
          workspaceIds: filters.value.workspaceIds.length > 0 ? filters.value.workspaceIds : undefined,
          dateFrom: filters.value.dateRange?.from || undefined,
          dateTo: filters.value.dateRange?.to || undefined,
          sortBy: 'relevance'
        }
      )

      // Map and append results
      const newResults = response.results.map(r => ({
        id: r.id,
        type: r.type as SearchResultType,
        title: r.title,
        excerpt: r.excerpt,
        highlights: r.highlights,
        score: r.relevanceScore,
        metadata: {
          workspaceId: r.metadata.workspaceId,
          workspaceName: r.metadata.workspaceName,
          sessionId: r.metadata.sessionId,
          documentId: r.metadata.documentId
        },
        createdAt: r.createdAt,
        updatedAt: r.updatedAt
      }))
      results.value = [...results.value, ...newResults]
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
      const response = await searchService.getDocumentPreview(documentId)
      // Map to store type
      documentPreview.value = {
        id: response.id,
        name: response.name,
        mimeType: response.type,
        content: response.content,
        chunks: response.chunks.map(c => ({
          index: c.index,
          content: c.content,
          pageNumber: c.pageNumber
        })),
        metadata: {
          workspaceId: '',
          workspaceName: '',
          uploadedBy: '',
          uploadedAt: '',
          fileSize: 0,
          pageCount: response.pageCount,
          wordCount: response.wordCount
        }
      }
      return documentPreview.value
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

  // ============================================
  // SAVED SEARCHES ACTIONS
  // ============================================

  function createSavedSearch(request: CreateSavedSearchRequest): SavedSearch {
    const newSearch: SavedSearch = {
      id: crypto.randomUUID(),
      name: request.name,
      query: request.query,
      filters: request.filters || { types: [], workspaceIds: [], dateRange: null },
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      useCount: 0,
      color: request.color || 'blue',
      icon: request.icon
    }

    savedSearches.value.unshift(newSearch)

    if (savedSearches.value.length > MAX_SAVED_SEARCHES) {
      savedSearches.value = savedSearches.value.slice(0, MAX_SAVED_SEARCHES)
    }

    persistSavedSearches()
    return newSearch
  }

  function updateSavedSearch(id: string, updates: UpdateSavedSearchRequest): SavedSearch | null {
    const index = savedSearches.value.findIndex(s => s.id === id)
    if (index === -1) return null

    const search = savedSearches.value[index]
    const updated: SavedSearch = {
      ...search,
      ...updates,
      filters: updates.filters || search.filters,
      updatedAt: new Date().toISOString()
    }

    savedSearches.value[index] = updated
    persistSavedSearches()
    return updated
  }

  function deleteSavedSearch(id: string): boolean {
    const index = savedSearches.value.findIndex(s => s.id === id)
    if (index === -1) return false

    savedSearches.value.splice(index, 1)
    persistSavedSearches()
    return true
  }

  function executeSavedSearch(id: string): Promise<SearchResponse | null> {
    const savedSearch = savedSearches.value.find(s => s.id === id)
    if (!savedSearch) return Promise.resolve(null)

    // Update use count
    const index = savedSearches.value.findIndex(s => s.id === id)
    if (index !== -1) {
      savedSearches.value[index] = {
        ...savedSearches.value[index],
        useCount: savedSearches.value[index].useCount + 1,
        lastUsedAt: new Date().toISOString()
      }
      persistSavedSearches()
    }

    // Apply filters
    filters.value = { ...savedSearch.filters }

    // Execute search
    return search(savedSearch.query)
  }

  function setDefaultSavedSearch(id: string): void {
    savedSearches.value = savedSearches.value.map(s => ({
      ...s,
      isDefault: s.id === id
    }))
    persistSavedSearches()
  }

  function getDefaultSavedSearch(): SavedSearch | undefined {
    return savedSearches.value.find(s => s.isDefault)
  }

  // ============================================
  // SEARCH SUGGESTIONS ACTIONS
  // ============================================

  function generateSuggestions(query: string): SearchSuggestion[] {
    if (!query.trim()) {
      showSuggestions.value = false
      return []
    }

    const lowerQuery = query.toLowerCase()
    const allSuggestions: SearchSuggestion[] = []

    // Add matching saved searches
    const matchingSaved = savedSearches.value
      .filter(s => s.name.toLowerCase().includes(lowerQuery) || s.query.toLowerCase().includes(lowerQuery))
      .slice(0, 3)
      .map(s => ({
        type: 'saved' as const,
        text: s.query,
        icon: s.icon || 'bookmark',
        metadata: { savedSearchId: s.id, count: s.useCount }
      }))
    allSuggestions.push(...matchingSaved)

    // Add matching recent searches
    const matchingRecent = recentSearches.value
      .filter(r => r.query.toLowerCase().includes(lowerQuery))
      .slice(0, 3)
      .map(r => ({
        type: 'recent' as const,
        text: r.query,
        icon: 'clock',
        metadata: { count: r.resultCount }
      }))
    allSuggestions.push(...matchingRecent)

    // Add query suggestion if not already in results
    if (!allSuggestions.some(s => s.text.toLowerCase() === lowerQuery)) {
      allSuggestions.unshift({
        type: 'query' as const,
        text: query,
        icon: 'search'
      })
    }

    suggestions.value = allSuggestions.slice(0, 8)
    showSuggestions.value = allSuggestions.length > 0
    return suggestions.value
  }

  function clearSuggestions() {
    suggestions.value = []
    showSuggestions.value = false
  }

  function hideSuggestions() {
    showSuggestions.value = false
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
    savedSearches,
    suggestions,
    isSuggestionsLoading,
    showSuggestions,

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
    clearError,

    // Saved Searches
    createSavedSearch,
    updateSavedSearch,
    deleteSavedSearch,
    executeSavedSearch,
    setDefaultSavedSearch,
    getDefaultSavedSearch,

    // Suggestions
    generateSuggestions,
    clearSuggestions,
    hideSuggestions
  }
})
