<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAdminStore } from '@/stores/admin'
import { useAuthStore } from '@/stores/auth'
import {
  ServerIcon,
  UsersIcon,
  DocumentTextIcon,
  ChatBubbleLeftRightIcon,
  CpuChipIcon,
  CircleStackIcon,
  ArrowPathIcon,
  CheckCircleIcon,
  ExclamationTriangleIcon,
  XCircleIcon
} from '@heroicons/vue/24/outline'
import {
  TabGroup,
  TabList,
  Tab,
  TabPanels,
  TabPanel
} from '@headlessui/vue'
import LineChart from '@/components/charts/LineChart.vue'
import BarChart from '@/components/charts/BarChart.vue'
import DoughnutChart from '@/components/charts/DoughnutChart.vue'
import type { HealthStatus } from '@/types/admin'

const { t } = useI18n()

const adminStore = useAdminStore()
const authStore = useAuthStore()

const isRefreshing = ref(false)

// Check if user has admin role
const isAdmin = computed(() =>
  authStore.user?.role === 'Admin' || authStore.user?.role === 'SystemAdmin'
)

onMounted(async () => {
  await loadDashboard()
})

async function loadDashboard() {
  isRefreshing.value = true
  await Promise.all([
    adminStore.fetchOverview(),
    adminStore.fetchQueryMetrics(),
    adminStore.fetchDocumentMetrics(),
    adminStore.fetchUserMetrics(),
    adminStore.fetchCacheMetrics()
  ])
  isRefreshing.value = false
}

async function handleRefresh() {
  await loadDashboard()
}

function getHealthStatusIcon(status: HealthStatus) {
  switch (status) {
    case 'Healthy':
      return CheckCircleIcon
    case 'Degraded':
      return ExclamationTriangleIcon
    case 'Unhealthy':
      return XCircleIcon
    default:
      return CheckCircleIcon
  }
}

function getHealthStatusColor(status: HealthStatus) {
  switch (status) {
    case 'Healthy':
      return 'text-green-500'
    case 'Degraded':
      return 'text-yellow-500'
    case 'Unhealthy':
      return 'text-red-500'
    default:
      return 'text-gray-500'
  }
}

function formatNumber(num: number) {
  if (num >= 1000000) {
    return (num / 1000000).toFixed(1) + 'M'
  }
  if (num >= 1000) {
    return (num / 1000).toFixed(1) + 'K'
  }
  return num.toString()
}

function formatPercentage(num: number) {
  return (num * 100).toFixed(1) + '%'
}

function formatMs(ms: number) {
  if (ms >= 1000) {
    return (ms / 1000).toFixed(2) + 's'
  }
  return ms.toFixed(0) + 'ms'
}

// Chart data for Query Trends
const queryTrendLabels = computed(() => {
  // Generate last 7 days labels
  const labels = []
  for (let i = 6; i >= 0; i--) {
    const date = new Date()
    date.setDate(date.getDate() - i)
    labels.push(date.toLocaleDateString('en-US', { weekday: 'short' }))
  }
  return labels
})

const queryTrendData = computed(() => {
  // Mock data - in real app, this would come from API
  const total = adminStore.queryMetrics?.queriesLast7d ?? 0
  const avgPerDay = Math.floor(total / 7)
  return [
    { label: t('admin.metrics.queriesPerHour'), data: generateTrendData(avgPerDay, 7) }
  ]
})

const cacheChartLabels = computed(() => [
  t('admin.metrics.cacheHits'),
  t('admin.metrics.cacheMisses')
])

const cacheChartData = computed(() => [
  {
    label: 'Semantic',
    data: [
      adminStore.cacheMetrics?.semanticCache.hitCount ?? 0,
      adminStore.cacheMetrics?.semanticCache.missCount ?? 0
    ]
  },
  {
    label: 'Embedding',
    data: [
      adminStore.cacheMetrics?.embeddingCache.hitCount ?? 0,
      adminStore.cacheMetrics?.embeddingCache.missCount ?? 0
    ]
  },
  {
    label: 'Response',
    data: [
      adminStore.cacheMetrics?.responseCache.hitCount ?? 0,
      adminStore.cacheMetrics?.responseCache.missCount ?? 0
    ]
  }
])

