import api from './api'

// =============================================================================
// Request/Response Types (matching backend DTOs)
// =============================================================================

export interface DocumentResponse {
  id: string
  workspaceId: string
  dataSourceId?: string
  name: string
  type: DocumentType
  status: DocumentStatus
  size: number
  chunkCount: number
  metadata: Record<string, string>
  createdAt: string
  processedAt?: string
  uploadedBy?: string
  errorMessage?: string
}

export type DocumentType =
  | 'Pdf'
  | 'Word'
  | 'Excel'
  | 'PowerPoint'
  | 'Text'
  | 'Markdown'
  | 'Html'
  | 'Json'
  | 'Csv'
  | 'Image'
  | 'Other'

export type DocumentStatus =
  | 'Pending'
  | 'Processing'
  | 'Indexed'
  | 'Failed'
  | 'Deleted'

export interface DocumentChunk {
  id: string
  documentId: string
  chunkIndex: number
  content: string
  tokenCount: number
  embedding?: number[]
  metadata: Record<string, string>
}

export interface DocumentPreview {
  id: string
  name: string
  type: DocumentType
  preview: string
  pageCount?: number
  wordCount?: number
}

export interface UploadProgress {
  fileName: string
  progress: number
  status: 'uploading' | 'processing' | 'complete' | 'error'
  error?: string
  documentId?: string
}

export interface UploadOptions {
  onProgress?: (progress: UploadProgress) => void
  dataSourceId?: string
}

export interface DocumentSearchResult {
  documentId: string
  documentName: string
  chunkIndex: number
  content: string
  relevanceScore: number
  highlights: string[]
}

export interface PagedDocumentsResponse {
  items: DocumentResponse[]
  totalCount: number
  pageNumber: number
  pageSize: number
  totalPages: number
  hasNextPage: boolean
  hasPreviousPage: boolean
}

// =============================================================================
// Document Service
// =============================================================================

class DocumentService {
  /**
   * Upload a document to a workspace
   */
  async upload(
    workspaceId: string,
    file: File,
    options?: UploadOptions
  ): Promise<DocumentResponse> {
    const formData = new FormData()
    formData.append('file', file)

    if (options?.dataSourceId) {
      formData.append('dataSourceId', options.dataSourceId)
    }

    // Initialize progress
    const progress: UploadProgress = {
      fileName: file.name,
      progress: 0,
      status: 'uploading'
    }
    options?.onProgress?.(progress)

    try {
      const response = await api.getClient().post<DocumentResponse>(
        `/workspaces/${workspaceId}/documents/upload`,
        formData,
        {
          headers: { 'Content-Type': 'multipart/form-data' },
          onUploadProgress: (progressEvent) => {
            if (progressEvent.total) {
              progress.progress = Math.round((progressEvent.loaded * 100) / progressEvent.total)
              options?.onProgress?.(progress)
            }
          }
        }
      )

      progress.status = 'complete'
      progress.documentId = response.data.id
      options?.onProgress?.(progress)

      return response.data
    } catch (error) {
      progress.status = 'error'
      progress.error = error instanceof Error ? error.message : 'Upload failed'
      options?.onProgress?.(progress)
      throw error
    }
  }

  /**
   * Upload multiple documents
   */
  async uploadMultiple(
    workspaceId: string,
    files: File[],
    options?: {
      onProgress?: (fileName: string, progress: number, index: number) => void
      onComplete?: (fileName: string, document: DocumentResponse, index: number) => void
      onError?: (fileName: string, error: string, index: number) => void
      dataSourceId?: string
    }
  ): Promise<DocumentResponse[]> {
    const results: DocumentResponse[] = []

    for (let i = 0; i < files.length; i++) {
      const file = files[i]

      try {
        const document = await this.upload(workspaceId, file, {
          dataSourceId: options?.dataSourceId,
          onProgress: (progress) => {
            options?.onProgress?.(file.name, progress.progress, i)
          }
        })

        results.push(document)
        options?.onComplete?.(file.name, document, i)
      } catch (error) {
        const errorMessage = error instanceof Error ? error.message : 'Upload failed'
        options?.onError?.(file.name, errorMessage, i)
      }
    }

    return results
  }

  /**
   * Get documents for a workspace
   */
  async getDocuments(
    workspaceId: string,
    page = 1,
    pageSize = 20,
    status?: DocumentStatus
  ): Promise<PagedDocumentsResponse> {
    const params: Record<string, unknown> = {
      pageNumber: page,
      pageSize
    }

    if (status) {
      params.status = status
    }

    return api.get<PagedDocumentsResponse>(
      `/workspaces/${workspaceId}/documents`,
      params
    )
  }

  /**
   * Get a single document
   */
  async getDocument(workspaceId: string, documentId: string): Promise<DocumentResponse> {
    return api.get<DocumentResponse>(
      `/workspaces/${workspaceId}/documents/${documentId}`
    )
  }

  /**
   * Get document chunks
   */
  async getChunks(workspaceId: string, documentId: string): Promise<DocumentChunk[]> {
    return api.get<DocumentChunk[]>(
      `/workspaces/${workspaceId}/documents/${documentId}/chunks`
    )
  }

