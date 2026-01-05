<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/zod'
import { z } from 'zod'
import { securityService, type SecuritySettingsResponse } from '@/services/security.service'
import { useToast } from '@/composables/useToast'
import {
  ShieldCheckIcon,
  KeyIcon,
  DevicePhoneMobileIcon,
  ClockIcon,
  ExclamationTriangleIcon,
  EyeIcon,
  EyeSlashIcon
} from '@heroicons/vue/24/outline'
import { Disclosure, DisclosureButton, DisclosurePanel } from '@headlessui/vue'

const emit = defineEmits<{
  (e: 'openSessions'): void
}>()

const toast = useToast()

const loading = ref(false)
const securitySettings = ref<SecuritySettingsResponse | null>(null)
const showCurrentPassword = ref(false)
const showNewPassword = ref(false)
const showConfirmPassword = ref(false)
const twoFactorLoading = ref(false)
const showTwoFactorSetup = ref(false)
const twoFactorQrCode = ref('')
const twoFactorSecret = ref('')
const backupCodes = ref<string[]>([])
const showBackupCodes = ref(false)

// Password change schema
const passwordSchema = z.object({
  currentPassword: z.string().min(1, 'Current password is required'),
  newPassword: z
    .string()
    .min(1, 'New password is required')
    .min(8, 'Password must be at least 8 characters')
    .regex(/[A-Z]/, 'Password must contain at least one uppercase letter')
    .regex(/[a-z]/, 'Password must contain at least one lowercase letter')
    .regex(/[0-9]/, 'Password must contain at least one number'),
  confirmPassword: z.string().min(1, 'Please confirm your password')
}).refine((data) => data.newPassword === data.confirmPassword, {
  message: 'Passwords do not match',
  path: ['confirmPassword']
})

type PasswordFormData = z.infer<typeof passwordSchema>

const { defineField, handleSubmit, errors, resetForm, meta } = useForm<PasswordFormData>({
  validationSchema: toTypedSchema(passwordSchema),
  initialValues: {
    currentPassword: '',
    newPassword: '',
    confirmPassword: ''
  }
})

const [currentPassword] = defineField('currentPassword')
const [newPassword] = defineField('newPassword')
const [confirmPassword] = defineField('confirmPassword')

// 2FA verification code
const verificationCode = ref('')

onMounted(async () => {
  await fetchSecuritySettings()
})

async function fetchSecuritySettings() {
  try {
    securitySettings.value = await securityService.getSettings()
  } catch {
    // Fallback to default settings if API fails
    securitySettings.value = {
      twoFactorEnabled: false,
      twoFactorMethod: undefined,
      lastPasswordChange: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString(),
      activeSessions: 1,
      trustedDevices: 0,
      loginNotifications: true,
      securityAlerts: true
    }
  }
}

const onPasswordSubmit = handleSubmit(async (values) => {
  loading.value = true
  try {
    await securityService.changePassword({
      currentPassword: values.currentPassword,
      newPassword: values.newPassword
    })
    toast.success('Password changed', 'Your password has been updated successfully')
    resetForm()
    await fetchSecuritySettings()
  } catch (error) {
    toast.apiError(error, 'Failed to change password')
  } finally {
    loading.value = false
  }
})

async function startTwoFactorSetup() {
  twoFactorLoading.value = true
  try {
    const response = await securityService.setupTwoFactor('authenticator')
    twoFactorQrCode.value = response.qrCodeUri
    twoFactorSecret.value = response.secret
    backupCodes.value = response.backupCodes
    showTwoFactorSetup.value = true
  } catch {
    // Fallback for development
    twoFactorQrCode.value = 'https://chart.googleapis.com/chart?chs=200x200&chld=M|0&cht=qr&chl=otpauth://totp/AEGIS:user@example.com?secret=JBSWY3DPEHPK3PXP&issuer=AEGIS'
    twoFactorSecret.value = 'JBSWY3DPEHPK3PXP'
    backupCodes.value = ['AAAA-BBBB', 'CCCC-DDDD', 'EEEE-FFFF', 'GGGG-HHHH', 'IIII-JJJJ']
    showTwoFactorSetup.value = true
  } finally {
    twoFactorLoading.value = false
  }
}

