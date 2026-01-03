import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { User } from '@/types'
import { UserRole } from '@/types'
import authService from '@/services/auth.service'
import type { LoginRequest, RegisterRequest } from '@/services/auth.service'

// Re-export for convenience
export type { LoginRequest, RegisterRequest }

const TOKEN_KEY = 'aegis_token'
const REFRESH_TOKEN_KEY = 'aegis_refresh_token'
const USER_KEY = 'aegis_user'
const TOKEN_EXPIRY_KEY = 'aegis_token_expiry'

export const useAuthStore = defineStore('auth', () => {
  // State
  const user = ref<User | null>(null)
  const token = ref<string | null>(null)
  const refreshToken = ref<string | null>(null)
  const tokenExpiry = ref<Date | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const isRefreshing = ref(false)

  // Getters
  const isAuthenticated = computed(() => !!token.value && !!user.value)
  const userName = computed(() => user.value?.displayName || user.value?.firstName || 'User')
  const userRole = computed(() => user.value?.role || 'Viewer')
  const isAdmin = computed(() =>
    user.value?.role === UserRole.Admin || user.value?.role === UserRole.SystemAdmin
  )
  const isTokenExpired = computed(() => {
    if (!tokenExpiry.value) return true
    // Consider token expired 1 minute before actual expiry
    return new Date() >= new Date(tokenExpiry.value.getTime() - 60000)
  })

  // Initialize from localStorage
  function initialize() {
    const storedToken = localStorage.getItem(TOKEN_KEY)
    const storedRefreshToken = localStorage.getItem(REFRESH_TOKEN_KEY)
    const storedUser = localStorage.getItem(USER_KEY)
    const storedExpiry = localStorage.getItem(TOKEN_EXPIRY_KEY)

    if (storedToken && storedUser) {
      token.value = storedToken
      refreshToken.value = storedRefreshToken

      if (storedExpiry) {
        tokenExpiry.value = new Date(storedExpiry)
      }

      try {
        user.value = JSON.parse(storedUser)
      } catch {
        logout()
      }
    }
  }

  // Persist auth state
  function persistAuthState() {
    if (token.value) {
      localStorage.setItem(TOKEN_KEY, token.value)
    }
    if (refreshToken.value) {
      localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken.value)
    }
    if (user.value) {
      localStorage.setItem(USER_KEY, JSON.stringify(user.value))
    }
    if (tokenExpiry.value) {
      localStorage.setItem(TOKEN_EXPIRY_KEY, tokenExpiry.value.toISOString())
    }
  }

  // Clear auth state
  function clearAuthState() {
    token.value = null
    refreshToken.value = null
    user.value = null
    tokenExpiry.value = null
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(REFRESH_TOKEN_KEY)
    localStorage.removeItem(USER_KEY)
    localStorage.removeItem(TOKEN_EXPIRY_KEY)
  }

  // Actions
  async function login(credentials: LoginRequest): Promise<boolean> {
    loading.value = true
    error.value = null

    try {
      const response = await authService.login(credentials)

      token.value = response.token
      refreshToken.value = response.refreshToken
      tokenExpiry.value = new Date(response.expiresAt)
      user.value = authService.mapToUser(response.user)

      persistAuthState()

      return true
    } catch (err) {
      error.value = (err as { detail?: string }).detail || 'Login failed. Please check your credentials.'
      return false
    } finally {
      loading.value = false
    }
  }

  async function register(request: RegisterRequest): Promise<boolean> {
    loading.value = true
    error.value = null

    try {
      await authService.register(request)
      // After registration, user needs to login
      return true
    } catch (err) {
      error.value = (err as { detail?: string }).detail || 'Registration failed. Please try again.'
      return false
    } finally {
      loading.value = false
    }
  }

  async function logout(): Promise<void> {
    try {
      await authService.logout()
    } catch {
      // Ignore logout errors - we'll clear local state anyway
    } finally {
      clearAuthState()
    }
  }

  async function refreshAccessToken(): Promise<boolean> {
    if (!refreshToken.value || isRefreshing.value) {
      return false
    }

    isRefreshing.value = true

    try {
      const response = await authService.refreshToken({
        refreshToken: refreshToken.value
      })

      token.value = response.token
      refreshToken.value = response.refreshToken
      tokenExpiry.value = new Date(response.expiresAt)

      persistAuthState()

      return true
    } catch {
      // Refresh failed - clear auth state
      clearAuthState()
      return false
    } finally {
      isRefreshing.value = false
    }
  }

  async function fetchCurrentUser(): Promise<void> {
    if (!token.value) return

    try {
      const response = await authService.getCurrentUser()
      user.value = authService.mapToUser(response)
      persistAuthState()
    } catch {
      // Failed to fetch user - token might be invalid
      logout()
    }
  }

  async function updateProfile(data: Partial<User>): Promise<boolean> {
    loading.value = true
    error.value = null

    try {
      const response = await authService.updateProfile(data)
      user.value = authService.mapToUser(response)
      persistAuthState()
      return true
    } catch (err) {
      error.value = (err as { detail?: string }).detail || 'Failed to update profile.'
      return false
    } finally {
      loading.value = false
    }
  }

  async function changePassword(currentPassword: string, newPassword: string): Promise<boolean> {
    loading.value = true
    error.value = null

    try {
      await authService.changePassword({ currentPassword, newPassword })
      return true
    } catch (err) {
      error.value = (err as { detail?: string }).detail || 'Failed to change password.'
      return false
    } finally {
      loading.value = false
    }
  }

  // Check and refresh token if needed
  async function ensureValidToken(): Promise<boolean> {
    if (!token.value) return false

    if (isTokenExpired.value && refreshToken.value) {
      return await refreshAccessToken()
    }

    return true
  }

  function clearError() {
    error.value = null
  }

  // Initialize on store creation
  initialize()

  return {
    // State
    user,
    token,
    refreshToken,
    tokenExpiry,
    loading,
    error,
    isRefreshing,

    // Getters
    isAuthenticated,
    userName,
    userRole,
    isAdmin,
    isTokenExpired,

    // Actions
    login,
    register,
    logout,
    refreshAccessToken,
    fetchCurrentUser,
    updateProfile,
    changePassword,
    ensureValidToken,
    initialize,
    clearError
  }
})
