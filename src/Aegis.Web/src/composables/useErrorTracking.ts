import { ref, readonly } from 'vue'
import type { App } from 'vue'

// Error severity levels
export type ErrorSeverity = 'info' | 'warning' | 'error' | 'critical'

// Error context for tracking
export interface ErrorContext {
  component?: string
  action?: string
  userId?: string
  sessionId?: string
  url?: string
  userAgent?: string
  timestamp?: string
  extra?: Record<string, unknown>
}

// Tracked error with metadata
export interface TrackedError {
  id: string
  message: string
  name: string
  stack?: string
  severity: ErrorSeverity
  context: ErrorContext
  timestamp: Date
  handled: boolean
}

// Error tracking state
const errors = ref<TrackedError[]>([])
const maxErrors = 100 // Keep last 100 errors in memory

// Generate unique ID
const generateId = () => {
  return `err_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`
}

// Get current context
const getCurrentContext = (): Partial<ErrorContext> => {
  return {
    url: window.location.href,
    userAgent: navigator.userAgent,
    timestamp: new Date().toISOString()
  }
}

// Track an error
export const trackError = (
  error: Error | string,
  severity: ErrorSeverity = 'error',
  context: Partial<ErrorContext> = {}
): TrackedError => {
  const err = typeof error === 'string' ? new Error(error) : error

  const trackedError: TrackedError = {
    id: generateId(),
    message: err.message,
    name: err.name,
    stack: err.stack,
    severity,
    context: {
      ...getCurrentContext(),
      ...context
    },
    timestamp: new Date(),
    handled: true
  }

  // Add to errors array (keep last N errors)
  errors.value = [trackedError, ...errors.value].slice(0, maxErrors)

  // Log to console in development
  if (import.meta.env.DEV) {
    const logMethod = severity === 'critical' || severity === 'error' ? 'error' : 'warn'
    console[logMethod](`[ErrorTracking] ${severity.toUpperCase()}:`, err.message, context)
  }

  // In production, could send to external service (Sentry, LogRocket, etc.)
  if (import.meta.env.PROD) {
    sendToExternalService(trackedError)
  }

  return trackedError
}

// Send error to external service (placeholder)
const sendToExternalService = async (trackedError: TrackedError) => {
  // This is where you'd integrate with Sentry, LogRocket, etc.
  // For now, we'll just log to console in production
  try {
    // Example: Sentry integration
    // if (window.Sentry) {
    //   window.Sentry.captureException(new Error(trackedError.message), {
    //     tags: { severity: trackedError.severity },
    //     extra: trackedError.context
    //   })
    // }

    // Example: Custom endpoint
    // await fetch('/api/errors', {
    //   method: 'POST',
    //   headers: { 'Content-Type': 'application/json' },
    //   body: JSON.stringify(trackedError)
    // })

    // Log for debugging purposes
    if (trackedError.severity === 'critical') {
      console.error('[ErrorTracking] Critical error logged:', trackedError.message)
    }
  } catch (e) {
    console.error('[ErrorTracking] Failed to send error to external service:', e)
  }
}

// Clear all tracked errors
export const clearErrors = () => {
  errors.value = []
}

// Get error by ID
export const getError = (id: string): TrackedError | undefined => {
  return errors.value.find(e => e.id === id)
}

// Composable for components
export function useErrorTracking() {
  // Track error with component context
  const track = (
    error: Error | string,
    severity: ErrorSeverity = 'error',
    context: Partial<ErrorContext> = {}
  ) => {
    return trackError(error, severity, context)
  }

  // Track API error
  const trackApiError = (
    error: unknown,
    endpoint: string,
    method: string = 'GET'
  ) => {
    const message = error instanceof Error ? error.message : String(error)
    return track(message, 'error', {
      action: `API ${method} ${endpoint}`,
      extra: { endpoint, method, error }
    })
  }

  // Track user action error
  const trackActionError = (
    error: Error | string,
    action: string,
    extra?: Record<string, unknown>
  ) => {
    return track(error, 'warning', { action, extra })
  }

  // Track critical error
  const trackCritical = (
    error: Error | string,
    context: Partial<ErrorContext> = {}
  ) => {
    return track(error, 'critical', context)
  }

  return {
    errors: readonly(errors),
    track,
    trackApiError,
    trackActionError,
    trackCritical,
    clearErrors,
    getError
  }
}

// Vue plugin for global error handling
export const errorTrackingPlugin = {
  install(app: App) {
    // Global error handler
    app.config.errorHandler = (err, instance, info) => {
      const error = err instanceof Error ? err : new Error(String(err))
      const componentName = instance?.$options?.name || instance?.$options?.__name || 'Unknown'

      trackError(error, 'error', {
        component: componentName,
        action: info,
        extra: { vueInfo: info }
      })

      // Re-throw in development for better debugging
      if (import.meta.env.DEV) {
        console.error(err)
      }
    }

    // Global warning handler (development only)
    if (import.meta.env.DEV) {
      app.config.warnHandler = (msg, instance, trace) => {
        const componentName = instance?.$options?.name || instance?.$options?.__name || 'Unknown'
        console.warn(`[Vue Warning] ${msg}`, { component: componentName, trace })
      }
    }

    // Handle unhandled promise rejections
    window.addEventListener('unhandledrejection', (event) => {
      const error = event.reason instanceof Error
        ? event.reason
        : new Error(String(event.reason))

      trackError(error, 'error', {
        action: 'Unhandled Promise Rejection',
        extra: { reason: event.reason }
      })
    })

    // Handle global errors
    window.addEventListener('error', (event) => {
      // Ignore ResizeObserver errors (common false positive)
      if (event.message?.includes('ResizeObserver')) {
        return
      }

      trackError(event.error || new Error(event.message), 'error', {
        action: 'Global Error',
        extra: {
          filename: event.filename,
          lineno: event.lineno,
          colno: event.colno
        }
      })
    })

    // Provide composable globally
    app.provide('errorTracking', useErrorTracking())
  }
}

export default useErrorTracking
