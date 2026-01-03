<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { TabGroup, TabList, Tab, TabPanels, TabPanel } from '@headlessui/vue'
import {
  Cog6ToothIcon,
  ShieldCheckIcon,
  EnvelopeIcon,
  FlagIcon,
  BellIcon,
  ClockIcon
} from '@heroicons/vue/24/outline'
import { useSystemConfigStore } from '@/stores/systemConfig'
import { useAuthStore } from '@/stores/auth'
import GeneralSettingsPanel from '@/components/config/GeneralSettingsPanel.vue'
import SecuritySettingsPanel from '@/components/config/SecuritySettingsPanel.vue'
import EmailTemplatesPanel from '@/components/config/EmailTemplatesPanel.vue'
import FeatureFlagsPanel from '@/components/config/FeatureFlagsPanel.vue'
import NotificationPreferencesPanel from '@/components/config/NotificationPreferencesPanel.vue'
import type { ConfigSection } from '@/types'

const store = useSystemConfigStore()
const authStore = useAuthStore()

const tabs = [
  { id: 'general' as ConfigSection, name: 'General', icon: Cog6ToothIcon },
  { id: 'security' as ConfigSection, name: 'Security', icon: ShieldCheckIcon },
  { id: 'notifications' as ConfigSection, name: 'Notifications', icon: BellIcon },
  { id: 'emailTemplates', name: 'Email Templates', icon: EnvelopeIcon },
  { id: 'featureFlags', name: 'Feature Flags', icon: FlagIcon },
  { id: 'history', name: 'Change History', icon: ClockIcon }
]

const selectedIndex = ref(0)

onMounted(async () => {
  await Promise.all([
    store.fetchSystemConfiguration(),
    store.fetchEmailTemplates(),
    store.fetchFeatureFlags(),
    authStore.user?.id && store.fetchNotificationPreferences(authStore.user.id)
  ])
})

const generalSettings = computed(() => store.systemConfig?.general ?? null)
const securitySettings = computed(() => store.systemConfig?.security ?? null)

async function handleGeneralUpdate(updates: Record<string, unknown>) {
  await store.updateSystemConfiguration('general', updates)
}

async function handleSecurityUpdate(updates: Record<string, unknown>) {
  await store.updateSystemConfiguration('security', updates)
}

async function handleNotificationUpdate(preferences: Record<string, unknown>) {
  await store.updateNotificationPreferences(preferences)
}

async function handleCreateTemplate(request: Parameters<typeof store.createEmailTemplate>[0]) {
  await store.createEmailTemplate(request)
}

async function handleUpdateTemplate(id: string, request: Parameters<typeof store.updateEmailTemplate>[1]) {
  await store.updateEmailTemplate(id, request)
}

async function handleDeleteTemplate(id: string) {
  await store.deleteEmailTemplate(id)
}

async function handleDuplicateTemplate(id: string) {
  await store.duplicateEmailTemplate(id)
}

async function handleCreateFlag(request: Parameters<typeof store.createFeatureFlag>[0]) {
  await store.createFeatureFlag(request)
}

async function handleUpdateFlag(id: string, request: Parameters<typeof store.updateFeatureFlag>[1]) {
  await store.updateFeatureFlag(id, request)
}

async function handleToggleFlag(id: string) {
  await store.toggleFeatureFlag(id)
}

async function handleDeleteFlag(id: string) {
  await store.deleteFeatureFlag(id)
}

