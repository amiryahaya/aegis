// Export format types
export type FileFormat = 'json' | 'csv' | 'pdf' | 'markdown' | 'html' | 'xlsx'

export interface ExportFormatOption {
  value: FileFormat
  label: string
  description: string
  icon: string
  mimeType: string
  extension: string
}

export const EXPORT_FORMATS: ExportFormatOption[] = [
  {
    value: 'json',
    label: 'JSON',
    description: 'Machine-readable format for data interchange',
    icon: 'code-bracket',
    mimeType: 'application/json',
    extension: '.json'
  },
  {
    value: 'csv',
    label: 'CSV',
    description: 'Spreadsheet-compatible comma-separated values',
    icon: 'table-cells',
    mimeType: 'text/csv',
    extension: '.csv'
  },
  {
    value: 'pdf',
    label: 'PDF',
    description: 'Portable document format for printing and sharing',
    icon: 'document',
    mimeType: 'application/pdf',
    extension: '.pdf'
  },
  {
    value: 'markdown',
    label: 'Markdown',
    description: 'Plain text format with formatting syntax',
    icon: 'document-text',
    mimeType: 'text/markdown',
    extension: '.md'
  },
  {
    value: 'html',
    label: 'HTML',
    description: 'Web page format viewable in browsers',
    icon: 'globe-alt',
    mimeType: 'text/html',
    extension: '.html'
  },
  {
    value: 'xlsx',
    label: 'Excel',
    description: 'Microsoft Excel spreadsheet format',
    icon: 'table-cells',
    mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    extension: '.xlsx'
  }
]

// Export resource types
export type ExportResourceType =
  | 'session'
  | 'sessions'
  | 'workspace'
  | 'workspaces'
  | 'documents'
  | 'analytics'
  | 'audit-logs'
  | 'activity'

// Export options
export interface ExportOptions {
  format: FileFormat
  resourceType: ExportResourceType
  resourceId?: string
  resourceIds?: string[]
  includeMetadata?: boolean
  includeTimestamps?: boolean
  dateRange?: {
    start: Date
    end: Date
  }
  fields?: string[]
  filters?: Record<string, unknown>
}

// Export job status
export type ExportStatus = 'pending' | 'processing' | 'completed' | 'failed' | 'cancelled'

export interface ExportJob {
  id: string
  status: ExportStatus
  resourceType: ExportResourceType
  format: FileFormat
  fileName: string
  fileSize?: number
  progress: number
  createdAt: string
  completedAt?: string
  expiresAt?: string
  downloadUrl?: string
  error?: string
  options: ExportOptions
}

// Export history
export interface ExportHistoryItem {
  id: string
  resourceType: ExportResourceType
  format: FileFormat
  fileName: string
  fileSize: number
  status: ExportStatus
  createdAt: string
  completedAt?: string
  expiresAt: string
  downloadUrl?: string
  downloadCount: number
}

// Report types
export type ReportType =
  | 'usage-summary'
  | 'query-analytics'
  | 'session-analytics'
  | 'document-analytics'
  | 'performance-report'
  | 'feedback-report'
  | 'audit-report'
  | 'workspace-report'

export interface ReportTemplate {
  id: ReportType
  name: string
  description: string
  category: 'analytics' | 'compliance' | 'operations'
  formats: FileFormat[]
  requiredFilters: string[]
  optionalFilters: string[]
}

