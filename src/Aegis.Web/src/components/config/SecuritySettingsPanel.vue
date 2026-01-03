<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import { Switch } from '@headlessui/vue'
import {
  ShieldCheckIcon,
  KeyIcon,
  LockClosedIcon,
  ExclamationTriangleIcon,
  PlusIcon,
  TrashIcon
} from '@heroicons/vue/24/outline'
import type { SecuritySettings } from '@/types'

const props = defineProps<{
  settings: SecuritySettings | null
  isLoading?: boolean
  isSaving?: boolean
}>()

const emit = defineEmits<{
  update: [settings: Partial<SecuritySettings>]
}>()

const localSettings = ref<SecuritySettings | null>(null)
const newWhitelistIp = ref('')
const newBlacklistIp = ref('')
const newOrigin = ref('')

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

function addWhitelistIp() {
  if (newWhitelistIp.value.trim() && localSettings.value) {
    localSettings.value.ipWhitelist = [...localSettings.value.ipWhitelist, newWhitelistIp.value.trim()]
    newWhitelistIp.value = ''
  }
}

function removeWhitelistIp(ip: string) {
  if (localSettings.value) {
    localSettings.value.ipWhitelist = localSettings.value.ipWhitelist.filter(i => i !== ip)
  }
}

function addBlacklistIp() {
  if (newBlacklistIp.value.trim() && localSettings.value) {
    localSettings.value.ipBlacklist = [...localSettings.value.ipBlacklist, newBlacklistIp.value.trim()]
    newBlacklistIp.value = ''
  }
}

function removeBlacklistIp(ip: string) {
  if (localSettings.value) {
    localSettings.value.ipBlacklist = localSettings.value.ipBlacklist.filter(i => i !== ip)
  }
}

function addOrigin() {
  if (newOrigin.value.trim() && localSettings.value) {
    localSettings.value.allowedOrigins = [...localSettings.value.allowedOrigins, newOrigin.value.trim()]
    newOrigin.value = ''
  }
}

function removeOrigin(origin: string) {
  if (localSettings.value) {
    localSettings.value.allowedOrigins = localSettings.value.allowedOrigins.filter(o => o !== origin)
  }
}

const passwordStrengthLabel = computed(() => {
  if (!localSettings.value) return 'Unknown'
  let score = 0
  if (localSettings.value.passwordMinLength >= 12) score++
  if (localSettings.value.passwordMinLength >= 16) score++
  if (localSettings.value.passwordRequireUppercase) score++
  if (localSettings.value.passwordRequireLowercase) score++
  if (localSettings.value.passwordRequireNumbers) score++
  if (localSettings.value.passwordRequireSpecial) score++

  if (score >= 6) return 'Very Strong'
  if (score >= 4) return 'Strong'
  if (score >= 2) return 'Moderate'
  return 'Weak'
})

const passwordStrengthColor = computed(() => {
  const label = passwordStrengthLabel.value
  if (label === 'Very Strong') return 'text-green-600 dark:text-green-400'
  if (label === 'Strong') return 'text-blue-600 dark:text-blue-400'
  if (label === 'Moderate') return 'text-yellow-600 dark:text-yellow-400'
  return 'text-red-600 dark:text-red-400'
})
</script>

