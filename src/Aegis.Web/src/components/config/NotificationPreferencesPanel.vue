<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import { Switch } from '@headlessui/vue'
import {
  EnvelopeIcon,
  BellIcon,
  DevicePhoneMobileIcon,
  ClockIcon,
  MoonIcon
} from '@heroicons/vue/24/outline'
import type { UserNotificationPreferences } from '@/types'

const props = defineProps<{
  preferences: UserNotificationPreferences | null
  isLoading?: boolean
  isSaving?: boolean
}>()

const emit = defineEmits<{
  update: [preferences: Partial<UserNotificationPreferences>]
}>()

// Local state for form
const localPrefs = ref<UserNotificationPreferences | null>(null)

watch(() => props.preferences, (prefs) => {
  if (prefs) {
    localPrefs.value = JSON.parse(JSON.stringify(prefs))
  }
}, { immediate: true, deep: true })

const hasChanges = computed(() => {
  if (!props.preferences || !localPrefs.value) return false
  return JSON.stringify(props.preferences) !== JSON.stringify(localPrefs.value)
})

function handleSave() {
  if (localPrefs.value && hasChanges.value) {
    emit('update', localPrefs.value)
  }
}

function handleReset() {
  if (props.preferences) {
    localPrefs.value = JSON.parse(JSON.stringify(props.preferences))
  }
}

const dayOptions = [
  { value: 0, label: 'Sunday' },
  { value: 1, label: 'Monday' },
  { value: 2, label: 'Tuesday' },
  { value: 3, label: 'Wednesday' },
  { value: 4, label: 'Thursday' },
  { value: 5, label: 'Friday' },
  { value: 6, label: 'Saturday' }
]
</script>

