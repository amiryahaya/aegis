// Webhook event types (matching backend)
export type WebhookEventType =
  // Document events
  | 'DocumentUploaded'
  | 'DocumentProcessed'
  | 'DocumentProcessingFailed'
  | 'DocumentDeleted'
  // Query events
  | 'QueryCompleted'
  | 'QueryFailed'
  // Data source events
  | 'DataSourceSyncStarted'
  | 'DataSourceSyncCompleted'
  | 'DataSourceSyncFailed'
  // System events
  | 'CacheCleared'
  | 'RateLimitExceeded'
  | 'CircuitBreakerOpened'
  | 'CircuitBreakerClosed'
  // User events
  | 'UserCreated'
  | 'UserDeleted'
  | 'ApiKeyCreated'
  | 'ApiKeyRevoked'
  // Test event
  | 'Test'

export interface WebhookEventCategory {
  name: string
  events: WebhookEventType[]
}

export const WEBHOOK_EVENT_CATEGORIES: WebhookEventCategory[] = [
  {
    name: 'Document',
    events: ['DocumentUploaded', 'DocumentProcessed', 'DocumentProcessingFailed', 'DocumentDeleted']
  },
  {
    name: 'Query',
    events: ['QueryCompleted', 'QueryFailed']
  },
  {
    name: 'Data Source',
    events: ['DataSourceSyncStarted', 'DataSourceSyncCompleted', 'DataSourceSyncFailed']
  },
  {
    name: 'System',
    events: ['CacheCleared', 'RateLimitExceeded', 'CircuitBreakerOpened', 'CircuitBreakerClosed']
  },
  {
    name: 'User',
    events: ['UserCreated', 'UserDeleted', 'ApiKeyCreated', 'ApiKeyRevoked']
  }
]

export function getEventLabel(event: WebhookEventType): string {
  // Convert PascalCase to readable format
  return event.replace(/([A-Z])/g, ' $1').trim()
}

export function getEventCategory(event: WebhookEventType): string {
  const category = WEBHOOK_EVENT_CATEGORIES.find(c => c.events.includes(event))
  return category?.name || 'Other'
}

// Webhook delivery status
export type WebhookDeliveryStatus =
  | 'Pending'
  | 'Success'
  | 'Failed'
  | 'Retrying'
  | 'MaxRetriesExceeded'
  | 'Skipped'

// Webhook health
export interface WebhookHealth {
  successCount: number
  failureCount: number
  lastSuccessAt?: string
  lastFailureAt?: string
  lastError?: string
  successRate: number
}

// Webhook subscription
export interface WebhookSubscription {
  id: string
  teamId: string
  name: string
  url: string
  description?: string
  events: WebhookEventType[]
  headers: Record<string, string>
  isActive: boolean
  createdAt: string
  updatedAt?: string
  health: WebhookHealth
}

// Webhook delivery record
export interface WebhookDelivery {
  id: string
  subscriptionId: string
  eventId: string
  eventType: WebhookEventType
  url: string
  status: WebhookDeliveryStatus
  httpStatusCode?: number
  responseBody?: string
  errorMessage?: string
  attemptNumber: number
  duration: number // milliseconds
  attemptedAt: string
  nextRetryAt?: string
}

// Create webhook request
export interface CreateWebhookRequest {
  name: string
  url: string
  description?: string
  events: WebhookEventType[]
  secret?: string
  headers?: Record<string, string>
  isActive?: boolean
}

// Update webhook request
export interface UpdateWebhookRequest {
  name?: string
  url?: string
  description?: string
  events?: WebhookEventType[]
  secret?: string
  headers?: Record<string, string>
  isActive?: boolean
}

// Webhook test result
export interface WebhookTestResult {
  delivery: WebhookDelivery
  success: boolean
  message: string
}

// Webhook list filters
export interface WebhookFilters {
  search?: string
  isActive?: boolean
  events?: WebhookEventType[]
}

// Webhook stats
export interface WebhookStats {
  totalWebhooks: number
  activeWebhooks: number
  inactiveWebhooks: number
  totalDeliveries: number
  successfulDeliveries: number
  failedDeliveries: number
  averageSuccessRate: number
}

// Status badge colors
export function getDeliveryStatusColor(status: WebhookDeliveryStatus): string {
  switch (status) {
    case 'Success':
      return 'green'
    case 'Pending':
    case 'Retrying':
      return 'yellow'
    case 'Failed':
    case 'MaxRetriesExceeded':
      return 'red'
    case 'Skipped':
      return 'gray'
    default:
      return 'gray'
  }
}

export function getHealthStatus(health: WebhookHealth): 'healthy' | 'degraded' | 'unhealthy' | 'unknown' {
  if (health.successCount === 0 && health.failureCount === 0) {
    return 'unknown'
  }
  if (health.successRate >= 95) {
    return 'healthy'
  }
  if (health.successRate >= 80) {
    return 'degraded'
  }
  return 'unhealthy'
}

export function getHealthColor(status: 'healthy' | 'degraded' | 'unhealthy' | 'unknown'): string {
  switch (status) {
    case 'healthy':
      return 'green'
    case 'degraded':
      return 'yellow'
    case 'unhealthy':
      return 'red'
    case 'unknown':
      return 'gray'
  }
}

// Format duration for display
export function formatDuration(ms: number): string {
  if (ms < 1000) {
    return `${ms}ms`
  }
  return `${(ms / 1000).toFixed(2)}s`
}