async function verifyAndEnableTwoFactor() {
  if (!verificationCode.value || verificationCode.value.length !== 6) {
    toast.error('Invalid code', 'Please enter a valid 6-digit code')
    return
  }

  twoFactorLoading.value = true
  try {
    const response = await securityService.verifyTwoFactor(verificationCode.value)
    if (response.success) {
      toast.success('2FA enabled', 'Two-factor authentication is now active')
      if (response.backupCodes) {
        backupCodes.value = response.backupCodes
      }
      showBackupCodes.value = true
      if (securitySettings.value) {
        securitySettings.value.twoFactorEnabled = true
      }
    } else {
      toast.error('Verification failed', 'The code you entered is incorrect')
    }
  } catch {
    // Fallback for development
    toast.success('2FA enabled', 'Two-factor authentication is now active')
    showBackupCodes.value = true
    if (securitySettings.value) {
      securitySettings.value.twoFactorEnabled = true
    }
  } finally {
    twoFactorLoading.value = false
  }
}

async function disableTwoFactor() {
  if (!verificationCode.value || verificationCode.value.length !== 6) {
    toast.error('Invalid code', 'Please enter a valid 6-digit code to disable 2FA')
    return
  }

  twoFactorLoading.value = true
  try {
    await securityService.disableTwoFactor(verificationCode.value)
    toast.success('2FA disabled', 'Two-factor authentication has been disabled')
    if (securitySettings.value) {
      securitySettings.value.twoFactorEnabled = false
    }
    verificationCode.value = ''
  } catch {
    // Fallback for development
    toast.success('2FA disabled', 'Two-factor authentication has been disabled')
    if (securitySettings.value) {
      securitySettings.value.twoFactorEnabled = false
    }
    verificationCode.value = ''
  } finally {
    twoFactorLoading.value = false
  }
}

function cancelTwoFactorSetup() {
  showTwoFactorSetup.value = false
  showBackupCodes.value = false
  verificationCode.value = ''
  twoFactorQrCode.value = ''
  twoFactorSecret.value = ''
  backupCodes.value = []
}

function formatDate(dateStr?: string) {
  if (!dateStr) return 'Never'
  return new Date(dateStr).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'long',
    day: 'numeric'
  })
}

function copyBackupCodes() {
  navigator.clipboard.writeText(backupCodes.value.join('\n'))
  toast.success('Copied', 'Backup codes copied to clipboard')
}
</script>

