import api from './api'

// =============================================================================
// Response Types (matching backend DTOs)
// =============================================================================

export interface SecuritySettingsResponse {
  twoFactorEnabled: boolean
  twoFactorMethod?: 'authenticator' | 'sms' | 'email'
  lastPasswordChange?: string
  activeSessions: number
  trustedDevices: number
  loginNotifications: boolean
  securityAlerts: boolean
}

export interface TwoFactorSetupResponse {
  secret: string
  qrCodeUri: string
  backupCodes: string[]
}

export interface TwoFactorVerifyResponse {
  success: boolean
  backupCodes?: string[]
}

export interface ApiKeyResponse {
  id: string
  name: string
  keyPrefix: string
  scopes: string[]
  lastUsed?: string
  expiresAt?: string
  createdAt: string
  isActive: boolean
}

export interface ApiKeyWithSecretResponse extends ApiKeyResponse {
  key: string // Full key, only returned on creation
}

export interface CreateApiKeyRequest {
  name: string
  scopes: string[]
  expiresInDays?: number
}

export interface UpdateApiKeyRequest {
  name?: string
  scopes?: string[]
  isActive?: boolean
}

export interface SessionResponse {
  id: string
  deviceName: string
  deviceType: 'desktop' | 'mobile' | 'tablet' | 'unknown'
  browser: string
  os: string
  ipAddress: string
  location?: string
  lastActive: string
  isCurrent: boolean
  createdAt: string
}

export interface TrustedDeviceResponse {
  id: string
  deviceName: string
  deviceType: 'desktop' | 'mobile' | 'tablet' | 'unknown'
  browser: string
  os: string
  lastUsed: string
  trustedAt: string
}

export interface PasswordChangeRequest {
  currentPassword: string
  newPassword: string
}

// =============================================================================
// Security Service
// =============================================================================

class SecurityService {
  private baseUrl = '/security'

  // =========================================================================
  // Security Settings
  // =========================================================================

  /**
   * Get current security settings
   */
  async getSettings(): Promise<SecuritySettingsResponse> {
    return await api.get<SecuritySettingsResponse>(`${this.baseUrl}/settings`)
  }

  /**
   * Update security settings
   */
  async updateSettings(settings: Partial<SecuritySettingsResponse>): Promise<SecuritySettingsResponse> {
    return await api.patch<SecuritySettingsResponse>(`${this.baseUrl}/settings`, settings)
  }

  // =========================================================================
  // Two-Factor Authentication
  // =========================================================================

  /**
   * Initialize 2FA setup - returns QR code and secret
   */
  async setupTwoFactor(method: 'authenticator' | 'sms' | 'email' = 'authenticator'): Promise<TwoFactorSetupResponse> {
    return await api.post<TwoFactorSetupResponse>(`${this.baseUrl}/2fa/setup`, { method })
  }

  /**
   * Verify 2FA code and enable 2FA
   */
  async verifyTwoFactor(code: string): Promise<TwoFactorVerifyResponse> {
    return await api.post<TwoFactorVerifyResponse>(`${this.baseUrl}/2fa/verify`, { code })
  }

  /**
   * Disable 2FA
   */
  async disableTwoFactor(code: string): Promise<void> {
    return await api.post<void>(`${this.baseUrl}/2fa/disable`, { code })
  }

  /**
   * Regenerate backup codes
   */
  async regenerateBackupCodes(code: string): Promise<string[]> {
    return await api.post<string[]>(`${this.baseUrl}/2fa/backup-codes`, { code })
  }

  // =========================================================================
  // API Keys
  // =========================================================================

  /**
   * List all API keys for current user
   */
  async listApiKeys(): Promise<ApiKeyResponse[]> {
    return await api.get<ApiKeyResponse[]>(`${this.baseUrl}/api-keys`)
  }

  /**
   * Get API key by ID
   */
  async getApiKey(id: string): Promise<ApiKeyResponse> {
    return await api.get<ApiKeyResponse>(`${this.baseUrl}/api-keys/${id}`)
  }

  /**
   * Create a new API key
   */
  async createApiKey(request: CreateApiKeyRequest): Promise<ApiKeyWithSecretResponse> {
    return await api.post<ApiKeyWithSecretResponse>(`${this.baseUrl}/api-keys`, request)
  }

  /**
   * Update API key
   */
  async updateApiKey(id: string, request: UpdateApiKeyRequest): Promise<ApiKeyResponse> {
    return await api.patch<ApiKeyResponse>(`${this.baseUrl}/api-keys/${id}`, request)
  }

  /**
   * Revoke (delete) API key
   */
  async revokeApiKey(id: string): Promise<void> {
    return await api.delete<void>(`${this.baseUrl}/api-keys/${id}`)
  }

  // =========================================================================
  // Sessions
  // =========================================================================

  /**
   * List active sessions
   */
  async listSessions(): Promise<SessionResponse[]> {
    return await api.get<SessionResponse[]>(`${this.baseUrl}/sessions`)
  }

  /**
   * Revoke a session
   */
  async revokeSession(id: string): Promise<void> {
    return await api.delete<void>(`${this.baseUrl}/sessions/${id}`)
  }

  /**
   * Revoke all sessions except current
   */
  async revokeAllSessions(): Promise<void> {
    return await api.post<void>(`${this.baseUrl}/sessions/revoke-all`)
  }

  // =========================================================================
  // Trusted Devices
  // =========================================================================

  /**
   * List trusted devices
   */
  async listTrustedDevices(): Promise<TrustedDeviceResponse[]> {
    return await api.get<TrustedDeviceResponse[]>(`${this.baseUrl}/trusted-devices`)
  }

  /**
   * Remove trusted device
   */
  async removeTrustedDevice(id: string): Promise<void> {
    return await api.delete<void>(`${this.baseUrl}/trusted-devices/${id}`)
  }

  /**
   * Remove all trusted devices
   */
  async removeAllTrustedDevices(): Promise<void> {
    return await api.post<void>(`${this.baseUrl}/trusted-devices/remove-all`)
  }

  // =========================================================================
  // Password
  // =========================================================================

  /**
   * Change password
   */
  async changePassword(request: PasswordChangeRequest): Promise<void> {
    return await api.post<void>(`${this.baseUrl}/password/change`, request)
  }
}

export const securityService = new SecurityService()
export default securityService