<template>
  <div class="space-y-8">
    <!-- Loading state -->
    <div v-if="isLoading" class="animate-pulse space-y-4">
      <div v-for="i in 4" :key="i" class="h-32 rounded-lg bg-gray-200 dark:bg-gray-700" />
    </div>

    <template v-else-if="localPrefs">
      <!-- Email Notifications -->
      <div class="rounded-lg border bg-white p-6 dark:border-gray-700 dark:bg-gray-800">
        <div class="mb-4 flex items-center gap-3">
          <div class="flex h-10 w-10 items-center justify-center rounded-lg bg-blue-100 dark:bg-blue-900/30">
            <EnvelopeIcon class="h-5 w-5 text-blue-600 dark:text-blue-400" />
          </div>
          <div class="flex-1">
            <h3 class="font-medium text-gray-900 dark:text-white">Email Notifications</h3>
            <p class="text-sm text-gray-500 dark:text-gray-400">Manage email notification preferences</p>
          </div>
          <Switch
            v-model="localPrefs.email.enabled"
            :class="[
              localPrefs.email.enabled ? 'bg-aegis-600' : 'bg-gray-200 dark:bg-gray-700',
              'relative inline-flex h-6 w-11 flex-shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-aegis-500 focus:ring-offset-2 dark:focus:ring-offset-gray-800'
            ]"
          >
            <span
              :class="[
                localPrefs.email.enabled ? 'translate-x-5' : 'translate-x-0',
                'pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out'
              ]"
            />
          </Switch>
        </div>

        <div v-if="localPrefs.email.enabled" class="grid grid-cols-2 gap-4 border-t pt-4 dark:border-gray-700">
          <label class="flex items-center gap-3">
            <input
              v-model="localPrefs.email.queries"
              type="checkbox"
              class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700"
            />
            <span class="text-sm text-gray-700 dark:text-gray-300">Query completions</span>
          </label>
          <label class="flex items-center gap-3">
            <input
              v-model="localPrefs.email.sessions"
              type="checkbox"
              class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700"
            />
            <span class="text-sm text-gray-700 dark:text-gray-300">Session updates</span>
          </label>
          <label class="flex items-center gap-3">
            <input
              v-model="localPrefs.email.documents"
              type="checkbox"
              class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700"
            />
            <span class="text-sm text-gray-700 dark:text-gray-300">Document processing</span>
          </label>
          <label class="flex items-center gap-3">
            <input
              v-model="localPrefs.email.mentions"
              type="checkbox"
              class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700"
            />
            <span class="text-sm text-gray-700 dark:text-gray-300">@Mentions</span>
          </label>
          <label class="flex items-center gap-3">
            <input
              v-model="localPrefs.email.teamUpdates"
              type="checkbox"
              class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700"
            />
            <span class="text-sm text-gray-700 dark:text-gray-300">Team updates</span>
          </label>
          <label class="flex items-center gap-3">
            <input
              v-model="localPrefs.email.securityAlerts"
              type="checkbox"
              class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700"
            />
            <span class="text-sm text-gray-700 dark:text-gray-300">Security alerts</span>
          </label>
          <label class="flex items-center gap-3">
            <input
              v-model="localPrefs.email.weeklyDigest"
              type="checkbox"
              class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700"
            />
            <span class="text-sm text-gray-700 dark:text-gray-300">Weekly digest</span>
          </label>
          <label class="flex items-center gap-3">
            <input
              v-model="localPrefs.email.marketingEmails"
              type="checkbox"
              class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700"
            />
            <span class="text-sm text-gray-700 dark:text-gray-300">Marketing & updates</span>
          </label>
        </div>
      </div>

      <!-- In-App Notifications -->
      <div class="rounded-lg border bg-white p-6 dark:border-gray-700 dark:bg-gray-800">
        <div class="mb-4 flex items-center gap-3">
          <div class="flex h-10 w-10 items-center justify-center rounded-lg bg-green-100 dark:bg-green-900/30">
            <BellIcon class="h-5 w-5 text-green-600 dark:text-green-400" />
          </div>
          <div class="flex-1">
            <h3 class="font-medium text-gray-900 dark:text-white">In-App Notifications</h3>
            <p class="text-sm text-gray-500 dark:text-gray-400">Notifications within the application</p>
          </div>
          <Switch
            v-model="localPrefs.inApp.enabled"
            :class="[
              localPrefs.inApp.enabled ? 'bg-aegis-600' : 'bg-gray-200 dark:bg-gray-700',
              'relative inline-flex h-6 w-11 flex-shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-aegis-500 focus:ring-offset-2 dark:focus:ring-offset-gray-800'
            ]"
          >
            <span
              :class="[
                localPrefs.inApp.enabled ? 'translate-x-5' : 'translate-x-0',
                'pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out'
              ]"
            />
          </Switch>
        </div>

        <div v-if="localPrefs.inApp.enabled" class="space-y-4 border-t pt-4 dark:border-gray-700">
          <div class="grid grid-cols-2 gap-4">
            <label class="flex items-center gap-3">
              <input
                v-model="localPrefs.inApp.queries"
                type="checkbox"
                class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700"
              />
              <span class="text-sm text-gray-700 dark:text-gray-300">Query completions</span>
            </label>
            <label class="flex items-center gap-3">
              <input
                v-model="localPrefs.inApp.sessions"
                type="checkbox"
                class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700"
              />
              <span class="text-sm text-gray-700 dark:text-gray-300">Session updates</span>
            </label>
            <label class="flex items-center gap-3">
              <input
                v-model="localPrefs.inApp.documents"
                type="checkbox"
                class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700"
              />
              <span class="text-sm text-gray-700 dark:text-gray-300">Document processing</span>
            </label>
            <label class="flex items-center gap-3">
              <input
                v-model="localPrefs.inApp.mentions"
                type="checkbox"
                class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700"
              />
              <span class="text-sm text-gray-700 dark:text-gray-300">@Mentions</span>
            </label>
            <label class="flex items-center gap-3">
              <input
                v-model="localPrefs.inApp.teamUpdates"
                type="checkbox"
                class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700"
              />
              <span class="text-sm text-gray-700 dark:text-gray-300">Team updates</span>
            </label>
            <label class="flex items-center gap-3">
              <input
                v-model="localPrefs.inApp.systemAnnouncements"
                type="checkbox"
                class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700"
              />
              <span class="text-sm text-gray-700 dark:text-gray-300">System announcements</span>
            </label>
          </div>

          <div class="flex gap-6 border-t pt-4 dark:border-gray-700">
            <label class="flex items-center gap-3">
              <input
                v-model="localPrefs.inApp.sound"
                type="checkbox"
                class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700"
              />
              <span class="text-sm text-gray-700 dark:text-gray-300">Play sound</span>
            </label>
            <label class="flex items-center gap-3">
              <input
                v-model="localPrefs.inApp.desktop"
                type="checkbox"
                class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700"
              />
              <span class="text-sm text-gray-700 dark:text-gray-300">Desktop notifications</span>
            </label>
          </div>
        </div>
      </div>

      <!-- Push Notifications -->
      <div class="rounded-lg border bg-white p-6 dark:border-gray-700 dark:bg-gray-800">
        <div class="mb-4 flex items-center gap-3">
          <div class="flex h-10 w-10 items-center justify-center rounded-lg bg-purple-100 dark:bg-purple-900/30">
            <DevicePhoneMobileIcon class="h-5 w-5 text-purple-600 dark:text-purple-400" />
          </div>
          <div class="flex-1">
            <h3 class="font-medium text-gray-900 dark:text-white">Push Notifications</h3>
            <p class="text-sm text-gray-500 dark:text-gray-400">Mobile push notifications</p>
          </div>
          <Switch
            v-model="localPrefs.push.enabled"
            :class="[
              localPrefs.push.enabled ? 'bg-aegis-600' : 'bg-gray-200 dark:bg-gray-700',
              'relative inline-flex h-6 w-11 flex-shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-aegis-500 focus:ring-offset-2 dark:focus:ring-offset-gray-800'
            ]"
          >
            <span
              :class="[
                localPrefs.push.enabled ? 'translate-x-5' : 'translate-x-0',
                'pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out'
              ]"
            />
          </Switch>
        </div>

        <div v-if="localPrefs.push.enabled" class="grid grid-cols-2 gap-4 border-t pt-4 dark:border-gray-700">
          <label class="flex items-center gap-3">
            <input
              v-model="localPrefs.push.queries"
              type="checkbox"
              class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700"
            />
            <span class="text-sm text-gray-700 dark:text-gray-300">Query completions</span>
          </label>
          <label class="flex items-center gap-3">
            <input
              v-model="localPrefs.push.mentions"
              type="checkbox"
              class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700"
            />
            <span class="text-sm text-gray-700 dark:text-gray-300">@Mentions</span>
          </label>
          <label class="flex items-center gap-3">
            <input
              v-model="localPrefs.push.securityAlerts"
              type="checkbox"
              class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700"
            />
            <span class="text-sm text-gray-700 dark:text-gray-300">Security alerts</span>
          </label>
          <label class="flex items-center gap-3">
            <input
              v-model="localPrefs.push.urgentOnly"
              type="checkbox"
              class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700"
            />
            <span class="text-sm text-gray-700 dark:text-gray-300">Urgent only</span>
          </label>
        </div>
      </div>

      <!-- Digest Settings -->
      <div class="rounded-lg border bg-white p-6 dark:border-gray-700 dark:bg-gray-800">
        <div class="mb-4 flex items-center gap-3">
          <div class="flex h-10 w-10 items-center justify-center rounded-lg bg-orange-100 dark:bg-orange-900/30">
            <ClockIcon class="h-5 w-5 text-orange-600 dark:text-orange-400" />
          </div>
          <div class="flex-1">
            <h3 class="font-medium text-gray-900 dark:text-white">Email Digest</h3>
            <p class="text-sm text-gray-500 dark:text-gray-400">Periodic summary emails</p>
          </div>
          <Switch
            v-model="localPrefs.digest.enabled"
            :class="[
              localPrefs.digest.enabled ? 'bg-aegis-600' : 'bg-gray-200 dark:bg-gray-700',
              'relative inline-flex h-6 w-11 flex-shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-aegis-500 focus:ring-offset-2 dark:focus:ring-offset-gray-800'
            ]"
          >
            <span
              :class="[
                localPrefs.digest.enabled ? 'translate-x-5' : 'translate-x-0',
                'pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out'
              ]"
            />
          </Switch>
        </div>

        <div v-if="localPrefs.digest.enabled" class="space-y-4 border-t pt-4 dark:border-gray-700">
          <div class="grid grid-cols-3 gap-4">
            <div>
              <label class="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Frequency</label>
              <select
                v-model="localPrefs.digest.frequency"
                class="w-full rounded-lg border-gray-300 text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
              >
                <option value="daily">Daily</option>
                <option value="weekly">Weekly</option>
                <option value="monthly">Monthly</option>
              </select>
            </div>
            <div>
              <label class="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Day</label>
              <select
                v-model="localPrefs.digest.dayOfWeek"
                class="w-full rounded-lg border-gray-300 text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
              >
                <option v-for="day in dayOptions" :key="day.value" :value="day.value">
                  {{ day.label }}
                </option>
              </select>
            </div>
            <div>
              <label class="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Time</label>
              <input
                v-model="localPrefs.digest.timeOfDay"
                type="time"
                class="w-full rounded-lg border-gray-300 text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
              />
            </div>
          </div>

          <div class="flex gap-6">
            <label class="flex items-center gap-3">
              <input
                v-model="localPrefs.digest.includeAnalytics"
                type="checkbox"
                class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700"
              />
              <span class="text-sm text-gray-700 dark:text-gray-300">Include analytics</span>
            </label>
            <label class="flex items-center gap-3">
              <input
                v-model="localPrefs.digest.includeTeamActivity"
                type="checkbox"
                class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700"
              />
              <span class="text-sm text-gray-700 dark:text-gray-300">Include team activity</span>
            </label>
          </div>
        </div>
      </div>

      <!-- Quiet Hours -->
      <div class="rounded-lg border bg-white p-6 dark:border-gray-700 dark:bg-gray-800">
        <div class="mb-4 flex items-center gap-3">
          <div class="flex h-10 w-10 items-center justify-center rounded-lg bg-indigo-100 dark:bg-indigo-900/30">
            <MoonIcon class="h-5 w-5 text-indigo-600 dark:text-indigo-400" />
          </div>
          <div class="flex-1">
            <h3 class="font-medium text-gray-900 dark:text-white">Quiet Hours</h3>
            <p class="text-sm text-gray-500 dark:text-gray-400">Pause notifications during specific times</p>
          </div>
          <Switch
            v-model="localPrefs.quietHours.enabled"
            :class="[
              localPrefs.quietHours.enabled ? 'bg-aegis-600' : 'bg-gray-200 dark:bg-gray-700',
              'relative inline-flex h-6 w-11 flex-shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-aegis-500 focus:ring-offset-2 dark:focus:ring-offset-gray-800'
            ]"
          >
            <span
              :class="[
                localPrefs.quietHours.enabled ? 'translate-x-5' : 'translate-x-0',
                'pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out'
              ]"
            />
          </Switch>
        </div>

        <div v-if="localPrefs.quietHours.enabled" class="space-y-4 border-t pt-4 dark:border-gray-700">
          <div class="grid grid-cols-2 gap-4">
            <div>
              <label class="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Start Time</label>
              <input
                v-model="localPrefs.quietHours.startTime"
                type="time"
                class="w-full rounded-lg border-gray-300 text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
              />
            </div>
            <div>
              <label class="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">End Time</label>
              <input
                v-model="localPrefs.quietHours.endTime"
                type="time"
                class="w-full rounded-lg border-gray-300 text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
              />
            </div>
          </div>

          <div class="flex gap-6">
            <label class="flex items-center gap-3">
              <input
                v-model="localPrefs.quietHours.allowUrgent"
                type="checkbox"
                class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700"
              />
              <span class="text-sm text-gray-700 dark:text-gray-300">Allow urgent notifications</span>
            </label>
            <label class="flex items-center gap-3">
              <input
                v-model="localPrefs.quietHours.weekendsOnly"
                type="checkbox"
                class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700"
              />
              <span class="text-sm text-gray-700 dark:text-gray-300">Weekends only</span>
            </label>
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
