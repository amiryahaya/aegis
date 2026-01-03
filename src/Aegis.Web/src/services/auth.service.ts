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
