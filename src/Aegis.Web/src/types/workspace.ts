// Workspace Types
// Note: The base Workspace interface is in api.ts
// These types extend it with additional functionality

import type { Workspace as BaseWorkspace } from './api'

// Re-export the base Workspace type for convenience
export type { Workspace } from './api'

// Extended workspace with stats (used in UI)
export interface WorkspaceWithStats extends BaseWorkspace {
  settings?: WorkspaceSettings
  stats?: WorkspaceStats
}

export interface WorkspaceSettings {
  defaultSearchMode: SearchMode
  maxResults: number
  enableCaching: boolean
  enableFollowUps: boolean
  llmModel?: string
  temperature?: number
  systemPrompt?: string
}

export type SearchMode = 'Semantic' | 'Keyword' | 'Hybrid'

export interface WorkspaceStats {
  documentCount: number
  queryCount: number
  totalTokensUsed: number
  averageResponseTime: number
  lastActivityAt?: string
}

export interface CreateWorkspaceRequest {
  teamId: string
  name: string
  description?: string
  settings?: Partial<WorkspaceSettings>
}

export interface UpdateWorkspaceRequest {
  name?: string
  description?: string
  settings?: Partial<WorkspaceSettings>
}

// Data Source Types

export interface DataSource {
  id: string
  workspaceId: string
  name: string
  type: DataSourceType
  status: DataSourceStatus
  config: DataSourceConfig
  lastSyncAt?: string
  nextSyncAt?: string
  documentCount: number
  createdAt: string
}

export type DataSourceType =
  | 'FileUpload'
  | 'WebCrawler'
  | 'Database'
  | 'SharePoint'
  | 'GoogleDrive'
  | 'Confluence'
  | 'Notion'
  | 'S3'
  | 'AzureBlob'

export type DataSourceStatus =
  | 'Active'
  | 'Syncing'
  | 'Error'
  | 'Disabled'
  | 'Pending'

export interface DataSourceConfig {
  connectionString?: string
  url?: string
  credentials?: Record<string, string>
  syncSchedule?: string
  filters?: string[]
  maxDocuments?: number
}

export interface CreateDataSourceRequest {
  workspaceId: string
  name: string
  type: DataSourceType
  config: DataSourceConfig
}

export interface UpdateDataSourceRequest {
  name?: string
  config?: Partial<DataSourceConfig>
  isEnabled?: boolean
}

// Sync History Types
export interface SyncHistory {
  id: string
  dataSourceId: string
  status: SyncStatus
  startedAt: string
  completedAt?: string
  documentsAdded: number
  documentsUpdated: number
  documentsDeleted: number
  documentsSkipped: number
  errorCount: number
  errorMessage?: string
  durationMs?: number
  triggeredBy: 'Manual' | 'Scheduled' | 'Webhook'
}

export type SyncStatus =
  | 'Pending'
  | 'InProgress'
  | 'Completed'
  | 'Failed'
  | 'Cancelled'
  | 'PartialSuccess'

export interface SyncStats {
  totalSyncs: number
  successfulSyncs: number
  failedSyncs: number
  averageDurationMs: number
  lastSuccessfulSync?: string
  documentsProcessed: number
}

// Extended Data Source with sync info
export interface DataSourceWithSync extends DataSource {
  syncHistory?: SyncHistory[]
  syncStats?: SyncStats
  isEnabled: boolean
}

// Data Source Type Configurations
export interface WebCrawlerConfig {
  url: string
  maxDepth?: number
  includePaths?: string[]
  excludePaths?: string[]
  respectRobotsTxt?: boolean
  userAgent?: string
  crawlFrequency?: 'hourly' | 'daily' | 'weekly' | 'monthly'
}

export interface DatabaseConfig {
  connectionString: string
  databaseType: 'PostgreSQL' | 'MySQL' | 'SQLServer' | 'MongoDB'
  query?: string
  tableName?: string
  incrementalColumn?: string
  batchSize?: number
}

export interface SharePointConfig {
  siteUrl: string
  clientId: string
  clientSecret?: string
  tenantId: string
  libraryName?: string
  folderPath?: string
}

export interface GoogleDriveConfig {
  folderId?: string
  serviceAccountKey?: string
  includeSharedDrives?: boolean
  fileTypes?: string[]
}

export interface ConfluenceConfig {
  baseUrl: string
  username: string
  apiToken?: string
  spaceKey?: string
  includeAttachments?: boolean
}

export interface NotionConfig {
  integrationToken?: string
  databaseId?: string
  pageIds?: string[]
}

export interface S3Config {
  bucketName: string
  region: string
  accessKeyId?: string
  secretAccessKey?: string
  prefix?: string
  fileTypes?: string[]
}

export interface AzureBlobConfig {
  connectionString?: string
  containerName: string
  sasToken?: string
  prefix?: string
  fileTypes?: string[]
}

// Document Types

export interface Document {
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

// Collaboration Types

export interface WorkspaceShare {
  id: string
  workspaceId: string
  userId: string
  role: WorkspaceRole
  createdAt: string
  createdBy: string
}

export type WorkspaceRole =
  | 'Viewer'
  | 'Commenter'
  | 'Editor'
  | 'Admin'
  | 'Owner'

export interface ShareableLink {
  id: string
  workspaceId: string
  token: string
  role: WorkspaceRole
  expiresAt?: string
  maxUses?: number
  useCount: number
  isActive: boolean
  createdAt: string
  createdBy: string
}

export interface CreateShareableLinkRequest {
  role: WorkspaceRole
  expiresAt?: string
  maxUses?: number
  password?: string
}
