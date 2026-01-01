import { defineStore } from 'pinia'
import { ref } from 'vue'
import api from '@/services/api'
import type {
  UserProfile,
  UserStats,
  UpdateProfileRequest,
  ChangePasswordRequest,
  ApiKeyInfo,
  ProfileApiKeyRequest,
  ProfileApiKeyResponse
} from '@/types/profile'

export const useProfileStore = defineStore('profile', () => {
  // State
  const profile = ref<UserProfile | null>(null)
  const stats = ref<UserStats | null>(null)
  const apiKeys = ref<ApiKeyInfo[]>([])
  const isLoading = ref(false)
  const error = ref<string | null>(null)

  // Actions
  async function fetchProfile(): Promise<UserProfile | null> {
    isLoading.value = true
    error.value = null
    try {
      const response = await api.get<UserProfile>('/users/me')
      profile.value = response
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch profile'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function updateProfile(request: UpdateProfileRequest): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      const response = await api.put<UserProfile>('/users/me', request)
      profile.value = response
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to update profile'
      return false
    } finally {
      isLoading.value = false
    }
  }

  async function uploadAvatar(file: File): Promise<string | null> {
    isLoading.value = true
    error.value = null
    try {
      const formData = new FormData()
      formData.append('avatar', file)

      const response = await api.getClient().post<{ url: string }>(
        '/users/me/avatar',
        formData,
        { headers: { 'Content-Type': 'multipart/form-data' } }
      )

      if (profile.value) {
        profile.value.avatar = response.data.url
      }
      return response.data.url
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to upload avatar'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function removeAvatar(): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await api.delete('/users/me/avatar')
      if (profile.value) {
        profile.value.avatar = undefined
      }
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to remove avatar'
      return false
    } finally {
      isLoading.value = false
    }
  }

  async function changePassword(request: ChangePasswordRequest): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await api.post('/users/me/change-password', request)
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to change password'
      return false
    } finally {
      isLoading.value = false
    }
  }

  async function fetchStats(): Promise<UserStats | null> {
    isLoading.value = true
    error.value = null
    try {
      const response = await api.get<UserStats>('/users/me/stats')
      stats.value = response
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch stats'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function fetchApiKeys(): Promise<ApiKeyInfo[]> {
    isLoading.value = true
    error.value = null
    try {
      const response = await api.get<ApiKeyInfo[]>('/users/me/api-keys')
      apiKeys.value = response
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch API keys'
      return []
    } finally {
      isLoading.value = false
    }
  }

  async function createApiKey(request: ProfileApiKeyRequest): Promise<ProfileApiKeyResponse | null> {
    isLoading.value = true
    error.value = null
    try {
      const response = await api.post<ProfileApiKeyResponse>('/users/me/api-keys', request)
      await fetchApiKeys()
      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to create API key'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function revokeApiKey(keyId: string): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await api.delete(`/users/me/api-keys/${keyId}`)
      apiKeys.value = apiKeys.value.filter(k => k.id !== keyId)
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to revoke API key'
      return false
    } finally {
      isLoading.value = false
    }
  }

  async function deleteAccount(): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await api.delete('/users/me')
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to delete account'
      return false
    } finally {
      isLoading.value = false
    }
  }

  function clearError() {
    error.value = null
  }

  function reset() {
    profile.value = null
    stats.value = null
    apiKeys.value = []
  }

  return {
    // State
    profile,
    stats,
    apiKeys,
    isLoading,
    error,

    // Actions
    fetchProfile,
    updateProfile,
    uploadAvatar,
    removeAvatar,
    changePassword,
    fetchStats,
    fetchApiKeys,
    createApiKey,
    revokeApiKey,
    deleteAccount,
    clearError,
    reset
  }
})
