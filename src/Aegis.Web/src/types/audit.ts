// Audit action types (matching backend)
export type AuditAction =
  // Authentication
  | 'Login'
  | 'Logout'
  | 'LoginFailed'
  | 'PasswordChange'
  | 'PasswordReset'
  | 'TokenRefresh'
  | 'MfaEnabled'
  | 'MfaDisabled'
  // User Management
  | 'UserCreated'
  | 'UserUpdated'
  | 'UserDeleted'
  | 'UserEnabled'
  | 'UserDisabled'
  | 'RoleAssigned'
  | 'RoleRevoked'
  // Team Management
  | 'TeamCreated'
  | 'TeamUpdated'
  | 'TeamDeleted'
  | 'MemberAdded'
  | 'MemberRemoved'
  // Workspace Management
  | 'WorkspaceCreated'
  | 'WorkspaceUpdated'
  | 'WorkspaceDeleted'
  | 'WorkspaceAccessGranted'
  | 'WorkspaceAccessRevoked'
  // Data Source Management
  | 'DataSourceCreated'
  | 'DataSourceUpdated'
  | 'DataSourceDeleted'
  | 'DataSourceSynced'
  | 'DataSourceSyncFailed'
  // Document Management
  | 'DocumentUploaded'
  | 'DocumentDeleted'
  | 'DocumentProcessed'
  | 'DocumentProcessingFailed'
  // Query Operations
  | 'QueryExecuted'
  | 'QueryFailed'
  // API Operations
  | 'ApiKeyCreated'
  | 'ApiKeyRevoked'
  | 'ApiKeyRotated'
  | 'RateLimitExceeded'
  // Export Operations
  | 'DataExported'
  | 'ReportGenerated'
  // System Operations
  | 'ConfigurationChanged'
  | 'SystemStarted'
  | 'SystemStopped'
  | 'BackupCreated'
  | 'BackupRestored'
  // Security Events
  | 'SecurityAlert'
  | 'SuspiciousActivity'
  | 'AccessDenied'
  | 'InjectionAttempt'

// Audit categories
export type AuditCategory =
  | 'Authentication'
  | 'Authorization'
  | 'UserManagement'
  | 'TeamManagement'
  | 'WorkspaceManagement'
  | 'DataSourceManagement'
  | 'DocumentManagement'
  | 'QueryOperations'
  | 'ApiOperations'
  | 'ExportOperations'
  | 'SystemOperations'
  | 'SecurityEvents'

// Audit severity levels
export type AuditSeverity = 'Debug' | 'Info' | 'Warning' | 'Error' | 'Critical'

// Category definitions for UI grouping
export interface AuditCategoryDefinition {
  name: AuditCategory
  label: string
  actions: AuditAction[]
}

export const AUDIT_CATEGORIES: AuditCategoryDefinition[] = [
  {
    name: 'Authentication',
    label: 'Authentication',
    actions: ['Login', 'Logout', 'LoginFailed', 'PasswordChange', 'PasswordReset', 'TokenRefresh', 'MfaEnabled', 'MfaDisabled']
  },
  {
    name: 'UserManagement',
    label: 'User Management',
    actions: ['UserCreated', 'UserUpdated', 'UserDeleted', 'UserEnabled', 'UserDisabled', 'RoleAssigned', 'RoleRevoked']
  },
  {
    name: 'TeamManagement',
    label: 'Team Management',
    actions: ['TeamCreated', 'TeamUpdated', 'TeamDeleted', 'MemberAdded', 'MemberRemoved']
  },
  {
    name: 'WorkspaceManagement',
    label: 'Workspace Management',
    actions: ['WorkspaceCreated', 'WorkspaceUpdated', 'WorkspaceDeleted', 'WorkspaceAccessGranted', 'WorkspaceAccessRevoked']
  },
  {
    name: 'DataSourceManagement',
    label: 'Data Source Management',
    actions: ['DataSourceCreated', 'DataSourceUpdated', 'DataSourceDeleted', 'DataSourceSynced', 'DataSourceSyncFailed']
  },
  {
    name: 'DocumentManagement',
    label: 'Document Management',
    actions: ['DocumentUploaded', 'DocumentDeleted', 'DocumentProcessed', 'DocumentProcessingFailed']
  },
  {
    name: 'QueryOperations',
    label: 'Query Operations',
    actions: ['QueryExecuted', 'QueryFailed']
  },
  {
    name: 'ApiOperations',
    label: 'API Operations',
    actions: ['ApiKeyCreated', 'ApiKeyRevoked', 'ApiKeyRotated', 'RateLimitExceeded']
  },
  {
    name: 'ExportOperations',
    label: 'Export Operations',
    actions: ['DataExported', 'ReportGenerated']
  },
  {
    name: 'SystemOperations',
    label: 'System Operations',
    actions: ['ConfigurationChanged', 'SystemStarted', 'SystemStopped', 'BackupCreated', 'BackupRestored']
  },
  {
    name: 'SecurityEvents',
    label: 'Security Events',
    actions: ['SecurityAlert', 'SuspiciousActivity', 'AccessDenied', 'InjectionAttempt']
  }
]

