// Admin Dashboard Types

export interface SystemOverview {
  systemHealth: SystemHealth
  activeUsers: number
  totalUsers: number
  totalTeams: number
  totalWorkspaces: number
  totalDocuments: number
  totalQueries: number
  uptime: string
  version: string
  environment: string
}

export interface SystemHealth {
  status: HealthStatus
  components: HealthComponent[]
  lastCheckedAt: string
}

export type HealthStatus = 'Healthy' | 'Degraded' | 'Unhealthy'

export interface HealthComponent {
  name: string
  status: HealthStatus
  description?: string
  duration?: number
  error?: string
}

export interface SystemMetrics {
  cpu: ResourceMetric
  memory: ResourceMetric
  disk: ResourceMetric
  network: NetworkMetrics
}

export interface ResourceMetric {
  used: number
  total: number
  percentage: number
  unit: string
}

export interface NetworkMetrics {
  requestsPerMinute: number
  averageLatencyMs: number
  errorRate: number
  activeConnections: number
}

export interface QueryMetrics {
  totalQueries: number
  queriesLast24h: number
  queriesLast7d: number
  averageResponseTime: number
  cacheHitRate: number
  errorRate: number
  byHour: TimeSeriesData[]
}

export interface TimeSeriesData {
  timestamp: string
  value: number
}

export interface DocumentMetrics {
  totalDocuments: number
  totalChunks: number
  documentsLast24h: number
  processingQueue: number
  failedCount: number
  byType: Record<string, number>
  byStatus: Record<string, number>
}

export interface UserMetrics {
  totalUsers: number
  activeUsersLast24h: number
  activeUsersLast7d: number
  newUsersLast7d: number
  byRole: Record<string, number>
}

export interface CacheMetrics {
  semanticCache: CacheStats
  embeddingCache: CacheStats
  responseCache: CacheStats
}

export interface CacheStats {
  hitCount: number
  missCount: number
  hitRate: number
  size: number
  maxSize: number
  evictions: number
}

// API Key Management

export interface ApiKey {
  id: string
  name: string
  prefix: string
  userId: string
  scopes: string[]
  status: ApiKeyStatus
  createdAt: string
  expiresAt?: string
  lastUsedAt?: string
  usageCount: number
}

export type ApiKeyStatus = 'Active' | 'Revoked' | 'Expired'

export interface CreateApiKeyRequest {
  name: string
  scopes: string[]
  expiresAt?: string
}

export interface CreateApiKeyResponse {
  id: string
  key: string
  prefix: string
  expiresAt?: string
}

// User Management

export interface UserDetails {
  id: string
  email: string
  name: string
  role: string
  teamId?: string
  teamName?: string
  isActive: boolean
  createdAt: string
  lastLoginAt?: string
  queryCount: number
  sessionCount: number
}

export interface UpdateUserRequest {
  name?: string
  role?: string
  isActive?: boolean
}

// Audit Log

export interface AuditLogEntry {
  id: string
  userId: string
  userName: string
  action: string
  resourceType: string
  resourceId?: string
  details?: Record<string, unknown>
  ipAddress?: string
  userAgent?: string
  timestamp: string
}

export interface AuditLogFilter {
  userId?: string
  action?: string
  resourceType?: string
  fromDate?: string
  toDate?: string
  pageNumber?: number
  pageSize?: number
}

// Settings

export interface UserSettings {
  theme: ThemeMode
  language: string
  timezone: string
  dateFormat: string
  notifications: NotificationSettings
  privacy: PrivacySettings
}

export type ThemeMode = 'light' | 'dark' | 'system'

export interface NotificationSettings {
  email: boolean
  inApp: boolean
  queryCompleted: boolean
  documentProcessed: boolean
  systemAlerts: boolean
  weeklyDigest: boolean
  quietHoursEnabled: boolean
  quietHoursStart?: string
  quietHoursEnd?: string
}

export interface PrivacySettings {
  shareUsageData: boolean
  showActivityStatus: boolean
  allowMentions: boolean
}
