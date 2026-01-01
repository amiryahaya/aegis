// Re-export all types
export * from './user'
export * from './session'
export * from './api'

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
