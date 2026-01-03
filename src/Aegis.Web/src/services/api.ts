import axios, { type AxiosInstance, type AxiosError, type InternalAxiosRequestConfig } from 'axios'
import type { ApiError } from '@/types'

// Environment configuration
const API_BASE_URL = import.meta.env.VITE_API_URL || '/api'
const API_TIMEOUT = Number(import.meta.env.VITE_API_TIMEOUT) || 30000

// Token storage keys (must match auth store)
const TOKEN_KEY = 'aegis_token'
const REFRESH_TOKEN_KEY = 'aegis_refresh_token'

// Flag to prevent multiple refresh attempts
let isRefreshing = false
let refreshSubscribers: ((token: string) => void)[] = []

function subscribeTokenRefresh(cb: (token: string) => void) {
  refreshSubscribers.push(cb)
}

function onTokenRefreshed(token: string) {
  refreshSubscribers.forEach(cb => cb(token))
  refreshSubscribers = []
}

class ApiService {
  private client: AxiosInstance

  constructor() {
    this.client = axios.create({
      baseURL: API_BASE_URL,
      headers: {
        'Content-Type': 'application/json'
      },
      timeout: API_TIMEOUT
    })

    this.setupInterceptors()
  }

  private setupInterceptors(): void {
    // Request interceptor - add auth token
    this.client.interceptors.request.use(
      (config: InternalAxiosRequestConfig) => {
        const token = localStorage.getItem(TOKEN_KEY)
        if (token && config.headers) {
          config.headers.Authorization = `Bearer ${token}`
        }
        return config
      },
      (error) => Promise.reject(error)
    )

    // Response interceptor - handle errors and token refresh
    this.client.interceptors.response.use(
      (response) => response,
      async (error: AxiosError<ApiError>) => {
        const originalRequest = error.config as InternalAxiosRequestConfig & { _retry?: boolean }

        // Handle 401 - attempt token refresh
        if (error.response?.status === 401 && !originalRequest._retry) {
          // Don't retry auth endpoints
          if (originalRequest.url?.includes('/auth/login') ||
              originalRequest.url?.includes('/auth/refresh')) {
            return Promise.reject(this.normalizeError(error))
          }

          if (isRefreshing) {
            // Wait for the ongoing refresh to complete
            return new Promise((resolve) => {
              subscribeTokenRefresh((token) => {
                if (originalRequest.headers) {
                  originalRequest.headers.Authorization = `Bearer ${token}`
                }
                resolve(this.client(originalRequest))
              })
            })
          }

          originalRequest._retry = true
          isRefreshing = true

          try {
            const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY)
            if (!refreshToken) {
              throw new Error('No refresh token')
            }

            // Attempt to refresh the token
            const response = await this.client.post<{
              token: string
              refreshToken: string
              expiresAt: string
            }>('/auth/refresh', { refreshToken })

            const { token, refreshToken: newRefreshToken } = response.data

            // Store new tokens
            localStorage.setItem(TOKEN_KEY, token)
            localStorage.setItem(REFRESH_TOKEN_KEY, newRefreshToken)

            // Update auth header and notify subscribers
            if (originalRequest.headers) {
              originalRequest.headers.Authorization = `Bearer ${token}`
            }
            onTokenRefreshed(token)

            return this.client(originalRequest)
          } catch {
            // Refresh failed - clear tokens and redirect to login
            this.clearTokens()
            window.location.href = '/login'
            return Promise.reject(this.normalizeError(error))
          } finally {
            isRefreshing = false
          }
        }

        // Handle 403 - forbidden
        if (error.response?.status === 403) {
          console.error('Access denied:', error.response.data)
        }

        // Handle 500+ - server errors
        if (error.response && error.response.status >= 500) {
          console.error('Server error:', error.response.data)
        }

        return Promise.reject(this.normalizeError(error))
      }
    )
  }

  private clearTokens(): void {
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(REFRESH_TOKEN_KEY)
    localStorage.removeItem('aegis_user')
    localStorage.removeItem('aegis_token_expiry')
  }

  private normalizeError(error: AxiosError<ApiError>): ApiError {
    if (error.response?.data) {
      return error.response.data
    }

    // Handle network errors
    if (error.code === 'ECONNABORTED') {
      return {
        type: 'TimeoutError',
        title: 'Request Timeout',
        status: 408,
        detail: 'The request took too long to complete. Please try again.'
      }
    }

    if (!error.response) {
      return {
        type: 'NetworkError',
        title: 'Network Error',
        status: 0,
        detail: 'Unable to connect to the server. Please check your internet connection.'
      }
    }

    return {
      type: 'UnknownError',
      title: 'Error',
      status: error.response?.status || 0,
      detail: error.message || 'An unexpected error occurred'
    }
  }

  // Generic request methods
  async get<T>(url: string, params?: Record<string, unknown>): Promise<T> {
    const response = await this.client.get<T>(url, { params })
    return response.data
  }

  async post<T>(url: string, data?: unknown): Promise<T> {
    const response = await this.client.post<T>(url, data)
    return response.data
  }

  async put<T>(url: string, data?: unknown): Promise<T> {
    const response = await this.client.put<T>(url, data)
    return response.data
  }

  async patch<T>(url: string, data?: unknown): Promise<T> {
    const response = await this.client.patch<T>(url, data)
    return response.data
  }

  async delete<T>(url: string): Promise<T> {
    const response = await this.client.delete<T>(url)
    return response.data
  }

  // Get the axios instance for custom usage (e.g., file uploads)
  getClient(): AxiosInstance {
    return this.client
  }

  // Get the base URL for SignalR connections
  getBaseUrl(): string {
    return API_BASE_URL
  }
}

export const api = new ApiService()
export default api
