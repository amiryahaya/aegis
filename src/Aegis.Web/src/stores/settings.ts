import { defineStore } from 'pinia'
import { ref, watch } from 'vue'
import api from '@/services/api'
import type { UserSettings, ThemeMode, NotificationSettings, PrivacySettings } from '@/types/admin'

const DEFAULT_SETTINGS: UserSettings = {
  theme: 'system',
  language: 'en',
  timezone: Intl.DateTimeFormat().resolvedOptions().timeZone,
  dateFormat: 'MMM d, yyyy',
  notifications: {
    email: true,
    inApp: true,
    queryCompleted: true,
    documentProcessed: true,
    systemAlerts: true,
    weeklyDigest: false,
    quietHoursEnabled: false
  },
  privacy: {
    shareUsageData: true,
    showActivityStatus: true,
    allowMentions: true
  }
}

export const useSettingsStore = defineStore('settings', () => {
  // State
  const settings = ref<UserSettings>(loadLocalSettings())
  const isLoading = ref(false)
  const error = ref<string | null>(null)
  const hasUnsavedChanges = ref(false)

  // Load settings from localStorage on init
  function loadLocalSettings(): UserSettings {
    const stored = localStorage.getItem('userSettings')
    if (stored) {
      try {
        return { ...DEFAULT_SETTINGS, ...JSON.parse(stored) }
      } catch {
        return DEFAULT_SETTINGS
      }
    }
    return DEFAULT_SETTINGS
  }

  // Save settings to localStorage
  function saveLocalSettings() {
    localStorage.setItem('userSettings', JSON.stringify(settings.value))
  }

  // Watch for theme changes and apply
  watch(() => settings.value.theme, (newTheme) => {
    applyTheme(newTheme)
  }, { immediate: true })

  function applyTheme(theme: ThemeMode) {
    const isDark = theme === 'dark' ||
      (theme === 'system' && window.matchMedia('(prefers-color-scheme: dark)').matches)

    document.documentElement.classList.toggle('dark', isDark)
    localStorage.setItem('theme', isDark ? 'dark' : 'light')
  }

  // Actions
  async function fetchSettings(): Promise<UserSettings | null> {
    isLoading.value = true
    error.value = null
    try {
      const response = await api.get<UserSettings>('/preferences')
      settings.value = { ...DEFAULT_SETTINGS, ...response }
      saveLocalSettings()
      hasUnsavedChanges.value = false
      return settings.value
    } catch (err) {
      // Use local settings if API fails
      error.value = err instanceof Error ? err.message : 'Failed to fetch settings'
      return settings.value
    } finally {
      isLoading.value = false
    }
  }

  async function saveSettings(): Promise<boolean> {
    isLoading.value = true
    error.value = null
    try {
      await api.put('/preferences', settings.value)
      saveLocalSettings()
      hasUnsavedChanges.value = false
      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to save settings'
      return false
    } finally {
      isLoading.value = false
    }
  }

  function updateTheme(theme: ThemeMode) {
    settings.value.theme = theme
    saveLocalSettings()
    hasUnsavedChanges.value = true
  }

  function updateLanguage(language: string) {
    settings.value.language = language
    saveLocalSettings()
    hasUnsavedChanges.value = true
  }

  function updateTimezone(timezone: string) {
    settings.value.timezone = timezone
    saveLocalSettings()
    hasUnsavedChanges.value = true
  }

  function updateDateFormat(format: string) {
    settings.value.dateFormat = format
    saveLocalSettings()
    hasUnsavedChanges.value = true
  }

  function updateNotificationSettings(notifications: Partial<NotificationSettings>) {
    settings.value.notifications = { ...settings.value.notifications, ...notifications }
    saveLocalSettings()
    hasUnsavedChanges.value = true
  }

  function updatePrivacySettings(privacy: Partial<PrivacySettings>) {
    settings.value.privacy = { ...settings.value.privacy, ...privacy }
    saveLocalSettings()
    hasUnsavedChanges.value = true
  }

  function resetToDefaults() {
    settings.value = { ...DEFAULT_SETTINGS }
    saveLocalSettings()
    hasUnsavedChanges.value = true
  }

  function clearError() {
    error.value = null
  }

  return {
    // State
    settings,
    isLoading,
    error,
    hasUnsavedChanges,

    // Actions
    fetchSettings,
    saveSettings,
    updateTheme,
    updateLanguage,
    updateTimezone,
    updateDateFormat,
    updateNotificationSettings,
    updatePrivacySettings,
    resetToDefaults,
    clearError
  }
})
