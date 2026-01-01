<script setup lang="ts">
import { onMounted, computed } from 'vue'
import { useSettingsStore } from '@/stores/settings'
import { useAuthStore } from '@/stores/auth'
import {
  SunIcon,
  MoonIcon,
  ComputerDesktopIcon,
  BellIcon,
  ShieldCheckIcon,
  UserCircleIcon,
  KeyIcon
} from '@heroicons/vue/24/outline'
import {
  Switch,
  TabGroup,
  TabList,
  Tab,
  TabPanels,
  TabPanel
} from '@headlessui/vue'
import type { ThemeMode } from '@/types/admin'

const settingsStore = useSettingsStore()
const authStore = useAuthStore()

const themeOptions: { value: ThemeMode; label: string; icon: typeof SunIcon }[] = [
  { value: 'light', label: 'Light', icon: SunIcon },
  { value: 'dark', label: 'Dark', icon: MoonIcon },
  { value: 'system', label: 'System', icon: ComputerDesktopIcon }
]

const timezones = [
  'America/New_York',
  'America/Chicago',
  'America/Denver',
  'America/Los_Angeles',
  'Europe/London',
  'Europe/Paris',
  'Asia/Tokyo',
  'Asia/Shanghai',
  'Australia/Sydney',
  'Pacific/Auckland'
]

const dateFormats = [
  { value: 'MMM d, yyyy', label: 'Jan 1, 2025' },
  { value: 'dd/MM/yyyy', label: '01/01/2025' },
  { value: 'MM/dd/yyyy', label: '01/01/2025' },
  { value: 'yyyy-MM-dd', label: '2025-01-01' }
]

onMounted(() => {
  settingsStore.fetchSettings()
})

async function handleSave() {
  await settingsStore.saveSettings()
}

const currentTheme = computed(() => settingsStore.settings.theme)
</script>

