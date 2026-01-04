import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { webhookService } from '@/services/webhook.service'
import { useAuthStore } from '@/stores/auth'
import type {
  WebhookSubscription,
  WebhookDelivery,
  CreateWebhookRequest,
  UpdateWebhookRequest,
  WebhookFilters,
  WebhookStats,
  WebhookTestResult
} from '@/types'

export const useWebhookStore = defineStore('webhook', () => {
  const authStore = useAuthStore()
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
      const teamId = authStore.user?.teamId
      if (!teamId) {
        throw new Error('No team ID available')
      }
      webhooks.value = await webhookService.list(teamId)
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
      // Check cache first
      const cached = webhooks.value.find(w => w.id === id)
      if (cached) {
        selectedWebhook.value = cached
        return cached
      }

      // Fetch from API
      const webhook = await webhookService.get(id)
      selectedWebhook.value = webhook
      return webhook
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
      const teamId = authStore.user?.teamId
      if (!teamId) {
        throw new Error('No team ID available')
      }

      const newWebhook = await webhookService.create({ ...request, teamId })
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
      const updated = await webhookService.update(id, request)

      const index = webhooks.value.findIndex(w => w.id === id)
      if (index !== -1) {
        webhooks.value[index] = updated
      }

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
      await webhookService.delete(id)

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
      const result = await webhookService.test(id)

      // Update local health after test
      const webhookIndex = webhooks.value.findIndex(w => w.id === id)
      if (webhookIndex !== -1) {
        const health = { ...webhooks.value[webhookIndex].health }
        if (result.success) {
          health.successCount++
          health.lastSuccessAt = new Date().toISOString()
        } else {
          health.failureCount++
          health.lastFailureAt = new Date().toISOString()
          health.lastError = result.delivery.errorMessage
        }
        health.successRate =
          health.successCount + health.failureCount > 0
            ? (health.successCount / (health.successCount + health.failureCount)) * 100
            : 100

        webhooks.value[webhookIndex] = {
          ...webhooks.value[webhookIndex],
          health
        }
      }

      return result
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
      deliveryHistory.value = await webhookService.getDeliveryHistory(webhookId, limit)
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
      // Find the delivery to get the webhook ID
      const delivery = deliveryHistory.value.find(d => d.id === deliveryId)
      if (!delivery) {
        throw new Error('Delivery not found')
      }

      // Re-test the webhook (backend doesn't have a retry endpoint, so we test again)
      const result = await webhookService.test(delivery.subscriptionId)

      // Update the delivery in the list
      const index = deliveryHistory.value.findIndex(d => d.id === deliveryId)
      if (index !== -1) {
        deliveryHistory.value[index] = {
          ...deliveryHistory.value[index],
          status: result.success ? 'Success' : 'Failed',
          httpStatusCode: result.delivery.httpStatusCode,
          attemptNumber: deliveryHistory.value[index].attemptNumber + 1,
          attemptedAt: new Date().toISOString(),
          errorMessage: result.success ? undefined : result.delivery.errorMessage
        }
      }

      return deliveryHistory.value[index]
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
