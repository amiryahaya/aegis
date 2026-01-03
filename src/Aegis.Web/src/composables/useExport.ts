import { ref, computed } from 'vue'
import type {
  FileFormat,
  ExportOptions,
  ExportJob,
  ExportResourceType,
  ExportHistoryItem,
  ExportDialogState,
  ScheduledReport,
  CreateScheduledReportRequest,
  ReportDialogState,
  ReportTemplate
} from '@/types/export'
import {
  generateExportFileName,
  EXPORT_FORMATS,
  REPORT_TEMPLATES
} from '@/types/export'

// Export service composable
export function useExport() {
  const isExporting = ref(false)
  const exportProgress = ref(0)
  const currentJob = ref<ExportJob | null>(null)
  const exportHistory = ref<ExportHistoryItem[]>([])
  const error = ref<string | null>(null)

  // Start an export job
  async function startExport(options: ExportOptions): Promise<ExportJob> {
    isExporting.value = true
    exportProgress.value = 0
    error.value = null

    const job: ExportJob = {
      id: `export-${Date.now()}`,
      status: 'pending',
      resourceType: options.resourceType,
      format: options.format,
      fileName: generateExportFileName(options.resourceType, options.format),
      progress: 0,
      createdAt: new Date().toISOString(),
      options
    }

    currentJob.value = job

    try {
      // Simulate export processing
      job.status = 'processing'

      // Simulate progress updates
      for (let i = 0; i <= 100; i += 10) {
        await new Promise(resolve => setTimeout(resolve, 200))
        exportProgress.value = i
        job.progress = i
      }

      // Generate mock file content based on format
      const content = await generateExportContent(options)
      const blob = new Blob([content], { type: getMimeType(options.format) })

      job.status = 'completed'
      job.completedAt = new Date().toISOString()
      job.fileSize = blob.size
      job.downloadUrl = URL.createObjectURL(blob)
      job.expiresAt = new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString() // 24 hours

      // Add to history
      exportHistory.value.unshift({
        id: job.id,
        resourceType: job.resourceType,
        format: job.format,
        fileName: job.fileName,
        fileSize: job.fileSize,
        status: job.status,
        createdAt: job.createdAt,
        completedAt: job.completedAt,
        expiresAt: job.expiresAt,
        downloadUrl: job.downloadUrl,
        downloadCount: 0
      })

      return job
    } catch (e) {
      job.status = 'failed'
      job.error = e instanceof Error ? e.message : 'Export failed'
      error.value = job.error
      throw e
    } finally {
      isExporting.value = false
    }
  }

  // Download an export
  function downloadExport(job: ExportJob) {
    if (!job.downloadUrl) return

    const link = document.createElement('a')
    link.href = job.downloadUrl
    link.download = job.fileName
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)

    // Update download count in history
    const historyItem = exportHistory.value.find(h => h.id === job.id)
    if (historyItem) {
      historyItem.downloadCount++
    }
  }

  // Cancel an export
  function cancelExport() {
    if (currentJob.value && currentJob.value.status === 'processing') {
      currentJob.value.status = 'cancelled'
    }
    isExporting.value = false
    exportProgress.value = 0
  }

  // Clear export history
  function clearHistory() {
    // Revoke object URLs to free memory
    exportHistory.value.forEach(item => {
      if (item.downloadUrl) {
        URL.revokeObjectURL(item.downloadUrl)
      }
    })
    exportHistory.value = []
  }

  // Delete a history item
  function deleteHistoryItem(id: string) {
    const index = exportHistory.value.findIndex(h => h.id === id)
    if (index !== -1) {
      const item = exportHistory.value[index]
      if (item.downloadUrl) {
        URL.revokeObjectURL(item.downloadUrl)
      }
      exportHistory.value.splice(index, 1)
    }
  }

  return {
    isExporting,
    exportProgress,
    currentJob,
    exportHistory,
    error,
    startExport,
    downloadExport,
    cancelExport,
    clearHistory,
    deleteHistoryItem
  }
}

