import api from './api'

// =============================================================================
// Request/Response Types (matching backend DTOs)
// =============================================================================

export type SearchResultType = 'session' | 'document' | 'workspace' | 'message'

export interface SearchRequest {
  query: string
  types?: SearchResultType[]
  workspaceIds?: string[]
  dateFrom?: string
  dateTo?: string
  pageNumber?: number
  pageSize?: number
  sortBy?: 'relevance' | 'date' | 'title'
}

export interface SearchResponse {
  results: SearchResult[]
  totalCount: number
  pageNumber: number
  pageSize: number
  totalPages: number
  searchTimeMs: number
  facets?: SearchFacets
}

export interface SearchResult {
  id: string
  type: SearchResultType
  title: string
  excerpt: string
  highlights: string[]
  relevanceScore: number
  createdAt: string
  updatedAt?: string
  metadata: SearchResultMetadata
}

export interface SearchResultMetadata {
  workspaceId?: string
  workspaceName?: string
  sessionId?: string
  documentId?: string
  messageIndex?: number
  pageNumber?: number
  chunkIndex?: number
}

export interface SearchFacets {
  types: Array<{ type: SearchResultType; count: number }>
  workspaces: Array<{ id: string; name: string; count: number }>
  dateRanges: Array<{ label: string; from: string; to: string; count: number }>
}

export interface DocumentPreviewResponse {
  id: string
  name: string
  type: string
  content: string
  pageCount?: number
  wordCount?: number
  chunks: DocumentChunkPreview[]
}

export interface DocumentChunkPreview {
  index: number
  content: string
  pageNumber?: number
}

// =============================================================================
// Search Service
// =============================================================================

class SearchService {
  private readonly basePath = '/search'

  /**
   * Execute a search query
   */
  async search(request: SearchRequest): Promise<SearchResponse> {
    return api.post<SearchResponse>(this.basePath, request)
  }

  /**
   * Search with pagination
   */
  async searchPaged(
    query: string,
    page = 1,
    pageSize = 20,
    options?: {
      types?: SearchResultType[]
      workspaceIds?: string[]
      dateFrom?: string
      dateTo?: string
      sortBy?: 'relevance' | 'date' | 'title'
    }
  ): Promise<SearchResponse> {
    return this.search({
      query,
      pageNumber: page,
      pageSize,
      ...options
    })
  }

  /**
   * Get search suggestions based on query
   */
  async getSuggestions(query: string, limit = 5): Promise<string[]> {
    try {
      return await api.get<string[]>(`${this.basePath}/suggestions`, {
        query,
        limit
      })
    } catch {
      // Return empty array if suggestions API is not available
      return []
    }
  }

  /**
   * Get document preview
   */
  async getDocumentPreview(documentId: string): Promise<DocumentPreviewResponse> {
    return api.get<DocumentPreviewResponse>(`/documents/${documentId}/preview`)
  }

  /**
   * Get search facets for filtering
   */
  async getFacets(query: string): Promise<SearchFacets> {
    try {
      return await api.get<SearchFacets>(`${this.basePath}/facets`, { query })
    } catch {
      // Return empty facets if not available
      return {
        types: [],
        workspaces: [],
        dateRanges: []
      }
    }
  }

  // =============================================================================
  // Utility Methods
  // =============================================================================

  /**
   * Highlight search terms in text
   */
  highlightTerms(text: string, terms: string[]): string {
    if (!terms.length) return text

    const pattern = new RegExp(`(${terms.map(t => this.escapeRegex(t)).join('|')})`, 'gi')
    return text.replace(pattern, '<mark>$1</mark>')
  }

  /**
   * Escape special regex characters
   */
  private escapeRegex(str: string): string {
    return str.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  }

  /**
   * Extract search terms from query
   */
  extractTerms(query: string): string[] {
    // Remove quoted phrases and split by whitespace
    const terms: string[] = []

    // Extract quoted phrases
    const quotedPattern = /"([^"]+)"/g
    let match
    while ((match = quotedPattern.exec(query)) !== null) {
      terms.push(match[1])
    }

    // Remove quoted phrases and split remaining
    const remaining = query.replace(quotedPattern, '').trim()
    if (remaining) {
      terms.push(...remaining.split(/\s+/).filter(t => t.length > 2))
    }

    return terms
  }

  /**
   * Get result type badge info
   */
  getTypeBadge(type: SearchResultType): {
    label: string
    color: 'blue' | 'green' | 'purple' | 'orange'
    icon: string
  } {
    switch (type) {
      case 'session':
        return { label: 'Session', color: 'blue', icon: 'chat-bubble-left-right' }
      case 'document':
        return { label: 'Document', color: 'green', icon: 'document-text' }
      case 'workspace':
        return { label: 'Workspace', color: 'purple', icon: 'folder' }
      case 'message':
        return { label: 'Message', color: 'orange', icon: 'chat-bubble-left' }
      default:
        return { label: type, color: 'blue', icon: 'magnifying-glass' }
    }
  }

  /**
   * Format search time for display
   */
  formatSearchTime(ms: number): string {
    if (ms < 1000) {
      return `${ms}ms`
    }
    return `${(ms / 1000).toFixed(2)}s`
  }

  /**
   * Build search URL with parameters
   */
  buildSearchUrl(query: string, filters?: {
    types?: SearchResultType[]
    workspaceIds?: string[]
    dateFrom?: string
    dateTo?: string
  }): string {
    const params = new URLSearchParams()
    params.set('q', query)

    if (filters?.types?.length) {
      params.set('types', filters.types.join(','))
    }
    if (filters?.workspaceIds?.length) {
      params.set('workspaces', filters.workspaceIds.join(','))
    }
    if (filters?.dateFrom) {
      params.set('from', filters.dateFrom)
    }
    if (filters?.dateTo) {
      params.set('to', filters.dateTo)
    }

    return `/search?${params.toString()}`
  }

  /**
   * Parse search URL parameters
   */
  parseSearchUrl(url: string): {
    query: string
    types?: SearchResultType[]
    workspaceIds?: string[]
    dateFrom?: string
    dateTo?: string
  } {
    const params = new URL(url, 'http://localhost').searchParams
    return {
      query: params.get('q') || '',
      types: params.get('types')?.split(',') as SearchResultType[] | undefined,
      workspaceIds: params.get('workspaces')?.split(','),
      dateFrom: params.get('from') || undefined,
      dateTo: params.get('to') || undefined
    }
  }
}

export const searchService = new SearchService()
export default searchService
