import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type {
  WebhookSubscription,
  WebhookDelivery,
  CreateWebhookRequest,
  UpdateWebhookRequest,
  WebhookFilters,
  WebhookStats,
  WebhookTestResult,
  WebhookEventType
} from '@/types'

export const useWebhookStore = defineStore('webhook', () => {
  // State
  const webhooks = ref<WebhookSubscription[]>([])
  const selectedWebhook = ref<WebhookSubscription | null>(null)
  const deliveryHistory = ref<WebhookDelivery[]>([])
  const isLoading = ref(false)
  const isLoadingDeliveries = ref(false)
  const error = ref<string | null>(null)
  const filters = ref<WebhookFilters>({})

  // Computed
  const filteredWebhooks = computed(() => {
    let result = [...webhooks.value]

    if (filters.value.search) {
      const search = filters.value.search.toLowerCase()
      result = result.filter(
        w =>
          w.name.toLowerCase().includes(search) ||
          w.url.toLowerCase().includes(search) ||
          w.description?.toLowerCase().includes(search)
      )
    }

    if (filters.value.isActive !== undefined) {
      result = result.filter(w => w.isActive === filters.value.isActive)
    }

    if (filters.value.events && filters.value.events.length > 0) {
      result = result.filter(w =>
        filters.value.events!.some(e => w.events.includes(e))
      )
    }

    return result
  })

  const stats = computed<WebhookStats>(() => {
    const active = webhooks.value.filter(w => w.isActive).length
    const inactive = webhooks.value.length - active

    let totalDeliveries = 0
    let successfulDeliveries = 0
    let failedDeliveries = 0

    webhooks.value.forEach(w => {
      totalDeliveries += w.health.successCount + w.health.failureCount
      successfulDeliveries += w.health.successCount
      failedDeliveries += w.health.failureCount
    })

    return {
      totalWebhooks: webhooks.value.length,
      activeWebhooks: active,
      inactiveWebhooks: inactive,
      totalDeliveries,
      successfulDeliveries,
      failedDeliveries,
      averageSuccessRate:
        totalDeliveries > 0 ? (successfulDeliveries / totalDeliveries) * 100 : 100
    }
  })

  // Actions
  async function fetchWebhooks(): Promise<void> {
    isLoading.value = true
    error.value = null

    try {
      // Mock data for development
      await new Promise(resolve => setTimeout(resolve, 500))

      webhooks.value = generateMockWebhooks()
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch webhooks'
      throw err
    } finally {
      isLoading.value = false
    }
  }

  async function fetchWebhook(id: string): Promise<WebhookSubscription | null> {
    isLoading.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 300))

      const webhook = webhooks.value.find(w => w.id === id)
      if (webhook) {
        selectedWebhook.value = webhook
        return webhook
      }

      // If not in cache, generate mock
      const mockWebhook = generateMockWebhook(id)
      selectedWebhook.value = mockWebhook
      return mockWebhook
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch webhook'
      throw err
    } finally {
      isLoading.value = false
    }
  }

  async function createWebhook(request: CreateWebhookRequest): Promise<WebhookSubscription> {
    isLoading.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 500))

      const newWebhook: WebhookSubscription = {
        id: crypto.randomUUID(),
        teamId: 'team-1',
        name: request.name,
        url: request.url,
        description: request.description,
        events: request.events,
        headers: request.headers || {},
        isActive: request.isActive ?? true,
        createdAt: new Date().toISOString(),
        health: {
          successCount: 0,
          failureCount: 0,
          successRate: 100
        }
      }

      webhooks.value.unshift(newWebhook)
      return newWebhook
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to create webhook'
      throw err
    } finally {
      isLoading.value = false
    }
  }

  async function updateWebhook(
    id: string,
    request: UpdateWebhookRequest
  ): Promise<WebhookSubscription> {
    isLoading.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 500))

      const index = webhooks.value.findIndex(w => w.id === id)
      if (index === -1) {
        throw new Error('Webhook not found')
      }

      const updated: WebhookSubscription = {
        ...webhooks.value[index],
        ...request,
        events: request.events || webhooks.value[index].events,
        headers: request.headers || webhooks.value[index].headers,
        updatedAt: new Date().toISOString()
      }

      webhooks.value[index] = updated

      if (selectedWebhook.value?.id === id) {
        selectedWebhook.value = updated
      }

      return updated
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to update webhook'
      throw err
    } finally {
      isLoading.value = false
    }
  }

  async function deleteWebhook(id: string): Promise<void> {
    isLoading.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 500))

      webhooks.value = webhooks.value.filter(w => w.id !== id)

      if (selectedWebhook.value?.id === id) {
        selectedWebhook.value = null
      }
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to delete webhook'
      throw err
    } finally {
      isLoading.value = false
    }
  }

  async function toggleWebhookActive(id: string): Promise<void> {
    const webhook = webhooks.value.find(w => w.id === id)
    if (webhook) {
      await updateWebhook(id, { isActive: !webhook.isActive })
    }
  }

  async function testWebhook(id: string): Promise<WebhookTestResult> {
    isLoading.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 1000))

      const webhook = webhooks.value.find(w => w.id === id)
      if (!webhook) {
        throw new Error('Webhook not found')
      }

      // Simulate test result (randomly succeed or fail for demo)
      const success = Math.random() > 0.3

      const delivery: WebhookDelivery = {
        id: crypto.randomUUID(),
        subscriptionId: id,
        eventId: crypto.randomUUID(),
        eventType: 'Test',
        url: webhook.url,
        status: success ? 'Success' : 'Failed',
        httpStatusCode: success ? 200 : 500,
        responseBody: success ? '{"status": "ok"}' : undefined,
        errorMessage: success ? undefined : 'Connection timeout',
        attemptNumber: 1,
        duration: Math.floor(Math.random() * 500) + 100,
        attemptedAt: new Date().toISOString()
      }

      // Update health
      const webhookIndex = webhooks.value.findIndex(w => w.id === id)
      if (webhookIndex !== -1) {
        const health = { ...webhooks.value[webhookIndex].health }
        if (success) {
          health.successCount++
          health.lastSuccessAt = new Date().toISOString()
        } else {
          health.failureCount++
          health.lastFailureAt = new Date().toISOString()
          health.lastError = delivery.errorMessage
        }
        health.successRate =
          (health.successCount / (health.successCount + health.failureCount)) * 100

        webhooks.value[webhookIndex] = {
          ...webhooks.value[webhookIndex],
          health
        }
      }

      return {
        delivery,
        success,
        message: success
          ? 'Test webhook delivered successfully'
          : 'Test webhook delivery failed'
      }
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to test webhook'
      throw err
    } finally {
      isLoading.value = false
    }
  }

  async function fetchDeliveryHistory(webhookId: string, limit = 50): Promise<void> {
    isLoadingDeliveries.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 500))

      deliveryHistory.value = generateMockDeliveries(webhookId, limit)
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch delivery history'
      throw err
    } finally {
      isLoadingDeliveries.value = false
    }
  }

  async function retryDelivery(deliveryId: string): Promise<WebhookDelivery> {
    isLoading.value = true
    error.value = null

    try {
      await new Promise(resolve => setTimeout(resolve, 1000))

      const index = deliveryHistory.value.findIndex(d => d.id === deliveryId)
      if (index === -1) {
        throw new Error('Delivery not found')
      }

      // Simulate retry (randomly succeed or fail)
      const success = Math.random() > 0.3
      const updated: WebhookDelivery = {
        ...deliveryHistory.value[index],
        status: success ? 'Success' : 'Failed',
        httpStatusCode: success ? 200 : 500,
        attemptNumber: deliveryHistory.value[index].attemptNumber + 1,
        attemptedAt: new Date().toISOString(),
        errorMessage: success ? undefined : 'Retry failed'
      }

      deliveryHistory.value[index] = updated
      return updated
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to retry delivery'
      throw err
    } finally {
      isLoading.value = false
    }
  }

  function setFilters(newFilters: WebhookFilters): void {
    filters.value = { ...filters.value, ...newFilters }
  }

  function clearFilters(): void {
    filters.value = {}
  }

  function clearError(): void {
    error.value = null
  }

  return {
    // State
    webhooks,
    selectedWebhook,
    deliveryHistory,
    isLoading,
    isLoadingDeliveries,
    error,
    filters,
    // Computed
    filteredWebhooks,
    stats,
    // Actions
    fetchWebhooks,
    fetchWebhook,
    createWebhook,
    updateWebhook,
    deleteWebhook,
    toggleWebhookActive,
    testWebhook,
    fetchDeliveryHistory,
    retryDelivery,
    setFilters,
    clearFilters,
    clearError
  }
})

