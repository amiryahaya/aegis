<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import {
  GlobeAltIcon,
  PhotoIcon,
  TrashIcon
} from '@heroicons/vue/24/outline'
import type { GeneralSettings } from '@/types'

const props = defineProps<{
  settings: GeneralSettings | null
  isLoading?: boolean
  isSaving?: boolean
}>()

const emit = defineEmits<{
  update: [settings: Partial<GeneralSettings>]
}>()

const localSettings = ref<GeneralSettings | null>(null)

watch(() => props.settings, (settings) => {
  if (settings) {
    localSettings.value = JSON.parse(JSON.stringify(settings))
  }
}, { immediate: true, deep: true })

const hasChanges = computed(() => {
  if (!props.settings || !localSettings.value) return false
  return JSON.stringify(props.settings) !== JSON.stringify(localSettings.value)
})

function handleSave() {
  if (localSettings.value && hasChanges.value) {
    emit('update', localSettings.value)
  }
}

function handleReset() {
  if (props.settings) {
    localSettings.value = JSON.parse(JSON.stringify(props.settings))
  }
}

const languages = [
  { value: 'en', label: 'English' },
  { value: 'es', label: 'Spanish' },
  { value: 'fr', label: 'French' },
  { value: 'de', label: 'German' },
  { value: 'zh', label: 'Chinese' },
  { value: 'ja', label: 'Japanese' }
]

const timezones = [
  { value: 'UTC', label: 'UTC' },
  { value: 'America/New_York', label: 'Eastern Time (ET)' },
  { value: 'America/Chicago', label: 'Central Time (CT)' },
  { value: 'America/Denver', label: 'Mountain Time (MT)' },
  { value: 'America/Los_Angeles', label: 'Pacific Time (PT)' },
  { value: 'Europe/London', label: 'London (GMT)' },
  { value: 'Europe/Paris', label: 'Paris (CET)' },
  { value: 'Asia/Tokyo', label: 'Tokyo (JST)' },
  { value: 'Asia/Singapore', label: 'Singapore (SGT)' },
  { value: 'Australia/Sydney', label: 'Sydney (AEST)' }
]

const dateFormats = [
  { value: 'YYYY-MM-DD', label: 'YYYY-MM-DD (2024-01-15)' },
  { value: 'MM/DD/YYYY', label: 'MM/DD/YYYY (01/15/2024)' },
  { value: 'DD/MM/YYYY', label: 'DD/MM/YYYY (15/01/2024)' },
  { value: 'DD.MM.YYYY', label: 'DD.MM.YYYY (15.01.2024)' },
  { value: 'MMM DD, YYYY', label: 'MMM DD, YYYY (Jan 15, 2024)' }
]

const timeFormats = [
  { value: 'HH:mm:ss', label: '24-hour (14:30:00)' },
  { value: 'hh:mm:ss A', label: '12-hour (02:30:00 PM)' },
  { value: 'HH:mm', label: '24-hour short (14:30)' },
  { value: 'hh:mm A', label: '12-hour short (02:30 PM)' }
]

const themes = [
  { value: 'light', label: 'Light' },
  { value: 'dark', label: 'Dark' },
  { value: 'system', label: 'System' }
]
</script>

