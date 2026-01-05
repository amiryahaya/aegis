<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { TabGroup, TabList, Tab, TabPanels, TabPanel } from '@headlessui/vue'
import {
  ChartBarIcon,
  DocumentTextIcon,
  ChatBubbleLeftRightIcon,
  BoltIcon,
  HandThumbUpIcon,
  LightBulbIcon,
  ArrowDownTrayIcon
} from '@heroicons/vue/24/outline'
import { useAnalyticsStore } from '@/stores/analytics'
import { analyticsService } from '@/services/analytics.service'
import { useToast } from '@/composables/useToast'
import StatCard from '@/components/analytics/StatCard.vue'
import DateRangePicker from '@/components/analytics/DateRangePicker.vue'
import InsightCard from '@/components/analytics/InsightCard.vue'
import TrendChart from '@/components/analytics/TrendChart.vue'
import DistributionChart from '@/components/analytics/DistributionChart.vue'

const analyticsStore = useAnalyticsStore()
const toast = useToast()

const selectedTab = ref(0)
const isExporting = ref(false)

const tabs = [
  { name: 'Overview', icon: ChartBarIcon },
  { name: 'Queries', icon: ChatBubbleLeftRightIcon },
  { name: 'Documents', icon: DocumentTextIcon },
  { name: 'Performance', icon: BoltIcon },
  { name: 'Feedback', icon: HandThumbUpIcon }
]

onMounted(async () => {
  await analyticsStore.fetchDashboard()
  await analyticsStore.fetchInsights()
})

// Export functionality
async function exportAnalytics(format: 'csv' | 'json' | 'pdf' | 'xlsx') {
  if (isExporting.value) return

  isExporting.value = true
  try {
    const blob = await analyticsService.export({
      format: format as 'json' | 'csv' | 'pdf' | 'xlsx',
      fromDate: analyticsStore.dateRange.start,
      toDate: analyticsStore.dateRange.end
    })

    // Create download link
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `analytics-${new Date().toISOString().split('T')[0]}.${format}`
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    URL.revokeObjectURL(url)

    toast.success('Export complete', `Analytics data exported as ${format.toUpperCase()}`)
  } catch (error) {
    toast.error('Export failed', error instanceof Error ? error.message : 'Unknown error')
  } finally {
    isExporting.value = false
  }
}
</script>

