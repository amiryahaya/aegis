import api from './api'
import type { Workspace, PagedResponse } from '@/types'
import type {
  DataSource,
  Document,
  WorkspaceShare,
  ShareableLink,
  WorkspaceRole
} from '@/types/workspace'

// =============================================================================
// Request/Response Types (matching backend DTOs)
// =============================================================================

export interface CreateWorkspaceRequest {
  name: string
  createdBy: string
  description?: string
  teamId?: string
  customInstructions?: string
}

export interface UpdateWorkspaceRequest {
  name?: string
  description?: string
  settings?: {
    defaultSearchMode?: 'Semantic' | 'Keyword' | 'Hybrid'
    maxResults?: number
    enableCaching?: boolean
    enableFollowUps?: boolean
    llmModel?: string
    temperature?: number
    systemPrompt?: string
  }
}

export interface WorkspaceResponse {
  id: string
  name: string
  description?: string
  teamId?: string
  createdBy: string
  status: string
}

export interface WorkspaceContextResponse {
  customInstructions?: string
  entities: EntityContextDto[]
  findings: FindingContextDto[]
  facts: FactContextDto[]
  formattedContext: string
}

export interface EntityContextDto {
  id: string
  name: string
  type: string
  description?: string
  aliases: string[]
  confidence: string
}

export interface FindingContextDto {
  id: string
  title: string
  content: string
  type: string
}

export interface FactContextDto {
  id: string
  statement: string
  confidence: string
}

export interface RelevantContextParams {
  query: string
  maxEntities?: number
  maxFindings?: number
  maxFacts?: number
}

// Data Source Types
export interface CreateDataSourceRequest {
  workspaceId: string
  name: string
  type: DataSourceType
  config: DataSourceConfig
}

export interface UpdateDataSourceRequest {
  name?: string
  config?: Partial<DataSourceConfig>
}

export type DataSourceType =
  | 'FileUpload'
  | 'WebCrawler'
  | 'Database'
  | 'SharePoint'
  | 'GoogleDrive'
  | 'Confluence'
  | 'Notion'
  | 'S3'
  | 'AzureBlob'

export interface DataSourceConfig {
  connectionString?: string
  url?: string
  credentials?: Record<string, string>
  syncSchedule?: string
  filters?: string[]
  maxDocuments?: number
}

export interface DataSourceResponse {
  id: string
  workspaceId: string
  name: string
  type: string
  status: string
  config: DataSourceConfig
  lastSyncAt?: string
  nextSyncAt?: string
  documentCount: number
  createdAt: string
}

// Document Types
export interface DocumentResponse {
  id: string
  workspaceId: string
  dataSourceId?: string
  name: string
  type: string
  status: string
  size: number
  chunkCount: number
  metadata: Record<string, string>
  createdAt: string
  processedAt?: string
}

export interface DocumentChunkResponse {
  id: string
  documentId: string
  chunkIndex: number
  content: string
  metadata: Record<string, string>
}

// Sharing Types
export interface ShareWorkspaceRequest {
  userId: string
  role: WorkspaceRole
}

export interface WorkspaceShareResponse {
  id: string
  workspaceId: string
  userId: string
  role: string
  createdAt: string
  createdBy: string
}

export interface CreateShareableLinkRequest {
  role: WorkspaceRole
  expiresAt?: string
  maxUses?: number
  password?: string
}

export interface ShareableLinkResponse {
  id: string
  workspaceId: string
  token: string
  role: string
  expiresAt?: string
  maxUses?: number
  useCount: number
  isActive: boolean
  createdAt: string
  createdBy: string
}

// =============================================================================
// Workspace Service
// =============================================================================

class WorkspaceService {
  private readonly basePath = '/workspaces'

  // =========================================================================
  // Workspace CRUD
  // =========================================================================

  /**
   * Create a new workspace
   */
  async create(request: CreateWorkspaceRequest): Promise<WorkspaceResponse> {
    return api.post<WorkspaceResponse>(this.basePath, request)
  }