// Export dialog composable
export function useExportDialog() {
  const state = ref<ExportDialogState>({
    isOpen: false,
    resourceType: 'session',
    step: 'format',
    selectedFormat: null,
    options: {},
    job: null
  })

  function openDialog(
    resourceType: ExportResourceType,
    resourceId?: string,
    resourceIds?: string[],
    resourceName?: string
  ) {
    state.value = {
      isOpen: true,
      resourceType,
      resourceId,
      resourceIds,
      resourceName,
      step: 'format',
      selectedFormat: null,
      options: {
        resourceType,
        resourceId,
        resourceIds,
        includeMetadata: true,
        includeTimestamps: true
      },
      job: null
    }
  }

  function closeDialog() {
    state.value.isOpen = false
  }

  function selectFormat(format: FileFormat) {
    state.value.selectedFormat = format
    state.value.options.format = format
  }

  function nextStep() {
    switch (state.value.step) {
      case 'format':
        state.value.step = 'options'
        break
      case 'options':
        state.value.step = 'progress'
        break
      case 'progress':
        state.value.step = 'complete'
        break
    }
  }

  function previousStep() {
    switch (state.value.step) {
      case 'options':
        state.value.step = 'format'
        break
      case 'progress':
        state.value.step = 'options'
        break
      case 'complete':
        state.value.step = 'progress'
        break
    }
  }

  function setJob(job: ExportJob) {
    state.value.job = job
  }

  function updateOptions(options: Partial<ExportOptions>) {
    state.value.options = { ...state.value.options, ...options }
  }

  const availableFormats = computed(() => {
    // Filter formats based on resource type
    const resourceType = state.value.resourceType
    if (resourceType === 'analytics' || resourceType === 'audit-logs') {
      return EXPORT_FORMATS.filter(f => ['pdf', 'xlsx', 'csv', 'json'].includes(f.value))
    }
    if (resourceType === 'session' || resourceType === 'sessions') {
      return EXPORT_FORMATS
    }
    return EXPORT_FORMATS.filter(f => ['json', 'csv', 'xlsx'].includes(f.value))
  })

  return {
    state,
    openDialog,
    closeDialog,
    selectFormat,
    nextStep,
    previousStep,
    setJob,
    updateOptions,
    availableFormats
  }
}

