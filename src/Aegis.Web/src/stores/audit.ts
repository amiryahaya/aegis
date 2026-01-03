import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type {
  DetailedAuditLogEntry,
  AuditLogFilters,
  AuditStatistics,
  DetailedSystemHealth,
  AuditAction,
  AuditCategory,
  AuditSeverity
} from '@/types'

export const useAuditStore = defineStore('audit', () => {
  // State
  const entries = ref<DetailedAuditLogEntry[]>([])
  const statistics = ref<AuditStatistics | null>(null)
  const systemHealth = ref<DetailedSystemHealth | null>(null)
  const selectedEntry = ref<DetailedAuditLogEntry | null>(null)
  const filters = ref<AuditLogFilters>({})
  const isLoading = ref(false)
  const isLoadingStats = ref(false)
  const isLoadingHealth = ref(false)
  const error = ref<string | null>(null)
  const page = ref(1)
  const pageSize = ref(50)
  const totalCount = ref(0)
  const sortBy = ref('timestamp')
  const sortDescending = ref(true)

  // Computed
  const totalPages = computed(() => Math.ceil(totalCount.value / pageSize.value))
  const hasNextPage = computed(() => page.value < totalPages.value)
  const hasPreviousPage = computed(() => page.value > 1)

  const filteredEntries = computed(() => {
    let result = [...entries.value]

    if (filters.value.searchText) {
      const search = filters.value.searchText.toLowerCase()
      result = result.filter(
        e =>
          e.description?.toLowerCase().includes(search) ||
          e.username?.toLowerCase().includes(search) ||
          e.resourceId?.toLowerCase().includes(search) ||
          e.action.toLowerCase().includes(search)
      )
    }

    if (filters.value.categories && filters.value.categories.length > 0) {
      result = result.filter(e => filters.value.categories!.includes(e.category))
    }

    if (filters.value.actions && filters.value.actions.length > 0) {
      result = result.filter(e => filters.value.actions!.includes(e.action))
    }

    if (filters.value.success !== undefined) {
      result = result.filter(e => e.success === filters.value.success)
    }

    if (filters.value.minSeverity) {
      const severityOrder: AuditSeverity[] = ['Debug', 'Info', 'Warning', 'Error', 'Critical']
      const minIndex = severityOrder.indexOf(filters.value.minSeverity)
      result = result.filter(e => severityOrder.indexOf(e.severity) >= minIndex)
    }

    return result
  })

  // Actions
  async function fetchEntries(): Promise<void> {
    isLoading.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 500))
      entries.value = generateMockEntries(100)
      totalCount.value = entries.value.length
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch audit logs'
      throw err
    } finally {
      isLoading.value = false
    }
  }

  async function fetchStatistics(): Promise<void> {
    isLoadingStats.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 300))
      statistics.value = generateMockStatistics()
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch statistics'
      throw err
    } finally {
      isLoadingStats.value = false
    }
  }

  async function fetchSystemHealth(): Promise<void> {
    isLoadingHealth.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 300))
      systemHealth.value = generateMockSystemHealth()
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch system health'
      throw err
    } finally {
      isLoadingHealth.value = false
    }
  }

  async function exportLogs(format: 'json' | 'csv'): Promise<Blob> {
    const data = filteredEntries.value

    if (format === 'json') {
      return new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' })
    }

    // CSV export
    const headers = [
      'Timestamp',
      'Action',
      'Category',
      'Username',
      'Resource Type',
      'Resource ID',
      'Success',
      'Severity',
      'Description',
      'IP Address'
    ]

    const rows = data.map(e => [
      e.timestamp,
      e.action,
      e.category,
      e.username || '',
      e.resourceType || '',
      e.resourceId || '',
      e.success ? 'Yes' : 'No',
      e.severity,
      e.description || '',
      e.ipAddress || ''
    ])

    const csv = [headers.join(','), ...rows.map(r => r.map(c => `"${c}"`).join(','))].join('\n')

    return new Blob([csv], { type: 'text/csv' })
  }

  function setFilters(newFilters: AuditLogFilters): void {
    filters.value = { ...filters.value, ...newFilters }
  }

  function clearFilters(): void {
    filters.value = {}
  }

  function setPage(newPage: number): void {
    page.value = newPage
  }

  function setPageSize(newPageSize: number): void {
    pageSize.value = newPageSize
    page.value = 1
  }

  function setSorting(field: string, descending: boolean): void {
    sortBy.value = field
    sortDescending.value = descending
  }

  function selectEntry(entry: DetailedAuditLogEntry | null): void {
    selectedEntry.value = entry
  }

  function clearError(): void {
    error.value = null
  }

  return {
    // State
    entries,
    statistics,
    systemHealth,
    selectedEntry,
    filters,
    isLoading,
    isLoadingStats,
    isLoadingHealth,
    error,
    page,
    pageSize,
    totalCount,
    sortBy,
    sortDescending,
    // Computed
    totalPages,
    hasNextPage,
    hasPreviousPage,
    filteredEntries,
    // Actions
    fetchEntries,
    fetchStatistics,
    fetchSystemHealth,
    exportLogs,
    setFilters,
    clearFilters,
    setPage,
    setPageSize,
    setSorting,
    selectEntry,
    clearError
  }
})