<template>
  <div class="h-full overflow-y-auto p-4 sm:p-6">
    <!-- Header -->
    <div class="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
      <div>
        <h1 class="text-xl sm:text-2xl font-bold text-gray-900 dark:text-white">
          Analytics
        </h1>
        <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Monitor usage patterns and system performance
        </p>
      </div>

      <div class="flex items-center gap-3">
        <DateRangePicker
          v-model="analyticsStore.dateRange"
          @update:model-value="analyticsStore.fetchDashboard()"
        />

        <div class="relative">
          <button
            class="btn-secondary gap-2"
            @click="exportAnalytics('csv')"
          >
            <ArrowDownTrayIcon class="h-4 w-4" />
            <span class="hidden sm:inline">Export</span>
          </button>
        </div>
      </div>
    </div>

    <!-- Summary Stats -->
    <div class="mb-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6">
      <StatCard
        title="Total Queries"
        :value="analyticsStore.summary?.totalQueries ?? 0"
        :previous-value="analyticsStore.summary?.totalQueries ? analyticsStore.summary.totalQueries * (1 - analyticsStore.summary.queriesChange / 100) : undefined"
        :icon="ChatBubbleLeftRightIcon"
        icon-color="bg-blue-100 dark:bg-blue-900/30"
        :loading="analyticsStore.isLoading"
      />
      <StatCard
        title="Sessions"
        :value="analyticsStore.summary?.totalSessions ?? 0"
        :previous-value="analyticsStore.summary?.totalSessions ? analyticsStore.summary.totalSessions * (1 - analyticsStore.summary.sessionsChange / 100) : undefined"
        :icon="ChartBarIcon"
        icon-color="bg-purple-100 dark:bg-purple-900/30"
        :loading="analyticsStore.isLoading"
      />
      <StatCard
        title="Documents"
        :value="analyticsStore.summary?.totalDocuments ?? 0"
        :previous-value="analyticsStore.summary?.totalDocuments ? analyticsStore.summary.totalDocuments * (1 - analyticsStore.summary.documentsChange / 100) : undefined"
        :icon="DocumentTextIcon"
        icon-color="bg-green-100 dark:bg-green-900/30"
        :loading="analyticsStore.isLoading"
      />
      <StatCard
        title="Avg Response"
        :value="analyticsStore.summary?.averageResponseTime ?? 0"
        :previous-value="analyticsStore.summary?.averageResponseTime ? analyticsStore.summary.averageResponseTime * (1 + analyticsStore.summary.responseTimeChange / 100) : undefined"
        format="duration"
        :icon="BoltIcon"
        icon-color="bg-amber-100 dark:bg-amber-900/30"
        :increase-is-positive="false"
        :loading="analyticsStore.isLoading"
      />
      <StatCard
        title="Active Users"
        :value="analyticsStore.summary?.activeUsers ?? 0"
        :previous-value="analyticsStore.summary?.activeUsers ? analyticsStore.summary.activeUsers * (1 - analyticsStore.summary.activeUsersChange / 100) : undefined"
        :icon="ChartBarIcon"
        icon-color="bg-cyan-100 dark:bg-cyan-900/30"
        :loading="analyticsStore.isLoading"
      />
      <StatCard
        title="Success Rate"
        :value="analyticsStore.summary?.successRate ?? 0"
        :previous-value="analyticsStore.summary?.successRate ? analyticsStore.summary.successRate - analyticsStore.summary.successRateChange : undefined"
        format="percent"
        :icon="HandThumbUpIcon"
        icon-color="bg-emerald-100 dark:bg-emerald-900/30"
        :loading="analyticsStore.isLoading"
      />
    </div>

    <!-- Insights -->
    <div v-if="analyticsStore.insights.length > 0" class="mb-6">
      <div class="mb-3 flex items-center gap-2">
        <LightBulbIcon class="h-5 w-5 text-yellow-500" />
        <h2 class="text-lg font-semibold text-gray-900 dark:text-white">
          Insights
        </h2>
      </div>
      <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        <InsightCard
          v-for="insight in analyticsStore.insights"
          :key="insight.id"
          :insight="insight"
        />
      </div>
    </div>

    <!-- Tabs -->
    <TabGroup v-model:selectedIndex="selectedTab">
      <TabList class="flex gap-1 rounded-xl bg-gray-100 p-1 dark:bg-gray-800">
        <Tab
          v-for="tab in tabs"
          :key="tab.name"
          v-slot="{ selected }"
          as="template"
        >
          <button
            class="flex w-full items-center justify-center gap-2 rounded-lg px-3 py-2 text-sm font-medium transition-colors"
            :class="selected
              ? 'bg-white text-aegis-600 shadow dark:bg-gray-700 dark:text-aegis-400'
              : 'text-gray-600 hover:bg-white/50 hover:text-gray-900 dark:text-gray-400 dark:hover:bg-gray-700/50'"
          >
            <component :is="tab.icon" class="h-4 w-4" />
            <span class="hidden sm:inline">{{ tab.name }}</span>
          </button>
        </Tab>
      </TabList>

      <TabPanels class="mt-4">
        <!-- Overview Tab -->
        <TabPanel>
          <div class="grid gap-4 lg:grid-cols-2">
            <TrendChart
              title="Queries Over Time"
              :data="analyticsStore.queryAnalytics?.queriesOverTime ?? []"
              color="#6366f1"
              :loading="analyticsStore.isLoading"
              :height="250"
            />
            <TrendChart
              title="Sessions Over Time"
              :data="analyticsStore.sessionAnalytics?.sessionsOverTime ?? []"
              color="#8b5cf6"
              :loading="analyticsStore.isLoading"
              :height="250"
            />
            <DistributionChart
              title="Queries by Type"
              :data="analyticsStore.queryAnalytics?.queriesByType ?? []"
              :loading="analyticsStore.isLoading"
              :height="180"
            />
            <DistributionChart
              title="Queries by Workspace"
              :data="analyticsStore.queryAnalytics?.queriesByWorkspace ?? []"
              :loading="analyticsStore.isLoading"
              :height="180"
            />
          </div>
        </TabPanel>

        <!-- Queries Tab -->
        <TabPanel>
          <div class="grid gap-4 lg:grid-cols-2">
            <TrendChart
              title="Query Volume"
              :data="analyticsStore.queryAnalytics?.queriesOverTime ?? []"
              color="#3b82f6"
              :loading="analyticsStore.isLoading"
              :height="250"
            />
            <div class="card p-4">
              <h3 class="text-sm font-medium text-gray-700 dark:text-gray-300">
                Query Statistics
              </h3>
              <div class="mt-4 space-y-4">
                <div class="flex items-center justify-between">
                  <span class="text-gray-600 dark:text-gray-400">Total Queries</span>
                  <span class="font-semibold text-gray-900 dark:text-white">
                    {{ analyticsStore.queryAnalytics?.totalQueries?.toLocaleString() ?? '-' }}
                  </span>
                </div>
                <div class="flex items-center justify-between">
                  <span class="text-gray-600 dark:text-gray-400">Successful</span>
                  <span class="font-semibold text-green-600 dark:text-green-400">
                    {{ analyticsStore.queryAnalytics?.successfulQueries?.toLocaleString() ?? '-' }}
                  </span>
                </div>
                <div class="flex items-center justify-between">
                  <span class="text-gray-600 dark:text-gray-400">Failed</span>
                  <span class="font-semibold text-red-600 dark:text-red-400">
                    {{ analyticsStore.queryAnalytics?.failedQueries?.toLocaleString() ?? '-' }}
                  </span>
                </div>
                <div class="flex items-center justify-between">
                  <span class="text-gray-600 dark:text-gray-400">Avg Response Time</span>
                  <span class="font-semibold text-gray-900 dark:text-white">
                    {{ analyticsStore.queryAnalytics?.averageResponseTime?.toFixed(2) ?? '-' }}s
                  </span>
                </div>
                <div class="flex items-center justify-between">
                  <span class="text-gray-600 dark:text-gray-400">P95 Response Time</span>
                  <span class="font-semibold text-gray-900 dark:text-white">
                    {{ analyticsStore.queryAnalytics?.p95ResponseTime?.toFixed(2) ?? '-' }}s
                  </span>
                </div>
              </div>
            </div>
            <DistributionChart
              title="Query Complexity"
              :data="analyticsStore.queryAnalytics?.queryComplexityDistribution ?? []"
              :loading="analyticsStore.isLoading"
              :height="180"
            />
            <div class="card p-4">
              <h3 class="text-sm font-medium text-gray-700 dark:text-gray-300">
                Top Keywords
              </h3>
              <div class="mt-4 space-y-2">
                <div
                  v-for="keyword in analyticsStore.queryAnalytics?.topKeywords ?? []"
                  :key="keyword.keyword"
                  class="flex items-center justify-between"
                >
                  <span class="text-gray-600 dark:text-gray-400">{{ keyword.keyword }}</span>
                  <span class="font-medium text-gray-900 dark:text-white">{{ keyword.count }}</span>
                </div>
              </div>
            </div>
          </div>
        </TabPanel>

        <!-- Documents Tab -->
        <TabPanel>
          <div class="grid gap-4 lg:grid-cols-2">
            <TrendChart
              title="Documents Added Over Time"
              :data="analyticsStore.documentAnalytics?.documentsOverTime ?? []"
              color="#10b981"
              :loading="analyticsStore.isLoading"
              :height="250"
            />
            <div class="card p-4">
              <h3 class="text-sm font-medium text-gray-700 dark:text-gray-300">
                Document Statistics
              </h3>
              <div class="mt-4 space-y-4">
                <div class="flex items-center justify-between">
                  <span class="text-gray-600 dark:text-gray-400">Total Documents</span>
                  <span class="font-semibold text-gray-900 dark:text-white">
                    {{ analyticsStore.documentAnalytics?.totalDocuments?.toLocaleString() ?? '-' }}
                  </span>
                </div>
                <div class="flex items-center justify-between">
                  <span class="text-gray-600 dark:text-gray-400">Indexed</span>
                  <span class="font-semibold text-green-600 dark:text-green-400">
                    {{ analyticsStore.documentAnalytics?.documentsIndexed?.toLocaleString() ?? '-' }}
                  </span>
                </div>
                <div class="flex items-center justify-between">
                  <span class="text-gray-600 dark:text-gray-400">Pending</span>
                  <span class="font-semibold text-yellow-600 dark:text-yellow-400">
                    {{ analyticsStore.documentAnalytics?.documentsPending?.toLocaleString() ?? '-' }}
                  </span>
                </div>
                <div class="flex items-center justify-between">
                  <span class="text-gray-600 dark:text-gray-400">Total Chunks</span>
                  <span class="font-semibold text-gray-900 dark:text-white">
                    {{ analyticsStore.documentAnalytics?.totalChunks?.toLocaleString() ?? '-' }}
                  </span>
                </div>
              </div>
            </div>
            <DistributionChart
              title="Documents by Type"
              :data="analyticsStore.documentAnalytics?.documentsByType ?? []"
              :loading="analyticsStore.isLoading"
              :height="180"
            />
            <DistributionChart
              title="Documents by Workspace"
              :data="analyticsStore.documentAnalytics?.documentsByWorkspace ?? []"
              :loading="analyticsStore.isLoading"
              :height="180"
            />
          </div>
        </TabPanel>

        <!-- Performance Tab -->
        <TabPanel>
          <div class="grid gap-4 lg:grid-cols-2">
            <TrendChart
              title="Latency Over Time"
              :data="analyticsStore.performanceAnalytics?.latencyOverTime ?? []"
              color="#f59e0b"
              value-format="duration"
              :loading="analyticsStore.isLoading"
              :height="250"
            />
            <TrendChart
              title="Error Rate Over Time"
              :data="analyticsStore.performanceAnalytics?.errorRateOverTime ?? []"
              color="#ef4444"
              value-format="percent"
              :loading="analyticsStore.isLoading"
              :height="250"
            />
            <TrendChart
              title="Throughput (requests/min)"
              :data="analyticsStore.performanceAnalytics?.throughputOverTime ?? []"
              color="#06b6d4"
              :loading="analyticsStore.isLoading"
              :height="250"
            />
            <div class="card p-4">
              <h3 class="text-sm font-medium text-gray-700 dark:text-gray-300">
                Performance Metrics
              </h3>
              <div class="mt-4 space-y-4">
                <div class="flex items-center justify-between">
                  <span class="text-gray-600 dark:text-gray-400">P50 Latency</span>
                  <span class="font-semibold text-gray-900 dark:text-white">
                    {{ analyticsStore.performanceAnalytics?.p50Latency?.toFixed(2) ?? '-' }}s
                  </span>
                </div>
                <div class="flex items-center justify-between">
                  <span class="text-gray-600 dark:text-gray-400">P95 Latency</span>
                  <span class="font-semibold text-gray-900 dark:text-white">
                    {{ analyticsStore.performanceAnalytics?.p95Latency?.toFixed(2) ?? '-' }}s
                  </span>
                </div>
                <div class="flex items-center justify-between">
                  <span class="text-gray-600 dark:text-gray-400">P99 Latency</span>
                  <span class="font-semibold text-gray-900 dark:text-white">
                    {{ analyticsStore.performanceAnalytics?.p99Latency?.toFixed(2) ?? '-' }}s
                  </span>
                </div>
                <div class="flex items-center justify-between">
                  <span class="text-gray-600 dark:text-gray-400">Cache Hit Rate</span>
                  <span class="font-semibold text-green-600 dark:text-green-400">
                    {{ analyticsStore.performanceAnalytics?.cacheHitRate?.toFixed(1) ?? '-' }}%
                  </span>
                </div>
                <div class="flex items-center justify-between">
                  <span class="text-gray-600 dark:text-gray-400">Success Rate</span>
                  <span class="font-semibold text-green-600 dark:text-green-400">
                    {{ analyticsStore.performanceAnalytics?.successRate?.toFixed(1) ?? '-' }}%
                  </span>
                </div>
              </div>
            </div>
          </div>
        </TabPanel>

        <!-- Feedback Tab -->
        <TabPanel>
          <div class="grid gap-4 lg:grid-cols-2">
            <TrendChart
              title="Feedback Over Time"
              :data="analyticsStore.feedbackAnalytics?.feedbackOverTime ?? []"
              color="#8b5cf6"
              :loading="analyticsStore.isLoading"
              :height="250"
            />
            <DistributionChart
              title="Feedback Distribution"
              :data="analyticsStore.feedbackAnalytics?.feedbackByRating ?? []"
              :loading="analyticsStore.isLoading"
              :height="180"
            />
            <div class="card p-4">
              <h3 class="text-sm font-medium text-gray-700 dark:text-gray-300">
                Feedback Summary
              </h3>
              <div class="mt-4 space-y-4">
                <div class="flex items-center justify-between">
                  <span class="text-gray-600 dark:text-gray-400">Total Feedback</span>
                  <span class="font-semibold text-gray-900 dark:text-white">
                    {{ analyticsStore.feedbackAnalytics?.totalFeedback?.toLocaleString() ?? '-' }}
                  </span>
                </div>
                <div class="flex items-center justify-between">
                  <span class="text-gray-600 dark:text-gray-400">Positive Rate</span>
                  <span class="font-semibold text-green-600 dark:text-green-400">
                    {{ analyticsStore.feedbackAnalytics?.positiveRate?.toFixed(1) ?? '-' }}%
                  </span>
                </div>
                <div class="flex items-center justify-between">
                  <span class="text-gray-600 dark:text-gray-400">Negative Rate</span>
                  <span class="font-semibold text-red-600 dark:text-red-400">
                    {{ analyticsStore.feedbackAnalytics?.negativeRate?.toFixed(1) ?? '-' }}%
                  </span>
                </div>
              </div>
            </div>
            <div class="card p-4">
              <h3 class="text-sm font-medium text-gray-700 dark:text-gray-300">
                Top Issues
              </h3>
              <div class="mt-4 space-y-2">
                <div
                  v-for="issue in analyticsStore.feedbackAnalytics?.topIssues ?? []"
                  :key="issue.issue"
                  class="flex items-center justify-between"
                >
                  <span class="text-gray-600 dark:text-gray-400">{{ issue.issue }}</span>
                  <span class="font-medium text-red-600 dark:text-red-400">{{ issue.count }}</span>
                </div>
              </div>
            </div>
          </div>
        </TabPanel>
      </TabPanels>
    </TabGroup>
  </div>
</template>
