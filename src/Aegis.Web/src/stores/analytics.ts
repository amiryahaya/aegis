import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type {
  DateRange,
  DateRangePreset,
  AnalyticsDashboard,
  DashboardSummary,
  QueryAnalytics,
  SessionAnalytics,
  DocumentAnalytics,
  PerformanceAnalytics,
  FeedbackAnalytics,
  UserUsageStats,
  WorkspaceAnalytics,
  Insight,
  AnalyticsFilter,
  TimeSeriesDataPoint
} from '@/types/analytics'
import { getDateRangeFromPreset } from '@/types/analytics'

export const useAnalyticsStore = defineStore('analytics', () => {
  // State
  const isLoading = ref(false)
  const error = ref<string | null>(null)
  const dateRange = ref<DateRange>(getDateRangeFromPreset('last30days'))
  const filter = ref<AnalyticsFilter>({ dateRange: dateRange.value })

  // Dashboard data
  const dashboard = ref<AnalyticsDashboard | null>(null)
  const userStats = ref<UserUsageStats | null>(null)
  const workspaceAnalytics = ref<WorkspaceAnalytics[]>([])
  const insights = ref<Insight[]>([])

  // Computed
  const summary = computed(() => dashboard.value?.summary ?? null)
  const queryAnalytics = computed(() => dashboard.value?.queryAnalytics ?? null)
  const sessionAnalytics = computed(() => dashboard.value?.sessionAnalytics ?? null)
  const documentAnalytics = computed(() => dashboard.value?.documentAnalytics ?? null)
  const performanceAnalytics = computed(() => dashboard.value?.performanceAnalytics ?? null)
  const feedbackAnalytics = computed(() => dashboard.value?.feedbackAnalytics ?? null)

  // Actions
  async function fetchDashboard() {
    isLoading.value = true
    error.value = null

    try {
      // In production, this would fetch from the API
      // For now, generate mock data
      dashboard.value = generateMockDashboard(dateRange.value)
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch analytics'
    } finally {
      isLoading.value = false
    }
  }

  async function fetchUserStats(userId: string) {
    isLoading.value = true
    error.value = null

    try {
      userStats.value = generateMockUserStats(userId, dateRange.value)
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch user stats'
    } finally {
      isLoading.value = false
    }
  }

  async function fetchWorkspaceAnalytics(workspaceId?: string) {
    isLoading.value = true
    error.value = null

    try {
      if (workspaceId) {
        const analytics = generateMockWorkspaceAnalytics(workspaceId)
        workspaceAnalytics.value = [analytics]
      } else {
        workspaceAnalytics.value = [
          generateMockWorkspaceAnalytics('ws-1', 'Marketing Knowledge Base'),
          generateMockWorkspaceAnalytics('ws-2', 'Engineering Docs'),
          generateMockWorkspaceAnalytics('ws-3', 'HR Policies')
        ]
      }
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch workspace analytics'
    } finally {
      isLoading.value = false
    }
  }

  async function fetchInsights() {
    isLoading.value = true
    error.value = null

    try {
      insights.value = generateMockInsights()
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch insights'
    } finally {
      isLoading.value = false
    }
  }

  function setDateRange(preset: DateRangePreset) {
    dateRange.value = getDateRangeFromPreset(preset)
    filter.value.dateRange = dateRange.value
    fetchDashboard()
  }

  function setCustomDateRange(start: Date, end: Date) {
    dateRange.value = { start, end, preset: 'custom' }
    filter.value.dateRange = dateRange.value
    fetchDashboard()
  }

  function updateFilter(newFilter: Partial<AnalyticsFilter>) {
    filter.value = { ...filter.value, ...newFilter }
    fetchDashboard()
  }

  // Mock data generators
  function generateMockDashboard(period: DateRange): AnalyticsDashboard {
    return {
      period,
      summary: generateMockSummary(),
      queryAnalytics: generateMockQueryAnalytics(period),
      sessionAnalytics: generateMockSessionAnalytics(period),
      documentAnalytics: generateMockDocumentAnalytics(period),
      performanceAnalytics: generateMockPerformanceAnalytics(period),
      feedbackAnalytics: generateMockFeedbackAnalytics(period)
    }
  }

  function generateMockSummary(): DashboardSummary {
    return {
      totalQueries: 12847,
      queriesChange: 15.3,
      totalSessions: 3421,
      sessionsChange: 8.7,
      totalDocuments: 856,
      documentsChange: 12.1,
      averageResponseTime: 1.24,
      responseTimeChange: -5.2,
      activeUsers: 247,
      activeUsersChange: 18.9,
      successRate: 98.7,
      successRateChange: 0.3
    }
  }

  function generateMockQueryAnalytics(period: DateRange): QueryAnalytics {
    const days = Math.ceil((period.end.getTime() - period.start.getTime()) / (1000 * 60 * 60 * 24))

    return {
      totalQueries: 12847,
      successfulQueries: 12680,
      failedQueries: 167,
      averageResponseTime: 1.24,
      medianResponseTime: 0.98,
      p95ResponseTime: 2.87,
      queriesByType: [
        { category: 'Quick Query', value: 6423, percentage: 50 },
        { category: 'Research', value: 3212, percentage: 25 },
        { category: 'Analysis', value: 1927, percentage: 15 },
        { category: 'Document', value: 1285, percentage: 10 }
      ],
      queriesByWorkspace: [
        { category: 'Marketing', value: 4500, percentage: 35 },
        { category: 'Engineering', value: 3800, percentage: 30 },
        { category: 'HR', value: 2500, percentage: 19 },
        { category: 'Sales', value: 2047, percentage: 16 }
      ],
      queriesOverTime: generateTimeSeriesData(days, 300, 500),
      topKeywords: [
        { keyword: 'pricing', count: 423 },
        { keyword: 'documentation', count: 387 },
        { keyword: 'api', count: 312 },
        { keyword: 'integration', count: 289 },
        { keyword: 'support', count: 245 }
      ],
      queryComplexityDistribution: [
        { category: 'Simple', value: 5500, percentage: 43 },
        { category: 'Medium', value: 5000, percentage: 39 },
        { category: 'Complex', value: 2347, percentage: 18 }
      ]
    }
  }

  function generateMockSessionAnalytics(period: DateRange): SessionAnalytics {
    const days = Math.ceil((period.end.getTime() - period.start.getTime()) / (1000 * 60 * 60 * 24))

    return {
      totalSessions: 3421,
      activeSessions: 127,
      completedSessions: 3294,
      averageDuration: 12.5,
      averageTurnsPerSession: 4.7,
      sessionsByType: [
        { category: 'Quick Query', value: 1711, percentage: 50 },
        { category: 'Research', value: 855, percentage: 25 },
        { category: 'Analysis', value: 513, percentage: 15 },
        { category: 'Document', value: 342, percentage: 10 }
      ],
      sessionsOverTime: generateTimeSeriesData(days, 80, 150),
      sessionCompletionRate: 96.3,
      abandonmentRate: 3.7,
      returningUserRate: 72.4
    }
  }

  function generateMockDocumentAnalytics(period: DateRange): DocumentAnalytics {
    const days = Math.ceil((period.end.getTime() - period.start.getTime()) / (1000 * 60 * 60 * 24))

    return {
      totalDocuments: 856,
      documentsIndexed: 823,
      documentsPending: 18,
      documentsFailed: 15,
      totalChunks: 45230,
      averageChunksPerDocument: 52.8,
      documentsByType: [
        { category: 'PDF', value: 342, percentage: 40 },
        { category: 'Word', value: 214, percentage: 25 },
        { category: 'Markdown', value: 171, percentage: 20 },
        { category: 'Other', value: 129, percentage: 15 }
      ],
      documentsByWorkspace: [
        { category: 'Marketing', value: 285, percentage: 33 },
        { category: 'Engineering', value: 257, percentage: 30 },
        { category: 'HR', value: 171, percentage: 20 },
        { category: 'Sales', value: 143, percentage: 17 }
      ],
      documentsOverTime: generateTimeSeriesData(days, 5, 20),
      storageUsed: 2.4 * 1024 * 1024 * 1024, // 2.4 GB
      averageDocumentSize: 2.8 * 1024 * 1024 // 2.8 MB
    }
  }

  function generateMockPerformanceAnalytics(period: DateRange): PerformanceAnalytics {
    const days = Math.ceil((period.end.getTime() - period.start.getTime()) / (1000 * 60 * 60 * 24))

    return {
      averageLatency: 1.24,
      p50Latency: 0.98,
      p95Latency: 2.87,
      p99Latency: 4.12,
      errorRate: 1.3,
      successRate: 98.7,
      cacheHitRate: 67.4,
      throughput: 428,
      latencyOverTime: generateTimeSeriesData(days, 0.8, 1.8),
      errorRateOverTime: generateTimeSeriesData(days, 0.5, 2.5),
      throughputOverTime: generateTimeSeriesData(days, 350, 550)
    }
  }

  function generateMockFeedbackAnalytics(period: DateRange): FeedbackAnalytics {
    const days = Math.ceil((period.end.getTime() - period.start.getTime()) / (1000 * 60 * 60 * 24))

    return {
      totalFeedback: 1247,
      positiveCount: 987,
      negativeCount: 156,
      neutralCount: 104,
      positiveRate: 79.2,
      negativeRate: 12.5,
      feedbackOverTime: generateTimeSeriesData(days, 20, 60),
      feedbackByRating: [
        { category: 'Positive', value: 987, percentage: 79.2, color: '#22c55e' },
        { category: 'Neutral', value: 104, percentage: 8.3, color: '#eab308' },
        { category: 'Negative', value: 156, percentage: 12.5, color: '#ef4444' }
      ],
      topIssues: [
        { issue: 'Incomplete answers', count: 45 },
        { issue: 'Slow response time', count: 38 },
        { issue: 'Irrelevant sources', count: 32 },
        { issue: 'Missing context', count: 28 }
      ],
      improvementSuggestions: [
        'Add more technical documentation',
        'Improve search relevance',
        'Faster response times',
        'Better source citations'
      ]
    }
  }

  function generateMockUserStats(userId: string, period: DateRange): UserUsageStats {
    return {
      userId,
      period,
      totalQueries: 347,
      totalSessions: 89,
      totalDocumentsViewed: 156,
      averageSessionDuration: 14.2,
      averageQueriesPerSession: 3.9,
      averageResponseTime: 1.18,
      peakUsageHour: 14,
      mostActiveDay: 'Tuesday',
      tokensUsed: 45230,
      cacheHitRate: 72.3
    }
  }

  function generateMockWorkspaceAnalytics(workspaceId: string, name?: string): WorkspaceAnalytics {
    return {
      workspaceId,
      workspaceName: name || `Workspace ${workspaceId}`,
      totalQueries: Math.floor(Math.random() * 5000) + 1000,
      totalDocuments: Math.floor(Math.random() * 300) + 50,
      totalUsers: Math.floor(Math.random() * 50) + 10,
      averageResponseTime: Math.random() * 2 + 0.5,
      querySuccessRate: 95 + Math.random() * 5,
      topQueries: [
        { query: 'pricing information', count: 89 },
        { query: 'api documentation', count: 76 },
        { query: 'integration guide', count: 65 }
      ],
      usersOverTime: generateTimeSeriesData(30, 5, 20),
      queriesOverTime: generateTimeSeriesData(30, 50, 200)
    }
  }

  function generateMockInsights(): Insight[] {
    return [
      {
        id: '1',
        type: 'performance',
        title: 'Response Time Improved',
        description: 'Average response time decreased by 15% compared to last week.',
        severity: 'success',
        metric: 'responseTime',
        value: 1.24,
        recommendation: 'Continue current caching strategy.',
        createdAt: new Date().toISOString()
      },
      {
        id: '2',
        type: 'usage',
        title: 'Peak Usage Hours',
        description: 'Highest query volume occurs between 2-4 PM.',
        severity: 'info',
        metric: 'queries',
        value: 428,
        recommendation: 'Consider scaling resources during peak hours.',
        createdAt: new Date().toISOString()
      },
      {
        id: '3',
        type: 'quality',
        title: 'Negative Feedback Increase',
        description: 'Negative feedback increased by 8% this week.',
        severity: 'warning',
        metric: 'negativeFeedback',
        value: 156,
        threshold: 100,
        recommendation: 'Review recent query failures and improve documentation.',
        createdAt: new Date().toISOString()
      }
    ]
  }

  function generateTimeSeriesData(days: number, min: number, max: number): TimeSeriesDataPoint[] {
    const data: TimeSeriesDataPoint[] = []
    const now = new Date()

    for (let i = days - 1; i >= 0; i--) {
      const date = new Date(now)
      date.setDate(date.getDate() - i)
      data.push({
        date: date.toISOString().split('T')[0],
        value: Math.floor(Math.random() * (max - min) + min)
      })
    }

    return data
  }

  return {
    // State
    isLoading,
    error,
    dateRange,
    filter,
    dashboard,
    userStats,
    workspaceAnalytics,
    insights,

    // Computed
    summary,
    queryAnalytics,
    sessionAnalytics,
    documentAnalytics,
    performanceAnalytics,
    feedbackAnalytics,

    // Actions
    fetchDashboard,
    fetchUserStats,
    fetchWorkspaceAnalytics,
    fetchInsights,
    setDateRange,
    setCustomDateRange,
    updateFilter
  }
})
