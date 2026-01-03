/**
 * Analytics & User Insights Types
 */

export type DateRangePreset = 'today' | 'yesterday' | 'last7days' | 'last30days' | 'last90days' | 'thisMonth' | 'lastMonth' | 'thisYear' | 'custom'

export interface DateRange {
  start: Date
  end: Date
  preset?: DateRangePreset
}

export interface TimeSeriesDataPoint {
  date: string
  value: number
  label?: string
}

export interface CategoryDataPoint {
  category: string
  value: number
  percentage?: number
  color?: string
}

// User Usage Analytics
export interface UserUsageStats {
  userId: string
  period: DateRange
  totalQueries: number
  totalSessions: number
  totalDocumentsViewed: number
  averageSessionDuration: number
  averageQueriesPerSession: number
  averageResponseTime: number
  peakUsageHour: number
  mostActiveDay: string
  tokensUsed: number
  cacheHitRate: number
}

export interface QueryAnalytics {
  totalQueries: number
  successfulQueries: number
  failedQueries: number
  averageResponseTime: number
  medianResponseTime: number
  p95ResponseTime: number
  queriesByType: CategoryDataPoint[]
  queriesByWorkspace: CategoryDataPoint[]
  queriesOverTime: TimeSeriesDataPoint[]
  topKeywords: { keyword: string; count: number }[]
  queryComplexityDistribution: CategoryDataPoint[]
}

export interface SessionAnalytics {
  totalSessions: number
  activeSessions: number
  completedSessions: number
  averageDuration: number
  averageTurnsPerSession: number
  sessionsByType: CategoryDataPoint[]
  sessionsOverTime: TimeSeriesDataPoint[]
  sessionCompletionRate: number
  abandonmentRate: number
  returningUserRate: number
}

export interface DocumentAnalytics {
  totalDocuments: number
  documentsIndexed: number
  documentsPending: number
  documentsFailed: number
  totalChunks: number
  averageChunksPerDocument: number
  documentsByType: CategoryDataPoint[]
  documentsByWorkspace: CategoryDataPoint[]
  documentsOverTime: TimeSeriesDataPoint[]
  storageUsed: number
  averageDocumentSize: number
}

export interface WorkspaceAnalytics {
  workspaceId: string
  workspaceName: string
  totalQueries: number
  totalDocuments: number
  totalUsers: number
  averageResponseTime: number
  querySuccessRate: number
  topQueries: { query: string; count: number }[]
  usersOverTime: TimeSeriesDataPoint[]
  queriesOverTime: TimeSeriesDataPoint[]
}

export interface PerformanceAnalytics {
  averageLatency: number
  p50Latency: number
  p95Latency: number
  p99Latency: number
  errorRate: number
  successRate: number
  cacheHitRate: number
  throughput: number
  latencyOverTime: TimeSeriesDataPoint[]
  errorRateOverTime: TimeSeriesDataPoint[]
  throughputOverTime: TimeSeriesDataPoint[]
}

export interface FeedbackAnalytics {
  totalFeedback: number
  positiveCount: number
  negativeCount: number
  neutralCount: number
  positiveRate: number
  negativeRate: number
  feedbackOverTime: TimeSeriesDataPoint[]
  feedbackByRating: CategoryDataPoint[]
  topIssues: { issue: string; count: number }[]
  improvementSuggestions: string[]
}

// Dashboard Summary
export interface AnalyticsDashboard {
  period: DateRange
  summary: DashboardSummary
  queryAnalytics: QueryAnalytics
  sessionAnalytics: SessionAnalytics
  documentAnalytics: DocumentAnalytics
  performanceAnalytics: PerformanceAnalytics
  feedbackAnalytics: FeedbackAnalytics
}

export interface DashboardSummary {
  totalQueries: number
  queriesChange: number
  totalSessions: number
  sessionsChange: number
  totalDocuments: number
  documentsChange: number
  averageResponseTime: number
  responseTimeChange: number
  activeUsers: number
  activeUsersChange: number
  successRate: number
  successRateChange: number
}

export interface TrendIndicator {
  value: number
  change: number
  changePercent: number
  trend: 'up' | 'down' | 'stable'
  isPositive: boolean
}

// Insight Types
export interface Insight {
  id: string
  type: InsightType
  title: string
  description: string
  severity: InsightSeverity
  metric?: string
  value?: number
  threshold?: number
  recommendation?: string
  createdAt: string
}

export type InsightType =
  | 'performance'
  | 'usage'
  | 'quality'
  | 'security'
  | 'cost'
  | 'engagement'

export type InsightSeverity = 'info' | 'warning' | 'critical' | 'success'

// Export Types
export interface AnalyticsExport {
  format: 'csv' | 'json' | 'pdf' | 'excel'
  dateRange: DateRange
  metrics: string[]
  groupBy?: 'day' | 'week' | 'month'
}

// Filter Types
export interface AnalyticsFilter {
  dateRange: DateRange
  workspaceIds?: string[]
  userIds?: string[]
  sessionTypes?: string[]
  documentTypes?: string[]
}

// Utility functions
export function getDateRangeFromPreset(preset: DateRangePreset): DateRange {
  const now = new Date()
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate())

  switch (preset) {
    case 'today':
      return { start: today, end: now, preset }
    case 'yesterday': {
      const yesterday = new Date(today)
      yesterday.setDate(yesterday.getDate() - 1)
      return { start: yesterday, end: today, preset }
    }
    case 'last7days': {
      const start = new Date(today)
      start.setDate(start.getDate() - 7)
      return { start, end: now, preset }
    }
    case 'last30days': {
      const start = new Date(today)
      start.setDate(start.getDate() - 30)
      return { start, end: now, preset }
    }
    case 'last90days': {
      const start = new Date(today)
      start.setDate(start.getDate() - 90)
      return { start, end: now, preset }
    }
    case 'thisMonth': {
      const start = new Date(now.getFullYear(), now.getMonth(), 1)
      return { start, end: now, preset }
    }
    case 'lastMonth': {
      const start = new Date(now.getFullYear(), now.getMonth() - 1, 1)
      const end = new Date(now.getFullYear(), now.getMonth(), 0)
      return { start, end, preset }
    }
    case 'thisYear': {
      const start = new Date(now.getFullYear(), 0, 1)
      return { start, end: now, preset }
    }
    default:
      return { start: today, end: now, preset: 'today' }
  }
}

export function formatDateRange(range: DateRange): string {
  const options: Intl.DateTimeFormatOptions = { month: 'short', day: 'numeric' }
  const startStr = range.start.toLocaleDateString('en-US', options)
  const endStr = range.end.toLocaleDateString('en-US', options)

  if (startStr === endStr) return startStr
  return `${startStr} - ${endStr}`
}

export function calculateTrend(current: number, previous: number): TrendIndicator {
  const change = current - previous
  const changePercent = previous !== 0 ? (change / previous) * 100 : 0

  return {
    value: current,
    change,
    changePercent,
    trend: change > 0 ? 'up' : change < 0 ? 'down' : 'stable',
    isPositive: change >= 0
  }
}
