<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/zod'
import { z } from 'zod'
import { authService } from '@/services/auth.service'
import { useToast } from '@/composables/useToast'
import {
  ExclamationCircleIcon,
  CheckCircleIcon,
  LockClosedIcon,
  XCircleIcon,
  EyeIcon,
  EyeSlashIcon
} from '@heroicons/vue/24/outline'

const router = useRouter()
const route = useRoute()
const toast = useToast()

const loading = ref(false)
const validating = ref(true)
const tokenValid = ref(false)
const tokenEmail = ref('')
const serverError = ref('')
const resetSuccess = ref(false)
const showPassword = ref(false)
const showConfirmPassword = ref(false)

// Get token from URL
const token = computed(() => route.query.token as string || '')

// Validation schema
const resetPasswordSchema = z.object({
  password: z
    .string()
    .min(1, 'Password is required')
    .min(8, 'Password must be at least 8 characters')
    .regex(/[A-Z]/, 'Password must contain at least one uppercase letter')
    .regex(/[a-z]/, 'Password must contain at least one lowercase letter')
    .regex(/[0-9]/, 'Password must contain at least one number'),
  confirmPassword: z.string().min(1, 'Please confirm your password')
}).refine((data) => data.password === data.confirmPassword, {
  message: 'Passwords do not match',
  path: ['confirmPassword']
})

type ResetPasswordFormData = z.infer<typeof resetPasswordSchema>

// Set up VeeValidate with Zod schema
const { defineField, handleSubmit, errors, meta } = useForm<ResetPasswordFormData>({
  validationSchema: toTypedSchema(resetPasswordSchema),
  initialValues: {
    password: '',
    confirmPassword: ''
  }
})

// Define form fields
const [password] = defineField('password')
const [confirmPassword] = defineField('confirmPassword')

// Track touched fields
const passwordTouched = ref(false)
const confirmPasswordTouched = ref(false)

// Password strength indicator
const passwordStrength = computed(() => {
  if (!password.value) return { score: 0, label: '', color: '' }

  let score = 0
  const pwd = password.value

  if (pwd.length >= 8) score++
  if (pwd.length >= 12) score++
  if (/[A-Z]/.test(pwd)) score++
  if (/[a-z]/.test(pwd)) score++
  if (/[0-9]/.test(pwd)) score++
  if (/[^A-Za-z0-9]/.test(pwd)) score++

  if (score <= 2) return { score, label: 'Weak', color: 'bg-red-500' }
  if (score <= 4) return { score, label: 'Medium', color: 'bg-yellow-500' }
  return { score, label: 'Strong', color: 'bg-green-500' }
})

// Computed for button disabled state
const isSubmitDisabled = computed(() => {
  return loading.value || !meta.value.valid
})

// Validate token on mount
onMounted(async () => {
  if (!token.value) {
    validating.value = false
    tokenValid.value = false
    return
  }

  try {
    const result = await authService.validateResetToken(token.value)
    tokenValid.value = result.valid
    tokenEmail.value = result.email || ''
  } catch {
    tokenValid.value = false
  } finally {
    validating.value = false
  }
})

// Handle form submission
const onSubmit = handleSubmit(async (values) => {
  loading.value = true
  serverError.value = ''

  try {
    await authService.resetPassword({
      token: token.value,
      newPassword: values.password
    })

    resetSuccess.value = true
    toast.success('Password reset!', 'You can now sign in with your new password.')

    // Redirect to login after a short delay
    setTimeout(() => {
      router.push('/login')
    }, 3000)
  } catch (error) {
    serverError.value = error instanceof Error ? error.message : 'Failed to reset password. Please try again.'
    toast.error('Reset failed', serverError.value)
  } finally {
    loading.value = false
  }
})

// Mark fields as touched on blur
function handleBlur(field: 'password' | 'confirmPassword') {
  if (field === 'password') passwordTouched.value = true
  if (field === 'confirmPassword') confirmPasswordTouched.value = true
}
</script>