  /**
   * Get all workspaces
   */
  async getAll(teamId?: string): Promise<WorkspaceResponse[]> {
    const params = teamId ? { teamId } : undefined
    return api.get<WorkspaceResponse[]>(this.basePath, params)
  }

  /**
   * Get workspaces with pagination
   */
  async getPaged(
    page?: number,
    pageSize?: number,
    teamId?: string
  ): Promise<PagedResponse<WorkspaceResponse>> {
    const params: Record<string, unknown> = {}
    if (page) params.page = page
    if (pageSize) params.pageSize = pageSize
    if (teamId) params.teamId = teamId
    return api.get<PagedResponse<WorkspaceResponse>>(this.basePath, params)
  }

  /**
   * Get workspace by ID
   */
  async getById(id: string): Promise<WorkspaceResponse> {
    return api.get<WorkspaceResponse>(`${this.basePath}/${id}`)
  }

  /**
   * Update workspace
   */
  async update(id: string, request: UpdateWorkspaceRequest): Promise<WorkspaceResponse> {
    return api.put<WorkspaceResponse>(`${this.basePath}/${id}`, request)
  }

  /**
   * Delete workspace
   */
  async delete(id: string): Promise<void> {
    return api.delete<void>(`${this.basePath}/${id}`)
  }

  /**
   * Archive workspace
   */
  async archive(id: string): Promise<WorkspaceResponse> {
    return api.post<WorkspaceResponse>(`${this.basePath}/${id}/archive`)
  }

  // =========================================================================
  // Workspace Context (RAG)
  // =========================================================================

  /**
   * Get full workspace context
   */
  async getContext(id: string): Promise<WorkspaceContextResponse> {
    return api.get<WorkspaceContextResponse>(`${this.basePath}/${id}/context`)
  }

  /**
   * Get relevant context for a query
   */
  async getRelevantContext(
    id: string,
    params: RelevantContextParams
  ): Promise<WorkspaceContextResponse> {
    return api.get<WorkspaceContextResponse>(`${this.basePath}/${id}/context/relevant`, {
      query: params.query,
      maxEntities: params.maxEntities ?? 10,
      maxFindings: params.maxFindings ?? 5,
      maxFacts: params.maxFacts ?? 10
    })
  }

  // =========================================================================
  // Data Sources
  // =========================================================================

  /**
   * Get data sources for a workspace
   */
  async getDataSources(workspaceId: string): Promise<DataSourceResponse[]> {
    return api.get<DataSourceResponse[]>(`${this.basePath}/${workspaceId}/datasources`)
  }

  /**
   * Get data sources with pagination
   */
  async getDataSourcesPaged(
    workspaceId: string,
    page?: number,
    pageSize?: number
  ): Promise<PagedResponse<DataSourceResponse>> {
    const params: Record<string, unknown> = {}
    if (page) params.page = page
    if (pageSize) params.pageSize = pageSize
    return api.get<PagedResponse<DataSourceResponse>>(
      `${this.basePath}/${workspaceId}/datasources`,
      params
    )
  }

  /**
   * Get data source by ID
   */
  async getDataSource(workspaceId: string, dataSourceId: string): Promise<DataSourceResponse> {
    return api.get<DataSourceResponse>(
      `${this.basePath}/${workspaceId}/datasources/${dataSourceId}`
    )
  }

  /**
   * Create a new data source
   */
  async createDataSource(request: CreateDataSourceRequest): Promise<DataSourceResponse> {
    return api.post<DataSourceResponse>(
      `${this.basePath}/${request.workspaceId}/datasources`,
      request
    )
  }

  /**
   * Update data source
   */
  async updateDataSource(
    workspaceId: string,
    dataSourceId: string,
    request: UpdateDataSourceRequest
  ): Promise<DataSourceResponse> {
    return api.put<DataSourceResponse>(
      `${this.basePath}/${workspaceId}/datasources/${dataSourceId}`,
      request
    )
  }

