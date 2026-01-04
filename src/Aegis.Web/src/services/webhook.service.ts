import api from './api'
import type {
  WebhookSubscription,
  WebhookDelivery,
  CreateWebhookRequest,
  UpdateWebhookRequest,
  WebhookTestResult,
  WebhookEventType
} from '@/types'

// =============================================================================
// Response Types (matching backend DTOs)
// =============================================================================

export interface WebhookHealthResponse {
  successCount: number
  failureCount: number
  successRate: number
  lastSuccessAt?: string
  lastFailureAt?: string
  lastError?: string
}

export interface WebhookSubscriptionResponse {
  id: string
  teamId: string
  name: string
  url: string
  description?: string
  events: string[]
  isActive: boolean
  createdAt: string
  updatedAt?: string
  health: WebhookHealthResponse
}

export interface WebhookDeliveryResponse {
  id: string
  subscriptionId: string
  eventId: string
  eventType: string
  status: string
  httpStatusCode?: number
  errorMessage?: string
  attemptNumber: number
  durationMs: number
  attemptedAt: string
  nextRetryAt?: string
}

export interface EventTypeInfo {
  name: string
  description: string
  category: string
}

export interface RegisterWebhookRequest {
  teamId: string
  name: string
  url: string
  description?: string
  events: WebhookEventType[]
  secret?: string
  headers?: Record<string, string>
  isActive?: boolean
}

export interface UpdateWebhookApiRequest {
  name?: string
  url?: string
  description?: string
  events?: WebhookEventType[]
  secret?: string
  headers?: Record<string, string>
  isActive?: boolean
}

// =============================================================================
// Type Mappers
// =============================================================================

function mapToWebhookSubscription(response: WebhookSubscriptionResponse): WebhookSubscription {
  return {
    id: response.id,
    teamId: response.teamId,
    name: response.name,
    url: response.url,
    description: response.description,
    events: response.events as WebhookEventType[],
    headers: {},
    isActive: response.isActive,
    createdAt: response.createdAt,
    updatedAt: response.updatedAt,
    health: {
      successCount: response.health.successCount,
      failureCount: response.health.failureCount,
      successRate: response.health.successRate,
      lastSuccessAt: response.health.lastSuccessAt,
      lastFailureAt: response.health.lastFailureAt,
      lastError: response.health.lastError
    }
  }
}

function mapToWebhookDelivery(response: WebhookDeliveryResponse): WebhookDelivery {
  return {
    id: response.id,
    subscriptionId: response.subscriptionId,
    eventId: response.eventId,
    eventType: response.eventType as WebhookEventType,
    url: '',
    status: response.status as WebhookDelivery['status'],
    httpStatusCode: response.httpStatusCode,
    errorMessage: response.errorMessage,
    attemptNumber: response.attemptNumber,
    duration: response.durationMs,
    attemptedAt: response.attemptedAt,
    nextRetryAt: response.nextRetryAt
  }
}

// =============================================================================
// Webhook Service
// =============================================================================

class WebhookService {
  private baseUrl = '/webhooks'

  /**
   * List all webhooks for a team
   */
  async list(teamId: string): Promise<WebhookSubscription[]> {
    const response = await api.get<WebhookSubscriptionResponse[]>(
      `${this.baseUrl}?teamId=${teamId}`
    )
    return response.map(mapToWebhookSubscription)
  }

  /**
   * Get a single webhook by ID
   */
  async get(id: string): Promise<WebhookSubscription> {
    const response = await api.get<WebhookSubscriptionResponse>(`${this.baseUrl}/${id}`)
    return mapToWebhookSubscription(response)
  }

  /**
   * Register a new webhook
   */
  async create(request: CreateWebhookRequest & { teamId: string }): Promise<WebhookSubscription> {
    const apiRequest: RegisterWebhookRequest = {
      teamId: request.teamId,
      name: request.name,
      url: request.url,
      description: request.description,
      events: request.events,
      secret: request.secret,
      headers: request.headers,
      isActive: request.isActive ?? true
    }
    const response = await api.post<WebhookSubscriptionResponse>(this.baseUrl, apiRequest)
    return mapToWebhookSubscription(response)
  }

  /**
   * Update an existing webhook
   */
  async update(id: string, request: UpdateWebhookRequest): Promise<WebhookSubscription> {
    const apiRequest: UpdateWebhookApiRequest = {
      name: request.name,
      url: request.url,
      description: request.description,
      events: request.events,
      secret: request.secret,
      headers: request.headers,
      isActive: request.isActive
    }
    const response = await api.put<WebhookSubscriptionResponse>(
      `${this.baseUrl}/${id}`,
      apiRequest
    )
    return mapToWebhookSubscription(response)
  }

  /**
   * Delete a webhook
   */
  async delete(id: string): Promise<void> {
    await api.delete(`${this.baseUrl}/${id}`)
  }

  /**
   * Test a webhook by sending a test event
   */
  async test(id: string): Promise<WebhookTestResult> {
    const response = await api.post<WebhookDeliveryResponse>(`${this.baseUrl}/${id}/test`)
    const delivery = mapToWebhookDelivery(response)
    const success = delivery.status === 'Success'
    return {
      delivery,
      success,
      message: success
        ? 'Test webhook delivered successfully'
        : `Test webhook delivery failed: ${delivery.errorMessage || 'Unknown error'}`
    }
  }

  /**
   * Get delivery history for a webhook
   */
  async getDeliveryHistory(id: string, limit = 50): Promise<WebhookDelivery[]> {
    const response = await api.get<WebhookDeliveryResponse[]>(
      `${this.baseUrl}/${id}/deliveries?limit=${limit}`
    )
    return response.map(mapToWebhookDelivery)
  }

  /**
   * Get available event types
   */
  async getEventTypes(): Promise<EventTypeInfo[]> {
    return await api.get<EventTypeInfo[]>(`${this.baseUrl}/event-types`)
  }
}

export const webhookService = new WebhookService()
export default webhookService
