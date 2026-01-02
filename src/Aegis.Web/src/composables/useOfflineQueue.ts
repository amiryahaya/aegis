import { ref, computed, watch, onMounted, onUnmounted } from 'vue'
import { indexedDBService, STORES, type PendingOperation, type StoreName } from '@/services/indexeddb.service'
import { useToast } from '@/composables/useToast'

export type OperationType = 'create' | 'update' | 'delete'

export interface QueuedOperation<T = unknown> {
  id: string
  type: OperationType
  store: StoreName
  data: T
  timestamp: number
  retryCount: number
  error?: string
}

export interface OfflineQueueState {
  isOnline: boolean
  isSyncing: boolean
  pendingCount: number
  lastSyncAt: number | null
  syncError: string | null
}

const MAX_RETRIES = 3
const SYNC_DEBOUNCE_MS = 1000

// Singleton state
const isOnline = ref(navigator.onLine)
const isSyncing = ref(false)
const pendingCount = ref(0)
const lastSyncAt = ref<number | null>(null)
const syncError = ref<string | null>(null)

// Sync handlers registry
const syncHandlers = new Map<StoreName, (operation: PendingOperation) => Promise<void>>()

/**
 * Composable for managing offline operation queue
 */
export function useOfflineQueue() {
  const toast = useToast()
  let syncTimeout: ReturnType<typeof setTimeout> | null = null

  // Update pending count
  async function updatePendingCount() {
    try {
      pendingCount.value = await indexedDBService.count(STORES.PENDING_OPERATIONS)
    } catch (error) {
      console.error('Failed to get pending count:', error)
    }
  }

  // Queue an operation for later sync
  async function queueOperation<T>(
    type: OperationType,
    store: StoreName,
    data: T
  ): Promise<string> {
    const id = await indexedDBService.addPendingOperation({
      type,
      store,
      data
    })

    await updatePendingCount()

    // If online, trigger sync with debounce
    if (isOnline.value) {
      debouncedSync()
    }

    return id
  }

  // Debounced sync to avoid rapid fire
  function debouncedSync() {
    if (syncTimeout) {
      clearTimeout(syncTimeout)
    }

    syncTimeout = setTimeout(() => {
      syncPendingOperations()
    }, SYNC_DEBOUNCE_MS)
  }

  // Process all pending operations
  async function syncPendingOperations(): Promise<void> {
    if (!isOnline.value || isSyncing.value) return

    isSyncing.value = true
    syncError.value = null

    try {
      const operations = await indexedDBService.getPendingOperations()

      for (const operation of operations) {
        // Skip if max retries exceeded
        if (operation.retryCount >= MAX_RETRIES) {
          console.warn(`Operation ${operation.id} exceeded max retries, skipping`)
          continue
        }

        // Get the handler for this store
        const handler = syncHandlers.get(operation.store)
        if (!handler) {
          console.warn(`No sync handler for store: ${operation.store}`)
          continue
        }

        try {
          await handler(operation)
          await indexedDBService.removePendingOperation(operation.id)
        } catch (error) {
          const errorMessage = error instanceof Error ? error.message : 'Unknown error'
          await indexedDBService.incrementRetryCount(operation.id, errorMessage)
          console.error(`Failed to sync operation ${operation.id}:`, error)
        }
      }

      lastSyncAt.value = Date.now()
      await updatePendingCount()

      if (pendingCount.value === 0) {
        toast.success('All changes synced')
      }
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Sync failed'
      syncError.value = errorMessage
      console.error('Sync error:', error)
    } finally {
      isSyncing.value = false
    }
  }

  // Register a sync handler for a store
  function registerSyncHandler(
    store: StoreName,
    handler: (operation: PendingOperation) => Promise<void>
  ): void {
    syncHandlers.set(store, handler)
  }

  // Unregister a sync handler
  function unregisterSyncHandler(store: StoreName): void {
    syncHandlers.delete(store)
  }

  // Clear all pending operations (use with caution)
  async function clearPendingOperations(): Promise<void> {
    await indexedDBService.clear(STORES.PENDING_OPERATIONS)
    await updatePendingCount()
  }

  // Get pending operations for a specific store
  async function getPendingForStore(store: StoreName): Promise<PendingOperation[]> {
    return indexedDBService.getByIndex<PendingOperation>(
      STORES.PENDING_OPERATIONS,
      'store',
      store
    )
  }

  // Handle online/offline events
  function handleOnline() {
    isOnline.value = true
    toast.info('Back online - syncing changes...')
    syncPendingOperations()
  }

  function handleOffline() {
    isOnline.value = false
    toast.warning('You are offline - changes will sync when reconnected')
  }

  // Setup event listeners
  onMounted(() => {
    window.addEventListener('online', handleOnline)
    window.addEventListener('offline', handleOffline)
    updatePendingCount()
  })

  onUnmounted(() => {
    window.removeEventListener('online', handleOnline)
    window.removeEventListener('offline', handleOffline)

    if (syncTimeout) {
      clearTimeout(syncTimeout)
    }
  })

  // Auto-sync when coming online
  watch(isOnline, (online) => {
    if (online && pendingCount.value > 0) {
      syncPendingOperations()
    }
  })

  return {
    // State
    isOnline: computed(() => isOnline.value),
    isSyncing: computed(() => isSyncing.value),
    pendingCount: computed(() => pendingCount.value),
    lastSyncAt: computed(() => lastSyncAt.value),
    syncError: computed(() => syncError.value),
    hasPendingChanges: computed(() => pendingCount.value > 0),

    // Actions
    queueOperation,
    syncPendingOperations,
    clearPendingOperations,
    getPendingForStore,
    registerSyncHandler,
    unregisterSyncHandler,
    updatePendingCount
  }
}

export default useOfflineQueue