<template>
  <div class="h-full overflow-auto">
    <div class="mx-auto max-w-4xl p-6">
      <!-- Header -->
      <div class="mb-8">
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Settings</h1>
        <p class="mt-1 text-gray-500 dark:text-gray-400">
          Manage your account preferences and configuration
        </p>
      </div>

      <!-- Tabs -->
      <TabGroup>
        <TabList class="flex space-x-1 rounded-xl bg-gray-100 p-1 dark:bg-gray-800">
          <Tab
            v-slot="{ selected }"
            as="template"
          >
            <button
              class="flex w-full items-center justify-center gap-2 rounded-lg py-2.5 text-sm font-medium leading-5 transition-all"
              :class="selected
                ? 'bg-white text-aegis-700 shadow dark:bg-gray-700 dark:text-aegis-400'
                : 'text-gray-600 hover:bg-white/30 hover:text-gray-800 dark:text-gray-400 dark:hover:text-gray-200'"
            >
              <UserCircleIcon class="h-5 w-5" />
              Profile
            </button>
          </Tab>
          <Tab
            v-slot="{ selected }"
            as="template"
          >
            <button
              class="flex w-full items-center justify-center gap-2 rounded-lg py-2.5 text-sm font-medium leading-5 transition-all"
              :class="selected
                ? 'bg-white text-aegis-700 shadow dark:bg-gray-700 dark:text-aegis-400'
                : 'text-gray-600 hover:bg-white/30 hover:text-gray-800 dark:text-gray-400 dark:hover:text-gray-200'"
            >
              <SunIcon class="h-5 w-5" />
              Appearance
            </button>
          </Tab>
          <Tab
            v-slot="{ selected }"
            as="template"
          >
            <button
              class="flex w-full items-center justify-center gap-2 rounded-lg py-2.5 text-sm font-medium leading-5 transition-all"
              :class="selected
                ? 'bg-white text-aegis-700 shadow dark:bg-gray-700 dark:text-aegis-400'
                : 'text-gray-600 hover:bg-white/30 hover:text-gray-800 dark:text-gray-400 dark:hover:text-gray-200'"
            >
              <BellIcon class="h-5 w-5" />
              Notifications
            </button>
          </Tab>
          <Tab
            v-slot="{ selected }"
            as="template"
          >
            <button
              class="flex w-full items-center justify-center gap-2 rounded-lg py-2.5 text-sm font-medium leading-5 transition-all"
              :class="selected
                ? 'bg-white text-aegis-700 shadow dark:bg-gray-700 dark:text-aegis-400'
                : 'text-gray-600 hover:bg-white/30 hover:text-gray-800 dark:text-gray-400 dark:hover:text-gray-200'"
            >
              <ShieldCheckIcon class="h-5 w-5" />
              Privacy
            </button>
          </Tab>
          <Tab
            v-slot="{ selected }"
            as="template"
          >
            <button
              class="flex w-full items-center justify-center gap-2 rounded-lg py-2.5 text-sm font-medium leading-5 transition-all"
              :class="selected
                ? 'bg-white text-aegis-700 shadow dark:bg-gray-700 dark:text-aegis-400'
                : 'text-gray-600 hover:bg-white/30 hover:text-gray-800 dark:text-gray-400 dark:hover:text-gray-200'"
            >
              <KeyIcon class="h-5 w-5" />
              API Keys
            </button>
          </Tab>
        </TabList>

        <TabPanels class="mt-6">
          <!-- Profile Tab -->
          <TabPanel>
            <div class="card space-y-6 p-6">
              <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Profile Information</h2>

              <div class="grid gap-6 sm:grid-cols-2">
                <div>
                  <label class="label">Name</label>
                  <input
                    type="text"
                    :value="authStore.user?.name"
                    class="input w-full"
                    disabled
                  />
                </div>

                <div>
                  <label class="label">Email</label>
                  <input
                    type="email"
                    :value="authStore.user?.email"
                    class="input w-full"
                    disabled
                  />
                </div>

                <div>
                  <label class="label">Role</label>
                  <input
                    type="text"
                    :value="authStore.user?.role"
                    class="input w-full"
                    disabled
                  />
                </div>

                <div>
                  <label class="label">Member Since</label>
                  <input
                    type="text"
                    :value="authStore.user?.createdAt ? new Date(authStore.user.createdAt).toLocaleDateString() : 'N/A'"
                    class="input w-full"
                    disabled
                  />
                </div>
              </div>

              <div class="border-t border-gray-200 pt-6 dark:border-gray-700">
                <h3 class="text-md font-medium text-gray-900 dark:text-white">Timezone & Locale</h3>

                <div class="mt-4 grid gap-6 sm:grid-cols-2">
                  <div>
                    <label class="label">Timezone</label>
                    <select
                      :value="settingsStore.settings.timezone"
                      class="input w-full"
                      @change="settingsStore.updateTimezone(($event.target as HTMLSelectElement).value)"
                    >
                      <option v-for="tz in timezones" :key="tz" :value="tz">
                        {{ tz }}
                      </option>
                    </select>
                  </div>

                  <div>
                    <label class="label">Date Format</label>
                    <select
                      :value="settingsStore.settings.dateFormat"
                      class="input w-full"
                      @change="settingsStore.updateDateFormat(($event.target as HTMLSelectElement).value)"
                    >
                      <option v-for="format in dateFormats" :key="format.value" :value="format.value">
                        {{ format.label }}
                      </option>
                    </select>
                  </div>
                </div>
              </div>
            </div>
          </TabPanel>

          <!-- Appearance Tab -->
          <TabPanel>
            <div class="card space-y-6 p-6">
              <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Appearance</h2>

              <div>
                <label class="label mb-3">Theme</label>
                <div class="flex gap-4">
                  <button
                    v-for="option in themeOptions"
                    :key="option.value"
                    class="flex flex-1 flex-col items-center gap-2 rounded-lg border-2 p-4 transition-all"
                    :class="currentTheme === option.value
                      ? 'border-aegis-500 bg-aegis-50 dark:bg-aegis-900/20'
                      : 'border-gray-200 hover:border-gray-300 dark:border-gray-700 dark:hover:border-gray-600'"
                    @click="settingsStore.updateTheme(option.value)"
                  >
                    <component
                      :is="option.icon"
                      class="h-8 w-8"
                      :class="currentTheme === option.value
                        ? 'text-aegis-600 dark:text-aegis-400'
                        : 'text-gray-400'"
                    />
                    <span
                      class="text-sm font-medium"
                      :class="currentTheme === option.value
                        ? 'text-aegis-700 dark:text-aegis-300'
                        : 'text-gray-600 dark:text-gray-400'"
                    >
                      {{ option.label }}
                    </span>
                  </button>
                </div>
              </div>
            </div>
          </TabPanel>

          <!-- Notifications Tab -->
          <TabPanel>
            <div class="card space-y-6 p-6">
              <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Notification Preferences</h2>

              <div class="space-y-4">
                <div class="flex items-center justify-between">
                  <div>
                    <p class="font-medium text-gray-900 dark:text-white">Email Notifications</p>
                    <p class="text-sm text-gray-500 dark:text-gray-400">Receive notifications via email</p>
                  </div>
                  <Switch
                    :model-value="settingsStore.settings.notifications.email"
                    class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors"
                    :class="settingsStore.settings.notifications.email ? 'bg-aegis-600' : 'bg-gray-300 dark:bg-gray-600'"
                    @update:model-value="settingsStore.updateNotificationSettings({ email: $event })"
                  >
                    <span
                      class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform"
                      :class="settingsStore.settings.notifications.email ? 'translate-x-6' : 'translate-x-1'"
                    />
                  </Switch>
                </div>

                <div class="flex items-center justify-between">
                  <div>
                    <p class="font-medium text-gray-900 dark:text-white">In-App Notifications</p>
                    <p class="text-sm text-gray-500 dark:text-gray-400">Show notifications in the app</p>
                  </div>
                  <Switch
                    :model-value="settingsStore.settings.notifications.inApp"
                    class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors"
                    :class="settingsStore.settings.notifications.inApp ? 'bg-aegis-600' : 'bg-gray-300 dark:bg-gray-600'"
                    @update:model-value="settingsStore.updateNotificationSettings({ inApp: $event })"
                  >
                    <span
                      class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform"
                      :class="settingsStore.settings.notifications.inApp ? 'translate-x-6' : 'translate-x-1'"
                    />
                  </Switch>
                </div>

                <div class="border-t border-gray-200 pt-4 dark:border-gray-700">
                  <h3 class="mb-4 text-md font-medium text-gray-900 dark:text-white">Notification Types</h3>

                  <div class="space-y-4">
                    <div class="flex items-center justify-between">
                      <div>
                        <p class="font-medium text-gray-900 dark:text-white">Query Completed</p>
                        <p class="text-sm text-gray-500 dark:text-gray-400">When a query finishes processing</p>
                      </div>
                      <Switch
                        :model-value="settingsStore.settings.notifications.queryCompleted"
                        class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors"
                        :class="settingsStore.settings.notifications.queryCompleted ? 'bg-aegis-600' : 'bg-gray-300 dark:bg-gray-600'"
                        @update:model-value="settingsStore.updateNotificationSettings({ queryCompleted: $event })"
                      >
                        <span
                          class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform"
                          :class="settingsStore.settings.notifications.queryCompleted ? 'translate-x-6' : 'translate-x-1'"
                        />
                      </Switch>
                    </div>

                    <div class="flex items-center justify-between">
                      <div>
                        <p class="font-medium text-gray-900 dark:text-white">Document Processed</p>
                        <p class="text-sm text-gray-500 dark:text-gray-400">When documents finish indexing</p>
                      </div>
                      <Switch
                        :model-value="settingsStore.settings.notifications.documentProcessed"
                        class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors"
                        :class="settingsStore.settings.notifications.documentProcessed ? 'bg-aegis-600' : 'bg-gray-300 dark:bg-gray-600'"
                        @update:model-value="settingsStore.updateNotificationSettings({ documentProcessed: $event })"
                      >
                        <span
                          class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform"
                          :class="settingsStore.settings.notifications.documentProcessed ? 'translate-x-6' : 'translate-x-1'"
                        />
                      </Switch>
                    </div>

                    <div class="flex items-center justify-between">
                      <div>
                        <p class="font-medium text-gray-900 dark:text-white">System Alerts</p>
                        <p class="text-sm text-gray-500 dark:text-gray-400">Important system notifications</p>
                      </div>
                      <Switch
                        :model-value="settingsStore.settings.notifications.systemAlerts"
                        class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors"
                        :class="settingsStore.settings.notifications.systemAlerts ? 'bg-aegis-600' : 'bg-gray-300 dark:bg-gray-600'"
                        @update:model-value="settingsStore.updateNotificationSettings({ systemAlerts: $event })"
                      >
                        <span
                          class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform"
                          :class="settingsStore.settings.notifications.systemAlerts ? 'translate-x-6' : 'translate-x-1'"
                        />
                      </Switch>
                    </div>

                    <div class="flex items-center justify-between">
                      <div>
                        <p class="font-medium text-gray-900 dark:text-white">Weekly Digest</p>
                        <p class="text-sm text-gray-500 dark:text-gray-400">Weekly summary of activity</p>
                      </div>
                      <Switch
                        :model-value="settingsStore.settings.notifications.weeklyDigest"
                        class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors"
                        :class="settingsStore.settings.notifications.weeklyDigest ? 'bg-aegis-600' : 'bg-gray-300 dark:bg-gray-600'"
                        @update:model-value="settingsStore.updateNotificationSettings({ weeklyDigest: $event })"
                      >
                        <span
                          class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform"
                          :class="settingsStore.settings.notifications.weeklyDigest ? 'translate-x-6' : 'translate-x-1'"
                        />
                      </Switch>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </TabPanel>

          <!-- Privacy Tab -->
          <TabPanel>
            <div class="card space-y-6 p-6">
              <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Privacy Settings</h2>

              <div class="space-y-4">
                <div class="flex items-center justify-between">
                  <div>
                    <p class="font-medium text-gray-900 dark:text-white">Share Usage Data</p>
                    <p class="text-sm text-gray-500 dark:text-gray-400">Help improve AEGIS by sharing anonymous usage data</p>
                  </div>
                  <Switch
                    :model-value="settingsStore.settings.privacy.shareUsageData"
                    class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors"
                    :class="settingsStore.settings.privacy.shareUsageData ? 'bg-aegis-600' : 'bg-gray-300 dark:bg-gray-600'"
                    @update:model-value="settingsStore.updatePrivacySettings({ shareUsageData: $event })"
                  >
                    <span
                      class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform"
                      :class="settingsStore.settings.privacy.shareUsageData ? 'translate-x-6' : 'translate-x-1'"
                    />
                  </Switch>
                </div>

                <div class="flex items-center justify-between">
                  <div>
                    <p class="font-medium text-gray-900 dark:text-white">Show Activity Status</p>
                    <p class="text-sm text-gray-500 dark:text-gray-400">Let others see when you're online</p>
                  </div>
                  <Switch
                    :model-value="settingsStore.settings.privacy.showActivityStatus"
                    class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors"
                    :class="settingsStore.settings.privacy.showActivityStatus ? 'bg-aegis-600' : 'bg-gray-300 dark:bg-gray-600'"
                    @update:model-value="settingsStore.updatePrivacySettings({ showActivityStatus: $event })"
                  >
                    <span
                      class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform"
                      :class="settingsStore.settings.privacy.showActivityStatus ? 'translate-x-6' : 'translate-x-1'"
                    />
                  </Switch>
                </div>

                <div class="flex items-center justify-between">
                  <div>
                    <p class="font-medium text-gray-900 dark:text-white">Allow @Mentions</p>
                    <p class="text-sm text-gray-500 dark:text-gray-400">Let team members mention you in comments</p>
                  </div>
                  <Switch
                    :model-value="settingsStore.settings.privacy.allowMentions"
                    class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors"
                    :class="settingsStore.settings.privacy.allowMentions ? 'bg-aegis-600' : 'bg-gray-300 dark:bg-gray-600'"
                    @update:model-value="settingsStore.updatePrivacySettings({ allowMentions: $event })"
                  >
                    <span
                      class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform"
                      :class="settingsStore.settings.privacy.allowMentions ? 'translate-x-6' : 'translate-x-1'"
                    />
                  </Switch>
                </div>
              </div>
            </div>
          </TabPanel>

          <!-- API Keys Tab -->
          <TabPanel>
            <div class="card space-y-6 p-6">
              <div class="flex items-center justify-between">
                <div>
                  <h2 class="text-lg font-semibold text-gray-900 dark:text-white">API Keys</h2>
                  <p class="text-sm text-gray-500 dark:text-gray-400">Manage your API keys for programmatic access</p>
                </div>
                <button class="btn-primary">
                  Create API Key
                </button>
              </div>

              <div class="rounded-lg border border-gray-200 dark:border-gray-700">
                <div class="p-8 text-center">
                  <KeyIcon class="mx-auto h-12 w-12 text-gray-400" />
                  <h3 class="mt-4 text-lg font-medium text-gray-900 dark:text-white">No API Keys</h3>
                  <p class="mt-2 text-sm text-gray-500 dark:text-gray-400">
                    Create an API key to access AEGIS programmatically
                  </p>
                </div>
              </div>
            </div>
          </TabPanel>
        </TabPanels>
      </TabGroup>

      <!-- Save Button -->
      <div
        v-if="settingsStore.hasUnsavedChanges"
        class="fixed bottom-6 right-6 flex items-center gap-4 rounded-lg bg-white p-4 shadow-lg dark:bg-gray-800"
      >
        <span class="text-sm text-gray-500 dark:text-gray-400">You have unsaved changes</span>
        <button class="btn-ghost" @click="settingsStore.fetchSettings()">
          Discard
        </button>
        <button class="btn-primary" @click="handleSave">
          Save Changes
        </button>
      </div>
    </div>
  </div>
</template>
