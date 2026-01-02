/**
 * IndexedDB Service for offline data storage
 * Provides typed storage for sessions, workspaces, and pending operations
 */

const DB_NAME = 'aegis-offline'
const DB_VERSION = 1

// Store names
export const STORES = {
  SESSIONS: 'sessions',
  WORKSPACES: 'workspaces',
  DOCUMENTS: 'documents',
  PENDING_OPERATIONS: 'pendingOperations',
  CACHE_METADATA: 'cacheMetadata',
  USER_DATA: 'userData'
} as const

export type StoreName = (typeof STORES)[keyof typeof STORES]

export interface PendingOperation {
  id: string
  type: 'create' | 'update' | 'delete'
  store: StoreName
  data: unknown
  timestamp: number
  retryCount: number
  error?: string
}

export interface CacheMetadata {
  store: StoreName
  lastSync: number
  etag?: string
  version: number
}

class IndexedDBService {
  private db: IDBDatabase | null = null
  private initPromise: Promise<IDBDatabase> | null = null

  /**
   * Initialize the database connection
   */
  async init(): Promise<IDBDatabase> {
    if (this.db) return this.db

    if (this.initPromise) return this.initPromise

    this.initPromise = new Promise((resolve, reject) => {
      const request = indexedDB.open(DB_NAME, DB_VERSION)

      request.onerror = () => {
        console.error('IndexedDB error:', request.error)
        reject(request.error)
      }

      request.onsuccess = () => {
        this.db = request.result
        resolve(this.db)
      }

      request.onupgradeneeded = (event) => {
        const db = (event.target as IDBOpenDBRequest).result

        // Sessions store
        if (!db.objectStoreNames.contains(STORES.SESSIONS)) {
          const sessionsStore = db.createObjectStore(STORES.SESSIONS, { keyPath: 'id' })
          sessionsStore.createIndex('userId', 'userId', { unique: false })
          sessionsStore.createIndex('updatedAt', 'updatedAt', { unique: false })
        }

        // Workspaces store
        if (!db.objectStoreNames.contains(STORES.WORKSPACES)) {
          const workspacesStore = db.createObjectStore(STORES.WORKSPACES, { keyPath: 'id' })
          workspacesStore.createIndex('teamId', 'teamId', { unique: false })
        }

        // Documents store
        if (!db.objectStoreNames.contains(STORES.DOCUMENTS)) {
          const documentsStore = db.createObjectStore(STORES.DOCUMENTS, { keyPath: 'id' })
          documentsStore.createIndex('workspaceId', 'workspaceId', { unique: false })
        }

        // Pending operations store (for offline queue)
        if (!db.objectStoreNames.contains(STORES.PENDING_OPERATIONS)) {
          const pendingStore = db.createObjectStore(STORES.PENDING_OPERATIONS, { keyPath: 'id' })
          pendingStore.createIndex('timestamp', 'timestamp', { unique: false })
          pendingStore.createIndex('store', 'store', { unique: false })
        }

        // Cache metadata store
        if (!db.objectStoreNames.contains(STORES.CACHE_METADATA)) {
          db.createObjectStore(STORES.CACHE_METADATA, { keyPath: 'store' })
        }

        // User data store (for current user, preferences, etc.)
        if (!db.objectStoreNames.contains(STORES.USER_DATA)) {
          db.createObjectStore(STORES.USER_DATA, { keyPath: 'key' })
        }
      }
    })

    return this.initPromise
  }

  /**
   * Get a single item by key
   */
  async get<T>(store: StoreName, key: string): Promise<T | undefined> {
    const db = await this.init()

    return new Promise((resolve, reject) => {
      const transaction = db.transaction(store, 'readonly')
      const objectStore = transaction.objectStore(store)
      const request = objectStore.get(key)

      request.onsuccess = () => resolve(request.result as T | undefined)
      request.onerror = () => reject(request.error)
    })
  }

  /**
   * Get all items from a store
   */
  async getAll<T>(store: StoreName): Promise<T[]> {
    const db = await this.init()

    return new Promise((resolve, reject) => {
      const transaction = db.transaction(store, 'readonly')
      const objectStore = transaction.objectStore(store)
      const request = objectStore.getAll()

      request.onsuccess = () => resolve(request.result as T[])
      request.onerror = () => reject(request.error)
    })
  }

  /**
   * Get items by index
   */
  async getByIndex<T>(store: StoreName, indexName: string, value: IDBValidKey): Promise<T[]> {
    const db = await this.init()

    return new Promise((resolve, reject) => {
      const transaction = db.transaction(store, 'readonly')
      const objectStore = transaction.objectStore(store)
      const index = objectStore.index(indexName)
      const request = index.getAll(value)

      request.onsuccess = () => resolve(request.result as T[])
      request.onerror = () => reject(request.error)
    })
  }

  /**
   * Put (create or update) an item
   */
  async put<T>(store: StoreName, item: T): Promise<void> {
    const db = await this.init()

    return new Promise((resolve, reject) => {
      const transaction = db.transaction(store, 'readwrite')
      const objectStore = transaction.objectStore(store)
      const request = objectStore.put(item)

      request.onsuccess = () => resolve()
      request.onerror = () => reject(request.error)
    })
  }

