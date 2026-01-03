<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/zod'
import { useAuthStore } from '@/stores/auth'
import { useToast } from '@/composables/useToast'
import { z } from 'zod'
import { FormField } from '@/components/form'
import {
  ExclamationCircleIcon,
  CheckCircleIcon,
  EyeIcon,
  EyeSlashIcon
} from '@heroicons/vue/24/outline'

const router = useRouter()
const authStore = useAuthStore()
const toast = useToast()

const loading = ref(false)
const serverError = ref('')
const registrationSuccess = ref(false)
const showPassword = ref(false)
const showConfirmPassword = ref(false)

// Local registration schema
const registerSchema = z.object({
  firstName: z
    .string()
    .min(1, 'First name is required')
    .min(2, 'First name must be at least 2 characters')
    .max(50, 'First name must be less than 50 characters'),
  lastName: z
    .string()
    .min(1, 'Last name is required')
    .min(2, 'Last name must be at least 2 characters')
    .max(50, 'Last name must be less than 50 characters'),
  email: z
    .string()
    .min(1, 'Email is required')
    .email('Please enter a valid email address'),
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

type RegisterFormData = z.infer<typeof registerSchema>

// Set up VeeValidate with Zod schema
const { defineField, handleSubmit, errors, meta } = useForm<RegisterFormData>({
  validationSchema: toTypedSchema(registerSchema),
  initialValues: {
    firstName: '',
    lastName: '',
    email: '',
    password: '',
    confirmPassword: ''
  }
})

// Define form fields
const [firstName] = defineField('firstName')
const [lastName] = defineField('lastName')
const [email] = defineField('email')
const [password] = defineField('password')
const [confirmPassword] = defineField('confirmPassword')

// Track touched fields
const firstNameTouched = ref(false)
const lastNameTouched = ref(false)
const emailTouched = ref(false)
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

// Handle form submission
const onSubmit = handleSubmit(async (values) => {
  loading.value = true
  serverError.value = ''

  try {
    const success = await authStore.register({
      firstName: values.firstName,
      lastName: values.lastName,
      email: values.email,
      password: values.password
    })

    if (success) {
      registrationSuccess.value = true
      toast.success('Account created!', 'You can now sign in with your credentials.')

      // Redirect to login after a short delay
      setTimeout(() => {
        router.push('/login')
      }, 2000)
    } else {
      serverError.value = authStore.error || 'Registration failed. Please try again.'
    }
  } catch (error) {
    serverError.value = error instanceof Error ? error.message : 'An unexpected error occurred.'
    toast.error('Registration failed', serverError.value)
  } finally {
    loading.value = false
  }
})

// Mark fields as touched on blur
function handleBlur(field: 'firstName' | 'lastName' | 'email' | 'password' | 'confirmPassword') {
  switch (field) {
    case 'firstName': firstNameTouched.value = true; break
    case 'lastName': lastNameTouched.value = true; break
    case 'email': emailTouched.value = true; break
    case 'password': passwordTouched.value = true; break
    case 'confirmPassword': confirmPasswordTouched.value = true; break
  }
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

      <!-- Registration card -->
      <div class="card p-8">
        <!-- Success state -->
        <div v-if="registrationSuccess" class="text-center py-8">
          <div class="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-green-100 dark:bg-green-900/50">
            <CheckCircleIcon class="h-6 w-6 text-green-600 dark:text-green-400" />
          </div>
          <h2 class="mt-4 text-xl font-semibold text-gray-900 dark:text-white">
            Account Created!
          </h2>
          <p class="mt-2 text-gray-600 dark:text-gray-400">
            Redirecting you to sign in...
          </p>
        </div>

        <!-- Registration form -->
        <template v-else>
          <h2 class="mb-6 text-center text-xl font-semibold text-gray-900 dark:text-white">
            Create your account
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
            <!-- Name fields -->
            <div class="grid grid-cols-2 gap-4">
              <FormField
                v-model="firstName"
                name="firstName"
                label="First name"
                type="text"
                placeholder="John"
                :error="errors.firstName"
                :touched="firstNameTouched"
                :required="true"
                :show-success-icon="true"
                @blur="handleBlur('firstName')"
              />
              <FormField
                v-model="lastName"
                name="lastName"
                label="Last name"
                type="text"
                placeholder="Doe"
                :error="errors.lastName"
                :touched="lastNameTouched"
                :required="true"
                :show-success-icon="true"
                @blur="handleBlur('lastName')"
              />
            </div>

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
            <div class="space-y-1">
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300">
                Password <span class="text-red-500">*</span>
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

            <!-- Terms -->
            <p class="text-xs text-gray-500 dark:text-gray-400">
              By creating an account, you agree to our
              <a href="#" class="text-aegis-600 hover:underline dark:text-aegis-400">Terms of Service</a>
              and
              <a href="#" class="text-aegis-600 hover:underline dark:text-aegis-400">Privacy Policy</a>.
            </p>

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
                Creating account...
              </template>
              <template v-else>
                Create account
              </template>
            </button>
          </form>

          <!-- Sign in link -->
          <p class="mt-6 text-center text-sm text-gray-600 dark:text-gray-400">
            Already have an account?
            <RouterLink to="/login" class="font-medium text-aegis-600 hover:text-aegis-500 dark:text-aegis-400">
              Sign in
            </RouterLink>
          </p>
        </template>
      </div>
    </div>
  </div>
</template>