function formatDate(dateString: string): string {
  return new Date(dateString).toLocaleString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

function formatValue(value: unknown): string {
  if (value === null || value === undefined) return 'null'
  if (typeof value === 'boolean') return value ? 'true' : 'false'
  if (typeof value === 'object') return JSON.stringify(value)
  return String(value)
}
</script>

<template>
  <div class="min-h-screen bg-gray-50 dark:bg-gray-900">
    <div class="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
      <!-- Header -->
      <div class="mb-8">
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">System Configuration</h1>
        <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Manage system settings, security policies, email templates, and feature flags
        </p>
      </div>

      <!-- Tabs -->
      <TabGroup :selected-index="selectedIndex" @change="selectedIndex = $event">
        <div class="border-b border-gray-200 dark:border-gray-700">
          <TabList class="-mb-px flex space-x-8 overflow-x-auto">
            <Tab
              v-for="tab in tabs"
              :key="tab.id"
              v-slot="{ selected }"
              as="template"
            >
              <button
                :class="[
                  'flex items-center gap-2 whitespace-nowrap border-b-2 px-1 py-4 text-sm font-medium transition-colors',
                  selected
                    ? 'border-aegis-500 text-aegis-600 dark:text-aegis-400'
                    : 'border-transparent text-gray-500 hover:border-gray-300 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-300'
                ]"
              >
                <component :is="tab.icon" class="h-5 w-5" />
                {{ tab.name }}
              </button>
            </Tab>
          </TabList>
        </div>

        <TabPanels class="mt-6">
          <!-- General Settings -->
          <TabPanel>
            <GeneralSettingsPanel
              :settings="generalSettings"
              :is-loading="store.isLoading"
              :is-saving="store.isSaving"
              @update="handleGeneralUpdate"
            />
          </TabPanel>

          <!-- Security Settings -->
          <TabPanel>
            <SecuritySettingsPanel
              :settings="securitySettings"
              :is-loading="store.isLoading"
              :is-saving="store.isSaving"
              @update="handleSecurityUpdate"
            />
          </TabPanel>

          <!-- Notification Preferences -->
          <TabPanel>
            <NotificationPreferencesPanel
              :preferences="store.notificationPreferences"
              :is-loading="store.isLoading"
              :is-saving="store.isSaving"
              @update="handleNotificationUpdate"
            />
          </TabPanel>

          <!-- Email Templates -->
          <TabPanel>
            <EmailTemplatesPanel
              :templates="store.emailTemplates"
              :templates-by-category="store.templatesByCategory"
              :is-loading="store.isLoading"
              :is-saving="store.isSaving"
              @create="handleCreateTemplate"
              @update="handleUpdateTemplate"
              @delete="handleDeleteTemplate"
              @duplicate="handleDuplicateTemplate"
            />
          </TabPanel>

          <!-- Feature Flags -->
          <TabPanel>
            <FeatureFlagsPanel
              :feature-flags="store.featureFlags"
              :is-loading="store.isLoading"
              :is-saving="store.isSaving"
              @create="handleCreateFlag"
              @update="handleUpdateFlag"
              @toggle="handleToggleFlag"
              @delete="handleDeleteFlag"
            />
          </TabPanel>

          <!-- Change History -->
          <TabPanel>
            <div class="space-y-4">
              <div class="flex items-center justify-between">
                <h3 class="text-lg font-medium text-gray-900 dark:text-white">Configuration Changes</h3>
                <span class="text-sm text-gray-500 dark:text-gray-400">
                  {{ store.configHistory.length }} changes recorded
                </span>
              </div>

              <div v-if="store.configHistory.length === 0" class="py-12 text-center">
                <ClockIcon class="mx-auto h-12 w-12 text-gray-400" />
                <h3 class="mt-2 text-sm font-medium text-gray-900 dark:text-white">No changes yet</h3>
                <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
                  Configuration changes will appear here
                </p>
              </div>

              <div v-else class="divide-y rounded-lg border bg-white dark:divide-gray-700 dark:border-gray-700 dark:bg-gray-800">
                <div
                  v-for="change in store.configHistory"
                  :key="change.id"
                  class="p-4"
                >
                  <div class="flex items-start justify-between">
                    <div>
                      <div class="flex items-center gap-2">
                        <span class="rounded bg-gray-100 px-2 py-0.5 text-xs font-medium text-gray-600 dark:bg-gray-700 dark:text-gray-300">
                          {{ change.section }}
                        </span>
                        <span class="font-medium text-gray-900 dark:text-white">{{ change.field }}</span>
                      </div>
                      <div class="mt-2 flex items-center gap-4 text-sm">
                        <div>
                          <span class="text-gray-500 dark:text-gray-400">From:</span>
                          <code class="ml-1 rounded bg-red-50 px-1 text-red-700 dark:bg-red-900/20 dark:text-red-300">
                            {{ formatValue(change.oldValue) }}
                          </code>
                        </div>
                        <div>
                          <span class="text-gray-500 dark:text-gray-400">To:</span>
                          <code class="ml-1 rounded bg-green-50 px-1 text-green-700 dark:bg-green-900/20 dark:text-green-300">
                            {{ formatValue(change.newValue) }}
                          </code>
                        </div>
                      </div>
                      <p v-if="change.reason" class="mt-1 text-sm text-gray-500 dark:text-gray-400">
                        Reason: {{ change.reason }}
                      </p>
                    </div>
                    <div class="text-right text-sm text-gray-500 dark:text-gray-400">
                      <p>{{ change.changedByName }}</p>
                      <p>{{ formatDate(change.changedAt) }}</p>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </TabPanel>
        </TabPanels>
      </TabGroup>
    </div>
  </div>
</template>