// Mock data generators
function generateMockEntries(count: number): DetailedAuditLogEntry[] {
  const actions: AuditAction[] = [
    'Login',
    'Logout',
    'LoginFailed',
    'UserCreated',
    'UserUpdated',
    'WorkspaceCreated',
    'DocumentUploaded',
    'DocumentProcessed',
    'QueryExecuted',
    'QueryFailed',
    'ApiKeyCreated',
    'DataExported',
    'ConfigurationChanged',
    'SecurityAlert',
    'AccessDenied'
  ]

  const severities: AuditSeverity[] = ['Debug', 'Info', 'Warning', 'Error', 'Critical']
  const usernames = ['admin', 'john.doe', 'jane.smith', 'system', 'api-user', 'guest']
  const resourceTypes = ['User', 'Workspace', 'Document', 'Session', 'DataSource', 'ApiKey']

  const entries: DetailedAuditLogEntry[] = []

  for (let i = 0; i < count; i++) {
    const action = actions[Math.floor(Math.random() * actions.length)]
    const success = Math.random() > 0.15
    const severityWeights = success ? [0.1, 0.6, 0.2, 0.08, 0.02] : [0, 0.1, 0.3, 0.4, 0.2]
    const severityRand = Math.random()
    let severityIndex = 0
    let cumulative = 0
    for (let j = 0; j < severityWeights.length; j++) {
      cumulative += severityWeights[j]
      if (severityRand < cumulative) {
        severityIndex = j
        break
      }
    }

    const timestamp = new Date(
      Date.now() - Math.floor(Math.random() * 7 * 24 * 60 * 60 * 1000)
    ).toISOString()

    entries.push({
      id: crypto.randomUUID(),
      action,
      category: getCategoryForAction(action),
      userId: crypto.randomUUID(),
      username: usernames[Math.floor(Math.random() * usernames.length)],
      workspaceId: Math.random() > 0.3 ? crypto.randomUUID() : undefined,
      resourceType: resourceTypes[Math.floor(Math.random() * resourceTypes.length)],
      resourceId: Math.random() > 0.2 ? crypto.randomUUID().slice(0, 8) : undefined,
      description: getDescriptionForAction(action, success),
      ipAddress: `192.168.${Math.floor(Math.random() * 255)}.${Math.floor(Math.random() * 255)}`,
      userAgent: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)',
      success,
      errorMessage: success ? undefined : 'Operation failed',
      severity: severities[severityIndex],
      correlationId: crypto.randomUUID(),
      timestamp
    })
  }

  // Sort by timestamp descending
  return entries.sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime())
}

function getCategoryForAction(action: AuditAction): AuditCategory {
  if (['Login', 'Logout', 'LoginFailed', 'PasswordChange', 'TokenRefresh'].includes(action)) {
    return 'Authentication'
  }
  if (action.includes('User') || action.includes('Role')) return 'UserManagement'
  if (action.includes('Team') || action.includes('Member')) return 'TeamManagement'
  if (action.includes('Workspace')) return 'WorkspaceManagement'
  if (action.includes('DataSource')) return 'DataSourceManagement'
  if (action.includes('Document')) return 'DocumentManagement'
  if (action.includes('Query')) return 'QueryOperations'
  if (action.includes('ApiKey') || action.includes('RateLimit')) return 'ApiOperations'
  if (action.includes('Export') || action.includes('Report')) return 'ExportOperations'
  if (action.includes('System') || action.includes('Configuration') || action.includes('Backup')) {
    return 'SystemOperations'
  }
  return 'SecurityEvents'
}

function getDescriptionForAction(action: AuditAction, success: boolean): string {
  const descriptions: Record<string, { success: string; failure: string }> = {
    Login: { success: 'User logged in successfully', failure: 'Login attempt failed' },
    Logout: { success: 'User logged out', failure: 'Logout failed' },
    LoginFailed: {
      success: 'Login attempt recorded',
      failure: 'Invalid credentials or account locked'
    },
    UserCreated: { success: 'New user account created', failure: 'Failed to create user' },
    UserUpdated: { success: 'User profile updated', failure: 'Failed to update user' },
    WorkspaceCreated: { success: 'New workspace created', failure: 'Failed to create workspace' },
    DocumentUploaded: { success: 'Document uploaded successfully', failure: 'Document upload failed' },
    DocumentProcessed: {
      success: 'Document processing completed',
      failure: 'Document processing failed'
    },
    QueryExecuted: { success: 'Query executed successfully', failure: 'Query execution failed' },
    QueryFailed: { success: 'Query failure recorded', failure: 'Query execution error' },
    ApiKeyCreated: { success: 'API key generated', failure: 'Failed to generate API key' },
    DataExported: { success: 'Data export completed', failure: 'Data export failed' },
    ConfigurationChanged: {
      success: 'System configuration updated',
      failure: 'Configuration update failed'
    },
    SecurityAlert: { success: 'Security event logged', failure: 'Security incident detected' },
    AccessDenied: { success: 'Access attempt logged', failure: 'Access denied - insufficient permissions' }
  }

  const desc = descriptions[action] || { success: 'Action completed', failure: 'Action failed' }
  return success ? desc.success : desc.failure
}

