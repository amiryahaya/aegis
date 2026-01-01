// Search Types

export type SearchResultType = 'session' | 'document' | 'workspace' | 'message'

export interface SearchResult {
  id: string
  type: SearchResultType
  title: string
  excerpt: string
  highlights: string[]
  score: number
  metadata: SearchResultMetadata
  createdAt: string
  updatedAt?: string
}

export interface SearchResultMetadata {
  workspaceId?: string
  workspaceName?: string
  sessionId?: string
  sessionTitle?: string
  documentId?: string
  documentName?: string
  pageNumber?: number
  chunkIndex?: number
  mimeType?: string
  fileSize?: number
}

export interface SearchRequest {
  query: string
  types?: SearchResultType[]
  workspaceIds?: string[]
  dateFrom?: string
  dateTo?: string
  pageNumber?: number
  pageSize?: number
  sortBy?: SearchSortBy
  sortOrder?: 'asc' | 'desc'
}

export type SearchSortBy = 'relevance' | 'date' | 'title'

export interface SearchResponse {
  results: SearchResult[]
  totalCount: number
  pageNumber: number
  pageSize: number
  totalPages: number
  facets: SearchFacets
  query: string
  searchTimeMs: number
}

export interface SearchFacets {
  types: FacetCount[]
  workspaces: FacetCount[]
  dateRanges: FacetCount[]
}

export interface FacetCount {
  value: string
  label: string
  count: number
}

export interface SearchFilter {
  types: SearchResultType[]
  workspaceIds: string[]
  dateRange: DateRangeFilter | null
}

export interface DateRangeFilter {
  from: string | null
  to: string | null
  preset?: 'today' | 'week' | 'month' | 'year' | 'custom'
}

export interface DocumentPreview {
  id: string
  name: string
  mimeType: string
  content: string
  chunks: DocumentChunk[]
  metadata: DocumentPreviewMetadata
}

export interface DocumentChunk {
  index: number
  content: string
  pageNumber?: number
  embedding?: number[]
}

export interface DocumentPreviewMetadata {
  workspaceId: string
  workspaceName: string
  uploadedBy: string
  uploadedAt: string
  fileSize: number
  pageCount?: number
  wordCount?: number
  language?: string
}

export interface RecentSearch {
  query: string
  timestamp: string
  resultCount: number
}
