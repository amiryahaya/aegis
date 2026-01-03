// =============================================================================
// Services Index
// =============================================================================

// Base API service
export { default as api } from './api'

// Authentication
export { default as authService } from './auth.service'
export type {
  LoginRequest,
  LoginResponse,
  RegisterRequest,
  RegisterResponse,
  RefreshTokenRequest,
  RefreshTokenResponse,
  ChangePasswordRequest,
  UserResponse
} from './auth.service'

// Sessions
export { default as sessionService } from './session.service'
export type {
  SessionResponse,
  SessionTurnResponse,
  CreateSessionRequest,
  UpdateSessionRequest,
  AddTurnRequest,
  SessionFilter,
  SessionStatsResponse,
  ExportFormat
} from './session.service'

// Workspaces
export { default as workspaceService } from './workspace.service'
export type {
  WorkspaceResponse,
  CreateWorkspaceRequest,
  UpdateWorkspaceRequest,
  WorkspaceContextResponse,
  DataSourceResponse,
  CreateDataSourceRequest,
  ShareWorkspaceRequest,
  ShareableLinkResponse
} from './workspace.service'

// Documents
export { default as documentService } from './document.service'
export type {
  DocumentResponse,
  DocumentType,
  DocumentStatus,
  DocumentChunk,
  DocumentPreview,
  UploadProgress,
  UploadOptions,
  DocumentSearchResult,
  PagedDocumentsResponse
} from './document.service'

// RAG Queries
export { default as queryService } from './query.service'
export type {
  QueryRequest,
  QueryResponse,
  QuerySource,
  QueryAnalysis,
  QueryIntent,
  QueryComplexity,
  StreamChunk,
  StreamingCallbacks
} from './query.service'

// Notifications
export { default as notificationService } from './notification.service'
export type {
  NotificationResponse,
  NotificationType,
  NotificationPriority,
  NotificationStatus,
  NotificationChannel,
  NotificationFilter as ServiceNotificationFilter,
  NotificationPageResponse,
  NotificationStats as ServiceNotificationStats,
  NotificationTypeInfo,
  SendNotificationRequest
} from './notification.service'

// SignalR Real-time
export { default as signalRService } from './signalr.service'
export type {
  ConnectionState,
  QueryCitation,
  NotificationPayload,
  SignalRCallbacks
} from './signalr.service'

// Offline Storage
export { default as indexedDBService } from './indexeddb.service'

// Sync Service
export { default as syncService } from './sync.service'

// Search
export { default as searchService } from './search.service'
export type {
  SearchRequest,
  SearchResponse,
  SearchResult,
  SearchResultMetadata,
  SearchFacets,
  SearchResultType,
  DocumentPreviewResponse,
  DocumentChunkPreview
} from './search.service'