const documentTypeLabels = computed(() => ['PDF', 'DOCX', 'TXT', 'HTML', 'Other'])

const documentTypeData = computed(() => {
  // Mock data based on total documents
  const total = adminStore.documentMetrics?.totalDocuments ?? 0
  return [
    Math.floor(total * 0.35),
    Math.floor(total * 0.25),
    Math.floor(total * 0.15),
    Math.floor(total * 0.15),
    Math.floor(total * 0.10)
  ]
})

const userActivityLabels = computed(() => {
  const labels = []
  for (let i = 6; i >= 0; i--) {
    const date = new Date()
    date.setDate(date.getDate() - i)
    labels.push(date.toLocaleDateString('en-US', { weekday: 'short' }))
  }
  return labels
})

const userActivityData = computed(() => {
  const activeUsers = adminStore.userMetrics?.activeUsersLast7d ?? 0
  const avgPerDay = Math.floor(activeUsers / 7)
  return [
    { label: t('admin.metrics.activeUsers'), data: generateTrendData(avgPerDay, 7) },
    { label: t('admin.metrics.newUsers'), data: generateTrendData(Math.floor(avgPerDay * 0.2), 7) }
  ]
})

// Helper to generate mock trend data
function generateTrendData(avg: number, days: number): number[] {
  const data = []
  for (let i = 0; i < days; i++) {
    // Add some variation (+/- 30%)
    const variation = 1 + (Math.random() - 0.5) * 0.6
    data.push(Math.max(0, Math.floor(avg * variation)))
  }
  return data
}
</script>

