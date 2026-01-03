<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { TabGroup, TabList, Tab, TabPanels, TabPanel } from '@headlessui/vue'
import {
  DocumentChartBarIcon,
  ClockIcon,
  ArchiveBoxIcon,
  PlusIcon,
  PlayIcon,
  PauseIcon,
  TrashIcon,
  CalendarIcon
} from '@heroicons/vue/24/outline'
import { useExport, useReportDialog } from '@/composables/useExport'
import ExportHistory from '@/components/export/ExportHistory.vue'
import ReportDialog from '@/components/export/ReportDialog.vue'
import type { ExportHistoryItem, ScheduledReport } from '@/types/export'
import { REPORT_TEMPLATES, getReportTemplateById } from '@/types/export'

const { exportHistory, downloadExport, clearHistory, deleteHistoryItem } = useExport()
const {
  openDialog,
  scheduledReports,
  toggleScheduledReport,
  deleteScheduledReport
} = useReportDialog()

const selectedTab = ref(0)

const tabs = [
  { name: 'Report Templates', icon: DocumentChartBarIcon },
  { name: 'Scheduled Reports', icon: ClockIcon },
  { name: 'Export History', icon: ArchiveBoxIcon }
]

// Load sample scheduled reports for demo
onMounted(() => {
  if (scheduledReports.value.length === 0) {
    scheduledReports.value = [
      {
        id: 'sr-1',
        name: 'Weekly Usage Summary',
        reportType: 'usage-summary',
        format: 'pdf',
        frequency: 'weekly',
        dayOfWeek: 1,
        time: '09:00',
        timezone: 'America/New_York',
        recipients: ['admin@example.com'],
        enabled: true,
        lastRunAt: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString(),
        nextRunAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString(),
        filters: {},
        createdAt: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString(),
        updatedAt: new Date().toISOString()
      },
      {
        id: 'sr-2',
        name: 'Monthly Compliance Audit',
        reportType: 'audit-report',
        format: 'xlsx',
        frequency: 'monthly',
        dayOfMonth: 1,
        time: '08:00',
        timezone: 'America/New_York',
        recipients: ['compliance@example.com', 'admin@example.com'],
        enabled: true,
        lastRunAt: new Date(Date.now() - 15 * 24 * 60 * 60 * 1000).toISOString(),
        nextRunAt: new Date(Date.now() + 15 * 24 * 60 * 60 * 1000).toISOString(),
        filters: {},
        createdAt: new Date(Date.now() - 60 * 24 * 60 * 60 * 1000).toISOString(),
        updatedAt: new Date().toISOString()
      }
    ]
  }
})

function handleDownload(item: ExportHistoryItem) {
  downloadExport({
    id: item.id,
    status: item.status,
    resourceType: item.resourceType,
    format: item.format,
    fileName: item.fileName,
    fileSize: item.fileSize,
    progress: 100,
    createdAt: item.createdAt,
    completedAt: item.completedAt,
    downloadUrl: item.downloadUrl,
    options: {
      format: item.format,
      resourceType: item.resourceType
    }
  })
}