  /**
   * Put multiple items in a single transaction
   */
  async putMany<T>(store: StoreName, items: T[]): Promise<void> {
    const db = await this.init()

    return new Promise((resolve, reject) => {
      const transaction = db.transaction(store, 'readwrite')
      const objectStore = transaction.objectStore(store)

      transaction.oncomplete = () => resolve()
      transaction.onerror = () => reject(transaction.error)

      for (const item of items) {
        objectStore.put(item)
      }
    })
  }

  /**
   * Delete an item by key
   */
  async delete(store: StoreName, key: string): Promise<void> {
    const db = await this.init()

    return new Promise((resolve, reject) => {
      const transaction = db.transaction(store, 'readwrite')
      const objectStore = transaction.objectStore(store)
      const request = objectStore.delete(key)

      request.onsuccess = () => resolve()
      request.onerror = () => reject(request.error)
    })
  }

  /**
   * Clear all items from a store
   */
  async clear(store: StoreName): Promise<void> {
    const db = await this.init()

    return new Promise((resolve, reject) => {
      const transaction = db.transaction(store, 'readwrite')
      const objectStore = transaction.objectStore(store)
      const request = objectStore.clear()

      request.onsuccess = () => resolve()
      request.onerror = () => reject(request.error)
    })
  }

  /**
   * Count items in a store
   */
  async count(store: StoreName): Promise<number> {
    const db = await this.init()

    return new Promise((resolve, reject) => {
      const transaction = db.transaction(store, 'readonly')
      const objectStore = transaction.objectStore(store)
      const request = objectStore.count()

      request.onsuccess = () => resolve(request.result)
      request.onerror = () => reject(request.error)
    })
  }

  /**
   * Add a pending operation to the queue
   */
  async addPendingOperation(operation: Omit<PendingOperation, 'id' | 'timestamp' | 'retryCount'>): Promise<string> {
    const id = crypto.randomUUID()
    const pendingOp: PendingOperation = {
      ...operation,
      id,
      timestamp: Date.now(),
      retryCount: 0
    }

    await this.put(STORES.PENDING_OPERATIONS, pendingOp)
    return id
  }

  /**
   * Get all pending operations sorted by timestamp
   */
  async getPendingOperations(): Promise<PendingOperation[]> {
    const operations = await this.getAll<PendingOperation>(STORES.PENDING_OPERATIONS)
    return operations.sort((a, b) => a.timestamp - b.timestamp)
  }

  /**
   * Remove a pending operation after successful sync
   */
  async removePendingOperation(id: string): Promise<void> {
    await this.delete(STORES.PENDING_OPERATIONS, id)
  }

  /**
   * Update retry count for a failed operation
   */
  async incrementRetryCount(id: string, error?: string): Promise<void> {
    const operation = await this.get<PendingOperation>(STORES.PENDING_OPERATIONS, id)
    if (operation) {
      operation.retryCount++
      operation.error = error
      await this.put(STORES.PENDING_OPERATIONS, operation)
    }
  }

  /**
   * Update cache metadata for a store
   */
  async updateCacheMetadata(store: StoreName, etag?: string): Promise<void> {
    const metadata: CacheMetadata = {
      store,
      lastSync: Date.now(),
      etag,
      version: DB_VERSION
    }
    await this.put(STORES.CACHE_METADATA, metadata)
  }

  /**
   * Get cache metadata for a store
   */
  async getCacheMetadata(store: StoreName): Promise<CacheMetadata | undefined> {
    return this.get<CacheMetadata>(STORES.CACHE_METADATA, store)
  }

  /**
   * Check if cache is stale (older than maxAge in milliseconds)
   */
  async isCacheStale(store: StoreName, maxAge: number): Promise<boolean> {
    const metadata = await this.getCacheMetadata(store)
    if (!metadata) return true

    const age = Date.now() - metadata.lastSync
    return age > maxAge
  }

  /**
   * Store user data (generic key-value storage)
   */
  async setUserData<T>(key: string, value: T): Promise<void> {
    await this.put(STORES.USER_DATA, { key, value })
  }

  /**
   * Get user data by key
   */
  async getUserData<T>(key: string): Promise<T | undefined> {
    const result = await this.get<{ key: string; value: T }>(STORES.USER_DATA, key)
    return result?.value
  }

  /**
   * Close the database connection
   */
  close(): void {
    if (this.db) {
      this.db.close()
      this.db = null
      this.initPromise = null
    }
  }

  /**
   * Delete the entire database (for logout/reset)
   */
  async deleteDatabase(): Promise<void> {
    this.close()

    return new Promise((resolve, reject) => {
      const request = indexedDB.deleteDatabase(DB_NAME)

      request.onsuccess = () => resolve()
      request.onerror = () => reject(request.error)
    })
  }
}

// Export singleton instance
export const indexedDBService = new IndexedDBService()
export default indexedDBService
