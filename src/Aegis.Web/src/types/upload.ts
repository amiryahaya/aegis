// Upload Types

export type UploadStatus = 'pending' | 'uploading' | 'processing' | 'completed' | 'error'

export interface UploadFile {
  id: string
  file: File
  name: string
  size: number
  type: string
  status: UploadStatus
  progress: number
  error?: string
  uploadedAt?: string
  documentId?: string // ID returned from server after successful upload
}

export interface UploadOptions {
  url: string
  headers?: Record<string, string>
  fieldName?: string
  additionalData?: Record<string, string>
  onProgress?: (fileId: string, progress: number) => void
  onComplete?: (fileId: string, response: unknown) => void
  onError?: (fileId: string, error: string) => void
}

export interface UploadResult {
  success: boolean
  fileId: string
  documentId?: string
  error?: string
}

export interface BulkUploadResult {
  total: number
  successful: number
  failed: number
  results: UploadResult[]
}

// Bulk selection types
export interface SelectableItem {
  id: string
  selected: boolean
}

export interface BulkSelectionState {
  selectedIds: Set<string>
  allSelected: boolean
  someSelected: boolean
  count: number
}

export interface BulkAction {
  id: string
  label: string
  icon?: string
  variant?: 'default' | 'danger' | 'warning'
  requiresConfirmation?: boolean
  confirmationMessage?: string
}
