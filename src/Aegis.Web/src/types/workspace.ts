// Workspace Types
// Note: The base Workspace interface is in api.ts
// These types extend it with additional functionality

import type { Workspace as BaseWorkspace } from './api'

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