  /**
   * Delete data source
   */
  async deleteDataSource(workspaceId: string, dataSourceId: string): Promise<void> {
    return api.delete<void>(`${this.basePath}/${workspaceId}/datasources/${dataSourceId}`)
  }

  /**
   * Sync data source
   */
  async syncDataSource(workspaceId: string, dataSourceId: string): Promise<void> {
    return api.post<void>(`${this.basePath}/${workspaceId}/datasources/${dataSourceId}/sync`)
  }

  /**
   * Toggle data source enabled/disabled
   */
  async toggleDataSource(
    workspaceId: string,
    dataSourceId: string,
    enabled: boolean
  ): Promise<DataSourceResponse> {
    return api.patch<DataSourceResponse>(
      `${this.basePath}/${workspaceId}/datasources/${dataSourceId}`,
      { enabled }
    )
  }

  // =========================================================================
  // Documents
  // =========================================================================

  /**
   * Get documents for a workspace
   */
  async getDocuments(
    workspaceId: string,
    page?: number,
    pageSize?: number
  ): Promise<PagedResponse<DocumentResponse>> {
    const params: Record<string, unknown> = {}
    if (page) params.pageNumber = page
    if (pageSize) params.pageSize = pageSize
    return api.get<PagedResponse<DocumentResponse>>(
      `${this.basePath}/${workspaceId}/documents`,
      params
    )
  }

  /**
   * Get document by ID
   */
  async getDocument(workspaceId: string, documentId: string): Promise<DocumentResponse> {
    return api.get<DocumentResponse>(`${this.basePath}/${workspaceId}/documents/${documentId}`)
  }

  /**
   * Get document chunks
   */
  async getDocumentChunks(
    workspaceId: string,
    documentId: string
  ): Promise<DocumentChunkResponse[]> {
    return api.get<DocumentChunkResponse[]>(
      `${this.basePath}/${workspaceId}/documents/${documentId}/chunks`
    )
  }

  /**
   * Upload a document
   */
  async uploadDocument(
    workspaceId: string,
    file: File,
    onProgress?: (progress: number) => void
  ): Promise<DocumentResponse> {
    const formData = new FormData()
    formData.append('file', file)

    const response = await api.getClient().post<DocumentResponse>(
      `${this.basePath}/${workspaceId}/documents/upload`,
      formData,
      {
        headers: { 'Content-Type': 'multipart/form-data' },
        onUploadProgress: (progressEvent) => {
          if (onProgress && progressEvent.total) {
            const progress = Math.round((progressEvent.loaded * 100) / progressEvent.total)
            onProgress(progress)
          }
        }
      }
    )
    return response.data
  }

  /**
   * Upload multiple documents
   */
  async uploadDocuments(
    workspaceId: string,
    files: File[],
    onProgress?: (progress: number, fileName: string) => void
  ): Promise<DocumentResponse[]> {
    const results: DocumentResponse[] = []
    for (const file of files) {
      const result = await this.uploadDocument(workspaceId, file, (progress) => {
        onProgress?.(progress, file.name)
      })
      results.push(result)
    }
    return results
  }

  /**
   * Delete document
   */
  async deleteDocument(workspaceId: string, documentId: string): Promise<void> {
    return api.delete<void>(`${this.basePath}/${workspaceId}/documents/${documentId}`)
  }

  /**
   * Reprocess document
   */
  async reprocessDocument(workspaceId: string, documentId: string): Promise<void> {
    return api.post<void>(`${this.basePath}/${workspaceId}/documents/${documentId}/reprocess`)
  }

  // =========================================================================
  // Sharing
  // =========================================================================

  /**
   * Get workspace shares
   */
  async getShares(workspaceId: string): Promise<WorkspaceShareResponse[]> {
    return api.get<WorkspaceShareResponse[]>(`${this.basePath}/${workspaceId}/shares`)
  }

