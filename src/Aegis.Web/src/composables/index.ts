// =============================================================================
// Composables Index
// =============================================================================

// Core API & Data
export { useQuery, createQuerySession } from './useQuery'
export type { QueryState, UseQueryOptions, QueryComposable } from './useQuery'

export { useDocuments } from './useDocuments'
export type { DocumentsState, UploadState, UseDocumentsOptions, DocumentsComposable } from './useDocuments'

export { useNotifications } from './useNotifications'
export type { FormattedNotification, UseNotificationsOptions, NotificationsComposable } from './useNotifications'

// Real-time & Connection
export { useQueryStream, useNotifications as useNotificationStream } from './useSignalR'
export type { StreamingState } from './useSignalR'

export { useConnection, disconnectGlobal } from './useConnection'

export { usePresence } from './usePresence'

// Offline & Sync
export { useOfflineQueue } from './useOfflineQueue'
export type { OperationType, QueuedOperation, OfflineQueueState } from './useOfflineQueue'

export { useOfflineSessions, useOfflineWorkspaces, useOfflineCache } from './useOfflineData'

// UI & Interaction
export { useToast, addToast, removeToast, clearToasts } from './useToast'
export type { ToastType, Toast } from './useToast'

export { useFocusTrap } from './useFocusTrap'
export type { FocusTrapOptions } from './useFocusTrap'

export { useAnnounce, announce, clearAnnouncements, removeLiveRegions } from './useAnnounce'
export type { AnnounceMode } from './useAnnounce'

export { useErrorTracking, trackError, clearErrors, getError, errorTrackingPlugin } from './useErrorTracking'
export type { ErrorSeverity, ErrorContext, TrackedError } from './useErrorTracking'

export { useBulkSelection } from './useBulkSelection'
export { useFileUpload } from './useFileUpload'

export { useCommandPalette } from './useCommandPalette'
export { useKeyboardShortcuts } from './useKeyboardShortcuts'
export { useOnboarding } from './useOnboarding'

// Responsive & Mobile
export { useMediaQuery, useBreakpoints, useTouchDevice, useOrientation, breakpoints } from './useMediaQuery'
export type { Breakpoint } from './useMediaQuery'

export { useSwipe, usePullToRefresh, useLongPress, usePinch } from './useTouchGestures'
export type { SwipeDirection, SwipeState, SwipeOptions, PullToRefreshOptions, PullToRefreshState, LongPressOptions, PinchState } from './useTouchGestures'

// Performance
export { useVirtualScroll, useVariableVirtualScroll } from './useVirtualScroll'
export type { VirtualScrollOptions, VirtualScrollReturn } from './useVirtualScroll'

export { useIntersectionObserver, useLazyLoad, useInfiniteScroll } from './useIntersectionObserver'
export type { UseIntersectionObserverOptions, UseIntersectionObserverReturn } from './useIntersectionObserver'

export { usePerformanceMonitor, measureTime, measureTimeAsync, debounce, throttle } from './usePerformanceMonitor'
export type { PerformanceMetrics, UsePerformanceMonitorOptions, UsePerformanceMonitorReturn } from './usePerformanceMonitor'

// Export
export { useExport, useExportDialog, useReportDialog } from './useExport'

// PWA
export { usePWA } from './usePWA'