<template>
  <div class="flex min-h-screen items-center justify-center bg-gradient-to-br from-aegis-50 to-gray-100 px-4 py-12 dark:from-gray-900 dark:to-gray-800">
    <div class="w-full max-w-md">
      <!-- Logo and title -->
      <div class="mb-8 text-center">
        <h1 class="text-4xl font-bold text-aegis-600 dark:text-aegis-400">AEGIS</h1>
        <p class="mt-2 text-gray-600 dark:text-gray-400">
          Agentic Entity & Graph Intelligence System
        </p>
      </div>

      <!-- Reset password card -->
      <div class="card p-8">
        <!-- Loading state -->
        <div v-if="validating" class="text-center py-8">
          <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-aegis-600 mx-auto"></div>
          <p class="mt-4 text-gray-600 dark:text-gray-400">Validating reset link...</p>
        </div>

        <!-- Invalid token state -->
        <div v-else-if="!tokenValid" class="text-center py-4">
          <div class="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-red-100 dark:bg-red-900/50">
            <XCircleIcon class="h-6 w-6 text-red-600 dark:text-red-400" />
          </div>
          <h2 class="mt-4 text-xl font-semibold text-gray-900 dark:text-white">
            Invalid or expired link
          </h2>
          <p class="mt-2 text-gray-600 dark:text-gray-400">
            This password reset link is invalid or has expired.
            Please request a new one.
          </p>

          <div class="mt-6 space-y-3">
            <RouterLink
              to="/forgot-password"
              class="btn-primary w-full py-2.5 inline-block text-center"
            >
              Request new link
            </RouterLink>
            <RouterLink
              to="/login"
              class="btn-ghost w-full py-2.5 inline-block text-center"
            >
              Back to sign in
            </RouterLink>
          </div>
        </div>

        <!-- Success state -->
        <div v-else-if="resetSuccess" class="text-center py-4">
          <div class="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-green-100 dark:bg-green-900/50">
            <CheckCircleIcon class="h-6 w-6 text-green-600 dark:text-green-400" />
          </div>
          <h2 class="mt-4 text-xl font-semibold text-gray-900 dark:text-white">
            Password reset successful!
          </h2>
          <p class="mt-2 text-gray-600 dark:text-gray-400">
            Your password has been reset. Redirecting you to sign in...
          </p>

          <RouterLink
            to="/login"
            class="mt-6 btn-primary py-2.5 inline-block text-center w-full"
          >
            Sign in now
          </RouterLink>
        </div>

        <!-- Form state -->
        <template v-else>
          <div class="mb-6 text-center">
            <div class="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-aegis-100 dark:bg-aegis-900/50">
              <LockClosedIcon class="h-6 w-6 text-aegis-600 dark:text-aegis-400" />
            </div>
            <h2 class="mt-4 text-xl font-semibold text-gray-900 dark:text-white">
              Set new password
            </h2>
            <p v-if="tokenEmail" class="mt-2 text-sm text-gray-600 dark:text-gray-400">
              Enter a new password for <span class="font-medium">{{ tokenEmail }}</span>
            </p>
            <p v-else class="mt-2 text-sm text-gray-600 dark:text-gray-400">
              Enter your new password below.
            </p>
          </div>

          <!-- Server error message -->
          <div
            v-if="serverError"
            class="mb-4 flex items-center gap-2 rounded-lg bg-red-50 p-3 text-sm text-red-600 dark:bg-red-900/30 dark:text-red-400"
            role="alert"
          >
            <ExclamationCircleIcon class="h-5 w-5 shrink-0" />
            {{ serverError }}
          </div>

          <form @submit.prevent="onSubmit" class="space-y-4" novalidate>
            <!-- Password -->
            <div class="space-y-1">
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300">
                New password <span class="text-red-500">*</span>
              </label>
              <div class="relative">
                <input
                  v-model="password"
                  :type="showPassword ? 'text' : 'password'"
                  placeholder="Create a strong password"
                  class="input w-full pr-10"
                  :class="{ 'border-red-500 focus:border-red-500 focus:ring-red-500': errors.password && passwordTouched }"
                  @blur="handleBlur('password')"
                />
                <button
                  type="button"
                  class="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300"
                  @click="showPassword = !showPassword"
                >
                  <EyeSlashIcon v-if="showPassword" class="h-5 w-5" />
                  <EyeIcon v-else class="h-5 w-5" />
                </button>
              </div>

              <!-- Password strength indicator -->
              <div v-if="password" class="mt-2">
                <div class="flex items-center gap-2">
                  <div class="flex-1 h-1.5 bg-gray-200 rounded-full overflow-hidden dark:bg-gray-700">
                    <div
                      class="h-full transition-all duration-300"
                      :class="passwordStrength.color"
                      :style="{ width: `${(passwordStrength.score / 6) * 100}%` }"
                    />
                  </div>
                  <span class="text-xs font-medium" :class="{
                    'text-red-500': passwordStrength.label === 'Weak',
                    'text-yellow-500': passwordStrength.label === 'Medium',
                    'text-green-500': passwordStrength.label === 'Strong'
                  }">
                    {{ passwordStrength.label }}
                  </span>
                </div>
              </div>

              <p v-if="errors.password && passwordTouched" class="text-sm text-red-500">
                {{ errors.password }}
              </p>
              <p v-else class="text-xs text-gray-500 dark:text-gray-400">
                8+ characters, uppercase, lowercase, and number required
              </p>
            </div>

            <!-- Confirm Password -->
            <div class="space-y-1">
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300">
                Confirm password <span class="text-red-500">*</span>
              </label>
              <div class="relative">
                <input
                  v-model="confirmPassword"
                  :type="showConfirmPassword ? 'text' : 'password'"
                  placeholder="Confirm your password"
                  class="input w-full pr-10"
                  :class="{ 'border-red-500 focus:border-red-500 focus:ring-red-500': errors.confirmPassword && confirmPasswordTouched }"
                  @blur="handleBlur('confirmPassword')"
                />
                <button
                  type="button"
                  class="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300"
                  @click="showConfirmPassword = !showConfirmPassword"
                >
                  <EyeSlashIcon v-if="showConfirmPassword" class="h-5 w-5" />
                  <EyeIcon v-else class="h-5 w-5" />
                </button>
              </div>
              <p v-if="errors.confirmPassword && confirmPasswordTouched" class="text-sm text-red-500">
                {{ errors.confirmPassword }}
              </p>
            </div>

            <!-- Submit button -->
            <button
              type="submit"
              :disabled="isSubmitDisabled"
              class="btn-primary w-full py-2.5 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <template v-if="loading">
                <svg class="mr-2 h-4 w-4 animate-spin" viewBox="0 0 24 24">
                  <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" fill="none" />
                  <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
                </svg>
                Resetting password...
              </template>
              <template v-else>
                Reset password
              </template>
            </button>
          </form>

          <!-- Back to login link -->
          <p class="mt-6 text-center text-sm text-gray-600 dark:text-gray-400">
            Remember your password?
            <RouterLink to="/login" class="font-medium text-aegis-600 hover:text-aegis-500 dark:text-aegis-400">
              Sign in
            </RouterLink>
          </p>
        </template>
      </div>
    </div>
  </div>
</template>