function formatDate(dateStr: string) {
  const date = new Date(dateStr)
  return date.toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

function getFrequencyLabel(report: ScheduledReport): string {
  switch (report.frequency) {
    case 'daily':
      return `Daily at ${report.time}`
    case 'weekly':
      const days = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']
      return `Weekly on ${days[report.dayOfWeek || 0]} at ${report.time}`
    case 'monthly':
      return `Monthly on day ${report.dayOfMonth} at ${report.time}`
    case 'quarterly':
      return `Quarterly at ${report.time}`
    default:
      return report.frequency
  }
}
</script>

<template>
  <div class="h-full overflow-y-auto p-4 sm:p-6">
    <!-- Header -->
    <div class="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
      <div>
        <h1 class="text-xl sm:text-2xl font-bold text-gray-900 dark:text-white">
          Reports & Exports
        </h1>
        <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Generate reports and manage your export history
        </p>
      </div>

      <button type="button" class="btn-primary gap-2" @click="openDialog()">
        <PlusIcon class="h-5 w-5" />
        Generate Report
      </button>
    </div>

    <!-- Tabs -->
    <TabGroup :selected-index="selectedTab" @change="(index: number) => selectedTab = index">
      <TabList class="flex gap-1 rounded-lg bg-gray-100 p-1 dark:bg-gray-800">
        <Tab
          v-for="tab in tabs"
          :key="tab.name"
          v-slot="{ selected }"
          as="template"
        >
          <button
            class="flex items-center gap-2 rounded-md px-3 py-2 text-sm font-medium transition-colors focus:outline-none focus:ring-2 focus:ring-aegis-500 focus:ring-offset-2 dark:focus:ring-offset-gray-900"
            :class="[
              selected
                ? 'bg-white text-gray-900 shadow dark:bg-gray-700 dark:text-white'
                : 'text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200'
            ]"
          >
            <component :is="tab.icon" class="h-4 w-4" />
            {{ tab.name }}
          </button>
        </Tab>
      </TabList>

      <TabPanels class="mt-6">
        <!-- Report Templates -->
        <TabPanel>
          <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <div
              v-for="template in REPORT_TEMPLATES"
              :key="template.id"
              class="card p-4 hover:shadow-lg transition-shadow"
            >
              <div class="flex items-start justify-between">
                <div
                  class="rounded-lg p-2"
                  :class="[
                    template.category === 'analytics'
                      ? 'bg-blue-100 dark:bg-blue-900/30'
                      : template.category === 'compliance'
                        ? 'bg-green-100 dark:bg-green-900/30'
                        : 'bg-purple-100 dark:bg-purple-900/30'
                  ]"
                >
                  <DocumentChartBarIcon
                    class="h-5 w-5"
                    :class="[
                      template.category === 'analytics'
                        ? 'text-blue-600 dark:text-blue-400'
                        : template.category === 'compliance'
                          ? 'text-green-600 dark:text-green-400'
                          : 'text-purple-600 dark:text-purple-400'
                    ]"
                  />
                </div>
                <span
                  class="inline-flex rounded-full px-2 py-0.5 text-xs font-medium capitalize"
                  :class="[
                    template.category === 'analytics'
                      ? 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400'
                      : template.category === 'compliance'
                        ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400'
                        : 'bg-purple-100 text-purple-700 dark:bg-purple-900/30 dark:text-purple-400'
                  ]"
                >
                  {{ template.category }}
                </span>
              </div>

              <h3 class="mt-3 font-medium text-gray-900 dark:text-white">
                {{ template.name }}
              </h3>
              <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
                {{ template.description }}
              </p>

              <div class="mt-3 flex gap-1">
                <span
                  v-for="format in template.formats"
                  :key="format"
                  class="inline-flex rounded px-1.5 py-0.5 text-xs font-medium bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-300"
                >
                  {{ format.toUpperCase() }}
                </span>
              </div>

              <button
                type="button"
                class="mt-4 w-full btn-secondary text-sm"
                @click="openDialog()"
              >
                Generate
              </button>
            </div>
          </div>
        </TabPanel>

        <!-- Scheduled Reports -->
        <TabPanel>
          <div v-if="scheduledReports.length === 0" class="card p-8 text-center">
            <ClockIcon class="mx-auto h-12 w-12 text-gray-400" />
            <h3 class="mt-2 text-sm font-medium text-gray-900 dark:text-white">
              No scheduled reports
            </h3>
            <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
              Set up automatic report generation on a recurring schedule
            </p>
            <button type="button" class="mt-4 btn-primary gap-2" @click="openDialog()">
              <PlusIcon class="h-4 w-4" />
              Schedule Report
            </button>
          </div>

          <div v-else class="space-y-4">
            <div
              v-for="report in scheduledReports"
              :key="report.id"
              class="card p-4"
            >
              <div class="flex items-start justify-between">
                <div class="flex items-start gap-3">
                  <div
                    class="rounded-lg p-2"
                    :class="[
                      report.enabled
                        ? 'bg-green-100 dark:bg-green-900/30'
                        : 'bg-gray-100 dark:bg-gray-700'
                    ]"
                  >
                    <CalendarIcon
                      class="h-5 w-5"
                      :class="[
                        report.enabled
                          ? 'text-green-600 dark:text-green-400'
                          : 'text-gray-400'
                      ]"
                    />
                  </div>
                  <div>
                    <h3 class="font-medium text-gray-900 dark:text-white">
                      {{ report.name }}
                    </h3>
                    <p class="text-sm text-gray-500 dark:text-gray-400">
                      {{ getReportTemplateById(report.reportType)?.name }}
                    </p>
                    <div class="mt-1 flex items-center gap-2 text-xs text-gray-400 dark:text-gray-500">
                      <span>{{ getFrequencyLabel(report) }}</span>
                      <span>&middot;</span>
                      <span>{{ report.format.toUpperCase() }}</span>
                    </div>
                  </div>
                </div>

                <div class="flex items-center gap-1">
                  <button
                    type="button"
                    class="rounded-lg p-1.5 text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-700"
                    :title="report.enabled ? 'Pause' : 'Resume'"
                    @click="toggleScheduledReport(report.id)"
                  >
                    <PauseIcon v-if="report.enabled" class="h-4 w-4" />
                    <PlayIcon v-else class="h-4 w-4" />
                  </button>
                  <button
                    type="button"
                    class="rounded-lg p-1.5 text-gray-400 hover:bg-gray-100 hover:text-red-600 dark:hover:bg-gray-700 dark:hover:text-red-400"
                    title="Delete"
                    @click="deleteScheduledReport(report.id)"
                  >
                    <TrashIcon class="h-4 w-4" />
                  </button>
                </div>
              </div>

              <div class="mt-3 flex items-center gap-4 text-sm">
                <div v-if="report.lastRunAt">
                  <span class="text-gray-500 dark:text-gray-400">Last run:</span>
                  <span class="ml-1 text-gray-700 dark:text-gray-300">
                    {{ formatDate(report.lastRunAt) }}
                  </span>
                </div>
                <div>
                  <span class="text-gray-500 dark:text-gray-400">Next run:</span>
                  <span class="ml-1 text-gray-700 dark:text-gray-300">
                    {{ formatDate(report.nextRunAt) }}
                  </span>
                </div>
              </div>

              <div class="mt-2 flex gap-1">
                <span
                  v-for="recipient in report.recipients"
                  :key="recipient"
                  class="inline-flex rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-600 dark:bg-gray-700 dark:text-gray-300"
                >
                  {{ recipient }}
                </span>
              </div>
            </div>
          </div>
        </TabPanel>

        <!-- Export History -->
        <TabPanel>
          <div class="card p-4">
            <ExportHistory
              :history="exportHistory"
              @download="handleDownload"
              @delete="deleteHistoryItem"
              @clear="clearHistory"
            />
          </div>
        </TabPanel>
      </TabPanels>
    </TabGroup>

    <!-- Report Dialog -->
    <ReportDialog />
  </div>
</template>