function generateMockStatistics(): AuditStatistics {
  const now = new Date()
  const weekAgo = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000)

  const dailyBreakdown: Record<string, number> = {}
  for (let i = 0; i < 7; i++) {
    const date = new Date(now.getTime() - i * 24 * 60 * 60 * 1000)
    dailyBreakdown[date.toISOString().split('T')[0]] = Math.floor(Math.random() * 500) + 100
  }

  return {
    totalEvents: 3456,
    successfulEvents: 3102,
    failedEvents: 354,
    uniqueUsers: 47,
    actionBreakdown: {
      Login: 1200,
      Logout: 800,
      QueryExecuted: 1500,
      DocumentUploaded: 234,
      UserCreated: 45,
      ConfigurationChanged: 12
    } as Record<AuditAction, number>,
    categoryBreakdown: {
      Authentication: 2000,
      QueryOperations: 1500,
      DocumentManagement: 400,
      UserManagement: 156,
      SystemOperations: 100
    } as Record<AuditCategory, number>,
    userBreakdown: {
      admin: 456,
      'john.doe': 234,
      'jane.smith': 189,
      system: 567,
      'api-user': 890
    },
    hourlyBreakdown: {},
    dailyBreakdown,
    severityBreakdown: {
      Debug: 123,
      Info: 2500,
      Warning: 456,
      Error: 234,
      Critical: 43
    } as Record<AuditSeverity, number>,
    fromDate: weekAgo.toISOString(),
    toDate: now.toISOString()
  }
}

function generateMockSystemHealth(): DetailedSystemHealth {
  const statuses: Array<'healthy' | 'degraded' | 'unhealthy'> = ['healthy', 'degraded', 'unhealthy']
  const weights = [0.85, 0.12, 0.03]
  const rand = Math.random()
  let status: 'healthy' | 'degraded' | 'unhealthy' = 'healthy'
  let cumulative = 0
  for (let i = 0; i < weights.length; i++) {
    cumulative += weights[i]
    if (rand < cumulative) {
      status = statuses[i]
      break
    }
  }

  return {
    status,
    uptime: Math.floor(Math.random() * 30 * 24 * 60 * 60) + 86400, // 1-30 days
    lastCheck: new Date().toISOString(),
    components: [
      {
        name: 'API Server',
        status: 'healthy',
        message: 'All endpoints responding',
        lastCheck: new Date().toISOString(),
        responseTime: Math.floor(Math.random() * 50) + 10
      },
      {
        name: 'Database',
        status: status === 'unhealthy' ? 'degraded' : 'healthy',
        message: status === 'unhealthy' ? 'High connection latency' : 'Connection pool healthy',
        lastCheck: new Date().toISOString(),
        responseTime: Math.floor(Math.random() * 100) + 20
      },
      {
        name: 'Vector Store',
        status: 'healthy',
        message: 'Index up to date',
        lastCheck: new Date().toISOString(),
        responseTime: Math.floor(Math.random() * 80) + 15
      },
      {
        name: 'Cache',
        status: 'healthy',
        message: 'Hit rate 94%',
        lastCheck: new Date().toISOString(),
        responseTime: Math.floor(Math.random() * 10) + 1
      },
      {
        name: 'Background Jobs',
        status: status === 'degraded' ? 'degraded' : 'healthy',
        message: status === 'degraded' ? '3 jobs pending' : 'Queue empty',
        lastCheck: new Date().toISOString()
      },
      {
        name: 'LLM Provider',
        status: 'healthy',
        message: 'API available',
        lastCheck: new Date().toISOString(),
        responseTime: Math.floor(Math.random() * 500) + 100
      }
    ],
    metrics: {
      cpu: Math.floor(Math.random() * 60) + 10,
      memory: Math.floor(Math.random() * 40) + 30,
      disk: Math.floor(Math.random() * 30) + 20,
      activeConnections: Math.floor(Math.random() * 100) + 20,
      requestsPerMinute: Math.floor(Math.random() * 500) + 100,
      averageResponseTime: Math.floor(Math.random() * 200) + 50,
      errorRate: Math.random() * 2
    }
  }
}