// Report dialog composable
export function useReportDialog() {
  const state = ref<ReportDialogState>({
    isOpen: false,
    step: 'template',
    selectedTemplate: null,
    filters: {},
    isScheduled: false,
    scheduleOptions: {},
    job: null
  })

  const scheduledReports = ref<ScheduledReport[]>([])

  function openDialog() {
    state.value = {
      isOpen: true,
      step: 'template',
      selectedTemplate: null,
      filters: {},
      isScheduled: false,
      scheduleOptions: {},
      job: null
    }
  }

  function closeDialog() {
    state.value.isOpen = false
  }

  function selectTemplate(template: ReportTemplate) {
    state.value.selectedTemplate = template
    // Initialize required filters
    state.value.filters = {}
    template.requiredFilters.forEach((filter: string) => {
      if (filter === 'dateRange') {
        const end = new Date()
        const start = new Date()
        start.setDate(start.getDate() - 30)
        state.value.filters.dateRange = { start, end }
      }
    })
  }

  function nextStep() {
    switch (state.value.step) {
      case 'template':
        state.value.step = 'filters'
        break
      case 'filters':
        state.value.step = state.value.isScheduled ? 'schedule' : 'progress'
        break
      case 'schedule':
        state.value.step = 'progress'
        break
      case 'progress':
        state.value.step = 'complete'
        break
    }
  }

  function previousStep() {
    switch (state.value.step) {
      case 'filters':
        state.value.step = 'template'
        break
      case 'schedule':
        state.value.step = 'filters'
        break
      case 'progress':
        state.value.step = state.value.isScheduled ? 'schedule' : 'filters'
        break
    }
  }

  function setJob(job: ExportJob) {
    state.value.job = job
  }

  function updateFilters(filters: Record<string, unknown>) {
    state.value.filters = { ...state.value.filters, ...filters }
  }

  function toggleScheduled(value: boolean) {
    state.value.isScheduled = value
  }

  function updateScheduleOptions(options: Partial<CreateScheduledReportRequest>) {
    state.value.scheduleOptions = { ...state.value.scheduleOptions, ...options }
  }

  // Create a scheduled report
  async function createScheduledReport(request: CreateScheduledReportRequest): Promise<ScheduledReport> {
    const report: ScheduledReport = {
      id: `scheduled-${Date.now()}`,
      ...request,
      enabled: true,
      nextRunAt: calculateNextRunTime(request),
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString()
    }

    scheduledReports.value.push(report)
    return report
  }

  // Delete a scheduled report
  function deleteScheduledReport(id: string) {
    const index = scheduledReports.value.findIndex(r => r.id === id)
    if (index !== -1) {
      scheduledReports.value.splice(index, 1)
    }
  }

  // Toggle scheduled report enabled state
  function toggleScheduledReport(id: string) {
    const report = scheduledReports.value.find(r => r.id === id)
    if (report) {
      report.enabled = !report.enabled
      report.updatedAt = new Date().toISOString()
    }
  }

  const templatesByCategory = computed(() => {
    const categories = {
      analytics: REPORT_TEMPLATES.filter(t => t.category === 'analytics'),
      compliance: REPORT_TEMPLATES.filter(t => t.category === 'compliance'),
      operations: REPORT_TEMPLATES.filter(t => t.category === 'operations')
    }
    return categories
  })

  return {
    state,
    scheduledReports,
    openDialog,
    closeDialog,
    selectTemplate,
    nextStep,
    previousStep,
    setJob,
    updateFilters,
    toggleScheduled,
    updateScheduleOptions,
    createScheduledReport,
    deleteScheduledReport,
    toggleScheduledReport,
    templatesByCategory
  }
}

// Helper functions
function getMimeType(format: FileFormat): string {
  const formatOption = EXPORT_FORMATS.find(f => f.value === format)
  return formatOption?.mimeType || 'application/octet-stream'
}

async function generateExportContent(options: ExportOptions): Promise<string> {
  // Mock export content generation
  const mockData = generateMockExportData(options)

  switch (options.format) {
    case 'json':
      return JSON.stringify(mockData, null, 2)
    case 'csv':
      return convertToCSV(mockData)
    case 'markdown':
      return convertToMarkdown(mockData, options)
    case 'html':
      return convertToHTML(mockData, options)
    default:
      return JSON.stringify(mockData, null, 2)
  }
}

function generateMockExportData(options: ExportOptions): Record<string, unknown> {
  const metadata = options.includeMetadata ? {
    exportedAt: new Date().toISOString(),
    resourceType: options.resourceType,
    format: options.format
  } : {}

  switch (options.resourceType) {
    case 'session':
    case 'sessions':
      return {
        ...metadata,
        sessions: [
          {
            id: 'session-1',
            title: 'Research on AI trends',
            type: 'research',
            status: 'completed',
            turns: 5,
            createdAt: '2026-01-01T10:00:00Z',
            lastActivityAt: '2026-01-01T10:30:00Z'
          },
          {
            id: 'session-2',
            title: 'Quick product query',
            type: 'quick_query',
            status: 'completed',
            turns: 2,
            createdAt: '2026-01-02T14:00:00Z',
            lastActivityAt: '2026-01-02T14:05:00Z'
          }
        ]
      }
    case 'analytics':
      return {
        ...metadata,
        summary: {
          totalQueries: 12847,
          totalSessions: 3421,
          averageResponseTime: 1.24,
          successRate: 98.7
        },
        queryTrends: [
          { date: '2026-01-01', count: 423 },
          { date: '2026-01-02', count: 512 },
          { date: '2026-01-03', count: 387 }
        ]
      }
    default:
      return {
        ...metadata,
        data: []
      }
  }
}