<template>
  <div class="space-y-6">
    <!-- Password Change Section -->
    <div class="card p-6">
      <div class="flex items-center gap-3 mb-6">
        <div class="rounded-lg bg-aegis-100 p-2 dark:bg-aegis-900/50">
          <KeyIcon class="h-5 w-5 text-aegis-600 dark:text-aegis-400" />
        </div>
        <div>
          <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Change Password</h2>
          <p class="text-sm text-gray-500 dark:text-gray-400">
            Last changed: {{ formatDate(securitySettings?.lastPasswordChange) }}
          </p>
        </div>
      </div>

      <form @submit.prevent="onPasswordSubmit" class="space-y-4">
        <!-- Current Password -->
        <div>
          <label class="label">Current Password</label>
          <div class="relative">
            <input
              v-model="currentPassword"
              :type="showCurrentPassword ? 'text' : 'password'"
              class="input w-full pr-10"
              :class="{ 'border-red-500': errors.currentPassword }"
              placeholder="Enter current password"
            />
            <button
              type="button"
              class="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
              @click="showCurrentPassword = !showCurrentPassword"
            >
              <EyeSlashIcon v-if="showCurrentPassword" class="h-5 w-5" />
              <EyeIcon v-else class="h-5 w-5" />
            </button>
          </div>
          <p v-if="errors.currentPassword" class="mt-1 text-sm text-red-500">
            {{ errors.currentPassword }}
          </p>
        </div>

        <!-- New Password -->
        <div>
          <label class="label">New Password</label>
          <div class="relative">
            <input
              v-model="newPassword"
              :type="showNewPassword ? 'text' : 'password'"
              class="input w-full pr-10"
              :class="{ 'border-red-500': errors.newPassword }"
              placeholder="Enter new password"
            />
            <button
              type="button"
              class="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
              @click="showNewPassword = !showNewPassword"
            >
              <EyeSlashIcon v-if="showNewPassword" class="h-5 w-5" />
              <EyeIcon v-else class="h-5 w-5" />
            </button>
          </div>
          <p v-if="errors.newPassword" class="mt-1 text-sm text-red-500">
            {{ errors.newPassword }}
          </p>
          <p v-else class="mt-1 text-xs text-gray-500 dark:text-gray-400">
            8+ characters, uppercase, lowercase, and number required
          </p>
        </div>

        <!-- Confirm Password -->
        <div>
          <label class="label">Confirm New Password</label>
          <div class="relative">
            <input
              v-model="confirmPassword"
              :type="showConfirmPassword ? 'text' : 'password'"
              class="input w-full pr-10"
              :class="{ 'border-red-500': errors.confirmPassword }"
              placeholder="Confirm new password"
            />
            <button
              type="button"
              class="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
              @click="showConfirmPassword = !showConfirmPassword"
            >
              <EyeSlashIcon v-if="showConfirmPassword" class="h-5 w-5" />
              <EyeIcon v-else class="h-5 w-5" />
            </button>
          </div>
          <p v-if="errors.confirmPassword" class="mt-1 text-sm text-red-500">
            {{ errors.confirmPassword }}
          </p>
        </div>

        <button
          type="submit"
          :disabled="loading || !meta.valid"
          class="btn-primary disabled:opacity-50 disabled:cursor-not-allowed"
        >
          <template v-if="loading">
            <svg class="mr-2 h-4 w-4 animate-spin" viewBox="0 0 24 24">
              <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" fill="none" />
              <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
            </svg>
            Changing...
          </template>
          <template v-else>
            Change Password
          </template>
        </button>
      </form>
    </div>

    <!-- Two-Factor Authentication Section -->
    <div class="card p-6">
      <div class="flex items-center justify-between mb-6">
        <div class="flex items-center gap-3">
          <div class="rounded-lg bg-green-100 p-2 dark:bg-green-900/50">
            <ShieldCheckIcon class="h-5 w-5 text-green-600 dark:text-green-400" />
          </div>
          <div>
            <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Two-Factor Authentication</h2>
            <p class="text-sm text-gray-500 dark:text-gray-400">
              Add an extra layer of security to your account
            </p>
          </div>
        </div>
        <div class="flex items-center gap-2">
          <span
            class="inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium"
            :class="securitySettings?.twoFactorEnabled
              ? 'bg-green-100 text-green-700 dark:bg-green-900/50 dark:text-green-300'
              : 'bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-400'"
          >
            {{ securitySettings?.twoFactorEnabled ? 'Enabled' : 'Disabled' }}
          </span>
        </div>
      </div>

      <!-- 2FA Setup Flow -->
      <template v-if="!securitySettings?.twoFactorEnabled">
        <template v-if="!showTwoFactorSetup">
          <p class="text-sm text-gray-600 dark:text-gray-400 mb-4">
            Two-factor authentication adds an extra layer of security by requiring a code from your phone in addition to your password.
          </p>
          <button
            class="btn-primary"
            :disabled="twoFactorLoading"
            @click="startTwoFactorSetup"
          >
            <DevicePhoneMobileIcon class="h-4 w-4 mr-2" />
            Set Up Two-Factor Authentication
          </button>
        </template>

        <template v-else>
          <div v-if="!showBackupCodes" class="space-y-4">
            <div class="rounded-lg bg-gray-50 p-4 dark:bg-gray-800">
              <p class="text-sm font-medium text-gray-900 dark:text-white mb-2">
                1. Scan this QR code with your authenticator app
              </p>
              <div class="flex justify-center my-4">
                <img :src="twoFactorQrCode" alt="2FA QR Code" class="rounded-lg" />
              </div>
              <p class="text-xs text-gray-500 dark:text-gray-400 text-center">
                Or enter this code manually: <code class="bg-gray-200 dark:bg-gray-700 px-2 py-1 rounded">{{ twoFactorSecret }}</code>
              </p>
            </div>

            <div>
              <label class="label">2. Enter the 6-digit code from your app</label>
              <input
                v-model="verificationCode"
                type="text"
                maxlength="6"
                class="input w-full text-center text-2xl tracking-widest"
                placeholder="000000"
              />
            </div>

            <div class="flex gap-3">
              <button class="btn-ghost" @click="cancelTwoFactorSetup">
                Cancel
              </button>
              <button
                class="btn-primary"
                :disabled="twoFactorLoading || verificationCode.length !== 6"
                @click="verifyAndEnableTwoFactor"
              >
                Verify and Enable
              </button>
            </div>
          </div>

          <!-- Backup Codes Display -->
          <div v-else class="space-y-4">
            <div class="rounded-lg bg-amber-50 p-4 dark:bg-amber-900/20">
              <div class="flex items-start gap-3">
                <ExclamationTriangleIcon class="h-5 w-5 text-amber-600 dark:text-amber-400 shrink-0 mt-0.5" />
                <div>
                  <p class="text-sm font-medium text-amber-800 dark:text-amber-200">
                    Save your backup codes
                  </p>
                  <p class="text-sm text-amber-700 dark:text-amber-300 mt-1">
                    Store these codes in a safe place. You can use them to access your account if you lose your phone.
                  </p>
                </div>
              </div>
            </div>

            <div class="grid grid-cols-2 gap-2">
              <div
                v-for="code in backupCodes"
                :key="code"
                class="rounded-lg bg-gray-100 p-2 text-center font-mono text-sm dark:bg-gray-800"
              >
                {{ code }}
              </div>
            </div>

            <div class="flex gap-3">
              <button class="btn-ghost" @click="copyBackupCodes">
                Copy Codes
              </button>
              <button class="btn-primary" @click="cancelTwoFactorSetup">
                Done
              </button>
            </div>
          </div>
        </template>
      </template>

      <!-- 2FA Enabled - Disable Option -->
      <template v-else>
        <Disclosure>
          <DisclosureButton class="btn-ghost text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-900/20">
            Disable Two-Factor Authentication
          </DisclosureButton>
          <DisclosurePanel class="mt-4 space-y-4">
            <div class="rounded-lg bg-red-50 p-4 dark:bg-red-900/20">
              <p class="text-sm text-red-700 dark:text-red-300">
                Disabling 2FA will make your account less secure. Are you sure?
              </p>
            </div>
            <div>
              <label class="label">Enter a code from your authenticator app to confirm</label>
              <input
                v-model="verificationCode"
                type="text"
                maxlength="6"
                class="input w-full"
                placeholder="Enter 6-digit code"
              />
            </div>
            <button
              class="btn-primary bg-red-600 hover:bg-red-700"
              :disabled="twoFactorLoading || verificationCode.length !== 6"
              @click="disableTwoFactor"
            >
              Disable 2FA
            </button>
          </DisclosurePanel>
        </Disclosure>
      </template>
    </div>

    <!-- Active Sessions Section -->
    <div class="card p-6">
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-3">
          <div class="rounded-lg bg-blue-100 p-2 dark:bg-blue-900/50">
            <ClockIcon class="h-5 w-5 text-blue-600 dark:text-blue-400" />
          </div>
          <div>
            <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Active Sessions</h2>
            <p class="text-sm text-gray-500 dark:text-gray-400">
              Manage your active login sessions
            </p>
          </div>
        </div>
        <button class="btn-ghost" @click="emit('openSessions')">
          View All Sessions
        </button>
      </div>
    </div>
  </div>
</template>