  /**
   * Get document preview
   */
  async getPreview(workspaceId: string, documentId: string): Promise<DocumentPreview> {
    return api.get<DocumentPreview>(
      `/workspaces/${workspaceId}/documents/${documentId}/preview`
    )
  }

  /**
   * Delete a document
   */
  async delete(workspaceId: string, documentId: string): Promise<void> {
    return api.delete<void>(
      `/workspaces/${workspaceId}/documents/${documentId}`
    )
  }

  /**
   * Download a document
   */
  async download(workspaceId: string, documentId: string, fileName: string): Promise<void> {
    try {
      const response = await api.getClient().get(
        `/workspaces/${workspaceId}/documents/${documentId}/download`,
        { responseType: 'blob' }
      )

      // Create a blob URL and trigger download
      const blob = new Blob([response.data])
      const url = window.URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = fileName
      document.body.appendChild(link)
      link.click()
      document.body.removeChild(link)
      window.URL.revokeObjectURL(url)
    } catch (error) {
      console.error('Download failed:', error)
      throw error
    }
  }

  /**
   * Reprocess a document (re-chunk and re-embed)
   */
  async reprocess(workspaceId: string, documentId: string): Promise<void> {
    return api.post<void>(
      `/workspaces/${workspaceId}/documents/${documentId}/reprocess`
    )
  }

  /**
   * Search within documents
   */
  async search(
    workspaceId: string,
    query: string,
    maxResults = 10
  ): Promise<DocumentSearchResult[]> {
    return api.get<DocumentSearchResult[]>(
      `/workspaces/${workspaceId}/documents/search`,
      { query, maxResults }
    )
  }

  /**
   * Get document processing status
   */
  async getStatus(workspaceId: string, documentId: string): Promise<{
    status: DocumentStatus
    progress?: number
    errorMessage?: string
  }> {
    const doc = await this.getDocument(workspaceId, documentId)
    return {
      status: doc.status,
      errorMessage: doc.errorMessage
    }
  }

  /**
   * Poll document status until complete or failed
   */
  async waitForProcessing(
    workspaceId: string,
    documentId: string,
    options?: {
      interval?: number
      timeout?: number
      onProgress?: (status: DocumentStatus) => void
    }
  ): Promise<DocumentResponse> {
    const interval = options?.interval || 2000
    const timeout = options?.timeout || 300000 // 5 minutes default
    const startTime = Date.now()

    while (true) {
      const document = await this.getDocument(workspaceId, documentId)
      options?.onProgress?.(document.status)

      if (document.status === 'Indexed' || document.status === 'Failed') {
        return document
      }

      if (Date.now() - startTime > timeout) {
        throw new Error('Document processing timeout')
      }

      await new Promise(resolve => setTimeout(resolve, interval))
    }
  }

  // =============================================================================
  // Utility Methods
  // =============================================================================

  /**
   * Get file type from extension
   */
  getTypeFromFile(file: File): DocumentType {
    const extension = file.name.split('.').pop()?.toLowerCase()

    const typeMap: Record<string, DocumentType> = {
      pdf: 'Pdf',
      doc: 'Word',
      docx: 'Word',
      xls: 'Excel',
      xlsx: 'Excel',
      ppt: 'PowerPoint',
      pptx: 'PowerPoint',
      txt: 'Text',
      md: 'Markdown',
      html: 'Html',
      htm: 'Html',
      json: 'Json',
      csv: 'Csv',
      png: 'Image',
      jpg: 'Image',
      jpeg: 'Image',
      gif: 'Image',
      webp: 'Image'
    }

    return typeMap[extension || ''] || 'Other'
  }

  /**
   * Format file size for display
   */
  formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 Bytes'

    const k = 1024
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB']
    const i = Math.floor(Math.log(bytes) / Math.log(k))

    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
  }

  /**
   * Validate file before upload
   */
  validateFile(file: File, options?: {
    maxSize?: number
    allowedTypes?: DocumentType[]
  }): { valid: boolean; error?: string } {
    const maxSize = options?.maxSize || 100 * 1024 * 1024 // 100MB default

    if (file.size > maxSize) {
      return {
        valid: false,
        error: `File size exceeds maximum allowed (${this.formatFileSize(maxSize)})`
      }
    }

    if (options?.allowedTypes) {
      const fileType = this.getTypeFromFile(file)
      if (!options.allowedTypes.includes(fileType)) {
        return {
          valid: false,
          error: `File type not allowed. Allowed types: ${options.allowedTypes.join(', ')}`
        }
      }
    }

    return { valid: true }
  }

  /**
   * Get status badge info for display
   */
  getStatusBadge(status: DocumentStatus): {
    label: string
    color: 'green' | 'yellow' | 'red' | 'gray' | 'blue'
  } {
    switch (status) {
      case 'Indexed':
        return { label: 'Ready', color: 'green' }
      case 'Processing':
        return { label: 'Processing', color: 'blue' }
      case 'Pending':
        return { label: 'Pending', color: 'yellow' }
      case 'Failed':
        return { label: 'Failed', color: 'red' }
      case 'Deleted':
        return { label: 'Deleted', color: 'gray' }
      default:
        return { label: status, color: 'gray' }
    }
  }
}

export const documentService = new DocumentService()
export default documentService
