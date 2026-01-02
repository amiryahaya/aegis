<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/zod'
import { useAuthStore } from '@/stores/auth'
import { loginSchema, type LoginFormData } from '@/validation/schemas'
import { FormField, FormCheckbox } from '@/components/form'
import { ExclamationCircleIcon } from '@heroicons/vue/24/outline'

const router = useRouter()
const route = useRoute()
const authStore = useAuthStore()

const loading = ref(false)
const serverError = ref('')

// Set up VeeValidate with Zod schema
const { defineField, handleSubmit, errors, meta } = useForm<LoginFormData>({
  validationSchema: toTypedSchema(loginSchema),
  initialValues: {
    email: '',
    password: '',
    rememberMe: false
  }
})

// Define form fields
const [email] = defineField('email')
const [password] = defineField('password')
const [rememberMe] = defineField('rememberMe')

// Track touched fields
const emailTouched = ref(false)
const passwordTouched = ref(false)

// Computed for button disabled state
const isSubmitDisabled = computed(() => {
  return loading.value || !meta.value.valid
})

// Handle form submission
const onSubmit = handleSubmit(async (values) => {
  loading.value = true
  serverError.value = ''

  const success = await authStore.login({
    email: values.email,
    password: values.password
  })

  loading.value = false

  if (success) {
    const redirect = route.query.redirect as string || '/'
    router.push(redirect)
  } else {
    serverError.value = authStore.error || 'Login failed. Please try again.'
  }
})

// Mark fields as touched on blur
function handleBlur(field: 'email' | 'password') {
  if (field === 'email') emailTouched.value = true
  if (field === 'password') passwordTouched.value = true
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

      <!-- Login card -->
      <div class="card p-8">
        <h2 class="mb-6 text-center text-xl font-semibold text-gray-900 dark:text-white">
          Sign in to your account
        </h2>

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
            @blur="handleBlur('email')"
          />

          <!-- Password -->
          <FormField
            v-model="password"
            name="password"
            label="Password"
            type="password"
            placeholder="••••••••"
            :error="errors.password"
            :touched="passwordTouched"
            :required="true"
            :show-success-icon="true"
            @blur="handleBlur('password')"
          />

          <!-- Remember me -->
          <FormCheckbox
            v-model="rememberMe"
            name="rememberMe"
            label="Remember me"
            description="Stay signed in for 30 days"
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
              Signing in...
            </template>
            <template v-else>
              Sign in
            </template>
          </button>
        </form>

        <!-- Demo credentials hint -->
        <div class="mt-6 rounded-lg bg-gray-50 p-4 dark:bg-gray-700/50">
          <p class="text-xs text-gray-500 dark:text-gray-400">
            <strong>Demo credentials:</strong><br />
            Email: demo@aegis.local<br />
            Password: demo1234
          </p>
        </div>
      </div>
    </div>
  </div>
</template>
