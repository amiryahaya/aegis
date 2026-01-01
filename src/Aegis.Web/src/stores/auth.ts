import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { User, LoginRequest, LoginResponse } from '@/types'
import { UserRole } from '@/types'
import api from '@/services/api'

export const useAuthStore = defineStore('auth', () => {
  // State
  const user = ref<User | null>(null)
  const token = ref<string | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  // Getters
  const isAuthenticated = computed(() => !!token.value && !!user.value)
  const userName = computed(() => user.value?.name || 'User')
  const userRole = computed(() => user.value?.role || 'Viewer')
  const isAdmin = computed(() =>
    user.value?.role === UserRole.Admin || user.value?.role === UserRole.SystemAdmin
  )

  // Initialize from localStorage
  function initialize() {
    const storedToken = localStorage.getItem('token')
    const storedUser = localStorage.getItem('user')

    if (storedToken && storedUser) {
      token.value = storedToken
      try {
        user.value = JSON.parse(storedUser)
      } catch {
        logout()
      }
    }
  }

  // Actions
  async function login(credentials: LoginRequest): Promise<boolean> {
    loading.value = true
    error.value = null

    try {
      const response = await api.post<LoginResponse>('/auth/login', credentials)

      token.value = response.token
      user.value = response.user

      localStorage.setItem('token', response.token)
      localStorage.setItem('user', JSON.stringify(response.user))

      return true
    } catch (err) {
      error.value = (err as { detail?: string }).detail || 'Login failed'
      return false
    } finally {
      loading.value = false
    }
  }

  function logout() {
    token.value = null
    user.value = null
    localStorage.removeItem('token')
    localStorage.removeItem('user')
  }

  async function refreshUser(): Promise<void> {
    if (!token.value) return

    try {
      const response = await api.get<User>('/users/me')
      user.value = response
      localStorage.setItem('user', JSON.stringify(response))
    } catch {
      logout()
    }
  }

  // Initialize on store creation
  initialize()

  return {
    // State
    user,
    token,
    loading,
    error,
    // Getters
    isAuthenticated,
    userName,
    userRole,
    isAdmin,
    // Actions
    login,
    logout,
    refreshUser,
    initialize
  }
})
