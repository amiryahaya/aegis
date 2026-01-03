<script setup lang="ts">
import { ref, computed } from 'vue'
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/zod'
import { z } from 'zod'
import { authService } from '@/services/auth.service'
import { useToast } from '@/composables/useToast'
import { FormField } from '@/components/form'
import {
  ExclamationCircleIcon,
  CheckCircleIcon,
  EnvelopeIcon,
  ArrowLeftIcon
} from '@heroicons/vue/24/outline'

const toast = useToast()

const loading = ref(false)
const serverError = ref('')
const emailSent = ref(false)
const sentToEmail = ref('')

// Validation schema
const forgotPasswordSchema = z.object({
  email: z
    .string()
    .min(1, 'Email is required')
    .email('Please enter a valid email address')
})

type ForgotPasswordFormData = z.infer<typeof forgotPasswordSchema>

// Set up VeeValidate with Zod schema
const { defineField, handleSubmit, errors, meta, resetForm } = useForm<ForgotPasswordFormData>({
  validationSchema: toTypedSchema(forgotPasswordSchema),
  initialValues: {
    email: ''
  }
})

// Define form fields
const [email] = defineField('email')

// Track touched fields
const emailTouched = ref(false)

// Computed for button disabled state
const isSubmitDisabled = computed(() => {
  return loading.value || !meta.value.valid
})

// Handle form submission
const onSubmit = handleSubmit(async (values) => {
  loading.value = true
  serverError.value = ''

  try {
    await authService.forgotPassword({ email: values.email })

    sentToEmail.value = values.email
    emailSent.value = true
    toast.success('Email sent!', 'Check your inbox for password reset instructions.')
  } catch (error) {
    // Don't reveal if email exists for security
    // Still show success to prevent email enumeration
    sentToEmail.value = values.email
    emailSent.value = true
    toast.success('Email sent!', 'If an account exists, you will receive reset instructions.')
  } finally {
    loading.value = false
  }
})

// Reset to try again
function tryAgain() {
  emailSent.value = false
  sentToEmail.value = ''
  serverError.value = ''
  resetForm()
  emailTouched.value = false
}

// Mark field as touched on blur
function handleBlur() {
  emailTouched.value = true
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

      <!-- Forgot password card -->
      <div class="card p-8">
        <!-- Success state -->
        <div v-if="emailSent" class="text-center py-4">
          <div class="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-green-100 dark:bg-green-900/50">
            <CheckCircleIcon class="h-6 w-6 text-green-600 dark:text-green-400" />
          </div>
          <h2 class="mt-4 text-xl font-semibold text-gray-900 dark:text-white">
            Check your email
          </h2>
          <p class="mt-2 text-gray-600 dark:text-gray-400">
            We've sent password reset instructions to:
          </p>
          <p class="mt-1 font-medium text-gray-900 dark:text-white">
            {{ sentToEmail }}
          </p>
          <p class="mt-4 text-sm text-gray-500 dark:text-gray-400">
            Didn't receive the email? Check your spam folder or
            <button
              class="font-medium text-aegis-600 hover:text-aegis-500 dark:text-aegis-400"
              @click="tryAgain"
            >
              try again
            </button>
          </p>

          <RouterLink
            to="/login"
            class="mt-6 inline-flex items-center gap-2 text-sm font-medium text-aegis-600 hover:text-aegis-500 dark:text-aegis-400"
          >
            <ArrowLeftIcon class="h-4 w-4" />
            Back to sign in
          </RouterLink>
        </div>

        <!-- Form state -->
        <template v-else>
          <div class="mb-6 text-center">
            <div class="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-aegis-100 dark:bg-aegis-900/50">
              <EnvelopeIcon class="h-6 w-6 text-aegis-600 dark:text-aegis-400" />
            </div>
            <h2 class="mt-4 text-xl font-semibold text-gray-900 dark:text-white">
              Forgot your password?
            </h2>
            <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">
              No worries! Enter your email and we'll send you reset instructions.
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
            <!-- Email -->
            <FormField
              v-model="email"
              name="email"
              label="Email address"
              type="email"
              placeholder="you@example.com"
              :error="errors.email"
              :touched="emailTouched"
              :required="true"
              :show-success-icon="true"
              @blur="handleBlur"
            />

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
                Sending...
              </template>
              <template v-else>
                Send reset instructions
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