<template>
  <div class="h-full overflow-auto">
    <!-- Access denied -->
    <div v-if="!isAdmin" class="flex h-full items-center justify-center">
      <div class="text-center">
        <XCircleIcon class="mx-auto h-16 w-16 text-red-400" />
        <h2 class="mt-4 text-xl font-semibold text-gray-900 dark:text-white">Access Denied</h2>
        <p class="mt-2 text-gray-500 dark:text-gray-400">
          You don't have permission to access the admin dashboard.
        </p>
      </div>
    </div>

    <div v-else class="p-6">
      <!-- Header -->
      <div class="mb-6 flex items-center justify-between">
        <div>
          <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Admin Dashboard</h1>
          <p class="mt-1 text-gray-500 dark:text-gray-400">
            System overview and management
          </p>
        </div>

        <button
          class="btn-ghost inline-flex items-center gap-2"
          :disabled="isRefreshing"
          @click="handleRefresh"
        >
          <ArrowPathIcon class="h-5 w-5" :class="isRefreshing && 'animate-spin'" />
          Refresh
        </button>
      </div>

      <!-- System Health -->
      <div v-if="adminStore.overview" class="mb-6 card p-4">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-3">
            <component
              :is="getHealthStatusIcon(adminStore.overview.systemHealth.status)"
              class="h-8 w-8"
              :class="getHealthStatusColor(adminStore.overview.systemHealth.status)"
            />
            <div>
              <h2 class="font-semibold text-gray-900 dark:text-white">System Status</h2>
              <p class="text-sm text-gray-500 dark:text-gray-400">
                {{ adminStore.overview.systemHealth.status }} · v{{ adminStore.overview.version }} · {{ adminStore.overview.environment }}
              </p>
            </div>
          </div>
          <div class="text-right text-sm text-gray-500 dark:text-gray-400">
            <p>Uptime: {{ adminStore.overview.uptime }}</p>
            <p>Last checked: {{ new Date(adminStore.overview.systemHealth.lastCheckedAt).toLocaleTimeString() }}</p>
          </div>
        </div>

        <!-- Health Components -->
        <div v-if="adminStore.overview.systemHealth.components.length > 0" class="mt-4 flex flex-wrap gap-2">
          <div
            v-for="component in adminStore.overview.systemHealth.components"
            :key="component.name"
            class="inline-flex items-center gap-1 rounded-full px-3 py-1 text-xs font-medium"
            :class="component.status === 'Healthy'
              ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400'
              : component.status === 'Degraded'
                ? 'bg-yellow-100 text-yellow-700 dark:bg-yellow-900/30 dark:text-yellow-400'
                : 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400'"
          >
            <component
              :is="getHealthStatusIcon(component.status)"
              class="h-3 w-3"
            />
            {{ component.name }}
          </div>
        </div>
      </div>

      <!-- Stats Grid -->
      <div class="mb-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <!-- Active Users -->
        <div class="card p-5">
          <div class="flex items-center gap-4">
            <div class="rounded-lg bg-blue-100 p-3 dark:bg-blue-900/50">
              <UsersIcon class="h-6 w-6 text-blue-600 dark:text-blue-400" />
            </div>
            <div>
              <p class="text-sm text-gray-500 dark:text-gray-400">Active Users</p>
              <p class="text-2xl font-semibold text-gray-900 dark:text-white">
                {{ adminStore.overview?.activeUsers ?? 0 }}
              </p>
              <p class="text-xs text-gray-400">
                of {{ adminStore.overview?.totalUsers ?? 0 }} total
              </p>
            </div>
          </div>
        </div>

        <!-- Total Workspaces -->
        <div class="card p-5">
          <div class="flex items-center gap-4">
            <div class="rounded-lg bg-purple-100 p-3 dark:bg-purple-900/50">
              <ServerIcon class="h-6 w-6 text-purple-600 dark:text-purple-400" />
            </div>
            <div>
              <p class="text-sm text-gray-500 dark:text-gray-400">Workspaces</p>
              <p class="text-2xl font-semibold text-gray-900 dark:text-white">
                {{ formatNumber(adminStore.overview?.totalWorkspaces ?? 0) }}
              </p>
            </div>
          </div>
        </div>

        <!-- Total Documents -->
        <div class="card p-5">
          <div class="flex items-center gap-4">
            <div class="rounded-lg bg-green-100 p-3 dark:bg-green-900/50">
              <DocumentTextIcon class="h-6 w-6 text-green-600 dark:text-green-400" />
            </div>
            <div>
              <p class="text-sm text-gray-500 dark:text-gray-400">Documents</p>
              <p class="text-2xl font-semibold text-gray-900 dark:text-white">
                {{ formatNumber(adminStore.overview?.totalDocuments ?? 0) }}
              </p>
            </div>
          </div>
        </div>

        <!-- Total Queries -->
        <div class="card p-5">
          <div class="flex items-center gap-4">
            <div class="rounded-lg bg-amber-100 p-3 dark:bg-amber-900/50">
              <ChatBubbleLeftRightIcon class="h-6 w-6 text-amber-600 dark:text-amber-400" />
            </div>
            <div>
              <p class="text-sm text-gray-500 dark:text-gray-400">Total Queries</p>
              <p class="text-2xl font-semibold text-gray-900 dark:text-white">
                {{ formatNumber(adminStore.overview?.totalQueries ?? 0) }}
              </p>
            </div>
          </div>
        </div>
      </div>

      <!-- Tabs -->
      <TabGroup>
        <TabList class="flex space-x-1 rounded-xl bg-gray-100 p-1 dark:bg-gray-800">
          <Tab
            v-slot="{ selected }"
            as="template"
          >
            <button
              class="flex w-full items-center justify-center gap-2 rounded-lg py-2.5 text-sm font-medium leading-5 transition-all"
              :class="selected
                ? 'bg-white text-aegis-700 shadow dark:bg-gray-700 dark:text-aegis-400'
                : 'text-gray-600 hover:bg-white/30 hover:text-gray-800 dark:text-gray-400'"
            >
              <ChatBubbleLeftRightIcon class="h-5 w-5" />
              Queries
            </button>
          </Tab>
          <Tab
            v-slot="{ selected }"
            as="template"
          >
            <button
              class="flex w-full items-center justify-center gap-2 rounded-lg py-2.5 text-sm font-medium leading-5 transition-all"
              :class="selected
                ? 'bg-white text-aegis-700 shadow dark:bg-gray-700 dark:text-aegis-400'
                : 'text-gray-600 hover:bg-white/30 hover:text-gray-800 dark:text-gray-400'"
            >
              <DocumentTextIcon class="h-5 w-5" />
              Documents
            </button>
          </Tab>
          <Tab
            v-slot="{ selected }"
            as="template"
          >
            <button
              class="flex w-full items-center justify-center gap-2 rounded-lg py-2.5 text-sm font-medium leading-5 transition-all"
              :class="selected
                ? 'bg-white text-aegis-700 shadow dark:bg-gray-700 dark:text-aegis-400'
                : 'text-gray-600 hover:bg-white/30 hover:text-gray-800 dark:text-gray-400'"
            >
              <CircleStackIcon class="h-5 w-5" />
              Cache
            </button>
          </Tab>
          <Tab
            v-slot="{ selected }"
            as="template"
          >
            <button
              class="flex w-full items-center justify-center gap-2 rounded-lg py-2.5 text-sm font-medium leading-5 transition-all"
              :class="selected
                ? 'bg-white text-aegis-700 shadow dark:bg-gray-700 dark:text-aegis-400'
                : 'text-gray-600 hover:bg-white/30 hover:text-gray-800 dark:text-gray-400'"
            >
              <UsersIcon class="h-5 w-5" />
              Users
            </button>
          </Tab>
        </TabList>

        <TabPanels class="mt-6">
          <!-- Queries Tab -->
          <TabPanel>
            <div class="grid gap-6 lg:grid-cols-2">
              <div class="card p-6">
                <h3 class="text-lg font-semibold text-gray-900 dark:text-white">{{ t('admin.charts.queryTrends') }}</h3>
                <div class="mt-4 space-y-4">
                  <div class="flex justify-between">
                    <span class="text-gray-500 dark:text-gray-400">{{ t('admin.stats.totalQueries') }}</span>
                    <span class="font-medium text-gray-900 dark:text-white">
                      {{ formatNumber(adminStore.queryMetrics?.totalQueries ?? 0) }}
                    </span>
                  </div>
                  <div class="flex justify-between">
                    <span class="text-gray-500 dark:text-gray-400">Last 24 Hours</span>
                    <span class="font-medium text-gray-900 dark:text-white">
                      {{ formatNumber(adminStore.queryMetrics?.queriesLast24h ?? 0) }}
                    </span>
                  </div>
                  <div class="flex justify-between">
                    <span class="text-gray-500 dark:text-gray-400">Last 7 Days</span>
                    <span class="font-medium text-gray-900 dark:text-white">
                      {{ formatNumber(adminStore.queryMetrics?.queriesLast7d ?? 0) }}
                    </span>
                  </div>
                </div>
              </div>

              <div class="card p-6">
                <h3 class="text-lg font-semibold text-gray-900 dark:text-white">Performance</h3>
                <div class="mt-4 space-y-4">
                  <div class="flex justify-between">
                    <span class="text-gray-500 dark:text-gray-400">{{ t('admin.metrics.avgResponseTime') }}</span>
                    <span class="font-medium text-gray-900 dark:text-white">
                      {{ formatMs(adminStore.queryMetrics?.averageResponseTime ?? 0) }}
                    </span>
                  </div>
                  <div class="flex justify-between">
                    <span class="text-gray-500 dark:text-gray-400">{{ t('admin.stats.cacheHitRate') }}</span>
                    <span class="font-medium text-green-600 dark:text-green-400">
                      {{ formatPercentage(adminStore.queryMetrics?.cacheHitRate ?? 0) }}
                    </span>
                  </div>
                  <div class="flex justify-between">
                    <span class="text-gray-500 dark:text-gray-400">Error Rate</span>
                    <span class="font-medium" :class="(adminStore.queryMetrics?.errorRate ?? 0) > 0.05 ? 'text-red-600' : 'text-gray-900 dark:text-white'">
                      {{ formatPercentage(adminStore.queryMetrics?.errorRate ?? 0) }}
                    </span>
                  </div>
                </div>
              </div>
            </div>

            <!-- Query Trends Chart -->
            <div class="mt-6 card p-6">
              <h3 class="mb-4 text-lg font-semibold text-gray-900 dark:text-white">{{ t('admin.charts.queryTrends') }}</h3>
              <LineChart
                :labels="queryTrendLabels"
                :datasets="queryTrendData"
                :height="280"
              />
            </div>
          </TabPanel>

          <!-- Documents Tab -->
          <TabPanel>
            <div class="grid gap-6 lg:grid-cols-2">
              <div class="card p-6">
                <h3 class="text-lg font-semibold text-gray-900 dark:text-white">{{ t('admin.charts.documentStats') }}</h3>
                <div class="mt-4 space-y-4">
                  <div class="flex justify-between">
                    <span class="text-gray-500 dark:text-gray-400">{{ t('admin.stats.totalDocuments') }}</span>
                    <span class="font-medium text-gray-900 dark:text-white">
                      {{ formatNumber(adminStore.documentMetrics?.totalDocuments ?? 0) }}
                    </span>
                  </div>
                  <div class="flex justify-between">
                    <span class="text-gray-500 dark:text-gray-400">Total Chunks</span>
                    <span class="font-medium text-gray-900 dark:text-white">
                      {{ formatNumber(adminStore.documentMetrics?.totalChunks ?? 0) }}
                    </span>
                  </div>
                  <div class="flex justify-between">
                    <span class="text-gray-500 dark:text-gray-400">Uploaded Today</span>
                    <span class="font-medium text-gray-900 dark:text-white">
                      {{ formatNumber(adminStore.documentMetrics?.documentsLast24h ?? 0) }}
                    </span>
                  </div>
                </div>
              </div>

              <div class="card p-6">
                <h3 class="text-lg font-semibold text-gray-900 dark:text-white">Processing Queue</h3>
                <div class="mt-4 space-y-4">
                  <div class="flex justify-between">
                    <span class="text-gray-500 dark:text-gray-400">In Queue</span>
                    <span class="font-medium text-gray-900 dark:text-white">
                      {{ adminStore.documentMetrics?.processingQueue ?? 0 }}
                    </span>
                  </div>
                  <div class="flex justify-between">
                    <span class="text-gray-500 dark:text-gray-400">Failed</span>
                    <span class="font-medium" :class="(adminStore.documentMetrics?.failedCount ?? 0) > 0 ? 'text-red-600' : 'text-gray-900 dark:text-white'">
                      {{ adminStore.documentMetrics?.failedCount ?? 0 }}
                    </span>
                  </div>
                </div>
              </div>
            </div>

            <!-- Document Types Chart -->
            <div class="mt-6 card p-6">
              <h3 class="mb-4 text-lg font-semibold text-gray-900 dark:text-white">Document Types Distribution</h3>
              <DoughnutChart
                :labels="documentTypeLabels"
                :data="documentTypeData"
                :height="280"
              />
            </div>
          </TabPanel>

          <!-- Cache Tab -->
          <TabPanel>
            <div class="grid gap-6 lg:grid-cols-3">
              <div class="card p-6">
                <div class="flex items-center gap-2">
                  <CpuChipIcon class="h-5 w-5 text-aegis-600" />
                  <h3 class="text-lg font-semibold text-gray-900 dark:text-white">Semantic Cache</h3>
                </div>
                <div class="mt-4 space-y-3">
                  <div class="flex justify-between text-sm">
                    <span class="text-gray-500 dark:text-gray-400">Hit Rate</span>
                    <span class="font-medium text-green-600">
                      {{ formatPercentage(adminStore.cacheMetrics?.semanticCache.hitRate ?? 0) }}
                    </span>
                  </div>
                  <div class="flex justify-between text-sm">
                    <span class="text-gray-500 dark:text-gray-400">Hits / Misses</span>
                    <span class="font-medium text-gray-900 dark:text-white">
                      {{ formatNumber(adminStore.cacheMetrics?.semanticCache.hitCount ?? 0) }} /
                      {{ formatNumber(adminStore.cacheMetrics?.semanticCache.missCount ?? 0) }}
                    </span>
                  </div>
                  <div class="flex justify-between text-sm">
                    <span class="text-gray-500 dark:text-gray-400">Size</span>
                    <span class="font-medium text-gray-900 dark:text-white">
                      {{ adminStore.cacheMetrics?.semanticCache.size ?? 0 }} /
                      {{ adminStore.cacheMetrics?.semanticCache.maxSize ?? 0 }}
                    </span>
                  </div>
                </div>
              </div>

              <div class="card p-6">
                <div class="flex items-center gap-2">
                  <CpuChipIcon class="h-5 w-5 text-purple-600" />
                  <h3 class="text-lg font-semibold text-gray-900 dark:text-white">Embedding Cache</h3>
                </div>
                <div class="mt-4 space-y-3">
                  <div class="flex justify-between text-sm">
                    <span class="text-gray-500 dark:text-gray-400">Hit Rate</span>
                    <span class="font-medium text-green-600">
                      {{ formatPercentage(adminStore.cacheMetrics?.embeddingCache.hitRate ?? 0) }}
                    </span>
                  </div>
                  <div class="flex justify-between text-sm">
                    <span class="text-gray-500 dark:text-gray-400">Hits / Misses</span>
                    <span class="font-medium text-gray-900 dark:text-white">
                      {{ formatNumber(adminStore.cacheMetrics?.embeddingCache.hitCount ?? 0) }} /
                      {{ formatNumber(adminStore.cacheMetrics?.embeddingCache.missCount ?? 0) }}
                    </span>
                  </div>
                  <div class="flex justify-between text-sm">
                    <span class="text-gray-500 dark:text-gray-400">Size</span>
                    <span class="font-medium text-gray-900 dark:text-white">
                      {{ adminStore.cacheMetrics?.embeddingCache.size ?? 0 }} /
                      {{ adminStore.cacheMetrics?.embeddingCache.maxSize ?? 0 }}
                    </span>
                  </div>
                </div>
              </div>

              <div class="card p-6">
                <div class="flex items-center gap-2">
                  <CpuChipIcon class="h-5 w-5 text-amber-600" />
                  <h3 class="text-lg font-semibold text-gray-900 dark:text-white">Response Cache</h3>
                </div>
                <div class="mt-4 space-y-3">
                  <div class="flex justify-between text-sm">
                    <span class="text-gray-500 dark:text-gray-400">Hit Rate</span>
                    <span class="font-medium text-green-600">
                      {{ formatPercentage(adminStore.cacheMetrics?.responseCache.hitRate ?? 0) }}
                    </span>
                  </div>
                  <div class="flex justify-between text-sm">
                    <span class="text-gray-500 dark:text-gray-400">Hits / Misses</span>
                    <span class="font-medium text-gray-900 dark:text-white">
                      {{ formatNumber(adminStore.cacheMetrics?.responseCache.hitCount ?? 0) }} /
                      {{ formatNumber(adminStore.cacheMetrics?.responseCache.missCount ?? 0) }}
                    </span>
                  </div>
                  <div class="flex justify-between text-sm">
                    <span class="text-gray-500 dark:text-gray-400">Size</span>
                    <span class="font-medium text-gray-900 dark:text-white">
                      {{ adminStore.cacheMetrics?.responseCache.size ?? 0 }} /
                      {{ adminStore.cacheMetrics?.responseCache.maxSize ?? 0 }}
                    </span>
                  </div>
                </div>
              </div>
            </div>

            <!-- Cache Performance Chart -->
            <div class="mt-6 card p-6">
              <h3 class="mb-4 text-lg font-semibold text-gray-900 dark:text-white">{{ t('admin.charts.cachePerformance') }}</h3>
              <BarChart
                :labels="cacheChartLabels"
                :datasets="cacheChartData"
                :height="280"
              />
            </div>

            <div class="mt-6">
              <button
                class="btn-ghost text-red-600 hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-900/20"
                @click="adminStore.clearCache('all')"
              >
                Clear All Caches
              </button>
            </div>
          </TabPanel>

          <!-- Users Tab -->
          <TabPanel>
            <div class="card p-6">
              <div class="flex items-center justify-between mb-6">
                <h3 class="text-lg font-semibold text-gray-900 dark:text-white">{{ t('admin.charts.userActivity') }}</h3>
              </div>

              <div class="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
                <div>
                  <p class="text-sm text-gray-500 dark:text-gray-400">{{ t('admin.stats.totalUsers') }}</p>
                  <p class="text-2xl font-semibold text-gray-900 dark:text-white">
                    {{ formatNumber(adminStore.userMetrics?.totalUsers ?? 0) }}
                  </p>
                </div>
                <div>
                  <p class="text-sm text-gray-500 dark:text-gray-400">Active (24h)</p>
                  <p class="text-2xl font-semibold text-gray-900 dark:text-white">
                    {{ formatNumber(adminStore.userMetrics?.activeUsersLast24h ?? 0) }}
                  </p>
                </div>
                <div>
                  <p class="text-sm text-gray-500 dark:text-gray-400">Active (7d)</p>
                  <p class="text-2xl font-semibold text-gray-900 dark:text-white">
                    {{ formatNumber(adminStore.userMetrics?.activeUsersLast7d ?? 0) }}
                  </p>
                </div>
                <div>
                  <p class="text-sm text-gray-500 dark:text-gray-400">{{ t('admin.metrics.newUsers') }} (7d)</p>
                  <p class="text-2xl font-semibold text-green-600 dark:text-green-400">
                    +{{ formatNumber(adminStore.userMetrics?.newUsersLast7d ?? 0) }}
                  </p>
                </div>
              </div>

              <div v-if="adminStore.userMetrics?.byRole" class="mt-6 border-t border-gray-200 pt-6 dark:border-gray-700">
                <h4 class="mb-4 font-medium text-gray-900 dark:text-white">Users by Role</h4>
                <div class="flex flex-wrap gap-4">
                  <div
                    v-for="(count, role) in adminStore.userMetrics.byRole"
                    :key="role"
                    class="rounded-lg bg-gray-100 px-4 py-2 dark:bg-gray-700"
                  >
                    <span class="text-sm text-gray-500 dark:text-gray-400">{{ role }}</span>
                    <p class="font-semibold text-gray-900 dark:text-white">{{ count }}</p>
                  </div>
                </div>
              </div>
            </div>

            <!-- User Activity Chart -->
            <div class="mt-6 card p-6">
              <h3 class="mb-4 text-lg font-semibold text-gray-900 dark:text-white">{{ t('admin.charts.userActivity') }} (7 Days)</h3>
              <BarChart
                :labels="userActivityLabels"
                :datasets="userActivityData"
                :height="280"
              />
            </div>
          </TabPanel>
        </TabPanels>
      </TabGroup>
    </div>
  </div>
</template>