<template>
  <div class="space-y-6">
    <!-- Loading state -->
    <div v-if="isLoading" class="animate-pulse space-y-4">
      <div v-for="i in 3" :key="i" class="h-32 rounded-lg bg-gray-200 dark:bg-gray-700" />
    </div>

    <template v-else-if="localSettings">
      <!-- Site Information -->
      <div class="rounded-lg border bg-white p-6 dark:border-gray-700 dark:bg-gray-800">
        <div class="mb-4 flex items-center gap-3">
          <div class="flex h-10 w-10 items-center justify-center rounded-lg bg-aegis-100 dark:bg-aegis-900/30">
            <GlobeAltIcon class="h-5 w-5 text-aegis-600 dark:text-aegis-400" />
          </div>
          <div>
            <h3 class="font-medium text-gray-900 dark:text-white">Site Information</h3>
            <p class="text-sm text-gray-500 dark:text-gray-400">Basic site settings and branding</p>
          </div>
        </div>

        <div class="grid gap-4 md:grid-cols-2">
          <div>
            <label class="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Site Name</label>
            <input
              v-model="localSettings.siteName"
              type="text"
              class="w-full rounded-lg border-gray-300 text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
              placeholder="AEGIS RAG"
            />
          </div>
          <div>
            <label class="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Site URL</label>
            <input
              v-model="localSettings.siteUrl"
              type="url"
              class="w-full rounded-lg border-gray-300 text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
              placeholder="https://aegis.example.com"
            />
          </div>
          <div>
            <label class="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Admin Email</label>
            <input
              v-model="localSettings.adminEmail"
              type="email"
              class="w-full rounded-lg border-gray-300 text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
              placeholder="admin@example.com"
            />
          </div>
          <div>
            <label class="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Support Email</label>
            <input
              v-model="localSettings.supportEmail"
              type="email"
              class="w-full rounded-lg border-gray-300 text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
              placeholder="support@example.com"
            />
          </div>
        </div>
      </div>

      <!-- Branding -->
      <div class="rounded-lg border bg-white p-6 dark:border-gray-700 dark:bg-gray-800">
        <div class="mb-4 flex items-center gap-3">
          <div class="flex h-10 w-10 items-center justify-center rounded-lg bg-purple-100 dark:bg-purple-900/30">
            <PhotoIcon class="h-5 w-5 text-purple-600 dark:text-purple-400" />
          </div>
          <div>
            <h3 class="font-medium text-gray-900 dark:text-white">Branding</h3>
            <p class="text-sm text-gray-500 dark:text-gray-400">Logo, favicon, and theme settings</p>
          </div>
        </div>

        <div class="grid gap-6 md:grid-cols-2">
          <!-- Logo -->
          <div>
            <label class="mb-2 block text-sm font-medium text-gray-700 dark:text-gray-300">Logo</label>
            <div class="flex items-center gap-4">
              <div class="flex h-16 w-16 items-center justify-center rounded-lg border-2 border-dashed border-gray-300 dark:border-gray-600">
                <img
                  v-if="localSettings.logo"
                  :src="localSettings.logo"
                  alt="Logo"
                  class="h-12 w-12 object-contain"
                />
                <PhotoIcon v-else class="h-8 w-8 text-gray-400" />
              </div>
              <div class="flex flex-col gap-2">
                <button
                  type="button"
                  class="rounded-lg bg-gray-100 px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
                >
                  Upload
                </button>
                <button
                  v-if="localSettings.logo"
                  type="button"
                  class="inline-flex items-center gap-1 text-sm text-red-600 hover:text-red-700"
                  @click="localSettings.logo = null"
                >
                  <TrashIcon class="h-4 w-4" />
                  Remove
                </button>
              </div>
            </div>
          </div>

          <!-- Favicon -->
          <div>
            <label class="mb-2 block text-sm font-medium text-gray-700 dark:text-gray-300">Favicon</label>
            <div class="flex items-center gap-4">
              <div class="flex h-16 w-16 items-center justify-center rounded-lg border-2 border-dashed border-gray-300 dark:border-gray-600">
                <img
                  v-if="localSettings.favicon"
                  :src="localSettings.favicon"
                  alt="Favicon"
                  class="h-8 w-8 object-contain"
                />
                <PhotoIcon v-else class="h-8 w-8 text-gray-400" />
              </div>
              <div class="flex flex-col gap-2">
                <button
                  type="button"
                  class="rounded-lg bg-gray-100 px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
                >
                  Upload
                </button>
                <button
                  v-if="localSettings.favicon"
                  type="button"
                  class="inline-flex items-center gap-1 text-sm text-red-600 hover:text-red-700"
                  @click="localSettings.favicon = null"
                >
                  <TrashIcon class="h-4 w-4" />
                  Remove
                </button>
              </div>
            </div>
          </div>

          <!-- Theme -->
          <div class="md:col-span-2">
            <label class="mb-2 block text-sm font-medium text-gray-700 dark:text-gray-300">Default Theme</label>
            <div class="flex gap-3">
              <button
                v-for="theme in themes"
                :key="theme.value"
                type="button"
                :class="[
                  'rounded-lg px-4 py-2 text-sm font-medium transition-colors',
                  localSettings.theme === theme.value
                    ? 'bg-aegis-100 text-aegis-700 ring-2 ring-aegis-500 dark:bg-aegis-900/30 dark:text-aegis-300'
                    : 'bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600'
                ]"
                @click="localSettings.theme = theme.value as 'light' | 'dark' | 'system'"
              >
                {{ theme.label }}
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Localization -->
      <div class="rounded-lg border bg-white p-6 dark:border-gray-700 dark:bg-gray-800">
        <h3 class="mb-4 font-medium text-gray-900 dark:text-white">Localization</h3>

        <div class="grid gap-4 md:grid-cols-2">
          <div>
            <label class="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Default Language</label>
            <select
              v-model="localSettings.defaultLanguage"
              class="w-full rounded-lg border-gray-300 text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
            >
              <option v-for="lang in languages" :key="lang.value" :value="lang.value">
                {{ lang.label }}
              </option>
            </select>
          </div>
          <div>
            <label class="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Default Timezone</label>
            <select
              v-model="localSettings.defaultTimezone"
              class="w-full rounded-lg border-gray-300 text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
            >
              <option v-for="tz in timezones" :key="tz.value" :value="tz.value">
                {{ tz.label }}
              </option>
            </select>
          </div>
          <div>
            <label class="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Date Format</label>
            <select
              v-model="localSettings.dateFormat"
              class="w-full rounded-lg border-gray-300 text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
            >
              <option v-for="format in dateFormats" :key="format.value" :value="format.value">
                {{ format.label }}
              </option>
            </select>
          </div>
          <div>
            <label class="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Time Format</label>
            <select
              v-model="localSettings.timeFormat"
              class="w-full rounded-lg border-gray-300 text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
            >
              <option v-for="format in timeFormats" :key="format.value" :value="format.value">
                {{ format.label }}
              </option>
            </select>
          </div>
        </div>
      </div>

      <!-- Save/Reset Buttons -->
      <div class="flex justify-end gap-3">
        <button
          type="button"
          class="rounded-lg border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50 dark:border-gray-600 dark:bg-gray-800 dark:text-gray-300 dark:hover:bg-gray-700"
          :disabled="!hasChanges || isSaving"
          @click="handleReset"
        >
          Reset
        </button>
        <button
          type="button"
          class="rounded-lg bg-aegis-600 px-4 py-2 text-sm font-medium text-white hover:bg-aegis-700 disabled:opacity-50"
          :disabled="!hasChanges || isSaving"
          @click="handleSave"
        >
          {{ isSaving ? 'Saving...' : 'Save Changes' }}
        </button>
      </div>
    </template>
  </div>
</template>