  /**
   * Share workspace with user
   */
  async shareWithUser(
    workspaceId: string,
    request: ShareWorkspaceRequest
  ): Promise<WorkspaceShareResponse> {
    return api.post<WorkspaceShareResponse>(`${this.basePath}/${workspaceId}/shares`, request)
  }

  /**
   * Update share role
   */
  async updateShare(
    workspaceId: string,
    shareId: string,
    role: WorkspaceRole
  ): Promise<WorkspaceShareResponse> {
    return api.put<WorkspaceShareResponse>(`${this.basePath}/${workspaceId}/shares/${shareId}`, {
      role
    })
  }

  /**
   * Remove share
   */
  async removeShare(workspaceId: string, shareId: string): Promise<void> {
    return api.delete<void>(`${this.basePath}/${workspaceId}/shares/${shareId}`)
  }

  // =========================================================================
  // Shareable Links
  // =========================================================================

  /**
   * Get shareable links
   */
  async getShareableLinks(workspaceId: string): Promise<ShareableLinkResponse[]> {
    return api.get<ShareableLinkResponse[]>(`${this.basePath}/${workspaceId}/links`)
  }

  /**
   * Create shareable link
   */
  async createShareableLink(
    workspaceId: string,
    request: CreateShareableLinkRequest
  ): Promise<ShareableLinkResponse> {
    return api.post<ShareableLinkResponse>(`${this.basePath}/${workspaceId}/links`, request)
  }

  /**
   * Revoke shareable link
   */
  async revokeShareableLink(workspaceId: string, linkId: string): Promise<void> {
    return api.delete<void>(`${this.basePath}/${workspaceId}/links/${linkId}`)
  }

  // =========================================================================
  // Mappers
  // =========================================================================

  /**
   * Map backend response to frontend Workspace type
   */
  mapToWorkspace(response: WorkspaceResponse): Workspace {
    return {
      id: response.id,
      name: response.name,
      description: response.description,
      teamId: response.teamId,
      createdBy: response.createdBy,
      status: response.status as Workspace['status'],
      createdAt: new Date().toISOString() // Backend should return this
    }
  }

  /**
   * Map backend response to frontend DataSource type
   */
  mapToDataSource(response: DataSourceResponse): DataSource {
    return {
      id: response.id,
      workspaceId: response.workspaceId,
      name: response.name,
      type: response.type as DataSource['type'],
      status: response.status as DataSource['status'],
      config: response.config,
      lastSyncAt: response.lastSyncAt,
      nextSyncAt: response.nextSyncAt,
      documentCount: response.documentCount,
      createdAt: response.createdAt
    }
  }

  /**
   * Map backend response to frontend Document type
   */
  mapToDocument(response: DocumentResponse): Document {
    return {
      id: response.id,
      workspaceId: response.workspaceId,
      dataSourceId: response.dataSourceId,
      name: response.name,
      type: response.type as Document['type'],
      status: response.status as Document['status'],
      size: response.size,
      chunkCount: response.chunkCount,
      metadata: response.metadata,
      createdAt: response.createdAt,
      processedAt: response.processedAt
    }
  }

  /**
   * Map backend response to frontend WorkspaceShare type
   */
  mapToShare(response: WorkspaceShareResponse): WorkspaceShare {
    return {
      id: response.id,
      workspaceId: response.workspaceId,
      userId: response.userId,
      role: response.role as WorkspaceShare['role'],
      createdAt: response.createdAt,
      createdBy: response.createdBy
    }
  }

  /**
   * Map backend response to frontend ShareableLink type
   */
  mapToShareableLink(response: ShareableLinkResponse): ShareableLink {
    return {
      id: response.id,
      workspaceId: response.workspaceId,
      token: response.token,
      role: response.role as ShareableLink['role'],
      expiresAt: response.expiresAt,
      maxUses: response.maxUses,
      useCount: response.useCount,
      isActive: response.isActive,
      createdAt: response.createdAt,
      createdBy: response.createdBy
    }
  }
}

export const workspaceService = new WorkspaceService()
export default workspaceService