// Detailed audit log entry (extended from admin.ts AuditLogEntry)
export interface DetailedAuditLogEntry {
  id: string
  action: AuditAction
  category: AuditCategory
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
  oldValues?: Record<string, unknown>
  newValues?: Record<string, unknown>
  metadata?: Record<string, string>
  severity: AuditSeverity
  correlationId?: string
  timestamp: string
}

// Audit log query filters
export interface AuditLogFilters {
  actions?: AuditAction[]
  categories?: AuditCategory[]
  userId?: string
  workspaceId?: string
  teamId?: string
  resourceType?: string
  success?: boolean
  minSeverity?: AuditSeverity
  fromDate?: string
  toDate?: string
  searchText?: string
  correlationId?: string
}

// Audit log query
export interface AuditLogQuery extends AuditLogFilters {
  page: number
  pageSize: number
  sortBy: string
  sortDescending: boolean
}

// Audit statistics
export interface AuditStatistics {
  totalEvents: number
  successfulEvents: number
  failedEvents: number
  uniqueUsers: number
  actionBreakdown: Record<AuditAction, number>
  categoryBreakdown: Record<AuditCategory, number>
  userBreakdown: Record<string, number>
  hourlyBreakdown: Record<string, number>
  dailyBreakdown: Record<string, number>
  severityBreakdown: Record<AuditSeverity, number>
  fromDate: string
  toDate: string
}

// Detailed system health status (extended from admin.ts SystemHealth)
export interface DetailedSystemHealth {
  status: 'healthy' | 'degraded' | 'unhealthy'
  uptime: number // seconds
  lastCheck: string
  components: DetailedSystemComponent[]
  metrics: DetailedSystemMetrics
}

export interface DetailedSystemComponent {
  name: string
  status: 'healthy' | 'degraded' | 'unhealthy' | 'unknown'
  message?: string
  lastCheck: string
  responseTime?: number
}

export interface DetailedSystemMetrics {
  cpu: number // percentage
  memory: number // percentage
  disk: number // percentage
  activeConnections: number
  requestsPerMinute: number
  averageResponseTime: number // ms
  errorRate: number // percentage
}

// Utility functions
export function getActionLabel(action: AuditAction): string {
  // Convert PascalCase to readable format
  return action.replace(/([A-Z])/g, ' $1').trim()
}

export function getCategoryLabel(category: AuditCategory): string {
  const def = AUDIT_CATEGORIES.find(c => c.name === category)
  return def?.label || category.replace(/([A-Z])/g, ' $1').trim()
}

export function getSeverityColor(severity: AuditSeverity): string {
  switch (severity) {
    case 'Debug':
      return 'gray'
    case 'Info':
      return 'blue'
    case 'Warning':
      return 'yellow'
    case 'Error':
      return 'red'
    case 'Critical':
      return 'purple'
    default:
      return 'gray'
  }
}

export function getActionIcon(action: AuditAction): string {
  // Map actions to icon names
  if (action.includes('Login') || action.includes('Logout')) return 'user-circle'
  if (action.includes('User')) return 'user'
  if (action.includes('Team') || action.includes('Member')) return 'users'
  if (action.includes('Workspace')) return 'folder'
  if (action.includes('DataSource')) return 'database'
  if (action.includes('Document')) return 'document'
  if (action.includes('Query')) return 'search'
  if (action.includes('ApiKey')) return 'key'
  if (action.includes('Export') || action.includes('Report')) return 'download'
  if (action.includes('System') || action.includes('Configuration')) return 'cog'
  if (action.includes('Security') || action.includes('Injection') || action.includes('Access')) return 'shield'
  return 'document-text'
}

export function getSuccessColor(success: boolean): string {
  return success ? 'green' : 'red'
}

export function formatTimestamp(timestamp: string): string {
  const date = new Date(timestamp)
  return date.toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit'
  })
}

export function formatAuditRelativeTime(timestamp: string): string {
  const now = new Date()
  const date = new Date(timestamp)
  const diffMs = now.getTime() - date.getTime()
  const diffSeconds = Math.floor(diffMs / 1000)
  const diffMinutes = Math.floor(diffSeconds / 60)
  const diffHours = Math.floor(diffMinutes / 60)
  const diffDays = Math.floor(diffHours / 24)

  if (diffSeconds < 60) return 'Just now'
  if (diffMinutes < 60) return `${diffMinutes}m ago`
  if (diffHours < 24) return `${diffHours}h ago`
  if (diffDays < 7) return `${diffDays}d ago`

  return date.toLocaleDateString()
}

export function formatUptime(seconds: number): string {
  const days = Math.floor(seconds / 86400)
  const hours = Math.floor((seconds % 86400) / 3600)
  const minutes = Math.floor((seconds % 3600) / 60)

  const parts: string[] = []
  if (days > 0) parts.push(`${days}d`)
  if (hours > 0) parts.push(`${hours}h`)
  if (minutes > 0) parts.push(`${minutes}m`)

  return parts.join(' ') || '< 1m'
}

// Severity levels for filtering
export const SEVERITY_LEVELS: { value: AuditSeverity; label: string }[] = [
  { value: 'Debug', label: 'Debug' },
  { value: 'Info', label: 'Info' },
  { value: 'Warning', label: 'Warning' },
  { value: 'Error', label: 'Error' },
  { value: 'Critical', label: 'Critical' }
]
