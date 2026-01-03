import api from './api'

// =============================================================================
// Types
// =============================================================================

export type ApiKeyScope = 'read' | 'write' | 'delete' | 'admin' | 'query' | 'documents' | 'workspaces'

export interface ApiKey {
  id: string
  name: string
  prefix: string // First 8 characters of the key for identification
  scopes: ApiKeyScope[]
  createdAt: string
  expiresAt?: string
  lastUsedAt?: string
  isActive: boolean
  usageCount: number
}

export interface CreateApiKeyRequest {
  name: string
  scopes: ApiKeyScope[]
  expiresInDays?: number // null for never expires
  description?: string
}

export interface CreateApiKeyResponse {
  id: string
  name: string
  key: string // Full key - only shown once
  prefix: string
  scopes: ApiKeyScope[]
  createdAt: string
  expiresAt?: string
}

export interface UpdateApiKeyRequest {
  name?: string
  scopes?: ApiKeyScope[]
  isActive?: boolean
}

export interface ApiKeyUsageStats {
  keyId: string
  totalRequests: number
  requestsByDay: { date: string; count: number }[]
  requestsByEndpoint: { endpoint: string; count: number }[]
  lastUsedAt?: string
  averageResponseTime: number
}

// =============================================================================
// Scope Definitions
// =============================================================================

export const API_KEY_SCOPES: { value: ApiKeyScope; label: string; description: string; category: string }[] = [
  // General access
  { value: 'read', label: 'Read', description: 'Read access to all resources', category: 'General' },
  { value: 'write', label: 'Write', description: 'Create and update resources', category: 'General' },
  { value: 'delete', label: 'Delete', description: 'Delete resources', category: 'General' },
  { value: 'admin', label: 'Admin', description: 'Administrative operations', category: 'General' },
  // Specific access
  { value: 'query', label: 'Query', description: 'Execute RAG queries', category: 'Features' },
  { value: 'documents', label: 'Documents', description: 'Manage documents', category: 'Features' },
  { value: 'workspaces', label: 'Workspaces', description: 'Manage workspaces', category: 'Features' }
]

export const EXPIRATION_OPTIONS = [
  { value: 7, label: '7 days' },
  { value: 30, label: '30 days' },
  { value: 90, label: '90 days' },
  { value: 180, label: '6 months' },
  { value: 365, label: '1 year' },
  { value: 0, label: 'Never expires' }
]

// =============================================================================
// API Key Service
// =============================================================================

class ApiKeyService {
  private readonly basePath = '/api-keys'

  /**
   * Get all API keys for current user
   */
  async getApiKeys(): Promise<ApiKey[]> {
    return api.get<ApiKey[]>(this.basePath)
  }

  /**
   * Get a specific API key by ID
   */
  async getApiKey(keyId: string): Promise<ApiKey> {
    return api.get<ApiKey>(`${this.basePath}/${keyId}`)
  }

  /**
   * Create a new API key
   */
  async createApiKey(request: CreateApiKeyRequest): Promise<CreateApiKeyResponse> {
    return api.post<CreateApiKeyResponse>(this.basePath, request)
  }

  /**
   * Update an API key
   */
  async updateApiKey(keyId: string, request: UpdateApiKeyRequest): Promise<ApiKey> {
    return api.patch<ApiKey>(`${this.basePath}/${keyId}`, request)
  }

  /**
   * Revoke (delete) an API key
   */
  async revokeApiKey(keyId: string): Promise<void> {
    return api.delete<void>(`${this.basePath}/${keyId}`)
  }

  /**
   * Get usage statistics for an API key
   */
  async getApiKeyUsage(keyId: string, days: number = 30): Promise<ApiKeyUsageStats> {
    return api.get<ApiKeyUsageStats>(`${this.basePath}/${keyId}/usage?days=${days}`)
  }

  /**
   * Regenerate an API key (creates new key, revokes old one)
   */
  async regenerateApiKey(keyId: string): Promise<CreateApiKeyResponse> {
    return api.post<CreateApiKeyResponse>(`${this.basePath}/${keyId}/regenerate`)
  }

  /**
   * Check if a key name is already in use
   */
  async checkKeyName(name: string): Promise<{ available: boolean }> {
    return api.get<{ available: boolean }>(`${this.basePath}/check-name?name=${encodeURIComponent(name)}`)
  }
}

export const apiKeyService = new ApiKeyService()
export default apiKeyService
