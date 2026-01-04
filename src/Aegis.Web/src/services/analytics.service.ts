import api from './api'

// =============================================================================
// Response Types (matching backend DTOs)
// =============================================================================

export interface UsageSummaryResponse {
  totalEvents: number
  totalQueries: number
  totalDocuments: number
  totalTokensUsed: number
  totalCost: number
  averageResponseTimeMs: number
  cacheHitRate: number
  uniqueUsers: number
  activeWorkspaces: number
  fromDate: string
  toDate: string
}

export interface TrendDataPointResponse {
  timestamp: string
  period: string
  queries: number
  documents: number
  tokens: number
  cost: number
  averageResponseTimeMs: number
  uniqueUsers: number
}

export interface UsageTrendsResponse {
  dataPoints: TrendDataPointResponse[]
  granularity: string
  fromDate: string
  toDate: string
}

export interface UserUsageStatsResponse {
  userId: string
  username?: string
  queryCount: number
  documentCount: number
  tokensUsed: number
  cost: number
  averageResponseTimeMs: number
  lastActive: string
}

export interface WorkspaceUsageStatsResponse {
  workspaceId: string
  workspaceName?: string
  queryCount: number
  documentCount: number
  tokensUsed: number
  cost: number
  uniqueUsers: number
  averageResponseTimeMs: number
}

export interface CostAnalysisResponse {
  totalCost: number
  averageDailyCost: number
  projectedMonthlyCost: number
  costByModel: Record<string, number>
  dailyCosts: Record<string, number>
  costSavedByCache: number
  tokensUsed: number
  costPerThousandTokens: number
  fromDate: string
  toDate: string
}

export interface RealTimeMetricsResponse {
  activeUsers: number
  queriesLastMinute: number
  queriesLastHour: number
  averageResponseTimeMs: number
  cacheHitRate: number
  costLastHour: number
  tokensLastHour: number
  timestamp: string
}

// =============================================================================
// Analytics Service
// =============================================================================

class AnalyticsService {
  private baseUrl = '/analytics'

  /**
   * Get usage summary for a time period
   */
  async getSummary(params: {
    userId?: string
    workspaceId?: string
    teamId?: string
    fromDate?: Date
    toDate?: Date
  }): Promise<UsageSummaryResponse> {
    const query = new URLSearchParams()
    if (params.userId) query.set('userId', params.userId)
    if (params.workspaceId) query.set('workspaceId', params.workspaceId)
    if (params.teamId) query.set('teamId', params.teamId)
    if (params.fromDate) query.set('fromDate', params.fromDate.toISOString())
    if (params.toDate) query.set('toDate', params.toDate.toISOString())

    return await api.get<UsageSummaryResponse>(`${this.baseUrl}/summary?${query}`)
  }

  /**
   * Get usage trends over time
   */
  async getTrends(params: {
    userId?: string
    workspaceId?: string
    teamId?: string
    fromDate?: Date
    toDate?: Date
    granularity?: 'hourly' | 'daily' | 'weekly' | 'monthly'
  }): Promise<UsageTrendsResponse> {
    const query = new URLSearchParams()
    if (params.userId) query.set('userId', params.userId)
    if (params.workspaceId) query.set('workspaceId', params.workspaceId)
    if (params.teamId) query.set('teamId', params.teamId)
    if (params.fromDate) query.set('fromDate', params.fromDate.toISOString())
    if (params.toDate) query.set('toDate', params.toDate.toISOString())
    if (params.granularity) query.set('granularity', params.granularity)

    return await api.get<UsageTrendsResponse>(`${this.baseUrl}/trends?${query}`)
  }

  /**
   * Get top users by usage
   */
  async getTopUsers(params: {
    workspaceId?: string
    teamId?: string
    fromDate?: Date
    toDate?: Date
    limit?: number
  }): Promise<UserUsageStatsResponse[]> {
    const query = new URLSearchParams()
    if (params.workspaceId) query.set('workspaceId', params.workspaceId)
    if (params.teamId) query.set('teamId', params.teamId)
    if (params.fromDate) query.set('fromDate', params.fromDate.toISOString())
    if (params.toDate) query.set('toDate', params.toDate.toISOString())
    if (params.limit) query.set('limit', params.limit.toString())

    return await api.get<UserUsageStatsResponse[]>(`${this.baseUrl}/users/top?${query}`)
  }

  /**
   * Get usage by workspace
   */
  async getWorkspaceUsage(params: {
    teamId?: string
    fromDate?: Date
    toDate?: Date
    limit?: number
  }): Promise<WorkspaceUsageStatsResponse[]> {
    const query = new URLSearchParams()
    if (params.teamId) query.set('teamId', params.teamId)
    if (params.fromDate) query.set('fromDate', params.fromDate.toISOString())
    if (params.toDate) query.set('toDate', params.toDate.toISOString())
    if (params.limit) query.set('limit', params.limit.toString())

    return await api.get<WorkspaceUsageStatsResponse[]>(`${this.baseUrl}/workspaces?${query}`)
  }

  /**
   * Get cost analysis
   */
  async getCostAnalysis(params: {
    userId?: string
    workspaceId?: string
    teamId?: string
    fromDate?: Date
    toDate?: Date
  }): Promise<CostAnalysisResponse> {
    const query = new URLSearchParams()
    if (params.userId) query.set('userId', params.userId)
    if (params.workspaceId) query.set('workspaceId', params.workspaceId)
    if (params.teamId) query.set('teamId', params.teamId)
    if (params.fromDate) query.set('fromDate', params.fromDate.toISOString())
    if (params.toDate) query.set('toDate', params.toDate.toISOString())

    return await api.get<CostAnalysisResponse>(`${this.baseUrl}/costs?${query}`)
  }

  /**
   * Get real-time metrics
   */
  async getRealTimeMetrics(): Promise<RealTimeMetricsResponse> {
    return await api.get<RealTimeMetricsResponse>(`${this.baseUrl}/realtime`)
  }

  /**
   * Export analytics data to a specific format
   */
  async export(request: ExportAnalyticsRequest): Promise<Blob> {
    const client = api.getClient()
    const response = await client.post(`${this.baseUrl}/export`, request, {
      responseType: 'blob'
    })
    return response.data
  }
}

export interface ExportAnalyticsRequest {
  format: 'json' | 'csv' | 'pdf' | 'xlsx'
  userId?: string
  workspaceId?: string
  teamId?: string
  fromDate?: Date
  toDate?: Date
}

export const analyticsService = new AnalyticsService()
export default analyticsService
