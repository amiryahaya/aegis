import axios, { type AxiosInstance, type AxiosError, type InternalAxiosRequestConfig } from 'axios'
import type { ApiError } from '@/types'

const API_BASE_URL = import.meta.env.VITE_API_URL || '/api'

class ApiService {
  private client: AxiosInstance

  constructor() {
    this.client = axios.create({
      baseURL: API_BASE_URL,
      headers: {
        'Content-Type': 'application/json'
      },
      timeout: 30000
    })

    this.setupInterceptors()
  }

  private setupInterceptors(): void {
    // Request interceptor - add auth token
    this.client.interceptors.request.use(
      (config: InternalAxiosRequestConfig) => {
        const token = localStorage.getItem('token')
        if (token && config.headers) {
          config.headers.Authorization = `Bearer ${token}`
        }
        return config
      },
      (error) => Promise.reject(error)
    )

    // Response interceptor - handle errors
    this.client.interceptors.response.use(
      (response) => response,
      async (error: AxiosError<ApiError>) => {
        if (error.response?.status === 401) {
          // Token expired or invalid
          localStorage.removeItem('token')
          localStorage.removeItem('user')
          window.location.href = '/login'
        }
        return Promise.reject(this.normalizeError(error))
      }
    )
  }

  private normalizeError(error: AxiosError<ApiError>): ApiError {
    if (error.response?.data) {
      return error.response.data
    }
    return {
      type: 'NetworkError',
      title: 'Network Error',
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

  async delete<T>(url: string): Promise<T> {
    const response = await this.client.delete<T>(url)
    return response.data
  }

  // Get the axios instance for custom usage
  getClient(): AxiosInstance {
    return this.client
  }
}

export const api = new ApiService()
export default api
