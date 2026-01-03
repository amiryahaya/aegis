import { ref, computed, readonly } from 'vue'
import documentService from '@/services/document.service'
import type {
  DocumentResponse,
  DocumentStatus,
  DocumentType,
  DocumentChunk,
  DocumentPreview,
  UploadProgress,
  DocumentSearchResult
} from '@/services/document.service'

export interface DocumentsState {
  documents: DocumentResponse[]
  isLoading: boolean
  error: string | null
  totalCount: number
  currentPage: number
  pageSize: number
}

export interface UploadState {
  isUploading: boolean
  uploads: Map<string, UploadProgress>
  completedUploads: DocumentResponse[]
  failedUploads: Array<{ fileName: string; error: string }>
}

export interface UseDocumentsOptions {
  onUploadProgress?: (progress: UploadProgress) => void
  onUploadComplete?: (document: DocumentResponse) => void
  onUploadError?: (fileName: string, error: string) => void
  onProcessingComplete?: (document: DocumentResponse) => void
}

export function useDocuments(workspaceId: string, options: UseDocumentsOptions = {}) {
  // State
  const documents = ref<DocumentResponse[]>([])
  const isLoading = ref(false)
  const error = ref<string | null>(null)
  const totalCount = ref(0)
  const currentPage = ref(1)
  const pageSize = ref(20)

  // Upload state
  const isUploading = ref(false)
  const uploads = ref<Map<string, UploadProgress>>(new Map())
  const completedUploads = ref<DocumentResponse[]>([])
  const failedUploads = ref<Array<{ fileName: string; error: string }>>([])

  // Selected document state
  const selectedDocument = ref<DocumentResponse | null>(null)
  const documentChunks = ref<DocumentChunk[]>([])
  const documentPreview = ref<DocumentPreview | null>(null)

  // Search state
  const searchResults = ref<DocumentSearchResult[]>([])
  const isSearching = ref(false)

  // Computed
  const hasDocuments = computed(() => documents.value.length > 0)
  const totalPages = computed(() => Math.ceil(totalCount.value / pageSize.value))
  const hasNextPage = computed(() => currentPage.value < totalPages.value)
  const hasPreviousPage = computed(() => currentPage.value > 1)

  const pendingDocuments = computed(() =>
    documents.value.filter(d => d.status === 'Pending' || d.status === 'Processing')
  )
  const indexedDocuments = computed(() =>
    documents.value.filter(d => d.status === 'Indexed')
  )
  const failedDocuments = computed(() =>
    documents.value.filter(d => d.status === 'Failed')
  )

  const uploadProgress = computed(() => {
    const progressArray = Array.from(uploads.value.values())
    if (progressArray.length === 0) return 0
    const totalProgress = progressArray.reduce((sum, p) => sum + p.progress, 0)
    return Math.round(totalProgress / progressArray.length)
  })

  const documentsState = computed<DocumentsState>(() => ({
    documents: documents.value,
    isLoading: isLoading.value,
    error: error.value,
    totalCount: totalCount.value,
    currentPage: currentPage.value,
    pageSize: pageSize.value
  }))

  const uploadState = computed<UploadState>(() => ({
    isUploading: isUploading.value,
    uploads: uploads.value,
    completedUploads: completedUploads.value,
    failedUploads: failedUploads.value
  }))

  // Actions - Fetch
  async function fetchDocuments(page = 1, size = 20): Promise<void> {
    isLoading.value = true
    error.value = null
    try {
      const response = await documentService.getDocuments(workspaceId, page, size)
      documents.value = response.items
      totalCount.value = response.totalCount
      currentPage.value = response.pageNumber
      pageSize.value = response.pageSize
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch documents'
    } finally {
      isLoading.value = false
    }
  }

  async function fetchDocument(documentId: string): Promise<DocumentResponse | null> {
    isLoading.value = true
    error.value = null
    try {
      const document = await documentService.getDocument(workspaceId, documentId)
      selectedDocument.value = document
      return document
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch document'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function fetchChunks(documentId: string): Promise<DocumentChunk[]> {
    try {
      const chunks = await documentService.getChunks(workspaceId, documentId)
      documentChunks.value = chunks
      return chunks
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch chunks'
      return []
    }
  }

  async function fetchPreview(documentId: string): Promise<DocumentPreview | null> {
    try {
      const preview = await documentService.getPreview(workspaceId, documentId)
      documentPreview.value = preview
      return preview
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch preview'
      return null
    }
  }

  // Actions - Upload
  async function upload(file: File): Promise<DocumentResponse | null> {
    isUploading.value = true
    error.value = null

    try {
      const document = await documentService.upload(workspaceId, file, {
        onProgress: (progress) => {
          uploads.value.set(file.name, progress)
          options.onUploadProgress?.(progress)
        }
      })

      uploads.value.delete(file.name)
      completedUploads.value.push(document)
      documents.value.unshift(document)
      options.onUploadComplete?.(document)
      return document
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Upload failed'
      uploads.value.delete(file.name)
      failedUploads.value.push({ fileName: file.name, error: errorMessage })
      options.onUploadError?.(file.name, errorMessage)
      return null
    } finally {
      isUploading.value = uploads.value.size > 0
    }
  }

  async function uploadMultiple(files: File[]): Promise<DocumentResponse[]> {
    isUploading.value = true
    completedUploads.value = []
    failedUploads.value = []

    const results = await documentService.uploadMultiple(workspaceId, files, {
      onProgress: (fileName, progress) => {
        const existing = uploads.value.get(fileName)
        if (existing) {
          existing.progress = progress
        } else {
          uploads.value.set(fileName, {
            fileName,
            progress,
            status: 'uploading'
          })
        }
      },
      onComplete: (fileName, document) => {
        uploads.value.delete(fileName)
        completedUploads.value.push(document)
        documents.value.unshift(document)
        options.onUploadComplete?.(document)
      },
      onError: (fileName, errorMessage) => {
        uploads.value.delete(fileName)
        failedUploads.value.push({ fileName, error: errorMessage })
        options.onUploadError?.(fileName, errorMessage)
      }
    })

    isUploading.value = false
    return results
  }

  // Actions - Document Operations
  async function deleteDocument(documentId: string): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await documentService.delete(workspaceId, documentId)
      documents.value = documents.value.filter(d => d.id !== documentId)
      if (selectedDocument.value?.id === documentId) {
        selectedDocument.value = null
      }
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to delete document'
      return false
    } finally {
      isLoading.value = false
    }
  }

  async function reprocessDocument(documentId: string): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await documentService.reprocess(workspaceId, documentId)
      // Update status locally
      const doc = documents.value.find(d => d.id === documentId)
      if (doc) {
        doc.status = 'Processing'
      }
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to reprocess document'
      return false
    } finally {
      isLoading.value = false
    }
  }

  async function waitForProcessing(
    documentId: string,
    interval = 2000,
    timeout = 300000
  ): Promise<DocumentResponse | null> {
    try {
      const document = await documentService.waitForProcessing(workspaceId, documentId, {
        interval,
        timeout,
        onProgress: (status) => {
          const doc = documents.value.find(d => d.id === documentId)
          if (doc) {
            doc.status = status
          }
        }
      })

      // Update in documents list
      const index = documents.value.findIndex(d => d.id === documentId)
      if (index !== -1) {
        documents.value[index] = document
      }

      if (document.status === 'Indexed') {
        options.onProcessingComplete?.(document)
      }

      return document
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Processing timeout'
      return null
    }
  }

  // Actions - Search
  async function search(query: string, maxResults = 10): Promise<DocumentSearchResult[]> {
    isSearching.value = true
    error.value = null
    try {
      const results = await documentService.search(workspaceId, query, maxResults)
      searchResults.value = results
      return results
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Search failed'
      return []
    } finally {
      isSearching.value = false
    }
  }

  // Actions - Pagination
  function nextPage(): void {
    if (hasNextPage.value) {
      fetchDocuments(currentPage.value + 1, pageSize.value)
    }
  }

  function previousPage(): void {
    if (hasPreviousPage.value) {
      fetchDocuments(currentPage.value - 1, pageSize.value)
    }
  }

  function goToPage(page: number): void {
    if (page >= 1 && page <= totalPages.value) {
      fetchDocuments(page, pageSize.value)
    }
  }

  // Actions - Utilities
  function validateFile(file: File, maxSize?: number, allowedTypes?: DocumentType[]): {
    valid: boolean
    error?: string
  } {
    return documentService.validateFile(file, { maxSize, allowedTypes })
  }

  function getFileType(file: File): DocumentType {
    return documentService.getTypeFromFile(file)
  }

  function formatFileSize(bytes: number): string {
    return documentService.formatFileSize(bytes)
  }

  function getStatusBadge(status: DocumentStatus): { label: string; color: string } {
    return documentService.getStatusBadge(status)
  }

  // Actions - Reset
  function reset(): void {
    documents.value = []
    error.value = null
    totalCount.value = 0
    currentPage.value = 1
    selectedDocument.value = null
    documentChunks.value = []
    documentPreview.value = null
    searchResults.value = []
  }

  function clearUploads(): void {
    uploads.value.clear()
    completedUploads.value = []
    failedUploads.value = []
    isUploading.value = false
  }

  function selectDocument(document: DocumentResponse | null): void {
    selectedDocument.value = document
    documentChunks.value = []
    documentPreview.value = null
  }

  return {
    // State (readonly)
    documents: readonly(documents),
    isLoading: readonly(isLoading),
    error: readonly(error),
    totalCount: readonly(totalCount),
    currentPage: readonly(currentPage),
    pageSize: readonly(pageSize),
    isUploading: readonly(isUploading),
    uploads: readonly(uploads),
    completedUploads: readonly(completedUploads),
    failedUploads: readonly(failedUploads),
    selectedDocument: readonly(selectedDocument),
    documentChunks: readonly(documentChunks),
    documentPreview: readonly(documentPreview),
    searchResults: readonly(searchResults),
    isSearching: readonly(isSearching),

    // Computed
    hasDocuments,
    totalPages,
    hasNextPage,
    hasPreviousPage,
    pendingDocuments,
    indexedDocuments,
    failedDocuments,
    uploadProgress,
    documentsState,
    uploadState,

    // Actions - Fetch
    fetchDocuments,
    fetchDocument,
    fetchChunks,
    fetchPreview,

    // Actions - Upload
    upload,
    uploadMultiple,

    // Actions - Operations
    deleteDocument,
    reprocessDocument,
    waitForProcessing,

    // Actions - Search
    search,

    // Actions - Pagination
    nextPage,
    previousPage,
    goToPage,

    // Actions - Utilities
    validateFile,
    getFileType,
    formatFileSize,
    getStatusBadge,

    // Actions - Reset
    reset,
    clearUploads,
    selectDocument
  }
}

export type DocumentsComposable = ReturnType<typeof useDocuments>
