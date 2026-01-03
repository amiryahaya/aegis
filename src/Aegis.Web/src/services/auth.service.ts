import api from './api'
import type { User } from '@/types'

// =============================================================================
// Request/Response Types (matching backend DTOs)
// =============================================================================

export interface LoginRequest {
  email: string
  password: string
}

export interface LoginResponse {
  token: string
  refreshToken: string
  user: UserResponse
  expiresAt: string
}

export interface RegisterRequest {
  email: string
  password: string
  firstName: string
  lastName: string
  teamId?: string
}

export interface RegisterResponse {
  id: string
  email: string
  firstName: string
  lastName: string
  createdAt: string
}

export interface UserResponse {
  id: string
  email: string
  firstName: string
  lastName: string
  displayName: string
  avatarUrl?: string
  role: string
  teamId?: string
  teamName?: string
  isActive: boolean
  createdAt: string
  lastLoginAt?: string
}

export interface RefreshTokenRequest {
  refreshToken: string
}

export interface RefreshTokenResponse {
  token: string
  refreshToken: string
  expiresAt: string
}

export interface ChangePasswordRequest {
  currentPassword: string
  newPassword: string
}

export interface ForgotPasswordRequest {
  email: string
}

export interface ForgotPasswordResponse {
  message: string
  // In development, token might be returned for testing
  resetToken?: string
}

export interface ResetPasswordRequest {
  token: string
  newPassword: string
}

export interface ResetPasswordResponse {
  message: string
}

// =============================================================================
// Security Types
// =============================================================================

export interface ActiveSession {
  id: string
  deviceName: string
  deviceType: 'desktop' | 'mobile' | 'tablet' | 'unknown'
  browser: string
  os: string
  ipAddress: string
  location?: string
  lastActiveAt: string
  createdAt: string
  isCurrent: boolean
}

export interface LoginHistoryEntry {
  id: string
  ipAddress: string
  location?: string
  deviceName: string
  browser: string
  os: string
  status: 'success' | 'failed'
  failureReason?: string
  createdAt: string
}

export interface TwoFactorSetupResponse {
  secret: string
  qrCodeUrl: string
  backupCodes: string[]
}

export interface TwoFactorVerifyRequest {
  code: string
}

export interface SecuritySettings {
  twoFactorEnabled: boolean
  lastPasswordChange?: string
  passwordExpiresAt?: string
}

// =============================================================================
// Auth Service
// =============================================================================

class AuthService {
  private readonly basePath = '/auth'

  /**
   * Login with email and password
   */
  async login(request: LoginRequest): Promise<LoginResponse> {
    return api.post<LoginResponse>(`${this.basePath}/login`, request)
  }

  /**
   * Register a new user
   */
  async register(request: RegisterRequest): Promise<RegisterResponse> {
    return api.post<RegisterResponse>(`${this.basePath}/register`, request)
  }

  /**
   * Refresh access token
   */
  async refreshToken(request: RefreshTokenRequest): Promise<RefreshTokenResponse> {
    return api.post<RefreshTokenResponse>(`${this.basePath}/refresh`, request)
  }

  /**
   * Get current user profile
   */
  async getCurrentUser(): Promise<UserResponse> {
    return api.get<UserResponse>(`${this.basePath}/me`)
  }

  /**
   * Update user profile
   */
  async updateProfile(data: Partial<UserResponse>): Promise<UserResponse> {
    return api.put<UserResponse>(`${this.basePath}/me`, data)
  }

  /**
   * Change password
   */
  async changePassword(request: ChangePasswordRequest): Promise<void> {
    return api.post<void>(`${this.basePath}/change-password`, request)
  }

  /**
   * Logout (invalidate tokens on server)
   */
  async logout(): Promise<void> {
    try {
      await api.post<void>(`${this.basePath}/logout`)
    } catch {
      // Ignore logout errors - we'll clear local state anyway
    }
  }

  /**
   * Request password reset email
   */
  async forgotPassword(request: ForgotPasswordRequest): Promise<ForgotPasswordResponse> {
    return api.post<ForgotPasswordResponse>(`${this.basePath}/forgot-password`, request)
  }

  /**
   * Reset password with token
   */
  async resetPassword(request: ResetPasswordRequest): Promise<ResetPasswordResponse> {
    return api.post<ResetPasswordResponse>(`${this.basePath}/reset-password`, request)
  }

  /**
   * Validate reset token (check if it's still valid)
   */
  async validateResetToken(token: string): Promise<{ valid: boolean; email?: string }> {
    return api.get<{ valid: boolean; email?: string }>(`${this.basePath}/reset-password/validate?token=${token}`)
  }

  // ===========================================================================
  // Security Methods
  // ===========================================================================

  /**
   * Get security settings for current user
   */
  async getSecuritySettings(): Promise<SecuritySettings> {
    return api.get<SecuritySettings>(`${this.basePath}/security`)
  }

  /**
   * Get active sessions for current user
   */
  async getActiveSessions(): Promise<ActiveSession[]> {
    return api.get<ActiveSession[]>(`${this.basePath}/sessions`)
  }

  /**
   * Revoke a specific session
   */
  async revokeSession(sessionId: string): Promise<void> {
    return api.delete<void>(`${this.basePath}/sessions/${sessionId}`)
  }

  /**
   * Revoke all sessions except current
   */
  async revokeAllSessions(): Promise<void> {
    return api.post<void>(`${this.basePath}/sessions/revoke-all`)
  }

  /**
   * Get login history
   */
  async getLoginHistory(limit: number = 10): Promise<LoginHistoryEntry[]> {
    return api.get<LoginHistoryEntry[]>(`${this.basePath}/login-history?limit=${limit}`)
  }

  /**
   * Setup two-factor authentication
   */
  async setupTwoFactor(): Promise<TwoFactorSetupResponse> {
    return api.post<TwoFactorSetupResponse>(`${this.basePath}/2fa/setup`)
  }

  /**
   * Verify and enable two-factor authentication
   */
  async verifyTwoFactor(request: TwoFactorVerifyRequest): Promise<{ success: boolean; backupCodes?: string[] }> {
    return api.post<{ success: boolean; backupCodes?: string[] }>(`${this.basePath}/2fa/verify`, request)
  }

  /**
   * Disable two-factor authentication
   */
  async disableTwoFactor(request: TwoFactorVerifyRequest): Promise<void> {
    return api.post<void>(`${this.basePath}/2fa/disable`, request)
  }

  /**
   * Regenerate backup codes
   */
  async regenerateBackupCodes(request: TwoFactorVerifyRequest): Promise<{ backupCodes: string[] }> {
    return api.post<{ backupCodes: string[] }>(`${this.basePath}/2fa/backup-codes`, request)
  }

  /**
   * Map backend user response to frontend User type
   */
  mapToUser(response: UserResponse): User {
    return {
      id: response.id,
      email: response.email,
      firstName: response.firstName,
      lastName: response.lastName,
      displayName: response.displayName,
      avatarUrl: response.avatarUrl,
      role: response.role as User['role'],
      teamId: response.teamId,
      teamName: response.teamName,
      isActive: response.isActive,
      createdAt: response.createdAt,
      lastLoginAt: response.lastLoginAt
    }
  }
}

export const authService = new AuthService()
export default authService
