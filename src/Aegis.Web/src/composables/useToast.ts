import { ref, readonly } from 'vue'

export type ToastType = 'success' | 'error' | 'warning' | 'info'

export interface Toast {
  id: string
  type: ToastType
  title: string
  message?: string
  duration?: number
  dismissible?: boolean
  action?: {
    label: string
    onClick: () => void
  }
}

// Global toast state
const toasts = ref<Toast[]>([])
const maxToasts = 5

// Generate unique ID
const generateId = () => {
  return `toast_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`
}

// Add a toast
export const addToast = (toast: Omit<Toast, 'id'>): string => {
  const id = generateId()
  const newToast: Toast = {
    id,
    dismissible: true,
    duration: 5000,
    ...toast
  }

  // Add to beginning, limit total toasts
  toasts.value = [newToast, ...toasts.value].slice(0, maxToasts)

  // Auto-dismiss if duration is set
  if (newToast.duration && newToast.duration > 0) {
    setTimeout(() => {
      removeToast(id)
    }, newToast.duration)
  }

  return id
}

// Remove a toast
export const removeToast = (id: string) => {
  toasts.value = toasts.value.filter(t => t.id !== id)
}

// Clear all toasts
export const clearToasts = () => {
  toasts.value = []
}

// Composable
export function useToast() {
  // Convenience methods for different toast types
  const success = (title: string, message?: string, options?: Partial<Toast>) => {
    return addToast({ type: 'success', title, message, ...options })
  }

  const error = (title: string, message?: string, options?: Partial<Toast>) => {
    return addToast({
      type: 'error',
      title,
      message,
      duration: 8000, // Errors stay longer
      ...options
    })
  }

  const warning = (title: string, message?: string, options?: Partial<Toast>) => {
    return addToast({ type: 'warning', title, message, ...options })
  }

  const info = (title: string, message?: string, options?: Partial<Toast>) => {
    return addToast({ type: 'info', title, message, ...options })
  }

  // Show API error with formatted message
  const apiError = (err: unknown, fallbackMessage = 'An error occurred') => {
    let message = fallbackMessage
    let title = 'Error'

    if (err instanceof Error) {
      message = err.message
    } else if (typeof err === 'object' && err !== null) {
      const errorObj = err as Record<string, unknown>
      if (errorObj.title) title = String(errorObj.title)
      if (errorObj.detail) message = String(errorObj.detail)
      else if (errorObj.message) message = String(errorObj.message)
    }

    return error(title, message)
  }

  // Promise-based toast for async operations
  const promise = async <T>(
    promise: Promise<T>,
    options: {
      loading: string
      success: string | ((data: T) => string)
      error: string | ((err: unknown) => string)
    }
  ): Promise<T> => {
    const loadingId = addToast({
      type: 'info',
      title: options.loading,
      duration: 0, // Don't auto-dismiss
      dismissible: false
    })

    try {
      const result = await promise
      removeToast(loadingId)
      const successMessage = typeof options.success === 'function'
        ? options.success(result)
        : options.success
      success(successMessage)
      return result
    } catch (err) {
      removeToast(loadingId)
      const errorMessage = typeof options.error === 'function'
        ? options.error(err)
        : options.error
      error(errorMessage)
      throw err
    }
  }

  return {
    toasts: readonly(toasts),
    add: addToast,
    remove: removeToast,
    clear: clearToasts,
    success,
    error,
    warning,
    info,
    apiError,
    promise
  }
}

export default useToast