// Mock data generators
function generateMockWebhooks(): WebhookSubscription[] {
  const webhooks: WebhookSubscription[] = [
    {
      id: 'wh-1',
      teamId: 'team-1',
      name: 'Slack Notifications',
      url: 'https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXX',
      description: 'Send document and query notifications to Slack',
      events: ['DocumentUploaded', 'DocumentProcessed', 'QueryCompleted'],
      headers: { 'Content-Type': 'application/json' },
      isActive: true,
      createdAt: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString(),
      health: {
        successCount: 245,
        failureCount: 5,
        lastSuccessAt: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
        lastFailureAt: new Date(Date.now() - 5 * 24 * 60 * 60 * 1000).toISOString(),
        lastError: 'Connection timeout',
        successRate: 98
      }
    },
    {
      id: 'wh-2',
      teamId: 'team-1',
      name: 'Analytics Pipeline',
      url: 'https://api.analytics.example.com/webhooks/aegis',
      description: 'Send query analytics data for processing',
      events: ['QueryCompleted', 'QueryFailed'],
      headers: { Authorization: 'Bearer xxx', 'Content-Type': 'application/json' },
      isActive: true,
      createdAt: new Date(Date.now() - 15 * 24 * 60 * 60 * 1000).toISOString(),
      health: {
        successCount: 1523,
        failureCount: 12,
        lastSuccessAt: new Date(Date.now() - 30 * 60 * 1000).toISOString(),
        lastFailureAt: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString(),
        successRate: 99.2
      }
    },
    {
      id: 'wh-3',
      teamId: 'team-1',
      name: 'Document Sync Service',
      url: 'https://internal.company.com/api/document-sync',
      description: 'Sync document changes to internal systems',
      events: [
        'DocumentUploaded',
        'DocumentProcessed',
        'DocumentProcessingFailed',
        'DocumentDeleted'
      ],
      headers: {},
      isActive: false,
      createdAt: new Date(Date.now() - 60 * 24 * 60 * 60 * 1000).toISOString(),
      updatedAt: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString(),
      health: {
        successCount: 89,
        failureCount: 45,
        lastSuccessAt: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString(),
        lastFailureAt: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString(),
        lastError: 'Service unavailable',
        successRate: 66.4
      }
    },
    {
      id: 'wh-4',
      teamId: 'team-1',
      name: 'Security Audit Log',
      url: 'https://siem.company.com/api/events',
      description: 'Send security-related events to SIEM',
      events: [
        'UserCreated',
        'UserDeleted',
        'ApiKeyCreated',
        'ApiKeyRevoked',
        'RateLimitExceeded'
      ],
      headers: { 'X-API-Key': 'xxx' },
      isActive: true,
      createdAt: new Date(Date.now() - 45 * 24 * 60 * 60 * 1000).toISOString(),
      health: {
        successCount: 67,
        failureCount: 0,
        lastSuccessAt: new Date(Date.now() - 1 * 24 * 60 * 60 * 1000).toISOString(),
        successRate: 100
      }
    },
    {
      id: 'wh-5',
      teamId: 'team-1',
      name: 'Data Source Monitor',
      url: 'https://monitoring.internal/webhooks/datasource',
      description: 'Monitor data source sync status',
      events: ['DataSourceSyncStarted', 'DataSourceSyncCompleted', 'DataSourceSyncFailed'],
      headers: {},
      isActive: true,
      createdAt: new Date(Date.now() - 10 * 24 * 60 * 60 * 1000).toISOString(),
      health: {
        successCount: 42,
        failureCount: 3,
        lastSuccessAt: new Date(Date.now() - 6 * 60 * 60 * 1000).toISOString(),
        successRate: 93.3
      }
    }
  ]

  return webhooks
}

