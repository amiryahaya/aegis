// Re-export all types
export * from './user'
export * from './session'
export * from './api'
export * from './notification'
export * from './admin'
export * from './search'
export * from './profile'
export * from './activity'
export * from './comments'
export * from './presence'
export * from './upload'
export * from './command'
export * from './onboarding'
export * from './connection'
export * from './offline'
export * from './performance'
export * from './analytics'
export * from './export'
export * from './webhook'
export * from './audit'

// Re-export workspace types with explicit names to avoid conflicts
export type {
  WorkspaceSettings,
  WorkspaceStats,
  CreateWorkspaceRequest,
  UpdateWorkspaceRequest,
  DataSource,
  DataSourceType,
  DataSourceStatus,
  DataSourceConfig,
  CreateDataSourceRequest,
  Document,
  DocumentType,
  DocumentStatus,
  WorkspaceShare,
  WorkspaceRole,
  ShareableLink,
  CreateShareableLinkRequest,
  SearchMode
} from './workspace'

// Common types
export interface PagedResponse<T> {
  items: T[]
  totalCount: number
  pageNumber: number
  pageSize: number
  totalPages: number
  hasNextPage: boolean
  hasPreviousPage: boolean
}

export interface ApiError {
  type: string
  title: string
  status: number
  detail?: string
  errors?: Record<string, string[]>
}

export type Result<T> = { isSuccess: true; value: T } | { isSuccess: false; error: ApiError }