<template>
  <div class="space-y-6">
    <!-- Loading state -->
    <div v-if="isLoading" class="animate-pulse space-y-4">
      <div v-for="i in 4" :key="i" class="h-32 rounded-lg bg-gray-200 dark:bg-gray-700" />
    </div>

    <template v-else-if="localSettings">
      <!-- Password Policy -->
      <div class="rounded-lg border bg-white p-6 dark:border-gray-700 dark:bg-gray-800">
        <div class="mb-4 flex items-center gap-3">
          <div class="flex h-10 w-10 items-center justify-center rounded-lg bg-blue-100 dark:bg-blue-900/30">
            <KeyIcon class="h-5 w-5 text-blue-600 dark:text-blue-400" />
          </div>
          <div class="flex-1">
            <h3 class="font-medium text-gray-900 dark:text-white">Password Policy</h3>
            <p class="text-sm text-gray-500 dark:text-gray-400">Configure password requirements</p>
          </div>
          <span :class="['text-sm font-medium', passwordStrengthColor]">
            {{ passwordStrengthLabel }}
          </span>
        </div>

        <div class="grid gap-4 md:grid-cols-2">
          <div>
            <label class="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
              Minimum Length: {{ localSettings.passwordMinLength }}
            </label>
            <input
              v-model.number="localSettings.passwordMinLength"
              type="range"
              min="8"
              max="32"
              class="w-full"
            />
            <div class="flex justify-between text-xs text-gray-500">
              <span>8</span>
              <span>16</span>
              <span>24</span>
              <span>32</span>
            </div>
          </div>

          <div>
            <label class="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
              Password Expiry: {{ localSettings.passwordExpiryDays }} days
            </label>
            <input
              v-model.number="localSettings.passwordExpiryDays"
              type="range"
              min="0"
              max="365"
              step="30"
              class="w-full"
            />
            <div class="flex justify-between text-xs text-gray-500">
              <span>Never</span>
              <span>90</span>
              <span>180</span>
              <span>365</span>
            </div>
          </div>

          <div class="md:col-span-2">
            <label class="mb-2 block text-sm font-medium text-gray-700 dark:text-gray-300">Requirements</label>
            <div class="flex flex-wrap gap-4">
              <label class="flex items-center gap-2">
                <input
                  v-model="localSettings.passwordRequireUppercase"
                  type="checkbox"
                  class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500"
                />
                <span class="text-sm text-gray-700 dark:text-gray-300">Uppercase letter</span>
              </label>
              <label class="flex items-center gap-2">
                <input
                  v-model="localSettings.passwordRequireLowercase"
                  type="checkbox"
                  class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500"
                />
                <span class="text-sm text-gray-700 dark:text-gray-300">Lowercase letter</span>
              </label>
              <label class="flex items-center gap-2">
                <input
                  v-model="localSettings.passwordRequireNumbers"
                  type="checkbox"
                  class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500"
                />
                <span class="text-sm text-gray-700 dark:text-gray-300">Number</span>
              </label>
              <label class="flex items-center gap-2">
                <input
                  v-model="localSettings.passwordRequireSpecial"
                  type="checkbox"
                  class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500"
                />
                <span class="text-sm text-gray-700 dark:text-gray-300">Special character</span>
              </label>
            </div>
          </div>
        </div>
      </div>

      <!-- Session & Login Security -->
      <div class="rounded-lg border bg-white p-6 dark:border-gray-700 dark:bg-gray-800">
        <div class="mb-4 flex items-center gap-3">
          <div class="flex h-10 w-10 items-center justify-center rounded-lg bg-green-100 dark:bg-green-900/30">
            <LockClosedIcon class="h-5 w-5 text-green-600 dark:text-green-400" />
          </div>
          <div>
            <h3 class="font-medium text-gray-900 dark:text-white">Session & Login Security</h3>
            <p class="text-sm text-gray-500 dark:text-gray-400">Login attempts and session management</p>
          </div>
        </div>

        <div class="grid gap-4 md:grid-cols-2">
          <div>
            <label class="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Max Login Attempts</label>
            <input
              v-model.number="localSettings.maxLoginAttempts"
              type="number"
              min="3"
              max="10"
              class="w-full rounded-lg border-gray-300 text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
            />
          </div>
          <div>
            <label class="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Lockout Duration (minutes)</label>
            <input
              v-model.number="localSettings.lockoutDurationMinutes"
              type="number"
              min="5"
              max="1440"
              class="w-full rounded-lg border-gray-300 text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
            />
          </div>
          <div>
            <label class="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">Session Timeout (minutes)</label>
            <input
              v-model.number="localSettings.sessionTimeoutMinutes"
              type="number"
              min="5"
              max="1440"
              class="w-full rounded-lg border-gray-300 text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
            />
          </div>
        </div>
      </div>

      <!-- MFA Settings -->
      <div class="rounded-lg border bg-white p-6 dark:border-gray-700 dark:bg-gray-800">
        <div class="mb-4 flex items-center gap-3">
          <div class="flex h-10 w-10 items-center justify-center rounded-lg bg-purple-100 dark:bg-purple-900/30">
            <ShieldCheckIcon class="h-5 w-5 text-purple-600 dark:text-purple-400" />
          </div>
          <div class="flex-1">
            <h3 class="font-medium text-gray-900 dark:text-white">Multi-Factor Authentication</h3>
            <p class="text-sm text-gray-500 dark:text-gray-400">Require MFA for all users</p>
          </div>
          <Switch
            v-model="localSettings.mfaRequired"
            :class="[
              localSettings.mfaRequired ? 'bg-aegis-600' : 'bg-gray-200 dark:bg-gray-700',
              'relative inline-flex h-6 w-11 flex-shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-aegis-500 focus:ring-offset-2 dark:focus:ring-offset-gray-800'
            ]"
          >
            <span
              :class="[
                localSettings.mfaRequired ? 'translate-x-5' : 'translate-x-0',
                'pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out'
              ]"
            />
          </Switch>
        </div>

        <div v-if="localSettings.mfaRequired" class="border-t pt-4 dark:border-gray-700">
          <div>
            <label class="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
              Grace Period: {{ localSettings.mfaGracePeriodDays }} days
            </label>
            <input
              v-model.number="localSettings.mfaGracePeriodDays"
              type="range"
              min="0"
              max="30"
              class="w-full"
            />
            <p class="mt-1 text-xs text-gray-500">Days users have to set up MFA after account creation</p>
          </div>
        </div>
      </div>

      <!-- IP Restrictions -->
      <div class="rounded-lg border bg-white p-6 dark:border-gray-700 dark:bg-gray-800">
        <div class="mb-4 flex items-center gap-3">
          <div class="flex h-10 w-10 items-center justify-center rounded-lg bg-orange-100 dark:bg-orange-900/30">
            <ExclamationTriangleIcon class="h-5 w-5 text-orange-600 dark:text-orange-400" />
          </div>
          <div>
            <h3 class="font-medium text-gray-900 dark:text-white">IP Restrictions</h3>
            <p class="text-sm text-gray-500 dark:text-gray-400">Whitelist and blacklist IP addresses</p>
          </div>
        </div>

        <div class="grid gap-6 md:grid-cols-2">
          <!-- Whitelist -->
          <div>
            <label class="mb-2 block text-sm font-medium text-gray-700 dark:text-gray-300">IP Whitelist</label>
            <div class="space-y-2">
              <div
                v-for="ip in localSettings.ipWhitelist"
                :key="ip"
                class="flex items-center justify-between rounded bg-green-50 px-3 py-1.5 dark:bg-green-900/20"
              >
                <code class="text-sm text-green-700 dark:text-green-300">{{ ip }}</code>
                <button
                  type="button"
                  class="text-green-600 hover:text-green-800 dark:text-green-400"
                  @click="removeWhitelistIp(ip)"
                >
                  <TrashIcon class="h-4 w-4" />
                </button>
              </div>
              <div v-if="localSettings.ipWhitelist.length === 0" class="text-sm text-gray-500">
                No IPs whitelisted (all allowed)
              </div>
              <div class="flex gap-2">
                <input
                  v-model="newWhitelistIp"
                  type="text"
                  class="flex-1 rounded-lg border-gray-300 text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                  placeholder="192.168.1.0/24"
                  @keyup.enter="addWhitelistIp"
                />
                <button
                  type="button"
                  class="rounded-lg bg-green-100 p-2 text-green-600 hover:bg-green-200 dark:bg-green-900/30 dark:hover:bg-green-900/50"
                  @click="addWhitelistIp"
                >
                  <PlusIcon class="h-5 w-5" />
                </button>
              </div>
            </div>
          </div>

          <!-- Blacklist -->
          <div>
            <label class="mb-2 block text-sm font-medium text-gray-700 dark:text-gray-300">IP Blacklist</label>
            <div class="space-y-2">
              <div
                v-for="ip in localSettings.ipBlacklist"
                :key="ip"
                class="flex items-center justify-between rounded bg-red-50 px-3 py-1.5 dark:bg-red-900/20"
              >
                <code class="text-sm text-red-700 dark:text-red-300">{{ ip }}</code>
                <button
                  type="button"
                  class="text-red-600 hover:text-red-800 dark:text-red-400"
                  @click="removeBlacklistIp(ip)"
                >
                  <TrashIcon class="h-4 w-4" />
                </button>
              </div>
              <div v-if="localSettings.ipBlacklist.length === 0" class="text-sm text-gray-500">
                No IPs blacklisted
              </div>
              <div class="flex gap-2">
                <input
                  v-model="newBlacklistIp"
                  type="text"
                  class="flex-1 rounded-lg border-gray-300 text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                  placeholder="10.0.0.0/8"
                  @keyup.enter="addBlacklistIp"
                />
                <button
                  type="button"
                  class="rounded-lg bg-red-100 p-2 text-red-600 hover:bg-red-200 dark:bg-red-900/30 dark:hover:bg-red-900/50"
                  @click="addBlacklistIp"
                >
                  <PlusIcon class="h-5 w-5" />
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- CORS Settings -->
      <div class="rounded-lg border bg-white p-6 dark:border-gray-700 dark:bg-gray-800">
        <h3 class="mb-4 font-medium text-gray-900 dark:text-white">Allowed Origins (CORS)</h3>
        <div class="space-y-2">
          <div
            v-for="origin in localSettings.allowedOrigins"
            :key="origin"
            class="flex items-center justify-between rounded bg-gray-50 px-3 py-1.5 dark:bg-gray-700"
          >
            <code class="text-sm text-gray-700 dark:text-gray-300">{{ origin }}</code>
            <button
              type="button"
              class="text-gray-400 hover:text-red-600"
              @click="removeOrigin(origin)"
            >
              <TrashIcon class="h-4 w-4" />
            </button>
          </div>
          <div v-if="localSettings.allowedOrigins.length === 0" class="text-sm text-gray-500">
            No origins configured
          </div>
          <div class="flex gap-2">
            <input
              v-model="newOrigin"
              type="text"
              class="flex-1 rounded-lg border-gray-300 text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
              placeholder="https://example.com"
              @keyup.enter="addOrigin"
            />
            <button
              type="button"
              class="rounded-lg bg-gray-100 p-2 text-gray-600 hover:bg-gray-200 dark:bg-gray-600 dark:text-gray-300 dark:hover:bg-gray-500"
              @click="addOrigin"
            >
              <PlusIcon class="h-5 w-5" />
            </button>
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
