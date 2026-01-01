import { defineStore } from 'pinia'
import { ref } from 'vue'
import api from '@/services/api'
import type { PagedResponse } from '@/types'
import type {
  SystemOverview,
  SystemMetrics,
  QueryMetrics,
  DocumentMetrics,
  UserMetrics,
  CacheMetrics,
  ApiKey,
  CreateApiKeyRequest,
  CreateApiKeyResponse,
  UserDetails,
  UpdateUserRequest,
  AuditLogEntry,
  AuditLogFilter
} from '@/types/admin'

export const useAdminStore = defineStore('admin', () => {
  // State
  const overview = ref<SystemOverview | null>(null)
  const metrics = ref<SystemMetrics | null>(null)
  const queryMetrics = ref<QueryMetrics | null>(null)
  const documentMetrics = ref<DocumentMetrics | null>(null)
  const userMetrics = ref<UserMetrics | null>(null)
  const cacheMetrics = ref<CacheMetrics | null>(null)
  const apiKeys = ref<ApiKey[]>([])
  const users = ref<UserDetails[]>([])
  const auditLogs = ref<AuditLogEntry[]>([])
  const isLoading = ref(false)
  const error = ref<string | null>(null)

  // Dashboard Actions
  async function fetchOverview(): Promise<SystemOverview | null> {
    isLoading.value = true
    error.value = null
    try {
      const response = await api.get<SystemOverview>('/admin/overview')
      overview.value = response
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch overview'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function fetchMetrics(): Promise<SystemMetrics | null> {
    isLoading.value = true
    error.value = null
    try {
      const response = await api.get<SystemMetrics>('/admin/metrics')
      metrics.value = response
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch metrics'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function fetchQueryMetrics(): Promise<QueryMetrics | null> {
    try {
      const response = await api.get<QueryMetrics>('/admin/metrics/queries')
      queryMetrics.value = response
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch query metrics'
      return null
    }
  }

  async function fetchDocumentMetrics(): Promise<DocumentMetrics | null> {
    try {
      const response = await api.get<DocumentMetrics>('/admin/metrics/documents')
      documentMetrics.value = response
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch document metrics'
      return null
    }
  }

  async function fetchUserMetrics(): Promise<UserMetrics | null> {
    try {
      const response = await api.get<UserMetrics>('/admin/metrics/users')
      userMetrics.value = response
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch user metrics'
      return null
    }
  }

  async function fetchCacheMetrics(): Promise<CacheMetrics | null> {
    try {
      const response = await api.get<CacheMetrics>('/admin/metrics/cache')
      cacheMetrics.value = response
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch cache metrics'
      return null
    }
  }

  // API Key Actions
  async function fetchApiKeys(): Promise<ApiKey[]> {
    isLoading.value = true
    error.value = null
    try {
      const response = await api.get<ApiKey[]>('/api-keys')
      apiKeys.value = response
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch API keys'
      return []
    } finally {
      isLoading.value = false
    }
  }

  async function createApiKey(request: CreateApiKeyRequest): Promise<CreateApiKeyResponse | null> {
    isLoading.value = true
    error.value = null
    try {
      const response = await api.post<CreateApiKeyResponse>('/api-keys', request)
      await fetchApiKeys() // Refresh list
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to create API key'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function revokeApiKey(keyId: string): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await api.delete(`/api-keys/${keyId}`)
      apiKeys.value = apiKeys.value.filter(k => k.id !== keyId)
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to revoke API key'
      return false
    } finally {
      isLoading.value = false
    }
  }

  // User Management Actions
  async function fetchUsers(page = 1, pageSize = 20): Promise<PagedResponse<UserDetails> | null> {
    isLoading.value = true
    error.value = null
    try {
      const response = await api.get<PagedResponse<UserDetails>>('/admin/users', {
        pageNumber: page,
        pageSize
      })
      users.value = response.items
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch users'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function updateUser(userId: string, request: UpdateUserRequest): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await api.put(`/admin/users/${userId}`, request)
      const index = users.value.findIndex(u => u.id === userId)
      if (index !== -1) {
        users.value[index] = { ...users.value[index], ...request }
      }
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to update user'
      return false
    } finally {
      isLoading.value = false
    }
  }

  async function deactivateUser(userId: string): Promise<boolean> {
    return updateUser(userId, { isActive: false })
  }

  async function reactivateUser(userId: string): Promise<boolean> {
    return updateUser(userId, { isActive: true })
  }

  // Audit Log Actions
  async function fetchAuditLogs(filter?: AuditLogFilter): Promise<PagedResponse<AuditLogEntry> | null> {
    isLoading.value = true
    error.value = null
    try {
      const response = await api.get<PagedResponse<AuditLogEntry>>('/admin/audit-logs', filter as Record<string, unknown>)
      auditLogs.value = response.items
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch audit logs'
      return null
    } finally {
      isLoading.value = false
    }
  }

  // Cache Actions
  async function clearCache(cacheType: 'semantic' | 'embedding' | 'response' | 'all'): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await api.post(`/admin/cache/clear`, { type: cacheType })
      await fetchCacheMetrics()
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to clear cache'
      return false
    } finally {
      isLoading.value = false
    }
  }

  // Cleanup
  function clearError() {
    error.value = null
  }

  function reset() {
    overview.value = null
    metrics.value = null
    queryMetrics.value = null
    documentMetrics.value = null
    userMetrics.value = null
    cacheMetrics.value = null
    apiKeys.value = []
    users.value = []
    auditLogs.value = []
  }

  return {
    // State
    overview,
    metrics,
    queryMetrics,
    documentMetrics,
    userMetrics,
    cacheMetrics,
    apiKeys,
    users,
    auditLogs,
    isLoading,
    error,

    // Dashboard Actions
    fetchOverview,
    fetchMetrics,
    fetchQueryMetrics,
    fetchDocumentMetrics,
    fetchUserMetrics,
    fetchCacheMetrics,

    // API Key Actions
    fetchApiKeys,
    createApiKey,
    revokeApiKey,

    // User Management Actions
    fetchUsers,
    updateUser,
    deactivateUser,
    reactivateUser,

    // Audit Log Actions
    fetchAuditLogs,

    // Cache Actions
    clearCache,

    // Cleanup
    clearError,
    reset
  }
})
