import { ref, computed } from 'vue'
import type { UploadFile, UploadStatus, UploadResult, BulkUploadResult } from '@/types/upload'
import { useAuthStore } from '@/stores/auth'

interface UseFileUploadOptions {
  maxConcurrent?: number
  autoUpload?: boolean
  url?: string
}

export function useFileUpload(options: UseFileUploadOptions = {}) {
  const { maxConcurrent = 3, autoUpload = false, url = '/api/documents/upload' } = options

  const authStore = useAuthStore()
  const files = ref<UploadFile[]>([])
  const uploadQueue = ref<string[]>([])
  const activeUploads = ref<Set<string>>(new Set())
  const abortControllers = ref<Map<string, AbortController>>(new Map())

  // Computed
  const isUploading = computed(() => activeUploads.value.size > 0)

  const pendingCount = computed(() =>
    files.value.filter(f => f.status === 'pending').length
  )

  const uploadingCount = computed(() =>
    files.value.filter(f => f.status === 'uploading').length
  )

  const completedCount = computed(() =>
    files.value.filter(f => f.status === 'completed').length
  )

  const errorCount = computed(() =>
    files.value.filter(f => f.status === 'error').length
  )

  const overallProgress = computed(() => {
    if (files.value.length === 0) return 0
    const total = files.value.reduce((sum, f) => sum + f.progress, 0)
    return Math.round(total / files.value.length)
  })

  // Add files to the queue
  function addFiles(newFiles: File[]): UploadFile[] {
    const uploadFiles: UploadFile[] = newFiles.map(file => ({
      id: crypto.randomUUID(),
      file,
      name: file.name,
      size: file.size,
      type: file.type,
      status: 'pending' as UploadStatus,
      progress: 0
    }))

    files.value.push(...uploadFiles)

    if (autoUpload) {
      uploadFiles.forEach(f => uploadQueue.value.push(f.id))
      processQueue()
    }

    return uploadFiles
  }

  // Start upload for specific file
  function startUpload(fileId: string): void {
    if (!uploadQueue.value.includes(fileId)) {
      uploadQueue.value.push(fileId)
    }
    processQueue()
  }

  // Start all pending uploads
  function startAllUploads(): void {
    const pendingFiles = files.value.filter(f => f.status === 'pending')
    pendingFiles.forEach(f => {
      if (!uploadQueue.value.includes(f.id)) {
        uploadQueue.value.push(f.id)
      }
    })
    processQueue()
  }

  // Process upload queue
  async function processQueue(): Promise<void> {
    while (uploadQueue.value.length > 0 && activeUploads.value.size < maxConcurrent) {
      const fileId = uploadQueue.value.shift()
      if (fileId) {
        uploadFile(fileId)
      }
    }
  }

  // Upload single file
  async function uploadFile(fileId: string): Promise<UploadResult> {
    const file = files.value.find(f => f.id === fileId)
    if (!file) {
      return { success: false, fileId, error: 'File not found' }
    }

    activeUploads.value.add(fileId)
    updateFileStatus(fileId, 'uploading', 0)

    const abortController = new AbortController()
    abortControllers.value.set(fileId, abortController)

    try {
      const formData = new FormData()
      formData.append('file', file.file)

      const xhr = new XMLHttpRequest()

      const uploadPromise = new Promise<UploadResult>((resolve) => {
        xhr.upload.onprogress = (event) => {
          if (event.lengthComputable) {
            const progress = Math.round((event.loaded / event.total) * 100)
            updateFileStatus(fileId, 'uploading', progress)
          }
        }

        xhr.onload = () => {
          if (xhr.status >= 200 && xhr.status < 300) {
            try {
              const response = JSON.parse(xhr.responseText)
              updateFileStatus(fileId, 'completed', 100, undefined, response.id)
              resolve({
                success: true,
                fileId,
                documentId: response.id
              })
            } catch {
              updateFileStatus(fileId, 'completed', 100)
              resolve({ success: true, fileId })
            }
          } else {
            const errorMessage = xhr.statusText || 'Upload failed'
            updateFileStatus(fileId, 'error', 0, errorMessage)
            resolve({ success: false, fileId, error: errorMessage })
          }
        }

        xhr.onerror = () => {
          const errorMessage = 'Network error'
          updateFileStatus(fileId, 'error', 0, errorMessage)
          resolve({ success: false, fileId, error: errorMessage })
        }

        xhr.onabort = () => {
          updateFileStatus(fileId, 'error', 0, 'Upload cancelled')
          resolve({ success: false, fileId, error: 'Upload cancelled' })
        }

        // Handle abort signal
        abortController.signal.addEventListener('abort', () => {
          xhr.abort()
        })
      })

      xhr.open('POST', url)

      // Add auth header
      const token = authStore.token
      if (token) {
        xhr.setRequestHeader('Authorization', `Bearer ${token}`)
      }

      xhr.send(formData)

      return await uploadPromise
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Upload failed'
      updateFileStatus(fileId, 'error', 0, errorMessage)
      return { success: false, fileId, error: errorMessage }
    } finally {
      activeUploads.value.delete(fileId)
      abortControllers.value.delete(fileId)
      processQueue()
    }
  }

  // Update file status
  function updateFileStatus(
    fileId: string,
    status: UploadStatus,
    progress: number,
    error?: string,
    documentId?: string
  ): void {
    const file = files.value.find(f => f.id === fileId)
    if (file) {
      file.status = status
      file.progress = progress
      if (error !== undefined) file.error = error
      if (documentId) {
        file.documentId = documentId
        file.uploadedAt = new Date().toISOString()
      }
    }
  }

  // Cancel upload
  function cancelUpload(fileId: string): void {
    const controller = abortControllers.value.get(fileId)
    if (controller) {
      controller.abort()
    }

    // Remove from queue if pending
    const queueIndex = uploadQueue.value.indexOf(fileId)
    if (queueIndex !== -1) {
      uploadQueue.value.splice(queueIndex, 1)
      updateFileStatus(fileId, 'pending', 0)
    }
  }

  // Cancel all uploads
  function cancelAllUploads(): void {
    abortControllers.value.forEach(controller => controller.abort())
    uploadQueue.value = []
  }

  // Retry failed upload
  function retryUpload(fileId: string): void {
    const file = files.value.find(f => f.id === fileId)
    if (file && file.status === 'error') {
      file.status = 'pending'
      file.progress = 0
      file.error = undefined
      startUpload(fileId)
    }
  }

  // Retry all failed uploads
  function retryAllFailed(): void {
    files.value
      .filter(f => f.status === 'error')
      .forEach(f => retryUpload(f.id))
  }

  // Remove file from list
  function removeFile(fileId: string): void {
    cancelUpload(fileId)
    const index = files.value.findIndex(f => f.id === fileId)
    if (index !== -1) {
      files.value.splice(index, 1)
    }
  }

  // Clear completed files
  function clearCompleted(): void {
    files.value = files.value.filter(f => f.status !== 'completed')
  }

  // Clear all files
  function clearAll(): void {
    cancelAllUploads()
    files.value = []
  }

  // Get upload results
  function getResults(): BulkUploadResult {
    const results: UploadResult[] = files.value.map(f => ({
      success: f.status === 'completed',
      fileId: f.id,
      documentId: f.documentId,
      error: f.error
    }))

    return {
      total: files.value.length,
      successful: completedCount.value,
      failed: errorCount.value,
      results
    }
  }

  return {
    // State
    files,
    isUploading,
    pendingCount,
    uploadingCount,
    completedCount,
    errorCount,
    overallProgress,

    // Actions
    addFiles,
    startUpload,
    startAllUploads,
    cancelUpload,
    cancelAllUploads,
    retryUpload,
    retryAllFailed,
    removeFile,
    clearCompleted,
    clearAll,
    getResults
  }
}
