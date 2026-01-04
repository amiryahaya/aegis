import api from './api'

// =============================================================================
// Response Types (matching backend DTOs)
// =============================================================================

export interface AuditLogEntryResponse {
  id: string
  action: string
  category: string
  userId?: string
  username?: string
  workspaceId?: string
  teamId?: string
  resourceType?: string
  resourceId?: string
  description?: string
  ipAddress?: string
  userAgent?: string
  success: boolean
  errorMessage?: string
  severity: string
  correlationId?: string
  timestamp: string
}

export interface AuditLogQueryResponse {
  entries: AuditLogEntryResponse[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface AuditStatisticsResponse {
  totalEvents: number
  successfulEvents: number
  failedEvents: number
  uniqueUsers: number
  actionBreakdown: Record<string, number>
  categoryBreakdown: Record<string, number>
  severityBreakdown: Record<string, number>
  dailyBreakdown: Record<string, number>
  fromDate: string
  toDate: string
}

export interface AuditLogQuery {
  actions?: string[]
  categories?: string[]
  userId?: string
  workspaceId?: string
  teamId?: string
  resourceType?: string
  success?: boolean
  severity?: string
  fromDate?: Date
  toDate?: Date
  search?: string
  page?: number
  pageSize?: number
  sortBy?: string
  sortDesc?: boolean
}

export interface ExportAuditRequest {
  format: 'json' | 'csv' | 'pdf' | 'excel'
  actions?: string[]
  categories?: string[]
  userId?: string
  workspaceId?: string
  teamId?: string
  fromDate?: Date
  toDate?: Date
}

// =============================================================================
// Audit Service
// =============================================================================

class AuditService {
  private baseUrl = '/audit'

  /**
   * Query audit logs with filtering and pagination
   */
  async query(query: AuditLogQuery): Promise<AuditLogQueryResponse> {
    const params = new URLSearchParams()

    if (query.actions?.length) params.set('actions', query.actions.join(','))
    if (query.categories?.length) params.set('categories', query.categories.join(','))
    if (query.userId) params.set('userId', query.userId)
    if (query.workspaceId) params.set('workspaceId', query.workspaceId)
    if (query.teamId) params.set('teamId', query.teamId)
    if (query.resourceType) params.set('resourceType', query.resourceType)
    if (query.success !== undefined) params.set('success', query.success.toString())
    if (query.severity) params.set('severity', query.severity)
    if (query.fromDate) params.set('fromDate', query.fromDate.toISOString())
    if (query.toDate) params.set('toDate', query.toDate.toISOString())
    if (query.search) params.set('search', query.search)
    if (query.page) params.set('page', query.page.toString())
    if (query.pageSize) params.set('pageSize', query.pageSize.toString())
    if (query.sortBy) params.set('sortBy', query.sortBy)
    if (query.sortDesc !== undefined) params.set('sortDesc', query.sortDesc.toString())

    return await api.get<AuditLogQueryResponse>(`${this.baseUrl}?${params}`)
  }

  /**
   * Get a specific audit log entry by ID
   */
  async getById(id: string): Promise<AuditLogEntryResponse> {
    return await api.get<AuditLogEntryResponse>(`${this.baseUrl}/${id}`)
  }

  /**
   * Get audit statistics for a time period
   */
  async getStatistics(params: {
    workspaceId?: string
    teamId?: string
    fromDate?: Date
    toDate?: Date
  }): Promise<AuditStatisticsResponse> {
    const query = new URLSearchParams()
    if (params.workspaceId) query.set('workspaceId', params.workspaceId)
    if (params.teamId) query.set('teamId', params.teamId)
    if (params.fromDate) query.set('fromDate', params.fromDate.toISOString())
    if (params.toDate) query.set('toDate', params.toDate.toISOString())

    return await api.get<AuditStatisticsResponse>(`${this.baseUrl}/statistics?${query}`)
  }

  /**
   * Export audit logs to a specific format
   */
  async export(request: ExportAuditRequest): Promise<Blob> {
    const client = api.getClient()
    const response = await client.post(`${this.baseUrl}/export`, request, {
      responseType: 'blob'
    })
    return response.data
  }
}

export const auditService = new AuditService()
export default auditService
