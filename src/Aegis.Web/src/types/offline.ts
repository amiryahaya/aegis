/**
 * Offline and Sync Types
 */

export type OperationType = 'create' | 'update' | 'delete'

export type SyncStatus = 'synced' | 'pending' | 'syncing' | 'error' | 'offline'

export interface PendingOperation<T = unknown> {
  id: string
  type: OperationType
  store: string
  data: T
  timestamp: number
  retryCount: number
  error?: string
}

export interface CacheMetadata {
  store: string
  lastSync: number
  etag?: string
  version: number
}

export interface SyncState {
  isOnline: boolean
  isSyncing: boolean
  pendingCount: number
  lastSyncAt: number | null
  syncError: string | null
}

export interface OfflineCacheStats {
  sessions: number
  workspaces: number
  documents: number
  pendingOperations: number
  totalSize?: number
}

export interface SyncResult<T> {
  data: T[]
  fromCache: boolean
  stale: boolean
  lastModified?: string
}

export interface ConflictResolution<T> {
  strategy: 'local-wins' | 'remote-wins' | 'merge' | 'manual'
  resolver?: (local: T, remote: T) => T
}

export interface OfflineConfig {
  enabled: boolean
  maxCacheAge: number
  maxRetries: number
  syncDebounceMs: number
  persistAuth: boolean
}

// Default offline configuration
export const DEFAULT_OFFLINE_CONFIG: OfflineConfig = {
  enabled: true,
  maxCacheAge: 5 * 60 * 1000, // 5 minutes
  maxRetries: 3,
  syncDebounceMs: 1000,
  persistAuth: true
}

// Cache age constants
export const CACHE_MAX_AGE = {
  SESSIONS: 5 * 60 * 1000, // 5 minutes
  WORKSPACES: 10 * 60 * 1000, // 10 minutes
  DOCUMENTS: 15 * 60 * 1000, // 15 minutes
  USER_DATA: 60 * 60 * 1000 // 1 hour
} as const