function generateMockWebhook(id: string): WebhookSubscription {
  return {
    id,
    teamId: 'team-1',
    name: 'Webhook ' + id,
    url: 'https://example.com/webhook/' + id,
    description: 'Auto-generated webhook',
    events: ['DocumentUploaded', 'QueryCompleted'],
    headers: {},
    isActive: true,
    createdAt: new Date().toISOString(),
    health: {
      successCount: 0,
      failureCount: 0,
      successRate: 100
    }
  }
}

function generateMockDeliveries(webhookId: string, limit: number): WebhookDelivery[] {
  const deliveries: WebhookDelivery[] = []
  const eventTypes: WebhookEventType[] = [
    'DocumentUploaded',
    'DocumentProcessed',
    'QueryCompleted',
    'Test'
  ]
  const statuses: Array<{ status: WebhookDelivery['status']; weight: number }> = [
    { status: 'Success', weight: 85 },
    { status: 'Failed', weight: 10 },
    { status: 'Retrying', weight: 3 },
    { status: 'MaxRetriesExceeded', weight: 2 }
  ]

  for (let i = 0; i < limit; i++) {
    const rand = Math.random() * 100
    let statusCumulative = 0
    let status: WebhookDelivery['status'] = 'Success'

    for (const s of statuses) {
      statusCumulative += s.weight
      if (rand < statusCumulative) {
        status = s.status
        break
      }
    }

    const isSuccess = status === 'Success'
    const attemptedAt = new Date(
      Date.now() - i * Math.floor(Math.random() * 3600000)
    ).toISOString()

    deliveries.push({
      id: `del-${webhookId}-${i}`,
      subscriptionId: webhookId,
      eventId: crypto.randomUUID(),
      eventType: eventTypes[Math.floor(Math.random() * eventTypes.length)],
      url: 'https://example.com/webhook',
      status,
      httpStatusCode: isSuccess ? 200 : status === 'Failed' ? 500 : undefined,
      responseBody: isSuccess ? '{"status": "received"}' : undefined,
      errorMessage: !isSuccess ? 'Connection timeout' : undefined,
      attemptNumber: status === 'MaxRetriesExceeded' ? 5 : 1,
      duration: Math.floor(Math.random() * 800) + 50,
      attemptedAt,
      nextRetryAt:
        status === 'Retrying'
          ? new Date(Date.now() + 60000).toISOString()
          : undefined
    })
  }

  return deliveries
}
