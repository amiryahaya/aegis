import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import api from '@/services/api'
import type { Workspace, PagedResponse } from '@/types'
import type {
  CreateWorkspaceRequest,
  UpdateWorkspaceRequest,
  DataSource,
  CreateDataSourceRequest,
  Document,
  WorkspaceShare,
  ShareableLink,
  CreateShareableLinkRequest
} from '@/types/workspace'

export const useWorkspaceStore = defineStore('workspace', () => {
  // State
  const workspaces = ref<Workspace[]>([])
  const currentWorkspace = ref<Workspace | null>(null)
  const dataSources = ref<DataSource[]>([])
  const documents = ref<Document[]>([])
  const shares = ref<WorkspaceShare[]>([])
  const shareableLinks = ref<ShareableLink[]>([])
  const isLoading = ref(false)
  const error = ref<string | null>(null)

  // Getters
  const workspaceCount = computed(() => workspaces.value.length)
  const totalDocuments = computed(() =>
    workspaces.value.reduce((sum, ws) => sum + (ws.stats?.documentCount || 0), 0)
  )

  // Actions - Workspaces
  async function fetchWorkspaces(teamId: string): Promise<Workspace[]> {
    isLoading.value = true
    error.value = null
    try {
      const response = await api.get<PagedResponse<Workspace>>(`/workspaces`, { teamId })
      workspaces.value = response.items
      return response.items
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
      const workspace = await api.get<Workspace>(`/workspaces/${id}`)
      currentWorkspace.value = workspace
      return workspace
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch workspace'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function createWorkspace(request: CreateWorkspaceRequest): Promise<Workspace | null> {
    isLoading.value = true
    error.value = null
    try {
      const workspace = await api.post<Workspace>('/workspaces', request)
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
      const workspace = await api.put<Workspace>(`/workspaces/${id}`, request)
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
      await api.delete(`/workspaces/${id}`)
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

  // Actions - Data Sources
  async function fetchDataSources(workspaceId: string): Promise<DataSource[]> {
    isLoading.value = true
    error.value = null
    try {
      const response = await api.get<PagedResponse<DataSource>>(`/workspaces/${workspaceId}/datasources`)
      dataSources.value = response.items
      return response.items
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
      const dataSource = await api.post<DataSource>(`/workspaces/${request.workspaceId}/datasources`, request)
      dataSources.value.push(dataSource)
      return dataSource
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to create data source'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function syncDataSource(workspaceId: string, dataSourceId: string): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await api.post(`/workspaces/${workspaceId}/datasources/${dataSourceId}/sync`)
      // Refresh the data source to get updated status
      const updated = await api.get<DataSource>(`/workspaces/${workspaceId}/datasources/${dataSourceId}`)
      const index = dataSources.value.findIndex(ds => ds.id === dataSourceId)
      if (index !== -1) {
        dataSources.value[index] = updated
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
      await api.delete(`/workspaces/${workspaceId}/datasources/${dataSourceId}`)
      dataSources.value = dataSources.value.filter(ds => ds.id !== dataSourceId)
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to delete data source'
      return false
    } finally {
      isLoading.value = false
    }
  }

  // Actions - Documents
  async function fetchDocuments(workspaceId: string, page = 1, pageSize = 20): Promise<PagedResponse<Document> | null> {
    isLoading.value = true
    error.value = null
    try {
      const response = await api.get<PagedResponse<Document>>(`/workspaces/${workspaceId}/documents`, {
        pageNumber: page,
        pageSize
      })
      documents.value = response.items
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch documents'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function uploadDocument(workspaceId: string, file: File): Promise<Document | null> {
    isLoading.value = true
    error.value = null
    try {
      const formData = new FormData()
      formData.append('file', file)

      const response = await api.getClient().post<Document>(
        `/workspaces/${workspaceId}/documents/upload`,
        formData,
        {
          headers: { 'Content-Type': 'multipart/form-data' }
        }
      )
      documents.value.push(response.data)
      return response.data
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to upload document'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function deleteDocument(workspaceId: string, documentId: string): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await api.delete(`/workspaces/${workspaceId}/documents/${documentId}`)
      documents.value = documents.value.filter(doc => doc.id !== documentId)
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to delete document'
      return false
    } finally {
      isLoading.value = false
    }
  }

  // Actions - Sharing
  async function fetchShares(workspaceId: string): Promise<WorkspaceShare[]> {
    isLoading.value = true
    error.value = null
    try {
      const response = await api.get<WorkspaceShare[]>(`/workspaces/${workspaceId}/shares`)
      shares.value = response
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch shares'
      return []
    } finally {
      isLoading.value = false
    }
  }

  async function createShareableLink(workspaceId: string, request: CreateShareableLinkRequest): Promise<ShareableLink | null> {
    isLoading.value = true
    error.value = null
    try {
      const link = await api.post<ShareableLink>(`/workspaces/${workspaceId}/links`, request)
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
      await api.delete(`/workspaces/${workspaceId}/links/${linkId}`)
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
    dataSources.value = []
    documents.value = []
    shares.value = []
    shareableLinks.value = []
  }

  function clearError() {
    error.value = null
  }

  return {
    // State
    workspaces,
    currentWorkspace,
    dataSources,
    documents,
    shares,
    shareableLinks,
    isLoading,
    error,

    // Getters
    workspaceCount,
    totalDocuments,

    // Actions - Workspaces
    fetchWorkspaces,
    fetchWorkspace,
    createWorkspace,
    updateWorkspace,
    deleteWorkspace,

    // Actions - Data Sources
    fetchDataSources,
    createDataSource,
    syncDataSource,
    deleteDataSource,

    // Actions - Documents
    fetchDocuments,
    uploadDocument,
    deleteDocument,

    // Actions - Sharing
    fetchShares,
    createShareableLink,
    revokeShareableLink,

    // Cleanup
    clearCurrent,
    clearError
  }
})