export const REPORT_TEMPLATES: ReportTemplate[] = [
  {
    id: 'usage-summary',
    name: 'Usage Summary',
    description: 'Overview of system usage including queries, sessions, and active users',
    category: 'analytics',
    formats: ['pdf', 'xlsx', 'csv'],
    requiredFilters: ['dateRange'],
    optionalFilters: ['workspace', 'user']
  },
  {
    id: 'query-analytics',
    name: 'Query Analytics',
    description: 'Detailed analysis of query patterns, response times, and success rates',
    category: 'analytics',
    formats: ['pdf', 'xlsx', 'csv', 'json'],
    requiredFilters: ['dateRange'],
    optionalFilters: ['workspace', 'queryType']
  },
  {
    id: 'session-analytics',
    name: 'Session Analytics',
    description: 'Session duration, completion rates, and user engagement metrics',
    category: 'analytics',
    formats: ['pdf', 'xlsx', 'csv'],
    requiredFilters: ['dateRange'],
    optionalFilters: ['workspace', 'sessionType']
  },
  {
    id: 'document-analytics',
    name: 'Document Analytics',
    description: 'Document ingestion, retrieval rates, and storage metrics',
    category: 'operations',
    formats: ['pdf', 'xlsx', 'csv'],
    requiredFilters: ['dateRange'],
    optionalFilters: ['workspace', 'documentType']
  },
  {
    id: 'performance-report',
    name: 'Performance Report',
    description: 'System performance metrics including latency, throughput, and error rates',
    category: 'operations',
    formats: ['pdf', 'xlsx'],
    requiredFilters: ['dateRange'],
    optionalFilters: []
  },
  {
    id: 'feedback-report',
    name: 'Feedback Report',
    description: 'User feedback analysis with sentiment trends and improvement suggestions',
    category: 'analytics',
    formats: ['pdf', 'xlsx', 'csv'],
    requiredFilters: ['dateRange'],
    optionalFilters: ['workspace', 'rating']
  },
  {
    id: 'audit-report',
    name: 'Audit Report',
    description: 'Security and compliance audit trail for regulatory requirements',
    category: 'compliance',
    formats: ['pdf', 'xlsx', 'csv', 'json'],
    requiredFilters: ['dateRange'],
    optionalFilters: ['user', 'action', 'resource']
  },
  {
    id: 'workspace-report',
    name: 'Workspace Report',
    description: 'Workspace-specific metrics, documents, and user activity',
    category: 'operations',
    formats: ['pdf', 'xlsx'],
    requiredFilters: ['workspace', 'dateRange'],
    optionalFilters: []
  }
]

// Scheduled report
export type ScheduleFrequency = 'daily' | 'weekly' | 'monthly' | 'quarterly'

export interface ScheduledReport {
  id: string
  name: string
  reportType: ReportType
  format: FileFormat
  frequency: ScheduleFrequency
  dayOfWeek?: number // 0-6 for weekly
  dayOfMonth?: number // 1-31 for monthly
  time: string // HH:mm format
  timezone: string
  recipients: string[]
  enabled: boolean
  lastRunAt?: string
  nextRunAt: string
  filters: Record<string, unknown>
  createdAt: string
  updatedAt: string
}

export interface CreateScheduledReportRequest {
  name: string
  reportType: ReportType
  format: FileFormat
  frequency: ScheduleFrequency
  dayOfWeek?: number
  dayOfMonth?: number
  time: string
  timezone: string
  recipients: string[]
  filters: Record<string, unknown>
}

// Export dialog state
export interface ExportDialogState {
  isOpen: boolean
  resourceType: ExportResourceType
  resourceId?: string
  resourceIds?: string[]
  resourceName?: string
  step: 'format' | 'options' | 'progress' | 'complete'
  selectedFormat: FileFormat | null
  options: Partial<ExportOptions>
  job: ExportJob | null
}

// Report dialog state
export interface ReportDialogState {
  isOpen: boolean
  step: 'template' | 'filters' | 'schedule' | 'progress' | 'complete'
  selectedTemplate: ReportTemplate | null
  filters: Record<string, unknown>
  isScheduled: boolean
  scheduleOptions: Partial<CreateScheduledReportRequest>
  job: ExportJob | null
}

// Utility functions
export function getExportFormatByValue(format: FileFormat): ExportFormatOption | undefined {
  return EXPORT_FORMATS.find(f => f.value === format)
}

export function getReportTemplateById(id: ReportType): ReportTemplate | undefined {
  return REPORT_TEMPLATES.find(t => t.id === id)
}

export function formatFileSize(bytes: number): string {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(1))} ${sizes[i]}`
}

export function generateExportFileName(
  resourceType: ExportResourceType,
  format: FileFormat,
  resourceName?: string
): string {
  const timestamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, 19)
  const name = resourceName ? resourceName.replace(/[^a-zA-Z0-9]/g, '-').toLowerCase() : resourceType
  const formatOption = getExportFormatByValue(format)
  return `${name}-${timestamp}${formatOption?.extension || ''}`
}
