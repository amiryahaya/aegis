import api from './api'

// =============================================================================
// Response Types (matching backend DTOs)
// =============================================================================

export interface SystemHealthResponse {
  status: 'Healthy' | 'Warning' | 'Critical' | 'Unknown'
  uptime: string
  uptimeSeconds: number
  version: string
  environment: string
  metrics: SystemMetrics
  components: ComponentHealth[]
  timestamp: string
}

export interface SystemMetrics {
  cpuUsagePercent: number
  memoryUsedBytes: number
  memoryTotalBytes: number
  memoryUsagePercent: number
  threadCount: number
  handleCount: number
  gcGen0Collections: number
  gcGen1Collections: number
  gcGen2Collections: number
}

export interface ComponentHealth {
  name: string
  status: 'Healthy' | 'Warning' | 'Critical' | 'Unknown'
  message: string
  lastCheck: string
}

// =============================================================================
// Health Service
// =============================================================================

class HealthService {
  private baseUrl = '/health'

  /**
   * Get detailed system health
   */
  async getSystemHealth(): Promise<SystemHealthResponse> {
    return await api.get<SystemHealthResponse>(`${this.baseUrl}/system`)
  }

  /**
   * Get component health status
   */
  async getComponentHealth(): Promise<ComponentHealth[]> {
    return await api.get<ComponentHealth[]>(`${this.baseUrl}/components`)
  }

  /**
   * Get real-time system metrics
   */
  async getMetrics(): Promise<SystemMetrics> {
    return await api.get<SystemMetrics>(`${this.baseUrl}/metrics`)
  }
}

export const healthService = new HealthService()
export default healthService
