import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import workspaceService from '@/services/workspace.service'
import documentService from '@/services/document.service'
import type { Workspace, PagedResponse } from '@/types'
import type {
  DataSource,
  Document,
  WorkspaceShare,
  ShareableLink,
  CreateShareableLinkRequest
} from '@/types/workspace'
import type {
  CreateWorkspaceRequest as ServiceCreateWorkspaceRequest,
  UpdateWorkspaceRequest,
  CreateDataSourceRequest,
  UpdateDataSourceRequest,
  WorkspaceContextResponse
} from '@/services/workspace.service'
import type { CreateWorkspaceRequest } from '@/types/workspace'
import type { UploadProgress } from '@/services/document.service'

export const useWorkspaceStore = defineStore('workspace', () => {
  // State
  const workspaces = ref<Workspace[]>([])
  const currentWorkspace = ref<Workspace | null>(null)
  const workspaceContext = ref<WorkspaceContextResponse | null>(null)
  const dataSources = ref<DataSource[]>([])
  const documents = ref<Document[]>([])
  const shares = ref<WorkspaceShare[]>([])
  const shareableLinks = ref<ShareableLink[]>([])
  const isLoading = ref(false)
  const error = ref<string | null>(null)
  const uploadProgress = ref<Map<string, UploadProgress>>(new Map())

  // Pagination state
  const totalWorkspaces = ref(0)
  const totalDocuments = ref(0)
  const currentPage = ref(1)
  const pageSize = ref(20)

  // Getters
  const workspaceCount = computed(() => workspaces.value.length)
  const documentCount = computed(() =>
    workspaces.value.reduce((sum, ws) => sum + (ws.stats?.documentCount || 0), 0)
  )
  const hasMoreWorkspaces = computed(() => workspaces.value.length < totalWorkspaces.value)

  // Actions - Workspaces
  async function fetchWorkspaces(teamId?: string, page = 1, size = 20): Promise<Workspace[]> {
    isLoading.value = true
    error.value = null
    try {
      const response = await workspaceService.getPaged(page, size, teamId)
      workspaces.value = response.items.map(ws => workspaceService.mapToWorkspace(ws))
      totalWorkspaces.value = response.totalCount
      currentPage.value = page
      pageSize.value = size
      return workspaces.value
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch workspaces'
      return []
    } finally {
      isLoading.value = false
    }
  }

  async function fetchWorkspace(id: string): Promise<Workspace | null> {
    isLoading.value = true
    error.value = null
    try {
      const response = await workspaceService.getById(id)
      const workspace = workspaceService.mapToWorkspace(response)
      currentWorkspace.value = workspace
      return workspace
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch workspace'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function createWorkspace(request: CreateWorkspaceRequest, createdBy?: string): Promise<Workspace | null> {
    isLoading.value = true
    error.value = null
    try {
      // Convert frontend request to service request
      const serviceRequest: ServiceCreateWorkspaceRequest = {
        name: request.name,
        description: request.description,
        teamId: request.teamId,
        createdBy: createdBy || 'current-user' // Backend should use authenticated user if not provided
      }
      const response = await workspaceService.create(serviceRequest)
      const workspace = workspaceService.mapToWorkspace(response)
      workspaces.value.push(workspace)
      return workspace
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to create workspace'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function updateWorkspace(id: string, request: UpdateWorkspaceRequest): Promise<Workspace | null> {
    isLoading.value = true
    error.value = null
    try {
      const response = await workspaceService.update(id, request)
      const workspace = workspaceService.mapToWorkspace(response)
      const index = workspaces.value.findIndex(ws => ws.id === id)
      if (index !== -1) {
        workspaces.value[index] = workspace
      }
      if (currentWorkspace.value?.id === id) {
        currentWorkspace.value = workspace
      }
      return workspace
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to update workspace'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function deleteWorkspace(id: string): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await workspaceService.delete(id)
      workspaces.value = workspaces.value.filter(ws => ws.id !== id)
      if (currentWorkspace.value?.id === id) {
        currentWorkspace.value = null
      }
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to delete workspace'
      return false
    } finally {
      isLoading.value = false
    }
  }

  async function archiveWorkspace(id: string): Promise<Workspace | null> {
    isLoading.value = true
    error.value = null
    try {
      const response = await workspaceService.archive(id)
      const workspace = workspaceService.mapToWorkspace(response)
      const index = workspaces.value.findIndex(ws => ws.id === id)
      if (index !== -1) {
        workspaces.value[index] = workspace
      }
      if (currentWorkspace.value?.id === id) {
        currentWorkspace.value = workspace
      }
      return workspace
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to archive workspace'
      return null
    } finally {
      isLoading.value = false
    }
  }

  // Actions - Context
  async function fetchWorkspaceContext(id: string): Promise<WorkspaceContextResponse | null> {
    isLoading.value = true
    error.value = null
    try {
      const context = await workspaceService.getContext(id)
      workspaceContext.value = context
      return context
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch workspace context'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function fetchRelevantContext(
    id: string,
    query: string,
    maxEntities = 10,
    maxFindings = 5,
    maxFacts = 10
  ): Promise<WorkspaceContextResponse | null> {
    try {
      return await workspaceService.getRelevantContext(id, {
        query,
        maxEntities,
        maxFindings,
        maxFacts
      })
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch relevant context'
      return null
    }
  }

  // Actions - Data Sources
  async function fetchDataSources(workspaceId: string): Promise<DataSource[]> {
    isLoading.value = true
    error.value = null
    try {
      const response = await workspaceService.getDataSources(workspaceId)
      dataSources.value = response.map(ds => workspaceService.mapToDataSource(ds))
      return dataSources.value
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch data sources'
      return []
    } finally {
      isLoading.value = false
    }
  }

  async function createDataSource(request: CreateDataSourceRequest): Promise<DataSource | null> {
    isLoading.value = true
    error.value = null
    try {
      const response = await workspaceService.createDataSource(request)
      const dataSource = workspaceService.mapToDataSource(response)
      dataSources.value.push(dataSource)
      return dataSource
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to create data source'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function updateDataSource(
    workspaceId: string,
    dataSourceId: string,
    request: UpdateDataSourceRequest
  ): Promise<DataSource | null> {
    isLoading.value = true
    error.value = null
    try {
      const response = await workspaceService.updateDataSource(workspaceId, dataSourceId, request)
      const dataSource = workspaceService.mapToDataSource(response)
      const index = dataSources.value.findIndex(ds => ds.id === dataSourceId)
      if (index !== -1) {
        dataSources.value[index] = dataSource
      }
      return dataSource
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to update data source'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function syncDataSource(workspaceId: string, dataSourceId: string): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await workspaceService.syncDataSource(workspaceId, dataSourceId)
      // Refresh the data source to get updated status
      const updated = await workspaceService.getDataSource(workspaceId, dataSourceId)
      const index = dataSources.value.findIndex(ds => ds.id === dataSourceId)
      if (index !== -1) {
        dataSources.value[index] = workspaceService.mapToDataSource(updated)
      }
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to sync data source'
      return false
    } finally {
      isLoading.value = false
    }
  }

  async function deleteDataSource(workspaceId: string, dataSourceId: string): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await workspaceService.deleteDataSource(workspaceId, dataSourceId)
      dataSources.value = dataSources.value.filter(ds => ds.id !== dataSourceId)
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to delete data source'
      return false
    } finally {
      isLoading.value = false
    }
  }

  // Actions - Documents (using documentService)
  async function fetchDocuments(
    workspaceId: string,
    page = 1,
    size = 20
  ): Promise<PagedResponse<Document> | null> {
    isLoading.value = true
    error.value = null
    try {
      const response = await documentService.getDocuments(workspaceId, page, size)
      documents.value = response.items.map(doc => ({
        id: doc.id,
        workspaceId: doc.workspaceId,
        dataSourceId: doc.dataSourceId,
        name: doc.name,
        type: doc.type as Document['type'],
        status: doc.status as Document['status'],
        size: doc.size,
        chunkCount: doc.chunkCount,
        metadata: doc.metadata,
        createdAt: doc.createdAt,
        processedAt: doc.processedAt
      }))
      totalDocuments.value = response.totalCount
      return {
        items: documents.value,
        totalCount: response.totalCount,
        pageNumber: response.pageNumber,
        pageSize: response.pageSize,
        totalPages: response.totalPages,
        hasNextPage: response.hasNextPage,
        hasPreviousPage: response.hasPreviousPage
      }
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch documents'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function uploadDocument(
    workspaceId: string,
    file: File,
    onProgress?: (progress: UploadProgress) => void
  ): Promise<Document | null> {
    error.value = null
    try {
      const response = await documentService.upload(workspaceId, file, {
        onProgress: (progress) => {
          uploadProgress.value.set(file.name, progress)
          onProgress?.(progress)
        }
      })

      const document: Document = {
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
      documents.value.push(document)
      uploadProgress.value.delete(file.name)
      return document
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to upload document'
      uploadProgress.value.delete(file.name)
      return null
    }
  }

  async function uploadDocuments(
    workspaceId: string,
    files: File[],
    onProgress?: (fileName: string, progress: number, index: number) => void,
    onComplete?: (fileName: string, document: Document, index: number) => void,
    onError?: (fileName: string, error: string, index: number) => void
  ): Promise<Document[]> {
    const results: Document[] = []

    for (let i = 0; i < files.length; i++) {
      const file = files[i]
      const doc = await uploadDocument(workspaceId, file, (progress) => {
        onProgress?.(file.name, progress.progress, i)
      })

      if (doc) {
        results.push(doc)
        onComplete?.(file.name, doc, i)
      } else {
        onError?.(file.name, error.value || 'Upload failed', i)
      }
    }

    return results
  }

  async function deleteDocument(workspaceId: string, documentId: string): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await documentService.delete(workspaceId, documentId)
      documents.value = documents.value.filter(doc => doc.id !== documentId)
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to delete document'
      return false
    } finally {
      isLoading.value = false
    }
  }

  async function reprocessDocument(workspaceId: string, documentId: string): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await documentService.reprocess(workspaceId, documentId)
      // Refresh document to get updated status
      const updated = await documentService.getDocument(workspaceId, documentId)
      const index = documents.value.findIndex(doc => doc.id === documentId)
      if (index !== -1) {
        documents.value[index] = {
          ...documents.value[index],
          status: updated.status as Document['status']
        }
      }
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to reprocess document'
      return false
    } finally {
      isLoading.value = false
    }
  }

  async function searchDocuments(
    workspaceId: string,
    query: string,
    maxResults = 10
  ): Promise<Document[]> {
    try {
      const results = await documentService.search(workspaceId, query, maxResults)
      // Return matching documents from local state or create minimal objects
      return results.map(result => {
        const existing = documents.value.find(d => d.id === result.documentId)
        if (existing) return existing
        return {
          id: result.documentId,
          workspaceId,
          name: result.documentName,
          type: 'Other' as Document['type'],
          status: 'Indexed' as Document['status'],
          size: 0,
          chunkCount: 0,
          metadata: {},
          createdAt: ''
        }
      })
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to search documents'
      return []
    }
  }

  // Actions - Sharing
  async function fetchShares(workspaceId: string): Promise<WorkspaceShare[]> {
    isLoading.value = true
    error.value = null
    try {
      const response = await workspaceService.getShares(workspaceId)
      shares.value = response.map(share => workspaceService.mapToShare(share))
      return shares.value
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch shares'
      return []
    } finally {
      isLoading.value = false
    }
  }

  async function shareWorkspace(
    workspaceId: string,
    userId: string,
    role: import('@/types/workspace').WorkspaceRole
  ): Promise<WorkspaceShare | null> {
    isLoading.value = true
    error.value = null
    try {
      const response = await workspaceService.shareWithUser(workspaceId, { userId, role })
      const share = workspaceService.mapToShare(response)
      shares.value.push(share)
      return share
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to share workspace'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function updateShareRole(
    workspaceId: string,
    shareId: string,
    role: import('@/types/workspace').WorkspaceRole
  ): Promise<WorkspaceShare | null> {
    isLoading.value = true
    error.value = null
    try {
      const response = await workspaceService.updateShare(workspaceId, shareId, role)
      const share = workspaceService.mapToShare(response)
      const index = shares.value.findIndex(s => s.id === shareId)
      if (index !== -1) {
        shares.value[index] = share
      }
      return share
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to update share'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function removeShare(workspaceId: string, shareId: string): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await workspaceService.removeShare(workspaceId, shareId)
      shares.value = shares.value.filter(s => s.id !== shareId)
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to remove share'
      return false
    } finally {
      isLoading.value = false
    }
  }

  async function fetchShareableLinks(workspaceId: string): Promise<ShareableLink[]> {
    isLoading.value = true
    error.value = null
    try {
      const response = await workspaceService.getShareableLinks(workspaceId)
      shareableLinks.value = response.map(link => workspaceService.mapToShareableLink(link))
      return shareableLinks.value
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch shareable links'
      return []
    } finally {
      isLoading.value = false
    }
  }

  async function createShareableLink(
    workspaceId: string,
    request: CreateShareableLinkRequest
  ): Promise<ShareableLink | null> {
    isLoading.value = true
    error.value = null
    try {
      const response = await workspaceService.createShareableLink(workspaceId, {
        role: request.role,
        expiresAt: request.expiresAt,
        maxUses: request.maxUses,
        password: request.password
      })
      const link = workspaceService.mapToShareableLink(response)
      shareableLinks.value.push(link)
      return link
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to create shareable link'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function revokeShareableLink(workspaceId: string, linkId: string): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await workspaceService.revokeShareableLink(workspaceId, linkId)
      shareableLinks.value = shareableLinks.value.filter(link => link.id !== linkId)
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to revoke shareable link'
      return false
    } finally {
      isLoading.value = false
    }
  }

  // Cleanup
  function clearCurrent() {
    currentWorkspace.value = null
    workspaceContext.value = null
    dataSources.value = []
    documents.value = []
    shares.value = []
    shareableLinks.value = []
    uploadProgress.value.clear()
  }

  function clearError() {
    error.value = null
  }

  return {
    // State
    workspaces,
    currentWorkspace,
    workspaceContext,
    dataSources,
    documents,
    shares,
    shareableLinks,
    isLoading,
    error,
    uploadProgress,
    totalWorkspaces,
    totalDocuments,
    currentPage,
    pageSize,

    // Getters
    workspaceCount,
    documentCount,
    hasMoreWorkspaces,

    // Actions - Workspaces
    fetchWorkspaces,
    fetchWorkspace,
    createWorkspace,
    updateWorkspace,
    deleteWorkspace,
    archiveWorkspace,

    // Actions - Context
    fetchWorkspaceContext,
    fetchRelevantContext,

    // Actions - Data Sources
    fetchDataSources,
    createDataSource,
    updateDataSource,
    syncDataSource,
    deleteDataSource,

    // Actions - Documents
    fetchDocuments,
    uploadDocument,
    uploadDocuments,
    deleteDocument,
    reprocessDocument,
    searchDocuments,

    // Actions - Sharing
    fetchShares,
    shareWorkspace,
    updateShareRole,
    removeShare,
    fetchShareableLinks,
    createShareableLink,
    revokeShareableLink,

    // Cleanup
    clearCurrent,
    clearError
  }
})
