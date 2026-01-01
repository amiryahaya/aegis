import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import api from '@/services/api'
import type {
  Activity,
  ActivityPage,
  ActivityFilter,
  PersonalizedFeedOptions,
  ActivitySubscription,
  SubscribeToActivityRequest,
  ActivityGroup,
  AggregationOptions,
  ActivityStats
} from '@/types/activity'

export const useActivityStore = defineStore('activity', () => {
  // State
  const activities = ref<Activity[]>([])
  const totalCount = ref(0)
  const currentPage = ref(1)
  const pageSize = ref(20)
  const hasMore = ref(false)
  const filter = ref<ActivityFilter>({})
  const subscriptions = ref<ActivitySubscription[]>([])
  const unseenCount = ref(0)
  const stats = ref<ActivityStats | null>(null)
  const isLoading = ref(false)
  const error = ref<string | null>(null)

  // Getters
  const isEmpty = computed(() => activities.value.length === 0)
  const hasActivities = computed(() => activities.value.length > 0)

  // Actions
  async function fetchActivities(workspaceId?: string): Promise<ActivityPage | null> {
    isLoading.value = true
    error.value = null
    try {
      const params = new URLSearchParams()
      if (workspaceId) params.append('workspaceId', workspaceId)
      if (filter.value.type) params.append('type', filter.value.type)
      if (filter.value.verb) params.append('verb', filter.value.verb)
      if (filter.value.resourceType) params.append('resourceType', filter.value.resourceType)
      if (filter.value.actorId) params.append('actorId', filter.value.actorId)
      if (filter.value.minImportance) params.append('minImportance', filter.value.minImportance)
      if (filter.value.since) params.append('since', filter.value.since)
      if (filter.value.until) params.append('until', filter.value.until)
      params.append('page', currentPage.value.toString())
      params.append('pageSize', pageSize.value.toString())

      const endpoint = workspaceId
        ? `/activity/workspace/${workspaceId}?${params}`
        : `/activity?${params}`

      const response = await api.get<ActivityPage>(endpoint)
      activities.value = response.activities
      totalCount.value = response.totalCount
      hasMore.value = response.hasMore
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch activities'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function fetchPersonalizedFeed(options?: PersonalizedFeedOptions): Promise<ActivityPage | null> {
    isLoading.value = true
    error.value = null
    try {
      const params = new URLSearchParams()
      if (options?.includeOwnActivities) params.append('includeOwnActivities', 'true')
      if (options?.onlySubscribed) params.append('onlySubscribed', 'true')
      if (options?.onlyUnseen) params.append('onlyUnseen', 'true')
      if (options?.workspaceIds) {
        options.workspaceIds.forEach(id => params.append('workspaceIds', id))
      }
      params.append('page', (options?.page || currentPage.value).toString())
      params.append('pageSize', (options?.pageSize || pageSize.value).toString())

      const response = await api.get<ActivityPage>(`/activity/feed?${params}`)
      activities.value = response.activities
      totalCount.value = response.totalCount
      hasMore.value = response.hasMore
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch personalized feed'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function fetchResourceActivities(
    resourceId: string,
    resourceType: string
  ): Promise<ActivityPage | null> {
    isLoading.value = true
    error.value = null
    try {
      const params = new URLSearchParams()
      params.append('page', currentPage.value.toString())
      params.append('pageSize', pageSize.value.toString())

      const response = await api.get<ActivityPage>(
        `/activity/resource/${resourceId}?resourceType=${resourceType}&${params}`
      )
      activities.value = response.activities
      totalCount.value = response.totalCount
      hasMore.value = response.hasMore
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch resource activities'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function fetchUserActivities(userId: string): Promise<ActivityPage | null> {
    isLoading.value = true
    error.value = null
    try {
      const params = new URLSearchParams()
      params.append('page', currentPage.value.toString())
      params.append('pageSize', pageSize.value.toString())

      const response = await api.get<ActivityPage>(`/activity/user/${userId}?${params}`)
      activities.value = response.activities
      totalCount.value = response.totalCount
      hasMore.value = response.hasMore
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch user activities'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function loadMore(): Promise<void> {
    if (!hasMore.value || isLoading.value) return
    currentPage.value++

    try {
      const params = new URLSearchParams()
      params.append('page', currentPage.value.toString())
      params.append('pageSize', pageSize.value.toString())

      const response = await api.get<ActivityPage>(`/activity/feed?${params}`)
      activities.value = [...activities.value, ...response.activities]
      hasMore.value = response.hasMore
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to load more activities'
      currentPage.value--
    }
  }

  async function markAsSeen(activityIds?: string[]): Promise<boolean> {
    try {
      await api.post('/activity/seen', { activityIds })
      if (activityIds) {
        unseenCount.value = Math.max(0, unseenCount.value - activityIds.length)
      } else {
        unseenCount.value = 0
      }
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to mark as seen'
      return false
    }
  }

  async function fetchUnseenCount(workspaceId?: string): Promise<number> {
    try {
      const params = workspaceId ? `?workspaceId=${workspaceId}` : ''
      const response = await api.get<{ count: number }>(`/activity/unseen-count${params}`)
      unseenCount.value = response.count
      return response.count
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch unseen count'
      return 0
    }
  }

  async function subscribe(request: SubscribeToActivityRequest): Promise<ActivitySubscription | null> {
    try {
      const response = await api.post<ActivitySubscription>('/activity/subscriptions', request)
      subscriptions.value.push(response)
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to subscribe'
      return null
    }
  }

  async function unsubscribe(subscriptionId: string): Promise<boolean> {
    try {
      await api.delete(`/activity/subscriptions/${subscriptionId}`)
      subscriptions.value = subscriptions.value.filter(s => s.id !== subscriptionId)
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to unsubscribe'
      return false
    }
  }

  async function fetchSubscriptions(): Promise<ActivitySubscription[]> {
    try {
      const response = await api.get<ActivitySubscription[]>('/activity/subscriptions')
      subscriptions.value = response
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch subscriptions'
      return []
    }
  }

  async function fetchAggregatedActivities(
    workspaceId: string,
    options?: AggregationOptions
  ): Promise<ActivityGroup[]> {
    try {
      const params = new URLSearchParams()
      if (options?.groupWindowHours) params.append('groupWindowHours', options.groupWindowHours.toString())
      if (options?.groupByActor !== undefined) params.append('groupByActor', options.groupByActor.toString())
      if (options?.groupByResource !== undefined) params.append('groupByResource', options.groupByResource.toString())
      if (options?.groupByVerb !== undefined) params.append('groupByVerb', options.groupByVerb.toString())
      if (options?.since) params.append('since', options.since)
      if (options?.maxGroups) params.append('maxGroups', options.maxGroups.toString())

      return await api.get<ActivityGroup[]>(
        `/activity/workspace/${workspaceId}/aggregated?${params}`
      )
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch aggregated activities'
      return []
    }
  }

  async function fetchStats(workspaceId?: string, since?: string): Promise<ActivityStats | null> {
    try {
      const params = new URLSearchParams()
      if (workspaceId) params.append('workspaceId', workspaceId)
      if (since) params.append('since', since)

      const response = await api.get<ActivityStats>(`/activity/stats?${params}`)
      stats.value = response
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch stats'
      return null
    }
  }

  function setFilter(newFilter: ActivityFilter) {
    filter.value = newFilter
    currentPage.value = 1
  }

  function clearFilter() {
    filter.value = {}
    currentPage.value = 1
  }

  function clearError() {
    error.value = null
  }

  function reset() {
    activities.value = []
    totalCount.value = 0
    currentPage.value = 1
    hasMore.value = false
    filter.value = {}
    subscriptions.value = []
    unseenCount.value = 0
    stats.value = null
    error.value = null
  }

  // Add activity to the feed (for real-time updates via SignalR)
  function addActivity(activity: Activity) {
    // Add to the beginning of the list
    activities.value = [activity, ...activities.value]
    totalCount.value++
    unseenCount.value++
  }

  return {
    // State
    activities,
    totalCount,
    currentPage,
    pageSize,
    hasMore,
    filter,
    subscriptions,
    unseenCount,
    stats,
    isLoading,
    error,

    // Getters
    isEmpty,
    hasActivities,

    // Actions
    fetchActivities,
    fetchPersonalizedFeed,
    fetchResourceActivities,
    fetchUserActivities,
    loadMore,
    markAsSeen,
    fetchUnseenCount,
    subscribe,
    unsubscribe,
    fetchSubscriptions,
    fetchAggregatedActivities,
    fetchStats,
    setFilter,
    clearFilter,
    clearError,
    reset,
    addActivity
  }
})