function convertToCSV(data: Record<string, unknown>): string {
  // Simple CSV conversion
  const rows: string[] = []

  const arrayData = Object.values(data).find(v => Array.isArray(v)) as Record<string, unknown>[] | undefined
  if (arrayData && arrayData.length > 0) {
    // Header row
    const headers = Object.keys(arrayData[0])
    rows.push(headers.join(','))

    // Data rows
    arrayData.forEach(item => {
      const values = headers.map(h => {
        const val = item[h]
        if (typeof val === 'string' && val.includes(',')) {
          return `"${val}"`
        }
        return String(val ?? '')
      })
      rows.push(values.join(','))
    })
  }

  return rows.join('\n')
}

function convertToMarkdown(data: Record<string, unknown>, options: ExportOptions): string {
  const lines: string[] = []

  lines.push(`# ${options.resourceType.charAt(0).toUpperCase() + options.resourceType.slice(1)} Export`)
  lines.push('')
  lines.push(`Exported on: ${new Date().toLocaleString()}`)
  lines.push('')

  const arrayData = Object.values(data).find(v => Array.isArray(v)) as Record<string, unknown>[] | undefined
  if (arrayData && arrayData.length > 0) {
    const headers = Object.keys(arrayData[0])

    // Table header
    lines.push('| ' + headers.join(' | ') + ' |')
    lines.push('| ' + headers.map(() => '---').join(' | ') + ' |')

    // Table rows
    arrayData.forEach(item => {
      const values = headers.map(h => String(item[h] ?? ''))
      lines.push('| ' + values.join(' | ') + ' |')
    })
  }

  return lines.join('\n')
}

function convertToHTML(data: Record<string, unknown>, options: ExportOptions): string {
  const title = `${options.resourceType.charAt(0).toUpperCase() + options.resourceType.slice(1)} Export`

  let tableHtml = ''
  const arrayData = Object.values(data).find(v => Array.isArray(v)) as Record<string, unknown>[] | undefined
  if (arrayData && arrayData.length > 0) {
    const headers = Object.keys(arrayData[0])

    tableHtml = `
      <table>
        <thead>
          <tr>${headers.map(h => `<th>${h}</th>`).join('')}</tr>
        </thead>
        <tbody>
          ${arrayData.map(item => `
            <tr>${headers.map(h => `<td>${item[h] ?? ''}</td>`).join('')}</tr>
          `).join('')}
        </tbody>
      </table>
    `
  }

  return `
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>${title}</title>
  <style>
    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; padding: 2rem; }
    h1 { color: #1f2937; }
    table { border-collapse: collapse; width: 100%; margin-top: 1rem; }
    th, td { border: 1px solid #e5e7eb; padding: 0.75rem; text-align: left; }
    th { background-color: #f9fafb; font-weight: 600; }
    tr:hover { background-color: #f9fafb; }
  </style>
</head>
<body>
  <h1>${title}</h1>
  <p>Exported on: ${new Date().toLocaleString()}</p>
  ${tableHtml}
</body>
</html>
  `.trim()
}

function calculateNextRunTime(request: CreateScheduledReportRequest): string {
  const now = new Date()
  const [hours, minutes] = request.time.split(':').map(Number)

  const next = new Date(now)
  next.setHours(hours, minutes, 0, 0)

  if (next <= now) {
    switch (request.frequency) {
      case 'daily':
        next.setDate(next.getDate() + 1)
        break
      case 'weekly':
        next.setDate(next.getDate() + 7)
        break
      case 'monthly':
        next.setMonth(next.getMonth() + 1)
        break
      case 'quarterly':
        next.setMonth(next.getMonth() + 3)
        break
    }
  }

  return next.toISOString()
}
